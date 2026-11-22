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
    public const string Id = "single-sprite-play-wipe";
    public const string GitHint = "main-first-force-wipe-modular-paperdoll-library";
    public const string BuiltUtc = "2026-11-22T03:06:51Z";

    /// <summary>Fallback SHA only — OnLoad prefers API tip + raw@main first.</summary>
    public const string HardTipSha = "c414818b5bb591cd644e0f70a1c7bb16903f9ccf";

    const string Repo = "airizarry9938/DemonKnight";
    const string RawBase = "https://raw.githubusercontent.com/" + Repo + "/";

    /// <summary>
    /// After this file compiles (FORCE / paste / tip), pull Sync+Bootstrap+Architect from
    /// tip/main whenever Architect/Sync still advertise a stale package Id
    /// (architect-disk-tip-proof, runtime-single-sprite, …).
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
                           || syncBody.IndexOf("HardTipCommitSha", StringComparison.Ordinal) < 0
                           || syncBody.IndexOf("StalePackageIds", StringComparison.Ordinal) < 0
                           || syncBody.IndexOf(Id, StringComparison.Ordinal) < 0;
                }
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
                                || a.IndexOf("runtime-single-sprite\"", StringComparison.Ordinal) >= 0;
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
        // API tip + main FIRST — HardTip last (avoids re-landing architect-disk-tip-proof).
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
