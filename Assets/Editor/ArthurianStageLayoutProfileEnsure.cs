#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Cache;
using System.Text;
using ArthurianAutobattler;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Ensures Assets/Resources/StageLayoutProfile.asset exists with 52/52/20 slots.
/// Profile SOs live in GameDataTypes (tip fix-architect-save-load-pool-ux).
/// Ensure CreateInstance via Type.GetType + probe remap for multi-class MonoScript fileID.
/// tip: fix-architect-save-load-pool-ux
///
/// ALSO: chicken-egg escape from architect-breakglass-hosts. This file is Force-Synced via
/// tip SHA (not HardTip host overwrite). OnLoad pulls tip PackageVersion/Sync/Bootstrap/Architect
/// from raw@main when disk is still breakglass — then wipes Pose_/Outfit_/Head_ library residue
/// and imports Assets/Art/Spliced/Sprite into ModularSpriteLibrary.
/// </summary>
public static class ArthurianStageLayoutProfileEnsure
{
    const string AssetPath = "Assets/Resources/StageLayoutProfile.asset";
    const string Repo = "airizarry9938/DemonKnight";
    const string RawBase = "https://raw.githubusercontent.com/" + Repo + "/";
    const string TipPackageId = "fix-architect-save-load-pool-ux";
    const string SceneBgAssetPath = "Assets/Resources/SceneBackgroundProfile.asset";
    const string ClassPoolAssetPath = "Assets/Resources/ClassSpritePoolProfile.asset";
    const string LegendPewAssetPath = "Assets/Resources/LegendPewPortraitProfile.asset";
    const string BattleGoodEvilAssetPath = "Assets/Resources/BattleGoodEvilSpritePoolProfile.asset";
    const string ArtLibPath = "Assets/Art/Modular/ModularSpriteLibrary.asset";
    const string ResLibPath = "Assets/Resources/Art/Modular/ModularSpriteLibrary.asset";
    const string SplicedSpriteFolder = "Assets/Art/Spliced/Sprite";

    static readonly string[] StaleHostMarkers =
    {
        "architect-breakglass-hosts",
        "architect-disk-tip-proof",
        "architect-force-tip-ui",
        "runtime-single-sprite",
        "single-sprite-play-wipe",
        "single-sprite-auto-wipe",
        "single-sprite-no-paperdoll",
        "spliced-sprite-pews",
        "spliced-sprite-escape",
        "architect-no-bootstrap-clear",
        "breakglass-local-wipe",
        "pew-3rows-chroma-scale",
        "church-bg-pew-preview",
        "fix-seatshow-cs0436",
        "assign-only-pew-drag",
        "assign-only-runtime",
        "legend-unlocks-meta",
        "hidden-potential-bloodline",
        "architect-persist-scene-bg",
        "fix-scene-bg-applier-cs0103",
        "fix-wave-advance-stuck",
        "legends-fullwidth-column",
        "fix-hud-wave-match",
        "lore-boss-wave-order",
        "merlin-lore-narrative",
        "legends-lore-portrait",
        "architect-legend-pew-portraits",
        "fix-architect-pool-apply",
        "sprite-outline-highlight",
        "class-sprite-pools",
        "force-sync-appliers-hardfix",
        "sheet-splicer-chroma-despill",
        "church-train-scout-ux",
        "fix-somerset-church-ui-cs0246",
        "body-face-pool-build-persist",
        "battle-good-evil-sprite-pools",
        "fix-architect-build-persist-cs0103",
        "fix-build-persist-embed",
        "fix-pew-body-pool-empty",
        "fix-class-pool-kind-cs0019",
        "fix-profile-so-script-assets",
        "fix-named-profile-sync-cs0246",
        "fix-tipescape-profile-embeds",
        "fix-gdt-profile-always-compile",
        "fix-profiles-in-gdt-always",
        "fix-endless-compile-loop",
        "fix-hud-bar-button-layout",
        "fix-architect-apply-persist-facing",
    };

    const string OnLoadSessionGate = "DemonKnight.Ensure.OnLoadDone." + TipPackageId;

    /// <summary>
    /// tip: fix-architect-save-load-pool-ux — CreateInstance by Type from GameDataTypes assembly
    /// (multi-class MonoScript). Prefer Type.GetType so Ensure works even mid-recompile.
    /// NEVER touches .cs files. Soft-fails during compile/import (no CreateAsset churn).
    /// </summary>
    static T CreateProfileInstance<T>() where T : ScriptableObject
    {
        if (EditorApplication.isCompiling || EditorApplication.isUpdating)
        {
            Debug.LogWarning(
                "[DemonKnight] CreateProfileInstance<" + typeof(T).Name
                + "> skipped during compile/import (tip fix-architect-save-load-pool-ux).");
            return null;
        }
        string fullName = "ArthurianAutobattler." + typeof(T).Name;
        Type t = Type.GetType(fullName)
                 ?? Type.GetType(fullName + ", Assembly-CSharp")
                 ?? typeof(T);
        var so = ScriptableObject.CreateInstance(t) as T;
        if (so == null)
            so = ScriptableObject.CreateInstance<T>();
        return so;
    }

