#if UNITY_EDITOR
using System;
using System.IO;
using System.Net;
using System.Net.Cache;
using UnityEditor;
using UnityEngine;

/// <summary>Bump when shipping package updates so Diagnose can prove Unity loaded the new build.</summary>
public static class DemonKnightPackageVersion
{
    public const string Id = "architect-breakglass-hosts";
    public const string GitHint = "hard-tip-sha-reject-381B-breakglass-menu";
    public const string BuiltUtc = "2026-11-22T02:48:25Z";

    /// <summary>Hard tip known to contain LooksLikeHttpFailureBody Sync + OnLoad PackageVersion.</summary>
    public const string HardTipSha = "6e31cd9d28444881bbcbdcd3676720e4fd61ddd6";

    const string Repo = "airizarry9938/DemonKnight";
    const string RawBase = "https://raw.githubusercontent.com/" + Repo + "/";

    /// <summary>
    /// After this file compiles (even via manual paste / FORCE of PackageVersion only),
    /// pull Sync+Bootstrap+Architect from HardTipSha then main — never depends on stale Sync validate.
    /// </summary>
    [InitializeOnLoadMethod]
    static void DkForceTipHostsIfFalsePositiveStuck()
    {
        EditorApplication.delayCall += () =>
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            try
            {
                string root = Directory.GetParent(Application.dataPath)!.FullName;
                string syncPath = Path.Combine(root, "Assets", "Editor", "ArthurianSyncFromGitHub.cs");
                string archPath = Path.Combine(root, "Assets", "Editor", "ArthurianArtArchitectWindow.cs");
                bool need = !File.Exists(syncPath);
                if (File.Exists(syncPath))
                {
                    string syncBody = File.ReadAllText(syncPath);
                    need = syncBody.IndexOf("LooksLikeHttpFailureBody", StringComparison.Ordinal) < 0
                           || (syncBody.IndexOf("body.Contains(\"404: Not Found\")", StringComparison.Ordinal) >= 0
                               && syncBody.IndexOf("LooksLikeHttpFailureBody", StringComparison.Ordinal) < 0)
                           || syncBody.IndexOf("HardTipCommitSha", StringComparison.Ordinal) < 0;
                }
                bool archStale = !File.Exists(archPath);
                if (File.Exists(archPath))
                {
                    string a = File.ReadAllText(archPath);
                    archStale = a.IndexOf("Paper-Doll Unit Generator", StringComparison.Ordinal) >= 0
                                || a.IndexOf("enum SheetCategory", StringComparison.Ordinal) >= 0
                                || a.IndexOf("UiBuildId", StringComparison.Ordinal) < 0
                                || a.IndexOf("SpriteCategory", StringComparison.Ordinal) < 0
                                || a.IndexOf(Id, StringComparison.Ordinal) < 0;
                }
                // Also refresh hosts when our own Id is tip but Sync ExpectedPackageId lags.
                if (!need && !archStale && File.Exists(syncPath))
                {
                    string syncBody = File.ReadAllText(syncPath);
                    if (syncBody.IndexOf(Id, StringComparison.Ordinal) < 0)
                        need = true;
                }
                if (!need && !archStale) return;

                Debug.LogWarning(
                    "[DemonKnight] PackageVersion OnLoad: break-glass pull Sync/Bootstrap/Architect "
                    + "from HardTipSha=" + HardTipSha + " (+ main).");

                ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12;
                string tip = TryResolveTipSha() ?? HardTipSha;
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
                Debug.LogWarning("[DemonKnight] PackageVersion OnLoad host pull skipped: " + ex.Message);
            }
        };
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
            // Root commit sha only.
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
        // Hard tip FIRST (known good), then API tip, then main.
        string[] shas = { HardTipSha, tip, "main" };
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
