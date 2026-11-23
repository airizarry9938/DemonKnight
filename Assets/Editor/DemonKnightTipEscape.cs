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
    public const string TipPackageId = "fix-tipescape-profile-embeds";
    const string Repo = "airizarry9938/DemonKnight";
    const string RawBase = "https://raw.githubusercontent.com/" + Repo + "/";
    const string PrefsKey = "DemonKnight.TipEscape.LastOk." + TipPackageId;

    static readonly string[] HostRels =
    {
        "Assets/Editor/DemonKnightPackageVersion.cs",
        // PewLayout before Architect — SeatShowsSprite must exist if Architect still calls it.
        "Assets/Scripts/UI/SomersetPewLayout.cs",
        // Named MonoScript Profiles + shared types BEFORE GameDataTypes Appliers.
        // tip: fix-tipescape-profile-embeds — Force Sync wipe/never-pull → CS0246.
        "Assets/Scripts/Data/StageLayoutProfile.cs",
        "Assets/Scripts/Data/SceneBackgroundProfile.cs",
        "Assets/Scripts/Data/ClassSpritePoolProfile.cs",
        "Assets/Scripts/Data/LegendPewPortraitProfile.cs",
        "Assets/Scripts/Data/BattleGoodEvilSpritePoolProfile.cs",
        // Applier embed host — Appliers stay in GameDataTypes.
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
        "Assets/Editor/ArthurianArchitectBuildPersist.cs",
        "Assets/Editor/ArthurianFixMisplacedScripts.cs",
        "Assets/Editor/DemonKnightTipEscape.cs",
        "Assets/Scripts/UI/PaperDollSeatView.cs",
        "Assets/Scripts/UI/TownsfolkVisualApplier.cs",
    };

    [MenuItem("Arthurian/!!! Escape to Tip (fix-tipescape-profile-embeds)", false, 0)]
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
        string buildPersist = Path.Combine(root, "Assets", "Editor", "ArthurianArchitectBuildPersist.cs");
        if (!File.Exists(pv) || !File.Exists(arch) || !File.Exists(sync) || !File.Exists(buildPersist))
            return true;

        string pvBody = File.ReadAllText(pv);
        string archBody = File.ReadAllText(arch);
        string syncBody = File.ReadAllText(sync);

        if (pvBody.IndexOf("Id = \"" + TipPackageId + "\"", StringComparison.Ordinal) < 0) return true;
        if (archBody.IndexOf(TipPackageId, StringComparison.Ordinal) < 0) return true;
        if (syncBody.IndexOf(TipPackageId, StringComparison.Ordinal) < 0) return true;
        // CS0103 guard: Sync HardRestore + Architect embed SoftPromote belt.
        if (syncBody.IndexOf("HardRestoreArchitectBuildPersist", StringComparison.Ordinal) < 0) return true;
        if (archBody.IndexOf("class ArthurianArchitectBuildPersist", StringComparison.Ordinal) < 0) return true;
        if (archBody.IndexOf("SoftPromoteClassPoolIntoResources", StringComparison.Ordinal) < 0) return true;
        string bpBody = File.ReadAllText(buildPersist);
        // Standalone must be stub (type embedded in Architect) — full class = CS0101 risk / stale.
        if (bpBody.IndexOf("class ArthurianArchitectBuildPersist", StringComparison.Ordinal) >= 0) return true;
        if (bpBody.IndexOf("ArthurianArtArchitectWindow.cs", StringComparison.Ordinal) < 0) return true;
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
            "lore-boss-wave-order",
            "merlin-lore-narrative",
            "legends-lore-portrait",
            "architect-legend-pew-portraits",
            "fix-architect-pool-apply",
            "sprite-outline-highlight",
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
        };
        foreach (string s in stale)
        {
            if (pvBody.IndexOf("Id = \"" + s + "\"", StringComparison.Ordinal) >= 0) return true;
            if (archBody.IndexOf("UiBuildId = \"" + s + "\"", StringComparison.Ordinal) >= 0) return true;
        }
        // ClassPoolKind→UnitType CS0019; keep pew-body Persist/fileID belt.
        if (archBody.IndexOf("selectedClassPool == UnitType.", StringComparison.Ordinal) >= 0) return true;
        if (archBody.IndexOf("ClassPoolKind.Warrior => UnitType.Warrior", StringComparison.Ordinal) < 0) return true;
        // Nuclear embeds belt: TipEscape must carry HardWriteNamedProfilesIfMissing.
        string tipEscapePath = Path.Combine(root, "Assets", "Editor", "DemonKnightTipEscape.cs");
        if (!File.Exists(tipEscapePath)) return true;
        string tipEscapeBody = File.ReadAllText(tipEscapePath);
        if (tipEscapeBody.IndexOf("HardWriteNamedProfilesIfMissing", StringComparison.Ordinal) < 0) return true;
        if (tipEscapeBody.IndexOf("NamedProfileEmbeds", StringComparison.Ordinal) < 0) return true;
        // CS0246: ALL named Profile MonoScripts + shared types must exist on disk.
        if (DemonKnightPackageVersion.NamedProfileScriptsNeedRestore(root)) return true;
        if (syncBody.IndexOf("HardRestoreNamedProfileScripts", StringComparison.Ordinal) < 0) return true;
        string ensurePath = Path.Combine(root, "Assets", "Editor", "ArthurianStageLayoutProfileEnsure.cs");
        if (!File.Exists(ensurePath)) return true;
        string ensureBody = File.ReadAllText(ensurePath);
        if (ensureBody.IndexOf("TryResolveScriptRef", StringComparison.Ordinal) < 0) return true;
        if (ensureBody.IndexOf(TipPackageId, StringComparison.Ordinal) < 0) return true;
        if (archBody.IndexOf("PersistAllProfiles", StringComparison.Ordinal) < 0) return true;
        if (archBody.IndexOf("TryPewBodyFallbackFromGoodTroops", StringComparison.Ordinal) < 0
            && File.ReadAllText(Path.Combine(root, "Assets", "Scripts", "Data", "GameDataTypes.cs"))
                .IndexOf("TryPewBodyFallbackFromGoodTroops", StringComparison.Ordinal) < 0)
            return true;
        return false;
    }


    /// <summary>
    /// Nuclear CS0246 belt: write every named Profile MonoScript (+ shared types) to disk when
    /// missing/short/corrupt. Tries raw tip/main first; falls back to NamedProfileEmbeds constants
    /// so Escape works offline / when GitHub raw 404s. tip: fix-tipescape-profile-embeds
    /// </summary>
    public static int HardWriteNamedProfilesIfMissing(string root = null, System.Text.StringBuilder log = null)
    {
        if (string.IsNullOrEmpty(root))
            root = Directory.GetParent(Application.dataPath)!.FullName;
        int wrote = 0;
        string tip = null;
        try { tip = TryResolveTipSha() ?? "main"; } catch { tip = "main"; }

        foreach (var (rel, b64) in NamedProfileEmbeds.All)
        {
            try
            {
                string dest = Path.Combine(root, rel.Replace('/', Path.DirectorySeparatorChar));
                bool need = true;
                if (File.Exists(dest))
                {
                    string existing = File.ReadAllText(dest);
                    // Short / missing tip markers / missing shared types → rewrite.
                    need = existing.Length < 200
                           || existing.IndexOf(TipPackageId, StringComparison.Ordinal) < 0
                           || existing.IndexOf("class " + Path.GetFileNameWithoutExtension(rel), StringComparison.Ordinal) < 0;
                    // Shared-type belt for files that carry companions.
                    if (!need && rel.EndsWith("StageLayoutProfile.cs", StringComparison.OrdinalIgnoreCase))
                        need = existing.IndexOf("class SlotTransformConfig", StringComparison.Ordinal) < 0
                               || existing.IndexOf("class StageParallaxLayer", StringComparison.Ordinal) < 0;
                    if (!need && rel.EndsWith("SceneBackgroundProfile.cs", StringComparison.OrdinalIgnoreCase))
                        need = existing.IndexOf("enum SceneBackgroundSlot", StringComparison.Ordinal) < 0
                               || existing.IndexOf("class SceneBackgroundEntry", StringComparison.Ordinal) < 0;
                    if (!need && rel.EndsWith("BattleGoodEvilSpritePoolProfile.cs", StringComparison.OrdinalIgnoreCase))
                        need = existing.IndexOf("class BattleSpriteSlotEntry", StringComparison.Ordinal) < 0;
                }
                if (!need) continue;

                string body = null;
                string via = "embed";
                try
                {
                    body = DownloadTipMainOnly(rel, tip ?? "main");
                    if (!string.IsNullOrEmpty(body) && body.Length >= 200 && !LooksTiny404(body)
                        && body.IndexOf("class " + Path.GetFileNameWithoutExtension(rel), StringComparison.Ordinal) >= 0)
                        via = "raw";
                    else
                        body = null;
                }
                catch { body = null; }

                if (string.IsNullOrEmpty(body))
                {
                    body = NamedProfileEmbeds.Decode(b64);
                    via = "embed";
                }
                if (string.IsNullOrEmpty(body) || body.Length < 200)
                {
                    log?.AppendLine("MISS embed " + rel);
                    Debug.LogWarning("[DemonKnight] TipEscape HardWrite miss " + rel);
                    continue;
                }
                // Ensure tip id marker present even if raw is slightly stale.
                if (body.IndexOf(TipPackageId, StringComparison.Ordinal) < 0)
                    body = body.Replace("fix-named-profile-sync-cs0246", TipPackageId);

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
                wrote++;
                string line = $"HardWrite {rel} via {via} ({body.Length} B)";
                log?.AppendLine(line);
                Debug.Log("[DemonKnight] TipEscape " + line);
            }
            catch (Exception ex)
            {
                log?.AppendLine("HardWrite FAIL " + rel + ": " + ex.Message);
                Debug.LogWarning("[DemonKnight] TipEscape HardWrite FAIL " + rel + ": " + ex.Message);
            }
        }
        return wrote;
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

        // Nuclear: Profiles FIRST from raw-or-embed (CS0246 belt — no GitHub required).
        try
        {
            int hw = HardWriteNamedProfilesIfMissing(root, log);
            if (hw > 0) log.AppendLine("HardWriteNamedProfiles wrote " + hw + " file(s).");
        }
        catch (Exception ex) { log.AppendLine("HardWriteNamedProfiles: " + ex.Message); }

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
                if (rel.EndsWith("ArthurianArtArchitectWindow.cs", StringComparison.OrdinalIgnoreCase)
                    && (body.IndexOf("class ArthurianArchitectBuildPersist", StringComparison.Ordinal) < 0
                        || body.IndexOf("SoftPromoteClassPoolIntoResources", StringComparison.Ordinal) < 0
                        || body.IndexOf("IPreprocessBuildWithReport", StringComparison.Ordinal) < 0))
                    throw new Exception("Architect missing embedded BuildPersist / SoftPromote");
                if (rel.EndsWith("GameDataTypes.cs", StringComparison.OrdinalIgnoreCase)
                    && (body.IndexOf("class SceneBackgroundApplier", StringComparison.Ordinal) < 0
                        || body.IndexOf("class ClassSpritePoolApplier", StringComparison.Ordinal) < 0
                        || body.IndexOf("class LegendPewPortraitApplier", StringComparison.Ordinal) < 0
                        || body.IndexOf("class BattleGoodEvilSpritePoolApplier", StringComparison.Ordinal) < 0
                        || body.IndexOf("class StageLayoutProfile", StringComparison.Ordinal) >= 0
                        || body.IndexOf("class BattleGoodEvilSpritePoolProfile", StringComparison.Ordinal) >= 0
                        || body.IndexOf(TipPackageId, StringComparison.Ordinal) < 0))
                    throw new Exception("GameDataTypes must keep Appliers and NOT redefine Profile SOs");
                if (rel.EndsWith("StageLayoutProfile.cs", StringComparison.OrdinalIgnoreCase)
                    && (body.IndexOf("class StageLayoutProfile", StringComparison.Ordinal) < 0
                        || body.IndexOf("class SlotTransformConfig", StringComparison.Ordinal) < 0
                        || body.IndexOf("class StageParallaxLayer", StringComparison.Ordinal) < 0
                        || body.IndexOf(TipPackageId, StringComparison.Ordinal) < 0))
                    throw new Exception("StageLayoutProfile.cs missing SO/shared types");
                if (rel.EndsWith("SceneBackgroundProfile.cs", StringComparison.OrdinalIgnoreCase)
                    && (body.IndexOf("class SceneBackgroundProfile", StringComparison.Ordinal) < 0
                        || body.IndexOf("enum SceneBackgroundSlot", StringComparison.Ordinal) < 0
                        || body.IndexOf("class SceneBackgroundEntry", StringComparison.Ordinal) < 0
                        || body.IndexOf(TipPackageId, StringComparison.Ordinal) < 0))
                    throw new Exception("SceneBackgroundProfile.cs missing SO/shared types");
                if (rel.EndsWith("ClassSpritePoolProfile.cs", StringComparison.OrdinalIgnoreCase)
                    && (body.IndexOf("class ClassSpritePoolProfile", StringComparison.Ordinal) < 0
                        || body.IndexOf(TipPackageId, StringComparison.Ordinal) < 0))
                    throw new Exception("ClassSpritePoolProfile.cs missing SO class");
                if (rel.EndsWith("LegendPewPortraitProfile.cs", StringComparison.OrdinalIgnoreCase)
                    && (body.IndexOf("class LegendPewPortraitProfile", StringComparison.Ordinal) < 0
                        || body.IndexOf(TipPackageId, StringComparison.Ordinal) < 0))
                    throw new Exception("LegendPewPortraitProfile.cs missing SO class");
                if (rel.EndsWith("BattleGoodEvilSpritePoolProfile.cs", StringComparison.OrdinalIgnoreCase)
                    && (body.IndexOf("class BattleGoodEvilSpritePoolProfile", StringComparison.Ordinal) < 0
                        || body.IndexOf("class BattleSpriteSlotEntry", StringComparison.Ordinal) < 0
                        || body.IndexOf(TipPackageId, StringComparison.Ordinal) < 0))
                    throw new Exception("BattleGoodEvilSpritePoolProfile.cs missing SO/shared types");
                if (rel.EndsWith("SomersetChurchUI.cs", StringComparison.OrdinalIgnoreCase)
                    && (body.IndexOf("class SomersetChurchUI", StringComparison.Ordinal) < 0
                        || body.IndexOf("520f, 560f", StringComparison.Ordinal) < 0
                        || body.IndexOf("TrainATK", StringComparison.Ordinal) < 0))
                    throw new Exception("SomersetChurchUI missing tip church markers");
                if (rel.EndsWith("ArthurianArchitectBuildPersist.cs", StringComparison.OrdinalIgnoreCase)
                    && (body.IndexOf("class ArthurianArchitectBuildPersist", StringComparison.Ordinal) >= 0
                        || body.IndexOf("ArthurianArtArchitectWindow.cs", StringComparison.Ordinal) < 0))
                    throw new Exception("ArthurianArchitectBuildPersist must be stub → Architect embed");
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

        // Pew rebuild via reflection — TipEscape must compile even when Profile MonoScripts are
        // missing (Ensure → StageLayoutProfile CS0246 would otherwise block OnLoad restore).
        try
        {
            if (AssetDatabase.IsValidFolder("Assets/Art/Spliced/Sprite")
                || AssetDatabase.LoadMainAssetAtPath("Assets/Resources/Art/Modular/ModularSpriteLibrary.asset") != null)
            {
                var ensureType = Type.GetType("ArthurianStageLayoutProfileEnsure")
                                 ?? Type.GetType("ArthurianStageLayoutProfileEnsure, Assembly-CSharp-Editor");
                var rebuild = ensureType?.GetMethod(
                    "RebuildPewLibraryFromSplices",
                    System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
                if (rebuild != null)
                {
                    rebuild.Invoke(null, null);
                    log.AppendLine("Pew library rebuild requested.");
                }
                else
                {
                    log.AppendLine("Pew rebuild deferred (Ensure type not compiled yet).");
                }
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

    // ===== NUCLEAR NAMED PROFILE EMBEDS (tip fix-tipescape-profile-embeds) =====
    // Full tip Profile MonoScript sources as base64. TipEscape HardWrite uses these when
    // raw@main/jsDelivr miss or return short bodies. Regenerated from Assets/Scripts/Data/*Profile.cs.
    static class NamedProfileEmbeds
    {
        public const string TipId = "fix-tipescape-profile-embeds";

        // Assets/Scripts/Data/StageLayoutProfile.cs (10194 bytes)
        const string B64_StageLayoutProfile =
            "dXNpbmcgU3lzdGVtOwp1c2luZyBTeXN0ZW0uQ29sbGVjdGlvbnMuR2VuZXJpYzsKdXNpbmcgVW5pdHlFbmdpbmU7CgpuYW1lc3Bh" +
            "Y2UgQXJ0aHVyaWFuQXV0b2JhdHRsZXIKewogICAgLy8gdGlwOiBmaXgtdGlwZXNjYXBlLXByb2ZpbGUtZW1iZWRzIOKAlCBuYW1l" +
            "ZCBNb25vU2NyaXB0IGZvciBVbml0eSBTTyBiaW5kaW5nCiAgICBbU2VyaWFsaXphYmxlXQogICAgcHVibGljIGNsYXNzIFNsb3RU" +
            "cmFuc2Zvcm1Db25maWcKICAgIHsKICAgICAgICBwdWJsaWMgaW50IHNsb3ROdW1iZXI7CiAgICAgICAgcHVibGljIFZlY3RvcjIg" +
            "cG9zaXRpb247CiAgICAgICAgcHVibGljIFZlY3RvcjIgc2NhbGUgPSBWZWN0b3IyLm9uZTsKICAgICAgICBwdWJsaWMgVmVjdG9y" +
            "MiBhc3BlY3RNdWx0aXBsaWVyID0gVmVjdG9yMi5vbmU7CiAgICAgICAgcHVibGljIGludCBzb3J0aW5nT3JkZXI7CiAgICAgICAg" +
            "cHVibGljIGludCBib251c0hQOwogICAgICAgIHB1YmxpYyBpbnQgYm9udXNBVEs7CiAgICAgICAgcHVibGljIGludCBib251c1NQ" +
            "RDsKICAgICAgICAvLy8gPHN1bW1hcnk+RWRpdG9yL3J1bnRpbWUgcHJldmlldyBzcHJpdGUgb3ZlcnJpZGUgZm9yIHRoaXMgbnVt" +
            "YmVyZWQgaGFuZGxlLjwvc3VtbWFyeT4KICAgICAgICBwdWJsaWMgU3ByaXRlIHByZXZpZXdPck92ZXJyaWRlU3ByaXRlOwogICAg" +
            "ICAgIC8vLyA8c3VtbWFyeT5QYXRoIGJhY2t1cCBzbyBzZWF0IHNwcml0ZXMgcmVtYXRlcmlhbGl6ZSBpbiBwbGF5ZXIgYnVpbGRz" +
            "IChSZXNvdXJjZXMvU3RyZWFtaW5nQXNzZXRzKS48L3N1bW1hcnk+CiAgICAgICAgcHVibGljIHN0cmluZyBwcmV2aWV3QXNzZXRQ" +
            "YXRoOwogICAgICAgIC8vLyA8c3VtbWFyeT5PcHRpb25hbCBDaGFyYWN0ZXJEZWZpbml0aW9uLmNoYXJhY3RlcklkIHBpbm5lZCB0" +
            "byB0aGlzIHNsb3QuPC9zdW1tYXJ5PgogICAgICAgIHB1YmxpYyBzdHJpbmcgYXNzaWduZWRDaGFyYWN0ZXJJZDsKICAgIH0KCiAg" +
            "ICBbU2VyaWFsaXphYmxlXQogICAgcHVibGljIGNsYXNzIFN0YWdlUGFyYWxsYXhMYXllcgogICAgewogICAgICAgIHB1YmxpYyBz" +
            "dHJpbmcgbGF5ZXJOYW1lID0gIkxheWVyIjsKICAgICAgICBwdWJsaWMgU3ByaXRlIGxheWVyU3ByaXRlOwogICAgICAgIHB1Ymxp" +
            "YyBpbnQgc29ydGluZ09yZGVyOwogICAgICAgIHB1YmxpYyBWZWN0b3IyIHNjcm9sbFNwZWVkOwogICAgICAgIHB1YmxpYyBWZWN0" +
            "b3IyIHN3YXlBbXBsaXR1ZGU7CiAgICAgICAgcHVibGljIGZsb2F0IHN3YXlGcmVxdWVuY3kgPSAxZjsKICAgICAgICBwdWJsaWMg" +
            "Q29sb3IgbGF5ZXJUaW50ID0gQ29sb3Iud2hpdGU7CiAgICB9CgogICAgLy8vIDxzdW1tYXJ5PgogICAgLy8vIE51bWJlcmVkIFNv" +
            "bWVyc2V0IGNodXJjaCBwZXdzICg1MiksIHBsYXllciBiYXR0bGUgc2xvdHMgKDUyKSwgZW5lbXkgc2xvdHMgKDIwKSwKICAgIC8v" +
            "LyBwbHVzIHBhcmFsbGF4IC8gYXRtb3NwaGVyZSB0b2dnbGVzLiBSZXNvdXJjZXMgcGF0aDogU3RhZ2VMYXlvdXRQcm9maWxlLgog" +
            "ICAgLy8vIDwvc3VtbWFyeT4KICAgIFtDcmVhdGVBc3NldE1lbnUoZmlsZU5hbWUgPSAiU3RhZ2VMYXlvdXRQcm9maWxlIiwgbWVu" +
            "dU5hbWUgPSAiRGVtb25LbmlnaHQvU3RhZ2UgTGF5b3V0IFByb2ZpbGUiKV0KICAgIHB1YmxpYyBjbGFzcyBTdGFnZUxheW91dFBy" +
            "b2ZpbGUgOiBTY3JpcHRhYmxlT2JqZWN0CiAgICB7CiAgICAgICAgcHVibGljIGNvbnN0IHN0cmluZyBSZXNvdXJjZXNQYXRoID0g" +
            "IlN0YWdlTGF5b3V0UHJvZmlsZSI7CiAgICAgICAgcHVibGljIGNvbnN0IGludCBDaHVyY2hQZXdDb3VudCA9IDUyOwogICAgICAg" +
            "IHB1YmxpYyBjb25zdCBpbnQgUGxheWVyQmF0dGxlQ291bnQgPSA1MjsKICAgICAgICBwdWJsaWMgY29uc3QgaW50IEVuZW15QmF0" +
            "dGxlQ291bnQgPSAyMDsKICAgICAgICAvLy8gPHN1bW1hcnk+R29zc2lwIC8gcGV3IHJvdyBzaXplIChwZXdTZWF0SW5kZXggLyAx" +
            "MykuPC9zdW1tYXJ5PgogICAgICAgIHB1YmxpYyBjb25zdCBpbnQgUGV3Um93U2l6ZSA9IDEzOwoKICAgICAgICBwdWJsaWMgTGlz" +
            "dDxTbG90VHJhbnNmb3JtQ29uZmlnPiBjaHVyY2hQZXdTbG90cyA9IG5ldyBMaXN0PFNsb3RUcmFuc2Zvcm1Db25maWc+KCk7CiAg" +
            "ICAgICAgcHVibGljIExpc3Q8U2xvdFRyYW5zZm9ybUNvbmZpZz4gcGxheWVyQmF0dGxlU2xvdHMgPSBuZXcgTGlzdDxTbG90VHJh" +
            "bnNmb3JtQ29uZmlnPigpOwogICAgICAgIHB1YmxpYyBMaXN0PFNsb3RUcmFuc2Zvcm1Db25maWc+IGVuZW15QmF0dGxlU2xvdHMg" +
            "PSBuZXcgTGlzdDxTbG90VHJhbnNmb3JtQ29uZmlnPigpOwogICAgICAgIHB1YmxpYyBMaXN0PFN0YWdlUGFyYWxsYXhMYXllcj4g" +
            "cGFyYWxsYXhMYXllcnMgPSBuZXcgTGlzdDxTdGFnZVBhcmFsbGF4TGF5ZXI+KCk7CgogICAgICAgIHB1YmxpYyBib29sIGVuYWJs" +
            "ZVJhaW5GWDsKICAgICAgICBwdWJsaWMgYm9vbCBlbmFibGVMaWdodG5pbmdGbGFzaGVzOwogICAgICAgIHB1YmxpYyBib29sIGVu" +
            "YWJsZUNhbmRsZUZsaWNrZXIgPSB0cnVlOwoKICAgICAgICAvLy8gPHN1bW1hcnk+VHJ1ZSB3aGVuIHRoaXMgaXMgYSBDcmVhdGVJ" +
            "bnN0YW5jZSBzdGFuZC1pbiAobm90IGEgUmVzb3VyY2VzIGFzc2V0KS48L3N1bW1hcnk+CiAgICAgICAgW1N5c3RlbS5Ob25TZXJp" +
            "YWxpemVkXSBwdWJsaWMgYm9vbCBpc1J1bnRpbWVTdGFuZEluOwoKICAgICAgICBwdWJsaWMgc3RhdGljIFN0YWdlTGF5b3V0UHJv" +
            "ZmlsZSBMb2FkKCkKICAgICAgICB7CiAgICAgICAgICAgIFN0YWdlTGF5b3V0UHJvZmlsZSBwID0gbnVsbDsKICAgICAgICAgICAg" +
            "dHJ5IHsgcCA9IFJlc291cmNlcy5Mb2FkPFN0YWdlTGF5b3V0UHJvZmlsZT4oUmVzb3VyY2VzUGF0aCk7IH0KICAgICAgICAgICAg" +
            "Y2F0Y2ggeyAvKiBtaXNzaW5nIE1vbm9TY3JpcHQgR1VJRCBhZnRlciBlbWJlZCDigJQgZmFsbCB0aHJvdWdoICovIH0KICAgICAg" +
            "ICAgICAgaWYgKHAgIT0gbnVsbCkKICAgICAgICAgICAgewogICAgICAgICAgICAgICAgcC5FbnN1cmVTbG90Q291bnRzKCk7CiAg" +
            "ICAgICAgICAgICAgICBwLmlzUnVudGltZVN0YW5kSW4gPSBmYWxzZTsKICAgICAgICAgICAgICAgIHAuUmVtYXRlcmlhbGl6ZVNw" +
            "cml0ZXNGcm9tUGF0aHMoKTsKICAgICAgICAgICAgICAgIHJldHVybiBwOwogICAgICAgICAgICB9CiAgICAgICAgICAgIHRyeQog" +
            "ICAgICAgICAgICB7CiAgICAgICAgICAgICAgICB2YXIgYWxsID0gUmVzb3VyY2VzLkxvYWRBbGw8U3RhZ2VMYXlvdXRQcm9maWxl" +
            "PigiIik7CiAgICAgICAgICAgICAgICBpZiAoYWxsICE9IG51bGwpCiAgICAgICAgICAgICAgICB7CiAgICAgICAgICAgICAgICAg" +
            "ICAgZm9yIChpbnQgaSA9IDA7IGkgPCBhbGwuTGVuZ3RoOyBpKyspCiAgICAgICAgICAgICAgICAgICAgewogICAgICAgICAgICAg" +
            "ICAgICAgICAgICBpZiAoYWxsW2ldID09IG51bGwpIGNvbnRpbnVlOwogICAgICAgICAgICAgICAgICAgICAgICBhbGxbaV0uRW5z" +
            "dXJlU2xvdENvdW50cygpOwogICAgICAgICAgICAgICAgICAgICAgICBhbGxbaV0uaXNSdW50aW1lU3RhbmRJbiA9IGZhbHNlOwog" +
            "ICAgICAgICAgICAgICAgICAgICAgICBhbGxbaV0uUmVtYXRlcmlhbGl6ZVNwcml0ZXNGcm9tUGF0aHMoKTsKICAgICAgICAgICAg" +
            "ICAgICAgICAgICAgcmV0dXJuIGFsbFtpXTsKICAgICAgICAgICAgICAgICAgICB9CiAgICAgICAgICAgICAgICB9CiAgICAgICAg" +
            "ICAgIH0KICAgICAgICAgICAgY2F0Y2ggeyAvKiBicm9rZW4gUmVzb3VyY2VzIGFzc2V0ICovIH0KICAgICAgICAgICAgLy8gUnVu" +
            "dGltZSBmYWxsYmFjayB3aGVuIHRoZSBSZXNvdXJjZXMgYXNzZXQgaGFzIGEgc3RhbGUgc2NyaXB0IEdVSUQgKEZvcmNlIFN5bmMg" +
            "ZW1iZWQpLgogICAgICAgICAgICAvLyBOZXZlciB3aXBlIGF1dGhvcmVkIFJlc291cmNlcyDigJQgc3RhbmQtaW4gaXMgbGFzdCBy" +
            "ZXNvcnQgb25seS4KICAgICAgICAgICAgcCA9IENyZWF0ZUluc3RhbmNlPFN0YWdlTGF5b3V0UHJvZmlsZT4oKTsKICAgICAgICAg" +
            "ICAgcC5uYW1lID0gIlN0YWdlTGF5b3V0UHJvZmlsZV9SdW50aW1lIjsKICAgICAgICAgICAgcC5pc1J1bnRpbWVTdGFuZEluID0g" +
            "dHJ1ZTsKICAgICAgICAgICAgcC5FbnN1cmVTbG90Q291bnRzKCk7CiAgICAgICAgICAgIHJldHVybiBwOwogICAgICAgIH0KCiAg" +
            "ICAgICAgLy8vIDxzdW1tYXJ5PgogICAgICAgIC8vLyBSZXN0b3JlIE1pc3NpbmcgcGV3L2JhdHRsZSBzZWF0IHNwcml0ZXMgZnJv" +
            "bSBwYXRoIGJhY2t1cHMuCiAgICAgICAgLy8vIFdvcmtzIGluIHBsYXllciBidWlsZHMgdmlhIFJlc291cmNlcy5Mb2FkIC8gU3Ry" +
            "ZWFtaW5nQXNzZXRzIChub3QgRWRpdG9yLW9ubHkpLgogICAgICAgIC8vLyBOZXZlciBjbGVhcnMgcGF0aCBiYWNrdXBzIHdoZW4g" +
            "c3ByaXRlcyBhcmUgdGVtcG9yYXJpbHkgbnVsbC4KICAgICAgICAvLy8gPC9zdW1tYXJ5PgogICAgICAgIHB1YmxpYyBpbnQgUmVt" +
            "YXRlcmlhbGl6ZVNwcml0ZXNGcm9tUGF0aHMoKQogICAgICAgIHsKICAgICAgICAgICAgRW5zdXJlU2xvdENvdW50cygpOwogICAg" +
            "ICAgICAgICBpbnQgbiA9IDA7CiAgICAgICAgICAgIG4gKz0gUmVtYXRlcmlhbGl6ZVNsb3RMaXN0KGNodXJjaFBld1Nsb3RzKTsK" +
            "ICAgICAgICAgICAgbiArPSBSZW1hdGVyaWFsaXplU2xvdExpc3QocGxheWVyQmF0dGxlU2xvdHMpOwogICAgICAgICAgICBuICs9" +
            "IFJlbWF0ZXJpYWxpemVTbG90TGlzdChlbmVteUJhdHRsZVNsb3RzKTsKICAgICAgICAgICAgcmV0dXJuIG47CiAgICAgICAgfQoK" +
            "ICAgICAgICBzdGF0aWMgaW50IFJlbWF0ZXJpYWxpemVTbG90TGlzdChMaXN0PFNsb3RUcmFuc2Zvcm1Db25maWc+IHNsb3RzKQog" +
            "ICAgICAgIHsKICAgICAgICAgICAgaWYgKHNsb3RzID09IG51bGwpIHJldHVybiAwOwogICAgICAgICAgICBpbnQgcmVzdG9yZWQg" +
            "PSAwOwogICAgICAgICAgICBmb3IgKGludCBpID0gMDsgaSA8IHNsb3RzLkNvdW50OyBpKyspCiAgICAgICAgICAgIHsKICAgICAg" +
            "ICAgICAgICAgIHZhciBzID0gc2xvdHNbaV07CiAgICAgICAgICAgICAgICBpZiAocyA9PSBudWxsIHx8IHMucHJldmlld09yT3Zl" +
            "cnJpZGVTcHJpdGUgIT0gbnVsbCkgY29udGludWU7CiAgICAgICAgICAgICAgICBpZiAoc3RyaW5nLklzTnVsbE9yRW1wdHkocy5w" +
            "cmV2aWV3QXNzZXRQYXRoKSkgY29udGludWU7CiAgICAgICAgICAgICAgICBTcHJpdGUgc3AgPSBDbGFzc1Nwcml0ZVBvb2xQcm9m" +
            "aWxlLlRyeUxvYWRTcHJpdGVBdFBhdGgocy5wcmV2aWV3QXNzZXRQYXRoKTsKICAgICAgICAgICAgICAgIGlmIChzcCA9PSBudWxs" +
            "KSBjb250aW51ZTsKICAgICAgICAgICAgICAgIHMucHJldmlld09yT3ZlcnJpZGVTcHJpdGUgPSBzcDsKICAgICAgICAgICAgICAg" +
            "IHJlc3RvcmVkKys7CiAgICAgICAgICAgIH0KICAgICAgICAgICAgcmV0dXJuIHJlc3RvcmVkOwogICAgICAgIH0KCiAgICAgICAg" +
            "Ly8vIDxzdW1tYXJ5PlBhZCAvIHRyaW0gbGlzdHMgdG8gNTIgLyA1MiAvIDIwIHdpdGggZGVmYXVsdCB0cmFuc2Zvcm1zLjwvc3Vt" +
            "bWFyeT4KICAgICAgICBwdWJsaWMgdm9pZCBFbnN1cmVTbG90Q291bnRzKCkKICAgICAgICB7CiAgICAgICAgICAgIGNodXJjaFBl" +
            "d1Nsb3RzID8/PSBuZXcgTGlzdDxTbG90VHJhbnNmb3JtQ29uZmlnPigpOwogICAgICAgICAgICBwbGF5ZXJCYXR0bGVTbG90cyA/" +
            "Pz0gbmV3IExpc3Q8U2xvdFRyYW5zZm9ybUNvbmZpZz4oKTsKICAgICAgICAgICAgZW5lbXlCYXR0bGVTbG90cyA/Pz0gbmV3IExp" +
            "c3Q8U2xvdFRyYW5zZm9ybUNvbmZpZz4oKTsKICAgICAgICAgICAgcGFyYWxsYXhMYXllcnMgPz89IG5ldyBMaXN0PFN0YWdlUGFy" +
            "YWxsYXhMYXllcj4oKTsKCiAgICAgICAgICAgIEZpbGxTbG90cyhjaHVyY2hQZXdTbG90cywgQ2h1cmNoUGV3Q291bnQsIENodXJj" +
            "aFBld0RlZmF1bHQpOwogICAgICAgICAgICBGaWxsU2xvdHMocGxheWVyQmF0dGxlU2xvdHMsIFBsYXllckJhdHRsZUNvdW50LCBQ" +
            "bGF5ZXJCYXR0bGVEZWZhdWx0KTsKICAgICAgICAgICAgRmlsbFNsb3RzKGVuZW15QmF0dGxlU2xvdHMsIEVuZW15QmF0dGxlQ291" +
            "bnQsIEVuZW15QmF0dGxlRGVmYXVsdCk7CiAgICAgICAgfQoKICAgICAgICBzdGF0aWMgdm9pZCBGaWxsU2xvdHMoTGlzdDxTbG90" +
            "VHJhbnNmb3JtQ29uZmlnPiBsaXN0LCBpbnQgY291bnQsIEZ1bmM8aW50LCBTbG90VHJhbnNmb3JtQ29uZmlnPiBmYWN0b3J5KQog" +
            "ICAgICAgIHsKICAgICAgICAgICAgd2hpbGUgKGxpc3QuQ291bnQgPCBjb3VudCkKICAgICAgICAgICAgICAgIGxpc3QuQWRkKGZh" +
            "Y3RvcnkobGlzdC5Db3VudCkpOwogICAgICAgICAgICBpZiAobGlzdC5Db3VudCA+IGNvdW50KQogICAgICAgICAgICAgICAgbGlz" +
            "dC5SZW1vdmVSYW5nZShjb3VudCwgbGlzdC5Db3VudCAtIGNvdW50KTsKICAgICAgICAgICAgZm9yIChpbnQgaSA9IDA7IGkgPCBs" +
            "aXN0LkNvdW50OyBpKyspCiAgICAgICAgICAgIHsKICAgICAgICAgICAgICAgIGlmIChsaXN0W2ldID09IG51bGwpIGxpc3RbaV0g" +
            "PSBmYWN0b3J5KGkpOwogICAgICAgICAgICAgICAgbGlzdFtpXS5zbG90TnVtYmVyID0gaTsKICAgICAgICAgICAgfQogICAgICAg" +
            "IH0KCiAgICAgICAgLy8vIDxzdW1tYXJ5PgogICAgICAgIC8vLyBQbGF5LW5vcm1hbGl6ZWQgMOKAkzEgY2h1cmNoIHNlYXQgKG1h" +
            "dGNoZXMgU29tZXJzZXRQZXdMYXlvdXQgZGVmYXVsdHMpLgogICAgICAgIC8vLyBXb3JsZC1zcGFjZSBkZWZhdWx0cyAoLTYuLjYp" +
            "IHdlcmUgaWdub3JlZCBieSBQb3B1bGF0ZVBld3MsIHNvIFN0YWdlIFBsYWNlcgogICAgICAgIC8vLyBlZGl0cyBuZXZlciBzaG93" +
            "ZWQgaW4gUGxheSB1bnRpbCBBcmNoaXRlY3QgcmUtc2VlZGVkIOKAlCBmaXhlZCBoZXJlLgogICAgICAgIC8vLyA8L3N1bW1hcnk+" +
            "CiAgICAgICAgc3RhdGljIFNsb3RUcmFuc2Zvcm1Db25maWcgQ2h1cmNoUGV3RGVmYXVsdChpbnQgaSkKICAgICAgICB7CiAgICAg" +
            "ICAgICAgIFZlY3RvcjIgbm9ybSA9IERlZmF1bHRDaHVyY2hOb3JtYWxpemVkUG9zaXRpb24oaSk7CiAgICAgICAgICAgIHJldHVy" +
            "biBuZXcgU2xvdFRyYW5zZm9ybUNvbmZpZwogICAgICAgICAgICB7CiAgICAgICAgICAgICAgICBzbG90TnVtYmVyID0gaSwKICAg" +
            "ICAgICAgICAgICAgIHBvc2l0aW9uID0gbm9ybSwKICAgICAgICAgICAgICAgIHNjYWxlID0gVmVjdG9yMi5vbmUsCiAgICAgICAg" +
            "ICAgICAgICBhc3BlY3RNdWx0aXBsaWVyID0gVmVjdG9yMi5vbmUsCiAgICAgICAgICAgICAgICBzb3J0aW5nT3JkZXIgPSAtTWF0" +
            "aGYuUm91bmRUb0ludChub3JtLnkgKiAxMDBmKQogICAgICAgICAgICB9OwogICAgICAgIH0KCiAgICAgICAgLy8vIDxzdW1tYXJ5" +
            "Pk5vcm1hbGl6ZWQgMOKAkzEgcGV3IHNlYXQgbWF0Y2hpbmcgU29tZXJzZXRQZXdMYXlvdXQuQ3JlYXRlRGVmYXVsdFBld3MgY2Fw" +
            "YWNpdGllcy48L3N1bW1hcnk+CiAgICAgICAgcHVibGljIHN0YXRpYyBWZWN0b3IyIERlZmF1bHRDaHVyY2hOb3JtYWxpemVkUG9z" +
            "aXRpb24oaW50IGdsb2JhbFNlYXRJbmRleCkKICAgICAgICB7CiAgICAgICAgICAgIC8vIE1pcnJvciBTb21lcnNldFBld0xheW91" +
            "dC5DcmVhdGVEZWZhdWx0UGV3cyAodjQpIOKAlCBrZWVwIGluIHN5bmMuCiAgICAgICAgICAgIGZsb2F0W11bXSBjb2xYQnlSb3cg" +
            "PQogICAgICAgICAgICB7CiAgICAgICAgICAgICAgICBuZXdbXSB7IDAuMjIwZiwgMC41MDBmLCAwLjc4MGYgfSwKICAgICAgICAg" +
            "ICAgICAgIG5ld1tdIHsgMC4yMjZmLCAwLjUwMGYsIDAuNzc1ZiB9LAogICAgICAgICAgICAgICAgbmV3W10geyAwLjIzMmYsIDAu" +
            "NTAwZiwgMC43NzBmIH0sCiAgICAgICAgICAgICAgICBuZXdbXSB7IDAuMjM4ZiwgMC41MDBmLCAwLjc2NWYgfQogICAgICAgICAg" +
            "ICB9OwogICAgICAgICAgICBmbG9hdFtdIHJvd1kgPSB7IDAuNDAwZiwgMC41NDVmLCAwLjY5MGYsIDAuODIwZiB9OwogICAgICAg" +
            "ICAgICBmbG9hdFtdIHJvd1NwcmVhZCA9IHsgMC4xNzVmLCAwLjE2MGYsIDAuMTQ1ZiwgMC4xMjBmIH07CiAgICAgICAgICAgIGlu" +
            "dFssXSBjYXBzID0KICAgICAgICAgICAgewogICAgICAgICAgICAgICAgeyA1LCA1LCA1IH0sCiAgICAgICAgICAgICAgICB7IDQs" +
            "IDUsIDQgfSwKICAgICAgICAgICAgICAgIHsgNCwgNCwgNCB9LAogICAgICAgICAgICAgICAgeyA0LCA0LCA0IH0KICAgICAgICAg" +
            "ICAgfTsKCiAgICAgICAgICAgIGludCByZW1haW5pbmcgPSBNYXRoZi5DbGFtcChnbG9iYWxTZWF0SW5kZXgsIDAsIENodXJjaFBl" +
            "d0NvdW50IC0gMSk7CiAgICAgICAgICAgIGZvciAoaW50IHJvdyA9IDA7IHJvdyA8IDQ7IHJvdysrKQogICAgICAgICAgICB7CiAg" +
            "ICAgICAgICAgICAgICBmb3IgKGludCBjb2wgPSAwOyBjb2wgPCAzOyBjb2wrKykKICAgICAgICAgICAgICAgIHsKICAgICAgICAg" +
            "ICAgICAgICAgICBpbnQgY2FwID0gTWF0aGYuTWF4KDEsIGNhcHNbcm93LCBjb2xdKTsKICAgICAgICAgICAgICAgICAgICBpZiAo" +
            "cmVtYWluaW5nIDwgY2FwKQogICAgICAgICAgICAgICAgICAgIHsKICAgICAgICAgICAgICAgICAgICAgICAgZmxvYXQgdCA9IGNh" +
            "cCA9PSAxID8gMC41ZiA6IHJlbWFpbmluZyAvIChmbG9hdCkoY2FwIC0gMSk7CiAgICAgICAgICAgICAgICAgICAgICAgIGZsb2F0" +
            "IHggPSBjb2xYQnlSb3dbcm93XVtjb2xdCiAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICArIE1hdGhmLkxlcnAoLXJv" +
            "d1NwcmVhZFtyb3ddICogMC41Ziwgcm93U3ByZWFkW3Jvd10gKiAwLjVmLCB0KTsKICAgICAgICAgICAgICAgICAgICAgICAgcmV0" +
            "dXJuIG5ldyBWZWN0b3IyKHgsIHJvd1lbcm93XSk7CiAgICAgICAgICAgICAgICAgICAgfQogICAgICAgICAgICAgICAgICAgIHJl" +
            "bWFpbmluZyAtPSBjYXA7CiAgICAgICAgICAgICAgICB9CiAgICAgICAgICAgIH0KICAgICAgICAgICAgcmV0dXJuIG5ldyBWZWN0" +
            "b3IyKDAuNWYsIDAuNWYpOwogICAgICAgIH0KCiAgICAgICAgLy8vIDxzdW1tYXJ5PlN0YWdnZXJlZCBjb2x1bW5zIG9mIDYgKGZy" +
            "b250bGluZVNpemUgPSA2KSDigJQgbWF0Y2hlcyBQaGFzZSAzIGF1dG8tYXJyYW5nZSBtYXRoLjwvc3VtbWFyeT4KICAgICAgICBz" +
            "dGF0aWMgU2xvdFRyYW5zZm9ybUNvbmZpZyBQbGF5ZXJCYXR0bGVEZWZhdWx0KGludCBpKQogICAgICAgIHsKICAgICAgICAgICAg" +
            "Y29uc3QgaW50IGZyb250bGluZVNpemUgPSA2OwogICAgICAgICAgICBpbnQgY29sID0gaSAvIGZyb250bGluZVNpemU7CiAgICAg" +
            "ICAgICAgIGludCByb3cgPSBpICUgZnJvbnRsaW5lU2l6ZTsKICAgICAgICAgICAgZmxvYXQgc2NhbGUgPSBjb2wgPD0gMSA/IDFm" +
            "IDogTWF0aGYuTGVycCgwLjk1ZiwgMC43MGYsIE1hdGhmLkludmVyc2VMZXJwKDJmLCA4ZiwgY29sKSk7CiAgICAgICAgICAgIGZs" +
            "b2F0IHggPSAtMy41ZiArIHJvdyAqIDEuMWY7CiAgICAgICAgICAgIGZsb2F0IHkgPSAyLjBmIC0gY29sICogMC44NWY7CiAgICAg" +
            "ICAgICAgIHJldHVybiBuZXcgU2xvdFRyYW5zZm9ybUNvbmZpZwogICAgICAgICAgICB7CiAgICAgICAgICAgICAgICBzbG90TnVt" +
            "YmVyID0gaSwKICAgICAgICAgICAgICAgIHBvc2l0aW9uID0gbmV3IFZlY3RvcjIoeCwgeSksCiAgICAgICAgICAgICAgICBzY2Fs" +
            "ZSA9IG5ldyBWZWN0b3IyKHNjYWxlLCBzY2FsZSksCiAgICAgICAgICAgICAgICBhc3BlY3RNdWx0aXBsaWVyID0gVmVjdG9yMi5v" +
            "bmUsCiAgICAgICAgICAgICAgICBzb3J0aW5nT3JkZXIgPSBNYXRoZi5Sb3VuZFRvSW50KC15ICogMTBmKQogICAgICAgICAgICB9" +
            "OwogICAgICAgIH0KCiAgICAgICAgc3RhdGljIFNsb3RUcmFuc2Zvcm1Db25maWcgRW5lbXlCYXR0bGVEZWZhdWx0KGludCBpKQog" +
            "ICAgICAgIHsKICAgICAgICAgICAgaW50IGNvbCA9IGkgLyA1OwogICAgICAgICAgICBpbnQgcm93ID0gaSAlIDU7CiAgICAgICAg" +
            "ICAgIGZsb2F0IHggPSAzLjVmICsgcm93ICogMS4wZjsKICAgICAgICAgICAgZmxvYXQgeSA9IDEuNWYgLSBjb2wgKiAxLjBmOwog" +
            "ICAgICAgICAgICByZXR1cm4gbmV3IFNsb3RUcmFuc2Zvcm1Db25maWcKICAgICAgICAgICAgewogICAgICAgICAgICAgICAgc2xv" +
            "dE51bWJlciA9IGksCiAgICAgICAgICAgICAgICBwb3NpdGlvbiA9IG5ldyBWZWN0b3IyKHgsIHkpLAogICAgICAgICAgICAgICAg" +
            "c2NhbGUgPSBWZWN0b3IyLm9uZSwKICAgICAgICAgICAgICAgIGFzcGVjdE11bHRpcGxpZXIgPSBWZWN0b3IyLm9uZSwKICAgICAg" +
            "ICAgICAgICAgIHNvcnRpbmdPcmRlciA9IE1hdGhmLlJvdW5kVG9JbnQoLXkgKiAxMGYpCiAgICAgICAgICAgIH07CiAgICAgICAg" +
            "fQogICAgfQp9Cg==";

        // Assets/Scripts/Data/SceneBackgroundProfile.cs (5582 bytes)
        const string B64_SceneBackgroundProfile =
            "dXNpbmcgU3lzdGVtOwp1c2luZyBTeXN0ZW0uQ29sbGVjdGlvbnMuR2VuZXJpYzsKdXNpbmcgVW5pdHlFbmdpbmU7CgpuYW1lc3Bh" +
            "Y2UgQXJ0aHVyaWFuQXV0b2JhdHRsZXIKewogICAgLy8gdGlwOiBmaXgtdGlwZXNjYXBlLXByb2ZpbGUtZW1iZWRzIOKAlCBuYW1l" +
            "ZCBNb25vU2NyaXB0IGZvciBVbml0eSBTTyBiaW5kaW5nCiAgICAvLy8gPHN1bW1hcnk+TmFtZWQgc2NyZWVucyB0aGF0IEFyY2hp" +
            "dGVjdCBjYW4gYXNzaWduIGpwZy9wbmcgb3IgbXA0IGJhY2tncm91bmRzIHRvLjwvc3VtbWFyeT4KICAgIHB1YmxpYyBlbnVtIFNj" +
            "ZW5lQmFja2dyb3VuZFNsb3QKICAgIHsKICAgICAgICBNYWluTWVudSA9IDAsCiAgICAgICAgU29tZXJzZXRDaHVyY2ggPSAxLAog" +
            "ICAgICAgIENodXJjaHlhcmRCYXR0bGUgPSAyLAogICAgICAgIFZpY3RvcnlPdmVybGF5ID0gMywKICAgICAgICBNZXJsaW5SZXN1" +
            "cnJlY3Rpb24gPSA0LAogICAgICAgIExlZ2VuZENvZGV4ID0gNSwKICAgIH0KCiAgICBbU2VyaWFsaXphYmxlXQogICAgcHVibGlj" +
            "IGNsYXNzIFNjZW5lQmFja2dyb3VuZEVudHJ5CiAgICB7CiAgICAgICAgcHVibGljIFNjZW5lQmFja2dyb3VuZFNsb3Qgc2xvdCA9" +
            "IFNjZW5lQmFja2dyb3VuZFNsb3QuTWFpbk1lbnU7CiAgICAgICAgcHVibGljIHN0cmluZyBkaXNwbGF5TmFtZSA9ICJTY3JlZW4i" +
            "OwogICAgICAgIFtUb29sdGlwKCJTdGlsbCBpbWFnZSAoanBnL3BuZyBTcHJpdGUpLiBVc2VkIHdoZW4gbm8gdmlkZW8gaXMgYXNz" +
            "aWduZWQuIildCiAgICAgICAgcHVibGljIFNwcml0ZSBpbWFnZVNwcml0ZTsKICAgICAgICBbVG9vbHRpcCgiT3B0aW9uYWwgbG9v" +
            "cGluZyB2aWRlbyAobXA0IFZpZGVvQ2xpcCkuIFdpbnMgb3ZlciBpbWFnZSB3aGVuIHNldC4iKV0KICAgICAgICBwdWJsaWMgVW5p" +
            "dHlFbmdpbmUuVmlkZW8uVmlkZW9DbGlwIHZpZGVvQ2xpcDsKICAgICAgICBbVG9vbHRpcCgiRWRpdG9yLW9ubHkgYXNzZXQgcGF0" +
            "aCBiYWNrdXAgc28gYXNzaWdubWVudHMgc3Vydml2ZSByZWltcG9ydHMuIildCiAgICAgICAgcHVibGljIHN0cmluZyBpbWFnZUFz" +
            "c2V0UGF0aDsKICAgICAgICBbVG9vbHRpcCgiRWRpdG9yLW9ubHkgYXNzZXQgcGF0aCBiYWNrdXAgZm9yIFZpZGVvQ2xpcC4iKV0K" +
            "ICAgICAgICBwdWJsaWMgc3RyaW5nIHZpZGVvQXNzZXRQYXRoOwogICAgfQoKICAgIC8vLyA8c3VtbWFyeT4KICAgIC8vLyBBcmNo" +
            "aXRlY3QtYXNzaWduZWQgc2NlbmUvc2NyZWVuIGJhY2tncm91bmRzIChpbWFnZSBvciB2aWRlbykuIFJlc291cmNlcyBwYXRoOiBT" +
            "Y2VuZUJhY2tncm91bmRQcm9maWxlLgogICAgLy8vIEFzc2lnbi1vbmx5IOKAlCBlbXB0eSBzbG90cyBrZWVwIGVhY2ggc2NyZWVu" +
            "J3MgYnVpbHQtaW4gZGVmYXVsdCBhcnQuCiAgICAvLy8gPC9zdW1tYXJ5PgogICAgW0NyZWF0ZUFzc2V0TWVudShmaWxlTmFtZSA9" +
            "ICJTY2VuZUJhY2tncm91bmRQcm9maWxlIiwgbWVudU5hbWUgPSAiRGVtb25LbmlnaHQvU2NlbmUgQmFja2dyb3VuZCBQcm9maWxl" +
            "IildCiAgICBwdWJsaWMgY2xhc3MgU2NlbmVCYWNrZ3JvdW5kUHJvZmlsZSA6IFNjcmlwdGFibGVPYmplY3QKICAgIHsKICAgICAg" +
            "ICBwdWJsaWMgY29uc3Qgc3RyaW5nIFJlc291cmNlc1BhdGggPSAiU2NlbmVCYWNrZ3JvdW5kUHJvZmlsZSI7CgogICAgICAgIHB1" +
            "YmxpYyBMaXN0PFNjZW5lQmFja2dyb3VuZEVudHJ5PiBlbnRyaWVzID0gbmV3IExpc3Q8U2NlbmVCYWNrZ3JvdW5kRW50cnk+KCk7" +
            "CgogICAgICAgIHB1YmxpYyBzdGF0aWMgU2NlbmVCYWNrZ3JvdW5kUHJvZmlsZSBMb2FkKCkKICAgICAgICB7CiAgICAgICAgICAg" +
            "IFNjZW5lQmFja2dyb3VuZFByb2ZpbGUgcCA9IG51bGw7CiAgICAgICAgICAgIHRyeSB7IHAgPSBSZXNvdXJjZXMuTG9hZDxTY2Vu" +
            "ZUJhY2tncm91bmRQcm9maWxlPihSZXNvdXJjZXNQYXRoKTsgfQogICAgICAgICAgICBjYXRjaCB7IC8qIG1pc3NpbmcgTW9ub1Nj" +
            "cmlwdCBHVUlEL2ZpbGVJRCBhZnRlciBlbWJlZCDigJQgZmFsbCB0aHJvdWdoICovIH0KICAgICAgICAgICAgaWYgKHAgIT0gbnVs" +
            "bCkKICAgICAgICAgICAgewogICAgICAgICAgICAgICAgcC5FbnN1cmVEZWZhdWx0RW50cmllcygpOwogICAgICAgICAgICAgICAg" +
            "cC5SZW1hdGVyaWFsaXplU3ByaXRlc0Zyb21QYXRocygpOwogICAgICAgICAgICAgICAgcmV0dXJuIHA7CiAgICAgICAgICAgIH0K" +
            "ICAgICAgICAgICAgdHJ5CiAgICAgICAgICAgIHsKICAgICAgICAgICAgICAgIHZhciBhbGwgPSBSZXNvdXJjZXMuTG9hZEFsbDxT" +
            "Y2VuZUJhY2tncm91bmRQcm9maWxlPigiIik7CiAgICAgICAgICAgICAgICBpZiAoYWxsICE9IG51bGwpCiAgICAgICAgICAgICAg" +
            "ICB7CiAgICAgICAgICAgICAgICAgICAgZm9yIChpbnQgaSA9IDA7IGkgPCBhbGwuTGVuZ3RoOyBpKyspCiAgICAgICAgICAgICAg" +
            "ICAgICAgewogICAgICAgICAgICAgICAgICAgICAgICBpZiAoYWxsW2ldID09IG51bGwpIGNvbnRpbnVlOwogICAgICAgICAgICAg" +
            "ICAgICAgICAgICBhbGxbaV0uRW5zdXJlRGVmYXVsdEVudHJpZXMoKTsKICAgICAgICAgICAgICAgICAgICAgICAgYWxsW2ldLlJl" +
            "bWF0ZXJpYWxpemVTcHJpdGVzRnJvbVBhdGhzKCk7CiAgICAgICAgICAgICAgICAgICAgICAgIHJldHVybiBhbGxbaV07CiAgICAg" +
            "ICAgICAgICAgICAgICAgfQogICAgICAgICAgICAgICAgfQogICAgICAgICAgICB9CiAgICAgICAgICAgIGNhdGNoIHsgLyogYnJv" +
            "a2VuIFJlc291cmNlcyBhc3NldCAvIG1pc3Npbmcgc2NyaXB0IHNwYW0g4oCUIHNvZnQgc3RhbmQtaW4gKi8gfQogICAgICAgICAg" +
            "ICBwID0gQ3JlYXRlSW5zdGFuY2U8U2NlbmVCYWNrZ3JvdW5kUHJvZmlsZT4oKTsKICAgICAgICAgICAgcC5uYW1lID0gIlNjZW5l" +
            "QmFja2dyb3VuZFByb2ZpbGVfUnVudGltZSI7CiAgICAgICAgICAgIHAuRW5zdXJlRGVmYXVsdEVudHJpZXMoKTsKICAgICAgICAg" +
            "ICAgcmV0dXJuIHA7CiAgICAgICAgfQoKICAgICAgICAvLy8gPHN1bW1hcnk+CiAgICAgICAgLy8vIFJlc3RvcmUgTWlzc2luZyBp" +
            "bWFnZSBzcHJpdGVzIGZyb20gcGF0aCBiYWNrdXBzIChSZXNvdXJjZXMgLyBTdHJlYW1pbmdBc3NldHMpLgogICAgICAgIC8vLyB0" +
            "aXA6IGZpeC1jbGFzcy1wb29sLWtpbmQtY3MwMDE5CiAgICAgICAgLy8vIDwvc3VtbWFyeT4KICAgICAgICBwdWJsaWMgaW50IFJl" +
            "bWF0ZXJpYWxpemVTcHJpdGVzRnJvbVBhdGhzKCkKICAgICAgICB7CiAgICAgICAgICAgIEVuc3VyZURlZmF1bHRFbnRyaWVzKCk7" +
            "CiAgICAgICAgICAgIGlmIChlbnRyaWVzID09IG51bGwpIHJldHVybiAwOwogICAgICAgICAgICBpbnQgcmVzdG9yZWQgPSAwOwog" +
            "ICAgICAgICAgICBmb3IgKGludCBpID0gMDsgaSA8IGVudHJpZXMuQ291bnQ7IGkrKykKICAgICAgICAgICAgewogICAgICAgICAg" +
            "ICAgICAgdmFyIGUgPSBlbnRyaWVzW2ldOwogICAgICAgICAgICAgICAgaWYgKGUgPT0gbnVsbCB8fCBlLmltYWdlU3ByaXRlICE9" +
            "IG51bGwpIGNvbnRpbnVlOwogICAgICAgICAgICAgICAgaWYgKHN0cmluZy5Jc051bGxPckVtcHR5KGUuaW1hZ2VBc3NldFBhdGgp" +
            "KSBjb250aW51ZTsKICAgICAgICAgICAgICAgIFNwcml0ZSBzcCA9IENsYXNzU3ByaXRlUG9vbFByb2ZpbGUuVHJ5TG9hZFNwcml0" +
            "ZUF0UGF0aChlLmltYWdlQXNzZXRQYXRoKTsKICAgICAgICAgICAgICAgIGlmIChzcCA9PSBudWxsKSBjb250aW51ZTsKICAgICAg" +
            "ICAgICAgICAgIGUuaW1hZ2VTcHJpdGUgPSBzcDsKICAgICAgICAgICAgICAgIHJlc3RvcmVkKys7CiAgICAgICAgICAgIH0KICAg" +
            "ICAgICAgICAgcmV0dXJuIHJlc3RvcmVkOwogICAgICAgIH0KCiAgICAgICAgcHVibGljIHZvaWQgRW5zdXJlRGVmYXVsdEVudHJp" +
            "ZXMoKQogICAgICAgIHsKICAgICAgICAgICAgZW50cmllcyA/Pz0gbmV3IExpc3Q8U2NlbmVCYWNrZ3JvdW5kRW50cnk+KCk7CiAg" +
            "ICAgICAgICAgIEVuc3VyZUVudHJ5KFNjZW5lQmFja2dyb3VuZFNsb3QuTWFpbk1lbnUsICJNYWluIE1lbnUiKTsKICAgICAgICAg" +
            "ICAgRW5zdXJlRW50cnkoU2NlbmVCYWNrZ3JvdW5kU2xvdC5Tb21lcnNldENodXJjaCwgIlNvbWVyc2V0IENodXJjaCIpOwogICAg" +
            "ICAgICAgICBFbnN1cmVFbnRyeShTY2VuZUJhY2tncm91bmRTbG90LkNodXJjaHlhcmRCYXR0bGUsICJDaHVyY2h5YXJkIEJhdHRs" +
            "ZSIpOwogICAgICAgICAgICBFbnN1cmVFbnRyeShTY2VuZUJhY2tncm91bmRTbG90LlZpY3RvcnlPdmVybGF5LCAiVmljdG9yeSBP" +
            "dmVybGF5Iik7CiAgICAgICAgICAgIEVuc3VyZUVudHJ5KFNjZW5lQmFja2dyb3VuZFNsb3QuTWVybGluUmVzdXJyZWN0aW9uLCAi" +
            "TWVybGluIFJlc3VycmVjdGlvbiIpOwogICAgICAgICAgICBFbnN1cmVFbnRyeShTY2VuZUJhY2tncm91bmRTbG90LkxlZ2VuZENv" +
            "ZGV4LCAiTGVnZW5kcyBDb2RleCIpOwogICAgICAgIH0KCiAgICAgICAgdm9pZCBFbnN1cmVFbnRyeShTY2VuZUJhY2tncm91bmRT" +
            "bG90IHNsb3QsIHN0cmluZyBsYWJlbCkKICAgICAgICB7CiAgICAgICAgICAgIGZvciAoaW50IGkgPSAwOyBpIDwgZW50cmllcy5D" +
            "b3VudDsgaSsrKQogICAgICAgICAgICB7CiAgICAgICAgICAgICAgICBpZiAoZW50cmllc1tpXSAhPSBudWxsICYmIGVudHJpZXNb" +
            "aV0uc2xvdCA9PSBzbG90KQogICAgICAgICAgICAgICAgewogICAgICAgICAgICAgICAgICAgIGlmIChzdHJpbmcuSXNOdWxsT3JF" +
            "bXB0eShlbnRyaWVzW2ldLmRpc3BsYXlOYW1lKSkKICAgICAgICAgICAgICAgICAgICAgICAgZW50cmllc1tpXS5kaXNwbGF5TmFt" +
            "ZSA9IGxhYmVsOwogICAgICAgICAgICAgICAgICAgIHJldHVybjsKICAgICAgICAgICAgICAgIH0KICAgICAgICAgICAgfQogICAg" +
            "ICAgICAgICBlbnRyaWVzLkFkZChuZXcgU2NlbmVCYWNrZ3JvdW5kRW50cnkgeyBzbG90ID0gc2xvdCwgZGlzcGxheU5hbWUgPSBs" +
            "YWJlbCB9KTsKICAgICAgICB9CgogICAgICAgIHB1YmxpYyBTY2VuZUJhY2tncm91bmRFbnRyeSBHZXRFbnRyeShTY2VuZUJhY2tn" +
            "cm91bmRTbG90IHNsb3QpCiAgICAgICAgewogICAgICAgICAgICBFbnN1cmVEZWZhdWx0RW50cmllcygpOwogICAgICAgICAgICBm" +
            "b3IgKGludCBpID0gMDsgaSA8IGVudHJpZXMuQ291bnQ7IGkrKykKICAgICAgICAgICAgewogICAgICAgICAgICAgICAgaWYgKGVu" +
            "dHJpZXNbaV0gIT0gbnVsbCAmJiBlbnRyaWVzW2ldLnNsb3QgPT0gc2xvdCkKICAgICAgICAgICAgICAgICAgICByZXR1cm4gZW50" +
            "cmllc1tpXTsKICAgICAgICAgICAgfQogICAgICAgICAgICByZXR1cm4gbnVsbDsKICAgICAgICB9CgogICAgICAgIHB1YmxpYyBi" +
            "b29sIEhhc0Fzc2lnbm1lbnQoU2NlbmVCYWNrZ3JvdW5kU2xvdCBzbG90KQogICAgICAgIHsKICAgICAgICAgICAgdmFyIGUgPSBH" +
            "ZXRFbnRyeShzbG90KTsKICAgICAgICAgICAgcmV0dXJuIGUgIT0gbnVsbCAmJiAoZS5pbWFnZVNwcml0ZSAhPSBudWxsIHx8IGUu" +
            "dmlkZW9DbGlwICE9IG51bGwKICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgfHwgIXN0cmluZy5Jc051bGxPckVtcHR5" +
            "KGUuaW1hZ2VBc3NldFBhdGgpCiAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgIHx8ICFzdHJpbmcuSXNOdWxsT3JFbXB0" +
            "eShlLnZpZGVvQXNzZXRQYXRoKSk7CiAgICAgICAgfQogICAgfQp9Cg==";

        // Assets/Scripts/Data/ClassSpritePoolProfile.cs (9048 bytes)
        const string B64_ClassSpritePoolProfile =
            "dXNpbmcgU3lzdGVtOwp1c2luZyBTeXN0ZW0uQ29sbGVjdGlvbnMuR2VuZXJpYzsKdXNpbmcgVW5pdHlFbmdpbmU7CgpuYW1lc3Bh" +
            "Y2UgQXJ0aHVyaWFuQXV0b2JhdHRsZXIKewogICAgLy8gdGlwOiBmaXgtdGlwZXNjYXBlLXByb2ZpbGUtZW1iZWRzIOKAlCBuYW1l" +
            "ZCBNb25vU2NyaXB0IGZvciBVbml0eSBTTyBiaW5kaW5nCiAgICAvLy8gPHN1bW1hcnk+CiAgICAvLy8gQXJjaGl0ZWN0IGNsYXNz" +
            "IHNwcml0ZSBwb29scyAoUGVhc2FudCAvIFdhcnJpb3IgLyBLbmlnaHQpLiBSZXNvdXJjZXMgcGF0aDoKICAgIC8vLyBDbGFzc1Nw" +
            "cml0ZVBvb2xQcm9maWxlLiBTdGFnZUxheW91dFByb2ZpbGUgcGV3IHNsb3RzIHN0YXkgcG9zaXRpb24vc2NhbGUgZ3VpZGVzIOKA" +
            "lAogICAgLy8vIGNodXJjaC9jb21iYXQgdmlzdWFscyBwaWNrIGZyb20gdGhlc2UgcG9vbHMgYnkgdW5pdCBjbGFzcy4KICAgIC8v" +
            "LyA8L3N1bW1hcnk+CiAgICBbQ3JlYXRlQXNzZXRNZW51KGZpbGVOYW1lID0gIkNsYXNzU3ByaXRlUG9vbFByb2ZpbGUiLCBtZW51" +
            "TmFtZSA9ICJEZW1vbktuaWdodC9DbGFzcyBTcHJpdGUgUG9vbCBQcm9maWxlIildCiAgICBwdWJsaWMgY2xhc3MgQ2xhc3NTcHJp" +
            "dGVQb29sUHJvZmlsZSA6IFNjcmlwdGFibGVPYmplY3QKICAgIHsKICAgICAgICBwdWJsaWMgY29uc3Qgc3RyaW5nIFJlc291cmNl" +
            "c1BhdGggPSAiQ2xhc3NTcHJpdGVQb29sUHJvZmlsZSI7CgogICAgICAgIHB1YmxpYyBMaXN0PFNwcml0ZT4gcGVhc2FudFNwcml0" +
            "ZXMgPSBuZXcgTGlzdDxTcHJpdGU+KCk7CiAgICAgICAgcHVibGljIExpc3Q8U3ByaXRlPiB3YXJyaW9yU3ByaXRlcyA9IG5ldyBM" +
            "aXN0PFNwcml0ZT4oKTsKICAgICAgICBwdWJsaWMgTGlzdDxTcHJpdGU+IGtuaWdodFNwcml0ZXMgPSBuZXcgTGlzdDxTcHJpdGU+" +
            "KCk7CgogICAgICAgIC8vLyA8c3VtbWFyeT5FZGl0b3IgcGF0aCBiYWNrdXBzIHNvIGxpc3QgZW50cmllcyBzdXJ2aXZlIHJlaW1w" +
            "b3J0cy48L3N1bW1hcnk+CiAgICAgICAgcHVibGljIExpc3Q8c3RyaW5nPiBwZWFzYW50QXNzZXRQYXRocyA9IG5ldyBMaXN0PHN0" +
            "cmluZz4oKTsKICAgICAgICBwdWJsaWMgTGlzdDxzdHJpbmc+IHdhcnJpb3JBc3NldFBhdGhzID0gbmV3IExpc3Q8c3RyaW5nPigp" +
            "OwogICAgICAgIHB1YmxpYyBMaXN0PHN0cmluZz4ga25pZ2h0QXNzZXRQYXRocyA9IG5ldyBMaXN0PHN0cmluZz4oKTsKCiAgICAg" +
            "ICAgLy8vIDxzdW1tYXJ5PlRydWUgd2hlbiB0aGlzIGlzIGEgQ3JlYXRlSW5zdGFuY2Ugc3RhbmQtaW4gKG5vdCBhIFJlc291cmNl" +
            "cyBhc3NldCkuPC9zdW1tYXJ5PgogICAgICAgIFtTeXN0ZW0uTm9uU2VyaWFsaXplZF0gcHVibGljIGJvb2wgaXNSdW50aW1lU3Rh" +
            "bmRJbjsKCiAgICAgICAgcHVibGljIHN0YXRpYyBDbGFzc1Nwcml0ZVBvb2xQcm9maWxlIExvYWQoKQogICAgICAgIHsKICAgICAg" +
            "ICAgICAgQ2xhc3NTcHJpdGVQb29sUHJvZmlsZSBwID0gbnVsbDsKICAgICAgICAgICAgdHJ5IHsgcCA9IFJlc291cmNlcy5Mb2Fk" +
            "PENsYXNzU3ByaXRlUG9vbFByb2ZpbGU+KFJlc291cmNlc1BhdGgpOyB9CiAgICAgICAgICAgIGNhdGNoIHsgLyogbWlzc2luZyBN" +
            "b25vU2NyaXB0IEdVSUQvZmlsZUlEIGFmdGVyIGVtYmVkIOKAlCBmYWxsIHRocm91Z2ggKi8gfQogICAgICAgICAgICBpZiAocCAh" +
            "PSBudWxsKQogICAgICAgICAgICB7CiAgICAgICAgICAgICAgICBwLkVuc3VyZUxpc3RzKCk7CiAgICAgICAgICAgICAgICBwLmlz" +
            "UnVudGltZVN0YW5kSW4gPSBmYWxzZTsKICAgICAgICAgICAgICAgIC8vIFBhdGhzIGZpbGxlZCArIFNwcml0ZSBNaXNzaW5nIGFm" +
            "dGVyIFN5bmMg4oaSIHJlbWF0ZXJpYWxpemUgYmVmb3JlIEFwcGxpZXIgcGlja3MuCiAgICAgICAgICAgICAgICBwLlJlbWF0ZXJp" +
            "YWxpemVTcHJpdGVzRnJvbVBhdGhzKCk7CiAgICAgICAgICAgICAgICByZXR1cm4gcDsKICAgICAgICAgICAgfQogICAgICAgICAg" +
            "ICB0cnkKICAgICAgICAgICAgewogICAgICAgICAgICAgICAgdmFyIGFsbCA9IFJlc291cmNlcy5Mb2FkQWxsPENsYXNzU3ByaXRl" +
            "UG9vbFByb2ZpbGU+KCIiKTsKICAgICAgICAgICAgICAgIGlmIChhbGwgIT0gbnVsbCkKICAgICAgICAgICAgICAgIHsKICAgICAg" +
            "ICAgICAgICAgICAgICBmb3IgKGludCBpID0gMDsgaSA8IGFsbC5MZW5ndGg7IGkrKykKICAgICAgICAgICAgICAgICAgICB7CiAg" +
            "ICAgICAgICAgICAgICAgICAgICAgIGlmIChhbGxbaV0gPT0gbnVsbCkgY29udGludWU7CiAgICAgICAgICAgICAgICAgICAgICAg" +
            "IGFsbFtpXS5FbnN1cmVMaXN0cygpOwogICAgICAgICAgICAgICAgICAgICAgICBhbGxbaV0uaXNSdW50aW1lU3RhbmRJbiA9IGZh" +
            "bHNlOwogICAgICAgICAgICAgICAgICAgICAgICBhbGxbaV0uUmVtYXRlcmlhbGl6ZVNwcml0ZXNGcm9tUGF0aHMoKTsKICAgICAg" +
            "ICAgICAgICAgICAgICAgICAgcmV0dXJuIGFsbFtpXTsKICAgICAgICAgICAgICAgICAgICB9CiAgICAgICAgICAgICAgICB9CiAg" +
            "ICAgICAgICAgIH0KICAgICAgICAgICAgY2F0Y2ggeyAvKiBicm9rZW4gUmVzb3VyY2VzIGFzc2V0IC8gbWlzc2luZyBzY3JpcHQg" +
            "4oCUIHNvZnQgc3RhbmQtaW4gKi8gfQogICAgICAgICAgICBwID0gQ3JlYXRlSW5zdGFuY2U8Q2xhc3NTcHJpdGVQb29sUHJvZmls" +
            "ZT4oKTsKICAgICAgICAgICAgcC5uYW1lID0gIkNsYXNzU3ByaXRlUG9vbFByb2ZpbGVfUnVudGltZSI7CiAgICAgICAgICAgIHAu" +
            "aXNSdW50aW1lU3RhbmRJbiA9IHRydWU7CiAgICAgICAgICAgIHAuRW5zdXJlTGlzdHMoKTsKICAgICAgICAgICAgcmV0dXJuIHA7" +
            "CiAgICAgICAgfQoKICAgICAgICBwdWJsaWMgdm9pZCBFbnN1cmVMaXN0cygpCiAgICAgICAgewogICAgICAgICAgICBwZWFzYW50" +
            "U3ByaXRlcyA/Pz0gbmV3IExpc3Q8U3ByaXRlPigpOwogICAgICAgICAgICB3YXJyaW9yU3ByaXRlcyA/Pz0gbmV3IExpc3Q8U3By" +
            "aXRlPigpOwogICAgICAgICAgICBrbmlnaHRTcHJpdGVzID8/PSBuZXcgTGlzdDxTcHJpdGU+KCk7CiAgICAgICAgICAgIHBlYXNh" +
            "bnRBc3NldFBhdGhzID8/PSBuZXcgTGlzdDxzdHJpbmc+KCk7CiAgICAgICAgICAgIHdhcnJpb3JBc3NldFBhdGhzID8/PSBuZXcg" +
            "TGlzdDxzdHJpbmc+KCk7CiAgICAgICAgICAgIGtuaWdodEFzc2V0UGF0aHMgPz89IG5ldyBMaXN0PHN0cmluZz4oKTsKICAgICAg" +
            "ICB9CgogICAgICAgIC8vLyA8c3VtbWFyeT4KICAgICAgICAvLy8gUmVzdG9yZSBNaXNzaW5nIHNwcml0ZSByZWZzIGZyb20gcGF0" +
            "aCBiYWNrdXBzIChlZGl0b3IgQXNzZXREYXRhYmFzZSkuCiAgICAgICAgLy8vIE5ldmVyIHdpcGUgcGF0aCBiYWNrdXBzIHdoZW4g" +
            "c3ByaXRlcyBhcmUgdGVtcG9yYXJpbHkgbnVsbCDigJQgUGVyc2lzdCBzeW5jcwogICAgICAgIC8vLyBwYXRocyBvbmx5IHdoZW4g" +
            "YSBsaXZlIHNwcml0ZSBpcyBwcmVzZW50LgogICAgICAgIC8vLyA8L3N1bW1hcnk+CiAgICAgICAgcHVibGljIGludCBSZW1hdGVy" +
            "aWFsaXplU3ByaXRlc0Zyb21QYXRocygpCiAgICAgICAgewogICAgICAgICAgICBFbnN1cmVMaXN0cygpOwogICAgICAgICAgICBp" +
            "bnQgbiA9IDA7CiAgICAgICAgICAgIG4gKz0gUmVtYXRlcmlhbGl6ZUxpc3QocGVhc2FudFNwcml0ZXMsIHBlYXNhbnRBc3NldFBh" +
            "dGhzKTsKICAgICAgICAgICAgbiArPSBSZW1hdGVyaWFsaXplTGlzdCh3YXJyaW9yU3ByaXRlcywgd2FycmlvckFzc2V0UGF0aHMp" +
            "OwogICAgICAgICAgICBuICs9IFJlbWF0ZXJpYWxpemVMaXN0KGtuaWdodFNwcml0ZXMsIGtuaWdodEFzc2V0UGF0aHMpOwogICAg" +
            "ICAgICAgICByZXR1cm4gbjsKICAgICAgICB9CgogICAgICAgIHB1YmxpYyBzdGF0aWMgaW50IFJlbWF0ZXJpYWxpemVMaXN0KExp" +
            "c3Q8U3ByaXRlPiBzcHJpdGVzLCBMaXN0PHN0cmluZz4gcGF0aHMpCiAgICAgICAgewogICAgICAgICAgICBpZiAoc3ByaXRlcyA9" +
            "PSBudWxsIHx8IHBhdGhzID09IG51bGwpIHJldHVybiAwOwogICAgICAgICAgICBpbnQgcmVzdG9yZWQgPSAwOwogICAgICAgICAg" +
            "ICBpbnQgY291bnQgPSBNYXRoZi5NYXgoc3ByaXRlcy5Db3VudCwgcGF0aHMuQ291bnQpOwogICAgICAgICAgICB3aGlsZSAoc3By" +
            "aXRlcy5Db3VudCA8IGNvdW50KSBzcHJpdGVzLkFkZChudWxsKTsKICAgICAgICAgICAgd2hpbGUgKHBhdGhzLkNvdW50IDwgY291" +
            "bnQpIHBhdGhzLkFkZCgiIik7CiAgICAgICAgICAgIGZvciAoaW50IGkgPSAwOyBpIDwgY291bnQ7IGkrKykKICAgICAgICAgICAg" +
            "ewogICAgICAgICAgICAgICAgaWYgKHNwcml0ZXNbaV0gIT0gbnVsbCkgY29udGludWU7CiAgICAgICAgICAgICAgICBpZiAoc3Ry" +
            "aW5nLklzTnVsbE9yRW1wdHkocGF0aHNbaV0pKSBjb250aW51ZTsKICAgICAgICAgICAgICAgIFNwcml0ZSBzcCA9IFRyeUxvYWRT" +
            "cHJpdGVBdFBhdGgocGF0aHNbaV0pOwogICAgICAgICAgICAgICAgaWYgKHNwID09IG51bGwpIGNvbnRpbnVlOwogICAgICAgICAg" +
            "ICAgICAgc3ByaXRlc1tpXSA9IHNwOwogICAgICAgICAgICAgICAgcmVzdG9yZWQrKzsKICAgICAgICAgICAgfQogICAgICAgICAg" +
            "ICByZXR1cm4gcmVzdG9yZWQ7CiAgICAgICAgfQoKICAgICAgICBwdWJsaWMgc3RhdGljIFNwcml0ZSBUcnlMb2FkU3ByaXRlQXRQ" +
            "YXRoKHN0cmluZyBwYXRoKQogICAgICAgIHsKICAgICAgICAgICAgaWYgKHN0cmluZy5Jc051bGxPckVtcHR5KHBhdGgpKSByZXR1" +
            "cm4gbnVsbDsKICAgICAgICAgICAgc3RyaW5nIG5vcm0gPSBwYXRoLlJlcGxhY2UoIlxcIiwgIi8iKTsKI2lmIFVOSVRZX0VESVRP" +
            "UgogICAgICAgICAgICB0cnkKICAgICAgICAgICAgewogICAgICAgICAgICAgICAgdmFyIHNwID0gVW5pdHlFZGl0b3IuQXNzZXRE" +
            "YXRhYmFzZS5Mb2FkQXNzZXRBdFBhdGg8U3ByaXRlPihub3JtKTsKICAgICAgICAgICAgICAgIGlmIChzcCAhPSBudWxsKSByZXR1" +
            "cm4gc3A7CiAgICAgICAgICAgICAgICAvLyBUZXh0dXJlIHdpdGggbXVsdGlwbGUgc3ByaXRlcyDigJQgdGFrZSBmaXJzdCBzdWIt" +
            "YXNzZXQgU3ByaXRlLgogICAgICAgICAgICAgICAgdmFyIG9ianMgPSBVbml0eUVkaXRvci5Bc3NldERhdGFiYXNlLkxvYWRBbGxB" +
            "c3NldHNBdFBhdGgobm9ybSk7CiAgICAgICAgICAgICAgICBpZiAob2JqcyAhPSBudWxsKQogICAgICAgICAgICAgICAgewogICAg" +
            "ICAgICAgICAgICAgICAgIGZvciAoaW50IGkgPSAwOyBpIDwgb2Jqcy5MZW5ndGg7IGkrKykKICAgICAgICAgICAgICAgICAgICAg" +
            "ICAgaWYgKG9ianNbaV0gaXMgU3ByaXRlIHMpIHJldHVybiBzOwogICAgICAgICAgICAgICAgfQogICAgICAgICAgICB9CiAgICAg" +
            "ICAgICAgIGNhdGNoIHsgLyogaWdub3JlICovIH0KI2VuZGlmCiAgICAgICAgICAgIC8vIFBsYXllciBidWlsZHM6IFJlc291cmNl" +
            "cy5Mb2FkIChwcm9maWxlcyArIHByb21vdGVkIEFyY2hpdGVjdEFydCBjb3BpZXMpLgogICAgICAgICAgICB0cnkKICAgICAgICAg" +
            "ICAgewogICAgICAgICAgICAgICAgaW50IGlkeCA9IG5vcm0uSW5kZXhPZigiUmVzb3VyY2VzLyIsIFN5c3RlbS5TdHJpbmdDb21w" +
            "YXJpc29uLk9yZGluYWxJZ25vcmVDYXNlKTsKICAgICAgICAgICAgICAgIGlmIChpZHggPj0gMCkKICAgICAgICAgICAgICAgIHsK" +
            "ICAgICAgICAgICAgICAgICAgICBzdHJpbmcgcmVzID0gbm9ybS5TdWJzdHJpbmcoaWR4ICsgIlJlc291cmNlcy8iLkxlbmd0aCk7" +
            "CiAgICAgICAgICAgICAgICAgICAgaW50IGRvdCA9IHJlcy5MYXN0SW5kZXhPZignLicpOwogICAgICAgICAgICAgICAgICAgIGlm" +
            "IChkb3QgPiAwKSByZXMgPSByZXMuU3Vic3RyaW5nKDAsIGRvdCk7CiAgICAgICAgICAgICAgICAgICAgdmFyIGZyb21SZXMgPSBS" +
            "ZXNvdXJjZXMuTG9hZDxTcHJpdGU+KHJlcyk7CiAgICAgICAgICAgICAgICAgICAgaWYgKGZyb21SZXMgIT0gbnVsbCkgcmV0dXJu" +
            "IGZyb21SZXM7CiAgICAgICAgICAgICAgICAgICAgdmFyIGFsbCA9IFJlc291cmNlcy5Mb2FkQWxsPFNwcml0ZT4ocmVzKTsKICAg" +
            "ICAgICAgICAgICAgICAgICBpZiAoYWxsICE9IG51bGwgJiYgYWxsLkxlbmd0aCA+IDAgJiYgYWxsWzBdICE9IG51bGwpIHJldHVy" +
            "biBhbGxbMF07CiAgICAgICAgICAgICAgICB9CiAgICAgICAgICAgICAgICAvLyBCYXJlIFJlc291cmNlcyBrZXkgKG5vIEFzc2V0" +
            "cy8gcHJlZml4KSDigJQgQXJjaGl0ZWN0QXJ0L0JvZHkvZm9vCiAgICAgICAgICAgICAgICBpZiAoIW5vcm0uU3RhcnRzV2l0aCgi" +
            "QXNzZXRzLyIsIFN5c3RlbS5TdHJpbmdDb21wYXJpc29uLk9yZGluYWxJZ25vcmVDYXNlKQogICAgICAgICAgICAgICAgICAgICYm" +
            "IG5vcm0uSW5kZXhPZignOicpIDwgMCkKICAgICAgICAgICAgICAgIHsKICAgICAgICAgICAgICAgICAgICBzdHJpbmcga2V5ID0g" +
            "bm9ybTsKICAgICAgICAgICAgICAgICAgICBpbnQgZG90ID0ga2V5Lkxhc3RJbmRleE9mKCcuJyk7CiAgICAgICAgICAgICAgICAg" +
            "ICAgaWYgKGRvdCA+IDApIGtleSA9IGtleS5TdWJzdHJpbmcoMCwgZG90KTsKICAgICAgICAgICAgICAgICAgICB2YXIgYmFyZSA9" +
            "IFJlc291cmNlcy5Mb2FkPFNwcml0ZT4oa2V5KTsKICAgICAgICAgICAgICAgICAgICBpZiAoYmFyZSAhPSBudWxsKSByZXR1cm4g" +
            "YmFyZTsKICAgICAgICAgICAgICAgIH0KICAgICAgICAgICAgfQogICAgICAgICAgICBjYXRjaCB7IC8qIGlnbm9yZSAqLyB9Cgog" +
            "ICAgICAgICAgICAvLyBTdHJlYW1pbmdBc3NldHMgZmFsbGJhY2sgKHBuZy9qcGcgY29waWVkIGJ5IGJ1aWxkIHByZXBhcmUpLiBT" +
            "b2Z0LWZhaWwg4oCUIG5vIHdpcGUuCiAgICAgICAgICAgIHRyeQogICAgICAgICAgICB7CiAgICAgICAgICAgICAgICBzdHJpbmcg" +
            "ZmlsZU5hbWUgPSBTeXN0ZW0uSU8uUGF0aC5HZXRGaWxlTmFtZShub3JtKTsKICAgICAgICAgICAgICAgIGlmICghc3RyaW5nLklz" +
            "TnVsbE9yRW1wdHkoZmlsZU5hbWUpKQogICAgICAgICAgICAgICAgewogICAgICAgICAgICAgICAgICAgIHN0cmluZyBzYSA9IFN5" +
            "c3RlbS5JTy5QYXRoLkNvbWJpbmUoQXBwbGljYXRpb24uc3RyZWFtaW5nQXNzZXRzUGF0aCwgIkRlbW9uS25pZ2h0QXJjaGl0ZWN0" +
            "IiwgZmlsZU5hbWUpOwogICAgICAgICAgICAgICAgICAgIGlmIChTeXN0ZW0uSU8uRmlsZS5FeGlzdHMoc2EpKQogICAgICAgICAg" +
            "ICAgICAgICAgIHsKICAgICAgICAgICAgICAgICAgICAgICAgYnl0ZVtdIGJ5dGVzID0gU3lzdGVtLklPLkZpbGUuUmVhZEFsbEJ5" +
            "dGVzKHNhKTsKICAgICAgICAgICAgICAgICAgICAgICAgdmFyIHRleCA9IG5ldyBUZXh0dXJlMkQoMiwgMiwgVGV4dHVyZUZvcm1h" +
            "dC5SR0JBMzIsIGZhbHNlKTsKICAgICAgICAgICAgICAgICAgICAgICAgaWYgKHRleC5Mb2FkSW1hZ2UoYnl0ZXMpKQogICAgICAg" +
            "ICAgICAgICAgICAgICAgICB7CiAgICAgICAgICAgICAgICAgICAgICAgICAgICB0ZXgubmFtZSA9IFN5c3RlbS5JTy5QYXRoLkdl" +
            "dEZpbGVOYW1lV2l0aG91dEV4dGVuc2lvbihmaWxlTmFtZSk7CiAgICAgICAgICAgICAgICAgICAgICAgICAgICByZXR1cm4gU3By" +
            "aXRlLkNyZWF0ZSgKICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICB0ZXgsCiAgICAgICAgICAgICAgICAgICAgICAgICAg" +
            "ICAgICAgbmV3IFJlY3QoMCwgMCwgdGV4LndpZHRoLCB0ZXguaGVpZ2h0KSwKICAgICAgICAgICAgICAgICAgICAgICAgICAgICAg" +
            "ICBuZXcgVmVjdG9yMigwLjVmLCAwLjVmKSwKICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAxMDBmKTsKICAgICAgICAg" +
            "ICAgICAgICAgICAgICAgfQogICAgICAgICAgICAgICAgICAgIH0KICAgICAgICAgICAgICAgIH0KICAgICAgICAgICAgfQogICAg" +
            "ICAgICAgICBjYXRjaCB7IC8qIGlnbm9yZSAqLyB9CiAgICAgICAgICAgIHJldHVybiBudWxsOwogICAgICAgIH0KCiAgICAgICAg" +
            "cHVibGljIExpc3Q8U3ByaXRlPiBHZXRQb29sKFVuaXRUeXBlIHR5cGUpCiAgICAgICAgewogICAgICAgICAgICBFbnN1cmVMaXN0" +
            "cygpOwogICAgICAgICAgICByZXR1cm4gdHlwZSBzd2l0Y2gKICAgICAgICAgICAgewogICAgICAgICAgICAgICAgVW5pdFR5cGUu" +
            "UGVhc2FudCA9PiBwZWFzYW50U3ByaXRlcywKICAgICAgICAgICAgICAgIFVuaXRUeXBlLldhcnJpb3IgPT4gd2FycmlvclNwcml0" +
            "ZXMsCiAgICAgICAgICAgICAgICBVbml0VHlwZS5LbmlnaHQgPT4ga25pZ2h0U3ByaXRlcywKICAgICAgICAgICAgICAgIF8gPT4g" +
            "bnVsbAogICAgICAgICAgICB9OwogICAgICAgIH0KCiAgICAgICAgcHVibGljIGludCBDb3VudFVzYWJsZShVbml0VHlwZSB0eXBl" +
            "KQogICAgICAgIHsKICAgICAgICAgICAgdmFyIHBvb2wgPSBHZXRQb29sKHR5cGUpOwogICAgICAgICAgICBpZiAocG9vbCA9PSBu" +
            "dWxsKSByZXR1cm4gMDsKICAgICAgICAgICAgRW5zdXJlTGlzdHMoKTsKICAgICAgICAgICAgTGlzdDxzdHJpbmc+IHBhdGhzID0g" +
            "dHlwZSBzd2l0Y2gKICAgICAgICAgICAgewogICAgICAgICAgICAgICAgVW5pdFR5cGUuUGVhc2FudCA9PiBwZWFzYW50QXNzZXRQ" +
            "YXRocywKICAgICAgICAgICAgICAgIFVuaXRUeXBlLldhcnJpb3IgPT4gd2FycmlvckFzc2V0UGF0aHMsCiAgICAgICAgICAgICAg" +
            "ICBVbml0VHlwZS5LbmlnaHQgPT4ga25pZ2h0QXNzZXRQYXRocywKICAgICAgICAgICAgICAgIF8gPT4gbnVsbAogICAgICAgICAg" +
            "ICB9OwogICAgICAgICAgICBpbnQgbiA9IDA7CiAgICAgICAgICAgIGludCBjb3VudCA9IHBvb2wuQ291bnQ7CiAgICAgICAgICAg" +
            "IGlmIChwYXRocyAhPSBudWxsICYmIHBhdGhzLkNvdW50ID4gY291bnQpIGNvdW50ID0gcGF0aHMuQ291bnQ7CiAgICAgICAgICAg" +
            "IGZvciAoaW50IGkgPSAwOyBpIDwgY291bnQ7IGkrKykKICAgICAgICAgICAgewogICAgICAgICAgICAgICAgaWYgKGkgPCBwb29s" +
            "LkNvdW50ICYmIHBvb2xbaV0gIT0gbnVsbCkgeyBuKys7IGNvbnRpbnVlOyB9CiAgICAgICAgICAgICAgICAvLyBQYXRoIGJhY2t1" +
            "cCBjb3VudHMgYXMgdXNhYmxlIOKAlCBSZW1hdGVyaWFsaXplIHJlc3RvcmVzIHRoZSBNaXNzaW5nIFNwcml0ZSByZWYuCiAgICAg" +
            "ICAgICAgICAgICBpZiAocGF0aHMgIT0gbnVsbCAmJiBpIDwgcGF0aHMuQ291bnQgJiYgIXN0cmluZy5Jc051bGxPckVtcHR5KHBh" +
            "dGhzW2ldKSkgbisrOwogICAgICAgICAgICB9CiAgICAgICAgICAgIHJldHVybiBuOwogICAgICAgIH0KICAgIH0KfQo=";

        // Assets/Scripts/Data/LegendPewPortraitProfile.cs (9575 bytes)
        const string B64_LegendPewPortraitProfile =
            "dXNpbmcgU3lzdGVtOwp1c2luZyBTeXN0ZW0uQ29sbGVjdGlvbnMuR2VuZXJpYzsKdXNpbmcgVW5pdHlFbmdpbmU7CgpuYW1lc3Bh" +
            "Y2UgQXJ0aHVyaWFuQXV0b2JhdHRsZXIKewogICAgLy8gdGlwOiBmaXgtdGlwZXNjYXBlLXByb2ZpbGUtZW1iZWRzIOKAlCBuYW1l" +
            "ZCBNb25vU2NyaXB0IGZvciBVbml0eSBTTyBiaW5kaW5nCiAgICAvLy8gPHN1bW1hcnk+T25lIExlZ2VuZCBmYWNlLXBvcnRyYWl0" +
            "IHNsb3QgKEFyY2hpdGVjdCBUYWIgNSkuPC9zdW1tYXJ5PgogICAgW1NlcmlhbGl6YWJsZV0KICAgIHB1YmxpYyBjbGFzcyBMZWdl" +
            "bmRQb3J0cmFpdEVudHJ5CiAgICB7CiAgICAgICAgcHVibGljIFVuaXRUeXBlIHVuaXRUeXBlOwogICAgICAgIHB1YmxpYyBzdHJp" +
            "bmcgZGlzcGxheU5hbWU7CiAgICAgICAgcHVibGljIFNwcml0ZSBmYWNlUG9ydHJhaXQ7CiAgICAgICAgcHVibGljIHN0cmluZyBh" +
            "c3NldFBhdGg7CiAgICAgICAgW1Rvb2x0aXAoIk1lcmxpbiBhbmQgb3RoZXIgc29mdCBzbG90cyBzdGF5IGluIHRoZSBsaXN0IGJ1" +
            "dCBhcmUgbm90IHJlcXVpcmVkIGZvciBwbGF5YWJsZSBDb2RleC4iKV0KICAgICAgICBwdWJsaWMgYm9vbCBzb2Z0T3B0aW9uYWw7" +
            "CiAgICB9CgogICAgLy8vIDxzdW1tYXJ5PgogICAgLy8vIEFyY2hpdGVjdCBsZWdlbmQgZmFjZSBwb3J0cmFpdHMgKyB0b3duc2Zv" +
            "bGsgcGV3IEZBQ0UtT05MWSBwcm9maWxlIHBvb2xzLgogICAgLy8vIERpc3RpbmN0IGZyb20gQ2xhc3NTcHJpdGVQb29sUHJvZmls" +
            "ZSAoZnVsbC1ib2R5IHBldy9jb21iYXQgYXZhdGFycykuIFJlc291cmNlcyBwYXRoOgogICAgLy8vIExlZ2VuZFBld1BvcnRyYWl0" +
            "UHJvZmlsZS4gTmV2ZXIgYXNzaWduIHRoZXNlIGFzIGJvZHlTcHJpdGUuCiAgICAvLy8gPC9zdW1tYXJ5PgogICAgW0NyZWF0ZUFz" +
            "c2V0TWVudShmaWxlTmFtZSA9ICJMZWdlbmRQZXdQb3J0cmFpdFByb2ZpbGUiLCBtZW51TmFtZSA9ICJEZW1vbktuaWdodC9MZWdl" +
            "bmQgUGV3IFBvcnRyYWl0IFByb2ZpbGUiKV0KICAgIHB1YmxpYyBjbGFzcyBMZWdlbmRQZXdQb3J0cmFpdFByb2ZpbGUgOiBTY3Jp" +
            "cHRhYmxlT2JqZWN0CiAgICB7CiAgICAgICAgcHVibGljIGNvbnN0IHN0cmluZyBSZXNvdXJjZXNQYXRoID0gIkxlZ2VuZFBld1Bv" +
            "cnRyYWl0UHJvZmlsZSI7CgogICAgICAgIC8vLyA8c3VtbWFyeT4xNCBwbGF5YWJsZSBsZWdlbmRzICsgc29mdCBNZXJsaW4gc2xv" +
            "dCAobGFzdCkuPC9zdW1tYXJ5PgogICAgICAgIHB1YmxpYyBzdGF0aWMgcmVhZG9ubHkgVW5pdFR5cGVbXSBDYW5vbmljYWxMZWdl" +
            "bmRTbG90cyA9CiAgICAgICAgewogICAgICAgICAgICBVbml0VHlwZS5TaXJLYXksCiAgICAgICAgICAgIFVuaXRUeXBlLlNpckJl" +
            "ZGl2ZXJlLAogICAgICAgICAgICBVbml0VHlwZS5MYWR5R3VpbmV2ZXJlLAogICAgICAgICAgICBVbml0VHlwZS5BcmNoUHJpZXN0" +
            "T2ZHbGFzdG9uYnVyeSwKICAgICAgICAgICAgVW5pdFR5cGUuU2lyR2F3YWluLAogICAgICAgICAgICBVbml0VHlwZS5TaXJUcmlz" +
            "dGFuLAogICAgICAgICAgICBVbml0VHlwZS5TaXJMYW5jZWxvdCwKICAgICAgICAgICAgVW5pdFR5cGUuS2luZ0FydGh1ciwKICAg" +
            "ICAgICAgICAgVW5pdFR5cGUuU2lyR2FsYWhhZCwKICAgICAgICAgICAgVW5pdFR5cGUuU2lyUGVyY2l2YWwsCiAgICAgICAgICAg" +
            "IFVuaXRUeXBlLk5pbXVlLAogICAgICAgICAgICBVbml0VHlwZS5Nb3JkcmVkLAogICAgICAgICAgICBVbml0VHlwZS5Nb3JnYW5M" +
            "ZUZheSwKICAgICAgICAgICAgVW5pdFR5cGUuVGhlR3JlZW5LbmlnaHQsCiAgICAgICAgICAgIFVuaXRUeXBlLk1lcmxpbiwgLy8g" +
            "c29mdCDigJQgb3V0IG9mIHBsYXlhYmxlIENvZGV4IHBvb2wKICAgICAgICB9OwoKICAgICAgICBwdWJsaWMgTGlzdDxMZWdlbmRQ" +
            "b3J0cmFpdEVudHJ5PiBsZWdlbmRQb3J0cmFpdHMgPSBuZXcgTGlzdDxMZWdlbmRQb3J0cmFpdEVudHJ5PigpOwoKICAgICAgICBb" +
            "SGVhZGVyKCJUb3duc2ZvbGsgcGV3IEZBQ0UgcHJvZmlsZXMgb25seSAobmV2ZXIgYm9keVNwcml0ZSDigJQgZGlzdGluY3QgZnJv" +
            "bSBUYWIgNCBDbGFzc1Nwcml0ZVBvb2wpIildCiAgICAgICAgcHVibGljIExpc3Q8U3ByaXRlPiBwZWFzYW50RmFjZVByb2ZpbGVz" +
            "ID0gbmV3IExpc3Q8U3ByaXRlPigpOwogICAgICAgIHB1YmxpYyBMaXN0PFNwcml0ZT4gd2FycmlvckZhY2VQcm9maWxlcyA9IG5l" +
            "dyBMaXN0PFNwcml0ZT4oKTsKICAgICAgICBwdWJsaWMgTGlzdDxTcHJpdGU+IGtuaWdodEZhY2VQcm9maWxlcyA9IG5ldyBMaXN0" +
            "PFNwcml0ZT4oKTsKCiAgICAgICAgcHVibGljIExpc3Q8c3RyaW5nPiBwZWFzYW50RmFjZUFzc2V0UGF0aHMgPSBuZXcgTGlzdDxz" +
            "dHJpbmc+KCk7CiAgICAgICAgcHVibGljIExpc3Q8c3RyaW5nPiB3YXJyaW9yRmFjZUFzc2V0UGF0aHMgPSBuZXcgTGlzdDxzdHJp" +
            "bmc+KCk7CiAgICAgICAgcHVibGljIExpc3Q8c3RyaW5nPiBrbmlnaHRGYWNlQXNzZXRQYXRocyA9IG5ldyBMaXN0PHN0cmluZz4o" +
            "KTsKCiAgICAgICAgW1N5c3RlbS5Ob25TZXJpYWxpemVkXSBwdWJsaWMgYm9vbCBpc1J1bnRpbWVTdGFuZEluOwoKICAgICAgICBw" +
            "dWJsaWMgc3RhdGljIExlZ2VuZFBld1BvcnRyYWl0UHJvZmlsZSBMb2FkKCkKICAgICAgICB7CiAgICAgICAgICAgIExlZ2VuZFBl" +
            "d1BvcnRyYWl0UHJvZmlsZSBwID0gbnVsbDsKICAgICAgICAgICAgdHJ5IHsgcCA9IFJlc291cmNlcy5Mb2FkPExlZ2VuZFBld1Bv" +
            "cnRyYWl0UHJvZmlsZT4oUmVzb3VyY2VzUGF0aCk7IH0KICAgICAgICAgICAgY2F0Y2ggeyAvKiBtaXNzaW5nIE1vbm9TY3JpcHQg" +
            "R1VJRC9maWxlSUQgYWZ0ZXIgZW1iZWQg4oCUIGZhbGwgdGhyb3VnaCAqLyB9CiAgICAgICAgICAgIGlmIChwICE9IG51bGwpCiAg" +
            "ICAgICAgICAgIHsKICAgICAgICAgICAgICAgIHAuRW5zdXJlU2xvdHNBbmRMaXN0cygpOwogICAgICAgICAgICAgICAgcC5pc1J1" +
            "bnRpbWVTdGFuZEluID0gZmFsc2U7CiAgICAgICAgICAgICAgICBwLlJlbWF0ZXJpYWxpemVTcHJpdGVzRnJvbVBhdGhzKCk7CiAg" +
            "ICAgICAgICAgICAgICByZXR1cm4gcDsKICAgICAgICAgICAgfQogICAgICAgICAgICB0cnkKICAgICAgICAgICAgewogICAgICAg" +
            "ICAgICAgICAgdmFyIGFsbCA9IFJlc291cmNlcy5Mb2FkQWxsPExlZ2VuZFBld1BvcnRyYWl0UHJvZmlsZT4oIiIpOwogICAgICAg" +
            "ICAgICAgICAgaWYgKGFsbCAhPSBudWxsKQogICAgICAgICAgICAgICAgewogICAgICAgICAgICAgICAgICAgIGZvciAoaW50IGkg" +
            "PSAwOyBpIDwgYWxsLkxlbmd0aDsgaSsrKQogICAgICAgICAgICAgICAgICAgIHsKICAgICAgICAgICAgICAgICAgICAgICAgaWYg" +
            "KGFsbFtpXSA9PSBudWxsKSBjb250aW51ZTsKICAgICAgICAgICAgICAgICAgICAgICAgYWxsW2ldLkVuc3VyZVNsb3RzQW5kTGlz" +
            "dHMoKTsKICAgICAgICAgICAgICAgICAgICAgICAgYWxsW2ldLmlzUnVudGltZVN0YW5kSW4gPSBmYWxzZTsKICAgICAgICAgICAg" +
            "ICAgICAgICAgICAgYWxsW2ldLlJlbWF0ZXJpYWxpemVTcHJpdGVzRnJvbVBhdGhzKCk7CiAgICAgICAgICAgICAgICAgICAgICAg" +
            "IHJldHVybiBhbGxbaV07CiAgICAgICAgICAgICAgICAgICAgfQogICAgICAgICAgICAgICAgfQogICAgICAgICAgICB9CiAgICAg" +
            "ICAgICAgIGNhdGNoIHsgLyogYnJva2VuIFJlc291cmNlcyBhc3NldCAvIG1pc3Npbmcgc2NyaXB0IOKAlCBzb2Z0IHN0YW5kLWlu" +
            "ICovIH0KICAgICAgICAgICAgcCA9IENyZWF0ZUluc3RhbmNlPExlZ2VuZFBld1BvcnRyYWl0UHJvZmlsZT4oKTsKICAgICAgICAg" +
            "ICAgcC5uYW1lID0gIkxlZ2VuZFBld1BvcnRyYWl0UHJvZmlsZV9SdW50aW1lIjsKICAgICAgICAgICAgcC5pc1J1bnRpbWVTdGFu" +
            "ZEluID0gdHJ1ZTsKICAgICAgICAgICAgcC5FbnN1cmVTbG90c0FuZExpc3RzKCk7CiAgICAgICAgICAgIHJldHVybiBwOwogICAg" +
            "ICAgIH0KCiAgICAgICAgLy8vIDxzdW1tYXJ5PlJlc3RvcmUgTWlzc2luZyBmYWNlL3Bvb2wgc3ByaXRlcyBmcm9tIHBhdGggYmFj" +
            "a3VwcyAoZWRpdG9yICsgUmVzb3VyY2VzKS48L3N1bW1hcnk+CiAgICAgICAgcHVibGljIGludCBSZW1hdGVyaWFsaXplU3ByaXRl" +
            "c0Zyb21QYXRocygpCiAgICAgICAgewogICAgICAgICAgICBFbnN1cmVTbG90c0FuZExpc3RzKCk7CiAgICAgICAgICAgIGludCBu" +
            "ID0gMDsKICAgICAgICAgICAgbiArPSBDbGFzc1Nwcml0ZVBvb2xQcm9maWxlLlJlbWF0ZXJpYWxpemVMaXN0KHBlYXNhbnRGYWNl" +
            "UHJvZmlsZXMsIHBlYXNhbnRGYWNlQXNzZXRQYXRocyk7CiAgICAgICAgICAgIG4gKz0gQ2xhc3NTcHJpdGVQb29sUHJvZmlsZS5S" +
            "ZW1hdGVyaWFsaXplTGlzdCh3YXJyaW9yRmFjZVByb2ZpbGVzLCB3YXJyaW9yRmFjZUFzc2V0UGF0aHMpOwogICAgICAgICAgICBu" +
            "ICs9IENsYXNzU3ByaXRlUG9vbFByb2ZpbGUuUmVtYXRlcmlhbGl6ZUxpc3Qoa25pZ2h0RmFjZVByb2ZpbGVzLCBrbmlnaHRGYWNl" +
            "QXNzZXRQYXRocyk7CiAgICAgICAgICAgIGlmIChsZWdlbmRQb3J0cmFpdHMgIT0gbnVsbCkKICAgICAgICAgICAgewogICAgICAg" +
            "ICAgICAgICAgZm9yIChpbnQgaSA9IDA7IGkgPCBsZWdlbmRQb3J0cmFpdHMuQ291bnQ7IGkrKykKICAgICAgICAgICAgICAgIHsK" +
            "ICAgICAgICAgICAgICAgICAgICB2YXIgZSA9IGxlZ2VuZFBvcnRyYWl0c1tpXTsKICAgICAgICAgICAgICAgICAgICBpZiAoZSA9" +
            "PSBudWxsIHx8IGUuZmFjZVBvcnRyYWl0ICE9IG51bGwpIGNvbnRpbnVlOwogICAgICAgICAgICAgICAgICAgIGlmIChzdHJpbmcu" +
            "SXNOdWxsT3JFbXB0eShlLmFzc2V0UGF0aCkpIGNvbnRpbnVlOwogICAgICAgICAgICAgICAgICAgIFNwcml0ZSBzcCA9IENsYXNz" +
            "U3ByaXRlUG9vbFByb2ZpbGUuVHJ5TG9hZFNwcml0ZUF0UGF0aChlLmFzc2V0UGF0aCk7CiAgICAgICAgICAgICAgICAgICAgaWYg" +
            "KHNwID09IG51bGwpIGNvbnRpbnVlOwogICAgICAgICAgICAgICAgICAgIGUuZmFjZVBvcnRyYWl0ID0gc3A7CiAgICAgICAgICAg" +
            "ICAgICAgICAgbisrOwogICAgICAgICAgICAgICAgfQogICAgICAgICAgICB9CiAgICAgICAgICAgIHJldHVybiBuOwogICAgICAg" +
            "IH0KCiAgICAgICAgcHVibGljIHZvaWQgRW5zdXJlU2xvdHNBbmRMaXN0cygpCiAgICAgICAgewogICAgICAgICAgICBsZWdlbmRQ" +
            "b3J0cmFpdHMgPz89IG5ldyBMaXN0PExlZ2VuZFBvcnRyYWl0RW50cnk+KCk7CiAgICAgICAgICAgIHBlYXNhbnRGYWNlUHJvZmls" +
            "ZXMgPz89IG5ldyBMaXN0PFNwcml0ZT4oKTsKICAgICAgICAgICAgd2FycmlvckZhY2VQcm9maWxlcyA/Pz0gbmV3IExpc3Q8U3By" +
            "aXRlPigpOwogICAgICAgICAgICBrbmlnaHRGYWNlUHJvZmlsZXMgPz89IG5ldyBMaXN0PFNwcml0ZT4oKTsKICAgICAgICAgICAg" +
            "cGVhc2FudEZhY2VBc3NldFBhdGhzID8/PSBuZXcgTGlzdDxzdHJpbmc+KCk7CiAgICAgICAgICAgIHdhcnJpb3JGYWNlQXNzZXRQ" +
            "YXRocyA/Pz0gbmV3IExpc3Q8c3RyaW5nPigpOwogICAgICAgICAgICBrbmlnaHRGYWNlQXNzZXRQYXRocyA/Pz0gbmV3IExpc3Q8" +
            "c3RyaW5nPigpOwoKICAgICAgICAgICAgZm9yIChpbnQgaSA9IDA7IGkgPCBDYW5vbmljYWxMZWdlbmRTbG90cy5MZW5ndGg7IGkr" +
            "KykKICAgICAgICAgICAgewogICAgICAgICAgICAgICAgVW5pdFR5cGUgd2FudCA9IENhbm9uaWNhbExlZ2VuZFNsb3RzW2ldOwog" +
            "ICAgICAgICAgICAgICAgTGVnZW5kUG9ydHJhaXRFbnRyeSBmb3VuZCA9IG51bGw7CiAgICAgICAgICAgICAgICBmb3IgKGludCBq" +
            "ID0gMDsgaiA8IGxlZ2VuZFBvcnRyYWl0cy5Db3VudDsgaisrKQogICAgICAgICAgICAgICAgewogICAgICAgICAgICAgICAgICAg" +
            "IGlmIChsZWdlbmRQb3J0cmFpdHNbal0gIT0gbnVsbCAmJiBsZWdlbmRQb3J0cmFpdHNbal0udW5pdFR5cGUgPT0gd2FudCkKICAg" +
            "ICAgICAgICAgICAgICAgICB7CiAgICAgICAgICAgICAgICAgICAgICAgIGZvdW5kID0gbGVnZW5kUG9ydHJhaXRzW2pdOwogICAg" +
            "ICAgICAgICAgICAgICAgICAgICBicmVhazsKICAgICAgICAgICAgICAgICAgICB9CiAgICAgICAgICAgICAgICB9CiAgICAgICAg" +
            "ICAgICAgICBpZiAoZm91bmQgPT0gbnVsbCkKICAgICAgICAgICAgICAgIHsKICAgICAgICAgICAgICAgICAgICBmb3VuZCA9IG5l" +
            "dyBMZWdlbmRQb3J0cmFpdEVudHJ5CiAgICAgICAgICAgICAgICAgICAgewogICAgICAgICAgICAgICAgICAgICAgICB1bml0VHlw" +
            "ZSA9IHdhbnQsCiAgICAgICAgICAgICAgICAgICAgICAgIGRpc3BsYXlOYW1lID0gRGVmYXVsdERpc3BsYXlOYW1lKHdhbnQpLAog" +
            "ICAgICAgICAgICAgICAgICAgICAgICBzb2Z0T3B0aW9uYWwgPSB3YW50ID09IFVuaXRUeXBlLk1lcmxpbgogICAgICAgICAgICAg" +
            "ICAgICAgIH07CiAgICAgICAgICAgICAgICAgICAgbGVnZW5kUG9ydHJhaXRzLkFkZChmb3VuZCk7CiAgICAgICAgICAgICAgICB9" +
            "CiAgICAgICAgICAgICAgICBlbHNlCiAgICAgICAgICAgICAgICB7CiAgICAgICAgICAgICAgICAgICAgaWYgKHN0cmluZy5Jc051" +
            "bGxPckVtcHR5KGZvdW5kLmRpc3BsYXlOYW1lKSkKICAgICAgICAgICAgICAgICAgICAgICAgZm91bmQuZGlzcGxheU5hbWUgPSBE" +
            "ZWZhdWx0RGlzcGxheU5hbWUod2FudCk7CiAgICAgICAgICAgICAgICAgICAgZm91bmQuc29mdE9wdGlvbmFsID0gd2FudCA9PSBV" +
            "bml0VHlwZS5NZXJsaW47CiAgICAgICAgICAgICAgICB9CiAgICAgICAgICAgIH0KICAgICAgICB9CgogICAgICAgIHB1YmxpYyBz" +
            "dGF0aWMgc3RyaW5nIERlZmF1bHREaXNwbGF5TmFtZShVbml0VHlwZSB0KSA9PiB0IHN3aXRjaAogICAgICAgIHsKICAgICAgICAg" +
            "ICAgVW5pdFR5cGUuS2luZ0FydGh1ciA9PiAiS2luZyBBcnRodXIiLAogICAgICAgICAgICBVbml0VHlwZS5TaXJMYW5jZWxvdCA9" +
            "PiAiU2lyIExhbmNlbG90IiwKICAgICAgICAgICAgVW5pdFR5cGUuTW9yZ2FuTGVGYXkgPT4gIk1vcmdhbiBsZSBGYXkiLAogICAg" +
            "ICAgICAgICBVbml0VHlwZS5MYWR5T2ZUaGVMYWtlID0+ICJMYWR5IG9mIHRoZSBMYWtlIiwKICAgICAgICAgICAgVW5pdFR5cGUu" +
            "U2lyR2F3YWluID0+ICJTaXIgR2F3YWluIiwKICAgICAgICAgICAgVW5pdFR5cGUuU2lyR2FsYWhhZCA9PiAiU2lyIEdhbGFoYWQi" +
            "LAogICAgICAgICAgICBVbml0VHlwZS5TaXJUcmlzdGFuID0+ICJTaXIgVHJpc3RhbiIsCiAgICAgICAgICAgIFVuaXRUeXBlLlNp" +
            "clBlcmNpdmFsID0+ICJTaXIgUGVyY2l2YWwiLAogICAgICAgICAgICBVbml0VHlwZS5TaXJLYXkgPT4gIlNpciBLYXkiLAogICAg" +
            "ICAgICAgICBVbml0VHlwZS5TaXJCZWRpdmVyZSA9PiAiU2lyIEJlZGl2ZXJlIiwKICAgICAgICAgICAgVW5pdFR5cGUuTGFkeUd1" +
            "aW5ldmVyZSA9PiAiTGFkeSBHdWluZXZlcmUiLAogICAgICAgICAgICBVbml0VHlwZS5HdWluZXZlcmUgPT4gIkxhZHkgR3VpbmV2" +
            "ZXJlIiwKICAgICAgICAgICAgVW5pdFR5cGUuQXJjaFByaWVzdE9mR2xhc3RvbmJ1cnkgPT4gIkFyY2gtUHJpZXN0IG9mIEdsYXN0" +
            "b25idXJ5IiwKICAgICAgICAgICAgVW5pdFR5cGUuUHJpZXN0ID0+ICJBcmNoLVByaWVzdCBvZiBHbGFzdG9uYnVyeSIsCiAgICAg" +
            "ICAgICAgIFVuaXRUeXBlLlRoZUdyZWVuS25pZ2h0ID0+ICJUaGUgR3JlZW4gS25pZ2h0IiwKICAgICAgICAgICAgVW5pdFR5cGUu" +
            "TWVybGluID0+ICJNZXJsaW4gKHNvZnQpIiwKICAgICAgICAgICAgXyA9PiB0LlRvU3RyaW5nKCkKICAgICAgICB9OwoKICAgICAg" +
            "ICAvLy8gPHN1bW1hcnk+Tm9ybWFsaXplIGFsaWFzZXMgc28gR3VpbmV2ZXJlL1ByaWVzdCBtYXAgb250byBjYW5vbmljYWwgc2xv" +
            "dHMuPC9zdW1tYXJ5PgogICAgICAgIHB1YmxpYyBzdGF0aWMgVW5pdFR5cGUgTm9ybWFsaXplTGVnZW5kU2xvdChVbml0VHlwZSB0" +
            "eXBlKSA9PiB0eXBlIHN3aXRjaAogICAgICAgIHsKICAgICAgICAgICAgVW5pdFR5cGUuR3VpbmV2ZXJlID0+IFVuaXRUeXBlLkxh" +
            "ZHlHdWluZXZlcmUsCiAgICAgICAgICAgIFVuaXRUeXBlLlByaWVzdCA9PiBVbml0VHlwZS5BcmNoUHJpZXN0T2ZHbGFzdG9uYnVy" +
            "eSwKICAgICAgICAgICAgXyA9PiB0eXBlCiAgICAgICAgfTsKCiAgICAgICAgcHVibGljIExlZ2VuZFBvcnRyYWl0RW50cnkgRmlu" +
            "ZExlZ2VuZEVudHJ5KFVuaXRUeXBlIHR5cGUpCiAgICAgICAgewogICAgICAgICAgICBFbnN1cmVTbG90c0FuZExpc3RzKCk7CiAg" +
            "ICAgICAgICAgIFVuaXRUeXBlIHdhbnQgPSBOb3JtYWxpemVMZWdlbmRTbG90KHR5cGUpOwogICAgICAgICAgICBmb3IgKGludCBp" +
            "ID0gMDsgaSA8IGxlZ2VuZFBvcnRyYWl0cy5Db3VudDsgaSsrKQogICAgICAgICAgICB7CiAgICAgICAgICAgICAgICB2YXIgZSA9" +
            "IGxlZ2VuZFBvcnRyYWl0c1tpXTsKICAgICAgICAgICAgICAgIGlmIChlICE9IG51bGwgJiYgZS51bml0VHlwZSA9PSB3YW50KSBy" +
            "ZXR1cm4gZTsKICAgICAgICAgICAgfQogICAgICAgICAgICByZXR1cm4gbnVsbDsKICAgICAgICB9CgogICAgICAgIHB1YmxpYyBT" +
            "cHJpdGUgR2V0TGVnZW5kRmFjZShVbml0VHlwZSB0eXBlKQogICAgICAgIHsKICAgICAgICAgICAgdmFyIGUgPSBGaW5kTGVnZW5k" +
            "RW50cnkodHlwZSk7CiAgICAgICAgICAgIHJldHVybiBlICE9IG51bGwgPyBlLmZhY2VQb3J0cmFpdCA6IG51bGw7CiAgICAgICAg" +
            "fQoKICAgICAgICBwdWJsaWMgTGlzdDxTcHJpdGU+IEdldFBld0ZhY2VQb29sKFVuaXRUeXBlIHR5cGUpCiAgICAgICAgewogICAg" +
            "ICAgICAgICBFbnN1cmVTbG90c0FuZExpc3RzKCk7CiAgICAgICAgICAgIHJldHVybiB0eXBlIHN3aXRjaAogICAgICAgICAgICB7" +
            "CiAgICAgICAgICAgICAgICBVbml0VHlwZS5QZWFzYW50ID0+IHBlYXNhbnRGYWNlUHJvZmlsZXMsCiAgICAgICAgICAgICAgICBV" +
            "bml0VHlwZS5XYXJyaW9yID0+IHdhcnJpb3JGYWNlUHJvZmlsZXMsCiAgICAgICAgICAgICAgICBVbml0VHlwZS5LbmlnaHQgPT4g" +
            "a25pZ2h0RmFjZVByb2ZpbGVzLAogICAgICAgICAgICAgICAgXyA9PiBudWxsCiAgICAgICAgICAgIH07CiAgICAgICAgfQoKICAg" +
            "ICAgICBwdWJsaWMgaW50IENvdW50VXNhYmxlUGV3RmFjZXMoVW5pdFR5cGUgdHlwZSkKICAgICAgICB7CiAgICAgICAgICAgIHZh" +
            "ciBwb29sID0gR2V0UGV3RmFjZVBvb2wodHlwZSk7CiAgICAgICAgICAgIGlmIChwb29sID09IG51bGwpIHJldHVybiAwOwogICAg" +
            "ICAgICAgICBpbnQgbiA9IDA7CiAgICAgICAgICAgIGZvciAoaW50IGkgPSAwOyBpIDwgcG9vbC5Db3VudDsgaSsrKQogICAgICAg" +
            "ICAgICAgICAgaWYgKHBvb2xbaV0gIT0gbnVsbCkgbisrOwogICAgICAgICAgICByZXR1cm4gbjsKICAgICAgICB9CgogICAgICAg" +
            "IHB1YmxpYyBpbnQgQ291bnRBc3NpZ25lZExlZ2VuZEZhY2VzKCkKICAgICAgICB7CiAgICAgICAgICAgIEVuc3VyZVNsb3RzQW5k" +
            "TGlzdHMoKTsKICAgICAgICAgICAgaW50IG4gPSAwOwogICAgICAgICAgICBmb3IgKGludCBpID0gMDsgaSA8IGxlZ2VuZFBvcnRy" +
            "YWl0cy5Db3VudDsgaSsrKQogICAgICAgICAgICB7CiAgICAgICAgICAgICAgICB2YXIgZSA9IGxlZ2VuZFBvcnRyYWl0c1tpXTsK" +
            "ICAgICAgICAgICAgICAgIGlmIChlICE9IG51bGwgJiYgZS5mYWNlUG9ydHJhaXQgIT0gbnVsbCAmJiAhZS5zb2Z0T3B0aW9uYWwp" +
            "IG4rKzsKICAgICAgICAgICAgfQogICAgICAgICAgICByZXR1cm4gbjsKICAgICAgICB9CiAgICB9Cn0K";

        // Assets/Scripts/Data/BattleGoodEvilSpritePoolProfile.cs (14139 bytes)
        const string B64_BattleGoodEvilSpritePoolProfile =
            "dXNpbmcgU3lzdGVtOwp1c2luZyBTeXN0ZW0uQ29sbGVjdGlvbnMuR2VuZXJpYzsKdXNpbmcgVW5pdHlFbmdpbmU7CgpuYW1lc3Bh" +
            "Y2UgQXJ0aHVyaWFuQXV0b2JhdHRsZXIKewogICAgLy8gdGlwOiBmaXgtdGlwZXNjYXBlLXByb2ZpbGUtZW1iZWRzIOKAlCBuYW1l" +
            "ZCBNb25vU2NyaXB0IGZvciBVbml0eSBTTyBiaW5kaW5nCiAgICAvLy8gPHN1bW1hcnk+T25lIEdvb2Qtc2lkZSBMZWdlbmQgLyBF" +
            "dmlsIGJvc3MgYmF0dGxlLXNwcml0ZSBzbG90IChBcmNoaXRlY3QgVGFiIDYpLjwvc3VtbWFyeT4KICAgIFtTZXJpYWxpemFibGVd" +
            "CiAgICBwdWJsaWMgY2xhc3MgQmF0dGxlU3ByaXRlU2xvdEVudHJ5CiAgICB7CiAgICAgICAgcHVibGljIFVuaXRUeXBlIHVuaXRU" +
            "eXBlOwogICAgICAgIHB1YmxpYyBzdHJpbmcgZGlzcGxheU5hbWU7CiAgICAgICAgcHVibGljIFNwcml0ZSBiYXR0bGVTcHJpdGU7" +
            "CiAgICAgICAgcHVibGljIHN0cmluZyBhc3NldFBhdGg7CiAgICAgICAgW1Rvb2x0aXAoIlNvZnQgc2xvdHMgKGUuZy4gTWVybGlu" +
            "KSBzdGF5IGxpc3RlZCBidXQgYXJlIG9wdGlvbmFsLiIpXQogICAgICAgIHB1YmxpYyBib29sIHNvZnRPcHRpb25hbDsKICAgIH0K" +
            "CiAgICAvLy8gPHN1bW1hcnk+CiAgICAvLy8gQXJjaGl0ZWN0IFRhYiA2IOKAlCBBdXRvYmF0dGxlIEdvb2QgdnMgRXZpbCBiYXR0" +
            "bGUgc3ByaXRlcyBmb3IgVW5pdFZpc3VhbEFjdG9yLgogICAgLy8vIERpc3RpbmN0IGZyb20gVGFiIDQgcGV3IEJPRFkgcG9vbHMg" +
            "YW5kIFRhYiA1IEZBQ0UgcG9ydHJhaXRzLiBSZXNvdXJjZXMgcGF0aDoKICAgIC8vLyBCYXR0bGVHb29kRXZpbFNwcml0ZVBvb2xQ" +
            "cm9maWxlLiB0aXA6IGJhdHRsZS1nb29kLWV2aWwtc3ByaXRlLXBvb2xzCiAgICAvLy8gPC9zdW1tYXJ5PgogICAgW0NyZWF0ZUFz" +
            "c2V0TWVudShmaWxlTmFtZSA9ICJCYXR0bGVHb29kRXZpbFNwcml0ZVBvb2xQcm9maWxlIiwgbWVudU5hbWUgPSAiRGVtb25Lbmln" +
            "aHQvQmF0dGxlIEdvb2QgRXZpbCBTcHJpdGUgUG9vbCBQcm9maWxlIildCiAgICBwdWJsaWMgY2xhc3MgQmF0dGxlR29vZEV2aWxT" +
            "cHJpdGVQb29sUHJvZmlsZSA6IFNjcmlwdGFibGVPYmplY3QKICAgIHsKICAgICAgICBwdWJsaWMgY29uc3Qgc3RyaW5nIFJlc291" +
            "cmNlc1BhdGggPSAiQmF0dGxlR29vZEV2aWxTcHJpdGVQb29sUHJvZmlsZSI7CgogICAgICAgIC8vLyA8c3VtbWFyeT4xNCBwbGF5" +
            "YWJsZSBsZWdlbmRzICsgc29mdCBNZXJsaW4gKHNhbWUgb3JkZXIgYXMgVGFiIDUgQ2Fub25pY2FsTGVnZW5kU2xvdHMpLjwvc3Vt" +
            "bWFyeT4KICAgICAgICBwdWJsaWMgc3RhdGljIHJlYWRvbmx5IFVuaXRUeXBlW10gQ2Fub25pY2FsR29vZExlZ2VuZFNsb3RzID0g" +
            "TGVnZW5kUGV3UG9ydHJhaXRQcm9maWxlLkNhbm9uaWNhbExlZ2VuZFNsb3RzOwoKICAgICAgICAvLy8gPHN1bW1hcnk+V2F2ZSAz" +
            "4oCTMTArIGJvc3NlcyAobG9yZS1ib3NzLXdhdmUtb3JkZXIpLjwvc3VtbWFyeT4KICAgICAgICBwdWJsaWMgc3RhdGljIHJlYWRv" +
            "bmx5IFVuaXRUeXBlW10gQ2Fub25pY2FsQm9zc1Nsb3RzID0KICAgICAgICB7CiAgICAgICAgICAgIFVuaXRUeXBlLkJvc3NCbGFj" +
            "a0tuaWdodCwgICAvLyBXYXZlIDMKICAgICAgICAgICAgVW5pdFR5cGUuQm9zc01lbGVhZ2FudCwgICAgIC8vIFdhdmUgNAogICAg" +
            "ICAgICAgICBVbml0VHlwZS5Cb3NzS2luZ0xvdCwgICAgICAgLy8gV2F2ZSA1CiAgICAgICAgICAgIFVuaXRUeXBlLkJvc3NWb3J0" +
            "aWdlcm4sICAgICAvLyBXYXZlIDYKICAgICAgICAgICAgVW5pdFR5cGUuQm9zc0FjY29sb24sICAgICAgIC8vIFdhdmUgNwogICAg" +
            "ICAgICAgICBVbml0VHlwZS5Cb3NzUXVlc3RpbmdCZWFzdCwgLy8gV2F2ZSA4CiAgICAgICAgICAgIFVuaXRUeXBlLkJvc3NHcmVl" +
            "bktuaWdodCwgICAvLyBXYXZlIDkKICAgICAgICAgICAgVW5pdFR5cGUuQm9zc05pbXVlLCAgICAgICAgIC8vIFdhdmUgMTArCiAg" +
            "ICAgICAgfTsKCiAgICAgICAgW0hlYWRlcigiR29vZCDigJQgdHJvb3AgYmF0dGxlIHBvb2xzIChtdWx0aS1zcHJpdGUpIildCiAg" +
            "ICAgICAgcHVibGljIExpc3Q8U3ByaXRlPiBwZWFzYW50QmF0dGxlU3ByaXRlcyA9IG5ldyBMaXN0PFNwcml0ZT4oKTsKICAgICAg" +
            "ICBwdWJsaWMgTGlzdDxTcHJpdGU+IHdhcnJpb3JCYXR0bGVTcHJpdGVzID0gbmV3IExpc3Q8U3ByaXRlPigpOwogICAgICAgIHB1" +
            "YmxpYyBMaXN0PFNwcml0ZT4ga25pZ2h0QmF0dGxlU3ByaXRlcyA9IG5ldyBMaXN0PFNwcml0ZT4oKTsKICAgICAgICBwdWJsaWMg" +
            "TGlzdDxTcHJpdGU+IHByaWVzdEJhdHRsZVNwcml0ZXMgPSBuZXcgTGlzdDxTcHJpdGU+KCk7CgogICAgICAgIHB1YmxpYyBMaXN0" +
            "PHN0cmluZz4gcGVhc2FudEJhdHRsZUFzc2V0UGF0aHMgPSBuZXcgTGlzdDxzdHJpbmc+KCk7CiAgICAgICAgcHVibGljIExpc3Q8" +
            "c3RyaW5nPiB3YXJyaW9yQmF0dGxlQXNzZXRQYXRocyA9IG5ldyBMaXN0PHN0cmluZz4oKTsKICAgICAgICBwdWJsaWMgTGlzdDxz" +
            "dHJpbmc+IGtuaWdodEJhdHRsZUFzc2V0UGF0aHMgPSBuZXcgTGlzdDxzdHJpbmc+KCk7CiAgICAgICAgcHVibGljIExpc3Q8c3Ry" +
            "aW5nPiBwcmllc3RCYXR0bGVBc3NldFBhdGhzID0gbmV3IExpc3Q8c3RyaW5nPigpOwoKICAgICAgICBbSGVhZGVyKCJHb29kIOKA" +
            "lCBwbGF5YWJsZSBMZWdlbmQgYmF0dGxlIHNsb3RzIChzaW5nbGUgc3ByaXRlIGVhY2gpIildCiAgICAgICAgcHVibGljIExpc3Q8" +
            "QmF0dGxlU3ByaXRlU2xvdEVudHJ5PiBnb29kTGVnZW5kU2xvdHMgPSBuZXcgTGlzdDxCYXR0bGVTcHJpdGVTbG90RW50cnk+KCk7" +
            "CgogICAgICAgIFtIZWFkZXIoIkV2aWwg4oCUIHdhdmUtdW5pdCBiYXR0bGUgcG9vbHMiKV0KICAgICAgICBwdWJsaWMgTGlzdDxT" +
            "cHJpdGU+IGRlbW9uSW1wQmF0dGxlU3ByaXRlcyA9IG5ldyBMaXN0PFNwcml0ZT4oKTsKICAgICAgICBwdWJsaWMgTGlzdDxTcHJp" +
            "dGU+IGhlbGxob3VuZEJhdHRsZVNwcml0ZXMgPSBuZXcgTGlzdDxTcHJpdGU+KCk7CiAgICAgICAgcHVibGljIExpc3Q8U3ByaXRl" +
            "PiBkcmVhZExvcmRCYXR0bGVTcHJpdGVzID0gbmV3IExpc3Q8U3ByaXRlPigpOwoKICAgICAgICBwdWJsaWMgTGlzdDxzdHJpbmc+" +
            "IGRlbW9uSW1wQmF0dGxlQXNzZXRQYXRocyA9IG5ldyBMaXN0PHN0cmluZz4oKTsKICAgICAgICBwdWJsaWMgTGlzdDxzdHJpbmc+" +
            "IGhlbGxob3VuZEJhdHRsZUFzc2V0UGF0aHMgPSBuZXcgTGlzdDxzdHJpbmc+KCk7CiAgICAgICAgcHVibGljIExpc3Q8c3RyaW5n" +
            "PiBkcmVhZExvcmRCYXR0bGVBc3NldFBhdGhzID0gbmV3IExpc3Q8c3RyaW5nPigpOwoKICAgICAgICBbSGVhZGVyKCJFdmlsIOKA" +
            "lCBXYXZlIDPigJMxMCsgYm9zcyBiYXR0bGUgc2xvdHMiKV0KICAgICAgICBwdWJsaWMgTGlzdDxCYXR0bGVTcHJpdGVTbG90RW50" +
            "cnk+IGV2aWxCb3NzU2xvdHMgPSBuZXcgTGlzdDxCYXR0bGVTcHJpdGVTbG90RW50cnk+KCk7CgogICAgICAgIFtTeXN0ZW0uTm9u" +
            "U2VyaWFsaXplZF0gcHVibGljIGJvb2wgaXNSdW50aW1lU3RhbmRJbjsKCiAgICAgICAgcHVibGljIHN0YXRpYyBCYXR0bGVHb29k" +
            "RXZpbFNwcml0ZVBvb2xQcm9maWxlIExvYWQoKQogICAgICAgIHsKICAgICAgICAgICAgQmF0dGxlR29vZEV2aWxTcHJpdGVQb29s" +
            "UHJvZmlsZSBwID0gbnVsbDsKICAgICAgICAgICAgdHJ5IHsgcCA9IFJlc291cmNlcy5Mb2FkPEJhdHRsZUdvb2RFdmlsU3ByaXRl" +
            "UG9vbFByb2ZpbGU+KFJlc291cmNlc1BhdGgpOyB9CiAgICAgICAgICAgIGNhdGNoIHsgLyogbWlzc2luZyBNb25vU2NyaXB0IEdV" +
            "SUQvZmlsZUlEIGFmdGVyIGVtYmVkIOKAlCBmYWxsIHRocm91Z2ggKi8gfQogICAgICAgICAgICBpZiAocCAhPSBudWxsKQogICAg" +
            "ICAgICAgICB7CiAgICAgICAgICAgICAgICBwLkVuc3VyZVNsb3RzQW5kTGlzdHMoKTsKICAgICAgICAgICAgICAgIHAuaXNSdW50" +
            "aW1lU3RhbmRJbiA9IGZhbHNlOwogICAgICAgICAgICAgICAgcC5SZW1hdGVyaWFsaXplU3ByaXRlc0Zyb21QYXRocygpOwogICAg" +
            "ICAgICAgICAgICAgcmV0dXJuIHA7CiAgICAgICAgICAgIH0KICAgICAgICAgICAgdHJ5CiAgICAgICAgICAgIHsKICAgICAgICAg" +
            "ICAgICAgIHZhciBhbGwgPSBSZXNvdXJjZXMuTG9hZEFsbDxCYXR0bGVHb29kRXZpbFNwcml0ZVBvb2xQcm9maWxlPigiIik7CiAg" +
            "ICAgICAgICAgICAgICBpZiAoYWxsICE9IG51bGwpCiAgICAgICAgICAgICAgICB7CiAgICAgICAgICAgICAgICAgICAgZm9yIChp" +
            "bnQgaSA9IDA7IGkgPCBhbGwuTGVuZ3RoOyBpKyspCiAgICAgICAgICAgICAgICAgICAgewogICAgICAgICAgICAgICAgICAgICAg" +
            "ICBpZiAoYWxsW2ldID09IG51bGwpIGNvbnRpbnVlOwogICAgICAgICAgICAgICAgICAgICAgICBhbGxbaV0uRW5zdXJlU2xvdHNB" +
            "bmRMaXN0cygpOwogICAgICAgICAgICAgICAgICAgICAgICBhbGxbaV0uaXNSdW50aW1lU3RhbmRJbiA9IGZhbHNlOwogICAgICAg" +
            "ICAgICAgICAgICAgICAgICBhbGxbaV0uUmVtYXRlcmlhbGl6ZVNwcml0ZXNGcm9tUGF0aHMoKTsKICAgICAgICAgICAgICAgICAg" +
            "ICAgICAgcmV0dXJuIGFsbFtpXTsKICAgICAgICAgICAgICAgICAgICB9CiAgICAgICAgICAgICAgICB9CiAgICAgICAgICAgIH0K" +
            "ICAgICAgICAgICAgY2F0Y2ggeyAvKiBicm9rZW4gUmVzb3VyY2VzIGFzc2V0ICovIH0KICAgICAgICAgICAgcCA9IENyZWF0ZUlu" +
            "c3RhbmNlPEJhdHRsZUdvb2RFdmlsU3ByaXRlUG9vbFByb2ZpbGU+KCk7CiAgICAgICAgICAgIHAubmFtZSA9ICJCYXR0bGVHb29k" +
            "RXZpbFNwcml0ZVBvb2xQcm9maWxlX1J1bnRpbWUiOwogICAgICAgICAgICBwLmlzUnVudGltZVN0YW5kSW4gPSB0cnVlOwogICAg" +
            "ICAgICAgICBwLkVuc3VyZVNsb3RzQW5kTGlzdHMoKTsKICAgICAgICAgICAgcmV0dXJuIHA7CiAgICAgICAgfQoKICAgICAgICBw" +
            "dWJsaWMgdm9pZCBFbnN1cmVTbG90c0FuZExpc3RzKCkKICAgICAgICB7CiAgICAgICAgICAgIHBlYXNhbnRCYXR0bGVTcHJpdGVz" +
            "ID8/PSBuZXcgTGlzdDxTcHJpdGU+KCk7CiAgICAgICAgICAgIHdhcnJpb3JCYXR0bGVTcHJpdGVzID8/PSBuZXcgTGlzdDxTcHJp" +
            "dGU+KCk7CiAgICAgICAgICAgIGtuaWdodEJhdHRsZVNwcml0ZXMgPz89IG5ldyBMaXN0PFNwcml0ZT4oKTsKICAgICAgICAgICAg" +
            "cHJpZXN0QmF0dGxlU3ByaXRlcyA/Pz0gbmV3IExpc3Q8U3ByaXRlPigpOwogICAgICAgICAgICBwZWFzYW50QmF0dGxlQXNzZXRQ" +
            "YXRocyA/Pz0gbmV3IExpc3Q8c3RyaW5nPigpOwogICAgICAgICAgICB3YXJyaW9yQmF0dGxlQXNzZXRQYXRocyA/Pz0gbmV3IExp" +
            "c3Q8c3RyaW5nPigpOwogICAgICAgICAgICBrbmlnaHRCYXR0bGVBc3NldFBhdGhzID8/PSBuZXcgTGlzdDxzdHJpbmc+KCk7CiAg" +
            "ICAgICAgICAgIHByaWVzdEJhdHRsZUFzc2V0UGF0aHMgPz89IG5ldyBMaXN0PHN0cmluZz4oKTsKCiAgICAgICAgICAgIGRlbW9u" +
            "SW1wQmF0dGxlU3ByaXRlcyA/Pz0gbmV3IExpc3Q8U3ByaXRlPigpOwogICAgICAgICAgICBoZWxsaG91bmRCYXR0bGVTcHJpdGVz" +
            "ID8/PSBuZXcgTGlzdDxTcHJpdGU+KCk7CiAgICAgICAgICAgIGRyZWFkTG9yZEJhdHRsZVNwcml0ZXMgPz89IG5ldyBMaXN0PFNw" +
            "cml0ZT4oKTsKICAgICAgICAgICAgZGVtb25JbXBCYXR0bGVBc3NldFBhdGhzID8/PSBuZXcgTGlzdDxzdHJpbmc+KCk7CiAgICAg" +
            "ICAgICAgIGhlbGxob3VuZEJhdHRsZUFzc2V0UGF0aHMgPz89IG5ldyBMaXN0PHN0cmluZz4oKTsKICAgICAgICAgICAgZHJlYWRM" +
            "b3JkQmF0dGxlQXNzZXRQYXRocyA/Pz0gbmV3IExpc3Q8c3RyaW5nPigpOwoKICAgICAgICAgICAgZ29vZExlZ2VuZFNsb3RzID8/" +
            "PSBuZXcgTGlzdDxCYXR0bGVTcHJpdGVTbG90RW50cnk+KCk7CiAgICAgICAgICAgIGV2aWxCb3NzU2xvdHMgPz89IG5ldyBMaXN0" +
            "PEJhdHRsZVNwcml0ZVNsb3RFbnRyeT4oKTsKCiAgICAgICAgICAgIEVuc3VyZVNsb3RMaXN0KGdvb2RMZWdlbmRTbG90cywgQ2Fu" +
            "b25pY2FsR29vZExlZ2VuZFNsb3RzLCBzb2Z0TWVybGluOiB0cnVlKTsKICAgICAgICAgICAgRW5zdXJlU2xvdExpc3QoZXZpbEJv" +
            "c3NTbG90cywgQ2Fub25pY2FsQm9zc1Nsb3RzLCBzb2Z0TWVybGluOiBmYWxzZSk7CiAgICAgICAgfQoKICAgICAgICBzdGF0aWMg" +
            "dm9pZCBFbnN1cmVTbG90TGlzdChMaXN0PEJhdHRsZVNwcml0ZVNsb3RFbnRyeT4gbGlzdCwgVW5pdFR5cGVbXSB3YW50VHlwZXMs" +
            "IGJvb2wgc29mdE1lcmxpbikKICAgICAgICB7CiAgICAgICAgICAgIGZvciAoaW50IGkgPSAwOyBpIDwgd2FudFR5cGVzLkxlbmd0" +
            "aDsgaSsrKQogICAgICAgICAgICB7CiAgICAgICAgICAgICAgICBVbml0VHlwZSB3YW50ID0gd2FudFR5cGVzW2ldOwogICAgICAg" +
            "ICAgICAgICAgQmF0dGxlU3ByaXRlU2xvdEVudHJ5IGZvdW5kID0gbnVsbDsKICAgICAgICAgICAgICAgIGZvciAoaW50IGogPSAw" +
            "OyBqIDwgbGlzdC5Db3VudDsgaisrKQogICAgICAgICAgICAgICAgewogICAgICAgICAgICAgICAgICAgIGlmIChsaXN0W2pdICE9" +
            "IG51bGwgJiYgbGlzdFtqXS51bml0VHlwZSA9PSB3YW50KQogICAgICAgICAgICAgICAgICAgIHsKICAgICAgICAgICAgICAgICAg" +
            "ICAgICAgZm91bmQgPSBsaXN0W2pdOwogICAgICAgICAgICAgICAgICAgICAgICBicmVhazsKICAgICAgICAgICAgICAgICAgICB9" +
            "CiAgICAgICAgICAgICAgICB9CiAgICAgICAgICAgICAgICBpZiAoZm91bmQgPT0gbnVsbCkKICAgICAgICAgICAgICAgIHsKICAg" +
            "ICAgICAgICAgICAgICAgICBmb3VuZCA9IG5ldyBCYXR0bGVTcHJpdGVTbG90RW50cnkKICAgICAgICAgICAgICAgICAgICB7CiAg" +
            "ICAgICAgICAgICAgICAgICAgICAgIHVuaXRUeXBlID0gd2FudCwKICAgICAgICAgICAgICAgICAgICAgICAgZGlzcGxheU5hbWUg" +
            "PSBEZWZhdWx0QmF0dGxlRGlzcGxheU5hbWUod2FudCksCiAgICAgICAgICAgICAgICAgICAgICAgIHNvZnRPcHRpb25hbCA9IHNv" +
            "ZnRNZXJsaW4gJiYgd2FudCA9PSBVbml0VHlwZS5NZXJsaW4KICAgICAgICAgICAgICAgICAgICB9OwogICAgICAgICAgICAgICAg" +
            "ICAgIGxpc3QuQWRkKGZvdW5kKTsKICAgICAgICAgICAgICAgIH0KICAgICAgICAgICAgICAgIGVsc2UKICAgICAgICAgICAgICAg" +
            "IHsKICAgICAgICAgICAgICAgICAgICBpZiAoc3RyaW5nLklzTnVsbE9yRW1wdHkoZm91bmQuZGlzcGxheU5hbWUpKQogICAgICAg" +
            "ICAgICAgICAgICAgICAgICBmb3VuZC5kaXNwbGF5TmFtZSA9IERlZmF1bHRCYXR0bGVEaXNwbGF5TmFtZSh3YW50KTsKICAgICAg" +
            "ICAgICAgICAgICAgICBpZiAoc29mdE1lcmxpbikKICAgICAgICAgICAgICAgICAgICAgICAgZm91bmQuc29mdE9wdGlvbmFsID0g" +
            "d2FudCA9PSBVbml0VHlwZS5NZXJsaW47CiAgICAgICAgICAgICAgICB9CiAgICAgICAgICAgIH0KICAgICAgICB9CgogICAgICAg" +
            "IHB1YmxpYyBzdGF0aWMgc3RyaW5nIERlZmF1bHRCYXR0bGVEaXNwbGF5TmFtZShVbml0VHlwZSB0KSA9PiB0IHN3aXRjaAogICAg" +
            "ICAgIHsKICAgICAgICAgICAgVW5pdFR5cGUuS2luZ0FydGh1ciA9PiAiS2luZyBBcnRodXIiLAogICAgICAgICAgICBVbml0VHlw" +
            "ZS5TaXJMYW5jZWxvdCA9PiAiU2lyIExhbmNlbG90IiwKICAgICAgICAgICAgVW5pdFR5cGUuTW9yZ2FuTGVGYXkgPT4gIk1vcmdh" +
            "biBsZSBGYXkiLAogICAgICAgICAgICBVbml0VHlwZS5MYWR5T2ZUaGVMYWtlID0+ICJMYWR5IG9mIHRoZSBMYWtlIiwKICAgICAg" +
            "ICAgICAgVW5pdFR5cGUuU2lyR2F3YWluID0+ICJTaXIgR2F3YWluIiwKICAgICAgICAgICAgVW5pdFR5cGUuU2lyR2FsYWhhZCA9" +
            "PiAiU2lyIEdhbGFoYWQiLAogICAgICAgICAgICBVbml0VHlwZS5TaXJUcmlzdGFuID0+ICJTaXIgVHJpc3RhbiIsCiAgICAgICAg" +
            "ICAgIFVuaXRUeXBlLlNpclBlcmNpdmFsID0+ICJTaXIgUGVyY2l2YWwiLAogICAgICAgICAgICBVbml0VHlwZS5TaXJLYXkgPT4g" +
            "IlNpciBLYXkiLAogICAgICAgICAgICBVbml0VHlwZS5TaXJCZWRpdmVyZSA9PiAiU2lyIEJlZGl2ZXJlIiwKICAgICAgICAgICAg" +
            "VW5pdFR5cGUuTGFkeUd1aW5ldmVyZSA9PiAiTGFkeSBHdWluZXZlcmUiLAogICAgICAgICAgICBVbml0VHlwZS5HdWluZXZlcmUg" +
            "PT4gIkxhZHkgR3VpbmV2ZXJlIiwKICAgICAgICAgICAgVW5pdFR5cGUuQXJjaFByaWVzdE9mR2xhc3RvbmJ1cnkgPT4gIkFyY2gt" +
            "UHJpZXN0IG9mIEdsYXN0b25idXJ5IiwKICAgICAgICAgICAgVW5pdFR5cGUuUHJpZXN0ID0+ICJQcmllc3QiLAogICAgICAgICAg" +
            "ICBVbml0VHlwZS5UaGVHcmVlbktuaWdodCA9PiAiVGhlIEdyZWVuIEtuaWdodCIsCiAgICAgICAgICAgIFVuaXRUeXBlLk1lcmxp" +
            "biA9PiAiTWVybGluIChzb2Z0KSIsCiAgICAgICAgICAgIFVuaXRUeXBlLkRlbW9uSW1wID0+ICJEZW1vbiBJbXAiLAogICAgICAg" +
            "ICAgICBVbml0VHlwZS5EZW1vbkhlbGxob3VuZCA9PiAiSGVsbGhvdW5kIiwKICAgICAgICAgICAgVW5pdFR5cGUuRGVtb25EcmVh" +
            "ZExvcmQgPT4gIkRyZWFkIExvcmQiLAogICAgICAgICAgICBVbml0VHlwZS5Cb3NzQmxhY2tLbmlnaHQgPT4gIkJsYWNrIEtuaWdo" +
            "dCAoVzMpIiwKICAgICAgICAgICAgVW5pdFR5cGUuQm9zc01lbGVhZ2FudCA9PiAiTWVsZWFnYW50IChXNCkiLAogICAgICAgICAg" +
            "ICBVbml0VHlwZS5Cb3NzS2luZ0xvdCA9PiAiS2luZyBMb3QgKFc1KSIsCiAgICAgICAgICAgIFVuaXRUeXBlLkJvc3NWb3J0aWdl" +
            "cm4gPT4gIlZvcnRpZ2VybiAoVzYpIiwKICAgICAgICAgICAgVW5pdFR5cGUuQm9zc0FjY29sb24gPT4gIkFjY29sb24gKFc3KSIs" +
            "CiAgICAgICAgICAgIFVuaXRUeXBlLkJvc3NRdWVzdGluZ0JlYXN0ID0+ICJRdWVzdGluZyBCZWFzdCAoVzgpIiwKICAgICAgICAg" +
            "ICAgVW5pdFR5cGUuQm9zc0dyZWVuS25pZ2h0ID0+ICJHcmVlbiBLbmlnaHQgQm9zcyAoVzkpIiwKICAgICAgICAgICAgVW5pdFR5" +
            "cGUuQm9zc05pbXVlID0+ICJOaW11ZSAoVzEwKykiLAogICAgICAgICAgICBfID0+IHQuVG9TdHJpbmcoKQogICAgICAgIH07Cgog" +
            "ICAgICAgIHB1YmxpYyBpbnQgUmVtYXRlcmlhbGl6ZVNwcml0ZXNGcm9tUGF0aHMoKQogICAgICAgIHsKICAgICAgICAgICAgRW5z" +
            "dXJlU2xvdHNBbmRMaXN0cygpOwogICAgICAgICAgICBpbnQgbiA9IDA7CiAgICAgICAgICAgIG4gKz0gQ2xhc3NTcHJpdGVQb29s" +
            "UHJvZmlsZS5SZW1hdGVyaWFsaXplTGlzdChwZWFzYW50QmF0dGxlU3ByaXRlcywgcGVhc2FudEJhdHRsZUFzc2V0UGF0aHMpOwog" +
            "ICAgICAgICAgICBuICs9IENsYXNzU3ByaXRlUG9vbFByb2ZpbGUuUmVtYXRlcmlhbGl6ZUxpc3Qod2FycmlvckJhdHRsZVNwcml0" +
            "ZXMsIHdhcnJpb3JCYXR0bGVBc3NldFBhdGhzKTsKICAgICAgICAgICAgbiArPSBDbGFzc1Nwcml0ZVBvb2xQcm9maWxlLlJlbWF0" +
            "ZXJpYWxpemVMaXN0KGtuaWdodEJhdHRsZVNwcml0ZXMsIGtuaWdodEJhdHRsZUFzc2V0UGF0aHMpOwogICAgICAgICAgICBuICs9" +
            "IENsYXNzU3ByaXRlUG9vbFByb2ZpbGUuUmVtYXRlcmlhbGl6ZUxpc3QocHJpZXN0QmF0dGxlU3ByaXRlcywgcHJpZXN0QmF0dGxl" +
            "QXNzZXRQYXRocyk7CiAgICAgICAgICAgIG4gKz0gQ2xhc3NTcHJpdGVQb29sUHJvZmlsZS5SZW1hdGVyaWFsaXplTGlzdChkZW1v" +
            "bkltcEJhdHRsZVNwcml0ZXMsIGRlbW9uSW1wQmF0dGxlQXNzZXRQYXRocyk7CiAgICAgICAgICAgIG4gKz0gQ2xhc3NTcHJpdGVQ" +
            "b29sUHJvZmlsZS5SZW1hdGVyaWFsaXplTGlzdChoZWxsaG91bmRCYXR0bGVTcHJpdGVzLCBoZWxsaG91bmRCYXR0bGVBc3NldFBh" +
            "dGhzKTsKICAgICAgICAgICAgbiArPSBDbGFzc1Nwcml0ZVBvb2xQcm9maWxlLlJlbWF0ZXJpYWxpemVMaXN0KGRyZWFkTG9yZEJh" +
            "dHRsZVNwcml0ZXMsIGRyZWFkTG9yZEJhdHRsZUFzc2V0UGF0aHMpOwogICAgICAgICAgICBuICs9IFJlbWF0ZXJpYWxpemVTbG90" +
            "cyhnb29kTGVnZW5kU2xvdHMpOwogICAgICAgICAgICBuICs9IFJlbWF0ZXJpYWxpemVTbG90cyhldmlsQm9zc1Nsb3RzKTsKICAg" +
            "ICAgICAgICAgcmV0dXJuIG47CiAgICAgICAgfQoKICAgICAgICBzdGF0aWMgaW50IFJlbWF0ZXJpYWxpemVTbG90cyhMaXN0PEJh" +
            "dHRsZVNwcml0ZVNsb3RFbnRyeT4gc2xvdHMpCiAgICAgICAgewogICAgICAgICAgICBpZiAoc2xvdHMgPT0gbnVsbCkgcmV0dXJu" +
            "IDA7CiAgICAgICAgICAgIGludCBuID0gMDsKICAgICAgICAgICAgZm9yIChpbnQgaSA9IDA7IGkgPCBzbG90cy5Db3VudDsgaSsr" +
            "KQogICAgICAgICAgICB7CiAgICAgICAgICAgICAgICB2YXIgZSA9IHNsb3RzW2ldOwogICAgICAgICAgICAgICAgaWYgKGUgPT0g" +
            "bnVsbCB8fCBlLmJhdHRsZVNwcml0ZSAhPSBudWxsKSBjb250aW51ZTsKICAgICAgICAgICAgICAgIGlmIChzdHJpbmcuSXNOdWxs" +
            "T3JFbXB0eShlLmFzc2V0UGF0aCkpIGNvbnRpbnVlOwogICAgICAgICAgICAgICAgU3ByaXRlIHNwID0gQ2xhc3NTcHJpdGVQb29s" +
            "UHJvZmlsZS5UcnlMb2FkU3ByaXRlQXRQYXRoKGUuYXNzZXRQYXRoKTsKICAgICAgICAgICAgICAgIGlmIChzcCA9PSBudWxsKSBj" +
            "b250aW51ZTsKICAgICAgICAgICAgICAgIGUuYmF0dGxlU3ByaXRlID0gc3A7CiAgICAgICAgICAgICAgICBuKys7CiAgICAgICAg" +
            "ICAgIH0KICAgICAgICAgICAgcmV0dXJuIG47CiAgICAgICAgfQoKICAgICAgICBwdWJsaWMgTGlzdDxTcHJpdGU+IEdldFRyb29w" +
            "T3JXYXZlUG9vbChVbml0VHlwZSB0eXBlKQogICAgICAgIHsKICAgICAgICAgICAgRW5zdXJlU2xvdHNBbmRMaXN0cygpOwogICAg" +
            "ICAgICAgICByZXR1cm4gdHlwZSBzd2l0Y2gKICAgICAgICAgICAgewogICAgICAgICAgICAgICAgVW5pdFR5cGUuUGVhc2FudCA9" +
            "PiBwZWFzYW50QmF0dGxlU3ByaXRlcywKICAgICAgICAgICAgICAgIFVuaXRUeXBlLldhcnJpb3IgPT4gd2FycmlvckJhdHRsZVNw" +
            "cml0ZXMsCiAgICAgICAgICAgICAgICBVbml0VHlwZS5LbmlnaHQgPT4ga25pZ2h0QmF0dGxlU3ByaXRlcywKICAgICAgICAgICAg" +
            "ICAgIFVuaXRUeXBlLlByaWVzdCA9PiBwcmllc3RCYXR0bGVTcHJpdGVzLAogICAgICAgICAgICAgICAgVW5pdFR5cGUuRGVtb25J" +
            "bXAgPT4gZGVtb25JbXBCYXR0bGVTcHJpdGVzLAogICAgICAgICAgICAgICAgVW5pdFR5cGUuRGVtb25IZWxsaG91bmQgPT4gaGVs" +
            "bGhvdW5kQmF0dGxlU3ByaXRlcywKICAgICAgICAgICAgICAgIFVuaXRUeXBlLkRlbW9uRHJlYWRMb3JkID0+IGRyZWFkTG9yZEJh" +
            "dHRsZVNwcml0ZXMsCiAgICAgICAgICAgICAgICBfID0+IG51bGwKICAgICAgICAgICAgfTsKICAgICAgICB9CgogICAgICAgIHB1" +
            "YmxpYyBMaXN0PHN0cmluZz4gR2V0VHJvb3BPcldhdmVQYXRocyhVbml0VHlwZSB0eXBlKQogICAgICAgIHsKICAgICAgICAgICAg" +
            "RW5zdXJlU2xvdHNBbmRMaXN0cygpOwogICAgICAgICAgICByZXR1cm4gdHlwZSBzd2l0Y2gKICAgICAgICAgICAgewogICAgICAg" +
            "ICAgICAgICAgVW5pdFR5cGUuUGVhc2FudCA9PiBwZWFzYW50QmF0dGxlQXNzZXRQYXRocywKICAgICAgICAgICAgICAgIFVuaXRU" +
            "eXBlLldhcnJpb3IgPT4gd2FycmlvckJhdHRsZUFzc2V0UGF0aHMsCiAgICAgICAgICAgICAgICBVbml0VHlwZS5LbmlnaHQgPT4g" +
            "a25pZ2h0QmF0dGxlQXNzZXRQYXRocywKICAgICAgICAgICAgICAgIFVuaXRUeXBlLlByaWVzdCA9PiBwcmllc3RCYXR0bGVBc3Nl" +
            "dFBhdGhzLAogICAgICAgICAgICAgICAgVW5pdFR5cGUuRGVtb25JbXAgPT4gZGVtb25JbXBCYXR0bGVBc3NldFBhdGhzLAogICAg" +
            "ICAgICAgICAgICAgVW5pdFR5cGUuRGVtb25IZWxsaG91bmQgPT4gaGVsbGhvdW5kQmF0dGxlQXNzZXRQYXRocywKICAgICAgICAg" +
            "ICAgICAgIFVuaXRUeXBlLkRlbW9uRHJlYWRMb3JkID0+IGRyZWFkTG9yZEJhdHRsZUFzc2V0UGF0aHMsCiAgICAgICAgICAgICAg" +
            "ICBfID0+IG51bGwKICAgICAgICAgICAgfTsKICAgICAgICB9CgogICAgICAgIHB1YmxpYyBCYXR0bGVTcHJpdGVTbG90RW50cnkg" +
            "RmluZExlZ2VuZFNsb3QoVW5pdFR5cGUgdHlwZSkKICAgICAgICB7CiAgICAgICAgICAgIEVuc3VyZVNsb3RzQW5kTGlzdHMoKTsK" +
            "ICAgICAgICAgICAgLy8gTGFkeUd1aW5ldmVyZSAvIEd1aW5ldmVyZSBhbGlhcwogICAgICAgICAgICBmb3IgKGludCBpID0gMDsg" +
            "aSA8IGdvb2RMZWdlbmRTbG90cy5Db3VudDsgaSsrKQogICAgICAgICAgICB7CiAgICAgICAgICAgICAgICB2YXIgZSA9IGdvb2RM" +
            "ZWdlbmRTbG90c1tpXTsKICAgICAgICAgICAgICAgIGlmIChlID09IG51bGwpIGNvbnRpbnVlOwogICAgICAgICAgICAgICAgaWYg" +
            "KGUudW5pdFR5cGUgPT0gdHlwZSkgcmV0dXJuIGU7CiAgICAgICAgICAgICAgICBpZiAoKHR5cGUgPT0gVW5pdFR5cGUuR3VpbmV2" +
            "ZXJlIHx8IHR5cGUgPT0gVW5pdFR5cGUuTGFkeUd1aW5ldmVyZSkKICAgICAgICAgICAgICAgICAgICAmJiAoZS51bml0VHlwZSA9" +
            "PSBVbml0VHlwZS5HdWluZXZlcmUgfHwgZS51bml0VHlwZSA9PSBVbml0VHlwZS5MYWR5R3VpbmV2ZXJlKSkKICAgICAgICAgICAg" +
            "ICAgICAgICByZXR1cm4gZTsKICAgICAgICAgICAgfQogICAgICAgICAgICByZXR1cm4gbnVsbDsKICAgICAgICB9CgogICAgICAg" +
            "IHB1YmxpYyBCYXR0bGVTcHJpdGVTbG90RW50cnkgRmluZEJvc3NTbG90KFVuaXRUeXBlIHR5cGUpCiAgICAgICAgewogICAgICAg" +
            "ICAgICBFbnN1cmVTbG90c0FuZExpc3RzKCk7CiAgICAgICAgICAgIGZvciAoaW50IGkgPSAwOyBpIDwgZXZpbEJvc3NTbG90cy5D" +
            "b3VudDsgaSsrKQogICAgICAgICAgICB7CiAgICAgICAgICAgICAgICB2YXIgZSA9IGV2aWxCb3NzU2xvdHNbaV07CiAgICAgICAg" +
            "ICAgICAgICBpZiAoZSAhPSBudWxsICYmIGUudW5pdFR5cGUgPT0gdHlwZSkgcmV0dXJuIGU7CiAgICAgICAgICAgIH0KICAgICAg" +
            "ICAgICAgcmV0dXJuIG51bGw7CiAgICAgICAgfQoKICAgICAgICBwdWJsaWMgU3ByaXRlIEdldExlZ2VuZEJhdHRsZVNwcml0ZShV" +
            "bml0VHlwZSB0eXBlKQogICAgICAgIHsKICAgICAgICAgICAgdmFyIGUgPSBGaW5kTGVnZW5kU2xvdCh0eXBlKTsKICAgICAgICAg" +
            "ICAgcmV0dXJuIGUgIT0gbnVsbCA/IGUuYmF0dGxlU3ByaXRlIDogbnVsbDsKICAgICAgICB9CgogICAgICAgIHB1YmxpYyBTcHJp" +
            "dGUgR2V0Qm9zc0JhdHRsZVNwcml0ZShVbml0VHlwZSB0eXBlKQogICAgICAgIHsKICAgICAgICAgICAgdmFyIGUgPSBGaW5kQm9z" +
            "c1Nsb3QodHlwZSk7CiAgICAgICAgICAgIHJldHVybiBlICE9IG51bGwgPyBlLmJhdHRsZVNwcml0ZSA6IG51bGw7CiAgICAgICAg" +
            "fQoKICAgICAgICBwdWJsaWMgaW50IENvdW50VXNhYmxlUG9vbChVbml0VHlwZSB0eXBlKQogICAgICAgIHsKICAgICAgICAgICAg" +
            "dmFyIHBvb2wgPSBHZXRUcm9vcE9yV2F2ZVBvb2wodHlwZSk7CiAgICAgICAgICAgIGlmIChwb29sID09IG51bGwpIHJldHVybiAw" +
            "OwogICAgICAgICAgICBpbnQgbiA9IDA7CiAgICAgICAgICAgIGZvciAoaW50IGkgPSAwOyBpIDwgcG9vbC5Db3VudDsgaSsrKQog" +
            "ICAgICAgICAgICAgICAgaWYgKHBvb2xbaV0gIT0gbnVsbCkgbisrOwogICAgICAgICAgICByZXR1cm4gbjsKICAgICAgICB9Cgog" +
            "ICAgICAgIHB1YmxpYyBpbnQgQ291bnRBc3NpZ25lZExlZ2VuZEJhdHRsZXMoKQogICAgICAgIHsKICAgICAgICAgICAgRW5zdXJl" +
            "U2xvdHNBbmRMaXN0cygpOwogICAgICAgICAgICBpbnQgbiA9IDA7CiAgICAgICAgICAgIGZvciAoaW50IGkgPSAwOyBpIDwgZ29v" +
            "ZExlZ2VuZFNsb3RzLkNvdW50OyBpKyspCiAgICAgICAgICAgIHsKICAgICAgICAgICAgICAgIHZhciBlID0gZ29vZExlZ2VuZFNs" +
            "b3RzW2ldOwogICAgICAgICAgICAgICAgaWYgKGUgPT0gbnVsbCB8fCBlLnNvZnRPcHRpb25hbCkgY29udGludWU7CiAgICAgICAg" +
            "ICAgICAgICBpZiAoZS5iYXR0bGVTcHJpdGUgIT0gbnVsbCkgbisrOwogICAgICAgICAgICB9CiAgICAgICAgICAgIHJldHVybiBu" +
            "OwogICAgICAgIH0KCiAgICAgICAgcHVibGljIGludCBDb3VudEFzc2lnbmVkQm9zc0JhdHRsZXMoKQogICAgICAgIHsKICAgICAg" +
            "ICAgICAgRW5zdXJlU2xvdHNBbmRMaXN0cygpOwogICAgICAgICAgICBpbnQgbiA9IDA7CiAgICAgICAgICAgIGZvciAoaW50IGkg" +
            "PSAwOyBpIDwgZXZpbEJvc3NTbG90cy5Db3VudDsgaSsrKQogICAgICAgICAgICB7CiAgICAgICAgICAgICAgICB2YXIgZSA9IGV2" +
            "aWxCb3NzU2xvdHNbaV07CiAgICAgICAgICAgICAgICBpZiAoZSAhPSBudWxsICYmIGUuYmF0dGxlU3ByaXRlICE9IG51bGwpIG4r" +
            "KzsKICAgICAgICAgICAgfQogICAgICAgICAgICByZXR1cm4gbjsKICAgICAgICB9CiAgICB9Cn0K";

        public static readonly (string Rel, string B64)[] All =
        {
            ("Assets/Scripts/Data/StageLayoutProfile.cs", B64_StageLayoutProfile),
            ("Assets/Scripts/Data/SceneBackgroundProfile.cs", B64_SceneBackgroundProfile),
            ("Assets/Scripts/Data/ClassSpritePoolProfile.cs", B64_ClassSpritePoolProfile),
            ("Assets/Scripts/Data/LegendPewPortraitProfile.cs", B64_LegendPewPortraitProfile),
            ("Assets/Scripts/Data/BattleGoodEvilSpritePoolProfile.cs", B64_BattleGoodEvilSpritePoolProfile)
        };

        public static string Decode(string b64)
        {
            if (string.IsNullOrEmpty(b64)) return null;
            return Encoding.UTF8.GetString(Convert.FromBase64String(b64));
        }

        public static string SourceFor(string rel)
        {
            if (string.IsNullOrEmpty(rel)) return null;
            string leaf = Path.GetFileName(rel.Replace('\\', '/'));
            foreach (var (r, b) in All)
            {
                if (string.Equals(r, rel, StringComparison.OrdinalIgnoreCase)) return Decode(b);
                if (string.Equals(Path.GetFileName(r), leaf, StringComparison.OrdinalIgnoreCase)) return Decode(b);
            }
            return null;
        }
    }

}
#endif
