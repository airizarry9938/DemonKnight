#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using ArthurianAutobattler;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

/// <summary>
/// Keeps Architect Resources profiles + artwork in player builds.
/// tip: battle-good-evil-sprite-pools
///
/// Profiles (StageLayout, ClassSpritePool, SceneBackground, LegendPewPortrait, BattleGoodEvil) live under
/// Assets/Resources/ and must ship with the build. Ensure/Load rematerialize from path
/// backups via Resources.Load / StreamingAssets — never Editor-only wipe at runtime.
/// </summary>
public class ArthurianArchitectBuildPersist : IPreprocessBuildWithReport
{
    public const string TipId = "battle-good-evil-sprite-pools";
    public const string ArchitectArtRoot = "Assets/Resources/ArchitectArt";
    public const string StreamingRel = "Assets/StreamingAssets/DemonKnightArchitect";
    public const string ManifestRel = "Assets/StreamingAssets/DemonKnightArchitect/BUILD_INCLUDE.txt";

    public int callbackOrder => 50;

    [MenuItem("Arthurian/Prepare Architect Art for Player Build", false, 52)]
    public static void PrepareMenu()
    {
        string report = PrepareForPlayerBuild(forceCopyStreaming: true);
        EditorUtility.DisplayDialog(
            "Architect Build Persist",
            report + "\n\nInclude these in File → Build Settings / Build:\n"
            + "• Assets/Resources/*.asset profiles\n"
            + "• Assets/Resources/ArchitectArt/**\n"
            + "• Assets/StreamingAssets/DemonKnightArchitect/** (optional fallback)\n"
            + "• Referenced sprite textures (or their ArchitectArt copies)",
            "OK");
    }

    public void OnPreprocessBuild(BuildReport report)
    {
        try
        {
            string msg = PrepareForPlayerBuild(forceCopyStreaming: true);
            Debug.Log("[DemonKnight] Architect build persist (" + TipId + "):\n" + msg);
        }
        catch (Exception ex)
        {
            // Soft-fail — never wipe profiles / never abort build for missing optional art.
            Debug.LogWarning("[DemonKnight] Architect build persist soft-fail: " + ex.Message);
        }
    }

    /// <summary>
    /// Ensure profile assets exist, rematerialize path backups, promote sprites into
    /// Resources/ArchitectArt, mirror files into StreamingAssets. Never deletes profile YAML.
    /// </summary>
    public static string PrepareForPlayerBuild(bool forceCopyStreaming)
    {
        var log = new StringBuilder();
        log.AppendLine("tip=" + TipId);

        EnsureFolder("Assets/Resources");
        EnsureFolder(ArchitectArtRoot);
        EnsureFolder(ArchitectArtRoot + "/Body");
        EnsureFolder(ArchitectArtRoot + "/Face");
        EnsureFolder(ArchitectArtRoot + "/Stage");
        EnsureFolder(ArchitectArtRoot + "/SceneBg");
        EnsureFolder(ArchitectArtRoot + "/Battle");
        EnsureFolder("Assets/StreamingAssets");
        EnsureFolder(StreamingRel);

        var stage = ArthurianStageLayoutProfileEnsure.EnsureAsset();
        var scene = ArthurianStageLayoutProfileEnsure.EnsureSceneBackgroundAsset();
        var body = ArthurianStageLayoutProfileEnsure.EnsureClassSpritePoolAsset();
        var face = ArthurianStageLayoutProfileEnsure.EnsureLegendPewPortraitAsset();
        var battle = ArthurianStageLayoutProfileEnsure.EnsureBattleGoodEvilSpritePoolAsset();

        int promoted = 0;
        promoted += PromoteStageLayoutIntoResources(stage);
        promoted += PromoteSceneBackgroundIntoResources(scene);
        promoted += PromoteClassPoolIntoResources(body);
        promoted += PromoteLegendPewIntoResources(face);
        promoted += PromoteBattleGoodEvilIntoResources(battle);

        // Rematerialize after promote (Editor + runtime-safe paths).
        stage?.RematerializeSpritesFromPaths();
        body?.RematerializeSpritesFromPaths();
        face?.RematerializeSpritesFromPaths();
        battle?.RematerializeSpritesFromPaths();

        if (forceCopyStreaming)
            CopyReferencedTexturesToStreaming(stage, scene, body, face, battle, log);

        WriteManifest(stage, scene, body, face, battle, promoted, log);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        log.AppendLine(
            $"Profiles: Stage={(stage != null)} SceneBg={(scene != null)} "
            + $"ClassPool={(body != null)} LegendPew={(face != null)} "
            + $"Battle={(battle != null)}; promotedSprites≈{promoted}");
        log.AppendLine("NO Editor wipe of Resources profiles during build preprocess.");
        return log.ToString().TrimEnd();
    }