    [MenuItem("Arthurian/Ensure Stage Layout Profile", false, 55)]
    public static void EnsureFromMenu()
    {
        var p = EnsureAsset();
        var bg = EnsureSceneBackgroundAsset();
        var pools = EnsureClassSpritePoolAsset();
        var legendPew = EnsureLegendPewPortraitAsset();
        var battle = EnsureBattleGoodEvilSpritePoolAsset();
        Selection.activeObject = p;
        EditorGUIUtility.PingObject(p);
        int assigned = CountAssignedSprites(p);
        Debug.Log($"[DemonKnight] StageLayoutProfile ready → {AssetPath} " +
                  $"(pews={p.churchPewSlots.Count}, assignedSprites={assigned}, " +
                  $"player={p.playerBattleSlots.Count}, enemy={p.enemyBattleSlots.Count}). " +
                  $"SceneBackgroundProfile → {SceneBgAssetPath} ({bg.entries.Count} slots). " +
                  $"ClassSpritePoolProfile → {ClassPoolAssetPath} " +
                  $"(P={pools.CountUsable(UnitType.Peasant)} W={pools.CountUsable(UnitType.Warrior)} " +
                  $"K={pools.CountUsable(UnitType.Knight)}). " +
                  $"LegendPewPortraitProfile → {LegendPewAssetPath} " +
                  $"(legends={legendPew.CountAssignedLegendFaces()}/14 " +
                  $"pewFaces P={legendPew.CountUsablePewFaces(UnitType.Peasant)} " +
                  $"W={legendPew.CountUsablePewFaces(UnitType.Warrior)} " +
                  $"K={legendPew.CountUsablePewFaces(UnitType.Knight)}). " +
                  $"BattleGoodEvilSpritePoolProfile → {BattleGoodEvilAssetPath} " +
                  $"(goodLegends={battle.CountAssignedLegendBattles()} " +
                  $"bosses={battle.CountAssignedBossBattles()} " +
                  $"P={battle.CountUsablePool(UnitType.Peasant)} " +
                  $"Priest={battle.CountUsablePool(UnitType.Priest)} " +
                  $"Imp={battle.CountUsablePool(UnitType.DemonImp)}).");
    }

    [MenuItem("Arthurian/Escape Breakglass → Tip Hosts + Spliced Pew Library", false, 2)]
    public static void EscapeBreakglassMenu()
    {
        string report = EscapeStaleHostsAndRebuildPewLibrary();
        EditorUtility.DisplayDialog("Escape Breakglass", report, "OK");
    }

    [InitializeOnLoadMethod]
    static void EnsureOnLoad()
    {
        EditorApplication.delayCall += () =>
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            if (EditorApplication.isCompiling || EditorApplication.isUpdating) return;
            // tip: fix-architect-save-load-pool-ux — one-shot; SET GATE BEFORE Escape (Refresh mid-Escape = loop).
            if (SessionState.GetBool(OnLoadSessionGate, false)) return;
            SessionState.SetBool(OnLoadSessionGate, true);
            try
            {
                EnsureAsset();
                EnsureSceneBackgroundAsset();
                EnsureClassSpritePoolAsset();
                EnsureLegendPewPortraitAsset();
                EnsureBattleGoodEvilSpritePoolAsset();
                if (HostsLookStale())
                {
                    Debug.LogWarning(
                        "[DemonKnight] StageLayoutEnsure: disk hosts still breakglass/stale — " +
                        "pulling tip PackageVersion/Sync/Architect from raw@main (bypasses HardTip-first trap).");
                    EscapeStaleHostsAndRebuildPewLibrary();
                }
                else
                {
                    // Even on tip hosts, keep pew library on Spliced/Sprite (never Pose_/Outfit_ placeholders).
                    string wipe = RebuildPewLibraryFromSplices();
                    Debug.Log("[DemonKnight] StageLayoutEnsure pew library:\n" + wipe);
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[DemonKnight] StageLayoutProfile ensure skipped: " + ex.Message);
            }
        };
    }

