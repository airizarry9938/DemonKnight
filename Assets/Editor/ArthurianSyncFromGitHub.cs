#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Cache;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Pull DemonKnight scripts from GitHub. NEVER use unpinned jsDelivr @main —
/// that CDN lags multi-commit and was serving cs1061-compat-api after sync-complete-replace
/// landed on GitHub. Prefer commit-pinned jsDelivr + raw.githubusercontent.com.
/// </summary>
public static class ArthurianSyncFromGitHub
{
    private const string Repo = "airizarry9938/DemonKnight";
    private const string RawBase = "https://raw.githubusercontent.com/" + Repo + "/";
    private const string JsDelivrBase = "https://cdn.jsdelivr.net/gh/" + Repo + "@";

    /// <summary>
    /// Emergency pin if GitHub API cannot resolve tip of main. After land, keep equal to main tip.
    /// RunSync prefers ResolveMainSha() so Force Sync tracks main without raw overwrites.
    /// </summary>
    // Ship pin = this tip feature SHA (fix-gdt-profile-always-compile). Prefer API tip / raw@main FIRST.
    private const string FallbackCommitSha = "873b12cbf9ff19697cc04af2eaef2513a925ed09";

    /// <summary>
    /// Hard tip for FORCE host overwrite. Update every ship.
    /// Never let FORCE land a 381-byte pre-OnLoad PackageVersion (architect-force-tip-ui).
    /// Prefer API tip / raw@main BEFORE HardTip — HardTip-first re-stuck Downloads on old SHAs.
    /// </summary>
    public const string HardTipCommitSha = "873b12cbf9ff19697cc04af2eaef2513a925ed09";

    private const string ExpectedPackageId = "fix-gdt-profile-always-compile";

    /// <summary>Package Ids that must never win FORCE host overwrite (always fall through to tip/main).</summary>
    public static readonly string[] StalePackageIds =
    {
        "architect-disk-tip-proof",
        "architect-breakglass-hosts",
        "architect-force-tip-ui",
        "single-sprite-no-paperdoll",
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
        "assign-only-runtime",
        "sync-keep-editor-menus",
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
    };

    /// <summary>
    /// Always overwrite from tip/main — never skip-self, never Fallback pin.
    /// TipEscape FIRST so even partial FORCE lands the escape hatch.
    /// GameDataTypes carries EMBEDDED Appliers — land before Force Delete wipe.
    /// </summary>
    public static readonly string[] SyncHostForceOverwritePaths =
    {
        "Assets/Editor/DemonKnightTipEscape.cs",
        "Assets/Editor/DemonKnightPackageVersion.cs",
        "Assets/Editor/ArthurianSyncFromGitHub.cs",
        "Assets/Editor/ArthurianMenuBootstrap.cs",
        "Assets/Editor/ArthurianArtArchitectWindow.cs",
        "Assets/Editor/ArthurianStageLayoutProfileEnsure.cs",
        "Assets/Editor/ArthurianArchitectBuildPersist.cs", // stub — type embedded in ArchitectWindow
        // Named MonoScript Profile SOs + shared types (Unity requires class name == file name).
        // tip: fix-gdt-profile-always-compile
        "Assets/Scripts/Data/StageLayoutProfile.cs",
        "Assets/Scripts/Data/SceneBackgroundProfile.cs",
        "Assets/Scripts/Data/ClassSpritePoolProfile.cs",
        "Assets/Scripts/Data/LegendPewPortraitProfile.cs",
        "Assets/Scripts/Data/BattleGoodEvilSpritePoolProfile.cs",
        // Applier embed host for SceneBackgroundApplier + ClassSpritePoolApplier (CS0103 hard fix).
        "Assets/Scripts/Data/GameDataTypes.cs",
    };

    /// <summary>
    /// EDITOR InitializeOnLoad — runs even when runtime Assembly-CSharp is CS0101-broken
    /// (GameLoopManager Init never runs in that state). Nukes Phase 2–5 standalones on disk.
    /// </summary>
    [InitializeOnLoadMethod]
    static void DkEditorNukePhase25StandalonesOnLoad()
    {
        try
        {
            string projectRoot = Directory.GetParent(Application.dataPath)!.FullName;
            // Precompile belt: HardWrite Profiles from embeds before nuke (CS0246 chicken-egg).
            try { HardWriteNamedProfilesFromEmbeds(projectRoot); } catch { /* ignore */ }
            int n = NukePhase25Standalones(projectRoot, writeStubIfNeeded: true, errors: null);
            int a = StubApplierStandalonesOver(projectRoot, errors: null);
            if (n > 0 || a > 0)
            {
                Debug.Log($"[DemonKnight] Editor OnLoad nuked Phase25={n} stubbed Appliers={a} (CS0101 guard).");
                EditorApplication.delayCall += () => AssetDatabase.Refresh();
            }
        }
        catch (Exception ex)
        {
            Debug.LogWarning("[DemonKnight] DA-59 Editor OnLoad nuke skipped: " + ex.Message);
        }
    }

    /// <summary>
    /// Never bulk-delete these before re-download. Force Delete+Sync used to wipe
    /// ArthurianSyncFromGitHub + MenuBootstrap first — if Unity reloaded or a download
    /// failed mid-pass, Bootstrap / Force Delete+Sync menus vanished.
    /// </summary>
    private static readonly HashSet<string> NeverBulkDeleteEditorMenus = new HashSet<string>(
        StringComparer.OrdinalIgnoreCase)
    {
        "Assets/Editor/ArthurianSyncFromGitHub.cs",
        "Assets/Editor/ArthurianMenuBootstrap.cs",
        "Assets/Editor/DemonKnightPackageVersion.cs",
        "Assets/Editor/DemonKnightTipEscape.cs",
        "Assets/Editor/ArthurianFixMisplacedScripts.cs",
    };

    private static readonly string[] ScriptPaths =
    {
        // Menu hosts FIRST — keep Arthurian menu alive even on a half-failed sync.
        "Assets/Editor/ArthurianSyncFromGitHub.cs",
        "Assets/Editor/ArthurianMenuBootstrap.cs",
        "Assets/Editor/DemonKnightPackageVersion.cs",
        // TipEscape early — OnLoad can HardRestore Profiles even when Sync assembly is stale.
        "Assets/Editor/DemonKnightTipEscape.cs",
        "Assets/Scripts/Systems/DemonKnightCompatApi.cs",
        // Architect *Profile SOs + shared types = named MonoScript files.
        // tip: fix-gdt-profile-always-compile — MUST be early (before GameDataTypes consumers).
        // Still do NOT download SpriteAnimClip/ModularSpriteLibrary/
        // ModularSpriteAnimator/StageParallaxController/DiceManager standalones (CS0101).
        "Assets/Scripts/Data/StageLayoutProfile.cs",
        "Assets/Scripts/Data/SceneBackgroundProfile.cs",
        "Assets/Scripts/Data/ClassSpritePoolProfile.cs",
        "Assets/Scripts/Data/LegendPewPortraitProfile.cs",
        "Assets/Scripts/Data/BattleGoodEvilSpritePoolProfile.cs",
        "Assets/Scripts/Data/GameDataTypes.cs",
        "Assets/Editor/ArthurianStageLayoutProfileEnsure.cs",
        // BuildPersist early — before Architect Save Promote* consumer (CS0103).
        "Assets/Editor/ArthurianArchitectBuildPersist.cs",
        "Assets/Editor/ArthurianArtArchitectWindow.cs",
        // Applier TYPES are EMBEDDED in GameDataTypes.cs (force-sync-appliers-hardfix).
        // These paths are stubs only — overwrite leftovers to prevent CS0101.
        "Assets/Scripts/UI/SceneBackgroundApplier.cs",
        "Assets/Scripts/UI/ClassSpritePoolApplier.cs",
        "Assets/Scripts/UI/MenuListSortBar.cs",
        "Assets/Scripts/UI/PewSeatHoverHighlight.cs",
        // SomersetChurchUI BEFORE GameLoop/ChurchStage consumers (CS0246 Force Delete race).
        "Assets/Scripts/UI/SomersetChurchUI.cs",
        "Assets/Scripts/Systems/GameLoopManager.cs",
        "Assets/Scripts/UI/ChurchStageUIController.cs",
        // Wave boss HUD — keep early so night label Sync never references a missing type.
        "Assets/Scripts/UI/WaveBossHudView.cs",
        "Assets/Scripts/Combat/AutoBattlerEngine.cs",
        "Assets/Scripts/Combat/BattlefieldVisualizer.cs",
        "Assets/Scripts/Combat/BattleBossDialogUI.cs",
        "Assets/Scripts/Combat/DemonHordeGenerator.cs",
        "Assets/Scripts/Combat/FloatingCombatText.cs",
        "Assets/Scripts/Combat/UnitVisualActor.cs",
        "Assets/Scripts/Data/TownsfolkAppearance.cs",
        "Assets/Scripts/Data/Characters/CharacterDefinition.cs",
        "Assets/Scripts/Data/Characters/CharacterDatabase.cs",
        "Assets/Scripts/Data/SomersetRunes.cs",
        "Assets/Scripts/Data/SomersetVisualDatabase.cs",
        "Assets/Scripts/Persistence/GrudgeSaveManager.cs",
        "Assets/Scripts/Persistence/LegendUnlockManager.cs",
        "Assets/Scripts/Persistence/MerlinResurrectionManager.cs",
        "Assets/Scripts/Persistence/WarbandAutosave.cs",
        "Assets/Scripts/Systems/CardFusionSystem.cs",
        "Assets/Scripts/Systems/DemonicPactManager.cs",
        "Assets/Scripts/Systems/PerkDraftManager.cs",
        "Assets/Scripts/Systems/RelicManager.cs",
        "Assets/Scripts/Systems/SinRuneManager.cs",
        "Assets/Scripts/Systems/TavernDiceMinigame.cs",
        "Assets/Scripts/Systems/TownRNGGenerator.cs",
        "Assets/Scripts/Systems/HiddenPotentialSystem.cs",
        "Assets/Scripts/UI/SomersetPewLayout.cs",
        "Assets/Scripts/UI/ChurchPewLayout.cs",
        "Assets/Scripts/UI/MainMenuUI.cs",
        "Assets/Scripts/UI/LegendCodexUI.cs",
        "Assets/Scripts/UI/PaperDollSeatView.cs",
        "Assets/Scripts/UI/TownsfolkVisualApplier.cs",
        "Assets/Scripts/UI/TownsfolkPortraitView.cs",
        "Assets/Scripts/UI/VictoryStarBadge.cs",
        "Assets/Scripts/UI/TownSplitUIController.cs",
        "Assets/Editor/ArthurianAlignCrowdPews.cs",
        "Assets/Editor/ArthurianFixMisplacedScripts.cs",
        "Assets/Editor/ArthurianFixMissingScripts.cs",
        "Assets/Editor/ArthurianRepairChurchStage.cs",
        "Assets/Editor/ArthurianSceneBuilder.cs",
        "Assets/Editor/SomersetAssetStudioWindow.cs",
        "Assets/Editor/ArthurianModularSpriteSheetImporterWindow.cs",
        "Assets/Editor/ArthurianBattleSpriteSheetSlicerWindow.cs",
        "Assets/Editor/ArthurianCharacterDatabaseEditor.cs",
        "Assets/Editor/ArthurianCharacterCatalogSeeder.cs",
        "Assets/Data/Characters/README.md",
    };

    private static readonly string[] AtomicCritical =
    {
        "Assets/Editor/ArthurianSyncFromGitHub.cs",
        "Assets/Editor/ArthurianMenuBootstrap.cs",
        "Assets/Editor/DemonKnightPackageVersion.cs",
        "Assets/Editor/DemonKnightTipEscape.cs",
        "Assets/Scripts/Systems/DemonKnightCompatApi.cs",
        // Named MonoScript Profiles + shared types + Applier embed host (tip fix-gdt-profile-always-compile).
        "Assets/Scripts/Data/StageLayoutProfile.cs",
        "Assets/Scripts/Data/SceneBackgroundProfile.cs",
        "Assets/Scripts/Data/ClassSpritePoolProfile.cs",
        "Assets/Scripts/Data/LegendPewPortraitProfile.cs",
        "Assets/Scripts/Data/BattleGoodEvilSpritePoolProfile.cs",
        "Assets/Scripts/Data/GameDataTypes.cs",
        // Stub paths only — real Appliers live in GameDataTypes (AtomicCritical via embed host).
        "Assets/Scripts/UI/SceneBackgroundApplier.cs",
        "Assets/Scripts/UI/ClassSpritePoolApplier.cs",
        "Assets/Editor/ArthurianStageLayoutProfileEnsure.cs",
        // BuildPersist before Architect consumer (Force Delete+skip race → CS0103).
        "Assets/Editor/ArthurianArchitectBuildPersist.cs",
        "Assets/Editor/ArthurianArtArchitectWindow.cs",
        // PewSeatHoverHighlight + MenuListSortBar are EMBEDDED in these two files.
        "Assets/Scripts/UI/PaperDollSeatView.cs",
        "Assets/Scripts/UI/VictoryStarBadge.cs",
        "Assets/Scripts/UI/LegendCodexUI.cs",
        // SomersetChurchUI before GameLoop/ChurchStage (CS0246).
        "Assets/Scripts/UI/SomersetChurchUI.cs",
        "Assets/Scripts/Systems/GameLoopManager.cs",
        "Assets/Scripts/UI/ChurchStageUIController.cs",
        "Assets/Scripts/UI/WaveBossHudView.cs",
        "Assets/Scripts/UI/TownSplitUIController.cs",
        "Assets/Scripts/UI/MainMenuUI.cs",
        "Assets/Scripts/Combat/AutoBattlerEngine.cs",
        "Assets/Scripts/Combat/BattlefieldVisualizer.cs",
        "Assets/Scripts/Combat/UnitVisualActor.cs",
        "Assets/Scripts/Combat/BattleBossDialogUI.cs",
        "Assets/Scripts/Persistence/LegendUnlockManager.cs",
        "Assets/Scripts/Persistence/MerlinResurrectionManager.cs",
        "Assets/Scripts/Systems/PerkDraftManager.cs",
        "Assets/Scripts/Systems/CardFusionSystem.cs",
        "Assets/Scripts/Systems/TavernDiceMinigame.cs",
        "Assets/Scripts/Data/Characters/CharacterDatabase.cs",
    };

    /// <summary>
    /// Always write these first + restore after Force Delete. Embeds live in PaperDoll/LegendCodex;
    /// Phase 2–5 standalones are NOT restored (types embedded — CS0101 if re-downloaded).
    /// </summary>
    private static readonly string[] MustRestoreUiHelpers =
    {
        // TipEscape host — OnLoad HardRestores Profiles when Sync assembly is still stale.
        "Assets/Editor/DemonKnightTipEscape.cs",
        // Named MonoScript Profiles + shared types — must survive Force Delete (CS0246).
        // tip: fix-gdt-profile-always-compile
        "Assets/Scripts/Data/StageLayoutProfile.cs",
        "Assets/Scripts/Data/SceneBackgroundProfile.cs",
        "Assets/Scripts/Data/ClassSpritePoolProfile.cs",
        "Assets/Scripts/Data/LegendPewPortraitProfile.cs",
        "Assets/Scripts/Data/BattleGoodEvilSpritePoolProfile.cs",
        // PRIMARY Applier embed host — must survive every Force Delete (DA-59 + hardfix).
        "Assets/Scripts/Data/GameDataTypes.cs",
        // Stub paths — rewrite leftovers so CS0101 cannot win against GameDataTypes embeds.
        "Assets/Scripts/UI/SceneBackgroundApplier.cs",
        "Assets/Scripts/UI/ClassSpritePoolApplier.cs",
        "Assets/Editor/ArthurianStageLayoutProfileEnsure.cs",
        // Architect Save Promote* type — Force Delete wiped while Architect stayed → CS0103.
        "Assets/Editor/ArthurianArchitectBuildPersist.cs",
        "Assets/Editor/ArthurianArtArchitectWindow.cs",
        // Embed hosts (DA-59 — Phase 2–5 types live here).
        "Assets/Scripts/UI/PaperDollSeatView.cs",
        "Assets/Scripts/Systems/GameLoopManager.cs",
        "Assets/Scripts/Systems/TavernDiceMinigame.cs",
        "Assets/Scripts/UI/LegendCodexUI.cs",
        "Assets/Scripts/UI/MenuListSortBar.cs",
        "Assets/Scripts/UI/PewSeatHoverHighlight.cs",
        // Church stage type — Force Delete wiped this while GameLoop/ChurchStage stayed → CS0246.
        "Assets/Scripts/UI/SomersetChurchUI.cs",
    };

    /// <summary>Standalone Phase 2–5 paths — never download; delete if they redefine embeds.</summary>
    /// <summary>
    /// Standalones that redefine types still embedded elsewhere (CS0101).
    /// StageLayoutProfile (+ other Architect *Profiles) are REAL named MonoScripts now — NOT killed.
    /// tip: fix-gdt-profile-always-compile
    /// </summary>
    public static readonly string[] Phase25StandaloneKillList =
    {
        "Assets/Scripts/Data/SpriteAnimClip.cs",
        "Assets/Scripts/Data/ModularSpriteLibrary.cs",
        "Assets/Scripts/UI/ModularSpriteAnimator.cs",
        "Assets/Scripts/Systems/StageParallaxController.cs",
        "Assets/Scripts/Systems/DiceManager.cs",
    };

    /// <summary>
    /// Applier standalones must be stubs (types embedded in GameDataTypes).
    /// Full class bodies here → CS0101 against the embed.
    /// </summary>
    public static readonly string[] ApplierStandaloneStubPaths =
    {
        "Assets/Scripts/UI/SceneBackgroundApplier.cs",
        "Assets/Scripts/UI/ClassSpritePoolApplier.cs",
    };

    [MenuItem("Arthurian/Sync All Scripts From GitHub", false, 0)]
    public static void SyncAll()
    {
        if (!EditorUtility.DisplayDialog(
                "DemonKnight Sync",
                "Download and REPLACE all DemonKnight scripts from GitHub?\n\n" +
                "• Uses commit-pinned jsDelivr (never stale @main)\n" +
                "• Deletes each .cs then rewrites (Windows-safe)\n" +
                "• Does NOT touch scenes or art\n" +
                "• Diagnose must show: " + ExpectedPackageId,
                "Sync Now", "Cancel"))
            return;

        RunSync(showDialog: true, deleteFirst: true, preDeleteAll: false);
    }

    [MenuItem("Arthurian/Force Delete+Sync ALL Scripts", false, 1)]
    public static void ForceDeleteSyncAll()
    {
        if (!EditorUtility.DisplayDialog(
                "DemonKnight FORCE Sync",
                "DELETE listed DemonKnight .cs (except Editor menu hosts), then re-download from a pinned GitHub commit.\n\n" +
                "Menu hosts (Sync / Bootstrap / Diagnose / PackageVersion) are NEVER bulk-deleted — they are replaced in place.\n" +
                "Scenes/art are NOT deleted.\nDiagnose → " + ExpectedPackageId,
                "Delete + Sync", "Cancel"))
            return;

        RunSync(showDialog: true, deleteFirst: true, preDeleteAll: true);
    }

    /// <summary>
    /// ONE-CLICK: overwrite Sync / MenuBootstrap / PackageVersion from raw @main only.
    /// Never uses FallbackCommitSha (that re-landed sync-keep-editor-menus forever when tip CDN 404'd).
    /// No skip-self. Minimal sanity — no ExpectedPackageId gate.
    /// </summary>
    /// <summary>
    /// Invoked by older MenuBootstrap FORCE menus. NEVER HardTip — TipEscape tip/main only.
    /// </summary>
    public static void ForceUpgradeSyncHostsMenu()
    {
        if (!EditorUtility.DisplayDialog(
                "FORCE Upgrade → Tip Escape",
                "Pull tip hosts from GitHub main (never HardTip).\n"
                + "Diagnose after compile: " + ExpectedPackageId + " (compiled=disk).",
                "Escape Now", "Cancel"))
            return;

        string report = DemonKnightTipEscape.EscapeNow(force: true);
        // Also overwrite SyncHost paths via tip/main DownloadRawMainOnly (no HardTip).
        var errors = new List<string>();
        int n = ForceOverwriteSyncHostsFromMain(errors);
        AssetDatabase.Refresh();
        string diskId = ReadPackageIdFromDisk() ?? "(unread)";
        EditorUtility.DisplayDialog(
            "Tip Escape",
            report + "\n\nHost overwrite ok=" + n + "/" + SyncHostForceOverwritePaths.Length
            + "\nPackage Id on disk: " + diskId
            + "\nExpected: " + ExpectedPackageId
            + "\nWait compile → Diagnose (compiled must equal disk)."
            + (errors.Count > 0 ? "\n\nFailures:\n" + string.Join("\n", errors) : ""),
            "OK");
    }

    /// <summary>
    /// Chicken-egg fix: overwrite Sync + MenuBootstrap + PackageVersion + CompatApi.
    /// ALWAYS force-overwrites the three menu hosts from raw @main first (never Fallback pin).
    /// </summary>
    [MenuItem("Arthurian/Bootstrap CompatApi + Sync Tools (pinned)", false, 3)]
    public static void BootstrapCompatAndSyncTools()
    {
        if (!EditorUtility.DisplayDialog(
                "DemonKnight Bootstrap",
                "1) FORCE overwrite Sync / Diagnose / PackageVersion from raw @main\n"
                + "2) Then pull CompatApi + embeds from tip of main\n\n"
                + "Diagnose must show: " + ExpectedPackageId + "\n"
                + "Use when Diagnose is stuck on sync-keep-editor-menus.",
                "Bootstrap Now", "Cancel"))
            return;

        ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12;
        string sha = ResolveMainSha() ?? "main";
        string projectRoot = Directory.GetParent(Application.dataPath)!.FullName;
        string[] bootstrap =
        {
            // Hosts first — but ForceOverwriteSyncHostsFromMain runs before this loop.
            "Assets/Editor/ArthurianSyncFromGitHub.cs",
            "Assets/Editor/ArthurianMenuBootstrap.cs",
            "Assets/Editor/DemonKnightPackageVersion.cs",
            "Assets/Scripts/Systems/DemonKnightCompatApi.cs",
            "Assets/Scripts/UI/WaveBossHudView.cs",
            "Assets/Scripts/Data/GameDataTypes.cs",
            "Assets/Scripts/UI/PaperDollSeatView.cs",
            "Assets/Scripts/UI/LegendCodexUI.cs",
            "Assets/Scripts/UI/PewSeatHoverHighlight.cs",
            "Assets/Scripts/UI/MenuListSortBar.cs",
            "Assets/Editor/ArthurianStageLayoutProfileEnsure.cs",
            "Assets/Editor/ArthurianArtArchitectWindow.cs",
        };

        var errors = new List<string>();
        int ok = 0;
        // DA-59 FIRST: Editor path — delete ModularSpriteLibrary.cs before any compile attempt.
        NukePhase25Standalones(projectRoot, writeStubIfNeeded: true, errors: errors);
        // CRITICAL: hosts from raw @main ONLY — never FallbackCommitSha (stale pin trap).
        ok += ForceOverwriteSyncHostsFromMain(errors);
        try
        {
            for (int i = 0; i < bootstrap.Length; i++)
            {
                string rel = bootstrap[i];
                // Already hard-wrote hosts from main — skip re-download via Fallback-prone path.
                if (IsSyncHostForcePath(rel)) continue;
                EditorUtility.DisplayProgressBar("DemonKnight Bootstrap", rel, (float)i / bootstrap.Length);
                try
                {
                    string body = DownloadPinned(rel, sha, out string source, relaxPackageId: true);
                    if (rel.EndsWith("DemonKnightCompatApi.cs", StringComparison.OrdinalIgnoreCase)
                        && !body.Contains("ResolveLoop"))
                        throw new Exception("CompatApi body still missing ResolveLoop (CDN stale)");
                    string dest = Path.Combine(projectRoot, rel.Replace('/', Path.DirectorySeparatorChar));
                    WriteReplacing(dest, body, deleteFirst: true);
                    ok++;
                    Debug.Log($"[DemonKnight] Bootstrapped {rel} via {source} @ {sha}");
                }
                catch (Exception ex)
                {
                    errors.Add($"{rel}: {ex.Message}");
                }
            }
        }
        finally
        {
            EditorUtility.ClearProgressBar();
        }

        NukePhase25Standalones(projectRoot, writeStubIfNeeded: true, errors: errors);
        StubPhase25StandalonesOver(projectRoot, errors);

        AssetDatabase.Refresh();
        string diskId = ReadPackageIdFromDisk() ?? "(unread)";
        string msg = $"Bootstrapped {ok} files (hosts from raw @main). Package on disk: {diskId}.";
        if (errors.Count > 0) msg += "\n\nFailures:\n" + string.Join("\n", errors);
        else msg += "\n\nWait for compile → Diagnose must show `" + ExpectedPackageId
                    + "` → then Force Delete+Sync ALL Scripts.";
        EditorUtility.DisplayDialog("DemonKnight Bootstrap", msg, "OK");
    }

    static bool IsSyncHostForcePath(string rel)
    {
        foreach (string h in SyncHostForceOverwritePaths)
            if (string.Equals(h, rel, StringComparison.OrdinalIgnoreCase)) return true;
        return false;
    }

    /// <summary>
    /// Download Sync / MenuBootstrap / PackageVersion from raw @main only (cache-busted).
    /// Never FallbackCommitSha. Never skip-self. Minimal marker checks only.
    /// </summary>
    public static int ForceOverwriteSyncHostsFromMain(List<string> errors = null)
    {
        ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12;
        string projectRoot = Directory.GetParent(Application.dataPath)!.FullName;
        // Always clear stale Architect/PaperDoll editor copies before host overwrite.
        NukeStaleArchitectAndPaperDollEditors(projectRoot);
        CloseOpenArchitectWindows();
        int ok = 0;
        foreach (string rel in SyncHostForceOverwritePaths)
        {
            try
            {
                string body = DownloadRawMainOnly(rel, out string source);
                if (string.IsNullOrEmpty(body) || body.Length < 80)
                    throw new Exception("empty body from " + source);
                // Minimal markers — do NOT gate on ExpectedPackageId (that blocked tip upgrades).
                if (rel.EndsWith("ArthurianSyncFromGitHub.cs", StringComparison.OrdinalIgnoreCase)
                    && !(body.Contains("ForceDeleteSyncAll") && body.Contains("BootstrapCompatAndSyncTools")))
                    throw new Exception("Sync host body missing menus (" + source + ")");
                if (rel.EndsWith("ArthurianMenuBootstrap.cs", StringComparison.OrdinalIgnoreCase)
                    && !body.Contains("Diagnose Project"))
                    throw new Exception("MenuBootstrap body missing Diagnose (" + source + ")");
                if (rel.EndsWith("DemonKnightPackageVersion.cs", StringComparison.OrdinalIgnoreCase))
                {
                    if (!body.Contains("public const string Id"))
                        throw new Exception("PackageVersion body missing Id (" + source + ")");
                    if (body.Length < 500)
                        throw new Exception("PackageVersion too small " + body.Length + "B from " + source
                                            + " (want OnLoad break-glass ≥500B)");
                }
                if (rel.EndsWith("ArthurianArtArchitectWindow.cs", StringComparison.OrdinalIgnoreCase)
                    && !IsTipArchitectBody(body))
                    throw new Exception("Architect body still paperdoll / missing Sprite category (" + source + ")");
                if (rel.EndsWith("GameDataTypes.cs", StringComparison.OrdinalIgnoreCase)
                    && (body.IndexOf("class SceneBackgroundApplier", StringComparison.Ordinal) < 0
                        || body.IndexOf("class ClassSpritePoolApplier", StringComparison.Ordinal) < 0))
                    throw new Exception("GameDataTypes missing embedded Appliers (" + source + ")");

                string dest = Path.Combine(projectRoot, rel.Replace('/', Path.DirectorySeparatorChar));
                // Always delete+rewrite Architect — never leave a stale paperdoll body.
                if (rel.EndsWith("ArthurianArtArchitectWindow.cs", StringComparison.OrdinalIgnoreCase))
                {
                    NukeStaleArchitectAndPaperDollEditors(projectRoot);
                    TryDeleteFile(dest);
                }
                WriteReplacing(dest, body, deleteFirst: true);
                ok++;
                Debug.Log($"[DemonKnight] FORCE host overwrite {rel} via {source} ({body.Length} bytes)");
                if (rel.EndsWith("GameDataTypes.cs", StringComparison.OrdinalIgnoreCase))
                    StubApplierStandalonesOver(projectRoot, errors);
            }
            catch (Exception ex)
            {
                errors?.Add($"{rel}: {ex.Message}");
                Debug.LogError($"[DemonKnight] FORCE host overwrite FAILED {rel}: {ex.Message}");
            }
        }
        return ok;
    }

    /// <summary>
    /// FORCE host download: API tip + raw@main FIRST, HardTip last.
    /// Rejects 381B PackageVersion and known-stale package Ids (architect-disk-tip-proof, …).
    /// </summary>
    static string DownloadRawMainOnly(string rel, out string source)
    {
        Exception last = null;
        string tip = ResolveMainSha();
        if (string.IsNullOrEmpty(tip) || tip.Length < 7) tip = null;

        for (int attempt = 0; attempt < 4; attempt++)
        {
            string bust = "t=" + DateTime.UtcNow.Ticks + "&r=" + Guid.NewGuid().ToString("N") + "&a=" + attempt;
            var urlList = new List<string>();
            var labelList = new List<string>();
            // Tip of main + branch tip FIRST — HardTip-first re-landed architect-disk-tip-proof forever.
            var shas = new List<string>();
            if (!string.IsNullOrEmpty(tip)) shas.Add(tip);
            shas.Add("main");
            if (!string.IsNullOrEmpty(HardTipCommitSha)
                && (tip == null || !string.Equals(tip, HardTipCommitSha, StringComparison.OrdinalIgnoreCase)))
                shas.Add(HardTipCommitSha);
            foreach (string sha in shas)
            {
                urlList.Add(RawBase + sha + "/" + rel + "?" + bust);
                labelList.Add("raw@" + sha + "+bust");
                urlList.Add(RawBase + sha + "/" + rel);
                labelList.Add("raw@" + sha);
                urlList.Add(JsDelivrBase + sha + "/" + rel + "?" + bust);
                labelList.Add("jsdelivr@" + sha + "+bust");
            }

            for (int i = 0; i < urlList.Count; i++)
            {
                try
                {
                    string body = DownloadString(urlList[i]);
                    if (LooksLikeHttpFailureBody(body))
                        throw new Exception("Empty or 404 (len=" + (body == null ? 0 : body.Length) + ")");
                    if (rel.EndsWith("DemonKnightPackageVersion.cs", StringComparison.OrdinalIgnoreCase))
                    {
                        // 381-byte pre-OnLoad PackageVersion is the stuck FORCE symptom — reject.
                        if (body.Length < 500)
                            throw new Exception("PackageVersion too small (" + body.Length
                                + "B) — stale tip SHA; need OnLoad break-glass ≥500B");
                        if (!body.Contains("public const string Id"))
                            throw new Exception("PackageVersion missing Id");
                        if (body.Contains("sync-keep-editor-menus") && !body.Contains(ExpectedPackageId))
                            throw new Exception("stale PackageVersion (sync-keep-editor-menus)");
                        string gotId = TryParsePackageId(body);
                        if (IsStalePackageId(gotId))
                            throw new Exception("stale PackageVersion Id=" + gotId
                                + " — need " + ExpectedPackageId);
                        // Never accept breakglass even if HardTip somehow wins.
                        if (body.IndexOf("Id = \"architect-breakglass-hosts\"", StringComparison.Ordinal) >= 0)
                            throw new Exception("rejected architect-breakglass-hosts PackageVersion");
                    }
                    if (rel.EndsWith("ArthurianArtArchitectWindow.cs", StringComparison.OrdinalIgnoreCase))
                    {
                        // Accept structural tip, but reject Architect still branded with a stale UiBuildId.
                        if (!IsTipArchitectBody(body))
                            throw new Exception("Architect body not tip-shaped");
                        if (IsStaleArchitectUiBuild(body))
                            throw new Exception("Architect UiBuildId still stale (want " + ExpectedPackageId + ")");
                    }
                    source = labelList[i] + (attempt > 0 ? "#retry" + attempt : "");
                    return body;
                }
                catch (Exception ex) { last = ex; }
            }
        }
        throw last ?? new Exception("raw tip/main download failed for " + rel);
    }