    public static int PromoteClassPoolIntoResources(ClassSpritePoolProfile p)
    {
        if (p == null) return 0;
        p.EnsureLists();
        int n = 0;
        n += PromoteList(p.peasantSprites, p.peasantAssetPaths, "Body/Peasant");
        n += PromoteList(p.warriorSprites, p.warriorAssetPaths, "Body/Warrior");
        n += PromoteList(p.knightSprites, p.knightAssetPaths, "Body/Knight");
        EditorUtility.SetDirty(p);
        return n;
    }

    public static int PromoteLegendPewIntoResources(LegendPewPortraitProfile p)
    {
        if (p == null) return 0;
        p.EnsureSlotsAndLists();
        int n = 0;
        n += PromoteList(p.peasantFaceProfiles, p.peasantFaceAssetPaths, "Face/Peasant");
        n += PromoteList(p.warriorFaceProfiles, p.warriorFaceAssetPaths, "Face/Warrior");
        n += PromoteList(p.knightFaceProfiles, p.knightFaceAssetPaths, "Face/Knight");
        if (p.legendPortraits != null)
        {
            for (int i = 0; i < p.legendPortraits.Count; i++)
            {
                var e = p.legendPortraits[i];
                if (e == null || e.facePortrait == null) continue;
                Sprite promoted = PromoteSprite(e.facePortrait, "Face/Legend/" + e.unitType);
                if (promoted != null && promoted != e.facePortrait)
                {
                    e.facePortrait = promoted;
                    n++;
                }
                string ap = AssetDatabase.GetAssetPath(e.facePortrait);
                if (!string.IsNullOrEmpty(ap)) e.assetPath = ap;
            }
        }
        EditorUtility.SetDirty(p);
        return n;
    }

    public static int PromoteBattleGoodEvilIntoResources(BattleGoodEvilSpritePoolProfile p)
    {
        if (p == null) return 0;
        p.EnsureSlotsAndLists();
        int n = 0;
        n += PromoteList(p.peasantBattleSprites, p.peasantBattleAssetPaths, "Battle/Good/Peasant");
        n += PromoteList(p.warriorBattleSprites, p.warriorBattleAssetPaths, "Battle/Good/Warrior");
        n += PromoteList(p.knightBattleSprites, p.knightBattleAssetPaths, "Battle/Good/Knight");
        n += PromoteList(p.priestBattleSprites, p.priestBattleAssetPaths, "Battle/Good/Priest");
        n += PromoteList(p.demonImpBattleSprites, p.demonImpBattleAssetPaths, "Battle/Evil/Imp");
        n += PromoteList(p.hellhoundBattleSprites, p.hellhoundBattleAssetPaths, "Battle/Evil/Hellhound");
        n += PromoteList(p.dreadLordBattleSprites, p.dreadLordBattleAssetPaths, "Battle/Evil/DreadLord");
        if (p.goodLegendSlots != null)
        {
            for (int i = 0; i < p.goodLegendSlots.Count; i++)
            {
                var e = p.goodLegendSlots[i];
                if (e == null || e.battleSprite == null) continue;
                Sprite promoted = PromoteSprite(e.battleSprite, "Battle/Good/Legend/" + e.unitType);
                if (promoted != null && promoted != e.battleSprite)
                {
                    e.battleSprite = promoted;
                    n++;
                }
                string ap = AssetDatabase.GetAssetPath(e.battleSprite);
                if (!string.IsNullOrEmpty(ap)) e.assetPath = ap;
            }
        }
        if (p.evilBossSlots != null)
        {
            for (int i = 0; i < p.evilBossSlots.Count; i++)
            {
                var e = p.evilBossSlots[i];
                if (e == null || e.battleSprite == null) continue;
                Sprite promoted = PromoteSprite(e.battleSprite, "Battle/Evil/Boss/" + e.unitType);
                if (promoted != null && promoted != e.battleSprite)
                {
                    e.battleSprite = promoted;
                    n++;
                }
                string ap = AssetDatabase.GetAssetPath(e.battleSprite);
                if (!string.IsNullOrEmpty(ap)) e.assetPath = ap;
            }
        }
        EditorUtility.SetDirty(p);
        return n;
    }