    public static StageLayoutProfile EnsureAsset()
    {
        if (!AssetDatabase.IsValidFolder("Assets/Resources"))
            AssetDatabase.CreateFolder("Assets", "Resources");

        var existing = AssetDatabase.LoadAssetAtPath<StageLayoutProfile>(AssetPath);
        if (existing != null)
        {
            int before = existing.churchPewSlots != null ? existing.churchPewSlots.Count : 0;
            existing.EnsureSlotCounts();
            existing.RematerializeSpritesFromPaths();
            // Pad-only — do NOT SaveAssets on every editor load (was racing user assigns).
            if ((existing.churchPewSlots?.Count ?? 0) != before)
            {
                EditorUtility.SetDirty(existing);
                AssetDatabase.SaveAssets();
            }
            return existing;
        }

        string full = Path.Combine(Directory.GetParent(Application.dataPath)!.FullName,
            AssetPath.Replace('/', Path.DirectorySeparatorChar));
        bool fileOnDisk = File.Exists(full) || AssetDatabase.LoadMainAssetAtPath(AssetPath) != null;
        string yamlBackup = null;
        if (File.Exists(full))
        {
            try { yamlBackup = File.ReadAllText(full); }
            catch { yamlBackup = null; }
        }

        // Prefer remapping m_Script GUID (embed into GameDataTypes) over wipe — preserves seat sprites.
        if (fileOnDisk && !string.IsNullOrEmpty(yamlBackup))
        {
            if (TryRemapStageLayoutScriptGuid(full))
            {
                AssetDatabase.ImportAsset(AssetPath);
                existing = AssetDatabase.LoadAssetAtPath<StageLayoutProfile>(AssetPath);
                if (existing != null)
                {
                    existing.EnsureSlotCounts();
                    existing.RematerializeSpritesFromPaths();
                    int n = CountAssignedSprites(existing);
                    Debug.Log(
                        $"[DemonKnight] StageLayoutProfile script GUID remapped — preserved {n} assigned seat sprite(s).");
                    return existing;
                }
            }
        }

        if (fileOnDisk)
        {
            Debug.LogWarning(
                "[DemonKnight] Recreating StageLayoutProfile.asset (stale MonoScript) — " +
                "transplanting pew/battle slot YAML so assign-only sprites survive.");
            AssetDatabase.DeleteAsset(AssetPath);
            if (File.Exists(full))
            {
                try { File.Delete(full); } catch { /* ignore */ }
                string meta = full + ".meta";
                if (File.Exists(meta)) try { File.Delete(meta); } catch { /* ignore */ }
            }
            AssetDatabase.Refresh();
        }

        var created = CreateProfileInstance<StageLayoutProfile>();
        if (created == null) return null;
        created.EnsureSlotCounts();
        AssetDatabase.CreateAsset(created, AssetPath);
        AssetDatabase.SaveAssets();

        if (!string.IsNullOrEmpty(yamlBackup) && File.Exists(full))
        {
            try
            {
                string fresh = File.ReadAllText(full);
                string merged = TransplantStageLayoutLists(fresh, yamlBackup);
                if (!string.IsNullOrEmpty(merged) && merged != fresh)
                {
                    File.WriteAllText(full, merged);
                    AssetDatabase.ImportAsset(AssetPath);
                    existing = AssetDatabase.LoadAssetAtPath<StageLayoutProfile>(AssetPath);
                    if (existing != null)
                    {
                        existing.EnsureSlotCounts();
                        int n = CountAssignedSprites(existing);
                        Debug.Log(
                            $"[DemonKnight] StageLayoutProfile recreated with YAML transplant — {n} seat sprite(s) restored.");
                        return existing;
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[DemonKnight] StageLayout YAML transplant failed: " + ex.Message);
            }
        }

        AssetDatabase.Refresh();
        var stageLoaded = AssetDatabase.LoadAssetAtPath<StageLayoutProfile>(AssetPath);
        if (stageLoaded != null)
        {
            stageLoaded.EnsureSlotCounts();
            stageLoaded.RematerializeSpritesFromPaths();
        }
        return RefuseStandIn(stageLoaded, created, AssetPath);
    }

    /// <summary>Ensure SceneBackgroundProfile.asset exists (Architect Tab 3). Never wipe assignments.</summary>
    public static SceneBackgroundProfile EnsureSceneBackgroundAsset()
    {
        if (!AssetDatabase.IsValidFolder("Assets/Resources"))
            AssetDatabase.CreateFolder("Assets", "Resources");

        var existing = AssetDatabase.LoadAssetAtPath<SceneBackgroundProfile>(SceneBgAssetPath);
        if (existing != null)
        {
            existing.EnsureDefaultEntries();
            existing.RematerializeSpritesFromPaths();
            return existing;
        }

        string full = Path.Combine(Directory.GetParent(Application.dataPath)!.FullName,
            SceneBgAssetPath.Replace('/', Path.DirectorySeparatorChar));
        string yamlBackup = null;
        if (File.Exists(full))
        {
            try { yamlBackup = File.ReadAllText(full); } catch { /* ignore */ }
            if (TryRemapSceneBgScriptGuid(full))
            {
                AssetDatabase.ImportAsset(SceneBgAssetPath);
                existing = AssetDatabase.LoadAssetAtPath<SceneBackgroundProfile>(SceneBgAssetPath);
                if (existing != null)
                {
                    existing.EnsureDefaultEntries();
                    existing.RematerializeSpritesFromPaths();
                    return existing;
                }
            }
            AssetDatabase.DeleteAsset(SceneBgAssetPath);
            try { if (File.Exists(full)) File.Delete(full); } catch { /* ignore */ }
            try { if (File.Exists(full + ".meta")) File.Delete(full + ".meta"); } catch { /* ignore */ }
            AssetDatabase.Refresh();
        }

        var created = CreateProfileInstance<SceneBackgroundProfile>();
        if (created == null) return null;
        created.EnsureDefaultEntries();
        AssetDatabase.CreateAsset(created, SceneBgAssetPath);
        AssetDatabase.SaveAssets();

        if (!string.IsNullOrEmpty(yamlBackup) && File.Exists(full))
        {
            try
            {
                string fresh = File.ReadAllText(full);
                string merged = TransplantFromField(fresh, yamlBackup, "entries:");
                if (!string.IsNullOrEmpty(merged) && merged != fresh)
                {
                    File.WriteAllText(full, merged);
                    AssetDatabase.ImportAsset(SceneBgAssetPath);
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[DemonKnight] SceneBackground YAML transplant: " + ex.Message);
            }
        }

        var sceneLoaded = AssetDatabase.LoadAssetAtPath<SceneBackgroundProfile>(SceneBgAssetPath);
        if (sceneLoaded != null)
        {
            sceneLoaded.EnsureDefaultEntries();
            sceneLoaded.RematerializeSpritesFromPaths();
        }
        return RefuseStandIn(sceneLoaded, created, SceneBgAssetPath);
    }

    public static int CountAssignedSprites(StageLayoutProfile p)
    {
        if (p?.churchPewSlots == null) return 0;
        int n = 0;
        for (int i = 0; i < p.churchPewSlots.Count; i++)
            if (p.churchPewSlots[i]?.previewOrOverrideSprite != null) n++;
        return n;
    }

    /// <summary>Ensure ClassSpritePoolProfile.asset exists (Architect Tab 4). Never wipe pools.</summary>
    public static ClassSpritePoolProfile EnsureClassSpritePoolAsset()
    {
        if (!AssetDatabase.IsValidFolder("Assets/Resources"))
            AssetDatabase.CreateFolder("Assets", "Resources");

        var existing = AssetDatabase.LoadAssetAtPath<ClassSpritePoolProfile>(ClassPoolAssetPath);
        if (existing != null)
        {
            existing.EnsureLists();
            existing.RematerializeSpritesFromPaths();
            return existing;
        }

        string full = Path.Combine(Directory.GetParent(Application.dataPath)!.FullName,
            ClassPoolAssetPath.Replace('/', Path.DirectorySeparatorChar));
        string yamlBackup = null;
        if (File.Exists(full))
        {
            try { yamlBackup = File.ReadAllText(full); } catch { /* ignore */ }
            if (TryRemapClassPoolScriptGuid(full))
            {
                AssetDatabase.ImportAsset(ClassPoolAssetPath);
                existing = AssetDatabase.LoadAssetAtPath<ClassSpritePoolProfile>(ClassPoolAssetPath);
                if (existing != null)
                {
                    existing.EnsureLists();
                    existing.RematerializeSpritesFromPaths();
                    return existing;
                }
            }
            AssetDatabase.DeleteAsset(ClassPoolAssetPath);
            try { if (File.Exists(full)) File.Delete(full); } catch { /* ignore */ }
            try { if (File.Exists(full + ".meta")) File.Delete(full + ".meta"); } catch { /* ignore */ }
            AssetDatabase.Refresh();
        }

        var created = CreateProfileInstance<ClassSpritePoolProfile>();
        if (created == null) return null;
        created.EnsureLists();
        AssetDatabase.CreateAsset(created, ClassPoolAssetPath);
        AssetDatabase.SaveAssets();

        if (!string.IsNullOrEmpty(yamlBackup) && File.Exists(full))
        {
            try
            {
                string fresh = File.ReadAllText(full);
                // Transplant from first pool list field so all three lists + paths survive.
                string merged = TransplantFromField(fresh, yamlBackup, "peasantSprites:");
                if (!string.IsNullOrEmpty(merged) && merged != fresh)
                {
                    File.WriteAllText(full, merged);
                    AssetDatabase.ImportAsset(ClassPoolAssetPath);
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[DemonKnight] ClassSpritePool YAML transplant: " + ex.Message);
            }
        }

        var poolLoaded = AssetDatabase.LoadAssetAtPath<ClassSpritePoolProfile>(ClassPoolAssetPath);
        if (poolLoaded != null)
        {
            poolLoaded.EnsureLists();
            poolLoaded.RematerializeSpritesFromPaths();
        }
        return RefuseStandIn(poolLoaded, created, ClassPoolAssetPath);
    }

    /// <summary>Ensure LegendPewPortraitProfile.asset exists (Architect Tab 5). Never wipe assignments.</summary>
    public static LegendPewPortraitProfile EnsureLegendPewPortraitAsset()
    {
        if (!AssetDatabase.IsValidFolder("Assets/Resources"))
            AssetDatabase.CreateFolder("Assets", "Resources");

        var existing = AssetDatabase.LoadAssetAtPath<LegendPewPortraitProfile>(LegendPewAssetPath);
        if (existing != null)
        {
            existing.EnsureSlotsAndLists();
            existing.RematerializeSpritesFromPaths();
            return existing;
        }

        string full = Path.Combine(Directory.GetParent(Application.dataPath)!.FullName,
            LegendPewAssetPath.Replace('/', Path.DirectorySeparatorChar));
        string yamlBackup = null;
        if (File.Exists(full))
        {
            try { yamlBackup = File.ReadAllText(full); } catch { /* ignore */ }
            if (TryRemapLegendPewScriptGuid(full))
            {
                AssetDatabase.ImportAsset(LegendPewAssetPath);
                existing = AssetDatabase.LoadAssetAtPath<LegendPewPortraitProfile>(LegendPewAssetPath);
                if (existing != null)
                {
                    existing.EnsureSlotsAndLists();
                    existing.RematerializeSpritesFromPaths();
                    return existing;
                }
            }
            AssetDatabase.DeleteAsset(LegendPewAssetPath);
            try { if (File.Exists(full)) File.Delete(full); } catch { /* ignore */ }
            try { if (File.Exists(full + ".meta")) File.Delete(full + ".meta"); } catch { /* ignore */ }
            AssetDatabase.Refresh();
        }

        var created = CreateProfileInstance<LegendPewPortraitProfile>();
        if (created == null) return null;
        created.EnsureSlotsAndLists();
        AssetDatabase.CreateAsset(created, LegendPewAssetPath);
        AssetDatabase.SaveAssets();

        if (!string.IsNullOrEmpty(yamlBackup) && File.Exists(full))
        {
            try
            {
                string fresh = File.ReadAllText(full);
                string merged = TransplantFromField(fresh, yamlBackup, "legendPortraits:");
                if (!string.IsNullOrEmpty(merged) && merged != fresh)
                {
                    File.WriteAllText(full, merged);
                    AssetDatabase.ImportAsset(LegendPewAssetPath);
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[DemonKnight] LegendPewPortrait YAML transplant: " + ex.Message);
            }
        }

        existing = AssetDatabase.LoadAssetAtPath<LegendPewPortraitProfile>(LegendPewAssetPath);
        if (existing != null)
        {
            existing.EnsureSlotsAndLists();
            existing.RematerializeSpritesFromPaths();
        }
        existing = RefuseStandIn(existing, created, LegendPewAssetPath);
        existing?.EnsureSlotsAndLists();
        return existing;
    }

    /// <summary>Ensure BattleGoodEvilSpritePoolProfile.asset exists (Architect Tab 6). Never wipe pools.</summary>
    public static BattleGoodEvilSpritePoolProfile EnsureBattleGoodEvilSpritePoolAsset()
    {
        if (!AssetDatabase.IsValidFolder("Assets/Resources"))
            AssetDatabase.CreateFolder("Assets", "Resources");

        var existing = AssetDatabase.LoadAssetAtPath<BattleGoodEvilSpritePoolProfile>(BattleGoodEvilAssetPath);
        if (existing != null)
        {
            existing.EnsureSlotsAndLists();
            existing.RematerializeSpritesFromPaths();
            return existing;
        }

        string full = Path.Combine(Directory.GetParent(Application.dataPath)!.FullName,
            BattleGoodEvilAssetPath.Replace('/', Path.DirectorySeparatorChar));
        string yamlBackup = null;
        if (File.Exists(full))
        {
            try { yamlBackup = File.ReadAllText(full); } catch { /* ignore */ }
            if (TryRemapBattleGoodEvilScriptGuid(full))
            {
                AssetDatabase.ImportAsset(BattleGoodEvilAssetPath);
                existing = AssetDatabase.LoadAssetAtPath<BattleGoodEvilSpritePoolProfile>(BattleGoodEvilAssetPath);
                if (existing != null)
                {
                    existing.EnsureSlotsAndLists();
                    existing.RematerializeSpritesFromPaths();
                    return existing;
                }
            }
            AssetDatabase.DeleteAsset(BattleGoodEvilAssetPath);
            try { if (File.Exists(full)) File.Delete(full); } catch { /* ignore */ }
            try { if (File.Exists(full + ".meta")) File.Delete(full + ".meta"); } catch { /* ignore */ }
            AssetDatabase.Refresh();
        }

        var created = CreateProfileInstance<BattleGoodEvilSpritePoolProfile>();
        if (created == null) return null;
        created.EnsureSlotsAndLists();
        AssetDatabase.CreateAsset(created, BattleGoodEvilAssetPath);
        AssetDatabase.SaveAssets();

        if (!string.IsNullOrEmpty(yamlBackup) && File.Exists(full))
        {
            try
            {
                string fresh = File.ReadAllText(full);
                string merged = TransplantFromField(fresh, yamlBackup, "peasantBattleSprites:");
                if (!string.IsNullOrEmpty(merged) && merged != fresh)
                {
                    File.WriteAllText(full, merged);
                    AssetDatabase.ImportAsset(BattleGoodEvilAssetPath);
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[DemonKnight] BattleGoodEvil YAML transplant: " + ex.Message);
            }
        }

        existing = AssetDatabase.LoadAssetAtPath<BattleGoodEvilSpritePoolProfile>(BattleGoodEvilAssetPath);
        if (existing != null)
        {
            existing.EnsureSlotsAndLists();
            existing.RematerializeSpritesFromPaths();
        }
        existing = RefuseStandIn(existing, created, BattleGoodEvilAssetPath);
        existing?.EnsureSlotsAndLists();
        return existing;
    }

    static bool TryRemapStageLayoutScriptGuid(string fullPath)
        => TryRemapScriptGuid(fullPath, typeof(StageLayoutProfile));

    static bool TryRemapSceneBgScriptGuid(string fullPath)
        => TryRemapScriptGuid(fullPath, typeof(SceneBackgroundProfile));

    static bool TryRemapClassPoolScriptGuid(string fullPath)
        => TryRemapScriptGuid(fullPath, typeof(ClassSpritePoolProfile));

    static bool TryRemapLegendPewScriptGuid(string fullPath)
        => TryRemapScriptGuid(fullPath, typeof(LegendPewPortraitProfile));

    static bool TryRemapBattleGoodEvilScriptGuid(string fullPath)
        => TryRemapScriptGuid(fullPath, typeof(BattleGoodEvilSpritePoolProfile));

    /// <summary>
    /// Bind asset m_Script to GameDataTypes MonoScript for this SO type (multi-class fileID).
    /// Probe CreateAsset reads Unity's exact fileID+guid for the embedded class.
    /// Remap preserves YAML assigns — never wipe pools on reopen. tip: fix-architect-save-load-pool-ux
    /// </summary>
    static bool TryRemapScriptGuid(string fullPath, Type soType)
    {
        try
        {
            if (!TryResolveScriptRef(soType, out long localId, out string newGuid))
                return false;
            if (string.IsNullOrEmpty(newGuid) || newGuid.Length != 32) return false;

            string yaml = File.ReadAllText(fullPath);
            var re = new System.Text.RegularExpressions.Regex(
                @"m_Script:\s*\{fileID:\s*(-?\d+),\s*guid:\s*([0-9a-fA-F]+),\s*type:\s*3\}");
            var m = re.Match(yaml);
            if (!m.Success) return false;
            string oldFileId = m.Groups[1].Value;
            string oldGuid = m.Groups[2].Value;
            if (string.Equals(oldGuid, newGuid, StringComparison.OrdinalIgnoreCase)
                && string.Equals(oldFileId, localId.ToString(), StringComparison.Ordinal))
                return false; // fully correct — typed load failed for another reason
            string want = "m_Script: {fileID: " + localId + ", guid: " + newGuid + ", type: 3}";
            string replaced = re.Replace(yaml, want, 1);
            if (replaced == yaml) return false;
            File.WriteAllText(fullPath, replaced);
            Debug.Log(
                "[DemonKnight] Remapped " + soType.Name + " m_Script fileID "
                + oldFileId + "→" + localId + " guid " + oldGuid + "→" + newGuid);
            return true;
        }
        catch (Exception ex)
        {
            Debug.LogWarning("[DemonKnight] TryRemapScriptGuid: " + ex.Message);
            return false;
        }
    }

    /// <summary>
    /// Probe CreateAsset writes the exact MonoScript fileID+guid Unity expects for this SO type.
    /// Profiles in GameDataTypes use multi-class MonoScript fileID — probe CreateAsset reads exact fileID+guid. tip: fix-architect-save-load-pool-ux
    /// </summary>
    static bool TryResolveScriptRef(Type soType, out long localId, out string newGuid)
    {
        localId = 11500000L;
        newGuid = null;
        const string probePath = "Assets/Resources/_DkScriptRemapProbe.asset";
        try
        {
            if (AssetDatabase.LoadMainAssetAtPath(probePath) != null)
                AssetDatabase.DeleteAsset(probePath);
            string probeFull = Path.Combine(
                Directory.GetParent(Application.dataPath)!.FullName,
                probePath.Replace('/', Path.DirectorySeparatorChar));
            if (File.Exists(probeFull)) try { File.Delete(probeFull); } catch { /* ignore */ }
            if (File.Exists(probeFull + ".meta")) try { File.Delete(probeFull + ".meta"); } catch { /* ignore */ }

            var probe = ScriptableObject.CreateInstance(soType);
            AssetDatabase.CreateAsset(probe, probePath);
            AssetDatabase.SaveAssets();
            if (File.Exists(probeFull))
            {
                string py = File.ReadAllText(probeFull);
                var re = new System.Text.RegularExpressions.Regex(
                    @"m_Script:\s*\{fileID:\s*(-?\d+),\s*guid:\s*([0-9a-fA-F]+),\s*type:\s*3\}");
                var m = re.Match(py);
                AssetDatabase.DeleteAsset(probePath);
                if (m.Success)
                {
                    localId = long.Parse(m.Groups[1].Value);
                    newGuid = m.Groups[2].Value;
                    return !string.IsNullOrEmpty(newGuid);
                }
            }
            else
            {
                try { AssetDatabase.DeleteAsset(probePath); } catch { /* ignore */ }
            }
        }
        catch (Exception ex)
        {
            Debug.LogWarning("[DemonKnight] ScriptRef probe soft-fail: " + ex.Message);
            try { AssetDatabase.DeleteAsset(probePath); } catch { /* ignore */ }
        }

        try
        {
            var tmp = ScriptableObject.CreateInstance(soType);
            var ms = MonoScript.FromScriptableObject((ScriptableObject)tmp);
            UnityEngine.Object.DestroyImmediate(tmp);
            if (ms == null) return false;
            if (AssetDatabase.TryGetGUIDAndLocalFileIdentifier(ms, out newGuid, out localId)
                && !string.IsNullOrEmpty(newGuid) && newGuid.Length == 32)
                return true;
            string scriptPath = AssetDatabase.GetAssetPath(ms);
            newGuid = AssetDatabase.AssetPathToGUID(scriptPath);
            localId = 11500000L;
            return !string.IsNullOrEmpty(newGuid) && newGuid.Length == 32;
        }
        catch (Exception ex)
        {
            Debug.LogWarning("[DemonKnight] TryResolveScriptRef: " + ex.Message);
            return false;
        }
    }

    /// <summary>Never hand Architect a CreateInstance stand-in — Persist would SetDirty a non-asset.</summary>
    static T RefuseStandIn<T>(T loaded, T created, string assetPath) where T : ScriptableObject
    {
        if (loaded != null && !string.IsNullOrEmpty(AssetDatabase.GetAssetPath(loaded)))
            return loaded;
        if (created != null && !string.IsNullOrEmpty(AssetDatabase.GetAssetPath(created)))
            return created;
        Debug.LogError(
            "[DemonKnight] Ensure refused CreateInstance stand-in for " + assetPath
            + " — typed Load failed after CreateAsset. Run Arthurian → Ensure Stage Layout Profile.");
        return loaded ?? created;
    }

    /// <summary>Keep fresh m_Script header; transplant church/battle/parallax lists from backup YAML.</summary>
    static string TransplantStageLayoutLists(string freshYaml, string backupYaml)
        => TransplantFromField(freshYaml, backupYaml, "churchPewSlots:");

    static string TransplantFromField(string freshYaml, string backupYaml, string fieldMarker)
    {
        if (string.IsNullOrEmpty(freshYaml) || string.IsNullOrEmpty(backupYaml)) return freshYaml;
        int backupData = IndexOfYamlField(backupYaml, fieldMarker);
        int freshData = IndexOfYamlField(freshYaml, fieldMarker);
        if (backupData < 0 || freshData < 0) return freshYaml;
        return freshYaml.Substring(0, freshData) + backupYaml.Substring(backupData);
    }

    static int IndexOfYamlField(string yaml, string fieldMarker)
    {
        // Prefer indented Unity YAML field ("  churchPewSlots:").
        string indented = "\n  " + fieldMarker;
        int i = yaml.IndexOf(indented, StringComparison.Ordinal);
        if (i >= 0) return i;
        string bare = "\n" + fieldMarker;
        return yaml.IndexOf(bare, StringComparison.Ordinal);
    }

    public static bool HostsLookStalePublic() => HostsLookStale();

    static bool HostsLookStale()
    {
        string root = Directory.GetParent(Application.dataPath)!.FullName;
        string pv = Path.Combine(root, "Assets", "Editor", "DemonKnightPackageVersion.cs");
        string arch = Path.Combine(root, "Assets", "Editor", "ArthurianArtArchitectWindow.cs");
        string sync = Path.Combine(root, "Assets", "Editor", "ArthurianSyncFromGitHub.cs");
        if (!File.Exists(pv) || !File.Exists(arch)) return true;
        string tipEscape = Path.Combine(root, "Assets", "Editor", "DemonKnightTipEscape.cs");
        if (!File.Exists(tipEscape)) return true;
        string pvBody = File.ReadAllText(pv);
        string archBody = File.ReadAllText(arch);
        if (pvBody.IndexOf(TipPackageId, StringComparison.Ordinal) < 0) return true;
        if (archBody.IndexOf(TipPackageId, StringComparison.Ordinal) < 0) return true;
        if (archBody.IndexOf("WipeModularPaperdollAssets", StringComparison.Ordinal) >= 0) return true;
        foreach (string m in StaleHostMarkers)
        {
            if (m == TipPackageId) continue;
            // tip: fix-architect-save-load-pool-ux — only Id / UiBuildId assignments (bare "m" matches
            // StalePackageIds / comment lists → false-positive → Escape every OnLoad → compile loop).
            if (archBody.IndexOf("UiBuildId = \"" + m + "\"", StringComparison.Ordinal) >= 0) return true;
            if (pvBody.IndexOf("Id = \"" + m + "\"", StringComparison.Ordinal) >= 0) return true;
        }
        if (File.Exists(sync))
        {
            string s = File.ReadAllText(sync);
            if (s.IndexOf(TipPackageId, StringComparison.Ordinal) < 0) return true;
            if (s.IndexOf("HardRestoreNamedProfileScripts", StringComparison.Ordinal) < 0) return true;
            // HardTip-first without tip/main preference = breakglass trap.
            if (s.IndexOf("HardTip-first", StringComparison.Ordinal) >= 0
                && s.IndexOf("tip of main + branch tip FIRST", StringComparison.OrdinalIgnoreCase) < 0
                && s.IndexOf("API tip + raw@main FIRST", StringComparison.OrdinalIgnoreCase) < 0)
                return true;
        }
        // CS0246: Escape must re-pull when GameDataTypes is missing Profile SO classes.
        if (DemonKnightPackageVersion.NamedProfileScriptsNeedRestore(root)) return true;
        return false;
    }

    public static string EscapeStaleHostsAndRebuildPewLibrary()
    {
        var log = new StringBuilder();
        log.AppendLine("Escape → tip package " + TipPackageId);
        ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12;
        string tip = TryResolveTipSha() ?? "main";
        string root = Directory.GetParent(Application.dataPath)!.FullName;
        // tip: fix-architect-save-load-pool-ux — stub leftover named *Profile.cs before host pull.
        try
        {
            int n = ArthurianSyncFromGitHub.NukeNamedProfileFiles(root);
            if (n > 0) log.AppendLine("NukeNamedProfile files: " + n);
        }
        catch (Exception ex) { log.AppendLine("NukeNamedProfile: " + ex.Message); }
        string[] rels =
        {
            "Assets/Editor/DemonKnightTipEscape.cs",
            "Assets/Editor/DemonKnightPackageVersion.cs",
            // tip: fix-architect-save-load-pool-ux — Profile SOs live in GameDataTypes (MustRestore).
            "Assets/Scripts/Data/GameDataTypes.cs",
            "Assets/Scripts/UI/SceneBackgroundApplier.cs",
            "Assets/Scripts/UI/ClassSpritePoolApplier.cs",
            "Assets/Editor/ArthurianSyncFromGitHub.cs",
            "Assets/Editor/ArthurianMenuBootstrap.cs",
            "Assets/Editor/ArthurianArtArchitectWindow.cs",
            "Assets/Editor/ArthurianStageLayoutProfileEnsure.cs",
            "Assets/Editor/ArthurianArchitectBuildPersist.cs",
        };
        int ok = 0;
        foreach (string rel in rels)
        {
            try
            {
                string body = DownloadMainFirst(rel, tip);
                if (string.IsNullOrEmpty(body) || body.Length < 80 || LooksTiny404(body))
                    throw new Exception("bad body len=" + (body == null ? 0 : body.Length));
                if (rel.EndsWith("DemonKnightPackageVersion.cs", StringComparison.OrdinalIgnoreCase))
                {
                    if (body.Length < 500) throw new Exception("PackageVersion too small");
                    if (body.IndexOf(TipPackageId, StringComparison.Ordinal) < 0)
                        throw new Exception("PackageVersion missing " + TipPackageId);
                    // Never re-land breakglass PackageVersion.
                    if (body.IndexOf("Id = \"architect-breakglass-hosts\"", StringComparison.Ordinal) >= 0)
                        throw new Exception("rejected breakglass PackageVersion");
                }
                string dest = Path.Combine(root, rel.Replace('/', Path.DirectorySeparatorChar));
                if (ArthurianSyncFromGitHub.WriteTextIfChanged(dest, body))
                {
                    ok++;
                    log.AppendLine($"Wrote {rel} ({body.Length} B)");
                    Debug.Log($"[DemonKnight] EscapeBreakglass wrote {rel} ({body.Length} bytes)");
                }
                else
                {
                    log.AppendLine($"SKIP unchanged {rel}");
                }
            }
            catch (Exception ex)
            {
                log.AppendLine($"MISS {rel}: {ex.Message}");
                Debug.LogWarning($"[DemonKnight] EscapeBreakglass miss {rel}: {ex.Message}");
            }
        }
        // tip: fix-architect-save-load-pool-ux — Refresh only when bytes changed.
        if (ok > 0) AssetDatabase.Refresh();
        log.AppendLine($"Hosts written: {ok}/{rels.Length}");
        if (!EditorApplication.isCompiling && !EditorApplication.isUpdating)
            log.AppendLine(RebuildPewLibraryFromSplices());
        log.AppendLine("Wait compile → Diagnose must show " + TipPackageId + ".");
        log.AppendLine("Then Force Delete+Sync ALL Scripts → New Game.");
        log.AppendLine("Bind log: body=<spliced name> outfit=NULL head=NULL.");
        return log.ToString();
    }

    /// <summary>Clear Pose_/Outfit_/Head_ placeholders; fill basePoses from Assets/Art/Spliced/Sprite.</summary>
    public static string RebuildPewLibraryFromSplices()
    {
        var log = new StringBuilder();
        try
        {
            // Prefer tip PackageVersion wipe when compiled.
            try
            {
                var wipeType = Type.GetType("DemonKnightPackageVersion")
                               ?? Type.GetType("DemonKnightPackageVersion, Assembly-CSharp-Editor");
                var wipe = wipeType?.GetMethod("WipeModularPaperdollAssets",
                    System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
                if (wipe != null)
                {
                    object r = wipe.Invoke(null, null);
                    log.AppendLine("PackageVersion wipe: " + r);
                }
            }
            catch (Exception ex)
            {
                log.AppendLine("PackageVersion wipe skip: " + ex.Message);
            }

            EnsureFolderPath("Assets/Art/Modular");
            EnsureFolderPath("Assets/Resources/Art/Modular");
            EnsureFolderPath(SplicedSpriteFolder);

            var art = AssetDatabase.LoadAssetAtPath<ModularSpriteLibrary>(ArtLibPath);
            if (art == null)
            {
                art = ScriptableObject.CreateInstance<ModularSpriteLibrary>();
                art.EnsureSizedArrays();
                AssetDatabase.CreateAsset(art, ArtLibPath);
            }

            // Strip paperdoll arrays + placeholder basePoses.
            art.outfits = Array.Empty<Sprite>();
            art.heads = Array.Empty<ModularHeadEntry>();
            art.props = Array.Empty<Sprite>();
            if (art.animClips != null)
            {
                art.animClips.RemoveAll(e =>
                    e == null || !string.Equals(e.category, "Sprite", StringComparison.OrdinalIgnoreCase));
            }

            var spliced = LoadSplicedSprites();
            if (spliced.Count > 0)
            {
                art.basePoses = spliced.ToArray();
                log.AppendLine($"basePoses ← {spliced.Count} from {SplicedSpriteFolder}");
                foreach (var sp in spliced)
                    if (sp != null) log.AppendLine("  + " + sp.name);
            }
            else
            {
                // Empty — NOT paperdoll-shaped Pose_/Outfit_ placeholders.
                art.basePoses = Array.Empty<Sprite>();
                Array.Resize(ref art.basePoses, 4);
                log.AppendLine("No sprites in " + SplicedSpriteFolder
                               + " — left empty (solid pew color until Save Splices).");
            }

            EditorUtility.SetDirty(art);
            AssetDatabase.SaveAssets();

            if (AssetDatabase.LoadAssetAtPath<ModularSpriteLibrary>(ResLibPath) != null)
                AssetDatabase.DeleteAsset(ResLibPath);
            AssetDatabase.CopyAsset(ArtLibPath, ResLibPath);
            var res = AssetDatabase.LoadAssetAtPath<ModularSpriteLibrary>(ResLibPath);
            if (res != null)
            {
                res.outfits = Array.Empty<Sprite>();
                res.heads = Array.Empty<ModularHeadEntry>();
                res.props = Array.Empty<Sprite>();
                if (spliced.Count > 0) res.basePoses = spliced.ToArray();
                else
                {
                    res.basePoses = Array.Empty<Sprite>();
                    Array.Resize(ref res.basePoses, 4);
                }
                EditorUtility.SetDirty(res);
                AssetDatabase.SaveAssets();
            }
            AssetDatabase.Refresh();
            TownsfolkVisualApplier.SetLibrary(null);
            log.AppendLine("Resources copy updated. Stop Play → New Game.");
        }
        catch (Exception ex)
        {
            log.AppendLine("Rebuild ERROR: " + ex.Message);
            Debug.LogWarning("[DemonKnight] RebuildPewLibraryFromSplices: " + ex.Message);
        }
        return log.ToString().TrimEnd();
    }

    static List<Sprite> LoadSplicedSprites()
    {
        var list = new List<Sprite>();
        if (!AssetDatabase.IsValidFolder(SplicedSpriteFolder)) return list;
        string[] guids = AssetDatabase.FindAssets("t:Sprite", new[] { SplicedSpriteFolder });
        var seen = new HashSet<string>();
        foreach (string g in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(g);
            if (!seen.Add(path)) continue;
            var sp = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (sp == null) continue;
            if (IsPaperdollPlaceholderName(sp.name)) continue;
            list.Add(sp);
        }
        list.Sort((a, b) => string.CompareOrdinal(a.name, b.name));
        return list;
    }

    public static bool IsPaperdollPlaceholderName(string name)
    {
        if (string.IsNullOrEmpty(name)) return true;
        if (name.StartsWith("Pose_", StringComparison.OrdinalIgnoreCase)) return true;
        if (name.StartsWith("Outfit_", StringComparison.OrdinalIgnoreCase)) return true;
        if (name.StartsWith("Head_", StringComparison.OrdinalIgnoreCase)) return true;
        if (name.StartsWith("Prop_", StringComparison.OrdinalIgnoreCase)) return true;
        if (name.StartsWith("DK_Pose_", StringComparison.OrdinalIgnoreCase)) return true;
        if (name.StartsWith("DK_Outfit_", StringComparison.OrdinalIgnoreCase)) return true;
        if (name.StartsWith("DK_Head_", StringComparison.OrdinalIgnoreCase)) return true;
        if (name.StartsWith("DK_Sprite_", StringComparison.OrdinalIgnoreCase)) return true;
        if (name.IndexOf("Prepared_Pose", StringComparison.OrdinalIgnoreCase) >= 0) return true;
        if (name.IndexOf("Prepared_Outfit", StringComparison.OrdinalIgnoreCase) >= 0) return true;
        if (name.IndexOf("Prepared_Head", StringComparison.OrdinalIgnoreCase) >= 0) return true;
        return false;
    }

    static string TryResolveTipSha()
    {
        try
        {
            using var wc = new WebClient();
            wc.CachePolicy = new RequestCachePolicy(RequestCacheLevel.BypassCache);
            wc.Headers.Add("User-Agent", "DemonKnight-EscapeBreakglass/" + TipPackageId);
            wc.Headers.Add("Accept", "application/vnd.github.v3+json");
            string json = wc.DownloadString("https://api.github.com/repos/" + Repo + "/commits/main");
            if (json.Length > 50 && json.TrimStart().StartsWith("{", StringComparison.Ordinal))
            {
                int colon = json.IndexOf(':');
                int q1 = colon >= 0 ? json.IndexOf('"', colon + 1) : -1;
                int q2 = q1 >= 0 ? json.IndexOf('"', q1 + 1) : -1;
                if (q1 >= 0 && q2 == q1 + 41)
                {
                    string sha = json.Substring(q1 + 1, 40);
                    if (System.Text.RegularExpressions.Regex.IsMatch(sha, "^[0-9a-f]{40}$"))
                        return sha;
                }
            }
        }
        catch { /* ignore */ }
        return null;
    }

    static string DownloadMainFirst(string rel, string tip)
    {
        string bust = "t=" + DateTime.UtcNow.Ticks;
        // Tip + main ONLY — never HardTip (that re-lands breakglass).
        string[] shas = { tip, "main" };
        Exception last = null;
        foreach (string sha in shas)
        {
            if (string.IsNullOrEmpty(sha)) continue;
            string[] urls =
            {
                RawBase + sha + "/" + rel + "?" + bust,
                RawBase + sha + "/" + rel,
                "https://cdn.jsdelivr.net/gh/" + Repo + "@" + sha + "/" + rel + "?" + bust,
            };
            foreach (string url in urls)
            {
                try
                {
                    using var wc = new WebClient();
                    wc.CachePolicy = new RequestCachePolicy(RequestCacheLevel.BypassCache);
                    wc.Headers.Add("User-Agent", "DemonKnight-EscapeBreakglass/" + TipPackageId);
                    wc.Headers.Add("Cache-Control", "no-cache, no-store");
                    string body = wc.DownloadString(url);
                    if (!LooksTiny404(body) && body.Length > 80) return body;
                    last = new Exception("tiny/404 " + url);
                }
                catch (Exception ex) { last = ex; }
            }
        }
        throw last ?? new Exception("download failed " + rel);
    }

    static bool LooksTiny404(string body)
    {
        if (string.IsNullOrEmpty(body)) return true;
        if (body.Length >= 512) return false;
        string t = body.TrimStart();
        return t.StartsWith("404", StringComparison.Ordinal)
               || t.IndexOf("404: Not Found", StringComparison.OrdinalIgnoreCase) >= 0
               || t.StartsWith("<!DOCTYPE", StringComparison.OrdinalIgnoreCase)
               || t.StartsWith("<html", StringComparison.OrdinalIgnoreCase);
    }

    static void EnsureFolderPath(string folder)
    {
        if (string.IsNullOrEmpty(folder) || AssetDatabase.IsValidFolder(folder)) return;
        string[] parts = folder.Replace('\\', '/').Split('/');
        string cur = parts[0];
        for (int i = 1; i < parts.Length; i++)
        {
            string next = cur + "/" + parts[i];
            if (!AssetDatabase.IsValidFolder(next))
                AssetDatabase.CreateFolder(cur, parts[i]);
            cur = next;
        }
    }
}
#endif
