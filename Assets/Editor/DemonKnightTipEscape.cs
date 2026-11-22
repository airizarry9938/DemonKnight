#if UNITY_EDITOR
using System;
using System.IO;
using System.Net;
using System.Net.Cache;
using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>
/// One-click + OnLoad escape from architect-breakglass-hosts / architect-disk-tip-proof.
/// Force Sync lands this file via tip SHA (ScriptPaths) even when HardTip host overwrite
/// keeps stale Sync. Always pulls hosts from API tip + raw@main — never HardTip.
/// </summary>
public static class DemonKnightTipEscape
{
    public const string TipPackageId = "lore-boss-wave-order";
    const string Repo = "airizarry9938/DemonKnight";
    const string RawBase = "https://raw.githubusercontent.com/" + Repo + "/";
    const string PrefsKey = "DemonKnight.TipEscape.LastOk." + TipPackageId;

    static readonly string[] HostRels =
    {
        "Assets/Editor/DemonKnightPackageVersion.cs",
        // PewLayout before Architect — SeatShowsSprite must exist if Architect still calls it.
        "Assets/Scripts/UI/SomersetPewLayout.cs",
        // Embed host BEFORE stubs — Appliers live in GameDataTypes (force-sync-appliers-hardfix marker kept).
        "Assets/Scripts/Data/GameDataTypes.cs",
        "Assets/Scripts/UI/SceneBackgroundApplier.cs",
        "Assets/Scripts/UI/ClassSpritePoolApplier.cs",
        "Assets/Scripts/UI/SomersetChurchUI.cs",
        "Assets/Scripts/UI/LegendCodexUI.cs",
        "Assets/Scripts/UI/TownSplitUIController.cs",
        "Assets/Editor/ArthurianSyncFromGitHub.cs",
        "Assets/Editor/ArthurianMenuBootstrap.cs",
        "Assets/Editor/ArthurianArtArchitectWindow.cs",
        "Assets/Editor/ArthurianStageLayoutProfileEnsure.cs",
        "Assets/Editor/ArthurianFixMisplacedScripts.cs",
        "Assets/Editor/DemonKnightTipEscape.cs",
        "Assets/Scripts/UI/PaperDollSeatView.cs",
        "Assets/Scripts/UI/TownsfolkVisualApplier.cs",
    };

    [MenuItem("Arthurian/!!! Escape to Tip (lore-boss-wave-order)", false, 0)]
    public static void EscapeMenu()
    {
        string report = EscapeNow(force: true);
        EditorUtility.DisplayDialog(
            "Escape to Tip",
            report + "\n\nWait for compile spinner → Arthurian → Diagnose Project.\n"
            + "Must show Package compiled AND disk: " + TipPackageId,
            "OK");
    }