    public static int PromoteStageLayoutIntoResources(StageLayoutProfile p)
    {
        if (p == null) return 0;
        p.EnsureSlotCounts();
        int n = 0;
        n += PromoteSlots(p.churchPewSlots, "Stage/Church");
        n += PromoteSlots(p.playerBattleSlots, "Stage/Player");
        n += PromoteSlots(p.enemyBattleSlots, "Stage/Enemy");
        EditorUtility.SetDirty(p);
        return n;
    }

    public static int PromoteSceneBackgroundIntoResources(SceneBackgroundProfile p)
    {
        if (p == null) return 0;
        p.EnsureDefaultEntries();
        int n = 0;
        if (p.entries == null) return 0;
        for (int i = 0; i < p.entries.Count; i++)
        {
            var e = p.entries[i];
            if (e == null) continue;
            if (e.imageSprite != null)
            {
                Sprite promoted = PromoteSprite(e.imageSprite, "SceneBg/" + e.slot);
                if (promoted != null && promoted != e.imageSprite)
                {
                    e.imageSprite = promoted;
                    n++;
                }
                string ap = AssetDatabase.GetAssetPath(e.imageSprite);
                if (!string.IsNullOrEmpty(ap)) e.imageAssetPath = ap;
            }
        }
        EditorUtility.SetDirty(p);
        return n;
    }

    static int PromoteSlots(List<SlotTransformConfig> slots, string sub)
    {
        if (slots == null) return 0;
        int n = 0;
        for (int i = 0; i < slots.Count; i++)
        {
            var s = slots[i];
            if (s == null || s.previewOrOverrideSprite == null) continue;
            Sprite promoted = PromoteSprite(s.previewOrOverrideSprite, sub + "/Seat" + i);
            if (promoted != null && promoted != s.previewOrOverrideSprite)
            {
                s.previewOrOverrideSprite = promoted;
                n++;
            }
            string ap = AssetDatabase.GetAssetPath(s.previewOrOverrideSprite);
            if (!string.IsNullOrEmpty(ap)) s.previewAssetPath = ap;
        }
        return n;
    }

    static int PromoteList(List<Sprite> sprites, List<string> paths, string sub)
    {
        if (sprites == null) return 0;
        paths ??= new List<string>();
        while (paths.Count < sprites.Count) paths.Add("");
        int n = 0;
        for (int i = 0; i < sprites.Count; i++)
        {
            if (sprites[i] == null) continue;
            Sprite promoted = PromoteSprite(sprites[i], sub + "/" + i);
            if (promoted != null && promoted != sprites[i])
            {
                sprites[i] = promoted;
                n++;
            }
            string ap = AssetDatabase.GetAssetPath(sprites[i]);
            if (!string.IsNullOrEmpty(ap)) paths[i] = ap;
        }
        return n;
    }

    /// <summary>
    /// Copy sprite texture into Resources/ArchitectArt if it is not already under Resources.
    /// Returns the Resources sprite (or original when already in Resources / copy fails).
    /// Soft-fail — never clears the source assignment.
    /// </summary>
    public static Sprite PromoteSprite(Sprite sp, string logicalSubPath)
    {
        if (sp == null) return null;
        string src = AssetDatabase.GetAssetPath(sp);
        if (string.IsNullOrEmpty(src)) return sp;
        string norm = src.Replace('\\', '/');
        if (norm.IndexOf("/Resources/", StringComparison.OrdinalIgnoreCase) >= 0)
            return sp; // already build-safe

        try
        {
            string ext = Path.GetExtension(norm);
            if (string.IsNullOrEmpty(ext)) ext = ".png";
            string safe = Sanitize(logicalSubPath) + ext;
            string destFolder = ArchitectArtRoot + "/" + Path.GetDirectoryName(logicalSubPath)!.Replace('\\', '/');
            EnsureFolder(destFolder);
            string dest = destFolder + "/" + Path.GetFileName(safe);
            // Avoid clobbering distinct sprites that share a name — include guid suffix.
            string guid = AssetDatabase.AssetPathToGUID(norm);
            if (!string.IsNullOrEmpty(guid) && guid.Length >= 8)
            {
                string baseName = Path.GetFileNameWithoutExtension(safe);
                dest = destFolder + "/" + baseName + "_" + guid.Substring(0, 8) + ext;
            }

            if (!File.Exists(DestFull(dest)))
            {
                if (!AssetDatabase.CopyAsset(norm, dest))
                {
                    Debug.LogWarning("[DemonKnight] PromoteSprite CopyAsset soft-fail: " + norm + " → " + dest);
                    return sp;
                }
            }

            AssetDatabase.ImportAsset(dest);
            var copy = AssetDatabase.LoadAssetAtPath<Sprite>(dest);
            if (copy == null)
            {
                var objs = AssetDatabase.LoadAllAssetsAtPath(dest);
                if (objs != null)
                {
                    for (int i = 0; i < objs.Length; i++)
                        if (objs[i] is Sprite s) { copy = s; break; }
                }
            }
            return copy != null ? copy : sp;
        }
        catch (Exception ex)
        {
            Debug.LogWarning("[DemonKnight] PromoteSprite soft-fail: " + ex.Message);
            return sp;
        }
    }

