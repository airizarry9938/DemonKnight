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
    public const string Id = "architect-disk-tip-proof";
    public const string GitHint = "fix-404-false-positive-force-architect-disk";
    public const string BuiltUtc = "2026-11-22T02:39:47Z";

    const string Repo = "airizarry9938/DemonKnight";
    const string RawBase = "https://raw.githubusercontent.com/" + Repo + "/";

    /// <summary>
    /// Chicken-egg: old FORCE Upgrade rejected Sync/Bootstrap downloads because
    /// body.Contains("404: Not Found") matched those files' own source. Once this
    /// PackageVersion tip compiles, pull Sync + Bootstrap + Architect with a
    /// length-gated 404 check so the real tip hosts can land.
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
                if (!File.Exists(syncPath)) return;
                string syncBody = File.ReadAllText(syncPath);
                bool stuck = syncBody.IndexOf("body.Contains(\"404: Not Found\")", StringComparison.Ordinal) >= 0
                             && syncBody.IndexOf("LooksLikeHttpFailureBody", StringComparison.Ordinal) < 0;
                bool archStale = true;
                string archPath = Path.Combine(root, "Assets", "Editor", "ArthurianArtArchitectWindow.cs");
                if (File.Exists(archPath))
                {
                    string a = File.ReadAllText(archPath);
                    archStale = a.IndexOf("Paper-Doll Unit Generator", StringComparison.Ordinal) >= 0
                                || a.IndexOf("enum SheetCategory", StringComparison.Ordinal) >= 0
                                || a.IndexOf("UiBuildId", StringComparison.Ordinal) < 0
                                || a.IndexOf("SpriteCategory", StringComparison.Ordinal) < 0;
                }
                if (!stuck && !archStale) return;

                Debug.LogWarning(
                    "[DemonKnight] PackageVersion OnLoad: pulling tip Sync/Bootstrap/Architect "
                    + "(stuck 404-false-positive and/or stale Architect).");

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
            int i = json.IndexOf("\"sha\"", StringComparison.Ordinal);
            if (i < 0) return null;
            int q1 = json.IndexOf('"', i + 5);
            int q2 = json.IndexOf('"', q1 + 1);
            if (q1 < 0 || q2 < 0) return null;
            string sha = json.Substring(q1 + 1, q2 - q1 - 1);
            return sha.Length == 40 ? sha : null;
        }
        catch { return null; }
    }

    static string DownloadTip(string rel, string tip)
    {
        string bust = "t=" + DateTime.UtcNow.Ticks;
        string[] urls =
        {
            RawBase + tip + "/" + rel + "?" + bust,
            RawBase + tip + "/" + rel,
            RawBase + "main/" + rel + "?" + bust,
            RawBase + "main/" + rel,
            "https://cdn.jsdelivr.net/gh/" + Repo + "@" + tip + "/" + rel + "?" + bust,
            "https://cdn.jsdelivr.net/gh/" + Repo + "@main/" + rel + "?" + bust,
        };
        Exception last = null;
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
        throw last ?? new Exception("download failed " + rel);
    }
}
#endif