    public static string TryParsePackageId(string body)
    {
        if (string.IsNullOrEmpty(body)) return null;
        var m = Regex.Match(body, @"public\s+const\s+string\s+Id\s*=\s*""([^""]+)""");
        return m.Success ? m.Groups[1].Value : null;
    }

    public static bool IsStalePackageId(string id)
    {
        if (string.IsNullOrEmpty(id)) return true;
        if (string.Equals(id, ExpectedPackageId, StringComparison.Ordinal)) return false;
        foreach (string s in StalePackageIds)
            if (string.Equals(s, id, StringComparison.OrdinalIgnoreCase)) return true;
        return false;
    }

    public static bool IsStaleArchitectUiBuild(string body)
    {
        if (string.IsNullOrEmpty(body)) return true;
        // Tip Architect must advertise current package Id as UiBuildId.
        if (body.IndexOf(ExpectedPackageId, StringComparison.Ordinal) >= 0) return false;
        foreach (string s in StalePackageIds)
            if (body.IndexOf("\"" + s + "\"", StringComparison.Ordinal) >= 0
                || body.IndexOf("UiBuildId = \"" + s + "\"", StringComparison.Ordinal) >= 0)
                return true;
        // Unknown older build without ExpectedPackageId — treat as stale so FORCE keeps seeking tip.
        return body.IndexOf("UiBuildId", StringComparison.Ordinal) >= 0;
    }

    /// <summary>Read PackageVersion Id from disk (Diagnose compiled type can lag).</summary>
    public static string ReadPackageIdFromDisk()
    {
        try
        {
            string path = Path.Combine(
                Directory.GetParent(Application.dataPath)!.FullName,
                "Assets", "Editor", "DemonKnightPackageVersion.cs");
            if (!File.Exists(path)) return null;
            string text = File.ReadAllText(path);
            var m = Regex.Match(text, @"public\s+const\s+string\s+Id\s*=\s*""([^""]+)""");
            return m.Success ? m.Groups[1].Value : null;
        }
        catch { return null; }
    }

    /// <summary>Comment-only stub — no type defs (CS0101-safe overwrite when delete fails).</summary>
    static string Phase25StandaloneStub(string rel)
    {
        string host = rel.Contains("ModularSpriteAnimator") ? "PaperDollSeatView.cs"
            : rel.Contains("StageParallax") ? "GameLoopManager.cs"
            : rel.Contains("DiceManager") ? "TavernDiceMinigame.cs"
            : "GameDataTypes.cs";
        return "// DA-59: EMBEDDED in " + host + " — Sync nuked this standalone (CS0101).\n"
               + "// Do not put public class here. Safe to delete this file.\n";
    }

    /// <summary>Delete Phase 2–5 standalone .cs (+ .meta) that redefine embedded types.</summary>
    public static int DeletePhase25Standalones(string projectRoot = null, List<string> errors = null)
    {
        return NukePhase25Standalones(projectRoot, writeStubIfNeeded: false, errors: errors);
    }

    /// <summary>
    /// Force-Sync / Editor OnLoad nuke: DELETE standalones (+ .meta). Optionally overwrite with
    /// a comment stub when delete fails or when GameDataTypes Sync demands a safe on-disk body.
    /// Runs from Editor assembly — does NOT wait for runtime compile.
    /// </summary>
    public static int NukePhase25Standalones(
        string projectRoot = null,
        bool writeStubIfNeeded = true,
        List<string> errors = null)
    {
        projectRoot ??= Directory.GetParent(Application.dataPath)!.FullName;
        int touched = 0;
        foreach (string rel in Phase25StandaloneKillList)
        {
            try
            {
                string dest = Path.Combine(projectRoot, rel.Replace('/', Path.DirectorySeparatorChar));
                bool existed = File.Exists(dest);
                bool hadType = false;
                if (existed)
                {
                    string body = File.ReadAllText(dest);
                    hadType = body.IndexOf("public class ", StringComparison.Ordinal) >= 0
                              || body.IndexOf("public partial class ", StringComparison.Ordinal) >= 0
                              || body.IndexOf("class SpriteAnimClip", StringComparison.Ordinal) >= 0
                              || body.IndexOf("class StageLayoutProfile", StringComparison.Ordinal) >= 0
                              || body.IndexOf("class ModularSpriteLibrary", StringComparison.Ordinal) >= 0
                              || body.IndexOf("class ModularSpriteAnimator", StringComparison.Ordinal) >= 0
                              || body.IndexOf("class StageParallaxController", StringComparison.Ordinal) >= 0
                              || body.IndexOf("class DiceManager", StringComparison.Ordinal) >= 0
                              || body.IndexOf("class SlotTransformConfig", StringComparison.Ordinal) >= 0
                              || body.IndexOf("class ModularHeadEntry", StringComparison.Ordinal) >= 0
                              || body.IndexOf("class ModularAnimRegistration", StringComparison.Ordinal) >= 0
                              || body.IndexOf("class StageParallaxLayer", StringComparison.Ordinal) >= 0;
                }

                if (existed)
                {
                    try { File.SetAttributes(dest, FileAttributes.Normal); } catch { /* ignore */ }
                    try { File.Delete(dest); } catch (Exception delEx)
                    {
                        Debug.LogWarning($"[DemonKnight] Delete failed {rel}: {delEx.Message} — will stub.");
                    }
                    string meta = dest + ".meta";
                    if (File.Exists(meta))
                    {
                        try { File.SetAttributes(meta, FileAttributes.Normal); } catch { /* ignore */ }
                        try { File.Delete(meta); } catch { /* ignore */ }
                    }
                }

                // After GameDataTypes Sync / when delete fails: overwrite with type-free stub.
                if (writeStubIfNeeded && (hadType || File.Exists(dest) || existed))
                {
                    // Prefer gone; if still present OR we want a hard overwrite after GameDataTypes:
                    if (File.Exists(dest) || hadType)
                    {
                        try
                        {
                            Directory.CreateDirectory(Path.GetDirectoryName(dest)!);
                            if (File.Exists(dest))
                            {
                                try { File.SetAttributes(dest, FileAttributes.Normal); } catch { /* ignore */ }
                            }
                            // If delete succeeded, skip re-creating unless writeStubAlways path.
                            // When hadType and delete succeeded, leave deleted (cleanest).
                            if (File.Exists(dest))
                            {
                                File.WriteAllText(dest, Phase25StandaloneStub(rel));
                                Debug.Log($"[DemonKnight] DA-59 stubbed Phase 2–5 standalone {rel}");
                            }
                        }
                        catch (Exception stubEx)
                        {
                            errors?.Add($"{rel} (stub): {stubEx.Message}");
                        }
                    }
                }

                if (existed || hadType)
                {
                    touched++;
                    Debug.Log($"[DemonKnight] DA-59 nuked Phase 2–5 standalone {rel} (hadType={hadType}, gone={!File.Exists(dest)})");
                }
            }
            catch (Exception ex)
            {
                errors?.Add($"{rel} (nuke): {ex.Message}");
                Debug.LogWarning($"[DemonKnight] Could not nuke {rel}: {ex.Message}");
            }
        }
        return touched;
    }

    /// <summary>
    /// During Sync of GameDataTypes: overwrite kill-list paths with comment stubs (or delete).
    /// Guarantees ModularSpriteLibrary.cs cannot keep a public class beside the embed.
    /// </summary>
    public static int StubPhase25StandalonesOver(string projectRoot = null, List<string> errors = null)
    {
        projectRoot ??= Directory.GetParent(Application.dataPath)!.FullName;
        int n = 0;
        foreach (string rel in Phase25StandaloneKillList)
        {
            try
            {
                string dest = Path.Combine(projectRoot, rel.Replace('/', Path.DirectorySeparatorChar));
                Directory.CreateDirectory(Path.GetDirectoryName(dest)!);
                if (File.Exists(dest))
                {
                    try { File.SetAttributes(dest, FileAttributes.Normal); } catch { /* ignore */ }
                }
                // Hard overwrite — replaces full class bodies from old pins.
                File.WriteAllText(dest, Phase25StandaloneStub(rel));
                string meta = dest + ".meta";
                if (File.Exists(meta))
                {
                    try { File.SetAttributes(meta, FileAttributes.Normal); } catch { /* ignore */ }
                    try { File.Delete(meta); } catch { /* ignore */ }
                }
                n++;
                Debug.Log($"[DemonKnight] DA-59 GameDataTypes-pass stubbed {rel}");
            }
            catch (Exception ex)
            {
                errors?.Add($"{rel} (gdt-stub): {ex.Message}");
            }
        }
        // Prefer delete after stub write so no ghost files remain.
        NukePhase25Standalones(projectRoot, writeStubIfNeeded: false, errors: errors);
        n += StubApplierStandalonesOver(projectRoot, errors);
        return n;
    }

    static string ApplierStandaloneStub(string rel)
    {
        string name = Path.GetFileName(rel);
        return "// Types live in GameDataTypes.cs (Force Sync embed — avoids CS0103 when this\n"
               + "// standalone path is wiped by Force Delete under a stale compiled Sync host).\n"
               + "// This stub overwrites any leftover full copy so we never get CS0101 duplicates.\n"
               + "// Was: " + name + "\n"
               + "namespace ArthurianAutobattler\n{\n}\n";
    }

    /// <summary>
    /// Hard-overwrite Applier standalones with type-free stubs so embeds in GameDataTypes win.
    /// Called on Editor OnLoad, after GameDataTypes Sync, and after Force Delete ensure.
    /// </summary>
    public static int StubApplierStandalonesOver(string projectRoot = null, List<string> errors = null)
    {
        projectRoot ??= Directory.GetParent(Application.dataPath)!.FullName;
        int n = 0;
        foreach (string rel in ApplierStandaloneStubPaths)
        {
            try
            {
                string dest = Path.Combine(projectRoot, rel.Replace('/', Path.DirectorySeparatorChar));
                Directory.CreateDirectory(Path.GetDirectoryName(dest)!);
                bool rewrite = true;
                if (File.Exists(dest))
                {
                    try { File.SetAttributes(dest, FileAttributes.Normal); } catch { /* ignore */ }
                    string cur = File.ReadAllText(dest);
                    // Already a stub (no class body) — leave unless it somehow still defines the type.
                    if (cur.IndexOf("class SceneBackgroundApplier", StringComparison.Ordinal) < 0
                        && cur.IndexOf("class ClassSpritePoolApplier", StringComparison.Ordinal) < 0
                        && cur.IndexOf("GameDataTypes.cs", StringComparison.Ordinal) >= 0)
                        rewrite = false;
                }
                if (!rewrite) continue;
                File.WriteAllText(dest, ApplierStandaloneStub(rel));
                n++;
                Debug.Log($"[DemonKnight] Stubbed Applier standalone {rel} (embed lives in GameDataTypes)");
            }
            catch (Exception ex)
            {
                errors?.Add($"{rel} (applier-stub): {ex.Message}");
                Debug.LogWarning($"[DemonKnight] Could not stub Applier {rel}: {ex.Message}");
            }
        }
        return n;
    }

    /// <summary>Legacy name — now nukes standalones instead of fetching.</summary>
    static void TryFetchPhase25Insurance(List<string> errors, List<string> okFiles)
    {
        try
        {
            int n = NukePhase25Standalones(null, writeStubIfNeeded: true, errors: errors);
            Debug.Log($"[DemonKnight] Phase 2–5 insurance nuked {n} standalone(s).");
            if (okFiles != null)
            {
                foreach (string rel in Phase25StandaloneKillList)
                {
                    if (!okFiles.Contains(rel)) okFiles.Add(rel + " (nuked)");
                }
            }
        }
        catch (Exception ex)
        {
            Debug.LogWarning("[DemonKnight] Phase 2–5 nuke insurance skipped: " + ex.Message);
            errors?.Add("Phase 2–5 nuke: " + ex.Message);
        }
    }

    [MenuItem("Arthurian/Force Re-Sync Night/HUD Fix", false, 3)]
    public static void ForceResyncNightFix() => ForceDeleteSyncAll();

    public static void RunSync(bool showDialog, bool deleteFirst = true, bool preDeleteAll = false)
    {
        ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12;
        // Prefer tip of main so Downloads Unity Force Sync tracks GitHub without raw overwrites.
        string sha = ResolveMainSha() ?? FallbackCommitSha;
        Debug.Log($"[DemonKnight] Sync pinning downloads to commit {sha} (force-sync-main-landed; main tip preferred)");

        var errors = new List<string>();
        var okFiles = new List<string>();
        string projectRoot = Directory.GetParent(Application.dataPath)!.FullName;

        // NUCLEAR: HardWrite named Profile MonoScripts from embeds BEFORE Nuke/Delete/download.
        // Sync host is already compiled — offline embeds break the TipEscape chicken-egg (CS0246).
        // tip: fix-gdt-profile-always-compile
        try
        {
            int hw = HardWriteNamedProfilesFromEmbeds(projectRoot);
            if (hw > 0)
                Debug.Log("[DemonKnight] Pre-Sync HardWriteEmbed Profiles wrote " + hw);
        }
        catch (Exception ex)
        {
            Debug.LogWarning("[DemonKnight] Pre-Sync HardWriteEmbed: " + ex.Message);
        }

        try
        {
            // DA-59 FIRST (before compile / before any download): kill ModularSpriteLibrary.cs etc.
            // Runs in Editor menu path — does not need runtime Assembly-CSharp to compile.
            EditorUtility.DisplayProgressBar("DemonKnight Sync", "Nuking Phase 2–5 CS0101 standalones…", 0f);
            NukePhase25Standalones(projectRoot, writeStubIfNeeded: true, errors: errors);

            // NEVER skip-self: overwrite Sync / MenuBootstrap / PackageVersion from raw @main FIRST.
            // Old hosts used FallbackCommitSha after tip CDN 404 → stuck on sync-keep-editor-menus.
            EditorUtility.DisplayProgressBar("DemonKnight Sync", "FORCE overwrite Sync hosts from raw @main…", 0f);
            int hostOk = ForceOverwriteSyncHostsFromMain(errors);
            Debug.Log($"[DemonKnight] Force Sync host overwrite from main: {hostOk}/{SyncHostForceOverwritePaths.Length}");
            foreach (string h in SyncHostForceOverwritePaths)
                if (!okFiles.Contains(h)) okFiles.Add(h);

            // BEFORE Force Delete: land embeds + stubs so CS0103 never wins a race.
            EnsureUiHelpersPresent(projectRoot, sha, errors, okFiles);
            // Belt: even if MustRestore arrays are stale in a mid-upgrade host, stub Appliers now.
            StubApplierStandalonesOver(projectRoot, errors);

            if (preDeleteAll)
            {
                EditorUtility.DisplayProgressBar("DemonKnight Sync", "Deleting local script copies…", 0f);
                foreach (string rel in ScriptPaths)
                {
                    // NEVER wipe menu hosts first — that is what made Bootstrap/Force Sync disappear.
                    if (NeverBulkDeleteEditorMenus.Contains(rel)) continue;
                    // Keep embed hosts + stubs across Force Delete wipe.
                    if (IsMustRestoreUiHelper(rel)) continue;
                    // Never wipe GameDataTypes even if somehow missing from MustRestore (Applier embed).
                    if (rel.EndsWith("GameDataTypes.cs", StringComparison.OrdinalIgnoreCase)) continue;
                    // Never wipe named MonoScript Profile SOs (Unity SO binding — tip fix-gdt-profile-always-compile).
                    if (rel.EndsWith("StageLayoutProfile.cs", StringComparison.OrdinalIgnoreCase)
                        || rel.EndsWith("SceneBackgroundProfile.cs", StringComparison.OrdinalIgnoreCase)
                        || rel.EndsWith("ClassSpritePoolProfile.cs", StringComparison.OrdinalIgnoreCase)
                        || rel.EndsWith("LegendPewPortraitProfile.cs", StringComparison.OrdinalIgnoreCase)
                        || rel.EndsWith("BattleGoodEvilSpritePoolProfile.cs", StringComparison.OrdinalIgnoreCase))
                        continue;
                    // Never wipe SomersetChurchUI — stale Sync MustRestore omitted it → CS0246.
                    if (rel.EndsWith("SomersetChurchUI.cs", StringComparison.OrdinalIgnoreCase)) continue;
                    // Never wipe ArchitectBuildPersist — ForceOverwrite host + Force Delete skip race → CS0103.
                    if (rel.EndsWith("ArthurianArchitectBuildPersist.cs", StringComparison.OrdinalIgnoreCase)) continue;
                    TryDeleteFile(Path.Combine(projectRoot, rel.Replace('/', Path.DirectorySeparatorChar)));
                }
                // Force Delete must not leave Phase 2–5 standalones either.
                NukePhase25Standalones(projectRoot, writeStubIfNeeded: false, errors: errors);
                StubApplierStandalonesOver(projectRoot, errors);
            }

            for (int i = 0; i < ScriptPaths.Length; i++)
            {
                string rel = ScriptPaths[i];
                EditorUtility.DisplayProgressBar("DemonKnight Sync", rel + " @ " + sha, (float)i / ScriptPaths.Length);
                string dest = Path.Combine(projectRoot, rel.Replace('/', Path.DirectorySeparatorChar));
                bool protectMenu = NeverBulkDeleteEditorMenus.Contains(rel);
                bool isForceHost = IsSyncHostForcePath(rel);

                // Already hard-wrote Sync/MenuBootstrap/PackageVersion from raw @main — do not
                // re-download via FallbackCommitSha path (that re-stuck sync-keep-editor-menus).
                if (isForceHost)
                {
                    if (!okFiles.Contains(rel)) okFiles.Add(rel);
                    continue;
                }

                try
                {
                    string text = DownloadPinned(rel, sha, out string source, relaxPackageId: protectMenu);
                    if (!protectMenu) ValidateCritical(rel, text);
                    // No skip-self. Menu hosts outside the force-three still replace in place.
                    WriteReplacing(dest, text, deleteFirst: deleteFirst && !protectMenu);
                    VerifyOnDisk(rel, dest, relaxPackageId: protectMenu);
                    okFiles.Add(rel);
                    Debug.Log($"[DemonKnight] Synced {rel} via {source}");

                    // DA-59: the moment GameDataTypes (embed host) lands, stub/delete standalones.
                    if (rel.EndsWith("GameDataTypes.cs", StringComparison.OrdinalIgnoreCase))
                        StubPhase25StandalonesOver(projectRoot, errors);
                }
                catch (Exception ex)
                {
                    if (protectMenu && File.Exists(dest))
                    {
                        okFiles.Add(rel);
                        Debug.LogWarning(
                            $"[DemonKnight] Kept local {rel} after CDN miss ({ex.Message}). Use FORCE Upgrade Sync Hosts.");
                        continue;
                    }
                    errors.Add($"{rel}: {ex.Message}");
                    Debug.LogError($"[DemonKnight] Sync FAILED {rel}: {ex.Message}");
                }
            }

            // If a mid-pass failure still left a menu host missing, restore it now.
            EnsureEditorMenuHostsPresent(projectRoot, sha, errors, okFiles);
            // Force Delete can wipe helpers then fail mid-pass — restore before compile.
            EnsureUiHelpersPresent(projectRoot, sha, errors, okFiles);
            // DA-59: nuke again after ensure (GameDataTypes restore may race with old pin leftovers).
            StubPhase25StandalonesOver(projectRoot, errors);
            // Hard guarantee: Applier stubs on disk; types in GameDataTypes.
            StubApplierStandalonesOver(projectRoot, errors);
            HardRestoreNamedProfileScripts(projectRoot, sha, errors, okFiles);
            // Embed belt if CDN miss left Profiles short/corrupt.
            try { HardWriteNamedProfilesFromEmbeds(projectRoot); } catch { /* ignore */ }
            HardRestoreApplierEmbedHost(projectRoot, sha, errors, okFiles);
            HardRestoreSomersetChurchUI(projectRoot, sha, errors, okFiles);
            HardRestoreArchitectBuildPersist(projectRoot, sha, errors, okFiles);
            TryFetchPhase25Insurance(errors, okFiles);
        }
        finally
        {
            EditorUtility.ClearProgressBar();
        }

        ArthurianFixMisplacedScripts.Run();
        // After FixMisplaced (old compiled maps may re-drop standalones) — nuke again BEFORE refresh/compile.
        NukePhase25Standalones(projectRoot, writeStubIfNeeded: true, errors: errors);
        ArthurianMenuBootstrap.DedupeArtToolScripts();
        NukeStaleArchitectAndPaperDollEditors(projectRoot);
        CloseOpenArchitectWindows();
        AssetDatabase.Refresh();
        Debug.Log("[DemonKnight] " + ArchitectTipMarkersReport());

        // Post-Sync: escape breakglass hosts if needed, then rebuild pew library from Spliced/Sprite.
        string modularReport = null;
        try
        {
            if (ArthurianStageLayoutProfileEnsure.HostsLookStalePublic())
            {
                modularReport = ArthurianStageLayoutProfileEnsure.EscapeStaleHostsAndRebuildPewLibrary();
                Debug.Log("[DemonKnight] Post-Sync EscapeBreakglass:\n" + modularReport);
            }
            else
            {
                string wipe = DemonKnightPackageVersion.WipeModularPaperdollAssets();
                string rebuild = ArthurianStageLayoutProfileEnsure.RebuildPewLibraryFromSplices();
                modularReport = wipe + "\n" + rebuild;
                Debug.Log("[DemonKnight] Post-Sync spliced pew rebuild:\n" + modularReport);
            }
        }
        catch (Exception ex)
        {
            errors.Add("Escape/rebuild pew library: " + ex.Message);
            Debug.LogWarning("[DemonKnight] Post-Sync pew library failed: " + ex.Message);
        }
        try
        {
            ArthurianStageLayoutProfileEnsure.EnsureAsset();
            ArthurianStageLayoutProfileEnsure.EnsureSceneBackgroundAsset();
            ArthurianStageLayoutProfileEnsure.EnsureClassSpritePoolAsset();
            ArthurianStageLayoutProfileEnsure.EnsureLegendPewPortraitAsset();
            ArthurianStageLayoutProfileEnsure.EnsureBattleGoodEvilSpritePoolAsset();
        }
        catch (Exception ex)
        {
            errors.Add("Ensure StageLayoutProfile: " + ex.Message);
            Debug.LogWarning("[DemonKnight] Post-Sync StageLayout ensure failed: " + ex.Message);
        }

        // After tip Architect lands, close any leftover window so Open() shows tip UI.
        EditorApplication.delayCall += () =>
        {
            try
            {
                CloseOpenArchitectWindows();
                NukeStaleArchitectAndPaperDollEditors(null);
            }
            catch { /* ignore */ }
        };

        var missingCritical = new List<string>();
        foreach (string rel in AtomicCritical)
        {
            if (!okFiles.Contains(rel))
                missingCritical.Add(rel);
        }

        string msg = $"Synced {okFiles.Count}/{ScriptPaths.Length} files @ {sha}.";
        if (errors.Count > 0)
            msg += "\n\nFailures:\n" + string.Join("\n", errors);
        if (missingCritical.Count > 0)
        {
            msg += "\n\nINCOMPLETE — critical files missing:\n" + string.Join("\n", missingCritical)
                   + "\n\nTry: Arthurian → Bootstrap CompatApi + Sync Tools, then Force Delete+Sync again.\n"
                   + "Or overwrite from jsDelivr @" + sha + " (see README).";
        }
        else
        {
            msg += $"\n\nWait for compile. Diagnose must show `{ExpectedPackageId}` "
                   + $"(disk Id now: {ReadPackageIdFromDisk() ?? "?"}).\n"
                   + "If still sync-keep-editor-menus: Arthurian → FORCE Upgrade Sync Hosts From main.\n"
                   + "Arthurian menus kept: Bootstrap / Force Delete+Sync / Diagnose.\n"
                   + "Pew library rebuilt from Assets/Art/Spliced/Sprite (Pose_/Outfit_ wiped).\n"
                   + "If Architect still shows breakglass: Arthurian → Escape Breakglass → Tip Hosts.\n"
                   + "Stop Play → New Game. Bind: body=<spliced> outfit=NULL.\n"
                   + "Do NOT rebuild the scene.";
            if (!string.IsNullOrEmpty(modularReport))
                msg += "\n\n" + modularReport;
            msg += "\n" + DemonKnightPackageVersion.WipeStatusForDiagnose();
        }

        Debug.Log("[DemonKnight] " + msg);
        if (showDialog)
            EditorUtility.DisplayDialog(
                missingCritical.Count > 0 ? "DemonKnight Sync INCOMPLETE" : "DemonKnight Sync",
                msg, "OK");
    }

    /// <summary>Resolve tip SHA via GitHub API commits/main. Never returns null — HardTipCommitSha fallback.</summary>
    public static string ResolveMainSha()
    {
        try
        {
            string json = DownloadString("https://api.github.com/repos/" + Repo + "/commits/main");
            var m = Regex.Match(json, "^\\s*\\{\\s*\"sha\"\\s*:\\s*\"([0-9a-f]{40})\"", RegexOptions.Singleline);
            if (!m.Success)
                m = Regex.Match(json, "\"sha\"\\s*:\\s*\"([0-9a-f]{40})\"");
            if (m.Success)
            {
                Debug.Log("[DemonKnight] ResolveMainSha API tip=" + m.Groups[1].Value);
                return m.Groups[1].Value;
            }
            Debug.LogWarning("[DemonKnight] ResolveMainSha: no sha in API JSON — HardTipCommitSha");
        }
        catch (Exception ex)
        {
            Debug.LogWarning("[DemonKnight] ResolveMainSha API failed: " + ex.Message + " — HardTipCommitSha");
        }
        return HardTipCommitSha;
    }

    private static string DownloadPinned(string rel, string sha, out string source, bool relaxPackageId)
    {
        Exception last = null;

        // Prefer raw.githubusercontent.com first — jsDelivr often 404s for minutes after a
        // main push (DA-37: Force Sync 37/38 Empty or 404 on ArthurianSyncFromGitHub.cs).
        // NEVER unpinned jsDelivr @main — that CDN was days stale.
        for (int attempt = 0; attempt < 3; attempt++)
        {
            string bust = "t=" + DateTime.UtcNow.Ticks + "&r=" + Guid.NewGuid().ToString("N")
                          + "&a=" + attempt;
            // Prefer tip SHA + raw@main BEFORE FallbackCommitSha (old pin re-stuck sync-keep-editor-menus).
            string[] urls =
            {
                RawBase + sha + "/" + rel + "?" + bust,
                RawBase + sha + "/" + rel,
                RawBase + "main/" + rel + "?" + bust,
                RawBase + "main/" + rel,
                JsDelivrBase + sha + "/" + rel + "?" + bust,
                JsDelivrBase + sha + "/" + rel,
                JsDelivrBase + "main/" + rel + "?" + bust,
                RawBase + FallbackCommitSha + "/" + rel + "?" + bust,
                JsDelivrBase + FallbackCommitSha + "/" + rel + "?" + bust,
            };
            string[] labels =
            {
                "raw@" + sha + "+bust",
                "raw@" + sha,
                "raw@main+bust",
                "raw@main",
                "jsdelivr@" + sha + "+bust",
                "jsdelivr@" + sha,
                "jsdelivr@main+bust",
                "raw@" + FallbackCommitSha + "+bust",
                "jsdelivr@" + FallbackCommitSha + "+bust",
            };

            for (int i = 0; i < urls.Length; i++)
            {
                try
                {
                    string body = DownloadString(urls[i]);
                    if (LooksLikeHttpFailureBody(body))
                        throw new Exception("Empty or 404 (len=" + (body == null ? 0 : body.Length) + ")");
                    if (IsCritical(rel) && !PassesCriticalCheck(rel, body, relaxPackageId))
                        throw new Exception("stale body failed sanity check");
                    source = labels[i] + (attempt > 0 ? $"#retry{attempt}" : "");
                    return body;
                }
                catch (Exception ex)
                {
                    last = ex;
                }
            }
        }

        throw last ?? new Exception("download failed");
    }

    /// <summary>Re-download Sync / Diagnose menu hosts if they vanished mid-sync.</summary>
    private static void EnsureEditorMenuHostsPresent(
        string projectRoot,
        string sha,
        List<string> errors,
        List<string> okFiles)
    {
        foreach (string rel in NeverBulkDeleteEditorMenus)
        {
            string dest = Path.Combine(projectRoot, rel.Replace('/', Path.DirectorySeparatorChar));
            if (File.Exists(dest) && okFiles.Contains(rel)) continue;
            try
            {
                string text = DownloadPinned(rel, sha, out string source, relaxPackageId: true);
                WriteReplacing(dest, text, deleteFirst: false);
                VerifyOnDisk(rel, dest, relaxPackageId: true);
                if (!okFiles.Contains(rel)) okFiles.Add(rel);
                Debug.LogWarning($"[DemonKnight] Restored menu host {rel} via {source} @ {sha}");
            }
            catch (Exception ex)
            {
                errors.Add($"{rel} (menu restore): {ex.Message}");
                Debug.LogError($"[DemonKnight] Could not restore menu host {rel}: {ex.Message}");
            }
        }
    }

    static bool IsMustRestoreUiHelper(string rel)
    {
        if (string.IsNullOrEmpty(rel)) return false;
        foreach (string h in MustRestoreUiHelpers)
            if (string.Equals(h, rel, StringComparison.OrdinalIgnoreCase)) return true;
        return false;
    }

    /// <summary>
    /// Always (re)write PaperDollSeatView / LegendCodexUI embeds + helper stubs.
    /// Called before Force Delete and again after the main Sync pass.
    /// </summary>
    private static void EnsureUiHelpersPresent(
        string projectRoot,
        string sha,
        List<string> errors,
        List<string> okFiles)
    {
        foreach (string rel in MustRestoreUiHelpers)
        {
            string dest = Path.Combine(projectRoot, rel.Replace('/', Path.DirectorySeparatorChar));
            try
            {
                string text = DownloadPinned(rel, sha, out string source, relaxPackageId: true);
                // Always rewrite — Force Delete may have left a stale full standalone copy.
                WriteReplacing(dest, text, deleteFirst: true);
                VerifyOnDisk(rel, dest, relaxPackageId: true);
                if (!okFiles.Contains(rel)) okFiles.Add(rel);
                Debug.Log($"[DemonKnight] Ensured UI helper/embed {rel} via {source} @ {sha}");
                if (rel.EndsWith("GameDataTypes.cs", StringComparison.OrdinalIgnoreCase))
                {
                    StubPhase25StandalonesOver(projectRoot, errors);
                    StubApplierStandalonesOver(projectRoot, errors);
                }
            }
            catch (Exception ex)
            {
                errors.Add($"{rel} (ui helper ensure): {ex.Message}");
                Debug.LogError($"[DemonKnight] Could not ensure UI helper {rel}: {ex.Message}");
            }
        }
    }

    /// <summary>
    /// Last-resort: re-download GameDataTypes (+ rewrite Applier stubs) after every Force Sync.
    /// Independent of MustRestore array contents so a stale compiled Sync still cannot leave
    /// Battle/Merlin/Church call sites without SceneBackgroundApplier / ClassSpritePoolApplier.
    /// </summary>
    static void HardRestoreApplierEmbedHost(
        string projectRoot,
        string sha,
        List<string> errors,
        List<string> okFiles)
    {
        const string gdt = "Assets/Scripts/Data/GameDataTypes.cs";
        try
        {
            string dest = Path.Combine(projectRoot, gdt.Replace('/', Path.DirectorySeparatorChar));
            string text = DownloadPinned(gdt, sha, out string source, relaxPackageId: true);
            if (text.IndexOf("class SceneBackgroundApplier", StringComparison.Ordinal) < 0
                || text.IndexOf("class ClassSpritePoolApplier", StringComparison.Ordinal) < 0
                || text.IndexOf("class LegendPewPortraitApplier", StringComparison.Ordinal) < 0
                || text.IndexOf("class BattleGoodEvilSpritePoolApplier", StringComparison.Ordinal) < 0
                || text.IndexOf("class SlotTransformConfig", StringComparison.Ordinal) < 0
                || text.IndexOf("class StageParallaxLayer", StringComparison.Ordinal) < 0
                || text.IndexOf("enum SceneBackgroundSlot", StringComparison.Ordinal) < 0
                || text.IndexOf("class SceneBackgroundEntry", StringComparison.Ordinal) < 0
                || text.IndexOf("class BattleSpriteSlotEntry", StringComparison.Ordinal) < 0
                || text.IndexOf("class StageLayoutProfile", StringComparison.Ordinal) >= 0
                || text.IndexOf("class BattleGoodEvilSpritePoolProfile", StringComparison.Ordinal) >= 0
                || text.IndexOf("fix-gdt-profile-always-compile", StringComparison.Ordinal) < 0)
                throw new Exception("GameDataTypes Appliers/helpers/tip markers bad (" + source + ")");
            WriteReplacing(dest, text, deleteFirst: true);
            VerifyOnDisk(gdt, dest, relaxPackageId: true);
            if (!okFiles.Contains(gdt)) okFiles.Add(gdt);
            StubApplierStandalonesOver(projectRoot, errors);
            Debug.Log($"[DemonKnight] HardRestore Applier embed host via {source} @ {sha}");
        }
        catch (Exception ex)
        {
            errors.Add($"{gdt} (applier hard-restore): {ex.Message}");
            Debug.LogError($"[DemonKnight] HardRestore Applier embed FAILED: {ex.Message}");
        }
    }