    static void CopyReferencedTexturesToStreaming(
        StageLayoutProfile stage,
        SceneBackgroundProfile scene,
        ClassSpritePoolProfile body,
        LegendPewPortraitProfile face,
        BattleGoodEvilSpritePoolProfile battle,
        StringBuilder log)
    {
        var paths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        CollectPaths(body?.peasantAssetPaths, paths);
        CollectPaths(body?.warriorAssetPaths, paths);
        CollectPaths(body?.knightAssetPaths, paths);
        CollectPaths(face?.peasantFaceAssetPaths, paths);
        CollectPaths(face?.warriorFaceAssetPaths, paths);
        CollectPaths(face?.knightFaceAssetPaths, paths);
        if (face?.legendPortraits != null)
        {
            foreach (var e in face.legendPortraits)
                if (e != null && !string.IsNullOrEmpty(e.assetPath)) paths.Add(e.assetPath);
        }
        CollectPaths(battle?.peasantBattleAssetPaths, paths);
        CollectPaths(battle?.warriorBattleAssetPaths, paths);
        CollectPaths(battle?.knightBattleAssetPaths, paths);
        CollectPaths(battle?.priestBattleAssetPaths, paths);
        CollectPaths(battle?.demonImpBattleAssetPaths, paths);
        CollectPaths(battle?.hellhoundBattleAssetPaths, paths);
        CollectPaths(battle?.dreadLordBattleAssetPaths, paths);
        if (battle?.goodLegendSlots != null)
        {
            foreach (var e in battle.goodLegendSlots)
                if (e != null && !string.IsNullOrEmpty(e.assetPath)) paths.Add(e.assetPath);
        }
        if (battle?.evilBossSlots != null)
        {
            foreach (var e in battle.evilBossSlots)
                if (e != null && !string.IsNullOrEmpty(e.assetPath)) paths.Add(e.assetPath);
        }
        if (stage != null)
        {
            CollectSlotPaths(stage.churchPewSlots, paths);
            CollectSlotPaths(stage.playerBattleSlots, paths);
            CollectSlotPaths(stage.enemyBattleSlots, paths);
        }
        if (scene?.entries != null)
        {
            foreach (var e in scene.entries)
                if (e != null && !string.IsNullOrEmpty(e.imageAssetPath)) paths.Add(e.imageAssetPath);
        }

        int copied = 0;
        foreach (string ap in paths)
        {
            if (string.IsNullOrEmpty(ap)) continue;
            string full = DestFull(ap);
            if (!File.Exists(full)) continue;
            string ext = Path.GetExtension(full).ToLowerInvariant();
            if (ext != ".png" && ext != ".jpg" && ext != ".jpeg") continue;
            string dest = Path.Combine(DestFull(StreamingRel), Path.GetFileName(full));
            try
            {
                File.Copy(full, dest, overwrite: true);
                copied++;
            }
            catch (Exception ex)
            {
                log.AppendLine("Streaming copy soft-fail " + ap + ": " + ex.Message);
            }
        }
        log.AppendLine("StreamingAssets texture copies: " + copied);
    }

    static void CollectPaths(List<string> list, HashSet<string> into)
    {
        if (list == null) return;
        for (int i = 0; i < list.Count; i++)
            if (!string.IsNullOrEmpty(list[i])) into.Add(list[i]);
    }

    static void CollectSlotPaths(List<SlotTransformConfig> slots, HashSet<string> into)
    {
        if (slots == null) return;
        for (int i = 0; i < slots.Count; i++)
        {
            var s = slots[i];
            if (s == null) continue;
            if (!string.IsNullOrEmpty(s.previewAssetPath)) into.Add(s.previewAssetPath);
            else if (s.previewOrOverrideSprite != null)
            {
                string ap = AssetDatabase.GetAssetPath(s.previewOrOverrideSprite);
                if (!string.IsNullOrEmpty(ap))
                {
                    s.previewAssetPath = ap;
                    into.Add(ap);
                }
            }
        }
    }

