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
    public const string TipPackageId = "fix-evil-facing-profile-build";
    const string Repo = "airizarry9938/DemonKnight";
    const string RawBase = "https://raw.githubusercontent.com/" + Repo + "/";
    const string PrefsKey = "DemonKnight.TipEscape.LastOk." + TipPackageId;
    const string OnLoadSessionGate = "DemonKnight.TipEscape.OnLoadDone." + TipPackageId;

    static readonly string[] HostRels =
    {
        "Assets/Editor/DemonKnightPackageVersion.cs",
        // PewLayout before Architect — SeatShowsSprite must exist if Architect still calls it.
        "Assets/Scripts/UI/SomersetPewLayout.cs",
        // Named MonoScript Profiles + shared types BEFORE GameDataTypes Appliers.
        // tip: fix-architect-pool-load-list — Force Sync wipe/never-pull → CS0246.
        // Applier embed host — Appliers stay in GameDataTypes.
        "Assets/Scripts/Data/GameDataTypes.cs",
        "Assets/Scripts/UI/SceneBackgroundApplier.cs",
        "Assets/Scripts/UI/ClassSpritePoolApplier.cs",
        "Assets/Scripts/UI/SomersetChurchUI.cs",
        "Assets/Scripts/UI/LegendCodexUI.cs",
        "Assets/Scripts/UI/TownSplitUIController.cs",
        // tip: fix-architect-48-seat-preview — CS0104 Random + name pools; Escape must land with hosts.
        "Assets/Scripts/Systems/TownRNGGenerator.cs",
        "Assets/Editor/ArthurianSceneBuilder.cs",
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

    [MenuItem("Arthurian/!!! Escape to Tip (fix-evil-facing-profile-build)", false, 0)]
    public static void EscapeMenu()
    {
        // Manual Escape clears session gates so a stuck Downloads project can recover.
        SessionState.EraseBool(OnLoadSessionGate);
        SessionState.EraseBool("DemonKnight.PackageVersion.OnLoadDone." + TipPackageId);
        SessionState.EraseBool("DemonKnight.Ensure.OnLoadDone." + TipPackageId);
        SessionState.EraseBool("DemonKnight.Sync.OnLoadNuke." + TipPackageId);
        SessionState.EraseBool("DemonKnight.MenuBootstrap.NukeProfiles." + TipPackageId);
        SessionState.EraseBool("DemonKnight.MenuBootstrap.AutoVerify." + TipPackageId);
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
            if (EditorApplication.isCompiling || EditorApplication.isUpdating) return;
            // tip: fix-architect-pool-load-list — one-shot; SET GATE BEFORE EscapeNow (Refresh mid-Escape = loop).
            if (SessionState.GetBool(OnLoadSessionGate, false)) return;
            SessionState.SetBool(OnLoadSessionGate, true);
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
            "fix-townrng-name-pools",
            "fix-48-pew-seats",
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
            "fix-tipescape-profile-embeds",
            "fix-gdt-profile-always-compile",
            "fix-profiles-in-gdt-always",
            "fix-endless-compile-loop",
            "fix-hud-bar-button-layout",
            "fix-architect-apply-persist-facing",
            "fix-architect-load-pool-profiles",
            "fix-architect-save-load-pool-ux",
            "fix-architect-pool-load-list",
            "fix-architect-48-seat-preview",
            "fix-stage-placer-profiles",
            "fix-battle-ground-layout",
        };
        foreach (string s in stale)
        {
            // Only Id / UiBuildId assignments — never bare "stale-id" (lists false-positive).
            if (pvBody.IndexOf("Id = \"" + s + "\"", StringComparison.Ordinal) >= 0) return true;
            if (archBody.IndexOf("UiBuildId = \"" + s + "\"", StringComparison.Ordinal) >= 0) return true;
        }
        // ClassPoolKind→UnitType CS0019; keep pew-body Persist/fileID belt.
        if (archBody.IndexOf("selectedClassPool == UnitType.", StringComparison.Ordinal) >= 0) return true;
        if (archBody.IndexOf("ClassPoolKind.Warrior => UnitType.Warrior", StringComparison.Ordinal) < 0) return true;
        // tip: fix-architect-pool-load-list — SoftPromote videos + ApplyOnSceneEnter + facing.
        if (archBody.IndexOf("PromoteVideoClip", StringComparison.Ordinal) < 0) return true;
        // tip: fix-architect-pool-load-list — explicit Save/Load Pool Profile UX on Tabs 4/5/6.
        if (archBody.IndexOf("Save Pool Profile", StringComparison.Ordinal) < 0) return true;
        if (archBody.IndexOf("Load Pool Profile", StringComparison.Ordinal) < 0) return true;
        if (archBody.IndexOf("ArchitectPools", StringComparison.Ordinal) < 0) return true;
        if (archBody.IndexOf("SaveNamedClassPoolProfile", StringComparison.Ordinal) < 0) return true;
        // tip: fix-architect-pool-load-list — Load popup always visible even when empty.
        if (archBody.IndexOf("NamedPoolNoneSavedLabel", StringComparison.Ordinal) < 0) return true;
        if (archBody.IndexOf("EnsureNamedPoolAssetAtPath", StringComparison.Ordinal) < 0) return true;
        // tip: fix-architect-pool-load-list — Save Finalize + dual-scan rebuild list.
        if (archBody.IndexOf("FinalizeNamedPoolSave", StringComparison.Ordinal) < 0) return true;
        if (archBody.IndexOf("RebuildNamedPoolNameList", StringComparison.Ordinal) < 0) return true;
        if (archBody.IndexOf("CreateNamedPoolInstance", StringComparison.Ordinal) < 0) return true;
        // tip: fix-architect-48-seat-preview — Layout prefs + 48 hitbox preview.
        if (archBody.IndexOf("DrawChurchLayoutPrefsPanel", StringComparison.Ordinal) < 0) return true;
        if (archBody.IndexOf("churchHitboxSize", StringComparison.Ordinal) < 0) return true;
        // tip: fix-evil-facing-profile-build — Stage Placer named Save/Load.
        if (archBody.IndexOf("SaveNamedStageLayoutProfile", StringComparison.Ordinal) < 0) return true;
        if (archBody.IndexOf("ArchitectPoolsStageFolder", StringComparison.Ordinal) < 0) return true;
        // tip: fix-evil-facing-profile-build — mini battle + aisle-outward.
        if (archBody.IndexOf("DrawBattleMiniLayout", StringComparison.Ordinal) < 0) return true;
        if (archBody.IndexOf("ApplyAisleOutwardBattleDefaults", StringComparison.Ordinal) < 0
            && File.ReadAllText(Path.Combine(root, "Assets", "Scripts", "Data", "GameDataTypes.cs"))
                .IndexOf("ApplyAisleOutwardBattleDefaults", StringComparison.Ordinal) < 0) return true;
        string gdtPath = Path.Combine(root, "Assets", "Scripts", "Data", "GameDataTypes.cs");
        if (!File.Exists(gdtPath)) return true;
        string gdtBody = File.ReadAllText(gdtPath);
        if (gdtBody.IndexOf("ApplyOnSceneEnter", StringComparison.Ordinal) < 0) return true;
        string uvaPath = Path.Combine(root, "Assets", "Scripts", "Combat", "UnitVisualActor.cs");
        if (!File.Exists(uvaPath)) return true;
        if (File.ReadAllText(uvaPath).IndexOf("ApplyFacingScale", StringComparison.Ordinal) < 0) return true;
        // tip: fix-evil-facing-profile-build
        if (File.ReadAllText(uvaPath).IndexOf("Art faces LEFT by default", StringComparison.Ordinal) < 0) return true;
        if (gdtBody.IndexOf("IsLiveResourcesPool", StringComparison.Ordinal) < 0) return true;
        // tip: fix-architect-pool-load-list — TipEscape delegates NukeNamedProfileFiles; GDT must hold Profile SOs.
        string tipEscapePath = Path.Combine(root, "Assets", "Editor", "DemonKnightTipEscape.cs");
        if (!File.Exists(tipEscapePath)) return true;
        string tipEscapeBody = File.ReadAllText(tipEscapePath);
        if (tipEscapeBody.IndexOf("HardWriteNamedProfilesIfMissing", StringComparison.Ordinal) < 0) return true;
        if (tipEscapeBody.IndexOf("NukeNamedProfileFiles", StringComparison.Ordinal) < 0
            && tipEscapeBody.IndexOf(TipPackageId, StringComparison.Ordinal) < 0) return true;
        if (tipEscapeBody.IndexOf("OnLoadSessionGate", StringComparison.Ordinal) < 0) return true;
        // CS0246: ALL named Profile MonoScripts + helpers-in-GDT must exist on disk.
        if (DemonKnightPackageVersion.NamedProfileScriptsNeedRestore(root)) return true;
        if (syncBody.IndexOf("HardRestoreNamedProfileScripts", StringComparison.Ordinal) < 0) return true;
        if (syncBody.IndexOf("NukeNamedProfileFiles", StringComparison.Ordinal) < 0) return true;
        string bootPath = Path.Combine(root, "Assets", "Editor", "ArthurianMenuBootstrap.cs");
        if (!File.Exists(bootPath)) return true;
        string bootBody = File.ReadAllText(bootPath);
        if (bootBody.IndexOf("NukeNamedProfileFiles", StringComparison.Ordinal) < 0
            && bootBody.IndexOf("fix-evil-facing-profile-build", StringComparison.Ordinal) < 0
            && bootBody.IndexOf("fix-battle-ground-layout", StringComparison.Ordinal) < 0
            && bootBody.IndexOf("fix-architect-48-seat-preview", StringComparison.Ordinal) < 0) return true;
        string ensurePath = Path.Combine(root, "Assets", "Editor", "ArthurianStageLayoutProfileEnsure.cs");
        if (!File.Exists(ensurePath)) return true;
        string ensureBody = File.ReadAllText(ensurePath);
        if (ensureBody.IndexOf("TryResolveScriptRef", StringComparison.Ordinal) < 0) return true;
        if (ensureBody.IndexOf("CreateNamedPoolInstance", StringComparison.Ordinal) < 0) return true;
        if (ensureBody.IndexOf("TryRemapNamedPoolScript", StringComparison.Ordinal) < 0) return true;
        // tip: fix-evil-facing-profile-build — never CreateInstance mid-compile / defer Ensure.
        if (ensureBody.IndexOf("ScheduleEnsureAfterCompile", StringComparison.Ordinal) < 0) return true;
        if (ensureBody.IndexOf("ScriptsBusy", StringComparison.Ordinal) < 0) return true;
        if (ensureBody.IndexOf(TipPackageId, StringComparison.Ordinal) < 0) return true;
        if (archBody.IndexOf("Art authored facing LEFT", StringComparison.Ordinal) < 0) return true;
        if (archBody.IndexOf("PersistAllProfiles", StringComparison.Ordinal) < 0) return true;
        if (archBody.IndexOf("TryPewBodyFallbackFromGoodTroops", StringComparison.Ordinal) < 0
            && File.ReadAllText(Path.Combine(root, "Assets", "Scripts", "Data", "GameDataTypes.cs"))
                .IndexOf("TryPewBodyFallbackFromGoodTroops", StringComparison.Ordinal) < 0)
            return true;
        // tip: fix-architect-pool-load-list — stacked HUD after hierarchy rebuild needs HLG path.
        string churchPath = Path.Combine(root, "Assets", "Scripts", "UI", "SomersetChurchUI.cs");
        if (!File.Exists(churchPath)) return true;
        string churchBody = File.ReadAllText(churchPath);
        if (churchBody.IndexOf("ApplyHorizontalHudBarLayout", StringComparison.Ordinal) < 0) return true;
        if (churchBody.IndexOf("fix-evil-facing-profile-build", StringComparison.Ordinal) < 0
            && churchBody.IndexOf("fix-battle-ground-layout", StringComparison.Ordinal) < 0
            && churchBody.IndexOf("fix-architect-48-seat-preview", StringComparison.Ordinal) < 0) return true;
        if (gdtBody.IndexOf("fix-evil-facing-profile-build", StringComparison.Ordinal) < 0
            && gdtBody.IndexOf("fix-battle-ground-layout", StringComparison.Ordinal) < 0
            && gdtBody.IndexOf("fix-architect-48-seat-preview", StringComparison.Ordinal) < 0) return true;
        // tip: fix-architect-48-seat-preview — CS0104 TownRNG Random belt (Force Sync / Escape).
        string townRngPath = Path.Combine(root, "Assets", "Scripts", "Systems", "TownRNGGenerator.cs");
        if (!File.Exists(townRngPath)) return true;
        string townRngBody = File.ReadAllText(townRngPath);
        if (townRngBody.IndexOf("UnityEngine.Random.Range", StringComparison.Ordinal) < 0) return true;
        if (System.Text.RegularExpressions.Regex.IsMatch(
                townRngBody, @"(?<!UnityEngine\.)Random\.(Range|value)")) return true;
        string builderPath = Path.Combine(root, "Assets", "Editor", "ArthurianSceneBuilder.cs");
        if (!File.Exists(builderPath)) return true;
        string builderBody = File.ReadAllText(builderPath);
        if (builderBody.IndexOf("AddHudLayoutElement", StringComparison.Ordinal) < 0) return true;
        return false;
    }


    /// <summary>
    /// tip: fix-architect-pool-load-list — Profiles live in GameDataTypes. Delegate to Sync Nuke.
    /// </summary>
    public static int HardWriteNamedProfilesIfMissing(string root = null, System.Text.StringBuilder log = null)
    {
        return ArthurianSyncFromGitHub.NukeNamedProfileFiles(root, log);
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
                        || body.IndexOf("class SlotTransformConfig", StringComparison.Ordinal) < 0
                        || body.IndexOf("class StageParallaxLayer", StringComparison.Ordinal) < 0
                        || body.IndexOf("enum SceneBackgroundSlot", StringComparison.Ordinal) < 0
                        || body.IndexOf("class SceneBackgroundEntry", StringComparison.Ordinal) < 0
                        || body.IndexOf("class BattleSpriteSlotEntry", StringComparison.Ordinal) < 0
                        || body.IndexOf("class StageLayoutProfile", StringComparison.Ordinal) < 0
                        || body.IndexOf("class SceneBackgroundProfile", StringComparison.Ordinal) < 0
                        || body.IndexOf("class ClassSpritePoolProfile", StringComparison.Ordinal) < 0
                        || body.IndexOf("class LegendPewPortraitProfile", StringComparison.Ordinal) < 0
                        || body.IndexOf("class BattleGoodEvilSpritePoolProfile", StringComparison.Ordinal) < 0
                        || body.IndexOf(TipPackageId, StringComparison.Ordinal) < 0))
                    throw new Exception("GameDataTypes missing helpers/Appliers/Profile SOs (" + TipPackageId + ")");
                if (rel.EndsWith("SomersetChurchUI.cs", StringComparison.OrdinalIgnoreCase)
                    && (body.IndexOf("class SomersetChurchUI", StringComparison.Ordinal) < 0
                        || body.IndexOf("520f, 560f", StringComparison.Ordinal) < 0
                        || body.IndexOf("TrainATK", StringComparison.Ordinal) < 0
                        || body.IndexOf("ApplyHorizontalHudBarLayout", StringComparison.Ordinal) < 0
                        || body.IndexOf("ApplySeatSpriteVisualScale", StringComparison.Ordinal) < 0
                        || (body.IndexOf(TipPackageId, StringComparison.Ordinal) < 0
                            && body.IndexOf("fix-battle-ground-layout", StringComparison.Ordinal) < 0
                            && body.IndexOf("fix-stage-placer-profiles", StringComparison.Ordinal) < 0)))
                    throw new Exception("SomersetChurchUI missing tip church/HUD markers");
                if (rel.EndsWith("TownRNGGenerator.cs", StringComparison.OrdinalIgnoreCase)
                    && (body.IndexOf("class TownRNGGenerator", StringComparison.Ordinal) < 0
                        || body.IndexOf("AllocateUniqueCharacterName", StringComparison.Ordinal) < 0
                        || body.IndexOf("UnityEngine.Random.Range", StringComparison.Ordinal) < 0
                        || body.IndexOf("UnityEngine.Random.value", StringComparison.Ordinal) < 0
                        || System.Text.RegularExpressions.Regex.IsMatch(
                            body, @"(?<!UnityEngine\.)Random\.(Range|value)")))
                    throw new Exception("TownRNGGenerator missing UnityEngine.Random CS0104 belt");
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
                if (ArthurianSyncFromGitHub.WriteTextIfChanged(dest, body))
                {
                    ok++;
                    log.AppendLine($"OK {rel} ({body.Length} B)");
                    Debug.Log($"[DemonKnight] TipEscape wrote {rel} ({body.Length} bytes)");
                }
                else
                {
                    log.AppendLine($"SKIP unchanged {rel}");
                }
            }
            catch (Exception ex)
            {
                log.AppendLine($"MISS {rel}: {ex.Message}");
                Debug.LogWarning($"[DemonKnight] TipEscape miss {rel}: {ex.Message}");
            }
        }

        // Pew rebuild via reflection — TipEscape must compile even when Profile MonoScripts are
        // missing (Ensure → StageLayoutProfile CS0246 would otherwise block OnLoad restore).
        // tip: fix-architect-pool-load-list — skip during compile; never touch .cs from Ensure path here.
        try
        {
            if (!EditorApplication.isCompiling && !EditorApplication.isUpdating
                && (AssetDatabase.IsValidFolder("Assets/Art/Spliced/Sprite")
                    || AssetDatabase.LoadMainAssetAtPath("Assets/Resources/Art/Modular/ModularSpriteLibrary.asset") != null))
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
            if (nuked > 0) { ok += nuked; log.AppendLine("Nuked " + nuked + " duplicate PackageVersion (CS0436)."); }
        }
        catch (Exception ex) { log.AppendLine("PV nuke: " + ex.Message); }

        // tip: fix-architect-pool-load-list — Refresh ONLY when bytes changed.
        if (ok > 0) AssetDatabase.Refresh();
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