    /// <summary>
    /// Last-resort: re-download named MonoScript Profile SOs after every Force Sync.
    /// Unity requires each SO class in Assets/Scripts/Data/&lt;ExactClassName&gt;.cs.
    /// Independent of MustRestore so a stale Sync cannot leave CS0246 / missing shared types.
    /// tip: fix-gdt-profile-always-compile
    /// </summary>
    static void HardRestoreNamedProfileScripts(
        string projectRoot,
        string sha,
        List<string> errors,
        List<string> okFiles)
    {
        string[] rels =
        {
            "Assets/Scripts/Data/StageLayoutProfile.cs",
            "Assets/Scripts/Data/SceneBackgroundProfile.cs",
            "Assets/Scripts/Data/ClassSpritePoolProfile.cs",
            "Assets/Scripts/Data/LegendPewPortraitProfile.cs",
            "Assets/Scripts/Data/BattleGoodEvilSpritePoolProfile.cs",
        };
        // SO class only in named files (helpers live in GameDataTypes — tip fix-gdt-profile-always-compile).
        string[][] markers =
        {
            new[] { "class StageLayoutProfile" },
            new[] { "class SceneBackgroundProfile" },
            new[] { "class ClassSpritePoolProfile" },
            new[] { "class LegendPewPortraitProfile" },
            new[] { "class BattleGoodEvilSpritePoolProfile" },
        };
        for (int i = 0; i < rels.Length; i++)
        {
            string rel = rels[i];
            try
            {
                string dest = Path.Combine(projectRoot, rel.Replace('/', Path.DirectorySeparatorChar));
                string text = DownloadPinned(rel, sha, out string source, relaxPackageId: true);
                foreach (string m in markers[i])
                {
                    if (text.IndexOf(m, StringComparison.Ordinal) < 0)
                        throw new Exception(rel + " missing " + m + " (" + source + ")");
                }
                if (text.IndexOf("fix-gdt-profile-always-compile", StringComparison.Ordinal) < 0)
                    throw new Exception(rel + " missing tip marker (" + source + ")");
                WriteReplacing(dest, text, deleteFirst: true);
                VerifyOnDisk(rel, dest, relaxPackageId: true);
                if (!okFiles.Contains(rel)) okFiles.Add(rel);
                Debug.Log($"[DemonKnight] HardRestore named Profile {rel} via {source} @ {sha}");
            }
            catch (Exception ex)
            {
                errors.Add($"{rel} (profile hard-restore): {ex.Message}");
                Debug.LogError($"[DemonKnight] HardRestore named Profile FAILED {rel}: {ex.Message}");
            }
        }
    }

    /// <summary>
    /// Last-resort: re-download SomersetChurchUI after every Force Sync.
    /// Independent of MustRestore so a stale compiled Sync that wiped Church UI still
    /// re-lands the type before GameLoopManager / ChurchStageUIController compile (CS0246).
    /// </summary>
    static void HardRestoreSomersetChurchUI(
        string projectRoot,
        string sha,
        List<string> errors,
        List<string> okFiles)
    {
        const string rel = "Assets/Scripts/UI/SomersetChurchUI.cs";
        try
        {
            string dest = Path.Combine(projectRoot, rel.Replace('/', Path.DirectorySeparatorChar));
            string text = DownloadPinned(rel, sha, out string source, relaxPackageId: true);
            if (text.IndexOf("class SomersetChurchUI", StringComparison.Ordinal) < 0
                || text.IndexOf("520f, 560f", StringComparison.Ordinal) < 0
                || text.IndexOf("TrainATK", StringComparison.Ordinal) < 0
                || text.IndexOf("TrainChosenStat", StringComparison.Ordinal) < 0
                || text.IndexOf("ChurchBottomDialogue", StringComparison.Ordinal) < 0)
                throw new Exception("SomersetChurchUI missing tip church markers (" + source + ")");
            WriteReplacing(dest, text, deleteFirst: true);
            VerifyOnDisk(rel, dest, relaxPackageId: true);
            if (!okFiles.Contains(rel)) okFiles.Add(rel);
            Debug.Log($"[DemonKnight] HardRestore SomersetChurchUI via {source} @ {sha}");
        }
        catch (Exception ex)
        {
            errors.Add($"{rel} (somerset-church hard-restore): {ex.Message}");
            Debug.LogError($"[DemonKnight] HardRestore SomersetChurchUI FAILED: {ex.Message}");
        }
    }

    /// <summary>
    /// Last-resort: re-download Architect embed host + BuildPersist stub after every Force Sync.
    /// Type lives in ArthurianArtArchitectWindow (MustRestore). Standalone is stub-only (CS0101).
    /// SoftPromote* reflection no-ops if type somehow missing. Independent of MustRestore arrays.
    /// </summary>
    static void HardRestoreArchitectBuildPersist(
        string projectRoot,
        string sha,
        List<string> errors,
        List<string> okFiles)
    {
        const string archRel = "Assets/Editor/ArthurianArtArchitectWindow.cs";
        const string stubRel = "Assets/Editor/ArthurianArchitectBuildPersist.cs";
        try
        {
            string archDest = Path.Combine(projectRoot, archRel.Replace('/', Path.DirectorySeparatorChar));
            string archBody = DownloadPinned(archRel, sha, out string source, relaxPackageId: true);
            if (archBody.IndexOf("class ArthurianArchitectBuildPersist", StringComparison.Ordinal) < 0
                || archBody.IndexOf("PromoteClassPoolIntoResources", StringComparison.Ordinal) < 0
                || archBody.IndexOf("SoftPromoteClassPoolIntoResources", StringComparison.Ordinal) < 0
                || archBody.IndexOf("IPreprocessBuildWithReport", StringComparison.Ordinal) < 0
                || archBody.IndexOf("PersistAllProfiles", StringComparison.Ordinal) < 0
                || (archBody.IndexOf("fix-gdt-profile-always-compile", StringComparison.Ordinal) < 0
                    && archBody.IndexOf("fix-pew-body-pool-empty", StringComparison.Ordinal) < 0
                    && archBody.IndexOf("fix-build-persist-embed", StringComparison.Ordinal) < 0))
                throw new Exception("Architect missing embedded BuildPersist (" + source + ")");
            WriteReplacing(archDest, archBody, deleteFirst: true);
            VerifyOnDisk(archRel, archDest, relaxPackageId: true);
            if (!okFiles.Contains(archRel)) okFiles.Add(archRel);

            // Stub standalone so leftover full copies cannot CS0101 against the embed.
            string stubDest = Path.Combine(projectRoot, stubRel.Replace('/', Path.DirectorySeparatorChar));
            string stubBody;
            try
            {
                stubBody = DownloadPinned(stubRel, sha, out string stubSource, relaxPackageId: true);
                if (stubBody.IndexOf("class ArthurianArchitectBuildPersist", StringComparison.Ordinal) >= 0)
                    throw new Exception("rejected full BuildPersist standalone from " + stubSource);
                if (stubBody.IndexOf("ArthurianArtArchitectWindow.cs", StringComparison.Ordinal) < 0)
                    throw new Exception("BuildPersist stub missing embed-host marker");
            }
            catch (Exception stubEx)
            {
                Debug.LogWarning("[DemonKnight] BuildPersist stub download fallback: " + stubEx.Message);
                stubBody =
                    "#if UNITY_EDITOR" + "\n"
                    + "// Types live in ArthurianArtArchitectWindow.cs (Force Sync embed — avoids CS0103 when this" + "\n"
                    + "// standalone path is wiped by Force Delete under a stale compiled Sync host)." + "\n"
                    + "// This stub overwrites any leftover full copy so we never get CS0101 duplicates." + "\n"
                    + "// tip: fix-gdt-profile-always-compile" + "\n"
                    + "#endif" + "\n";
            }
            WriteReplacing(stubDest, stubBody, deleteFirst: true);
            if (!okFiles.Contains(stubRel)) okFiles.Add(stubRel);
            Debug.Log($"[DemonKnight] HardRestore ArchitectBuildPersist embed+stub via {source} @ {sha}");
        }
        catch (Exception ex)
        {
            errors.Add($"{archRel} (architect-build-persist hard-restore): {ex.Message}");
            Debug.LogError($"[DemonKnight] HardRestore ArchitectBuildPersist FAILED: {ex.Message}");
        }
    }


