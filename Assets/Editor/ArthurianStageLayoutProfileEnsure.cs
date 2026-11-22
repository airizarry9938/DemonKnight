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
/// After DA-59 embed into GameDataTypes.cs, old assets keep a stale MonoScript GUID →
/// "referenced script is missing". We delete + recreate when Load fails.
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
    const string TipPackageId = "breakglass-local-wipe";
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
    };

    [MenuItem("Arthurian/Ensure Stage Layout Profile", false, 55)]
    public static void EnsureFromMenu()
    {
        var p = EnsureAsset();
        Selection.activeObject = p;
        EditorGUIUtility.PingObject(p);
        Debug.Log($"[DemonKnight] StageLayoutProfile ready → {AssetPath} " +
                  $"(pews={p.churchPewSlots.Count}, player={p.playerBattleSlots.Count}, enemy={p.enemyBattleSlots.Count}).");
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
            try
            {
                EnsureAsset();
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
            existing.EnsureSlotCounts();
            EditorUtility.SetDirty(existing);
            AssetDatabase.SaveAssets();
            return existing;
        }

        string full = Path.Combine(Directory.GetParent(Application.dataPath)!.FullName,
            AssetPath.Replace('/', Path.DirectorySeparatorChar));
        bool staleOnDisk = File.Exists(full) || AssetDatabase.LoadMainAssetAtPath(AssetPath) != null;
        if (File.Exists(full))
        {
            try
            {
                if (File.ReadAllText(full).IndexOf("c4a8f2e19b6d4078a5e31c0d9f7b2a64", StringComparison.OrdinalIgnoreCase) >= 0)
                    staleOnDisk = true;
            }
            catch { staleOnDisk = true; }
        }
        if (staleOnDisk)
        {
            Debug.LogWarning("[DemonKnight] Recreating StageLayoutProfile.asset (stale MonoScript GUID after embed).");
            AssetDatabase.DeleteAsset(AssetPath);
            if (File.Exists(full))
            {
                try { File.Delete(full); } catch { /* ignore */ }
                string meta = full + ".meta";
                if (File.Exists(meta)) try { File.Delete(meta); } catch { /* ignore */ }
            }
            AssetDatabase.Refresh();
        }

        var created = ScriptableObject.CreateInstance<StageLayoutProfile>();
        created.EnsureSlotCounts();
        AssetDatabase.CreateAsset(created, AssetPath);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        return AssetDatabase.LoadAssetAtPath<StageLayoutProfile>(AssetPath) ?? created;
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
            // Stale if Architect title still advertises an old UiBuildId.
            if (archBody.IndexOf("\"" + m + "\"", StringComparison.Ordinal) >= 0) return true;
            if (pvBody.IndexOf("Id = \"" + m + "\"", StringComparison.Ordinal) >= 0) return true;
        }
        if (File.Exists(sync))
        {
            string s = File.ReadAllText(sync);
            if (s.IndexOf(TipPackageId, StringComparison.Ordinal) < 0) return true;
            // HardTip-first without tip/main preference = breakglass trap.
            if (s.IndexOf("HardTip-first", StringComparison.Ordinal) >= 0
                && s.IndexOf("tip of main + branch tip FIRST", StringComparison.OrdinalIgnoreCase) < 0
                && s.IndexOf("API tip + raw@main FIRST", StringComparison.OrdinalIgnoreCase) < 0)
                return true;
        }
        return false;
    }

    public static string EscapeStaleHostsAndRebuildPewLibrary()
    {
        var log = new StringBuilder();
        log.AppendLine("Escape → tip package " + TipPackageId);
        ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12;
        string tip = TryResolveTipSha() ?? "main";
        string root = Directory.GetParent(Application.dataPath)!.FullName;
        string[] rels =
        {
            "Assets/Editor/DemonKnightTipEscape.cs",
            "Assets/Editor/DemonKnightPackageVersion.cs",
            "Assets/Editor/ArthurianSyncFromGitHub.cs",
            "Assets/Editor/ArthurianMenuBootstrap.cs",
            "Assets/Editor/ArthurianArtArchitectWindow.cs",
            "Assets/Editor/ArthurianStageLayoutProfileEnsure.cs",
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
                log.AppendLine($"Wrote {rel} ({body.Length} B)");
                Debug.Log($"[DemonKnight] EscapeBreakglass wrote {rel} ({body.Length} bytes)");
            }
            catch (Exception ex)
            {
                log.AppendLine($"MISS {rel}: {ex.Message}");
                Debug.LogWarning($"[DemonKnight] EscapeBreakglass miss {rel}: {ex.Message}");
            }
        }
        AssetDatabase.Refresh();
        log.AppendLine($"Hosts written: {ok}/{rels.Length}");
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