    [InitializeOnLoadMethod]
    static void OnLoadEscape()
    {
        EditorApplication.delayCall += () =>
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            try
            {
                if (!NeedsEscape()) return;
                Debug.LogWarning(
                    "[DemonKnight] TipEscape OnLoad: hosts still breakglass/disk-tip-proof/stale — "
                    + "pulling " + TipPackageId + " from raw@main (never HardTip).");
                EscapeNow(force: false);
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[DemonKnight] TipEscape OnLoad: " + ex.Message);
            }
        };
    }

    public static bool NeedsEscape()
    {
        string root = Directory.GetParent(Application.dataPath)!.FullName;
        string pv = Path.Combine(root, "Assets", "Editor", "DemonKnightPackageVersion.cs");
        string arch = Path.Combine(root, "Assets", "Editor", "ArthurianArtArchitectWindow.cs");
        string sync = Path.Combine(root, "Assets", "Editor", "ArthurianSyncFromGitHub.cs");
        if (!File.Exists(pv) || !File.Exists(arch) || !File.Exists(sync)) return true;

        string pvBody = File.ReadAllText(pv);
        string archBody = File.ReadAllText(arch);
        string syncBody = File.ReadAllText(sync);

        if (pvBody.IndexOf("Id = \"" + TipPackageId + "\"", StringComparison.Ordinal) < 0) return true;
        if (archBody.IndexOf(TipPackageId, StringComparison.Ordinal) < 0) return true;
        if (syncBody.IndexOf(TipPackageId, StringComparison.Ordinal) < 0) return true;
        // CS0117: tip Architect must never call PackageVersion.Wipe*
        if (archBody.IndexOf("WipeModularPaperdollAssets", StringComparison.Ordinal) >= 0) return true;

        // Compiled vs disk: if compiled type exists and Id differs, still need refresh.
        try
        {
            var verType = Type.GetType("DemonKnightPackageVersion")
                          ?? Type.GetType("DemonKnightPackageVersion, Assembly-CSharp-Editor");
            string compiled = verType?.GetField("Id")?.GetValue(null) as string;
            if (!string.IsNullOrEmpty(compiled)
                && !string.Equals(compiled, TipPackageId, StringComparison.Ordinal))
                return true;
        }
        catch { /* ignore */ }

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
            "assign-only-runtime",
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
        };
        foreach (string s in stale)
        {
            if (pvBody.IndexOf("Id = \"" + s + "\"", StringComparison.Ordinal) >= 0) return true;
            if (archBody.IndexOf("UiBuildId = \"" + s + "\"", StringComparison.Ordinal) >= 0) return true;
        }
        return false;
    }

    public static string EscapeNow(bool force)
    {
        var log = new StringBuilder();
        log.AppendLine("TipEscape → " + TipPackageId + " (raw tip/main ONLY, never HardTip)");
        if (!force && !NeedsEscape())
        {
            log.AppendLine("Already on tip — skip.");
            return log.ToString();
        }

        ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12;
        string tip = TryResolveTipSha() ?? "main";
        string root = Directory.GetParent(Application.dataPath)!.FullName;
        int ok = 0;
        foreach (string rel in HostRels)
        {
            try
            {
                string body = DownloadTipMainOnly(rel, tip);
                if (string.IsNullOrEmpty(body) || body.Length < 80 || LooksTiny404(body))
                    throw new Exception("bad body");
                if (rel.EndsWith("DemonKnightPackageVersion.cs", StringComparison.OrdinalIgnoreCase))
                {
                    if (body.Length < 500) throw new Exception("PackageVersion too small");
                    if (body.IndexOf("Id = \"" + TipPackageId + "\"", StringComparison.Ordinal) < 0)
                        throw new Exception("PackageVersion missing " + TipPackageId);
                    if (body.IndexOf("Id = \"architect-breakglass-hosts\"", StringComparison.Ordinal) >= 0
                        || body.IndexOf("Id = \"architect-disk-tip-proof\"", StringComparison.Ordinal) >= 0)
                        throw new Exception("rejected stale PackageVersion");
                }
                if (rel.EndsWith("ArthurianArtArchitectWindow.cs", StringComparison.OrdinalIgnoreCase)
                    && body.IndexOf("WipeModularPaperdollAssets", StringComparison.Ordinal) >= 0)
                    throw new Exception("Architect still calls PackageVersion.Wipe* (CS0117)");
                if (rel.EndsWith("GameDataTypes.cs", StringComparison.OrdinalIgnoreCase)
                    && (body.IndexOf("class SceneBackgroundApplier", StringComparison.Ordinal) < 0
                        || body.IndexOf("class ClassSpritePoolApplier", StringComparison.Ordinal) < 0))
                    throw new Exception("GameDataTypes missing embedded Appliers");
                if (rel.EndsWith("SceneBackgroundApplier.cs", StringComparison.OrdinalIgnoreCase)
                    || rel.EndsWith("ClassSpritePoolApplier.cs", StringComparison.OrdinalIgnoreCase))
                {
                    if (body.IndexOf("class SceneBackgroundApplier", StringComparison.Ordinal) >= 0
                        || body.IndexOf("class ClassSpritePoolApplier", StringComparison.Ordinal) >= 0)
                        throw new Exception("rejected full Applier standalone (want stub)");
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
                log.AppendLine($"OK {rel} ({body.Length} B)");
                Debug.Log($"[DemonKnight] TipEscape wrote {rel} ({body.Length} bytes)");
            }
            catch (Exception ex)
            {
                log.AppendLine($"MISS {rel}: {ex.Message}");
                Debug.LogWarning($"[DemonKnight] TipEscape miss {rel}: {ex.Message}");
            }
        }

        // Wipe pew library placeholders when PackageVersion tip compiled later; best-effort now.
        try
        {
            if (AssetDatabase.IsValidFolder("Assets/Art/Spliced/Sprite")
                || AssetDatabase.LoadMainAssetAtPath("Assets/Resources/Art/Modular/ModularSpriteLibrary.asset") != null)
            {
                ArthurianStageLayoutProfileEnsure.RebuildPewLibraryFromSplices();
                log.AppendLine("Pew library rebuild requested.");
            }
        }
        catch (Exception ex)
        {
            log.AppendLine("Pew rebuild later: " + ex.Message);
        }

        try
        {
            int nuked = DemonKnightPackageVersion.NukeDuplicatePackageVersionOutsideEditor();
            if (nuked > 0) log.AppendLine("Nuked " + nuked + " duplicate PackageVersion (CS0436).");
        }
        catch (Exception ex) { log.AppendLine("PV nuke: " + ex.Message); }

        AssetDatabase.Refresh();
        EditorPrefs.SetString(PrefsKey, DateTime.UtcNow.ToString("o"));
        log.AppendLine($"Wrote {ok}/{HostRels.Length}. Wait compile → Diagnose: compiled=disk={TipPackageId}.");
        return log.ToString();
    }

    static string TryResolveTipSha()
    {
        try
        {
            using var wc = new WebClient();
            wc.CachePolicy = new RequestCachePolicy(RequestCacheLevel.BypassCache);
            wc.Headers.Add("User-Agent", "DemonKnight-TipEscape/" + TipPackageId);
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

    static string DownloadTipMainOnly(string rel, string tip)
    {
        string bust = "t=" + DateTime.UtcNow.Ticks + "&r=" + Guid.NewGuid().ToString("N");
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
                    wc.Headers.Add("User-Agent", "DemonKnight-TipEscape/" + TipPackageId);
                    wc.Headers.Add("Cache-Control", "no-cache, no-store");
                    string body = wc.DownloadString(url);
                    if (!LooksTiny404(body) && body.Length > 80) return body;
                    last = new Exception("tiny/404");
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
}
#endif