    private static void WriteReplacing(string dest, string text, bool deleteFirst)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(dest)!);
        if (deleteFirst)
            TryDeleteFile(dest);

        string tmp = dest + ".dk-sync-tmp";
        TryDeleteFile(tmp);
        File.WriteAllText(tmp, text);
        if (File.Exists(dest))
        {
            try { File.Replace(tmp, dest, null); }
            catch
            {
                TryDeleteFile(dest);
                File.Move(tmp, dest);
            }
        }
        else File.Move(tmp, dest);
        TryDeleteFile(tmp);
    }

    private static void TryDeleteFile(string path)
    {
        if (!File.Exists(path)) return;
        try { File.SetAttributes(path, FileAttributes.Normal); } catch { /* ignore */ }
        try { File.Delete(path); }
        catch (Exception ex)
        {
            throw new Exception("could not delete old file (Unity lock?): " + ex.Message);
        }
    }

    private static void VerifyOnDisk(string rel, string dest, bool relaxPackageId = false)
    {
        if (!File.Exists(dest))
            throw new Exception("write vanished — file missing after sync");
        string disk = File.ReadAllText(dest);
        if (!PassesCriticalCheck(rel, disk, relaxPackageId))
            throw new Exception("on-disk file failed sanity check after write");
    }

    private static string DownloadString(string url)
    {
        using var wc = new WebClient();
        wc.CachePolicy = new RequestCachePolicy(RequestCacheLevel.BypassCache);
        wc.Headers.Add("User-Agent", "DemonKnight-Unity-Sync/" + ExpectedPackageId);
        wc.Headers.Add("Cache-Control", "no-cache, no-store");
        wc.Headers.Add("Pragma", "no-cache");
        // GitHub API needs Accept
        if (url.Contains("api.github.com"))
            wc.Headers.Add("Accept", "application/vnd.github.v3+json");
        return wc.DownloadString(url);
    }

    private static bool IsCritical(string rel)
    {
        foreach (string c in AtomicCritical)
        {
            if (rel.Equals(c, StringComparison.OrdinalIgnoreCase))
                return true;
        }
        return false;
    }

    private static void ValidateCritical(string rel, string body)
    {
        if (!IsCritical(rel)) return;
        if (!PassesCriticalCheck(rel, body, relaxPackageId: false))
            throw new Exception("sanity check failed — try Bootstrap CompatApi + Sync Tools");
    }

    private static bool PassesCriticalCheck(string rel, string body, bool relaxPackageId)
    {
        if (rel.EndsWith("GameLoopManager.cs", StringComparison.OrdinalIgnoreCase))
        {
            if (Regex.IsMatch(body, @"FindFirstObjectByType\s*<\s*SomersetChurchUI\s*>\s*\(\s*\)[\s\S]{0,80}?SetNight"))
                return false;
            return body.Contains("WaveVictoryGold")
                   && body.Contains("EnsureDisbandedAfterWave")
                   && body.Contains("AutosaveWarband")
                   && body.Contains("mustDisbandPartyAfterCurrentWave")
                   && body.Contains("AdvanceNightCounterAfterWaveWin")
                   && body.Contains("ContinueAfterWaveVictory")
                   && body.Contains("pendingMidRunWaveVictory")
                   && body.Contains("SyncNightToCombatWave")
                   && body.Contains("AwardWaveVictoryVeteranRanks")
                   && body.Contains("RedrawVeteranStarIcons")
                   && body.Contains("StartTownRecruitPhase")
                   && body.Contains("soulsLostThisRun")
                   && body.Contains("DiceManager")
                   && body.Contains("DA-59 EMBEDDED StageParallaxController")
                   && body.Contains("MicroShake")
                   && body.Contains("DkDeleteDuplicatePhase25Standalones");
        }

        if (rel.EndsWith("DemonKnightCompatApi.cs", StringComparison.OrdinalIgnoreCase))
        {
            bool core = body.Contains("ResolveLoop")
                        && body.Contains("AutosaveWarband")
                        && body.Contains("ChurchRewireMarch")
                        && body.Contains("WaveVictoryGold")
                        && body.Contains("RefreshTownAfterDisband")
                        && body.Contains("AwardWaveVictoryVeteranRanks");
            if (!core) return false;
            // Bootstrap may land an older CompatApi; Force Sync wants the soft HUD helper.
            if (relaxPackageId) return true;
            return body.Contains("RefreshWaveBossHud");
        }

        if (rel.EndsWith("VictoryStarBadge.cs", StringComparison.OrdinalIgnoreCase))
        {
            return body.Contains("class VictoryStarBadge")
                   && body.Contains("StarIcon")
                   && body.Contains("ChildName")
                   && body.Contains("sortingOrder = 520")
                   && body.Contains("anchoredPosition = new Vector2(-2f, -2f)");
        }

        if (rel.EndsWith("GameDataTypes.cs", StringComparison.OrdinalIgnoreCase))
        {
            // MenuListSortMode must live here so LegendCodexUI / SomersetChurchUI never CS0246.
            // Architect *Profile SOs are named MonoScript files (tip fix-gdt-profile-always-compile) — must NOT be redefined here.
            return body.Contains("enum MenuListSortMode")
                   && body.Contains("HighestHp")
                   && body.Contains("HighestAttack")
                   && body.Contains("LowestAttack")
                   && body.Contains("class RecruitInstance")
                   && body.Contains("veteranRank")
                   && body.Contains("battleSlotIndex")
                   && body.Contains("isDeployed")
                   && body.Contains("modularPoseId")
                   && body.Contains("EffectiveVeteranRank")
                   && body.Contains("class IntelGossip")
                   && body.Contains("class FlockEconomy")
                   && body.Contains("PewRowSize = 13")
                   && body.Contains("class SpriteAnimClip")
                   && body.Contains("class ModularSpriteLibrary")
                   && !body.Contains("class StageLayoutProfile")
                   && !body.Contains("class SceneBackgroundProfile")
                   && !body.Contains("class ClassSpritePoolProfile")
                   && !body.Contains("class LegendPewPortraitProfile")
                   && !body.Contains("class BattleGoodEvilSpritePoolProfile")
                   && body.Contains("class SlotTransformConfig")
                   && body.Contains("class StageParallaxLayer")
                   && body.Contains("enum SceneBackgroundSlot")
                   && body.Contains("class SceneBackgroundEntry")
                   && body.Contains("class BattleSpriteSlotEntry")
                   && body.Contains("class LegendPewPortraitApplier")
                   && body.Contains("GetLegendFace")
                   && body.Contains("PickPewProfile")
                   && body.Contains("ResolvePewFaceOverlay")
                   && body.Contains("IsFacePoolSprite")
                   && body.Contains("class BattleGoodEvilSpritePoolApplier")
                   && body.Contains("class SceneBackgroundApplier")
                   && body.Contains("ApplyToUiHost")
                   && body.Contains("class ClassSpritePoolApplier")
                   && body.Contains("PickForRecruit")
                   && body.Contains("force-sync-appliers-hardfix EMBEDDED Appliers")
                   && body.Contains("fix-gdt-profile-always-compile");
        }

        if (rel.EndsWith("StageLayoutProfile.cs", StringComparison.OrdinalIgnoreCase))
        {
            // SO-only file — helpers live in GameDataTypes (tip fix-gdt-profile-always-compile).
            return body.Contains("class StageLayoutProfile")
                   && !body.Contains("class SlotTransformConfig")
                   && !body.Contains("class StageParallaxLayer")
                   && body.Contains("ChurchPewCount = 52")
                   && body.Contains("previewOrOverrideSprite")
                   && body.Contains("RematerializeSpritesFromPaths")
                   && body.Contains("fix-gdt-profile-always-compile");
        }
        if (rel.EndsWith("SceneBackgroundProfile.cs", StringComparison.OrdinalIgnoreCase))
        {
            return body.Contains("class SceneBackgroundProfile")
                   && !body.Contains("enum SceneBackgroundSlot")
                   && !body.Contains("class SceneBackgroundEntry")
                   && body.Contains("RematerializeSpritesFromPaths")
                   && body.Contains("fix-gdt-profile-always-compile");
        }
        if (rel.EndsWith("ClassSpritePoolProfile.cs", StringComparison.OrdinalIgnoreCase))
        {
            return body.Contains("class ClassSpritePoolProfile")
                   && body.Contains("peasantSprites")
                   && body.Contains("TryLoadSpriteAtPath")
                   && body.Contains("fix-gdt-profile-always-compile");
        }
        if (rel.EndsWith("LegendPewPortraitProfile.cs", StringComparison.OrdinalIgnoreCase))
        {
            return body.Contains("class LegendPewPortraitProfile")
                   && body.Contains("CanonicalLegendSlots")
                   && body.Contains("RematerializeSpritesFromPaths")
                   && body.Contains("fix-gdt-profile-always-compile");
        }
        if (rel.EndsWith("BattleGoodEvilSpritePoolProfile.cs", StringComparison.OrdinalIgnoreCase))
        {
            return body.Contains("class BattleGoodEvilSpritePoolProfile")
                   && !body.Contains("class BattleSpriteSlotEntry")
                   && body.Contains("CanonicalBossSlots")
                   && body.Contains("RematerializeSpritesFromPaths")
                   && body.Contains("fix-gdt-profile-always-compile");
        }

        if (rel.EndsWith("SceneBackgroundApplier.cs", StringComparison.OrdinalIgnoreCase))
        {
            // Stub only — real class is embedded in GameDataTypes.cs.
            return body.Contains("GameDataTypes.cs") && !body.Contains("class SceneBackgroundApplier");
        }

        if (rel.EndsWith("ClassSpritePoolApplier.cs", StringComparison.OrdinalIgnoreCase))
        {
            // Stub only — real class is embedded in GameDataTypes.cs.
            return body.Contains("GameDataTypes.cs") && !body.Contains("class ClassSpritePoolApplier");
        }

        // Remaining Phase 2–5 standalones are NOT synced (still embedded elsewhere).
        // StageLayoutProfile.cs (+ sibling *Profile.cs) ARE synced — named MonoScripts (tip fix-gdt-profile-always-compile).
        if (rel.EndsWith("SpriteAnimClip.cs", StringComparison.OrdinalIgnoreCase)
            || rel.EndsWith("ModularSpriteLibrary.cs", StringComparison.OrdinalIgnoreCase)
            || rel.EndsWith("ModularSpriteAnimator.cs", StringComparison.OrdinalIgnoreCase)
            || rel.EndsWith("StageParallaxController.cs", StringComparison.OrdinalIgnoreCase)
            || rel.EndsWith("DiceManager.cs", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (rel.EndsWith("ArthurianArchitectBuildPersist.cs", StringComparison.OrdinalIgnoreCase))
        {
            // Stub only — real class is embedded in ArthurianArtArchitectWindow.cs.
            return body.Contains("ArthurianArtArchitectWindow.cs")
                   && !body.Contains("class ArthurianArchitectBuildPersist")
                   && (body.Contains("fix-gdt-profile-always-compile")
                       || body.Contains("fix-class-pool-kind-cs0019")
                       || body.Contains("fix-pew-body-pool-empty")
                       || body.Contains("fix-build-persist-embed"));
        }

        if (rel.EndsWith("ArthurianArtArchitectWindow.cs", StringComparison.OrdinalIgnoreCase))
            return IsTipArchitectBody(body);

        if (rel.EndsWith("SomersetChurchUI.cs", StringComparison.OrdinalIgnoreCase))
        {
            // Tip church-train-scout-ux+: dossier 520×560 + Train ATK/HP/SPD (NOT old 420×460).
            // Stale sanity "420f, 460f" rejected tip downloads → Force Wipe left CS0246.
            return body.Contains("class SomersetChurchUI")
                   && body.Contains("RewireMarchButton")
                   && body.Contains("ForceNamedTavernDicePanelsInactive")
                   && body.Contains("forceEqualHudSlots")
                   && body.Contains("MenuListSortBar")
                   && body.Contains("ShowHoverPreview")
                   && body.Contains("ToggleUnitSelectionMenu")
                   && body.Contains("textWrappingMode")
                   && !body.Contains("enableWordWrapping")
                   && body.Contains("CompactUnitStatsPanel")
                   && body.Contains("PositionUnitStatsNearSelected")
                   && body.Contains("ChurchBottomDialogue")
                   && body.Contains("LockSeatHighlight")
                   && body.Contains("UnityEngine.Object.Destroy")
                   && body.Contains("using System;")
                   && body.Contains("520f, 560f")
                   && body.Contains("TrainATK")
                   && body.Contains("TrainChosenStat")
                   && body.Contains("onLeftClick: () => ToggleUnitSelectionMenu")
                   && body.Contains("CloseUnitSelectionMenu")
                   && body.Contains("ApplyRecruitButtonLabel")
                   && body.Contains("Recruit {hireCost}g ({purse}g)")
                   && body.Contains("58f, 8f")
                   && body.Contains("GetMouseButtonDown(1)")
                   && body.Contains("LegendPewPortraitApplier.ResolvePewVisual")
                   && body.Contains("ApplyFaceOverlay")
                   && body.Contains("ResolvePewFaceOverlay");
        }

        if (rel.EndsWith("ChurchStageUIController.cs", StringComparison.OrdinalIgnoreCase))
        {
            return Regex.IsMatch(body, @"class\s+ChurchStageUIController\s*:\s*SomersetChurchUI")
                   && !Regex.IsMatch(body, @"void\s+SetNight\s*\(\s*int");
        }

        if (rel.EndsWith("AutoBattlerEngine.cs", StringComparison.OrdinalIgnoreCase))
        {
            // Must reference BattleBossDialogUI (CS0103 if that file is missing from Sync).
            return body.Contains("DemonKnightCompatApi")
                   && body.Contains("BattleBossDialogUI")
                   && body.Contains("waveBoss") // CS0136 fix: no shadowed local `boss`
                   && body.Contains("TriggerWaveVictoryScreen")
                   && body.Contains("endingBattle")
                   && body.Contains("EndBattleBody")
                   && body.Contains("AwardWaveVictoryVeteranRanks")
                   && body.Contains("RefreshTownAfterDisband")
                   && body.Contains("StageLayoutProfile")
                   && body.Contains("FlockEconomy")
                   && body.Contains("ApplyLinePierceSplash");
        }

        if (rel.EndsWith("CardFusionSystem.cs", StringComparison.OrdinalIgnoreCase))
        {
            return body.Contains("CanFuseLegacy")
                   && body.Contains("CountType(p, UnitType.Peasant) >= 2")
                   && body.Contains("public static void SanitizeBaseUnitFusionFlags")
                   && !body.Contains("CharacterDatabase.SanitizeBaseUnitFusionFlags")
                   && !body.Contains("!HasType(p, UnitType.Priest)")
                   && !body.Contains("!HasType(p, UnitType.Merlin)");
        }

        if (rel.EndsWith("MerlinResurrectionManager.cs", StringComparison.OrdinalIgnoreCase))
        {
            return body.Contains("DemonKnightCompatApi")
                   && body.Contains("TriggerWaveVictoryScreen")
                   && body.Contains("ContinueAfterWaveVictory")
                   && body.Contains("pendingMidRunWaveVictory")
                   && body.Contains("Next Night")
                   && body.Contains("PromoteVictoryCanvas")
                   && body.Contains("StyleVictoryActionButton")
                   && body.Contains("CullDuplicateEventSystems")
                   && body.Contains("BeginVictoryShow")
                   && body.Contains("BindButtonOnce")
                   && body.Contains("ForceVictoryNextNightVisible")
                   && body.Contains("GetVictoryButtonHost")
                   && !body.Contains("nextRunButtonWired")
                   && !body.Contains("endRunButtonWired");
        }

        if (rel.EndsWith("PewSeatHoverHighlight.cs", StringComparison.OrdinalIgnoreCase))
        {
            // Stub only — real class is embedded in PaperDollSeatView.cs.
            return body.Contains("PaperDollSeatView.cs") && !body.Contains("class PewSeatHoverHighlight");
        }

        if (rel.EndsWith("MenuListSortBar.cs", StringComparison.OrdinalIgnoreCase))
        {
            // Stub only — real class is embedded in LegendCodexUI.cs.
            return body.Contains("LegendCodexUI.cs") && !body.Contains("class MenuListSortBar");
        }

        if (rel.EndsWith("PaperDollSeatView.cs", StringComparison.OrdinalIgnoreCase))
        {
            return body.Contains("class PaperDollSeatView")
                   && body.Contains("class PewSeatHoverHighlight")
                   && body.Contains("IPointerEnterHandler")
                   && body.Contains("onHoverInfo")
                   && body.Contains("onRightClick")
                   && body.Contains("IPointerClickHandler")
                   && body.Contains("onClick?.Invoke(recruit)")
                   && body.Contains("SetMenuLocked")
                   && body.Contains("menuLocked")
                   && body.Contains("VictoryStarBadge.Apply")
                   && body.Contains("IsProtectedSeatChrome")
                   && body.Contains("ModularSpriteAnimator")
                   && body.Contains("DA-59 EMBEDDED ModularSpriteAnimator")
                   && body.Contains("ConfigurePaperDoll")
                   && body.Contains("PlayRecruitReveal")
                   && body.Contains("UnityEngine.Random.Range")
                   && !Regex.IsMatch(body, @"(?<!UnityEngine\.)Random\.(Range|insideUnitCircle)");
        }

        if (rel.EndsWith("TavernDiceMinigame.cs", StringComparison.OrdinalIgnoreCase))
        {
            return body.Contains("class TavernDiceMinigame")
                   && body.Contains("DA-59 EMBEDDED DiceManager")
                   && body.Contains("GrantTownPhaseFreeRoll");
        }

        if (rel.EndsWith("ArthurianStageLayoutProfileEnsure.cs", StringComparison.OrdinalIgnoreCase))
        {
            return body.Contains("class ArthurianStageLayoutProfileEnsure")
                   && body.Contains("StageLayoutProfile.asset");
        }

        if (rel.EndsWith("UnitVisualActor.cs", StringComparison.OrdinalIgnoreCase))
        {
            return body.Contains("class UnitVisualActor")
                   && body.Contains("ModularSpriteAnimator");
        }

        if (rel.EndsWith("TownSplitUIController.cs", StringComparison.OrdinalIgnoreCase))
        {
            return body.Contains("DemonKnightCompatApi")
                   && body.Contains("PewSeatHoverHighlight")
                   && body.Contains("DiceManager")
                   && body.Contains("Free Stakes Roll");
        }

        if (rel.EndsWith("LegendCodexUI.cs", StringComparison.OrdinalIgnoreCase))
        {
            return body.Contains("DemonKnightCompatApi")
                   && body.Contains("class LegendCodexUI")
                   && body.Contains("class MenuListSortBar")
                   && body.Contains("SortedParty")
                   && body.Contains("0.905f")
                   && body.Contains("High Attack")
                   && body.Contains("Low Attack")
                   && body.Contains("Low Speed")
                   && body.Contains("High Speed")
                   && body.Contains("sortAttack")
                   && body.Contains("ChipsMatchRoot")
                   && body.Contains("FacePortrait")
                   && body.Contains("LegendPewPortraitApplier.GetLegendFace")
                   && !body.Contains("(MenuListSortMode.Default, \"Default\")");
        }

        if (rel.EndsWith("CharacterDatabase.cs", StringComparison.OrdinalIgnoreCase))
        {
            // Priest/legend fuse: ingredients-only — no HasUnitType one-product lock.
            return body.Contains("TryPickIngredients")
                   && body.Contains("CreateLegendRecruit")
                   && body.Contains("LogFuseBlocked")
                   && body.Contains("CardFusionSystem.SanitizeBaseUnitFusionFlags")
                   && body.Contains("LegendPewPortraitApplier.GetLegendFace")
                   && !Regex.IsMatch(
                       body,
                       @"CanFuse\s*\([^)]*\)[\s\S]{0,200}?HasUnitType\s*\(\s*party\s*,\s*def\.unitType\s*\)");
        }

        if (rel.EndsWith("MainMenuUI.cs", StringComparison.OrdinalIgnoreCase)
            || rel.EndsWith("PerkDraftManager.cs", StringComparison.OrdinalIgnoreCase))
        {
            return body.Contains("DemonKnightCompatApi");
        }

        if (rel.EndsWith("BattlefieldVisualizer.cs", StringComparison.OrdinalIgnoreCase))
        {
            return body.Contains("ResolveArenaBindings")
                   && body.Contains("ApplyBackgroundArt")
                   && body.Contains("ApplyVideoBackground")
                   && body.Contains("StageLayoutProfile")
                   && body.Contains("BattleGoodEvilSpritePoolApplier.ResolveBattleSprite");
        }

        if (rel.EndsWith("BattleBossDialogUI.cs", StringComparison.OrdinalIgnoreCase))
        {
            return body.Contains("ShowForBoss") && body.Contains("IsBlockingCombat");
        }

        if (rel.EndsWith("WaveBossHudView.cs", StringComparison.OrdinalIgnoreCase))
        {
            return body.Contains("EnsureNearNightText") && body.Contains("class WaveBossHudView");
        }

        if (rel.EndsWith("DemonKnightPackageVersion.cs", StringComparison.OrdinalIgnoreCase))
        {
            // Accept current OR previous good ids so old Sync / bootstrap can land the file.
            if (relaxPackageId)
                return body.Contains("public const string Id");
            return body.Contains(ExpectedPackageId)
                   || body.Contains("fix-cs0117-sanitize-on-fusion")
                   || body.Contains("victory-nextnight-multifuse-priest")
                   || body.Contains("rmb-anywhere-sort-chip-order")
                   || body.Contains("rmb-close-sort-recruit-purse")
                   || body.Contains("rmb-deselect-legends-atk-sort")
                   || body.Contains("unit-stats-lmb-taller-panel")
                   || body.Contains("fix-cs0104-object-ambiguous")
                   || body.Contains("fix-cs0103-cs0414-wire-flags")
                   || body.Contains("unit-stats-ur-bottom-dialog")
                   || body.Contains("dedupe-victory-wire-eventsystem")
                   || body.Contains("victory-next-night-visible")
                   || body.Contains("fix-bind-action-textwrapping")
                   || body.Contains("sync-skip-self-hover-rmb")
                   || body.Contains("sortbar-raise-hover-instant")
                   || body.Contains("embed-hover-sortbar-cs0103")
                   || body.Contains("fix-hover-sortbar-sync-cs0103")
                   || body.Contains("fix-menulistsortmode-cs0246")
                   || body.Contains("legends-warband-sort-hp-spd")
                   || body.Contains("priest-multi-fuse-victory-hover")
                   || body.Contains("wave-win-night-priest-fuse")
                   || body.Contains("next-night-no-legacy")
                   || body.Contains("sync-host-da37")
                   || body.Contains("train-random-plus2")
                   || body.Contains("hide-perks-runes-ui")
                   || body.Contains("legends-columns-wider")
                   || body.Contains("force-sync-main-landed")
                   || body.Contains("fix-wavebosshud-cs0103")
                   || body.Contains("warrior-no-train-ranged-night-seq")
                   || body.Contains("pew-generate-appearance-if-null")
                   || body.Contains("art-tools-runtime-deps")
                   || body.Contains("art-tools-install-menu")
                   || body.Contains("art-tools-under-arthurian")
                   || body.Contains("end-run-remove-perks-runes")
                   || body.Contains("character-registry-battle-sprites")
                   || body.Contains("modular-townsfolk-appearance")
                   || body.Contains("fix-night-double-advance")
                   || body.Contains("fix-fusion-serialize-flat-autosave")
                   || body.Contains("fix-fusion-serialize-intel-costs")
                   || body.Contains("fix-boss-dialog-cs0136")
                   || body.Contains("balance-v2-arthurian-expansion")
                   || body.Contains("autobattle-boss-dialog-video-bg")
                   || body.Contains("back-row-melee-no-attack")
                   || body.Contains("perk-rune-bonus-math")
                   || body.Contains("autobattle-frontline-targeting")
                   || body.Contains("warband-formation-diagram")
                   || body.Contains("new-game-wipe-runes-perks")
                   || body.Contains("peasant-hire-floor-10")
                   || body.Contains("new-game-gold-200")
                   || body.Contains("tavern-discount-wave-hud")
                   || body.Contains("stars-visible-hire-bands-gold200")
                   || body.Contains("sync-keep-editor-menus")
                   || body.Contains("victory-stars-hire")
                   || body.Contains("mediate-mordred-wave-rules")
                   || body.Contains("hire-once-unfuse-disband")
                   || body.Contains("hud-esc-legends-warband-pace")
                   || body.Contains("sync-pin-jsdelivr")
                   || body.Contains("sync-complete-replace")
                   || body.Contains("class-sprite-pools")
                   || body.Contains("fix-scene-bg-applier-cs0103")
                   || body.Contains("force-sync-appliers-hardfix")
                   || body.Contains("sheet-splicer-chroma-despill")
                   || body.Contains("fix-hud-wave-match")
                   || body.Contains("merlin-lore-narrative")
                   || body.Contains("lore-boss-wave-order")
                   || body.Contains("legends-lore-portrait")
                   || body.Contains("architect-legend-pew-portraits")
                   || body.Contains("sprite-outline-highlight")
                   || body.Contains("fix-architect-pool-apply")
                   || body.Contains("church-train-scout-ux")
                   || body.Contains("body-face-pool-build-persist")
                   || body.Contains("battle-good-evil-sprite-pools")
                   || body.Contains("fix-architect-build-persist-cs0103")
                   || body.Contains("fix-build-persist-embed")
                   || body.Contains("fix-pew-body-pool-empty")
                   || body.Contains("fix-class-pool-kind-cs0019")
                   || body.Contains("fix-profile-so-script-assets");
        }

        if (rel.EndsWith("ArthurianSyncFromGitHub.cs", StringComparison.OrdinalIgnoreCase))
        {
            if (relaxPackageId)
                return body.Contains("DownloadPinned") || body.Contains("BootstrapCompatAndSyncTools")
                       || body.Contains("ForceDeleteSyncAll") || body.Contains("ExpectedPackageId");
            // DA-37: always accept a Sync host that still has the Force Sync menus.
            // Package-id match is best-effort — tip Sync may be newer than local ExpectedPackageId.
            return body.Contains("DownloadPinned")
                   && body.Contains("NeverBulkDeleteEditorMenus")
                   && body.Contains("ForceDeleteSyncAll")
                   && body.Contains("BootstrapCompatAndSyncTools")
                   && body.Contains("ForceOverwriteSyncHostsFromMain")
                   && body.Contains("DownloadRawMainOnly")
                   && body.Contains("SyncHostForceOverwritePaths");
        }

        if (rel.EndsWith("ArthurianMenuBootstrap.cs", StringComparison.OrdinalIgnoreCase))
        {
            return body.Contains("Diagnose Project")
                   && body.Contains("DemonKnightPackageVersion")
                   && body.Contains("ForceUpgradeSyncHostsFromMain")
                   && body.Contains("Package Id on disk");
        }

        return true;
    }

    /// <summary>Tip Architect = single Sprite category, no paperdoll tab/generator.</summary>
    public static bool IsTipArchitectBody(string body)
    {
        if (string.IsNullOrEmpty(body)) return false;
        if (!body.Contains("class ArthurianArtArchitectWindow")) return false;
        if (!body.Contains("Art & Stage Architect")) return false;
        if (!body.Contains("SpriteCategory")) return false;
        // Tip: 6 tabs including Battle Good/Evil sprite pools.
        if (!body.Contains("enum ArchitectTab { Splicer, StagePlacer, SceneBackgrounds, ClassSprites, LegendPewPortraits, BattleGoodEvil }"))
            return false;
        if (!body.Contains("Sheet Splicer")) return false;
        if (!body.Contains("Manual Box Crop")) return false;
        if (!body.Contains("Save Splices to Folder")) return false;
        if (!body.Contains("Scene Backgrounds")) return false;
        if (!body.Contains("SceneBackgroundProfile")) return false;
        if (!body.Contains("Class Sprites")) return false;
        if (!body.Contains("ClassSpritePoolProfile")) return false;
        if (!body.Contains("DrawClassSpritesTab")) return false;
        if (!body.Contains("Legend / Pew Portraits")) return false;
        if (!body.Contains("LegendPewPortraitProfile")) return false;
        if (!body.Contains("DrawLegendPewPortraitsTab")) return false;
        if (!body.Contains("Battle Good / Evil")) return false;
        if (!body.Contains("BattleGoodEvilSpritePoolProfile")) return false;
        if (!body.Contains("DrawBattleGoodEvilTab")) return false;
        if (!body.Contains("TryAddBodyPoolSprite")) return false;
        if (!body.Contains("TryAddFacePoolSprite")) return false;
        if (!body.Contains("TryAddBattlePoolSprite")) return false;
        if (!body.Contains("fix-gdt-profile-always-compile")
            && !body.Contains("fix-profile-so-script-assets")
            && !body.Contains("fix-class-pool-kind-cs0019")
            && !body.Contains("fix-pew-body-pool-empty")
            && !body.Contains("fix-build-persist-embed")) return false;
        // CS0019 belt: ClassPoolKind must map to UnitType (never compare enums directly).
        if (body.Contains("selectedClassPool == UnitType.")) return false;
        if (!body.Contains("ClassPoolKind.Warrior => UnitType.Warrior")) return false;
        if (!body.Contains("UiBuildId")) return false;
        if (!body.Contains("PersistAllProfiles")) return false;
        // Embed + SoftPromote belt (CS0103 after Force Sync wiping standalone BuildPersist).
        if (!body.Contains("class ArthurianArchitectBuildPersist")) return false;
        if (!body.Contains("SoftPromoteClassPoolIntoResources")) return false;
        if (!body.Contains("IPreprocessBuildWithReport")) return false;
        // Reject stale paperdoll multi-category bodies (this was blocking tip Sync).
        if (body.Contains("Paper-Doll Unit Generator")) return false;
        if (body.Contains("enum SheetCategory")) return false;
        if (body.Contains("Save as CharacterDefinition")) return false;
        if (body.Contains("DrawPaperDollTab")) return false;
        return true;
    }

    /// <summary>Delete nested/duplicate ArthurianArtArchitectWindow.cs under Assets (zip extracts).</summary>
    public static int NukeNestedArchitectCopies(string projectRoot = null)
    {
        projectRoot ??= Directory.GetParent(Application.dataPath)!.FullName;
        string canonical = Path.Combine(projectRoot, "Assets", "Editor", "ArthurianArtArchitectWindow.cs");
        int n = 0;
        try
        {
            string assets = Path.Combine(projectRoot, "Assets");
            if (!Directory.Exists(assets)) return 0;
            foreach (string hit in Directory.GetFiles(assets, "ArthurianArtArchitectWindow.cs", SearchOption.AllDirectories))
            {
                if (string.Equals(hit, canonical, StringComparison.OrdinalIgnoreCase)) continue;
                try
                {
                    string rel = "Assets" + hit.Substring(assets.Length).Replace('\\', '/');
                    Debug.LogWarning("[DemonKnight] Removing nested/stale Architect copy: " + rel);
                    if (!AssetDatabase.DeleteAsset(rel))
                    {
                        File.SetAttributes(hit, FileAttributes.Normal);
                        File.Delete(hit);
                        if (File.Exists(hit + ".meta")) File.Delete(hit + ".meta");
                    }
                    n++;
                }
                catch (Exception ex)
                {
                    Debug.LogWarning("[DemonKnight] Could not delete nested Architect: " + ex.Message);
                }
            }
        }
        catch (Exception ex)
        {
            Debug.LogWarning("[DemonKnight] NukeNestedArchitectCopies: " + ex.Message);
        }
        return n;
    }

    /// <summary>
    /// Real CDN/raw failures are tiny HTML/text. Large Editor hosts contain the literal
    /// string "404: Not Found" in their OWN source — body.Contains that string is a false positive
    /// that blocked FORCE Upgrade of SyncFromGitHub + MenuBootstrap forever.
    /// </summary>
    public static bool LooksLikeHttpFailureBody(string body)
    {
        if (string.IsNullOrEmpty(body)) return true;
        string t = body.TrimStart();
        if (t.StartsWith("<!DOCTYPE", StringComparison.OrdinalIgnoreCase)) return true;
        if (t.StartsWith("<html", StringComparison.OrdinalIgnoreCase)) return true;
        // GitHub raw 404 bodies are a few dozen bytes ("404: Not Found").
        if (body.Length < 512)
        {
            if (t.StartsWith("404", StringComparison.Ordinal)) return true;
            if (t.IndexOf("404: Not Found", StringComparison.OrdinalIgnoreCase) >= 0) return true;
            if (t.IndexOf("Failed to fetch", StringComparison.OrdinalIgnoreCase) >= 0) return true;
            if (t.IndexOf("Package size exceeded", StringComparison.OrdinalIgnoreCase) >= 0) return true;
            if (t.IndexOf("Not Found", StringComparison.OrdinalIgnoreCase) >= 0) return true;
        }
        return false;
    }

    /// <summary>
    /// Delete every nested/stale ArtArchitect EditorWindow and PaperDoll *editor* window
    /// under Assets (keep runtime PaperDollSeatView.cs). Canonical Architect is rewritten after.
    /// </summary>
    public static int NukeStaleArchitectAndPaperDollEditors(string projectRoot = null)
    {
        projectRoot ??= Directory.GetParent(Application.dataPath)!.FullName;
        string canonical = Path.Combine(projectRoot, "Assets", "Editor", "ArthurianArtArchitectWindow.cs");
        int n = NukeNestedArchitectCopies(projectRoot);
        try
        {
            string assets = Path.Combine(projectRoot, "Assets");
            if (!Directory.Exists(assets)) return n;
            var patterns = new[] { "*ArtArchitect*.cs", "*ArchitectWindow*.cs", "*PaperDoll*Window*.cs", "*PaperDoll*Editor*.cs" };
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (string pat in patterns)
            {
                foreach (string hit in Directory.GetFiles(assets, pat, SearchOption.AllDirectories))
                {
                    if (!seen.Add(hit)) continue;
                    // Never delete the canonical tip Architect path here — caller rewrite handles it.
                    if (string.Equals(hit, canonical, StringComparison.OrdinalIgnoreCase)) continue;
                    // Never delete runtime pew paperdoll.
                    string name = Path.GetFileName(hit);
                    if (name.Equals("PaperDollSeatView.cs", StringComparison.OrdinalIgnoreCase)) continue;
                    try
                    {
                        string rel = "Assets" + hit.Substring(assets.Length).Replace('\\', '/');
                        Debug.LogWarning("[DemonKnight] Nuking stale Architect/PaperDoll editor: " + rel);
                        if (!AssetDatabase.DeleteAsset(rel))
                        {
                            File.SetAttributes(hit, FileAttributes.Normal);
                            File.Delete(hit);
                            if (File.Exists(hit + ".meta")) File.Delete(hit + ".meta");
                        }
                        n++;
                    }
                    catch (Exception ex)
                    {
                        Debug.LogWarning("[DemonKnight] Nuke editor failed: " + ex.Message);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Debug.LogWarning("[DemonKnight] NukeStaleArchitectAndPaperDollEditors: " + ex.Message);
        }
        return n;
    }

    /// <summary>Disk proof for Diagnose — SpriteCategory / UiBuildId / PaperDoll remnant.</summary>
    public static string ArchitectTipMarkersReport()
    {
        try
        {
            string path = Path.Combine(
                Directory.GetParent(Application.dataPath)!.FullName,
                "Assets", "Editor", "ArthurianArtArchitectWindow.cs");
            if (!File.Exists(path))
                return "Architect tip markers: SpriteCategory=NO UiBuildId=NO PaperDoll remnant=YES (file MISSING)";
            string body = File.ReadAllText(path);
            bool sprite = body.Contains("SpriteCategory")
                          && body.Contains("enum ArchitectTab { Splicer, StagePlacer, SceneBackgrounds, ClassSprites, LegendPewPortraits, BattleGoodEvil }");
            bool ui = body.Contains("UiBuildId");
            bool remnant = body.Contains("Paper-Doll Unit Generator")
                           || body.Contains("enum SheetCategory")
                           || body.Contains("DrawPaperDollTab")
                           || body.Contains("Save as CharacterDefinition");
            return $"Architect tip markers: SpriteCategory={(sprite ? "YES" : "NO")} UiBuildId={(ui ? "YES" : "NO")} PaperDoll remnant={(remnant ? "YES" : "NO")}";
        }
        catch (Exception ex)
        {
            return "Architect tip markers: ERROR " + ex.Message;
        }
    }

    /// <summary>Close any open Architect windows so the next Open() uses the tip type.</summary>
    public static void CloseOpenArchitectWindows()
    {
        try
        {
            var windows = Resources.FindObjectsOfTypeAll<EditorWindow>();
            foreach (var w in windows)
            {
                if (w == null) continue;
                string tn = w.GetType().Name;
                if (tn == "ArthurianArtArchitectWindow" || (w.titleContent != null
                    && w.titleContent.text != null
                    && w.titleContent.text.IndexOf("Art & Stage Architect", StringComparison.OrdinalIgnoreCase) >= 0))
                {
                    try { w.Close(); } catch { /* ignore */ }
                }
            }
        }
        catch (Exception ex)
        {
            Debug.LogWarning("[DemonKnight] CloseOpenArchitectWindows: " + ex.Message);
        }
    }

    /// <summary>
    /// Nuclear CS0246 belt: HardWrite all five named Profile MonoScripts from embeds
    /// BEFORE Force Delete / download. Offline — no GitHub required. tip: fix-gdt-profile-always-compile
    /// </summary>
    public static int HardWriteNamedProfilesFromEmbeds(string root = null, System.Text.StringBuilder log = null)
    {
        if (string.IsNullOrEmpty(root))
            root = Directory.GetParent(Application.dataPath)!.FullName;
        int wrote = 0;
        foreach (var (rel, b64) in NamedProfileEmbeds.All)
        {
            try
            {
                string dest = Path.Combine(root, rel.Replace('/', Path.DirectorySeparatorChar));
                bool need = true;
                if (File.Exists(dest))
                {
                    string existing = File.ReadAllText(dest);
                    string className = Path.GetFileNameWithoutExtension(rel);
                    need = existing.Length < 200
                           || existing.IndexOf(NamedProfileEmbeds.TipId, StringComparison.Ordinal) < 0
                           || existing.IndexOf("class " + className, StringComparison.Ordinal) < 0;
                    // Helpers moved to GameDataTypes — Profile files must NOT redefine them (CS0101).
                    if (!need && rel.EndsWith("StageLayoutProfile.cs", StringComparison.OrdinalIgnoreCase))
                        need = existing.IndexOf("class SlotTransformConfig", StringComparison.Ordinal) >= 0
                               || existing.IndexOf("class StageParallaxLayer", StringComparison.Ordinal) >= 0;
                    if (!need && rel.EndsWith("SceneBackgroundProfile.cs", StringComparison.OrdinalIgnoreCase))
                        need = existing.IndexOf("enum SceneBackgroundSlot", StringComparison.Ordinal) >= 0
                               || existing.IndexOf("class SceneBackgroundEntry", StringComparison.Ordinal) >= 0;
                    if (!need && rel.EndsWith("BattleGoodEvilSpritePoolProfile.cs", StringComparison.OrdinalIgnoreCase))
                        need = existing.IndexOf("class BattleSpriteSlotEntry", StringComparison.Ordinal) >= 0;
                }
                if (!need) continue;

                string body = NamedProfileEmbeds.Decode(b64);
                if (string.IsNullOrEmpty(body) || body.Length < 200)
                {
                    log?.AppendLine("MISS embed " + rel);
                    Debug.LogWarning("[DemonKnight] Sync HardWrite miss " + rel);
                    continue;
                }
                if (body.IndexOf(NamedProfileEmbeds.TipId, StringComparison.Ordinal) < 0)
                    body = "// tip: " + NamedProfileEmbeds.TipId + "
" + body;

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
                string line = $"HardWriteEmbed {rel} ({body.Length} B)";
                log?.AppendLine(line);
                Debug.Log("[DemonKnight] Sync " + line);
            }
            catch (System.Exception ex)
            {
                log?.AppendLine("HardWriteEmbed FAIL " + rel + ": " + ex.Message);
                Debug.LogWarning("[DemonKnight] Sync HardWriteEmbed FAIL " + rel + ": " + ex.Message);
            }
        }
        return wrote;
    }


        // ===== NUCLEAR NAMED PROFILE EMBEDS (tip fix-gdt-profile-always-compile) =====
    // Full tip Profile MonoScript sources as base64. HardWrite BEFORE Force Delete / compile.
    // Helpers (SlotTransformConfig etc.) live in GameDataTypes — these files are SO-only.
    public static class NamedProfileEmbeds
    {
        public const string TipId = "fix-gdt-profile-always-compile";

        // Assets/Scripts/Data/StageLayoutProfile.cs (9102 bytes)
        const string B64_StageLayoutProfile =
            "dXNpbmcgU3lzdGVtOwp1c2luZyBTeXN0ZW0uQ29sbGVjdGlvbnMuR2VuZXJpYzsKdXNpbmcgVW5pdHlFbmdpbmU7CgpuYW1lc3Bh" +
            "Y2UgQXJ0aHVyaWFuQXV0b2JhdHRsZXIKewogICAgLy8gdGlwOiBmaXgtZ2R0LXByb2ZpbGUtYWx3YXlzLWNvbXBpbGUg4oCUIG5h" +
            "bWVkIE1vbm9TY3JpcHQgU08gb25seSAoaGVscGVycyBsaXZlIGluIEdhbWVEYXRhVHlwZXMpCiAgICAvLy8gPHN1bW1hcnk+CiAg" +
            "ICAvLy8gTnVtYmVyZWQgU29tZXJzZXQgY2h1cmNoIHBld3MgKDUyKSwgcGxheWVyIGJhdHRsZSBzbG90cyAoNTIpLCBlbmVteSBz" +
            "bG90cyAoMjApLAogICAgLy8vIHBsdXMgcGFyYWxsYXggLyBhdG1vc3BoZXJlIHRvZ2dsZXMuIFJlc291cmNlcyBwYXRoOiBTdGFn" +
            "ZUxheW91dFByb2ZpbGUuCiAgICAvLy8gPC9zdW1tYXJ5PgogICAgW0NyZWF0ZUFzc2V0TWVudShmaWxlTmFtZSA9ICJTdGFnZUxh" +
            "eW91dFByb2ZpbGUiLCBtZW51TmFtZSA9ICJEZW1vbktuaWdodC9TdGFnZSBMYXlvdXQgUHJvZmlsZSIpXQogICAgcHVibGljIGNs" +
            "YXNzIFN0YWdlTGF5b3V0UHJvZmlsZSA6IFNjcmlwdGFibGVPYmplY3QKICAgIHsKICAgICAgICBwdWJsaWMgY29uc3Qgc3RyaW5n" +
            "IFJlc291cmNlc1BhdGggPSAiU3RhZ2VMYXlvdXRQcm9maWxlIjsKICAgICAgICBwdWJsaWMgY29uc3QgaW50IENodXJjaFBld0Nv" +
            "dW50ID0gNTI7CiAgICAgICAgcHVibGljIGNvbnN0IGludCBQbGF5ZXJCYXR0bGVDb3VudCA9IDUyOwogICAgICAgIHB1YmxpYyBj" +
            "b25zdCBpbnQgRW5lbXlCYXR0bGVDb3VudCA9IDIwOwogICAgICAgIC8vLyA8c3VtbWFyeT5Hb3NzaXAgLyBwZXcgcm93IHNpemUg" +
            "KHBld1NlYXRJbmRleCAvIDEzKS48L3N1bW1hcnk+CiAgICAgICAgcHVibGljIGNvbnN0IGludCBQZXdSb3dTaXplID0gMTM7Cgog" +
            "ICAgICAgIHB1YmxpYyBMaXN0PFNsb3RUcmFuc2Zvcm1Db25maWc+IGNodXJjaFBld1Nsb3RzID0gbmV3IExpc3Q8U2xvdFRyYW5z" +
            "Zm9ybUNvbmZpZz4oKTsKICAgICAgICBwdWJsaWMgTGlzdDxTbG90VHJhbnNmb3JtQ29uZmlnPiBwbGF5ZXJCYXR0bGVTbG90cyA9" +
            "IG5ldyBMaXN0PFNsb3RUcmFuc2Zvcm1Db25maWc+KCk7CiAgICAgICAgcHVibGljIExpc3Q8U2xvdFRyYW5zZm9ybUNvbmZpZz4g" +
            "ZW5lbXlCYXR0bGVTbG90cyA9IG5ldyBMaXN0PFNsb3RUcmFuc2Zvcm1Db25maWc+KCk7CiAgICAgICAgcHVibGljIExpc3Q8U3Rh" +
            "Z2VQYXJhbGxheExheWVyPiBwYXJhbGxheExheWVycyA9IG5ldyBMaXN0PFN0YWdlUGFyYWxsYXhMYXllcj4oKTsKCiAgICAgICAg" +
            "cHVibGljIGJvb2wgZW5hYmxlUmFpbkZYOwogICAgICAgIHB1YmxpYyBib29sIGVuYWJsZUxpZ2h0bmluZ0ZsYXNoZXM7CiAgICAg" +
            "ICAgcHVibGljIGJvb2wgZW5hYmxlQ2FuZGxlRmxpY2tlciA9IHRydWU7CgogICAgICAgIC8vLyA8c3VtbWFyeT5UcnVlIHdoZW4g" +
            "dGhpcyBpcyBhIENyZWF0ZUluc3RhbmNlIHN0YW5kLWluIChub3QgYSBSZXNvdXJjZXMgYXNzZXQpLjwvc3VtbWFyeT4KICAgICAg" +
            "ICBbU3lzdGVtLk5vblNlcmlhbGl6ZWRdIHB1YmxpYyBib29sIGlzUnVudGltZVN0YW5kSW47CgogICAgICAgIHB1YmxpYyBzdGF0" +
            "aWMgU3RhZ2VMYXlvdXRQcm9maWxlIExvYWQoKQogICAgICAgIHsKICAgICAgICAgICAgU3RhZ2VMYXlvdXRQcm9maWxlIHAgPSBu" +
            "dWxsOwogICAgICAgICAgICB0cnkgeyBwID0gUmVzb3VyY2VzLkxvYWQ8U3RhZ2VMYXlvdXRQcm9maWxlPihSZXNvdXJjZXNQYXRo" +
            "KTsgfQogICAgICAgICAgICBjYXRjaCB7IC8qIG1pc3NpbmcgTW9ub1NjcmlwdCBHVUlEIGFmdGVyIGVtYmVkIOKAlCBmYWxsIHRo" +
            "cm91Z2ggKi8gfQogICAgICAgICAgICBpZiAocCAhPSBudWxsKQogICAgICAgICAgICB7CiAgICAgICAgICAgICAgICBwLkVuc3Vy" +
            "ZVNsb3RDb3VudHMoKTsKICAgICAgICAgICAgICAgIHAuaXNSdW50aW1lU3RhbmRJbiA9IGZhbHNlOwogICAgICAgICAgICAgICAg" +
            "cC5SZW1hdGVyaWFsaXplU3ByaXRlc0Zyb21QYXRocygpOwogICAgICAgICAgICAgICAgcmV0dXJuIHA7CiAgICAgICAgICAgIH0K" +
            "ICAgICAgICAgICAgdHJ5CiAgICAgICAgICAgIHsKICAgICAgICAgICAgICAgIHZhciBhbGwgPSBSZXNvdXJjZXMuTG9hZEFsbDxT" +
            "dGFnZUxheW91dFByb2ZpbGU+KCIiKTsKICAgICAgICAgICAgICAgIGlmIChhbGwgIT0gbnVsbCkKICAgICAgICAgICAgICAgIHsK" +
            "ICAgICAgICAgICAgICAgICAgICBmb3IgKGludCBpID0gMDsgaSA8IGFsbC5MZW5ndGg7IGkrKykKICAgICAgICAgICAgICAgICAg" +
            "ICB7CiAgICAgICAgICAgICAgICAgICAgICAgIGlmIChhbGxbaV0gPT0gbnVsbCkgY29udGludWU7CiAgICAgICAgICAgICAgICAg" +
            "ICAgICAgIGFsbFtpXS5FbnN1cmVTbG90Q291bnRzKCk7CiAgICAgICAgICAgICAgICAgICAgICAgIGFsbFtpXS5pc1J1bnRpbWVT" +
            "dGFuZEluID0gZmFsc2U7CiAgICAgICAgICAgICAgICAgICAgICAgIGFsbFtpXS5SZW1hdGVyaWFsaXplU3ByaXRlc0Zyb21QYXRo" +
            "cygpOwogICAgICAgICAgICAgICAgICAgICAgICByZXR1cm4gYWxsW2ldOwogICAgICAgICAgICAgICAgICAgIH0KICAgICAgICAg" +
            "ICAgICAgIH0KICAgICAgICAgICAgfQogICAgICAgICAgICBjYXRjaCB7IC8qIGJyb2tlbiBSZXNvdXJjZXMgYXNzZXQgKi8gfQog" +
            "ICAgICAgICAgICAvLyBSdW50aW1lIGZhbGxiYWNrIHdoZW4gdGhlIFJlc291cmNlcyBhc3NldCBoYXMgYSBzdGFsZSBzY3JpcHQg" +
            "R1VJRCAoRm9yY2UgU3luYyBlbWJlZCkuCiAgICAgICAgICAgIC8vIE5ldmVyIHdpcGUgYXV0aG9yZWQgUmVzb3VyY2VzIOKAlCBz" +
            "dGFuZC1pbiBpcyBsYXN0IHJlc29ydCBvbmx5LgogICAgICAgICAgICBwID0gQ3JlYXRlSW5zdGFuY2U8U3RhZ2VMYXlvdXRQcm9m" +
            "aWxlPigpOwogICAgICAgICAgICBwLm5hbWUgPSAiU3RhZ2VMYXlvdXRQcm9maWxlX1J1bnRpbWUiOwogICAgICAgICAgICBwLmlz" +
            "UnVudGltZVN0YW5kSW4gPSB0cnVlOwogICAgICAgICAgICBwLkVuc3VyZVNsb3RDb3VudHMoKTsKICAgICAgICAgICAgcmV0dXJu" +
            "IHA7CiAgICAgICAgfQoKICAgICAgICAvLy8gPHN1bW1hcnk+CiAgICAgICAgLy8vIFJlc3RvcmUgTWlzc2luZyBwZXcvYmF0dGxl" +
            "IHNlYXQgc3ByaXRlcyBmcm9tIHBhdGggYmFja3Vwcy4KICAgICAgICAvLy8gV29ya3MgaW4gcGxheWVyIGJ1aWxkcyB2aWEgUmVz" +
            "b3VyY2VzLkxvYWQgLyBTdHJlYW1pbmdBc3NldHMgKG5vdCBFZGl0b3Itb25seSkuCiAgICAgICAgLy8vIE5ldmVyIGNsZWFycyBw" +
            "YXRoIGJhY2t1cHMgd2hlbiBzcHJpdGVzIGFyZSB0ZW1wb3JhcmlseSBudWxsLgogICAgICAgIC8vLyA8L3N1bW1hcnk+CiAgICAg" +
            "ICAgcHVibGljIGludCBSZW1hdGVyaWFsaXplU3ByaXRlc0Zyb21QYXRocygpCiAgICAgICAgewogICAgICAgICAgICBFbnN1cmVT" +
            "bG90Q291bnRzKCk7CiAgICAgICAgICAgIGludCBuID0gMDsKICAgICAgICAgICAgbiArPSBSZW1hdGVyaWFsaXplU2xvdExpc3Qo" +
            "Y2h1cmNoUGV3U2xvdHMpOwogICAgICAgICAgICBuICs9IFJlbWF0ZXJpYWxpemVTbG90TGlzdChwbGF5ZXJCYXR0bGVTbG90cyk7" +
            "CiAgICAgICAgICAgIG4gKz0gUmVtYXRlcmlhbGl6ZVNsb3RMaXN0KGVuZW15QmF0dGxlU2xvdHMpOwogICAgICAgICAgICByZXR1" +
            "cm4gbjsKICAgICAgICB9CgogICAgICAgIHN0YXRpYyBpbnQgUmVtYXRlcmlhbGl6ZVNsb3RMaXN0KExpc3Q8U2xvdFRyYW5zZm9y" +
            "bUNvbmZpZz4gc2xvdHMpCiAgICAgICAgewogICAgICAgICAgICBpZiAoc2xvdHMgPT0gbnVsbCkgcmV0dXJuIDA7CiAgICAgICAg" +
            "ICAgIGludCByZXN0b3JlZCA9IDA7CiAgICAgICAgICAgIGZvciAoaW50IGkgPSAwOyBpIDwgc2xvdHMuQ291bnQ7IGkrKykKICAg" +
            "ICAgICAgICAgewogICAgICAgICAgICAgICAgdmFyIHMgPSBzbG90c1tpXTsKICAgICAgICAgICAgICAgIGlmIChzID09IG51bGwg" +
            "fHwgcy5wcmV2aWV3T3JPdmVycmlkZVNwcml0ZSAhPSBudWxsKSBjb250aW51ZTsKICAgICAgICAgICAgICAgIGlmIChzdHJpbmcu" +
            "SXNOdWxsT3JFbXB0eShzLnByZXZpZXdBc3NldFBhdGgpKSBjb250aW51ZTsKICAgICAgICAgICAgICAgIFNwcml0ZSBzcCA9IENs" +
            "YXNzU3ByaXRlUG9vbFByb2ZpbGUuVHJ5TG9hZFNwcml0ZUF0UGF0aChzLnByZXZpZXdBc3NldFBhdGgpOwogICAgICAgICAgICAg" +
            "ICAgaWYgKHNwID09IG51bGwpIGNvbnRpbnVlOwogICAgICAgICAgICAgICAgcy5wcmV2aWV3T3JPdmVycmlkZVNwcml0ZSA9IHNw" +
            "OwogICAgICAgICAgICAgICAgcmVzdG9yZWQrKzsKICAgICAgICAgICAgfQogICAgICAgICAgICByZXR1cm4gcmVzdG9yZWQ7CiAg" +
            "ICAgICAgfQoKICAgICAgICAvLy8gPHN1bW1hcnk+UGFkIC8gdHJpbSBsaXN0cyB0byA1MiAvIDUyIC8gMjAgd2l0aCBkZWZhdWx0" +
            "IHRyYW5zZm9ybXMuPC9zdW1tYXJ5PgogICAgICAgIHB1YmxpYyB2b2lkIEVuc3VyZVNsb3RDb3VudHMoKQogICAgICAgIHsKICAg" +
            "ICAgICAgICAgY2h1cmNoUGV3U2xvdHMgPz89IG5ldyBMaXN0PFNsb3RUcmFuc2Zvcm1Db25maWc+KCk7CiAgICAgICAgICAgIHBs" +
            "YXllckJhdHRsZVNsb3RzID8/PSBuZXcgTGlzdDxTbG90VHJhbnNmb3JtQ29uZmlnPigpOwogICAgICAgICAgICBlbmVteUJhdHRs" +
            "ZVNsb3RzID8/PSBuZXcgTGlzdDxTbG90VHJhbnNmb3JtQ29uZmlnPigpOwogICAgICAgICAgICBwYXJhbGxheExheWVycyA/Pz0g" +
            "bmV3IExpc3Q8U3RhZ2VQYXJhbGxheExheWVyPigpOwoKICAgICAgICAgICAgRmlsbFNsb3RzKGNodXJjaFBld1Nsb3RzLCBDaHVy" +
            "Y2hQZXdDb3VudCwgQ2h1cmNoUGV3RGVmYXVsdCk7CiAgICAgICAgICAgIEZpbGxTbG90cyhwbGF5ZXJCYXR0bGVTbG90cywgUGxh" +
            "eWVyQmF0dGxlQ291bnQsIFBsYXllckJhdHRsZURlZmF1bHQpOwogICAgICAgICAgICBGaWxsU2xvdHMoZW5lbXlCYXR0bGVTbG90" +
            "cywgRW5lbXlCYXR0bGVDb3VudCwgRW5lbXlCYXR0bGVEZWZhdWx0KTsKICAgICAgICB9CgogICAgICAgIHN0YXRpYyB2b2lkIEZp" +
            "bGxTbG90cyhMaXN0PFNsb3RUcmFuc2Zvcm1Db25maWc+IGxpc3QsIGludCBjb3VudCwgRnVuYzxpbnQsIFNsb3RUcmFuc2Zvcm1D" +
            "b25maWc+IGZhY3RvcnkpCiAgICAgICAgewogICAgICAgICAgICB3aGlsZSAobGlzdC5Db3VudCA8IGNvdW50KQogICAgICAgICAg" +
            "ICAgICAgbGlzdC5BZGQoZmFjdG9yeShsaXN0LkNvdW50KSk7CiAgICAgICAgICAgIGlmIChsaXN0LkNvdW50ID4gY291bnQpCiAg" +
            "ICAgICAgICAgICAgICBsaXN0LlJlbW92ZVJhbmdlKGNvdW50LCBsaXN0LkNvdW50IC0gY291bnQpOwogICAgICAgICAgICBmb3Ig" +
            "KGludCBpID0gMDsgaSA8IGxpc3QuQ291bnQ7IGkrKykKICAgICAgICAgICAgewogICAgICAgICAgICAgICAgaWYgKGxpc3RbaV0g" +
            "PT0gbnVsbCkgbGlzdFtpXSA9IGZhY3RvcnkoaSk7CiAgICAgICAgICAgICAgICBsaXN0W2ldLnNsb3ROdW1iZXIgPSBpOwogICAg" +
            "ICAgICAgICB9CiAgICAgICAgfQoKICAgICAgICAvLy8gPHN1bW1hcnk+CiAgICAgICAgLy8vIFBsYXktbm9ybWFsaXplZCAw4oCT" +
            "MSBjaHVyY2ggc2VhdCAobWF0Y2hlcyBTb21lcnNldFBld0xheW91dCBkZWZhdWx0cykuCiAgICAgICAgLy8vIFdvcmxkLXNwYWNl" +
            "IGRlZmF1bHRzICgtNi4uNikgd2VyZSBpZ25vcmVkIGJ5IFBvcHVsYXRlUGV3cywgc28gU3RhZ2UgUGxhY2VyCiAgICAgICAgLy8v" +
            "IGVkaXRzIG5ldmVyIHNob3dlZCBpbiBQbGF5IHVudGlsIEFyY2hpdGVjdCByZS1zZWVkZWQg4oCUIGZpeGVkIGhlcmUuCiAgICAg" +
            "ICAgLy8vIDwvc3VtbWFyeT4KICAgICAgICBzdGF0aWMgU2xvdFRyYW5zZm9ybUNvbmZpZyBDaHVyY2hQZXdEZWZhdWx0KGludCBp" +
            "KQogICAgICAgIHsKICAgICAgICAgICAgVmVjdG9yMiBub3JtID0gRGVmYXVsdENodXJjaE5vcm1hbGl6ZWRQb3NpdGlvbihpKTsK" +
            "ICAgICAgICAgICAgcmV0dXJuIG5ldyBTbG90VHJhbnNmb3JtQ29uZmlnCiAgICAgICAgICAgIHsKICAgICAgICAgICAgICAgIHNs" +
            "b3ROdW1iZXIgPSBpLAogICAgICAgICAgICAgICAgcG9zaXRpb24gPSBub3JtLAogICAgICAgICAgICAgICAgc2NhbGUgPSBWZWN0" +
            "b3IyLm9uZSwKICAgICAgICAgICAgICAgIGFzcGVjdE11bHRpcGxpZXIgPSBWZWN0b3IyLm9uZSwKICAgICAgICAgICAgICAgIHNv" +
            "cnRpbmdPcmRlciA9IC1NYXRoZi5Sb3VuZFRvSW50KG5vcm0ueSAqIDEwMGYpCiAgICAgICAgICAgIH07CiAgICAgICAgfQoKICAg" +
            "ICAgICAvLy8gPHN1bW1hcnk+Tm9ybWFsaXplZCAw4oCTMSBwZXcgc2VhdCBtYXRjaGluZyBTb21lcnNldFBld0xheW91dC5DcmVh" +
            "dGVEZWZhdWx0UGV3cyBjYXBhY2l0aWVzLjwvc3VtbWFyeT4KICAgICAgICBwdWJsaWMgc3RhdGljIFZlY3RvcjIgRGVmYXVsdENo" +
            "dXJjaE5vcm1hbGl6ZWRQb3NpdGlvbihpbnQgZ2xvYmFsU2VhdEluZGV4KQogICAgICAgIHsKICAgICAgICAgICAgLy8gTWlycm9y" +
            "IFNvbWVyc2V0UGV3TGF5b3V0LkNyZWF0ZURlZmF1bHRQZXdzICh2NCkg4oCUIGtlZXAgaW4gc3luYy4KICAgICAgICAgICAgZmxv" +
            "YXRbXVtdIGNvbFhCeVJvdyA9CiAgICAgICAgICAgIHsKICAgICAgICAgICAgICAgIG5ld1tdIHsgMC4yMjBmLCAwLjUwMGYsIDAu" +
            "NzgwZiB9LAogICAgICAgICAgICAgICAgbmV3W10geyAwLjIyNmYsIDAuNTAwZiwgMC43NzVmIH0sCiAgICAgICAgICAgICAgICBu" +
            "ZXdbXSB7IDAuMjMyZiwgMC41MDBmLCAwLjc3MGYgfSwKICAgICAgICAgICAgICAgIG5ld1tdIHsgMC4yMzhmLCAwLjUwMGYsIDAu" +
            "NzY1ZiB9CiAgICAgICAgICAgIH07CiAgICAgICAgICAgIGZsb2F0W10gcm93WSA9IHsgMC40MDBmLCAwLjU0NWYsIDAuNjkwZiwg" +
            "MC44MjBmIH07CiAgICAgICAgICAgIGZsb2F0W10gcm93U3ByZWFkID0geyAwLjE3NWYsIDAuMTYwZiwgMC4xNDVmLCAwLjEyMGYg" +
            "fTsKICAgICAgICAgICAgaW50WyxdIGNhcHMgPQogICAgICAgICAgICB7CiAgICAgICAgICAgICAgICB7IDUsIDUsIDUgfSwKICAg" +
            "ICAgICAgICAgICAgIHsgNCwgNSwgNCB9LAogICAgICAgICAgICAgICAgeyA0LCA0LCA0IH0sCiAgICAgICAgICAgICAgICB7IDQs" +
            "IDQsIDQgfQogICAgICAgICAgICB9OwoKICAgICAgICAgICAgaW50IHJlbWFpbmluZyA9IE1hdGhmLkNsYW1wKGdsb2JhbFNlYXRJ" +
            "bmRleCwgMCwgQ2h1cmNoUGV3Q291bnQgLSAxKTsKICAgICAgICAgICAgZm9yIChpbnQgcm93ID0gMDsgcm93IDwgNDsgcm93Kysp" +
            "CiAgICAgICAgICAgIHsKICAgICAgICAgICAgICAgIGZvciAoaW50IGNvbCA9IDA7IGNvbCA8IDM7IGNvbCsrKQogICAgICAgICAg" +
            "ICAgICAgewogICAgICAgICAgICAgICAgICAgIGludCBjYXAgPSBNYXRoZi5NYXgoMSwgY2Fwc1tyb3csIGNvbF0pOwogICAgICAg" +
            "ICAgICAgICAgICAgIGlmIChyZW1haW5pbmcgPCBjYXApCiAgICAgICAgICAgICAgICAgICAgewogICAgICAgICAgICAgICAgICAg" +
            "ICAgICBmbG9hdCB0ID0gY2FwID09IDEgPyAwLjVmIDogcmVtYWluaW5nIC8gKGZsb2F0KShjYXAgLSAxKTsKICAgICAgICAgICAg" +
            "ICAgICAgICAgICAgZmxvYXQgeCA9IGNvbFhCeVJvd1tyb3ddW2NvbF0KICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAg" +
            "ICsgTWF0aGYuTGVycCgtcm93U3ByZWFkW3Jvd10gKiAwLjVmLCByb3dTcHJlYWRbcm93XSAqIDAuNWYsIHQpOwogICAgICAgICAg" +
            "ICAgICAgICAgICAgICByZXR1cm4gbmV3IFZlY3RvcjIoeCwgcm93WVtyb3ddKTsKICAgICAgICAgICAgICAgICAgICB9CiAgICAg" +
            "ICAgICAgICAgICAgICAgcmVtYWluaW5nIC09IGNhcDsKICAgICAgICAgICAgICAgIH0KICAgICAgICAgICAgfQogICAgICAgICAg" +
            "ICByZXR1cm4gbmV3IFZlY3RvcjIoMC41ZiwgMC41Zik7CiAgICAgICAgfQoKICAgICAgICAvLy8gPHN1bW1hcnk+U3RhZ2dlcmVk" +
            "IGNvbHVtbnMgb2YgNiAoZnJvbnRsaW5lU2l6ZSA9IDYpIOKAlCBtYXRjaGVzIFBoYXNlIDMgYXV0by1hcnJhbmdlIG1hdGguPC9z" +
            "dW1tYXJ5PgogICAgICAgIHN0YXRpYyBTbG90VHJhbnNmb3JtQ29uZmlnIFBsYXllckJhdHRsZURlZmF1bHQoaW50IGkpCiAgICAg" +
            "ICAgewogICAgICAgICAgICBjb25zdCBpbnQgZnJvbnRsaW5lU2l6ZSA9IDY7CiAgICAgICAgICAgIGludCBjb2wgPSBpIC8gZnJv" +
            "bnRsaW5lU2l6ZTsKICAgICAgICAgICAgaW50IHJvdyA9IGkgJSBmcm9udGxpbmVTaXplOwogICAgICAgICAgICBmbG9hdCBzY2Fs" +
            "ZSA9IGNvbCA8PSAxID8gMWYgOiBNYXRoZi5MZXJwKDAuOTVmLCAwLjcwZiwgTWF0aGYuSW52ZXJzZUxlcnAoMmYsIDhmLCBjb2wp" +
            "KTsKICAgICAgICAgICAgZmxvYXQgeCA9IC0zLjVmICsgcm93ICogMS4xZjsKICAgICAgICAgICAgZmxvYXQgeSA9IDIuMGYgLSBj" +
            "b2wgKiAwLjg1ZjsKICAgICAgICAgICAgcmV0dXJuIG5ldyBTbG90VHJhbnNmb3JtQ29uZmlnCiAgICAgICAgICAgIHsKICAgICAg" +
            "ICAgICAgICAgIHNsb3ROdW1iZXIgPSBpLAogICAgICAgICAgICAgICAgcG9zaXRpb24gPSBuZXcgVmVjdG9yMih4LCB5KSwKICAg" +
            "ICAgICAgICAgICAgIHNjYWxlID0gbmV3IFZlY3RvcjIoc2NhbGUsIHNjYWxlKSwKICAgICAgICAgICAgICAgIGFzcGVjdE11bHRp" +
            "cGxpZXIgPSBWZWN0b3IyLm9uZSwKICAgICAgICAgICAgICAgIHNvcnRpbmdPcmRlciA9IE1hdGhmLlJvdW5kVG9JbnQoLXkgKiAx" +
            "MGYpCiAgICAgICAgICAgIH07CiAgICAgICAgfQoKICAgICAgICBzdGF0aWMgU2xvdFRyYW5zZm9ybUNvbmZpZyBFbmVteUJhdHRs" +
            "ZURlZmF1bHQoaW50IGkpCiAgICAgICAgewogICAgICAgICAgICBpbnQgY29sID0gaSAvIDU7CiAgICAgICAgICAgIGludCByb3cg" +
            "PSBpICUgNTsKICAgICAgICAgICAgZmxvYXQgeCA9IDMuNWYgKyByb3cgKiAxLjBmOwogICAgICAgICAgICBmbG9hdCB5ID0gMS41" +
            "ZiAtIGNvbCAqIDEuMGY7CiAgICAgICAgICAgIHJldHVybiBuZXcgU2xvdFRyYW5zZm9ybUNvbmZpZwogICAgICAgICAgICB7CiAg" +
            "ICAgICAgICAgICAgICBzbG90TnVtYmVyID0gaSwKICAgICAgICAgICAgICAgIHBvc2l0aW9uID0gbmV3IFZlY3RvcjIoeCwgeSks" +
            "CiAgICAgICAgICAgICAgICBzY2FsZSA9IFZlY3RvcjIub25lLAogICAgICAgICAgICAgICAgYXNwZWN0TXVsdGlwbGllciA9IFZl" +
            "Y3RvcjIub25lLAogICAgICAgICAgICAgICAgc29ydGluZ09yZGVyID0gTWF0aGYuUm91bmRUb0ludCgteSAqIDEwZikKICAgICAg" +
            "ICAgICAgfTsKICAgICAgICB9CiAgICB9Cn0K"
            ;
        // Assets/Scripts/Data/SceneBackgroundProfile.cs (4643 bytes)
        const string B64_SceneBackgroundProfile =
            "dXNpbmcgU3lzdGVtOwp1c2luZyBTeXN0ZW0uQ29sbGVjdGlvbnMuR2VuZXJpYzsKdXNpbmcgVW5pdHlFbmdpbmU7CgpuYW1lc3Bh" +
            "Y2UgQXJ0aHVyaWFuQXV0b2JhdHRsZXIKewogICAgLy8gdGlwOiBmaXgtZ2R0LXByb2ZpbGUtYWx3YXlzLWNvbXBpbGUg4oCUIG5h" +
            "bWVkIE1vbm9TY3JpcHQgU08gb25seSAoU2NlbmVCYWNrZ3JvdW5kU2xvdC9FbnRyeSBsaXZlIGluIEdhbWVEYXRhVHlwZXMpCiAg" +
            "ICAvLy8gPHN1bW1hcnk+CiAgICAvLy8gQXJjaGl0ZWN0LWFzc2lnbmVkIHNjZW5lL3NjcmVlbiBiYWNrZ3JvdW5kcyAoaW1hZ2Ug" +
            "b3IgdmlkZW8pLiBSZXNvdXJjZXMgcGF0aDogU2NlbmVCYWNrZ3JvdW5kUHJvZmlsZS4KICAgIC8vLyBBc3NpZ24tb25seSDigJQg" +
            "ZW1wdHkgc2xvdHMga2VlcCBlYWNoIHNjcmVlbidzIGJ1aWx0LWluIGRlZmF1bHQgYXJ0LgogICAgLy8vIDwvc3VtbWFyeT4KICAg" +
            "IFtDcmVhdGVBc3NldE1lbnUoZmlsZU5hbWUgPSAiU2NlbmVCYWNrZ3JvdW5kUHJvZmlsZSIsIG1lbnVOYW1lID0gIkRlbW9uS25p" +
            "Z2h0L1NjZW5lIEJhY2tncm91bmQgUHJvZmlsZSIpXQogICAgcHVibGljIGNsYXNzIFNjZW5lQmFja2dyb3VuZFByb2ZpbGUgOiBT" +
            "Y3JpcHRhYmxlT2JqZWN0CiAgICB7CiAgICAgICAgcHVibGljIGNvbnN0IHN0cmluZyBSZXNvdXJjZXNQYXRoID0gIlNjZW5lQmFj" +
            "a2dyb3VuZFByb2ZpbGUiOwoKICAgICAgICBwdWJsaWMgTGlzdDxTY2VuZUJhY2tncm91bmRFbnRyeT4gZW50cmllcyA9IG5ldyBM" +
            "aXN0PFNjZW5lQmFja2dyb3VuZEVudHJ5PigpOwoKICAgICAgICBwdWJsaWMgc3RhdGljIFNjZW5lQmFja2dyb3VuZFByb2ZpbGUg" +
            "TG9hZCgpCiAgICAgICAgewogICAgICAgICAgICBTY2VuZUJhY2tncm91bmRQcm9maWxlIHAgPSBudWxsOwogICAgICAgICAgICB0" +
            "cnkgeyBwID0gUmVzb3VyY2VzLkxvYWQ8U2NlbmVCYWNrZ3JvdW5kUHJvZmlsZT4oUmVzb3VyY2VzUGF0aCk7IH0KICAgICAgICAg" +
            "ICAgY2F0Y2ggeyAvKiBtaXNzaW5nIE1vbm9TY3JpcHQgR1VJRC9maWxlSUQgYWZ0ZXIgZW1iZWQg4oCUIGZhbGwgdGhyb3VnaCAq" +
            "LyB9CiAgICAgICAgICAgIGlmIChwICE9IG51bGwpCiAgICAgICAgICAgIHsKICAgICAgICAgICAgICAgIHAuRW5zdXJlRGVmYXVs" +
            "dEVudHJpZXMoKTsKICAgICAgICAgICAgICAgIHAuUmVtYXRlcmlhbGl6ZVNwcml0ZXNGcm9tUGF0aHMoKTsKICAgICAgICAgICAg" +
            "ICAgIHJldHVybiBwOwogICAgICAgICAgICB9CiAgICAgICAgICAgIHRyeQogICAgICAgICAgICB7CiAgICAgICAgICAgICAgICB2" +
            "YXIgYWxsID0gUmVzb3VyY2VzLkxvYWRBbGw8U2NlbmVCYWNrZ3JvdW5kUHJvZmlsZT4oIiIpOwogICAgICAgICAgICAgICAgaWYg" +
            "KGFsbCAhPSBudWxsKQogICAgICAgICAgICAgICAgewogICAgICAgICAgICAgICAgICAgIGZvciAoaW50IGkgPSAwOyBpIDwgYWxs" +
            "Lkxlbmd0aDsgaSsrKQogICAgICAgICAgICAgICAgICAgIHsKICAgICAgICAgICAgICAgICAgICAgICAgaWYgKGFsbFtpXSA9PSBu" +
            "dWxsKSBjb250aW51ZTsKICAgICAgICAgICAgICAgICAgICAgICAgYWxsW2ldLkVuc3VyZURlZmF1bHRFbnRyaWVzKCk7CiAgICAg" +
            "ICAgICAgICAgICAgICAgICAgIGFsbFtpXS5SZW1hdGVyaWFsaXplU3ByaXRlc0Zyb21QYXRocygpOwogICAgICAgICAgICAgICAg" +
            "ICAgICAgICByZXR1cm4gYWxsW2ldOwogICAgICAgICAgICAgICAgICAgIH0KICAgICAgICAgICAgICAgIH0KICAgICAgICAgICAg" +
            "fQogICAgICAgICAgICBjYXRjaCB7IC8qIGJyb2tlbiBSZXNvdXJjZXMgYXNzZXQgLyBtaXNzaW5nIHNjcmlwdCBzcGFtIOKAlCBz" +
            "b2Z0IHN0YW5kLWluICovIH0KICAgICAgICAgICAgcCA9IENyZWF0ZUluc3RhbmNlPFNjZW5lQmFja2dyb3VuZFByb2ZpbGU+KCk7" +
            "CiAgICAgICAgICAgIHAubmFtZSA9ICJTY2VuZUJhY2tncm91bmRQcm9maWxlX1J1bnRpbWUiOwogICAgICAgICAgICBwLkVuc3Vy" +
            "ZURlZmF1bHRFbnRyaWVzKCk7CiAgICAgICAgICAgIHJldHVybiBwOwogICAgICAgIH0KCiAgICAgICAgLy8vIDxzdW1tYXJ5Pgog" +
            "ICAgICAgIC8vLyBSZXN0b3JlIE1pc3NpbmcgaW1hZ2Ugc3ByaXRlcyBmcm9tIHBhdGggYmFja3VwcyAoUmVzb3VyY2VzIC8gU3Ry" +
            "ZWFtaW5nQXNzZXRzKS4KICAgICAgICAvLy8gdGlwOiBmaXgtY2xhc3MtcG9vbC1raW5kLWNzMDAxOQogICAgICAgIC8vLyA8L3N1" +
            "bW1hcnk+CiAgICAgICAgcHVibGljIGludCBSZW1hdGVyaWFsaXplU3ByaXRlc0Zyb21QYXRocygpCiAgICAgICAgewogICAgICAg" +
            "ICAgICBFbnN1cmVEZWZhdWx0RW50cmllcygpOwogICAgICAgICAgICBpZiAoZW50cmllcyA9PSBudWxsKSByZXR1cm4gMDsKICAg" +
            "ICAgICAgICAgaW50IHJlc3RvcmVkID0gMDsKICAgICAgICAgICAgZm9yIChpbnQgaSA9IDA7IGkgPCBlbnRyaWVzLkNvdW50OyBp" +
            "KyspCiAgICAgICAgICAgIHsKICAgICAgICAgICAgICAgIHZhciBlID0gZW50cmllc1tpXTsKICAgICAgICAgICAgICAgIGlmIChl" +
            "ID09IG51bGwgfHwgZS5pbWFnZVNwcml0ZSAhPSBudWxsKSBjb250aW51ZTsKICAgICAgICAgICAgICAgIGlmIChzdHJpbmcuSXNO" +
            "dWxsT3JFbXB0eShlLmltYWdlQXNzZXRQYXRoKSkgY29udGludWU7CiAgICAgICAgICAgICAgICBTcHJpdGUgc3AgPSBDbGFzc1Nw" +
            "cml0ZVBvb2xQcm9maWxlLlRyeUxvYWRTcHJpdGVBdFBhdGgoZS5pbWFnZUFzc2V0UGF0aCk7CiAgICAgICAgICAgICAgICBpZiAo" +
            "c3AgPT0gbnVsbCkgY29udGludWU7CiAgICAgICAgICAgICAgICBlLmltYWdlU3ByaXRlID0gc3A7CiAgICAgICAgICAgICAgICBy" +
            "ZXN0b3JlZCsrOwogICAgICAgICAgICB9CiAgICAgICAgICAgIHJldHVybiByZXN0b3JlZDsKICAgICAgICB9CgogICAgICAgIHB1" +
            "YmxpYyB2b2lkIEVuc3VyZURlZmF1bHRFbnRyaWVzKCkKICAgICAgICB7CiAgICAgICAgICAgIGVudHJpZXMgPz89IG5ldyBMaXN0" +
            "PFNjZW5lQmFja2dyb3VuZEVudHJ5PigpOwogICAgICAgICAgICBFbnN1cmVFbnRyeShTY2VuZUJhY2tncm91bmRTbG90Lk1haW5N" +
            "ZW51LCAiTWFpbiBNZW51Iik7CiAgICAgICAgICAgIEVuc3VyZUVudHJ5KFNjZW5lQmFja2dyb3VuZFNsb3QuU29tZXJzZXRDaHVy" +
            "Y2gsICJTb21lcnNldCBDaHVyY2giKTsKICAgICAgICAgICAgRW5zdXJlRW50cnkoU2NlbmVCYWNrZ3JvdW5kU2xvdC5DaHVyY2h5" +
            "YXJkQmF0dGxlLCAiQ2h1cmNoeWFyZCBCYXR0bGUiKTsKICAgICAgICAgICAgRW5zdXJlRW50cnkoU2NlbmVCYWNrZ3JvdW5kU2xv" +
            "dC5WaWN0b3J5T3ZlcmxheSwgIlZpY3RvcnkgT3ZlcmxheSIpOwogICAgICAgICAgICBFbnN1cmVFbnRyeShTY2VuZUJhY2tncm91" +
            "bmRTbG90Lk1lcmxpblJlc3VycmVjdGlvbiwgIk1lcmxpbiBSZXN1cnJlY3Rpb24iKTsKICAgICAgICAgICAgRW5zdXJlRW50cnko" +
            "U2NlbmVCYWNrZ3JvdW5kU2xvdC5MZWdlbmRDb2RleCwgIkxlZ2VuZHMgQ29kZXgiKTsKICAgICAgICB9CgogICAgICAgIHZvaWQg" +
            "RW5zdXJlRW50cnkoU2NlbmVCYWNrZ3JvdW5kU2xvdCBzbG90LCBzdHJpbmcgbGFiZWwpCiAgICAgICAgewogICAgICAgICAgICBm" +
            "b3IgKGludCBpID0gMDsgaSA8IGVudHJpZXMuQ291bnQ7IGkrKykKICAgICAgICAgICAgewogICAgICAgICAgICAgICAgaWYgKGVu" +
            "dHJpZXNbaV0gIT0gbnVsbCAmJiBlbnRyaWVzW2ldLnNsb3QgPT0gc2xvdCkKICAgICAgICAgICAgICAgIHsKICAgICAgICAgICAg" +
            "ICAgICAgICBpZiAoc3RyaW5nLklzTnVsbE9yRW1wdHkoZW50cmllc1tpXS5kaXNwbGF5TmFtZSkpCiAgICAgICAgICAgICAgICAg" +
            "ICAgICAgIGVudHJpZXNbaV0uZGlzcGxheU5hbWUgPSBsYWJlbDsKICAgICAgICAgICAgICAgICAgICByZXR1cm47CiAgICAgICAg" +
            "ICAgICAgICB9CiAgICAgICAgICAgIH0KICAgICAgICAgICAgZW50cmllcy5BZGQobmV3IFNjZW5lQmFja2dyb3VuZEVudHJ5IHsg" +
            "c2xvdCA9IHNsb3QsIGRpc3BsYXlOYW1lID0gbGFiZWwgfSk7CiAgICAgICAgfQoKICAgICAgICBwdWJsaWMgU2NlbmVCYWNrZ3Jv" +
            "dW5kRW50cnkgR2V0RW50cnkoU2NlbmVCYWNrZ3JvdW5kU2xvdCBzbG90KQogICAgICAgIHsKICAgICAgICAgICAgRW5zdXJlRGVm" +
            "YXVsdEVudHJpZXMoKTsKICAgICAgICAgICAgZm9yIChpbnQgaSA9IDA7IGkgPCBlbnRyaWVzLkNvdW50OyBpKyspCiAgICAgICAg" +
            "ICAgIHsKICAgICAgICAgICAgICAgIGlmIChlbnRyaWVzW2ldICE9IG51bGwgJiYgZW50cmllc1tpXS5zbG90ID09IHNsb3QpCiAg" +
            "ICAgICAgICAgICAgICAgICAgcmV0dXJuIGVudHJpZXNbaV07CiAgICAgICAgICAgIH0KICAgICAgICAgICAgcmV0dXJuIG51bGw7" +
            "CiAgICAgICAgfQoKICAgICAgICBwdWJsaWMgYm9vbCBIYXNBc3NpZ25tZW50KFNjZW5lQmFja2dyb3VuZFNsb3Qgc2xvdCkKICAg" +
            "ICAgICB7CiAgICAgICAgICAgIHZhciBlID0gR2V0RW50cnkoc2xvdCk7CiAgICAgICAgICAgIHJldHVybiBlICE9IG51bGwgJiYg" +
            "KGUuaW1hZ2VTcHJpdGUgIT0gbnVsbCB8fCBlLnZpZGVvQ2xpcCAhPSBudWxsCiAgICAgICAgICAgICAgICAgICAgICAgICAgICAg" +
            "ICAgIHx8ICFzdHJpbmcuSXNOdWxsT3JFbXB0eShlLmltYWdlQXNzZXRQYXRoKQogICAgICAgICAgICAgICAgICAgICAgICAgICAg" +
            "ICAgICB8fCAhc3RyaW5nLklzTnVsbE9yRW1wdHkoZS52aWRlb0Fzc2V0UGF0aCkpOwogICAgICAgIH0KICAgIH0KfQo="
            ;
        // Assets/Scripts/Data/ClassSpritePoolProfile.cs (9070 bytes)
        const string B64_ClassSpritePoolProfile =
            "dXNpbmcgU3lzdGVtOwp1c2luZyBTeXN0ZW0uQ29sbGVjdGlvbnMuR2VuZXJpYzsKdXNpbmcgVW5pdHlFbmdpbmU7CgpuYW1lc3Bh" +
            "Y2UgQXJ0aHVyaWFuQXV0b2JhdHRsZXIKewogICAgLy8gdGlwOiBmaXgtZ2R0LXByb2ZpbGUtYWx3YXlzLWNvbXBpbGUg4oCUIG5h" +
            "bWVkIE1vbm9TY3JpcHQgZm9yIFVuaXR5IFNPIGJpbmRpbmcKICAgIC8vLyA8c3VtbWFyeT4KICAgIC8vLyBBcmNoaXRlY3QgY2xh" +
            "c3Mgc3ByaXRlIHBvb2xzIChQZWFzYW50IC8gV2FycmlvciAvIEtuaWdodCkuIFJlc291cmNlcyBwYXRoOgogICAgLy8vIENsYXNz" +
            "U3ByaXRlUG9vbFByb2ZpbGUuIFN0YWdlTGF5b3V0UHJvZmlsZSBwZXcgc2xvdHMgc3RheSBwb3NpdGlvbi9zY2FsZSBndWlkZXMg" +
            "4oCUCiAgICAvLy8gY2h1cmNoL2NvbWJhdCB2aXN1YWxzIHBpY2sgZnJvbSB0aGVzZSBwb29scyBieSB1bml0IGNsYXNzLgogICAg" +
            "Ly8vIDwvc3VtbWFyeT4KICAgIFtDcmVhdGVBc3NldE1lbnUoZmlsZU5hbWUgPSAiQ2xhc3NTcHJpdGVQb29sUHJvZmlsZSIsIG1l" +
            "bnVOYW1lID0gIkRlbW9uS25pZ2h0L0NsYXNzIFNwcml0ZSBQb29sIFByb2ZpbGUiKV0KICAgIHB1YmxpYyBjbGFzcyBDbGFzc1Nw" +
            "cml0ZVBvb2xQcm9maWxlIDogU2NyaXB0YWJsZU9iamVjdAogICAgewogICAgICAgIHB1YmxpYyBjb25zdCBzdHJpbmcgUmVzb3Vy" +
            "Y2VzUGF0aCA9ICJDbGFzc1Nwcml0ZVBvb2xQcm9maWxlIjsKCiAgICAgICAgcHVibGljIExpc3Q8U3ByaXRlPiBwZWFzYW50U3By" +
            "aXRlcyA9IG5ldyBMaXN0PFNwcml0ZT4oKTsKICAgICAgICBwdWJsaWMgTGlzdDxTcHJpdGU+IHdhcnJpb3JTcHJpdGVzID0gbmV3" +
            "IExpc3Q8U3ByaXRlPigpOwogICAgICAgIHB1YmxpYyBMaXN0PFNwcml0ZT4ga25pZ2h0U3ByaXRlcyA9IG5ldyBMaXN0PFNwcml0" +
            "ZT4oKTsKCiAgICAgICAgLy8vIDxzdW1tYXJ5PkVkaXRvciBwYXRoIGJhY2t1cHMgc28gbGlzdCBlbnRyaWVzIHN1cnZpdmUgcmVp" +
            "bXBvcnRzLjwvc3VtbWFyeT4KICAgICAgICBwdWJsaWMgTGlzdDxzdHJpbmc+IHBlYXNhbnRBc3NldFBhdGhzID0gbmV3IExpc3Q8" +
            "c3RyaW5nPigpOwogICAgICAgIHB1YmxpYyBMaXN0PHN0cmluZz4gd2FycmlvckFzc2V0UGF0aHMgPSBuZXcgTGlzdDxzdHJpbmc+" +
            "KCk7CiAgICAgICAgcHVibGljIExpc3Q8c3RyaW5nPiBrbmlnaHRBc3NldFBhdGhzID0gbmV3IExpc3Q8c3RyaW5nPigpOwoKICAg" +
            "ICAgICAvLy8gPHN1bW1hcnk+VHJ1ZSB3aGVuIHRoaXMgaXMgYSBDcmVhdGVJbnN0YW5jZSBzdGFuZC1pbiAobm90IGEgUmVzb3Vy" +
            "Y2VzIGFzc2V0KS48L3N1bW1hcnk+CiAgICAgICAgW1N5c3RlbS5Ob25TZXJpYWxpemVkXSBwdWJsaWMgYm9vbCBpc1J1bnRpbWVT" +
            "dGFuZEluOwoKICAgICAgICBwdWJsaWMgc3RhdGljIENsYXNzU3ByaXRlUG9vbFByb2ZpbGUgTG9hZCgpCiAgICAgICAgewogICAg" +
            "ICAgICAgICBDbGFzc1Nwcml0ZVBvb2xQcm9maWxlIHAgPSBudWxsOwogICAgICAgICAgICB0cnkgeyBwID0gUmVzb3VyY2VzLkxv" +
            "YWQ8Q2xhc3NTcHJpdGVQb29sUHJvZmlsZT4oUmVzb3VyY2VzUGF0aCk7IH0KICAgICAgICAgICAgY2F0Y2ggeyAvKiBtaXNzaW5n" +
            "IE1vbm9TY3JpcHQgR1VJRC9maWxlSUQgYWZ0ZXIgZW1iZWQg4oCUIGZhbGwgdGhyb3VnaCAqLyB9CiAgICAgICAgICAgIGlmIChw" +
            "ICE9IG51bGwpCiAgICAgICAgICAgIHsKICAgICAgICAgICAgICAgIHAuRW5zdXJlTGlzdHMoKTsKICAgICAgICAgICAgICAgIHAu" +
            "aXNSdW50aW1lU3RhbmRJbiA9IGZhbHNlOwogICAgICAgICAgICAgICAgLy8gUGF0aHMgZmlsbGVkICsgU3ByaXRlIE1pc3Npbmcg" +
            "YWZ0ZXIgU3luYyDihpIgcmVtYXRlcmlhbGl6ZSBiZWZvcmUgQXBwbGllciBwaWNrcy4KICAgICAgICAgICAgICAgIHAuUmVtYXRl" +
            "cmlhbGl6ZVNwcml0ZXNGcm9tUGF0aHMoKTsKICAgICAgICAgICAgICAgIHJldHVybiBwOwogICAgICAgICAgICB9CiAgICAgICAg" +
            "ICAgIHRyeQogICAgICAgICAgICB7CiAgICAgICAgICAgICAgICB2YXIgYWxsID0gUmVzb3VyY2VzLkxvYWRBbGw8Q2xhc3NTcHJp" +
            "dGVQb29sUHJvZmlsZT4oIiIpOwogICAgICAgICAgICAgICAgaWYgKGFsbCAhPSBudWxsKQogICAgICAgICAgICAgICAgewogICAg" +
            "ICAgICAgICAgICAgICAgIGZvciAoaW50IGkgPSAwOyBpIDwgYWxsLkxlbmd0aDsgaSsrKQogICAgICAgICAgICAgICAgICAgIHsK" +
            "ICAgICAgICAgICAgICAgICAgICAgICAgaWYgKGFsbFtpXSA9PSBudWxsKSBjb250aW51ZTsKICAgICAgICAgICAgICAgICAgICAg" +
            "ICAgYWxsW2ldLkVuc3VyZUxpc3RzKCk7CiAgICAgICAgICAgICAgICAgICAgICAgIGFsbFtpXS5pc1J1bnRpbWVTdGFuZEluID0g" +
            "ZmFsc2U7CiAgICAgICAgICAgICAgICAgICAgICAgIGFsbFtpXS5SZW1hdGVyaWFsaXplU3ByaXRlc0Zyb21QYXRocygpOwogICAg" +
            "ICAgICAgICAgICAgICAgICAgICByZXR1cm4gYWxsW2ldOwogICAgICAgICAgICAgICAgICAgIH0KICAgICAgICAgICAgICAgIH0K" +
            "ICAgICAgICAgICAgfQogICAgICAgICAgICBjYXRjaCB7IC8qIGJyb2tlbiBSZXNvdXJjZXMgYXNzZXQgLyBtaXNzaW5nIHNjcmlw" +
            "dCDigJQgc29mdCBzdGFuZC1pbiAqLyB9CiAgICAgICAgICAgIHAgPSBDcmVhdGVJbnN0YW5jZTxDbGFzc1Nwcml0ZVBvb2xQcm9m" +
            "aWxlPigpOwogICAgICAgICAgICBwLm5hbWUgPSAiQ2xhc3NTcHJpdGVQb29sUHJvZmlsZV9SdW50aW1lIjsKICAgICAgICAgICAg" +
            "cC5pc1J1bnRpbWVTdGFuZEluID0gdHJ1ZTsKICAgICAgICAgICAgcC5FbnN1cmVMaXN0cygpOwogICAgICAgICAgICByZXR1cm4g" +
            "cDsKICAgICAgICB9CgogICAgICAgIHB1YmxpYyB2b2lkIEVuc3VyZUxpc3RzKCkKICAgICAgICB7CiAgICAgICAgICAgIHBlYXNh" +
            "bnRTcHJpdGVzID8/PSBuZXcgTGlzdDxTcHJpdGU+KCk7CiAgICAgICAgICAgIHdhcnJpb3JTcHJpdGVzID8/PSBuZXcgTGlzdDxT" +
            "cHJpdGU+KCk7CiAgICAgICAgICAgIGtuaWdodFNwcml0ZXMgPz89IG5ldyBMaXN0PFNwcml0ZT4oKTsKICAgICAgICAgICAgcGVh" +
            "c2FudEFzc2V0UGF0aHMgPz89IG5ldyBMaXN0PHN0cmluZz4oKTsKICAgICAgICAgICAgd2FycmlvckFzc2V0UGF0aHMgPz89IG5l" +
            "dyBMaXN0PHN0cmluZz4oKTsKICAgICAgICAgICAga25pZ2h0QXNzZXRQYXRocyA/Pz0gbmV3IExpc3Q8c3RyaW5nPigpOwogICAg" +
            "ICAgIH0KCiAgICAgICAgLy8vIDxzdW1tYXJ5PgogICAgICAgIC8vLyBSZXN0b3JlIE1pc3Npbmcgc3ByaXRlIHJlZnMgZnJvbSBw" +
            "YXRoIGJhY2t1cHMgKGVkaXRvciBBc3NldERhdGFiYXNlKS4KICAgICAgICAvLy8gTmV2ZXIgd2lwZSBwYXRoIGJhY2t1cHMgd2hl" +
            "biBzcHJpdGVzIGFyZSB0ZW1wb3JhcmlseSBudWxsIOKAlCBQZXJzaXN0IHN5bmNzCiAgICAgICAgLy8vIHBhdGhzIG9ubHkgd2hl" +
            "biBhIGxpdmUgc3ByaXRlIGlzIHByZXNlbnQuCiAgICAgICAgLy8vIDwvc3VtbWFyeT4KICAgICAgICBwdWJsaWMgaW50IFJlbWF0" +
            "ZXJpYWxpemVTcHJpdGVzRnJvbVBhdGhzKCkKICAgICAgICB7CiAgICAgICAgICAgIEVuc3VyZUxpc3RzKCk7CiAgICAgICAgICAg" +
            "IGludCBuID0gMDsKICAgICAgICAgICAgbiArPSBSZW1hdGVyaWFsaXplTGlzdChwZWFzYW50U3ByaXRlcywgcGVhc2FudEFzc2V0" +
            "UGF0aHMpOwogICAgICAgICAgICBuICs9IFJlbWF0ZXJpYWxpemVMaXN0KHdhcnJpb3JTcHJpdGVzLCB3YXJyaW9yQXNzZXRQYXRo" +
            "cyk7CiAgICAgICAgICAgIG4gKz0gUmVtYXRlcmlhbGl6ZUxpc3Qoa25pZ2h0U3ByaXRlcywga25pZ2h0QXNzZXRQYXRocyk7CiAg" +
            "ICAgICAgICAgIHJldHVybiBuOwogICAgICAgIH0KCiAgICAgICAgcHVibGljIHN0YXRpYyBpbnQgUmVtYXRlcmlhbGl6ZUxpc3Qo" +
            "TGlzdDxTcHJpdGU+IHNwcml0ZXMsIExpc3Q8c3RyaW5nPiBwYXRocykKICAgICAgICB7CiAgICAgICAgICAgIGlmIChzcHJpdGVz" +
            "ID09IG51bGwgfHwgcGF0aHMgPT0gbnVsbCkgcmV0dXJuIDA7CiAgICAgICAgICAgIGludCByZXN0b3JlZCA9IDA7CiAgICAgICAg" +
            "ICAgIGludCBjb3VudCA9IE1hdGhmLk1heChzcHJpdGVzLkNvdW50LCBwYXRocy5Db3VudCk7CiAgICAgICAgICAgIHdoaWxlIChz" +
            "cHJpdGVzLkNvdW50IDwgY291bnQpIHNwcml0ZXMuQWRkKG51bGwpOwogICAgICAgICAgICB3aGlsZSAocGF0aHMuQ291bnQgPCBj" +
            "b3VudCkgcGF0aHMuQWRkKCIiKTsKICAgICAgICAgICAgZm9yIChpbnQgaSA9IDA7IGkgPCBjb3VudDsgaSsrKQogICAgICAgICAg" +
            "ICB7CiAgICAgICAgICAgICAgICBpZiAoc3ByaXRlc1tpXSAhPSBudWxsKSBjb250aW51ZTsKICAgICAgICAgICAgICAgIGlmIChz" +
            "dHJpbmcuSXNOdWxsT3JFbXB0eShwYXRoc1tpXSkpIGNvbnRpbnVlOwogICAgICAgICAgICAgICAgU3ByaXRlIHNwID0gVHJ5TG9h" +
            "ZFNwcml0ZUF0UGF0aChwYXRoc1tpXSk7CiAgICAgICAgICAgICAgICBpZiAoc3AgPT0gbnVsbCkgY29udGludWU7CiAgICAgICAg" +
            "ICAgICAgICBzcHJpdGVzW2ldID0gc3A7CiAgICAgICAgICAgICAgICByZXN0b3JlZCsrOwogICAgICAgICAgICB9CiAgICAgICAg" +
            "ICAgIHJldHVybiByZXN0b3JlZDsKICAgICAgICB9CgogICAgICAgIHB1YmxpYyBzdGF0aWMgU3ByaXRlIFRyeUxvYWRTcHJpdGVB" +
            "dFBhdGgoc3RyaW5nIHBhdGgpCiAgICAgICAgewogICAgICAgICAgICBpZiAoc3RyaW5nLklzTnVsbE9yRW1wdHkocGF0aCkpIHJl" +
            "dHVybiBudWxsOwogICAgICAgICAgICBzdHJpbmcgbm9ybSA9IHBhdGguUmVwbGFjZSgiXFwiLCAiLyIpOwojaWYgVU5JVFlfRURJ" +
            "VE9SCiAgICAgICAgICAgIHRyeQogICAgICAgICAgICB7CiAgICAgICAgICAgICAgICB2YXIgc3AgPSBVbml0eUVkaXRvci5Bc3Nl" +
            "dERhdGFiYXNlLkxvYWRBc3NldEF0UGF0aDxTcHJpdGU+KG5vcm0pOwogICAgICAgICAgICAgICAgaWYgKHNwICE9IG51bGwpIHJl" +
            "dHVybiBzcDsKICAgICAgICAgICAgICAgIC8vIFRleHR1cmUgd2l0aCBtdWx0aXBsZSBzcHJpdGVzIOKAlCB0YWtlIGZpcnN0IHN1" +
            "Yi1hc3NldCBTcHJpdGUuCiAgICAgICAgICAgICAgICB2YXIgb2JqcyA9IFVuaXR5RWRpdG9yLkFzc2V0RGF0YWJhc2UuTG9hZEFs" +
            "bEFzc2V0c0F0UGF0aChub3JtKTsKICAgICAgICAgICAgICAgIGlmIChvYmpzICE9IG51bGwpCiAgICAgICAgICAgICAgICB7CiAg" +
            "ICAgICAgICAgICAgICAgICAgZm9yIChpbnQgaSA9IDA7IGkgPCBvYmpzLkxlbmd0aDsgaSsrKQogICAgICAgICAgICAgICAgICAg" +
            "ICAgICBpZiAob2Jqc1tpXSBpcyBTcHJpdGUgcykgcmV0dXJuIHM7CiAgICAgICAgICAgICAgICB9CiAgICAgICAgICAgIH0KICAg" +
            "ICAgICAgICAgY2F0Y2ggeyAvKiBpZ25vcmUgKi8gfQojZW5kaWYKICAgICAgICAgICAgLy8gUGxheWVyIGJ1aWxkczogUmVzb3Vy" +
            "Y2VzLkxvYWQgKHByb2ZpbGVzICsgcHJvbW90ZWQgQXJjaGl0ZWN0QXJ0IGNvcGllcykuCiAgICAgICAgICAgIHRyeQogICAgICAg" +
            "ICAgICB7CiAgICAgICAgICAgICAgICBpbnQgaWR4ID0gbm9ybS5JbmRleE9mKCJSZXNvdXJjZXMvIiwgU3lzdGVtLlN0cmluZ0Nv" +
            "bXBhcmlzb24uT3JkaW5hbElnbm9yZUNhc2UpOwogICAgICAgICAgICAgICAgaWYgKGlkeCA+PSAwKQogICAgICAgICAgICAgICAg" +
            "ewogICAgICAgICAgICAgICAgICAgIHN0cmluZyByZXMgPSBub3JtLlN1YnN0cmluZyhpZHggKyAiUmVzb3VyY2VzLyIuTGVuZ3Ro" +
            "KTsKICAgICAgICAgICAgICAgICAgICBpbnQgZG90ID0gcmVzLkxhc3RJbmRleE9mKCcuJyk7CiAgICAgICAgICAgICAgICAgICAg" +
            "aWYgKGRvdCA+IDApIHJlcyA9IHJlcy5TdWJzdHJpbmcoMCwgZG90KTsKICAgICAgICAgICAgICAgICAgICB2YXIgZnJvbVJlcyA9" +
            "IFJlc291cmNlcy5Mb2FkPFNwcml0ZT4ocmVzKTsKICAgICAgICAgICAgICAgICAgICBpZiAoZnJvbVJlcyAhPSBudWxsKSByZXR1" +
            "cm4gZnJvbVJlczsKICAgICAgICAgICAgICAgICAgICB2YXIgYWxsID0gUmVzb3VyY2VzLkxvYWRBbGw8U3ByaXRlPihyZXMpOwog" +
            "ICAgICAgICAgICAgICAgICAgIGlmIChhbGwgIT0gbnVsbCAmJiBhbGwuTGVuZ3RoID4gMCAmJiBhbGxbMF0gIT0gbnVsbCkgcmV0" +
            "dXJuIGFsbFswXTsKICAgICAgICAgICAgICAgIH0KICAgICAgICAgICAgICAgIC8vIEJhcmUgUmVzb3VyY2VzIGtleSAobm8gQXNz" +
            "ZXRzLyBwcmVmaXgpIOKAlCBBcmNoaXRlY3RBcnQvQm9keS9mb28KICAgICAgICAgICAgICAgIGlmICghbm9ybS5TdGFydHNXaXRo" +
            "KCJBc3NldHMvIiwgU3lzdGVtLlN0cmluZ0NvbXBhcmlzb24uT3JkaW5hbElnbm9yZUNhc2UpCiAgICAgICAgICAgICAgICAgICAg" +
            "JiYgbm9ybS5JbmRleE9mKCc6JykgPCAwKQogICAgICAgICAgICAgICAgewogICAgICAgICAgICAgICAgICAgIHN0cmluZyBrZXkg" +
            "PSBub3JtOwogICAgICAgICAgICAgICAgICAgIGludCBkb3QgPSBrZXkuTGFzdEluZGV4T2YoJy4nKTsKICAgICAgICAgICAgICAg" +
            "ICAgICBpZiAoZG90ID4gMCkga2V5ID0ga2V5LlN1YnN0cmluZygwLCBkb3QpOwogICAgICAgICAgICAgICAgICAgIHZhciBiYXJl" +
            "ID0gUmVzb3VyY2VzLkxvYWQ8U3ByaXRlPihrZXkpOwogICAgICAgICAgICAgICAgICAgIGlmIChiYXJlICE9IG51bGwpIHJldHVy" +
            "biBiYXJlOwogICAgICAgICAgICAgICAgfQogICAgICAgICAgICB9CiAgICAgICAgICAgIGNhdGNoIHsgLyogaWdub3JlICovIH0K" +
            "CiAgICAgICAgICAgIC8vIFN0cmVhbWluZ0Fzc2V0cyBmYWxsYmFjayAocG5nL2pwZyBjb3BpZWQgYnkgYnVpbGQgcHJlcGFyZSku" +
            "IFNvZnQtZmFpbCDigJQgbm8gd2lwZS4KICAgICAgICAgICAgdHJ5CiAgICAgICAgICAgIHsKICAgICAgICAgICAgICAgIHN0cmlu" +
            "ZyBmaWxlTmFtZSA9IFN5c3RlbS5JTy5QYXRoLkdldEZpbGVOYW1lKG5vcm0pOwogICAgICAgICAgICAgICAgaWYgKCFzdHJpbmcu" +
            "SXNOdWxsT3JFbXB0eShmaWxlTmFtZSkpCiAgICAgICAgICAgICAgICB7CiAgICAgICAgICAgICAgICAgICAgc3RyaW5nIHNhID0g" +
            "U3lzdGVtLklPLlBhdGguQ29tYmluZShBcHBsaWNhdGlvbi5zdHJlYW1pbmdBc3NldHNQYXRoLCAiRGVtb25LbmlnaHRBcmNoaXRl" +
            "Y3QiLCBmaWxlTmFtZSk7CiAgICAgICAgICAgICAgICAgICAgaWYgKFN5c3RlbS5JTy5GaWxlLkV4aXN0cyhzYSkpCiAgICAgICAg" +
            "ICAgICAgICAgICAgewogICAgICAgICAgICAgICAgICAgICAgICBieXRlW10gYnl0ZXMgPSBTeXN0ZW0uSU8uRmlsZS5SZWFkQWxs" +
            "Qnl0ZXMoc2EpOwogICAgICAgICAgICAgICAgICAgICAgICB2YXIgdGV4ID0gbmV3IFRleHR1cmUyRCgyLCAyLCBUZXh0dXJlRm9y" +
            "bWF0LlJHQkEzMiwgZmFsc2UpOwogICAgICAgICAgICAgICAgICAgICAgICBpZiAodGV4LkxvYWRJbWFnZShieXRlcykpCiAgICAg" +
            "ICAgICAgICAgICAgICAgICAgIHsKICAgICAgICAgICAgICAgICAgICAgICAgICAgIHRleC5uYW1lID0gU3lzdGVtLklPLlBhdGgu" +
            "R2V0RmlsZU5hbWVXaXRob3V0RXh0ZW5zaW9uKGZpbGVOYW1lKTsKICAgICAgICAgICAgICAgICAgICAgICAgICAgIHJldHVybiBT" +
            "cHJpdGUuQ3JlYXRlKAogICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgIHRleCwKICAgICAgICAgICAgICAgICAgICAgICAg" +
            "ICAgICAgICBuZXcgUmVjdCgwLCAwLCB0ZXgud2lkdGgsIHRleC5oZWlnaHQpLAogICAgICAgICAgICAgICAgICAgICAgICAgICAg" +
            "ICAgIG5ldyBWZWN0b3IyKDAuNWYsIDAuNWYpLAogICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgIDEwMGYpOwogICAgICAg" +
            "ICAgICAgICAgICAgICAgICB9CiAgICAgICAgICAgICAgICAgICAgfQogICAgICAgICAgICAgICAgfQogICAgICAgICAgICB9CiAg" +
            "ICAgICAgICAgIGNhdGNoIHsgLyogaWdub3JlICovIH0KICAgICAgICAgICAgcmV0dXJuIG51bGw7CiAgICAgICAgfQoKICAgICAg" +
            "ICBwdWJsaWMgTGlzdDxTcHJpdGU+IEdldFBvb2woVW5pdFR5cGUgdHlwZSkKICAgICAgICB7CiAgICAgICAgICAgIEVuc3VyZUxp" +
            "c3RzKCk7CiAgICAgICAgICAgIHJldHVybiB0eXBlIHN3aXRjaAogICAgICAgICAgICB7CiAgICAgICAgICAgICAgICBVbml0VHlw" +
            "ZS5QZWFzYW50ID0+IHBlYXNhbnRTcHJpdGVzLAogICAgICAgICAgICAgICAgVW5pdFR5cGUuV2FycmlvciA9PiB3YXJyaW9yU3By" +
            "aXRlcywKICAgICAgICAgICAgICAgIFVuaXRUeXBlLktuaWdodCA9PiBrbmlnaHRTcHJpdGVzLAogICAgICAgICAgICAgICAgXyA9" +
            "PiBudWxsCiAgICAgICAgICAgIH07CiAgICAgICAgfQoKICAgICAgICBwdWJsaWMgaW50IENvdW50VXNhYmxlKFVuaXRUeXBlIHR5" +
            "cGUpCiAgICAgICAgewogICAgICAgICAgICB2YXIgcG9vbCA9IEdldFBvb2wodHlwZSk7CiAgICAgICAgICAgIGlmIChwb29sID09" +
            "IG51bGwpIHJldHVybiAwOwogICAgICAgICAgICBFbnN1cmVMaXN0cygpOwogICAgICAgICAgICBMaXN0PHN0cmluZz4gcGF0aHMg" +
            "PSB0eXBlIHN3aXRjaAogICAgICAgICAgICB7CiAgICAgICAgICAgICAgICBVbml0VHlwZS5QZWFzYW50ID0+IHBlYXNhbnRBc3Nl" +
            "dFBhdGhzLAogICAgICAgICAgICAgICAgVW5pdFR5cGUuV2FycmlvciA9PiB3YXJyaW9yQXNzZXRQYXRocywKICAgICAgICAgICAg" +
            "ICAgIFVuaXRUeXBlLktuaWdodCA9PiBrbmlnaHRBc3NldFBhdGhzLAogICAgICAgICAgICAgICAgXyA9PiBudWxsCiAgICAgICAg" +
            "ICAgIH07CiAgICAgICAgICAgIGludCBuID0gMDsKICAgICAgICAgICAgaW50IGNvdW50ID0gcG9vbC5Db3VudDsKICAgICAgICAg" +
            "ICAgaWYgKHBhdGhzICE9IG51bGwgJiYgcGF0aHMuQ291bnQgPiBjb3VudCkgY291bnQgPSBwYXRocy5Db3VudDsKICAgICAgICAg" +
            "ICAgZm9yIChpbnQgaSA9IDA7IGkgPCBjb3VudDsgaSsrKQogICAgICAgICAgICB7CiAgICAgICAgICAgICAgICBpZiAoaSA8IHBv" +
            "b2wuQ291bnQgJiYgcG9vbFtpXSAhPSBudWxsKSB7IG4rKzsgY29udGludWU7IH0KICAgICAgICAgICAgICAgIC8vIFBhdGggYmFj" +
            "a3VwIGNvdW50cyBhcyB1c2FibGUg4oCUIFJlbWF0ZXJpYWxpemUgcmVzdG9yZXMgdGhlIE1pc3NpbmcgU3ByaXRlIHJlZi4KICAg" +
            "ICAgICAgICAgICAgIGlmIChwYXRocyAhPSBudWxsICYmIGkgPCBwYXRocy5Db3VudCAmJiAhc3RyaW5nLklzTnVsbE9yRW1wdHko" +
            "cGF0aHNbaV0pKSBuKys7CiAgICAgICAgICAgIH0KICAgICAgICAgICAgcmV0dXJuIG47CiAgICAgICAgfQogICAgfQp9Cg=="
            ;
        // Assets/Scripts/Data/LegendPewPortraitProfile.cs (9587 bytes)
        const string B64_LegendPewPortraitProfile =
            "dXNpbmcgU3lzdGVtOwp1c2luZyBTeXN0ZW0uQ29sbGVjdGlvbnMuR2VuZXJpYzsKdXNpbmcgVW5pdHlFbmdpbmU7CgpuYW1lc3Bh" +
            "Y2UgQXJ0aHVyaWFuQXV0b2JhdHRsZXIKewogICAgLy8gdGlwOiBmaXgtZ2R0LXByb2ZpbGUtYWx3YXlzLWNvbXBpbGUg4oCUIG5h" +
            "bWVkIE1vbm9TY3JpcHQgZm9yIFVuaXR5IFNPIGJpbmRpbmcKICAgIC8vLyA8c3VtbWFyeT5PbmUgTGVnZW5kIGZhY2UtcG9ydHJh" +
            "aXQgc2xvdCAoQXJjaGl0ZWN0IFRhYiA1KS48L3N1bW1hcnk+CiAgICBbU2VyaWFsaXphYmxlXQogICAgcHVibGljIGNsYXNzIExl" +
            "Z2VuZFBvcnRyYWl0RW50cnkKICAgIHsKICAgICAgICBwdWJsaWMgVW5pdFR5cGUgdW5pdFR5cGU7CiAgICAgICAgcHVibGljIHN0" +
            "cmluZyBkaXNwbGF5TmFtZTsKICAgICAgICBwdWJsaWMgU3ByaXRlIGZhY2VQb3J0cmFpdDsKICAgICAgICBwdWJsaWMgc3RyaW5n" +
            "IGFzc2V0UGF0aDsKICAgICAgICBbVG9vbHRpcCgiTWVybGluIGFuZCBvdGhlciBzb2Z0IHNsb3RzIHN0YXkgaW4gdGhlIGxpc3Qg" +
            "YnV0IGFyZSBub3QgcmVxdWlyZWQgZm9yIHBsYXlhYmxlIENvZGV4LiIpXQogICAgICAgIHB1YmxpYyBib29sIHNvZnRPcHRpb25h" +
            "bDsKICAgIH0KCiAgICAvLy8gPHN1bW1hcnk+CiAgICAvLy8gQXJjaGl0ZWN0IGxlZ2VuZCBmYWNlIHBvcnRyYWl0cyArIHRvd25z" +
            "Zm9sayBwZXcgRkFDRS1PTkxZIHByb2ZpbGUgcG9vbHMuCiAgICAvLy8gRGlzdGluY3QgZnJvbSBDbGFzc1Nwcml0ZVBvb2xQcm9m" +
            "aWxlIChmdWxsLWJvZHkgcGV3L2NvbWJhdCBhdmF0YXJzKS4gUmVzb3VyY2VzIHBhdGg6CiAgICAvLy8gTGVnZW5kUGV3UG9ydHJh" +
            "aXRQcm9maWxlLiBOZXZlciBhc3NpZ24gdGhlc2UgYXMgYm9keVNwcml0ZS4KICAgIC8vLyA8L3N1bW1hcnk+CiAgICBbQ3JlYXRl" +
            "QXNzZXRNZW51KGZpbGVOYW1lID0gIkxlZ2VuZFBld1BvcnRyYWl0UHJvZmlsZSIsIG1lbnVOYW1lID0gIkRlbW9uS25pZ2h0L0xl" +
            "Z2VuZCBQZXcgUG9ydHJhaXQgUHJvZmlsZSIpXQogICAgcHVibGljIGNsYXNzIExlZ2VuZFBld1BvcnRyYWl0UHJvZmlsZSA6IFNj" +
            "cmlwdGFibGVPYmplY3QKICAgIHsKICAgICAgICBwdWJsaWMgY29uc3Qgc3RyaW5nIFJlc291cmNlc1BhdGggPSAiTGVnZW5kUGV3" +
            "UG9ydHJhaXRQcm9maWxlIjsKCiAgICAgICAgLy8vIDxzdW1tYXJ5PjE0IHBsYXlhYmxlIGxlZ2VuZHMgKyBzb2Z0IE1lcmxpbiBz" +
            "bG90IChsYXN0KS48L3N1bW1hcnk+CiAgICAgICAgcHVibGljIHN0YXRpYyByZWFkb25seSBVbml0VHlwZVtdIENhbm9uaWNhbExl" +
            "Z2VuZFNsb3RzID0KICAgICAgICB7CiAgICAgICAgICAgIFVuaXRUeXBlLlNpcktheSwKICAgICAgICAgICAgVW5pdFR5cGUuU2ly" +
            "QmVkaXZlcmUsCiAgICAgICAgICAgIFVuaXRUeXBlLkxhZHlHdWluZXZlcmUsCiAgICAgICAgICAgIFVuaXRUeXBlLkFyY2hQcmll" +
            "c3RPZkdsYXN0b25idXJ5LAogICAgICAgICAgICBVbml0VHlwZS5TaXJHYXdhaW4sCiAgICAgICAgICAgIFVuaXRUeXBlLlNpclRy" +
            "aXN0YW4sCiAgICAgICAgICAgIFVuaXRUeXBlLlNpckxhbmNlbG90LAogICAgICAgICAgICBVbml0VHlwZS5LaW5nQXJ0aHVyLAog" +
            "ICAgICAgICAgICBVbml0VHlwZS5TaXJHYWxhaGFkLAogICAgICAgICAgICBVbml0VHlwZS5TaXJQZXJjaXZhbCwKICAgICAgICAg" +
            "ICAgVW5pdFR5cGUuTmltdWUsCiAgICAgICAgICAgIFVuaXRUeXBlLk1vcmRyZWQsCiAgICAgICAgICAgIFVuaXRUeXBlLk1vcmdh" +
            "bkxlRmF5LAogICAgICAgICAgICBVbml0VHlwZS5UaGVHcmVlbktuaWdodCwKICAgICAgICAgICAgVW5pdFR5cGUuTWVybGluLCAv" +
            "LyBzb2Z0IOKAlCBvdXQgb2YgcGxheWFibGUgQ29kZXggcG9vbAogICAgICAgIH07CgogICAgICAgIHB1YmxpYyBMaXN0PExlZ2Vu" +
            "ZFBvcnRyYWl0RW50cnk+IGxlZ2VuZFBvcnRyYWl0cyA9IG5ldyBMaXN0PExlZ2VuZFBvcnRyYWl0RW50cnk+KCk7CgogICAgICAg" +
            "IFtIZWFkZXIoIlRvd25zZm9sayBwZXcgRkFDRSBwcm9maWxlcyBvbmx5IChuZXZlciBib2R5U3ByaXRlIOKAlCBkaXN0aW5jdCBm" +
            "cm9tIFRhYiA0IENsYXNzU3ByaXRlUG9vbCkiKV0KICAgICAgICBwdWJsaWMgTGlzdDxTcHJpdGU+IHBlYXNhbnRGYWNlUHJvZmls" +
            "ZXMgPSBuZXcgTGlzdDxTcHJpdGU+KCk7CiAgICAgICAgcHVibGljIExpc3Q8U3ByaXRlPiB3YXJyaW9yRmFjZVByb2ZpbGVzID0g" +
            "bmV3IExpc3Q8U3ByaXRlPigpOwogICAgICAgIHB1YmxpYyBMaXN0PFNwcml0ZT4ga25pZ2h0RmFjZVByb2ZpbGVzID0gbmV3IExp" +
            "c3Q8U3ByaXRlPigpOwoKICAgICAgICBwdWJsaWMgTGlzdDxzdHJpbmc+IHBlYXNhbnRGYWNlQXNzZXRQYXRocyA9IG5ldyBMaXN0" +
            "PHN0cmluZz4oKTsKICAgICAgICBwdWJsaWMgTGlzdDxzdHJpbmc+IHdhcnJpb3JGYWNlQXNzZXRQYXRocyA9IG5ldyBMaXN0PHN0" +
            "cmluZz4oKTsKICAgICAgICBwdWJsaWMgTGlzdDxzdHJpbmc+IGtuaWdodEZhY2VBc3NldFBhdGhzID0gbmV3IExpc3Q8c3RyaW5n" +
            "PigpOwoKICAgICAgICBbU3lzdGVtLk5vblNlcmlhbGl6ZWRdIHB1YmxpYyBib29sIGlzUnVudGltZVN0YW5kSW47CgogICAgICAg" +
            "IHB1YmxpYyBzdGF0aWMgTGVnZW5kUGV3UG9ydHJhaXRQcm9maWxlIExvYWQoKQogICAgICAgIHsKICAgICAgICAgICAgTGVnZW5k" +
            "UGV3UG9ydHJhaXRQcm9maWxlIHAgPSBudWxsOwogICAgICAgICAgICB0cnkgeyBwID0gUmVzb3VyY2VzLkxvYWQ8TGVnZW5kUGV3" +
            "UG9ydHJhaXRQcm9maWxlPihSZXNvdXJjZXNQYXRoKTsgfQogICAgICAgICAgICBjYXRjaCB7IC8qIG1pc3NpbmcgTW9ub1Njcmlw" +
            "dCBHVUlEL2ZpbGVJRCBhZnRlciBlbWJlZCDigJQgZmFsbCB0aHJvdWdoICovIH0KICAgICAgICAgICAgaWYgKHAgIT0gbnVsbCkK" +
            "ICAgICAgICAgICAgewogICAgICAgICAgICAgICAgcC5FbnN1cmVTbG90c0FuZExpc3RzKCk7CiAgICAgICAgICAgICAgICBwLmlz" +
            "UnVudGltZVN0YW5kSW4gPSBmYWxzZTsKICAgICAgICAgICAgICAgIHAuUmVtYXRlcmlhbGl6ZVNwcml0ZXNGcm9tUGF0aHMoKTsK" +
            "ICAgICAgICAgICAgICAgIHJldHVybiBwOwogICAgICAgICAgICB9CiAgICAgICAgICAgIHRyeQogICAgICAgICAgICB7CiAgICAg" +
            "ICAgICAgICAgICB2YXIgYWxsID0gUmVzb3VyY2VzLkxvYWRBbGw8TGVnZW5kUGV3UG9ydHJhaXRQcm9maWxlPigiIik7CiAgICAg" +
            "ICAgICAgICAgICBpZiAoYWxsICE9IG51bGwpCiAgICAgICAgICAgICAgICB7CiAgICAgICAgICAgICAgICAgICAgZm9yIChpbnQg" +
            "aSA9IDA7IGkgPCBhbGwuTGVuZ3RoOyBpKyspCiAgICAgICAgICAgICAgICAgICAgewogICAgICAgICAgICAgICAgICAgICAgICBp" +
            "ZiAoYWxsW2ldID09IG51bGwpIGNvbnRpbnVlOwogICAgICAgICAgICAgICAgICAgICAgICBhbGxbaV0uRW5zdXJlU2xvdHNBbmRM" +
            "aXN0cygpOwogICAgICAgICAgICAgICAgICAgICAgICBhbGxbaV0uaXNSdW50aW1lU3RhbmRJbiA9IGZhbHNlOwogICAgICAgICAg" +
            "ICAgICAgICAgICAgICBhbGxbaV0uUmVtYXRlcmlhbGl6ZVNwcml0ZXNGcm9tUGF0aHMoKTsKICAgICAgICAgICAgICAgICAgICAg" +
            "ICAgcmV0dXJuIGFsbFtpXTsKICAgICAgICAgICAgICAgICAgICB9CiAgICAgICAgICAgICAgICB9CiAgICAgICAgICAgIH0KICAg" +
            "ICAgICAgICAgY2F0Y2ggeyAvKiBicm9rZW4gUmVzb3VyY2VzIGFzc2V0IC8gbWlzc2luZyBzY3JpcHQg4oCUIHNvZnQgc3RhbmQt" +
            "aW4gKi8gfQogICAgICAgICAgICBwID0gQ3JlYXRlSW5zdGFuY2U8TGVnZW5kUGV3UG9ydHJhaXRQcm9maWxlPigpOwogICAgICAg" +
            "ICAgICBwLm5hbWUgPSAiTGVnZW5kUGV3UG9ydHJhaXRQcm9maWxlX1J1bnRpbWUiOwogICAgICAgICAgICBwLmlzUnVudGltZVN0" +
            "YW5kSW4gPSB0cnVlOwogICAgICAgICAgICBwLkVuc3VyZVNsb3RzQW5kTGlzdHMoKTsKICAgICAgICAgICAgcmV0dXJuIHA7CiAg" +
            "ICAgICAgfQoKICAgICAgICAvLy8gPHN1bW1hcnk+UmVzdG9yZSBNaXNzaW5nIGZhY2UvcG9vbCBzcHJpdGVzIGZyb20gcGF0aCBi" +
            "YWNrdXBzIChlZGl0b3IgKyBSZXNvdXJjZXMpLjwvc3VtbWFyeT4KICAgICAgICBwdWJsaWMgaW50IFJlbWF0ZXJpYWxpemVTcHJp" +
            "dGVzRnJvbVBhdGhzKCkKICAgICAgICB7CiAgICAgICAgICAgIEVuc3VyZVNsb3RzQW5kTGlzdHMoKTsKICAgICAgICAgICAgaW50" +
            "IG4gPSAwOwogICAgICAgICAgICBuICs9IENsYXNzU3ByaXRlUG9vbFByb2ZpbGUuUmVtYXRlcmlhbGl6ZUxpc3QocGVhc2FudEZh" +
            "Y2VQcm9maWxlcywgcGVhc2FudEZhY2VBc3NldFBhdGhzKTsKICAgICAgICAgICAgbiArPSBDbGFzc1Nwcml0ZVBvb2xQcm9maWxl" +
            "LlJlbWF0ZXJpYWxpemVMaXN0KHdhcnJpb3JGYWNlUHJvZmlsZXMsIHdhcnJpb3JGYWNlQXNzZXRQYXRocyk7CiAgICAgICAgICAg" +
            "IG4gKz0gQ2xhc3NTcHJpdGVQb29sUHJvZmlsZS5SZW1hdGVyaWFsaXplTGlzdChrbmlnaHRGYWNlUHJvZmlsZXMsIGtuaWdodEZh" +
            "Y2VBc3NldFBhdGhzKTsKICAgICAgICAgICAgaWYgKGxlZ2VuZFBvcnRyYWl0cyAhPSBudWxsKQogICAgICAgICAgICB7CiAgICAg" +
            "ICAgICAgICAgICBmb3IgKGludCBpID0gMDsgaSA8IGxlZ2VuZFBvcnRyYWl0cy5Db3VudDsgaSsrKQogICAgICAgICAgICAgICAg" +
            "ewogICAgICAgICAgICAgICAgICAgIHZhciBlID0gbGVnZW5kUG9ydHJhaXRzW2ldOwogICAgICAgICAgICAgICAgICAgIGlmIChl" +
            "ID09IG51bGwgfHwgZS5mYWNlUG9ydHJhaXQgIT0gbnVsbCkgY29udGludWU7CiAgICAgICAgICAgICAgICAgICAgaWYgKHN0cmlu" +
            "Zy5Jc051bGxPckVtcHR5KGUuYXNzZXRQYXRoKSkgY29udGludWU7CiAgICAgICAgICAgICAgICAgICAgU3ByaXRlIHNwID0gQ2xh" +
            "c3NTcHJpdGVQb29sUHJvZmlsZS5UcnlMb2FkU3ByaXRlQXRQYXRoKGUuYXNzZXRQYXRoKTsKICAgICAgICAgICAgICAgICAgICBp" +
            "ZiAoc3AgPT0gbnVsbCkgY29udGludWU7CiAgICAgICAgICAgICAgICAgICAgZS5mYWNlUG9ydHJhaXQgPSBzcDsKICAgICAgICAg" +
            "ICAgICAgICAgICBuKys7CiAgICAgICAgICAgICAgICB9CiAgICAgICAgICAgIH0KICAgICAgICAgICAgcmV0dXJuIG47CiAgICAg" +
            "ICAgfQoKICAgICAgICBwdWJsaWMgdm9pZCBFbnN1cmVTbG90c0FuZExpc3RzKCkKICAgICAgICB7CiAgICAgICAgICAgIGxlZ2Vu" +
            "ZFBvcnRyYWl0cyA/Pz0gbmV3IExpc3Q8TGVnZW5kUG9ydHJhaXRFbnRyeT4oKTsKICAgICAgICAgICAgcGVhc2FudEZhY2VQcm9m" +
            "aWxlcyA/Pz0gbmV3IExpc3Q8U3ByaXRlPigpOwogICAgICAgICAgICB3YXJyaW9yRmFjZVByb2ZpbGVzID8/PSBuZXcgTGlzdDxT" +
            "cHJpdGU+KCk7CiAgICAgICAgICAgIGtuaWdodEZhY2VQcm9maWxlcyA/Pz0gbmV3IExpc3Q8U3ByaXRlPigpOwogICAgICAgICAg" +
            "ICBwZWFzYW50RmFjZUFzc2V0UGF0aHMgPz89IG5ldyBMaXN0PHN0cmluZz4oKTsKICAgICAgICAgICAgd2FycmlvckZhY2VBc3Nl" +
            "dFBhdGhzID8/PSBuZXcgTGlzdDxzdHJpbmc+KCk7CiAgICAgICAgICAgIGtuaWdodEZhY2VBc3NldFBhdGhzID8/PSBuZXcgTGlz" +
            "dDxzdHJpbmc+KCk7CgogICAgICAgICAgICBmb3IgKGludCBpID0gMDsgaSA8IENhbm9uaWNhbExlZ2VuZFNsb3RzLkxlbmd0aDsg" +
            "aSsrKQogICAgICAgICAgICB7CiAgICAgICAgICAgICAgICBVbml0VHlwZSB3YW50ID0gQ2Fub25pY2FsTGVnZW5kU2xvdHNbaV07" +
            "CiAgICAgICAgICAgICAgICBMZWdlbmRQb3J0cmFpdEVudHJ5IGZvdW5kID0gbnVsbDsKICAgICAgICAgICAgICAgIGZvciAoaW50" +
            "IGogPSAwOyBqIDwgbGVnZW5kUG9ydHJhaXRzLkNvdW50OyBqKyspCiAgICAgICAgICAgICAgICB7CiAgICAgICAgICAgICAgICAg" +
            "ICAgaWYgKGxlZ2VuZFBvcnRyYWl0c1tqXSAhPSBudWxsICYmIGxlZ2VuZFBvcnRyYWl0c1tqXS51bml0VHlwZSA9PSB3YW50KQog" +
            "ICAgICAgICAgICAgICAgICAgIHsKICAgICAgICAgICAgICAgICAgICAgICAgZm91bmQgPSBsZWdlbmRQb3J0cmFpdHNbal07CiAg" +
            "ICAgICAgICAgICAgICAgICAgICAgIGJyZWFrOwogICAgICAgICAgICAgICAgICAgIH0KICAgICAgICAgICAgICAgIH0KICAgICAg" +
            "ICAgICAgICAgIGlmIChmb3VuZCA9PSBudWxsKQogICAgICAgICAgICAgICAgewogICAgICAgICAgICAgICAgICAgIGZvdW5kID0g" +
            "bmV3IExlZ2VuZFBvcnRyYWl0RW50cnkKICAgICAgICAgICAgICAgICAgICB7CiAgICAgICAgICAgICAgICAgICAgICAgIHVuaXRU" +
            "eXBlID0gd2FudCwKICAgICAgICAgICAgICAgICAgICAgICAgZGlzcGxheU5hbWUgPSBEZWZhdWx0RGlzcGxheU5hbWUod2FudCks" +
            "CiAgICAgICAgICAgICAgICAgICAgICAgIHNvZnRPcHRpb25hbCA9IHdhbnQgPT0gVW5pdFR5cGUuTWVybGluCiAgICAgICAgICAg" +
            "ICAgICAgICAgfTsKICAgICAgICAgICAgICAgICAgICBsZWdlbmRQb3J0cmFpdHMuQWRkKGZvdW5kKTsKICAgICAgICAgICAgICAg" +
            "IH0KICAgICAgICAgICAgICAgIGVsc2UKICAgICAgICAgICAgICAgIHsKICAgICAgICAgICAgICAgICAgICBpZiAoc3RyaW5nLklz" +
            "TnVsbE9yRW1wdHkoZm91bmQuZGlzcGxheU5hbWUpKQogICAgICAgICAgICAgICAgICAgICAgICBmb3VuZC5kaXNwbGF5TmFtZSA9" +
            "IERlZmF1bHREaXNwbGF5TmFtZSh3YW50KTsKICAgICAgICAgICAgICAgICAgICBmb3VuZC5zb2Z0T3B0aW9uYWwgPSB3YW50ID09" +
            "IFVuaXRUeXBlLk1lcmxpbjsKICAgICAgICAgICAgICAgIH0KICAgICAgICAgICAgfQogICAgICAgIH0KCiAgICAgICAgcHVibGlj" +
            "IHN0YXRpYyBzdHJpbmcgRGVmYXVsdERpc3BsYXlOYW1lKFVuaXRUeXBlIHQpID0+IHQgc3dpdGNoCiAgICAgICAgewogICAgICAg" +
            "ICAgICBVbml0VHlwZS5LaW5nQXJ0aHVyID0+ICJLaW5nIEFydGh1ciIsCiAgICAgICAgICAgIFVuaXRUeXBlLlNpckxhbmNlbG90" +
            "ID0+ICJTaXIgTGFuY2Vsb3QiLAogICAgICAgICAgICBVbml0VHlwZS5Nb3JnYW5MZUZheSA9PiAiTW9yZ2FuIGxlIEZheSIsCiAg" +
            "ICAgICAgICAgIFVuaXRUeXBlLkxhZHlPZlRoZUxha2UgPT4gIkxhZHkgb2YgdGhlIExha2UiLAogICAgICAgICAgICBVbml0VHlw" +
            "ZS5TaXJHYXdhaW4gPT4gIlNpciBHYXdhaW4iLAogICAgICAgICAgICBVbml0VHlwZS5TaXJHYWxhaGFkID0+ICJTaXIgR2FsYWhh" +
            "ZCIsCiAgICAgICAgICAgIFVuaXRUeXBlLlNpclRyaXN0YW4gPT4gIlNpciBUcmlzdGFuIiwKICAgICAgICAgICAgVW5pdFR5cGUu" +
            "U2lyUGVyY2l2YWwgPT4gIlNpciBQZXJjaXZhbCIsCiAgICAgICAgICAgIFVuaXRUeXBlLlNpcktheSA9PiAiU2lyIEtheSIsCiAg" +
            "ICAgICAgICAgIFVuaXRUeXBlLlNpckJlZGl2ZXJlID0+ICJTaXIgQmVkaXZlcmUiLAogICAgICAgICAgICBVbml0VHlwZS5MYWR5" +
            "R3VpbmV2ZXJlID0+ICJMYWR5IEd1aW5ldmVyZSIsCiAgICAgICAgICAgIFVuaXRUeXBlLkd1aW5ldmVyZSA9PiAiTGFkeSBHdWlu" +
            "ZXZlcmUiLAogICAgICAgICAgICBVbml0VHlwZS5BcmNoUHJpZXN0T2ZHbGFzdG9uYnVyeSA9PiAiQXJjaC1Qcmllc3Qgb2YgR2xh" +
            "c3RvbmJ1cnkiLAogICAgICAgICAgICBVbml0VHlwZS5Qcmllc3QgPT4gIkFyY2gtUHJpZXN0IG9mIEdsYXN0b25idXJ5IiwKICAg" +
            "ICAgICAgICAgVW5pdFR5cGUuVGhlR3JlZW5LbmlnaHQgPT4gIlRoZSBHcmVlbiBLbmlnaHQiLAogICAgICAgICAgICBVbml0VHlw" +
            "ZS5NZXJsaW4gPT4gIk1lcmxpbiAoc29mdCkiLAogICAgICAgICAgICBfID0+IHQuVG9TdHJpbmcoKQogICAgICAgIH07CgogICAg" +
            "ICAgIC8vLyA8c3VtbWFyeT5Ob3JtYWxpemUgYWxpYXNlcyBzbyBHdWluZXZlcmUvUHJpZXN0IG1hcCBvbnRvIGNhbm9uaWNhbCBz" +
            "bG90cy48L3N1bW1hcnk+CiAgICAgICAgcHVibGljIHN0YXRpYyBVbml0VHlwZSBOb3JtYWxpemVMZWdlbmRTbG90KFVuaXRUeXBl" +
            "IHR5cGUpID0+IHR5cGUgc3dpdGNoCiAgICAgICAgewogICAgICAgICAgICBVbml0VHlwZS5HdWluZXZlcmUgPT4gVW5pdFR5cGUu" +
            "TGFkeUd1aW5ldmVyZSwKICAgICAgICAgICAgVW5pdFR5cGUuUHJpZXN0ID0+IFVuaXRUeXBlLkFyY2hQcmllc3RPZkdsYXN0b25i" +
            "dXJ5LAogICAgICAgICAgICBfID0+IHR5cGUKICAgICAgICB9OwoKICAgICAgICBwdWJsaWMgTGVnZW5kUG9ydHJhaXRFbnRyeSBG" +
            "aW5kTGVnZW5kRW50cnkoVW5pdFR5cGUgdHlwZSkKICAgICAgICB7CiAgICAgICAgICAgIEVuc3VyZVNsb3RzQW5kTGlzdHMoKTsK" +
            "ICAgICAgICAgICAgVW5pdFR5cGUgd2FudCA9IE5vcm1hbGl6ZUxlZ2VuZFNsb3QodHlwZSk7CiAgICAgICAgICAgIGZvciAoaW50" +
            "IGkgPSAwOyBpIDwgbGVnZW5kUG9ydHJhaXRzLkNvdW50OyBpKyspCiAgICAgICAgICAgIHsKICAgICAgICAgICAgICAgIHZhciBl" +
            "ID0gbGVnZW5kUG9ydHJhaXRzW2ldOwogICAgICAgICAgICAgICAgaWYgKGUgIT0gbnVsbCAmJiBlLnVuaXRUeXBlID09IHdhbnQp" +
            "IHJldHVybiBlOwogICAgICAgICAgICB9CiAgICAgICAgICAgIHJldHVybiBudWxsOwogICAgICAgIH0KCiAgICAgICAgcHVibGlj" +
            "IFNwcml0ZSBHZXRMZWdlbmRGYWNlKFVuaXRUeXBlIHR5cGUpCiAgICAgICAgewogICAgICAgICAgICB2YXIgZSA9IEZpbmRMZWdl" +
            "bmRFbnRyeSh0eXBlKTsKICAgICAgICAgICAgcmV0dXJuIGUgIT0gbnVsbCA/IGUuZmFjZVBvcnRyYWl0IDogbnVsbDsKICAgICAg" +
            "ICB9CgogICAgICAgIHB1YmxpYyBMaXN0PFNwcml0ZT4gR2V0UGV3RmFjZVBvb2woVW5pdFR5cGUgdHlwZSkKICAgICAgICB7CiAg" +
            "ICAgICAgICAgIEVuc3VyZVNsb3RzQW5kTGlzdHMoKTsKICAgICAgICAgICAgcmV0dXJuIHR5cGUgc3dpdGNoCiAgICAgICAgICAg" +
            "IHsKICAgICAgICAgICAgICAgIFVuaXRUeXBlLlBlYXNhbnQgPT4gcGVhc2FudEZhY2VQcm9maWxlcywKICAgICAgICAgICAgICAg" +
            "IFVuaXRUeXBlLldhcnJpb3IgPT4gd2FycmlvckZhY2VQcm9maWxlcywKICAgICAgICAgICAgICAgIFVuaXRUeXBlLktuaWdodCA9" +
            "PiBrbmlnaHRGYWNlUHJvZmlsZXMsCiAgICAgICAgICAgICAgICBfID0+IG51bGwKICAgICAgICAgICAgfTsKICAgICAgICB9Cgog" +
            "ICAgICAgIHB1YmxpYyBpbnQgQ291bnRVc2FibGVQZXdGYWNlcyhVbml0VHlwZSB0eXBlKQogICAgICAgIHsKICAgICAgICAgICAg" +
            "dmFyIHBvb2wgPSBHZXRQZXdGYWNlUG9vbCh0eXBlKTsKICAgICAgICAgICAgaWYgKHBvb2wgPT0gbnVsbCkgcmV0dXJuIDA7CiAg" +
            "ICAgICAgICAgIGludCBuID0gMDsKICAgICAgICAgICAgZm9yIChpbnQgaSA9IDA7IGkgPCBwb29sLkNvdW50OyBpKyspCiAgICAg" +
            "ICAgICAgICAgICBpZiAocG9vbFtpXSAhPSBudWxsKSBuKys7CiAgICAgICAgICAgIHJldHVybiBuOwogICAgICAgIH0KCiAgICAg" +
            "ICAgcHVibGljIGludCBDb3VudEFzc2lnbmVkTGVnZW5kRmFjZXMoKQogICAgICAgIHsKICAgICAgICAgICAgRW5zdXJlU2xvdHNB" +
            "bmRMaXN0cygpOwogICAgICAgICAgICBpbnQgbiA9IDA7CiAgICAgICAgICAgIGZvciAoaW50IGkgPSAwOyBpIDwgbGVnZW5kUG9y" +
            "dHJhaXRzLkNvdW50OyBpKyspCiAgICAgICAgICAgIHsKICAgICAgICAgICAgICAgIHZhciBlID0gbGVnZW5kUG9ydHJhaXRzW2ld" +
            "OwogICAgICAgICAgICAgICAgaWYgKGUgIT0gbnVsbCAmJiBlLmZhY2VQb3J0cmFpdCAhPSBudWxsICYmICFlLnNvZnRPcHRpb25h" +
            "bCkgbisrOwogICAgICAgICAgICB9CiAgICAgICAgICAgIHJldHVybiBuOwogICAgICAgIH0KICAgIH0KfQo="
            ;
        // Assets/Scripts/Data/BattleGoodEvilSpritePoolProfile.cs (13776 bytes)
        const string B64_BattleGoodEvilSpritePoolProfile =
            "dXNpbmcgU3lzdGVtOwp1c2luZyBTeXN0ZW0uQ29sbGVjdGlvbnMuR2VuZXJpYzsKdXNpbmcgVW5pdHlFbmdpbmU7CgpuYW1lc3Bh" +
            "Y2UgQXJ0aHVyaWFuQXV0b2JhdHRsZXIKewogICAgLy8gdGlwOiBmaXgtZ2R0LXByb2ZpbGUtYWx3YXlzLWNvbXBpbGUg4oCUIG5h" +
            "bWVkIE1vbm9TY3JpcHQgU08gb25seSAoQmF0dGxlU3ByaXRlU2xvdEVudHJ5IGxpdmVzIGluIEdhbWVEYXRhVHlwZXMpCiAgICAv" +
            "Ly8gPHN1bW1hcnk+CiAgICAvLy8gQXJjaGl0ZWN0IFRhYiA2IOKAlCBBdXRvYmF0dGxlIEdvb2QgdnMgRXZpbCBiYXR0bGUgc3By" +
            "aXRlcyBmb3IgVW5pdFZpc3VhbEFjdG9yLgogICAgLy8vIERpc3RpbmN0IGZyb20gVGFiIDQgcGV3IEJPRFkgcG9vbHMgYW5kIFRh" +
            "YiA1IEZBQ0UgcG9ydHJhaXRzLiBSZXNvdXJjZXMgcGF0aDoKICAgIC8vLyBCYXR0bGVHb29kRXZpbFNwcml0ZVBvb2xQcm9maWxl" +
            "LiB0aXA6IGJhdHRsZS1nb29kLWV2aWwtc3ByaXRlLXBvb2xzCiAgICAvLy8gPC9zdW1tYXJ5PgogICAgW0NyZWF0ZUFzc2V0TWVu" +
            "dShmaWxlTmFtZSA9ICJCYXR0bGVHb29kRXZpbFNwcml0ZVBvb2xQcm9maWxlIiwgbWVudU5hbWUgPSAiRGVtb25LbmlnaHQvQmF0" +
            "dGxlIEdvb2QgRXZpbCBTcHJpdGUgUG9vbCBQcm9maWxlIildCiAgICBwdWJsaWMgY2xhc3MgQmF0dGxlR29vZEV2aWxTcHJpdGVQ" +
            "b29sUHJvZmlsZSA6IFNjcmlwdGFibGVPYmplY3QKICAgIHsKICAgICAgICBwdWJsaWMgY29uc3Qgc3RyaW5nIFJlc291cmNlc1Bh" +
            "dGggPSAiQmF0dGxlR29vZEV2aWxTcHJpdGVQb29sUHJvZmlsZSI7CgogICAgICAgIC8vLyA8c3VtbWFyeT4xNCBwbGF5YWJsZSBs" +
            "ZWdlbmRzICsgc29mdCBNZXJsaW4gKHNhbWUgb3JkZXIgYXMgVGFiIDUgQ2Fub25pY2FsTGVnZW5kU2xvdHMpLjwvc3VtbWFyeT4K" +
            "ICAgICAgICBwdWJsaWMgc3RhdGljIHJlYWRvbmx5IFVuaXRUeXBlW10gQ2Fub25pY2FsR29vZExlZ2VuZFNsb3RzID0gTGVnZW5k" +
            "UGV3UG9ydHJhaXRQcm9maWxlLkNhbm9uaWNhbExlZ2VuZFNsb3RzOwoKICAgICAgICAvLy8gPHN1bW1hcnk+V2F2ZSAz4oCTMTAr" +
            "IGJvc3NlcyAobG9yZS1ib3NzLXdhdmUtb3JkZXIpLjwvc3VtbWFyeT4KICAgICAgICBwdWJsaWMgc3RhdGljIHJlYWRvbmx5IFVu" +
            "aXRUeXBlW10gQ2Fub25pY2FsQm9zc1Nsb3RzID0KICAgICAgICB7CiAgICAgICAgICAgIFVuaXRUeXBlLkJvc3NCbGFja0tuaWdo" +
            "dCwgICAvLyBXYXZlIDMKICAgICAgICAgICAgVW5pdFR5cGUuQm9zc01lbGVhZ2FudCwgICAgIC8vIFdhdmUgNAogICAgICAgICAg" +
            "ICBVbml0VHlwZS5Cb3NzS2luZ0xvdCwgICAgICAgLy8gV2F2ZSA1CiAgICAgICAgICAgIFVuaXRUeXBlLkJvc3NWb3J0aWdlcm4s" +
            "ICAgICAvLyBXYXZlIDYKICAgICAgICAgICAgVW5pdFR5cGUuQm9zc0FjY29sb24sICAgICAgIC8vIFdhdmUgNwogICAgICAgICAg" +
            "ICBVbml0VHlwZS5Cb3NzUXVlc3RpbmdCZWFzdCwgLy8gV2F2ZSA4CiAgICAgICAgICAgIFVuaXRUeXBlLkJvc3NHcmVlbktuaWdo" +
            "dCwgICAvLyBXYXZlIDkKICAgICAgICAgICAgVW5pdFR5cGUuQm9zc05pbXVlLCAgICAgICAgIC8vIFdhdmUgMTArCiAgICAgICAg" +
            "fTsKCiAgICAgICAgW0hlYWRlcigiR29vZCDigJQgdHJvb3AgYmF0dGxlIHBvb2xzIChtdWx0aS1zcHJpdGUpIildCiAgICAgICAg" +
            "cHVibGljIExpc3Q8U3ByaXRlPiBwZWFzYW50QmF0dGxlU3ByaXRlcyA9IG5ldyBMaXN0PFNwcml0ZT4oKTsKICAgICAgICBwdWJs" +
            "aWMgTGlzdDxTcHJpdGU+IHdhcnJpb3JCYXR0bGVTcHJpdGVzID0gbmV3IExpc3Q8U3ByaXRlPigpOwogICAgICAgIHB1YmxpYyBM" +
            "aXN0PFNwcml0ZT4ga25pZ2h0QmF0dGxlU3ByaXRlcyA9IG5ldyBMaXN0PFNwcml0ZT4oKTsKICAgICAgICBwdWJsaWMgTGlzdDxT" +
            "cHJpdGU+IHByaWVzdEJhdHRsZVNwcml0ZXMgPSBuZXcgTGlzdDxTcHJpdGU+KCk7CgogICAgICAgIHB1YmxpYyBMaXN0PHN0cmlu" +
            "Zz4gcGVhc2FudEJhdHRsZUFzc2V0UGF0aHMgPSBuZXcgTGlzdDxzdHJpbmc+KCk7CiAgICAgICAgcHVibGljIExpc3Q8c3RyaW5n" +
            "PiB3YXJyaW9yQmF0dGxlQXNzZXRQYXRocyA9IG5ldyBMaXN0PHN0cmluZz4oKTsKICAgICAgICBwdWJsaWMgTGlzdDxzdHJpbmc+" +
            "IGtuaWdodEJhdHRsZUFzc2V0UGF0aHMgPSBuZXcgTGlzdDxzdHJpbmc+KCk7CiAgICAgICAgcHVibGljIExpc3Q8c3RyaW5nPiBw" +
            "cmllc3RCYXR0bGVBc3NldFBhdGhzID0gbmV3IExpc3Q8c3RyaW5nPigpOwoKICAgICAgICBbSGVhZGVyKCJHb29kIOKAlCBwbGF5" +
            "YWJsZSBMZWdlbmQgYmF0dGxlIHNsb3RzIChzaW5nbGUgc3ByaXRlIGVhY2gpIildCiAgICAgICAgcHVibGljIExpc3Q8QmF0dGxl" +
            "U3ByaXRlU2xvdEVudHJ5PiBnb29kTGVnZW5kU2xvdHMgPSBuZXcgTGlzdDxCYXR0bGVTcHJpdGVTbG90RW50cnk+KCk7CgogICAg" +
            "ICAgIFtIZWFkZXIoIkV2aWwg4oCUIHdhdmUtdW5pdCBiYXR0bGUgcG9vbHMiKV0KICAgICAgICBwdWJsaWMgTGlzdDxTcHJpdGU+" +
            "IGRlbW9uSW1wQmF0dGxlU3ByaXRlcyA9IG5ldyBMaXN0PFNwcml0ZT4oKTsKICAgICAgICBwdWJsaWMgTGlzdDxTcHJpdGU+IGhl" +
            "bGxob3VuZEJhdHRsZVNwcml0ZXMgPSBuZXcgTGlzdDxTcHJpdGU+KCk7CiAgICAgICAgcHVibGljIExpc3Q8U3ByaXRlPiBkcmVh" +
            "ZExvcmRCYXR0bGVTcHJpdGVzID0gbmV3IExpc3Q8U3ByaXRlPigpOwoKICAgICAgICBwdWJsaWMgTGlzdDxzdHJpbmc+IGRlbW9u" +
            "SW1wQmF0dGxlQXNzZXRQYXRocyA9IG5ldyBMaXN0PHN0cmluZz4oKTsKICAgICAgICBwdWJsaWMgTGlzdDxzdHJpbmc+IGhlbGxo" +
            "b3VuZEJhdHRsZUFzc2V0UGF0aHMgPSBuZXcgTGlzdDxzdHJpbmc+KCk7CiAgICAgICAgcHVibGljIExpc3Q8c3RyaW5nPiBkcmVh" +
            "ZExvcmRCYXR0bGVBc3NldFBhdGhzID0gbmV3IExpc3Q8c3RyaW5nPigpOwoKICAgICAgICBbSGVhZGVyKCJFdmlsIOKAlCBXYXZl" +
            "IDPigJMxMCsgYm9zcyBiYXR0bGUgc2xvdHMiKV0KICAgICAgICBwdWJsaWMgTGlzdDxCYXR0bGVTcHJpdGVTbG90RW50cnk+IGV2" +
            "aWxCb3NzU2xvdHMgPSBuZXcgTGlzdDxCYXR0bGVTcHJpdGVTbG90RW50cnk+KCk7CgogICAgICAgIFtTeXN0ZW0uTm9uU2VyaWFs" +
            "aXplZF0gcHVibGljIGJvb2wgaXNSdW50aW1lU3RhbmRJbjsKCiAgICAgICAgcHVibGljIHN0YXRpYyBCYXR0bGVHb29kRXZpbFNw" +
            "cml0ZVBvb2xQcm9maWxlIExvYWQoKQogICAgICAgIHsKICAgICAgICAgICAgQmF0dGxlR29vZEV2aWxTcHJpdGVQb29sUHJvZmls" +
            "ZSBwID0gbnVsbDsKICAgICAgICAgICAgdHJ5IHsgcCA9IFJlc291cmNlcy5Mb2FkPEJhdHRsZUdvb2RFdmlsU3ByaXRlUG9vbFBy" +
            "b2ZpbGU+KFJlc291cmNlc1BhdGgpOyB9CiAgICAgICAgICAgIGNhdGNoIHsgLyogbWlzc2luZyBNb25vU2NyaXB0IEdVSUQvZmls" +
            "ZUlEIGFmdGVyIGVtYmVkIOKAlCBmYWxsIHRocm91Z2ggKi8gfQogICAgICAgICAgICBpZiAocCAhPSBudWxsKQogICAgICAgICAg" +
            "ICB7CiAgICAgICAgICAgICAgICBwLkVuc3VyZVNsb3RzQW5kTGlzdHMoKTsKICAgICAgICAgICAgICAgIHAuaXNSdW50aW1lU3Rh" +
            "bmRJbiA9IGZhbHNlOwogICAgICAgICAgICAgICAgcC5SZW1hdGVyaWFsaXplU3ByaXRlc0Zyb21QYXRocygpOwogICAgICAgICAg" +
            "ICAgICAgcmV0dXJuIHA7CiAgICAgICAgICAgIH0KICAgICAgICAgICAgdHJ5CiAgICAgICAgICAgIHsKICAgICAgICAgICAgICAg" +
            "IHZhciBhbGwgPSBSZXNvdXJjZXMuTG9hZEFsbDxCYXR0bGVHb29kRXZpbFNwcml0ZVBvb2xQcm9maWxlPigiIik7CiAgICAgICAg" +
            "ICAgICAgICBpZiAoYWxsICE9IG51bGwpCiAgICAgICAgICAgICAgICB7CiAgICAgICAgICAgICAgICAgICAgZm9yIChpbnQgaSA9" +
            "IDA7IGkgPCBhbGwuTGVuZ3RoOyBpKyspCiAgICAgICAgICAgICAgICAgICAgewogICAgICAgICAgICAgICAgICAgICAgICBpZiAo" +
            "YWxsW2ldID09IG51bGwpIGNvbnRpbnVlOwogICAgICAgICAgICAgICAgICAgICAgICBhbGxbaV0uRW5zdXJlU2xvdHNBbmRMaXN0" +
            "cygpOwogICAgICAgICAgICAgICAgICAgICAgICBhbGxbaV0uaXNSdW50aW1lU3RhbmRJbiA9IGZhbHNlOwogICAgICAgICAgICAg" +
            "ICAgICAgICAgICBhbGxbaV0uUmVtYXRlcmlhbGl6ZVNwcml0ZXNGcm9tUGF0aHMoKTsKICAgICAgICAgICAgICAgICAgICAgICAg" +
            "cmV0dXJuIGFsbFtpXTsKICAgICAgICAgICAgICAgICAgICB9CiAgICAgICAgICAgICAgICB9CiAgICAgICAgICAgIH0KICAgICAg" +
            "ICAgICAgY2F0Y2ggeyAvKiBicm9rZW4gUmVzb3VyY2VzIGFzc2V0ICovIH0KICAgICAgICAgICAgcCA9IENyZWF0ZUluc3RhbmNl" +
            "PEJhdHRsZUdvb2RFdmlsU3ByaXRlUG9vbFByb2ZpbGU+KCk7CiAgICAgICAgICAgIHAubmFtZSA9ICJCYXR0bGVHb29kRXZpbFNw" +
            "cml0ZVBvb2xQcm9maWxlX1J1bnRpbWUiOwogICAgICAgICAgICBwLmlzUnVudGltZVN0YW5kSW4gPSB0cnVlOwogICAgICAgICAg" +
            "ICBwLkVuc3VyZVNsb3RzQW5kTGlzdHMoKTsKICAgICAgICAgICAgcmV0dXJuIHA7CiAgICAgICAgfQoKICAgICAgICBwdWJsaWMg" +
            "dm9pZCBFbnN1cmVTbG90c0FuZExpc3RzKCkKICAgICAgICB7CiAgICAgICAgICAgIHBlYXNhbnRCYXR0bGVTcHJpdGVzID8/PSBu" +
            "ZXcgTGlzdDxTcHJpdGU+KCk7CiAgICAgICAgICAgIHdhcnJpb3JCYXR0bGVTcHJpdGVzID8/PSBuZXcgTGlzdDxTcHJpdGU+KCk7" +
            "CiAgICAgICAgICAgIGtuaWdodEJhdHRsZVNwcml0ZXMgPz89IG5ldyBMaXN0PFNwcml0ZT4oKTsKICAgICAgICAgICAgcHJpZXN0" +
            "QmF0dGxlU3ByaXRlcyA/Pz0gbmV3IExpc3Q8U3ByaXRlPigpOwogICAgICAgICAgICBwZWFzYW50QmF0dGxlQXNzZXRQYXRocyA/" +
            "Pz0gbmV3IExpc3Q8c3RyaW5nPigpOwogICAgICAgICAgICB3YXJyaW9yQmF0dGxlQXNzZXRQYXRocyA/Pz0gbmV3IExpc3Q8c3Ry" +
            "aW5nPigpOwogICAgICAgICAgICBrbmlnaHRCYXR0bGVBc3NldFBhdGhzID8/PSBuZXcgTGlzdDxzdHJpbmc+KCk7CiAgICAgICAg" +
            "ICAgIHByaWVzdEJhdHRsZUFzc2V0UGF0aHMgPz89IG5ldyBMaXN0PHN0cmluZz4oKTsKCiAgICAgICAgICAgIGRlbW9uSW1wQmF0" +
            "dGxlU3ByaXRlcyA/Pz0gbmV3IExpc3Q8U3ByaXRlPigpOwogICAgICAgICAgICBoZWxsaG91bmRCYXR0bGVTcHJpdGVzID8/PSBu" +
            "ZXcgTGlzdDxTcHJpdGU+KCk7CiAgICAgICAgICAgIGRyZWFkTG9yZEJhdHRsZVNwcml0ZXMgPz89IG5ldyBMaXN0PFNwcml0ZT4o" +
            "KTsKICAgICAgICAgICAgZGVtb25JbXBCYXR0bGVBc3NldFBhdGhzID8/PSBuZXcgTGlzdDxzdHJpbmc+KCk7CiAgICAgICAgICAg" +
            "IGhlbGxob3VuZEJhdHRsZUFzc2V0UGF0aHMgPz89IG5ldyBMaXN0PHN0cmluZz4oKTsKICAgICAgICAgICAgZHJlYWRMb3JkQmF0" +
            "dGxlQXNzZXRQYXRocyA/Pz0gbmV3IExpc3Q8c3RyaW5nPigpOwoKICAgICAgICAgICAgZ29vZExlZ2VuZFNsb3RzID8/PSBuZXcg" +
            "TGlzdDxCYXR0bGVTcHJpdGVTbG90RW50cnk+KCk7CiAgICAgICAgICAgIGV2aWxCb3NzU2xvdHMgPz89IG5ldyBMaXN0PEJhdHRs" +
            "ZVNwcml0ZVNsb3RFbnRyeT4oKTsKCiAgICAgICAgICAgIEVuc3VyZVNsb3RMaXN0KGdvb2RMZWdlbmRTbG90cywgQ2Fub25pY2Fs" +
            "R29vZExlZ2VuZFNsb3RzLCBzb2Z0TWVybGluOiB0cnVlKTsKICAgICAgICAgICAgRW5zdXJlU2xvdExpc3QoZXZpbEJvc3NTbG90" +
            "cywgQ2Fub25pY2FsQm9zc1Nsb3RzLCBzb2Z0TWVybGluOiBmYWxzZSk7CiAgICAgICAgfQoKICAgICAgICBzdGF0aWMgdm9pZCBF" +
            "bnN1cmVTbG90TGlzdChMaXN0PEJhdHRsZVNwcml0ZVNsb3RFbnRyeT4gbGlzdCwgVW5pdFR5cGVbXSB3YW50VHlwZXMsIGJvb2wg" +
            "c29mdE1lcmxpbikKICAgICAgICB7CiAgICAgICAgICAgIGZvciAoaW50IGkgPSAwOyBpIDwgd2FudFR5cGVzLkxlbmd0aDsgaSsr" +
            "KQogICAgICAgICAgICB7CiAgICAgICAgICAgICAgICBVbml0VHlwZSB3YW50ID0gd2FudFR5cGVzW2ldOwogICAgICAgICAgICAg" +
            "ICAgQmF0dGxlU3ByaXRlU2xvdEVudHJ5IGZvdW5kID0gbnVsbDsKICAgICAgICAgICAgICAgIGZvciAoaW50IGogPSAwOyBqIDwg" +
            "bGlzdC5Db3VudDsgaisrKQogICAgICAgICAgICAgICAgewogICAgICAgICAgICAgICAgICAgIGlmIChsaXN0W2pdICE9IG51bGwg" +
            "JiYgbGlzdFtqXS51bml0VHlwZSA9PSB3YW50KQogICAgICAgICAgICAgICAgICAgIHsKICAgICAgICAgICAgICAgICAgICAgICAg" +
            "Zm91bmQgPSBsaXN0W2pdOwogICAgICAgICAgICAgICAgICAgICAgICBicmVhazsKICAgICAgICAgICAgICAgICAgICB9CiAgICAg" +
            "ICAgICAgICAgICB9CiAgICAgICAgICAgICAgICBpZiAoZm91bmQgPT0gbnVsbCkKICAgICAgICAgICAgICAgIHsKICAgICAgICAg" +
            "ICAgICAgICAgICBmb3VuZCA9IG5ldyBCYXR0bGVTcHJpdGVTbG90RW50cnkKICAgICAgICAgICAgICAgICAgICB7CiAgICAgICAg" +
            "ICAgICAgICAgICAgICAgIHVuaXRUeXBlID0gd2FudCwKICAgICAgICAgICAgICAgICAgICAgICAgZGlzcGxheU5hbWUgPSBEZWZh" +
            "dWx0QmF0dGxlRGlzcGxheU5hbWUod2FudCksCiAgICAgICAgICAgICAgICAgICAgICAgIHNvZnRPcHRpb25hbCA9IHNvZnRNZXJs" +
            "aW4gJiYgd2FudCA9PSBVbml0VHlwZS5NZXJsaW4KICAgICAgICAgICAgICAgICAgICB9OwogICAgICAgICAgICAgICAgICAgIGxp" +
            "c3QuQWRkKGZvdW5kKTsKICAgICAgICAgICAgICAgIH0KICAgICAgICAgICAgICAgIGVsc2UKICAgICAgICAgICAgICAgIHsKICAg" +
            "ICAgICAgICAgICAgICAgICBpZiAoc3RyaW5nLklzTnVsbE9yRW1wdHkoZm91bmQuZGlzcGxheU5hbWUpKQogICAgICAgICAgICAg" +
            "ICAgICAgICAgICBmb3VuZC5kaXNwbGF5TmFtZSA9IERlZmF1bHRCYXR0bGVEaXNwbGF5TmFtZSh3YW50KTsKICAgICAgICAgICAg" +
            "ICAgICAgICBpZiAoc29mdE1lcmxpbikKICAgICAgICAgICAgICAgICAgICAgICAgZm91bmQuc29mdE9wdGlvbmFsID0gd2FudCA9" +
            "PSBVbml0VHlwZS5NZXJsaW47CiAgICAgICAgICAgICAgICB9CiAgICAgICAgICAgIH0KICAgICAgICB9CgogICAgICAgIHB1Ymxp" +
            "YyBzdGF0aWMgc3RyaW5nIERlZmF1bHRCYXR0bGVEaXNwbGF5TmFtZShVbml0VHlwZSB0KSA9PiB0IHN3aXRjaAogICAgICAgIHsK" +
            "ICAgICAgICAgICAgVW5pdFR5cGUuS2luZ0FydGh1ciA9PiAiS2luZyBBcnRodXIiLAogICAgICAgICAgICBVbml0VHlwZS5TaXJM" +
            "YW5jZWxvdCA9PiAiU2lyIExhbmNlbG90IiwKICAgICAgICAgICAgVW5pdFR5cGUuTW9yZ2FuTGVGYXkgPT4gIk1vcmdhbiBsZSBG" +
            "YXkiLAogICAgICAgICAgICBVbml0VHlwZS5MYWR5T2ZUaGVMYWtlID0+ICJMYWR5IG9mIHRoZSBMYWtlIiwKICAgICAgICAgICAg" +
            "VW5pdFR5cGUuU2lyR2F3YWluID0+ICJTaXIgR2F3YWluIiwKICAgICAgICAgICAgVW5pdFR5cGUuU2lyR2FsYWhhZCA9PiAiU2ly" +
            "IEdhbGFoYWQiLAogICAgICAgICAgICBVbml0VHlwZS5TaXJUcmlzdGFuID0+ICJTaXIgVHJpc3RhbiIsCiAgICAgICAgICAgIFVu" +
            "aXRUeXBlLlNpclBlcmNpdmFsID0+ICJTaXIgUGVyY2l2YWwiLAogICAgICAgICAgICBVbml0VHlwZS5TaXJLYXkgPT4gIlNpciBL" +
            "YXkiLAogICAgICAgICAgICBVbml0VHlwZS5TaXJCZWRpdmVyZSA9PiAiU2lyIEJlZGl2ZXJlIiwKICAgICAgICAgICAgVW5pdFR5" +
            "cGUuTGFkeUd1aW5ldmVyZSA9PiAiTGFkeSBHdWluZXZlcmUiLAogICAgICAgICAgICBVbml0VHlwZS5HdWluZXZlcmUgPT4gIkxh" +
            "ZHkgR3VpbmV2ZXJlIiwKICAgICAgICAgICAgVW5pdFR5cGUuQXJjaFByaWVzdE9mR2xhc3RvbmJ1cnkgPT4gIkFyY2gtUHJpZXN0" +
            "IG9mIEdsYXN0b25idXJ5IiwKICAgICAgICAgICAgVW5pdFR5cGUuUHJpZXN0ID0+ICJQcmllc3QiLAogICAgICAgICAgICBVbml0" +
            "VHlwZS5UaGVHcmVlbktuaWdodCA9PiAiVGhlIEdyZWVuIEtuaWdodCIsCiAgICAgICAgICAgIFVuaXRUeXBlLk1lcmxpbiA9PiAi" +
            "TWVybGluIChzb2Z0KSIsCiAgICAgICAgICAgIFVuaXRUeXBlLkRlbW9uSW1wID0+ICJEZW1vbiBJbXAiLAogICAgICAgICAgICBV" +
            "bml0VHlwZS5EZW1vbkhlbGxob3VuZCA9PiAiSGVsbGhvdW5kIiwKICAgICAgICAgICAgVW5pdFR5cGUuRGVtb25EcmVhZExvcmQg" +
            "PT4gIkRyZWFkIExvcmQiLAogICAgICAgICAgICBVbml0VHlwZS5Cb3NzQmxhY2tLbmlnaHQgPT4gIkJsYWNrIEtuaWdodCAoVzMp" +
            "IiwKICAgICAgICAgICAgVW5pdFR5cGUuQm9zc01lbGVhZ2FudCA9PiAiTWVsZWFnYW50IChXNCkiLAogICAgICAgICAgICBVbml0" +
            "VHlwZS5Cb3NzS2luZ0xvdCA9PiAiS2luZyBMb3QgKFc1KSIsCiAgICAgICAgICAgIFVuaXRUeXBlLkJvc3NWb3J0aWdlcm4gPT4g" +
            "IlZvcnRpZ2VybiAoVzYpIiwKICAgICAgICAgICAgVW5pdFR5cGUuQm9zc0FjY29sb24gPT4gIkFjY29sb24gKFc3KSIsCiAgICAg" +
            "ICAgICAgIFVuaXRUeXBlLkJvc3NRdWVzdGluZ0JlYXN0ID0+ICJRdWVzdGluZyBCZWFzdCAoVzgpIiwKICAgICAgICAgICAgVW5p" +
            "dFR5cGUuQm9zc0dyZWVuS25pZ2h0ID0+ICJHcmVlbiBLbmlnaHQgQm9zcyAoVzkpIiwKICAgICAgICAgICAgVW5pdFR5cGUuQm9z" +
            "c05pbXVlID0+ICJOaW11ZSAoVzEwKykiLAogICAgICAgICAgICBfID0+IHQuVG9TdHJpbmcoKQogICAgICAgIH07CgogICAgICAg" +
            "IHB1YmxpYyBpbnQgUmVtYXRlcmlhbGl6ZVNwcml0ZXNGcm9tUGF0aHMoKQogICAgICAgIHsKICAgICAgICAgICAgRW5zdXJlU2xv" +
            "dHNBbmRMaXN0cygpOwogICAgICAgICAgICBpbnQgbiA9IDA7CiAgICAgICAgICAgIG4gKz0gQ2xhc3NTcHJpdGVQb29sUHJvZmls" +
            "ZS5SZW1hdGVyaWFsaXplTGlzdChwZWFzYW50QmF0dGxlU3ByaXRlcywgcGVhc2FudEJhdHRsZUFzc2V0UGF0aHMpOwogICAgICAg" +
            "ICAgICBuICs9IENsYXNzU3ByaXRlUG9vbFByb2ZpbGUuUmVtYXRlcmlhbGl6ZUxpc3Qod2FycmlvckJhdHRsZVNwcml0ZXMsIHdh" +
            "cnJpb3JCYXR0bGVBc3NldFBhdGhzKTsKICAgICAgICAgICAgbiArPSBDbGFzc1Nwcml0ZVBvb2xQcm9maWxlLlJlbWF0ZXJpYWxp" +
            "emVMaXN0KGtuaWdodEJhdHRsZVNwcml0ZXMsIGtuaWdodEJhdHRsZUFzc2V0UGF0aHMpOwogICAgICAgICAgICBuICs9IENsYXNz" +
            "U3ByaXRlUG9vbFByb2ZpbGUuUmVtYXRlcmlhbGl6ZUxpc3QocHJpZXN0QmF0dGxlU3ByaXRlcywgcHJpZXN0QmF0dGxlQXNzZXRQ" +
            "YXRocyk7CiAgICAgICAgICAgIG4gKz0gQ2xhc3NTcHJpdGVQb29sUHJvZmlsZS5SZW1hdGVyaWFsaXplTGlzdChkZW1vbkltcEJh" +
            "dHRsZVNwcml0ZXMsIGRlbW9uSW1wQmF0dGxlQXNzZXRQYXRocyk7CiAgICAgICAgICAgIG4gKz0gQ2xhc3NTcHJpdGVQb29sUHJv" +
            "ZmlsZS5SZW1hdGVyaWFsaXplTGlzdChoZWxsaG91bmRCYXR0bGVTcHJpdGVzLCBoZWxsaG91bmRCYXR0bGVBc3NldFBhdGhzKTsK" +
            "ICAgICAgICAgICAgbiArPSBDbGFzc1Nwcml0ZVBvb2xQcm9maWxlLlJlbWF0ZXJpYWxpemVMaXN0KGRyZWFkTG9yZEJhdHRsZVNw" +
            "cml0ZXMsIGRyZWFkTG9yZEJhdHRsZUFzc2V0UGF0aHMpOwogICAgICAgICAgICBuICs9IFJlbWF0ZXJpYWxpemVTbG90cyhnb29k" +
            "TGVnZW5kU2xvdHMpOwogICAgICAgICAgICBuICs9IFJlbWF0ZXJpYWxpemVTbG90cyhldmlsQm9zc1Nsb3RzKTsKICAgICAgICAg" +
            "ICAgcmV0dXJuIG47CiAgICAgICAgfQoKICAgICAgICBzdGF0aWMgaW50IFJlbWF0ZXJpYWxpemVTbG90cyhMaXN0PEJhdHRsZVNw" +
            "cml0ZVNsb3RFbnRyeT4gc2xvdHMpCiAgICAgICAgewogICAgICAgICAgICBpZiAoc2xvdHMgPT0gbnVsbCkgcmV0dXJuIDA7CiAg" +
            "ICAgICAgICAgIGludCBuID0gMDsKICAgICAgICAgICAgZm9yIChpbnQgaSA9IDA7IGkgPCBzbG90cy5Db3VudDsgaSsrKQogICAg" +
            "ICAgICAgICB7CiAgICAgICAgICAgICAgICB2YXIgZSA9IHNsb3RzW2ldOwogICAgICAgICAgICAgICAgaWYgKGUgPT0gbnVsbCB8" +
            "fCBlLmJhdHRsZVNwcml0ZSAhPSBudWxsKSBjb250aW51ZTsKICAgICAgICAgICAgICAgIGlmIChzdHJpbmcuSXNOdWxsT3JFbXB0" +
            "eShlLmFzc2V0UGF0aCkpIGNvbnRpbnVlOwogICAgICAgICAgICAgICAgU3ByaXRlIHNwID0gQ2xhc3NTcHJpdGVQb29sUHJvZmls" +
            "ZS5UcnlMb2FkU3ByaXRlQXRQYXRoKGUuYXNzZXRQYXRoKTsKICAgICAgICAgICAgICAgIGlmIChzcCA9PSBudWxsKSBjb250aW51" +
            "ZTsKICAgICAgICAgICAgICAgIGUuYmF0dGxlU3ByaXRlID0gc3A7CiAgICAgICAgICAgICAgICBuKys7CiAgICAgICAgICAgIH0K" +
            "ICAgICAgICAgICAgcmV0dXJuIG47CiAgICAgICAgfQoKICAgICAgICBwdWJsaWMgTGlzdDxTcHJpdGU+IEdldFRyb29wT3JXYXZl" +
            "UG9vbChVbml0VHlwZSB0eXBlKQogICAgICAgIHsKICAgICAgICAgICAgRW5zdXJlU2xvdHNBbmRMaXN0cygpOwogICAgICAgICAg" +
            "ICByZXR1cm4gdHlwZSBzd2l0Y2gKICAgICAgICAgICAgewogICAgICAgICAgICAgICAgVW5pdFR5cGUuUGVhc2FudCA9PiBwZWFz" +
            "YW50QmF0dGxlU3ByaXRlcywKICAgICAgICAgICAgICAgIFVuaXRUeXBlLldhcnJpb3IgPT4gd2FycmlvckJhdHRsZVNwcml0ZXMs" +
            "CiAgICAgICAgICAgICAgICBVbml0VHlwZS5LbmlnaHQgPT4ga25pZ2h0QmF0dGxlU3ByaXRlcywKICAgICAgICAgICAgICAgIFVu" +
            "aXRUeXBlLlByaWVzdCA9PiBwcmllc3RCYXR0bGVTcHJpdGVzLAogICAgICAgICAgICAgICAgVW5pdFR5cGUuRGVtb25JbXAgPT4g" +
            "ZGVtb25JbXBCYXR0bGVTcHJpdGVzLAogICAgICAgICAgICAgICAgVW5pdFR5cGUuRGVtb25IZWxsaG91bmQgPT4gaGVsbGhvdW5k" +
            "QmF0dGxlU3ByaXRlcywKICAgICAgICAgICAgICAgIFVuaXRUeXBlLkRlbW9uRHJlYWRMb3JkID0+IGRyZWFkTG9yZEJhdHRsZVNw" +
            "cml0ZXMsCiAgICAgICAgICAgICAgICBfID0+IG51bGwKICAgICAgICAgICAgfTsKICAgICAgICB9CgogICAgICAgIHB1YmxpYyBM" +
            "aXN0PHN0cmluZz4gR2V0VHJvb3BPcldhdmVQYXRocyhVbml0VHlwZSB0eXBlKQogICAgICAgIHsKICAgICAgICAgICAgRW5zdXJl" +
            "U2xvdHNBbmRMaXN0cygpOwogICAgICAgICAgICByZXR1cm4gdHlwZSBzd2l0Y2gKICAgICAgICAgICAgewogICAgICAgICAgICAg" +
            "ICAgVW5pdFR5cGUuUGVhc2FudCA9PiBwZWFzYW50QmF0dGxlQXNzZXRQYXRocywKICAgICAgICAgICAgICAgIFVuaXRUeXBlLldh" +
            "cnJpb3IgPT4gd2FycmlvckJhdHRsZUFzc2V0UGF0aHMsCiAgICAgICAgICAgICAgICBVbml0VHlwZS5LbmlnaHQgPT4ga25pZ2h0" +
            "QmF0dGxlQXNzZXRQYXRocywKICAgICAgICAgICAgICAgIFVuaXRUeXBlLlByaWVzdCA9PiBwcmllc3RCYXR0bGVBc3NldFBhdGhz" +
            "LAogICAgICAgICAgICAgICAgVW5pdFR5cGUuRGVtb25JbXAgPT4gZGVtb25JbXBCYXR0bGVBc3NldFBhdGhzLAogICAgICAgICAg" +
            "ICAgICAgVW5pdFR5cGUuRGVtb25IZWxsaG91bmQgPT4gaGVsbGhvdW5kQmF0dGxlQXNzZXRQYXRocywKICAgICAgICAgICAgICAg" +
            "IFVuaXRUeXBlLkRlbW9uRHJlYWRMb3JkID0+IGRyZWFkTG9yZEJhdHRsZUFzc2V0UGF0aHMsCiAgICAgICAgICAgICAgICBfID0+" +
            "IG51bGwKICAgICAgICAgICAgfTsKICAgICAgICB9CgogICAgICAgIHB1YmxpYyBCYXR0bGVTcHJpdGVTbG90RW50cnkgRmluZExl" +
            "Z2VuZFNsb3QoVW5pdFR5cGUgdHlwZSkKICAgICAgICB7CiAgICAgICAgICAgIEVuc3VyZVNsb3RzQW5kTGlzdHMoKTsKICAgICAg" +
            "ICAgICAgLy8gTGFkeUd1aW5ldmVyZSAvIEd1aW5ldmVyZSBhbGlhcwogICAgICAgICAgICBmb3IgKGludCBpID0gMDsgaSA8IGdv" +
            "b2RMZWdlbmRTbG90cy5Db3VudDsgaSsrKQogICAgICAgICAgICB7CiAgICAgICAgICAgICAgICB2YXIgZSA9IGdvb2RMZWdlbmRT" +
            "bG90c1tpXTsKICAgICAgICAgICAgICAgIGlmIChlID09IG51bGwpIGNvbnRpbnVlOwogICAgICAgICAgICAgICAgaWYgKGUudW5p" +
            "dFR5cGUgPT0gdHlwZSkgcmV0dXJuIGU7CiAgICAgICAgICAgICAgICBpZiAoKHR5cGUgPT0gVW5pdFR5cGUuR3VpbmV2ZXJlIHx8" +
            "IHR5cGUgPT0gVW5pdFR5cGUuTGFkeUd1aW5ldmVyZSkKICAgICAgICAgICAgICAgICAgICAmJiAoZS51bml0VHlwZSA9PSBVbml0" +
            "VHlwZS5HdWluZXZlcmUgfHwgZS51bml0VHlwZSA9PSBVbml0VHlwZS5MYWR5R3VpbmV2ZXJlKSkKICAgICAgICAgICAgICAgICAg" +
            "ICByZXR1cm4gZTsKICAgICAgICAgICAgfQogICAgICAgICAgICByZXR1cm4gbnVsbDsKICAgICAgICB9CgogICAgICAgIHB1Ymxp" +
            "YyBCYXR0bGVTcHJpdGVTbG90RW50cnkgRmluZEJvc3NTbG90KFVuaXRUeXBlIHR5cGUpCiAgICAgICAgewogICAgICAgICAgICBF" +
            "bnN1cmVTbG90c0FuZExpc3RzKCk7CiAgICAgICAgICAgIGZvciAoaW50IGkgPSAwOyBpIDwgZXZpbEJvc3NTbG90cy5Db3VudDsg" +
            "aSsrKQogICAgICAgICAgICB7CiAgICAgICAgICAgICAgICB2YXIgZSA9IGV2aWxCb3NzU2xvdHNbaV07CiAgICAgICAgICAgICAg" +
            "ICBpZiAoZSAhPSBudWxsICYmIGUudW5pdFR5cGUgPT0gdHlwZSkgcmV0dXJuIGU7CiAgICAgICAgICAgIH0KICAgICAgICAgICAg" +
            "cmV0dXJuIG51bGw7CiAgICAgICAgfQoKICAgICAgICBwdWJsaWMgU3ByaXRlIEdldExlZ2VuZEJhdHRsZVNwcml0ZShVbml0VHlw" +
            "ZSB0eXBlKQogICAgICAgIHsKICAgICAgICAgICAgdmFyIGUgPSBGaW5kTGVnZW5kU2xvdCh0eXBlKTsKICAgICAgICAgICAgcmV0" +
            "dXJuIGUgIT0gbnVsbCA/IGUuYmF0dGxlU3ByaXRlIDogbnVsbDsKICAgICAgICB9CgogICAgICAgIHB1YmxpYyBTcHJpdGUgR2V0" +
            "Qm9zc0JhdHRsZVNwcml0ZShVbml0VHlwZSB0eXBlKQogICAgICAgIHsKICAgICAgICAgICAgdmFyIGUgPSBGaW5kQm9zc1Nsb3Qo" +
            "dHlwZSk7CiAgICAgICAgICAgIHJldHVybiBlICE9IG51bGwgPyBlLmJhdHRsZVNwcml0ZSA6IG51bGw7CiAgICAgICAgfQoKICAg" +
            "ICAgICBwdWJsaWMgaW50IENvdW50VXNhYmxlUG9vbChVbml0VHlwZSB0eXBlKQogICAgICAgIHsKICAgICAgICAgICAgdmFyIHBv" +
            "b2wgPSBHZXRUcm9vcE9yV2F2ZVBvb2wodHlwZSk7CiAgICAgICAgICAgIGlmIChwb29sID09IG51bGwpIHJldHVybiAwOwogICAg" +
            "ICAgICAgICBpbnQgbiA9IDA7CiAgICAgICAgICAgIGZvciAoaW50IGkgPSAwOyBpIDwgcG9vbC5Db3VudDsgaSsrKQogICAgICAg" +
            "ICAgICAgICAgaWYgKHBvb2xbaV0gIT0gbnVsbCkgbisrOwogICAgICAgICAgICByZXR1cm4gbjsKICAgICAgICB9CgogICAgICAg" +
            "IHB1YmxpYyBpbnQgQ291bnRBc3NpZ25lZExlZ2VuZEJhdHRsZXMoKQogICAgICAgIHsKICAgICAgICAgICAgRW5zdXJlU2xvdHNB" +
            "bmRMaXN0cygpOwogICAgICAgICAgICBpbnQgbiA9IDA7CiAgICAgICAgICAgIGZvciAoaW50IGkgPSAwOyBpIDwgZ29vZExlZ2Vu" +
            "ZFNsb3RzLkNvdW50OyBpKyspCiAgICAgICAgICAgIHsKICAgICAgICAgICAgICAgIHZhciBlID0gZ29vZExlZ2VuZFNsb3RzW2ld" +
            "OwogICAgICAgICAgICAgICAgaWYgKGUgPT0gbnVsbCB8fCBlLnNvZnRPcHRpb25hbCkgY29udGludWU7CiAgICAgICAgICAgICAg" +
            "ICBpZiAoZS5iYXR0bGVTcHJpdGUgIT0gbnVsbCkgbisrOwogICAgICAgICAgICB9CiAgICAgICAgICAgIHJldHVybiBuOwogICAg" +
            "ICAgIH0KCiAgICAgICAgcHVibGljIGludCBDb3VudEFzc2lnbmVkQm9zc0JhdHRsZXMoKQogICAgICAgIHsKICAgICAgICAgICAg" +
            "RW5zdXJlU2xvdHNBbmRMaXN0cygpOwogICAgICAgICAgICBpbnQgbiA9IDA7CiAgICAgICAgICAgIGZvciAoaW50IGkgPSAwOyBp" +
            "IDwgZXZpbEJvc3NTbG90cy5Db3VudDsgaSsrKQogICAgICAgICAgICB7CiAgICAgICAgICAgICAgICB2YXIgZSA9IGV2aWxCb3Nz" +
            "U2xvdHNbaV07CiAgICAgICAgICAgICAgICBpZiAoZSAhPSBudWxsICYmIGUuYmF0dGxlU3ByaXRlICE9IG51bGwpIG4rKzsKICAg" +
            "ICAgICAgICAgfQogICAgICAgICAgICByZXR1cm4gbjsKICAgICAgICB9CiAgICB9Cn0K"
            ;

        public static readonly (string rel, string b64)[] All =
        {
            ("Assets/Scripts/Data/StageLayoutProfile.cs", B64_StageLayoutProfile),
            ("Assets/Scripts/Data/SceneBackgroundProfile.cs", B64_SceneBackgroundProfile),
            ("Assets/Scripts/Data/ClassSpritePoolProfile.cs", B64_ClassSpritePoolProfile),
            ("Assets/Scripts/Data/LegendPewPortraitProfile.cs", B64_LegendPewPortraitProfile),
            ("Assets/Scripts/Data/BattleGoodEvilSpritePoolProfile.cs", B64_BattleGoodEvilSpritePoolProfile),
        };

        public static string Decode(string b64)
        {
            if (string.IsNullOrEmpty(b64)) return null;
            try
            {
                return System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(b64));
            }
            catch { return null; }
        }
    }

}
#endif
