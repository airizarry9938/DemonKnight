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
    public const string Id = "single-sprite-auto-wipe";
    public const string GitHint = "onload-auto-wipe-modular-no-menu-required";
    public const string BuiltUtc = "2026-11-22T03:35:50Z";

    /// <summary>Fallback SHA only — OnLoad prefers API tip + raw@main first.</summary>
    public const string HardTipSha = "a32a5bc7e8169ea0dfad42caaf04fee85b583432";

    const string Repo = "airizarry9938/DemonKnight";
    const string RawBase = "https://raw.githubusercontent.com/" + Repo + "/";
    const string ArtLibPath = "Assets/Art/Modular/ModularSpriteLibrary.asset";
    const string ResLibPath = "Assets/Resources/Art/Modular/ModularSpriteLibrary.asset";

    /// <summary>Last wipe summary for Diagnose (session).</summary>
    public static string LastWipeReport { get; private set; } = "(wipe not run this session)";

    /// <summary>
    /// After this file compiles (FORCE / paste / tip):
    /// 1) Always wipe ModularSpriteLibrary paperdoll residue (no Art Tools menu required).
    /// 2) Pull Sync/Bootstrap/Architect from tip/main when hosts still stale.
    /// </summary>
    [InitializeOnLoadMethod]
    static void DkForceTipHostsAndWipeLibrary()
    {
        EditorApplication.delayCall += () =>
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            try
            {
                // 1) Library wipe first — works even when MenuBootstrap tip menus never landed.
                LastWipeReport = WipeModularPaperdollAssets();
                Debug.Log("[DemonKnight] PackageVersion OnLoad library wipe:\n" + LastWipeReport);

                string root = Directory.GetParent(Application.dataPath)!.FullName;
                string syncPath = Path.Combine(root, "Assets", "Editor", "ArthurianSyncFromGitHub.cs");
                string archPath = Path.Combine(root, "Assets", "Editor", "ArthurianArtArchitectWindow.cs");
                string bootPath = Path.Combine(root, "Assets", "Editor", "ArthurianMenuBootstrap.cs");
                bool need = !File.Exists(syncPath);
                if (File.Exists(syncPath))
                {
                    string syncBody = File.ReadAllText(syncPath);
                    need = syncBody.IndexOf("LooksLikeHttpFailureBody", StringComparison.Ordinal) < 0
                           || (syncBody.IndexOf("body.Contains(\"404: Not Found\")", StringComparison.Ordinal) >= 0
                               && syncBody.IndexOf("LooksLikeHttpFailureBody", StringComparison.Ordinal) < 0)
                           || syncBody.IndexOf("HardTipCommitSha", StringComparison.Ordinal) < 0
                           || syncBody.IndexOf("StalePackageIds", StringComparison.Ordinal) < 0
                           || syncBody.IndexOf(Id, StringComparison.Ordinal) < 0
                           || syncBody.IndexOf("ClearPaperdollLibraryInternal", StringComparison.Ordinal) < 0;
                }
                if (File.Exists(bootPath))
                {
                    string boot = File.ReadAllText(bootPath);
                    if (boot.IndexOf("Clear Paperdoll Library", StringComparison.Ordinal) < 0
                        || boot.IndexOf(Id, StringComparison.Ordinal) < 0)
                        need = true;
                }
                else need = true;

                bool archStale = !File.Exists(archPath);
                if (File.Exists(archPath))
                {
                    string a = File.ReadAllText(archPath);
                    archStale = a.IndexOf("Paper-Doll Unit Generator", StringComparison.Ordinal) >= 0
                                || a.IndexOf("enum SheetCategory", StringComparison.Ordinal) >= 0
                                || a.IndexOf("UiBuildId", StringComparison.Ordinal) < 0
                                || a.IndexOf("SpriteCategory", StringComparison.Ordinal) < 0
                                || a.IndexOf(Id, StringComparison.Ordinal) < 0
                                || a.IndexOf("architect-disk-tip-proof", StringComparison.Ordinal) >= 0
                                || a.IndexOf("architect-breakglass-hosts", StringComparison.Ordinal) >= 0
                                || a.IndexOf("runtime-single-sprite\"", StringComparison.Ordinal) >= 0
                                || a.IndexOf("single-sprite-play-wipe\"", StringComparison.Ordinal) >= 0;
                }
                if (!need && !archStale) return;

                Debug.LogWarning(
                    "[DemonKnight] PackageVersion OnLoad: pull tip Sync/Bootstrap/Architect "
                    + "(want Id=" + Id + "; prefer API tip + main, HardTip last).");

                ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12;
                string tip = TryResolveTipSha() ?? "main";
                string[] rels =
                {
                    "Assets/Editor/ArthurianSyncFromGitHub.cs",
                    "Assets/Editor/ArthurianMenuBootstrap.cs",
                    "Assets/Editor/ArthurianArtArchitectWindow.cs",
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
                        if (rel.EndsWith("DemonKnightPackageVersion.cs", StringComparison.OrdinalIgnoreCase)
                            && body.Length < 500)
                            throw new Exception("PackageVersion too small " + body.Length);
                        if (rel.EndsWith("DemonKnightPackageVersion.cs", StringComparison.OrdinalIgnoreCase)
                            && body.IndexOf(Id, StringComparison.Ordinal) < 0)
                            throw new Exception("PackageVersion missing tip Id " + Id);
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
                if (ok > 0) AssetDatabase.Refresh();
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[DemonKnight] PackageVersion OnLoad skipped: " + ex.Message);
            }
        };
    }

    /// <summary>
    /// Self-contained wipe — no MenuBootstrap / ClearPaperdollLayersKeepSprites required.
    /// Clears Outfit/Head/Prop arrays + non-Sprite anim clips on every ModularSpriteLibrary asset,
    /// deletes Outfit_/Head_/Prop_ placeholder textures, copies Art → Resources.
    /// </summary>
    public static string WipeModularPaperdollAssets()
    {
        var log = new StringBuilder();
        log.AppendLine("Package=" + Id);
        int libs = 0, clearedSlots = 0, deletedTex = 0;

        try
        {
            EnsureFolderPath("Assets/Art/Modular");
            EnsureFolderPath("Assets/Resources/Art/Modular");

            var paths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (AssetDatabase.LoadMainAssetAtPath(ArtLibPath) != null) paths.Add(ArtLibPath);
            if (AssetDatabase.LoadMainAssetAtPath(ResLibPath) != null) paths.Add(ResLibPath);
            foreach (string g in AssetDatabase.FindAssets("t:ScriptableObject"))
            {
                string p = AssetDatabase.GUIDToAssetPath(g);
                if (p.IndexOf("ModularSpriteLibrary", StringComparison.OrdinalIgnoreCase) >= 0)
                    paths.Add(p);
            }

            foreach (string path in paths)
            {
                var obj = AssetDatabase.LoadMainAssetAtPath(path);
                if (obj == null) continue;
                libs++;
                clearedSlots += WipeLibraryObject(obj);
                EditorUtility.SetDirty(obj);
                log.AppendLine("Wiped " + path);
            }

            deletedTex += DeleteMatchingTextures("Assets/Art/Modular/Placeholders", "Outfit_");
            deletedTex += DeleteMatchingTextures("Assets/Art/Modular/Placeholders", "Head_");
            deletedTex += DeleteMatchingTextures("Assets/Art/Modular/Placeholders", "Prop_");
            deletedTex += DeleteMatchingTextures("Assets/Resources/Art/Modular/Placeholders", "Outfit_");
            deletedTex += DeleteMatchingTextures("Assets/Resources/Art/Modular/Placeholders", "Head_");
            deletedTex += DeleteMatchingTextures("Assets/Resources/Art/Modular/Placeholders", "Prop_");

            AssetDatabase.SaveAssets();

            // Force Resources copy from Art (play reads Resources).
            if (AssetDatabase.LoadMainAssetAtPath(ArtLibPath) != null)
            {
                if (AssetDatabase.LoadMainAssetAtPath(ResLibPath) != null)
                    AssetDatabase.DeleteAsset(ResLibPath);
                AssetDatabase.CopyAsset(ArtLibPath, ResLibPath);
                var res = AssetDatabase.LoadMainAssetAtPath(ResLibPath);
                if (res != null)
                {
                    WipeLibraryObject(res);
                    EditorUtility.SetDirty(res);
                }
                AssetDatabase.SaveAssets();
                log.AppendLine("Copied Art → Resources " + ResLibPath);
            }
            else if (AssetDatabase.LoadMainAssetAtPath(ResLibPath) != null)
            {
                // No Art copy — wipe Resources in place.
                var res = AssetDatabase.LoadMainAssetAtPath(ResLibPath);
                if (res != null)
                {
                    clearedSlots += WipeLibraryObject(res);
                    EditorUtility.SetDirty(res);
                    AssetDatabase.SaveAssets();
                }
            }

            AssetDatabase.Refresh();
            log.AppendLine($"libs={libs} clearedSlots≈{clearedSlots} deletedPlaceholderTex={deletedTex}");
            log.AppendLine("outfit/head/prop arrays emptied; non-Sprite anim clips removed.");
            log.AppendLine("Stop Play → New Game. Bind log must show outfit=NULL.");
        }
        catch (Exception ex)
        {
            log.AppendLine("WIPE ERROR: " + ex.Message);
            Debug.LogWarning("[DemonKnight] WipeModularPaperdollAssets: " + ex.Message);
        }

        LastWipeReport = log.ToString().TrimEnd();
        return LastWipeReport;
    }

    /// <summary>Diagnose one-liner: Resources outfits/heads + LastWipeReport head.</summary>
    public static string WipeStatusForDiagnose()
    {
        int outfits = 0, heads = 0, poses = 0, legacyClips = 0;
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
        }
        catch (Exception ex)
        {
            return "Modular wipe status: ERROR " + ex.Message + "\nLast wipe: " + LastWipeReport;
        }

        string ok = (outfits == 0 && heads == 0 && legacyClips == 0) ? "CLEAN" : "DIRTY (need wipe)";
        return $"Modular wipe status: {ok} — sprites(basePoses)={poses} outfits={outfits} heads={heads} legacyClips={legacyClips}\n"
               + "Last wipe: " + LastWipeReport;
    }

    static int WipeLibraryObject(UnityEngine.Object obj)
    {
        int n = 0;
        var so = new SerializedObject(obj);
        n += ClearSpriteArray(so.FindProperty("outfits"));
        n += ClearHeadArray(so.FindProperty("heads"));
        n += ClearSpriteArray(so.FindProperty("props"));
        n += StripLegacyAnimClips(so.FindProperty("animClips"));
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
            var el = prop.GetArrayElementAtIndex(i);
            var sp = el?.FindPropertyRelative("sprite");
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
            var el = prop.GetArrayElementAtIndex(i);
            var cat = el?.FindPropertyRelative("category");
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
                    if (!IsTiny404(body) && body.Length > 80) return body;
                    last = new Exception("tiny/404 from " + url);
                }
                catch (Exception ex) { last = ex; }
            }
        }
        throw last ?? new Exception("download failed " + rel);
    }
}
#endif