    static void WriteManifest(
        StageLayoutProfile stage,
        SceneBackgroundProfile scene,
        ClassSpritePoolProfile body,
        LegendPewPortraitProfile face,
        BattleGoodEvilSpritePoolProfile battle,
        int promoted,
        StringBuilder log)
    {
        var sb = new StringBuilder();
        sb.AppendLine("DemonKnight Architect — player build include list");
        sb.AppendLine("tip=" + TipId);
        sb.AppendLine("generated=" + DateTime.UtcNow.ToString("o"));
        sb.AppendLine();
        sb.AppendLine("REQUIRED Resources profiles (Assets/Resources/):");
        sb.AppendLine("  StageLayoutProfile.asset");
        sb.AppendLine("  ClassSpritePoolProfile.asset");
        sb.AppendLine("  SceneBackgroundProfile.asset");
        sb.AppendLine("  LegendPewPortraitProfile.asset");
        sb.AppendLine("  BattleGoodEvilSpritePoolProfile.asset");
        sb.AppendLine();
        sb.AppendLine("REQUIRED promoted art:");
        sb.AppendLine("  Assets/Resources/ArchitectArt/** (Body + Face + Battle + Stage + SceneBg)");
        sb.AppendLine();
        sb.AppendLine("OPTIONAL fallback:");
        sb.AppendLine("  Assets/StreamingAssets/DemonKnightArchitect/*.(png|jpg)");
        sb.AppendLine();
        sb.AppendLine("Counts:");
        sb.AppendLine("  Body P/W/K = "
            + (body?.CountUsable(UnitType.Peasant) ?? 0) + "/"
            + (body?.CountUsable(UnitType.Warrior) ?? 0) + "/"
            + (body?.CountUsable(UnitType.Knight) ?? 0));
        sb.AppendLine("  Face P/W/K = "
            + (face?.CountUsablePewFaces(UnitType.Peasant) ?? 0) + "/"
            + (face?.CountUsablePewFaces(UnitType.Warrior) ?? 0) + "/"
            + (face?.CountUsablePewFaces(UnitType.Knight) ?? 0));
        sb.AppendLine("  Legend faces = " + (face?.CountAssignedLegendFaces() ?? 0));
        sb.AppendLine("  Battle good legends = " + (battle?.CountAssignedLegendBattles() ?? 0));
        sb.AppendLine("  Battle bosses = " + (battle?.CountAssignedBossBattles() ?? 0));
        sb.AppendLine("  Battle P/Priest/Imp = "
            + (battle?.CountUsablePool(UnitType.Peasant) ?? 0) + "/"
            + (battle?.CountUsablePool(UnitType.Priest) ?? 0) + "/"
            + (battle?.CountUsablePool(UnitType.DemonImp) ?? 0));
        sb.AppendLine("  Promoted this pass ≈ " + promoted);
        sb.AppendLine();
        sb.AppendLine("Body = Tab 4 Class Sprites (full pew avatar → bodySprite).");
        sb.AppendLine("Face = Tab 5 Legend/Pew Portraits (FacePortrait + optional Head overlay).");
        sb.AppendLine("Battle = Tab 6 Good/Evil pools (UnitVisualActor Autobattle sprites).");
        sb.AppendLine("Keep Tab 4 BODY / Tab 5 FACE / Tab 6 BATTLE separate (soft-fail cross-assign).");
        sb.AppendLine("Runtime Load rematerializes from path backups — no Editor wipe in player.");

        string full = DestFull(ManifestRel);
        Directory.CreateDirectory(Path.GetDirectoryName(full)!);
        File.WriteAllText(full, sb.ToString());
        log.AppendLine("Wrote " + ManifestRel);
    }

    static void EnsureFolder(string assetPath)
    {
        assetPath = assetPath.Replace('\\', '/').TrimEnd('/');
        if (AssetDatabase.IsValidFolder(assetPath)) return;
        string[] parts = assetPath.Split('/');
        string cur = parts[0];
        for (int i = 1; i < parts.Length; i++)
        {
            string next = cur + "/" + parts[i];
            if (!AssetDatabase.IsValidFolder(next))
                AssetDatabase.CreateFolder(cur, parts[i]);
            cur = next;
        }
    }

    static string Sanitize(string logical)
    {
        if (string.IsNullOrEmpty(logical)) return "sprite";
        var sb = new StringBuilder(logical.Length);
        foreach (char c in logical)
        {
            if (char.IsLetterOrDigit(c) || c == '_' || c == '-' || c == '/') sb.Append(c);
            else sb.Append('_');
        }
        return sb.ToString().Trim('/');
    }

    static string DestFull(string assetPath)
    {
        string root = Directory.GetParent(Application.dataPath)!.FullName;
        return Path.Combine(root, assetPath.Replace('/', Path.DirectorySeparatorChar));
    }
}
#endif
