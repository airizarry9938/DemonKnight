#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Cache;
using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>Bump when shipping package updates so Diagnose can prove Unity loaded the new build.</summary>
public static class DemonKnightPackageVersion
{
    public const string Id = "battle-good-evil-sprite-pools";
    public const string GitHint = "battle-good-evil-sprite-pools";
    public const string BuiltUtc = "2026-11-23T01:25:00Z";

    /// <summary>Fallback only — OnLoad prefers API tip + raw@main (never re-land breakglass).</summary>
    public const string HardTipSha = "c4a0867cfece2c40d33216f06144ed9d33e03a51";

    const string Repo = "airizarry9938/DemonKnight";
    const string RawBase = "https://raw.githubusercontent.com/" + Repo + "/";
    const string ArtLibPath = "Assets/Art/Modular/ModularSpriteLibrary.asset";
    const string ResLibPath = "Assets/Resources/Art/Modular/ModularSpriteLibrary.asset";
    const string SplicedFolder = "Assets/Art/Spliced/Sprite";

    public static string LastWipeReport { get; private set; } = "(wipe not run this session)";

    [InitializeOnLoadMethod]
    static void DkForceTipHostsAndWipeLibrary()
    {
        EditorApplication.delayCall += () =>
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            try
            {
                // CS0436: PackageVersion must live ONLY under Assets/Editor (not Scripts/).
                NukeDuplicatePackageVersionOutsideEditor();

                LastWipeReport = WipeModularPaperdollAssets();
                Debug.Log("[DemonKnight] PackageVersion OnLoad library wipe:\n" + LastWipeReport);

                string root = Directory.GetParent(Application.dataPath)!.FullName;
                string syncPath = Path.Combine(root, "Assets", "Editor", "ArthurianSyncFromGitHub.cs");
                string archPath = Path.Combine(root, "Assets", "Editor", "ArthurianArtArchitectWindow.cs");
                string bootPath = Path.Combine(root, "Assets", "Editor", "ArthurianMenuBootstrap.cs");
                bool need = !File.Exists(syncPath) || !File.Exists(archPath) || !File.Exists(bootPath);

                string tipEscapePath = Path.Combine(root, "Assets", "Editor", "DemonKnightTipEscape.cs");
                if (!File.Exists(tipEscapePath)) need = true;

                if (!need)
                {
                    string arch = File.ReadAllText(archPath);
                    string sync = File.ReadAllText(syncPath);
                    string boot = File.ReadAllText(bootPath);
                    if (arch.IndexOf(Id, StringComparison.Ordinal) < 0) need = true;
                    if (sync.IndexOf(Id, StringComparison.Ordinal) < 0) need = true;
                    if (ContainsStaleMarker(arch) || ContainsStaleMarker(sync) || ContainsStaleMarker(boot))
                        need = true;
                    if (sync.IndexOf("LooksLikeHttpFailureBody", StringComparison.Ordinal) < 0) need = true;
                    if (sync.IndexOf("StalePackageIds", StringComparison.Ordinal) < 0) need = true;
                    if (boot.IndexOf("Clear Paperdoll Library", StringComparison.Ordinal) < 0) need = true;
                    // CS0117 chicken-egg: Architect must not call PackageVersion.Wipe*
                    if (arch.IndexOf("WipeModularPaperdollAssets", StringComparison.Ordinal) >= 0) need = true;
                }

                if (!need) return;

                Debug.LogWarning(
                    "[DemonKnight] PackageVersion OnLoad: pull tip hosts (want Id=" + Id
                    + "; tip/main FIRST — never HardTip-first breakglass).");

                ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12;
                string tip = TryResolveTipSha() ?? "main";
                string[] rels =
                {
                    "Assets/Editor/DemonKnightTipEscape.cs",
                    "Assets/Scripts/UI/SomersetPewLayout.cs",
                    // Applier embed host FIRST — CS0103 hard fix under stale Sync.
                    "Assets/Scripts/Data/GameDataTypes.cs",
                    "Assets/Scripts/UI/SomersetChurchUI.cs",
                    "Assets/Scripts/UI/SceneBackgroundApplier.cs",
                    "Assets/Scripts/UI/ClassSpritePoolApplier.cs",
                    "Assets/Editor/ArthurianSyncFromGitHub.cs",
                    "Assets/Editor/ArthurianMenuBootstrap.cs",
                    "Assets/Editor/ArthurianArtArchitectWindow.cs",
                    "Assets/Editor/ArthurianStageLayoutProfileEnsure.cs",
                    "Assets/Editor/ArthurianArchitectBuildPersist.cs",
                    "Assets/Editor/DemonKnightPackageVersion.cs",
                };
                int ok = 0;
                foreach (string rel in rels)
                {
                    try
                    {
                        string body = DownloadTip(rel, tip);
                        if (string.IsNullOrEmpty(body) || body.Length < 80 || IsTiny404(body))
                            throw new Exception("bad body");
                        if (rel.EndsWith("DemonKnightPackageVersion.cs", StringComparison.OrdinalIgnoreCase))
                        {
                            if (body.Length < 500) throw new Exception("PackageVersion too small " + body.Length);
                            if (body.IndexOf(Id, StringComparison.Ordinal) < 0)
                                throw new Exception("missing tip Id " + Id);
                            if (body.IndexOf("Id = \"architect-breakglass-hosts\"", StringComparison.Ordinal) >= 0)
                                throw new Exception("rejected breakglass PackageVersion");
                        }
                        if (rel.EndsWith("GameDataTypes.cs", StringComparison.OrdinalIgnoreCase)
                            && (body.IndexOf("class SceneBackgroundApplier", StringComparison.Ordinal) < 0
                                || body.IndexOf("class ClassSpritePoolApplier", StringComparison.Ordinal) < 0
                                || body.IndexOf("class LegendPewPortraitApplier", StringComparison.Ordinal) < 0
                                || body.IndexOf("class BattleGoodEvilSpritePoolApplier", StringComparison.Ordinal) < 0
                                || body.IndexOf("class BattleGoodEvilSpritePoolProfile", StringComparison.Ordinal) < 0))
                            throw new Exception("GameDataTypes missing embedded Appliers");
                        if (rel.EndsWith("SomersetChurchUI.cs", StringComparison.OrdinalIgnoreCase)
                            && (body.IndexOf("class SomersetChurchUI", StringComparison.Ordinal) < 0
                                || body.IndexOf("520f, 560f", StringComparison.Ordinal) < 0
                                || body.IndexOf("TrainATK", StringComparison.Ordinal) < 0))
                            throw new Exception("SomersetChurchUI missing tip church markers");
                        // Never re-land full standalone Applier bodies (CS0101 vs GameDataTypes embed).
                        if (rel.EndsWith("SceneBackgroundApplier.cs", StringComparison.OrdinalIgnoreCase)
                            || rel.EndsWith("ClassSpritePoolApplier.cs", StringComparison.OrdinalIgnoreCase))
                        {
                            if (body.IndexOf("class SceneBackgroundApplier", StringComparison.Ordinal) >= 0
                                || body.IndexOf("class ClassSpritePoolApplier", StringComparison.Ordinal) >= 0)
                                throw new Exception("rejected full Applier standalone (want stub → GameDataTypes embed)");
                            if (body.IndexOf("GameDataTypes.cs", StringComparison.Ordinal) < 0)
                                throw new Exception("Applier stub missing GameDataTypes embed marker");
                        }
                        string dest = Path.Combine(root, rel.Replace('/', Path.DirectorySeparatorChar));
                        Directory.CreateDirectory(Path.GetDirectoryName(dest)!);
                        try
                        {
                            if (File.Exists(dest))
                            {
                                File.SetAttributes(dest, FileAttributes.Normal);
                                File.Delete(dest);
                            }
                        }
                        catch { /* ignore */ }
                        File.WriteAllText(dest, body);
                        ok++;
                        Debug.Log($"[DemonKnight] PackageVersion OnLoad wrote {rel} ({body.Length} bytes)");
                    }
                    catch (Exception ex)
                    {
                        Debug.LogWarning($"[DemonKnight] PackageVersion OnLoad miss {rel}: {ex.Message}");
                    }
                }
                // Belt: stub any leftover full Applier standalones after tip pull.
                try { ArthurianSyncFromGitHub.StubApplierStandalonesOver(root, null); } catch { /* Sync type may be stale */ }
                if (ok > 0) AssetDatabase.Refresh();
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[DemonKnight] PackageVersion OnLoad skipped: " + ex.Message);
            }
        };
    }

    /// <summary>
    /// CS0436: type in both Assembly-CSharp and Assembly-CSharp-Editor when a copy
    /// lands under Assets/Scripts (or elsewhere). Keep only Assets/Editor/.
    /// </summary>
    public static int NukeDuplicatePackageVersionOutsideEditor()
    {
        int n = 0;
        try
        {
            string assets = Application.dataPath;
            string canonical = Path.GetFullPath(Path.Combine(assets, "Editor", "DemonKnightPackageVersion.cs"));
            foreach (string path in Directory.GetFiles(assets, "DemonKnightPackageVersion.cs", SearchOption.AllDirectories))
            {
                if (path.EndsWith(".meta", StringComparison.OrdinalIgnoreCase)) continue;
                string full = Path.GetFullPath(path);
                if (string.Equals(full, canonical, StringComparison.OrdinalIgnoreCase)) continue;
                try
                {
                    File.SetAttributes(full, FileAttributes.Normal);
                    File.Delete(full);
                    string meta = full + ".meta";
                    if (File.Exists(meta)) File.Delete(meta);
                    n++;
                    Debug.LogWarning("[DemonKnight] Deleted duplicate PackageVersion (CS0436): " + full);
                }
                catch (Exception ex)
                {
                    Debug.LogWarning("[DemonKnight] Could not delete duplicate PackageVersion " + full + ": " + ex.Message);
                }
            }
            if (n > 0) AssetDatabase.Refresh();
        }
        catch (Exception ex)
        {
            Debug.LogWarning("[DemonKnight] NukeDuplicatePackageVersionOutsideEditor: " + ex.Message);
        }
        return n;
    }

    static bool ContainsStaleMarker(string body)
    {
        if (string.IsNullOrEmpty(body)) return true;
        string[] stale =
        {
            "architect-breakglass-hosts",
            "architect-disk-tip-proof",
            "architect-force-tip-ui",
            "runtime-single-sprite",
            "single-sprite-play-wipe",
            "single-sprite-auto-wipe",
            "spliced-sprite-pews",
            "spliced-sprite-escape",
            "architect-no-bootstrap-clear",
            "breakglass-local-wipe",
            "pew-3rows-chroma-scale",
            "church-bg-pew-preview",
            "fix-seatshow-cs0436",
            "assign-only-pew-drag",
            "legend-unlocks-meta",
            "hidden-potential-bloodline",
            "architect-persist-scene-bg",
            "fix-scene-bg-applier-cs0103",
            "class-sprite-pools",
            "force-sync-appliers-hardfix",
            "sheet-splicer-chroma-despill",
            "fix-wave-advance-stuck",
            "legends-fullwidth-column",
            "fix-hud-wave-match",
            "merlin-lore-narrative",
            "lore-boss-wave-order",
            "legends-lore-portrait",
            "architect-legend-pew-portraits",
            "fix-architect-pool-apply",
            "sprite-outline-highlight",
            "church-train-scout-ux",
            "fix-somerset-church-ui-cs0246",
            "body-face-pool-build-persist",
        };
        foreach (string s in stale)
        {
            if (body.IndexOf("\"" + s + "\"", StringComparison.Ordinal) >= 0) return true;
            if (body.IndexOf("Id = \"" + s + "\"", StringComparison.Ordinal) >= 0) return true;
            if (body.IndexOf("UiBuildId = \"" + s + "\"", StringComparison.Ordinal) >= 0) return true;
            if (body.IndexOf("ExpectedPackageId = \"" + s + "\"", StringComparison.Ordinal) >= 0) return true;
        }
        return false;
    }

    public static string WipeModularPaperdollAssets()
    {
        var log = new StringBuilder();
        log.AppendLine("Package=" + Id);
        int libs = 0, clearedSlots = 0, deletedTex = 0;

        try
        {
            EnsureFolderPath("Assets/Art/Modular");
            EnsureFolderPath("Assets/Resources/Art/Modular");
            EnsureFolderPath(SplicedFolder);

            var paths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (AssetDatabase.LoadMainAssetAtPath(ArtLibPath) != null) paths.Add(ArtLibPath);
            if (AssetDatabase.LoadMainAssetAtPath(ResLibPath) != null) paths.Add(ResLibPath);
            foreach (string g in AssetDatabase.FindAssets("t:ScriptableObject"))
            {
                string p = AssetDatabase.GUIDToAssetPath(g);
                if (p.IndexOf("ModularSpriteLibrary", StringComparison.OrdinalIgnoreCase) >= 0)
                    paths.Add(p);
            }

            var spliced = LoadSplicedSprites();
            log.AppendLine("Spliced/Sprite count=" + spliced.Count);

            foreach (string path in paths)
            {
                var obj = AssetDatabase.LoadMainAssetAtPath(path);
                if (obj == null) continue;
                libs++;
                clearedSlots += WipeLibraryObject(obj, spliced);
                EditorUtility.SetDirty(obj);
                log.AppendLine("Wiped " + path);
            }

            deletedTex += DeleteMatchingTextures("Assets/Art/Modular/Placeholders", "Outfit_");
            deletedTex += DeleteMatchingTextures("Assets/Art/Modular/Placeholders", "Head_");
            deletedTex += DeleteMatchingTextures("Assets/Art/Modular/Placeholders", "Prop_");
            deletedTex += DeleteMatchingTextures("Assets/Art/Modular/Placeholders", "Pose_");
            deletedTex += DeleteMatchingTextures("Assets/Art/Modular/Placeholders", "Sprite_");
            deletedTex += DeleteMatchingTextures("Assets/Resources/Art/Modular/Placeholders", "Outfit_");
            deletedTex += DeleteMatchingTextures("Assets/Resources/Art/Modular/Placeholders", "Head_");
            deletedTex += DeleteMatchingTextures("Assets/Resources/Art/Modular/Placeholders", "Prop_");
            deletedTex += DeleteMatchingTextures("Assets/Resources/Art/Modular/Placeholders", "Pose_");
            deletedTex += DeleteMatchingTextures("Assets/Resources/Art/Modular/Placeholders", "Sprite_");

            AssetDatabase.SaveAssets();

            if (AssetDatabase.LoadMainAssetAtPath(ArtLibPath) != null)
            {
                if (AssetDatabase.LoadMainAssetAtPath(ResLibPath) != null)
                    AssetDatabase.DeleteAsset(ResLibPath);
                AssetDatabase.CopyAsset(ArtLibPath, ResLibPath);
                var res = AssetDatabase.LoadMainAssetAtPath(ResLibPath);
                if (res != null)
                {
                    WipeLibraryObject(res, spliced);
                    EditorUtility.SetDirty(res);
                }
                AssetDatabase.SaveAssets();
                log.AppendLine("Copied Art → Resources " + ResLibPath);
            }

            AssetDatabase.Refresh();
            log.AppendLine("libs=" + libs + " clearedSlots≈" + clearedSlots + " deletedPlaceholderTex=" + deletedTex);
            if (spliced.Count == 0)
                log.AppendLine("Library empty of splices — pews use solid color (NOT Pose_/Outfit_ shapes). Save Splices Tab 1.");
            else
                log.AppendLine("Pew library uses Spliced/Sprite only. New Game → body=<spliced> outfit=NULL.");
        }
        catch (Exception ex)
        {
            log.AppendLine("WIPE ERROR: " + ex.Message);
            Debug.LogWarning("[DemonKnight] WipeModularPaperdollAssets: " + ex.Message);
        }

        LastWipeReport = log.ToString().TrimEnd();
        return LastWipeReport;
    }

    public static string WipeStatusForDiagnose()
    {
        int outfits = 0, heads = 0, poses = 0, legacyClips = 0, placeholders = 0;
        try
        {
            var res = AssetDatabase.LoadMainAssetAtPath(ResLibPath);
            if (res == null)
                return "Modular wipe status: Resources library MISSING\nLast wipe: " + LastWipeReport;
            var so = new SerializedObject(res);
            poses = CountFilledArray(so.FindProperty("basePoses"));
            outfits = CountFilledArray(so.FindProperty("outfits"));
            heads = CountFilledHeads(so.FindProperty("heads"));
            legacyClips = CountLegacyAnimClips(so.FindProperty("animClips"));
            placeholders = CountPlaceholderBasePoses(so.FindProperty("basePoses"));
        }
        catch (Exception ex)
        {
            return "Modular wipe status: ERROR " + ex.Message + "\nLast wipe: " + LastWipeReport;
        }

        string ok = (outfits == 0 && heads == 0 && legacyClips == 0 && placeholders == 0)
            ? "CLEAN"
            : "DIRTY (Pose_/Outfit_/Head_ residue)";
        return "Modular wipe status: " + ok + " — splicedSprites=" + poses + " outfits=" + outfits
               + " heads=" + heads + " legacyClips=" + legacyClips + " placeholderPoses=" + placeholders + "\n"
               + "Last wipe: " + LastWipeReport;
    }

    static List<Sprite> LoadSplicedSprites()
    {
        var list = new List<Sprite>();
        if (!AssetDatabase.IsValidFolder(SplicedFolder)) return list;
        string[] guids = AssetDatabase.FindAssets("t:Sprite", new[] { SplicedFolder });
        var seen = new HashSet<string>();
        foreach (string g in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(g);
            if (!seen.Add(path)) continue;
            var sp = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (sp == null) continue;
            if (IsPlaceholderName(sp.name)) continue;
            list.Add(sp);
        }
        list.Sort((a, b) => string.CompareOrdinal(a.name, b.name));
        return list;
    }

    public static bool IsPlaceholderName(string name)
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

    static int WipeLibraryObject(UnityEngine.Object obj, List<Sprite> spliced)
    {
        int n = 0;
        var so = new SerializedObject(obj);
        n += ClearSpriteArray(so.FindProperty("outfits"));
        n += ClearHeadArray(so.FindProperty("heads"));
        n += ClearSpriteArray(so.FindProperty("props"));
        n += StripLegacyAnimClips(so.FindProperty("animClips"));

        var poses = so.FindProperty("basePoses");
        if (poses != null && poses.isArray)
        {
            if (spliced != null && spliced.Count > 0)
            {
                poses.arraySize = spliced.Count;
                for (int i = 0; i < spliced.Count; i++)
                    poses.GetArrayElementAtIndex(i).objectReferenceValue = spliced[i];
                n += spliced.Count;
            }
            else
            {
                for (int i = 0; i < poses.arraySize; i++)
                {
                    var sp = poses.GetArrayElementAtIndex(i).objectReferenceValue as Sprite;
                    if (sp != null && IsPlaceholderName(sp.name))
                    {
                        poses.GetArrayElementAtIndex(i).objectReferenceValue = null;
                        n++;
                    }
                }
                bool any = false;
                for (int i = 0; i < poses.arraySize; i++)
                    if (poses.GetArrayElementAtIndex(i).objectReferenceValue != null) { any = true; break; }
                if (!any) poses.arraySize = 0;
            }
        }

        so.ApplyModifiedPropertiesWithoutUndo();
        return n;
    }

    static int ClearSpriteArray(SerializedProperty prop)
    {
        if (prop == null || !prop.isArray) return 0;
        int had = 0;
        for (int i = 0; i < prop.arraySize; i++)
            if (prop.GetArrayElementAtIndex(i).objectReferenceValue != null) had++;
        prop.arraySize = 0;
        return had;
    }

    static int ClearHeadArray(SerializedProperty prop)
    {
        if (prop == null || !prop.isArray) return 0;
        int had = 0;
        for (int i = 0; i < prop.arraySize; i++)
        {
            var sp = prop.GetArrayElementAtIndex(i)?.FindPropertyRelative("sprite");
            if (sp != null && sp.objectReferenceValue != null) had++;
        }
        prop.arraySize = 0;
        return had;
    }

    static int StripLegacyAnimClips(SerializedProperty prop)
    {
        if (prop == null || !prop.isArray) return 0;
        int removed = 0;
        for (int i = prop.arraySize - 1; i >= 0; i--)
        {
            var cat = prop.GetArrayElementAtIndex(i)?.FindPropertyRelative("category");
            string c = cat != null ? cat.stringValue : "";
            if (string.Equals(c, "Sprite", StringComparison.OrdinalIgnoreCase)) continue;
            prop.DeleteArrayElementAtIndex(i);
            removed++;
        }
        return removed;
    }

    static int CountFilledArray(SerializedProperty prop)
    {
        if (prop == null || !prop.isArray) return 0;
        int n = 0;
        for (int i = 0; i < prop.arraySize; i++)
            if (prop.GetArrayElementAtIndex(i).objectReferenceValue != null) n++;
        return n;
    }

    static int CountFilledHeads(SerializedProperty prop)
    {
        if (prop == null || !prop.isArray) return 0;
        int n = 0;
        for (int i = 0; i < prop.arraySize; i++)
        {
            var sp = prop.GetArrayElementAtIndex(i)?.FindPropertyRelative("sprite");
            if (sp != null && sp.objectReferenceValue != null) n++;
        }
        return n;
    }

    static int CountLegacyAnimClips(SerializedProperty prop)
    {
        if (prop == null || !prop.isArray) return 0;
        int n = 0;
        for (int i = 0; i < prop.arraySize; i++)
        {
            var cat = prop.GetArrayElementAtIndex(i)?.FindPropertyRelative("category");
            string c = cat != null ? cat.stringValue : "";
            if (!string.Equals(c, "Sprite", StringComparison.OrdinalIgnoreCase)) n++;
        }
        return n;
    }

    static int CountPlaceholderBasePoses(SerializedProperty prop)
    {
        if (prop == null || !prop.isArray) return 0;
        int n = 0;
        for (int i = 0; i < prop.arraySize; i++)
        {
            var sp = prop.GetArrayElementAtIndex(i).objectReferenceValue as Sprite;
            if (sp != null && IsPlaceholderName(sp.name)) n++;
        }
        return n;
    }

    static int DeleteMatchingTextures(string folder, string namePrefix)
    {
        if (!AssetDatabase.IsValidFolder(folder)) return 0;
        int n = 0;
        string[] guids = AssetDatabase.FindAssets(namePrefix, new[] { folder });
        foreach (string g in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(g);
            string file = Path.GetFileName(path);
            if (file == null || !file.StartsWith(namePrefix, StringComparison.OrdinalIgnoreCase)) continue;
            if (AssetDatabase.DeleteAsset(path)) n++;
        }
        return n;
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

    static bool IsTiny404(string body)
    {
        if (string.IsNullOrEmpty(body)) return true;
        if (body.Length >= 512) return false;
        string t = body.TrimStart();
        return t.StartsWith("404", StringComparison.Ordinal)
               || t.IndexOf("404: Not Found", StringComparison.OrdinalIgnoreCase) >= 0
               || t.StartsWith("<!DOCTYPE", StringComparison.OrdinalIgnoreCase)
               || t.StartsWith("<html", StringComparison.OrdinalIgnoreCase);
    }

    static string TryResolveTipSha()
    {
        try
        {
            using var wc = new WebClient();
            wc.CachePolicy = new RequestCachePolicy(RequestCacheLevel.BypassCache);
            wc.Headers.Add("User-Agent", "DemonKnight-PackageVersion-OnLoad/" + Id);
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

    static string DownloadTip(string rel, string tip)
    {
        string bust = "t=" + DateTime.UtcNow.Ticks;
        string[] shas = { tip, "main", HardTipSha };
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
                    wc.Headers.Add("User-Agent", "DemonKnight-PackageVersion-OnLoad/" + Id);
                    wc.Headers.Add("Cache-Control", "no-cache, no-store");
                    string body = wc.DownloadString(url);
                    if (IsTiny404(body) || body.Length <= 80) { last = new Exception("tiny"); continue; }
                    if (rel.EndsWith("DemonKnightPackageVersion.cs", StringComparison.OrdinalIgnoreCase)
                        && body.IndexOf("Id = \"architect-breakglass-hosts\"", StringComparison.Ordinal) >= 0)
                    {
                        last = new Exception("skip breakglass PackageVersion");
                        continue;
                    }
                    return body;
                }
                catch (Exception ex) { last = ex; }
            }
        }
        throw last ?? new Exception("download failed " + rel);
    }
}
#endif
