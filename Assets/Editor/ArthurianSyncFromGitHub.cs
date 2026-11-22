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
    private const string FallbackCommitSha = "1f51ac6a0ecc1eac3faa3a5086602c41292c5119";

    /// <summary>
    /// Hard tip for FORCE host overwrite. Update every ship.
    /// Never let FORCE land a 381-byte pre-OnLoad PackageVersion (architect-force-tip-ui).
    /// Prefer API tip / raw@main BEFORE HardTip — HardTip-first re-stuck Downloads on old SHAs.
    /// </summary>
    public const string HardTipCommitSha = "1f51ac6a0ecc1eac3faa3a5086602c41292c5119";

    private const string ExpectedPackageId = "legend-unlocks-meta";

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
        "assign-only-runtime",
        "sync-keep-editor-menus",
    };

    /// <summary>
    /// Always overwrite from tip/main — never skip-self, never Fallback pin.
    /// TipEscape FIRST so even partial FORCE lands the escape hatch.
    /// </summary>
    public static readonly string[] SyncHostForceOverwritePaths =
    {
        "Assets/Editor/DemonKnightTipEscape.cs",
        "Assets/Editor/DemonKnightPackageVersion.cs",
        "Assets/Editor/ArthurianSyncFromGitHub.cs",
        "Assets/Editor/ArthurianMenuBootstrap.cs",
        "Assets/Editor/ArthurianArtArchitectWindow.cs",
        "Assets/Editor/ArthurianStageLayoutProfileEnsure.cs",
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
            int n = NukePhase25Standalones(projectRoot, writeStubIfNeeded: true, errors: null);
            if (n > 0)
            {
                Debug.Log($"[DemonKnight] DA-59 Editor OnLoad nuked {n} Phase 2–5 standalone(s) (CS0101).");
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
        "Assets/Editor/ArthurianFixMisplacedScripts.cs",
    };

    private static readonly string[] ScriptPaths =
    {
        // Menu hosts FIRST — keep Arthurian menu alive even on a half-failed sync.
        "Assets/Editor/ArthurianSyncFromGitHub.cs",
        "Assets/Editor/ArthurianMenuBootstrap.cs",
        "Assets/Editor/DemonKnightPackageVersion.cs",
        "Assets/Scripts/Systems/DemonKnightCompatApi.cs",
        // Phase 2–5 types are EMBEDDED (GameDataTypes / PaperDoll / GameLoop / TavernDice).
        // Do NOT download SpriteAnimClip/StageLayoutProfile/ModularSpriteLibrary/
        // ModularSpriteAnimator/StageParallaxController/DiceManager standalones (CS0101).
        "Assets/Scripts/Data/GameDataTypes.cs",
        "Assets/Editor/ArthurianStageLayoutProfileEnsure.cs",
        "Assets/Editor/ArthurianArtArchitectWindow.cs",
        "Assets/Editor/DemonKnightTipEscape.cs",
        // Types + helpers BEFORE any UI that references them (CS0103 / CS0246).
        "Assets/Scripts/UI/MenuListSortBar.cs",
        "Assets/Scripts/UI/PewSeatHoverHighlight.cs",
        "Assets/Scripts/Systems/GameLoopManager.cs",
        "Assets/Scripts/UI/SomersetChurchUI.cs",
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
        "Assets/Scripts/Systems/DemonKnightCompatApi.cs",
        // Phase 2–5 types embedded in hosts below — no standalone downloads.
        "Assets/Scripts/Data/GameDataTypes.cs",
        "Assets/Editor/ArthurianStageLayoutProfileEnsure.cs",
        "Assets/Editor/ArthurianArtArchitectWindow.cs",
        // PewSeatHoverHighlight + MenuListSortBar are EMBEDDED in these two files.
        "Assets/Scripts/UI/PaperDollSeatView.cs",
        "Assets/Scripts/UI/VictoryStarBadge.cs",
        "Assets/Scripts/UI/LegendCodexUI.cs",
        "Assets/Scripts/Systems/GameLoopManager.cs",
        "Assets/Scripts/UI/SomersetChurchUI.cs",
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
        "Assets/Scripts/Data/GameDataTypes.cs",
        "Assets/Editor/ArthurianStageLayoutProfileEnsure.cs",
        "Assets/Editor/ArthurianArtArchitectWindow.cs",
        // Embed hosts (DA-59 — Phase 2–5 types live here).
        "Assets/Scripts/UI/PaperDollSeatView.cs",
        "Assets/Scripts/Systems/GameLoopManager.cs",
        "Assets/Scripts/Systems/TavernDiceMinigame.cs",
        "Assets/Scripts/UI/LegendCodexUI.cs",
        "Assets/Scripts/UI/MenuListSortBar.cs",
        "Assets/Scripts/UI/PewSeatHoverHighlight.cs",
    };

    /// <summary>Standalone Phase 2–5 paths — never download; delete if they redefine embeds.</summary>
    public static readonly string[] Phase25StandaloneKillList =
    {
        "Assets/Scripts/Data/SpriteAnimClip.cs",
        "Assets/Scripts/Data/StageLayoutProfile.cs",
        "Assets/Scripts/Data/ModularSpriteLibrary.cs",
        "Assets/Scripts/UI/ModularSpriteAnimator.cs",
        "Assets/Scripts/Systems/StageParallaxController.cs",
        "Assets/Scripts/Systems/DiceManager.cs",
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

            if (preDeleteAll)
            {
                EditorUtility.DisplayProgressBar("DemonKnight Sync", "Deleting local script copies…", 0f);
                foreach (string rel in ScriptPaths)
                {
                    // NEVER wipe menu hosts first — that is what made Bootstrap/Force Sync disappear.
                    if (NeverBulkDeleteEditorMenus.Contains(rel)) continue;
                    // Keep embed hosts + stubs across Force Delete wipe.
                    if (IsMustRestoreUiHelper(rel)) continue;
                    TryDeleteFile(Path.Combine(projectRoot, rel.Replace('/', Path.DirectorySeparatorChar)));
                }
                // Force Delete must not leave Phase 2–5 standalones either.
                NukePhase25Standalones(projectRoot, writeStubIfNeeded: false, errors: errors);
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
                    StubPhase25StandalonesOver(projectRoot, errors);
            }
            catch (Exception ex)
            {
                errors.Add($"{rel} (ui helper ensure): {ex.Message}");
                Debug.LogError($"[DemonKnight] Could not ensure UI helper {rel}: {ex.Message}");
            }
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
                   // DA-59 nuclear embeds — types live here so stale Sync hosts still compile.
                   && body.Contains("class SpriteAnimClip")
                   && body.Contains("class StageLayoutProfile")
                   && body.Contains("class ModularSpriteLibrary")
                   && body.Contains("previewOrOverrideSprite")
                   && body.Contains("assignedCharacterId")
                   && body.Contains("ChurchPewCount = 52");
        }

        // Phase 2–5 standalones are NOT synced — reject any download that would redefine embeds.
        if (rel.EndsWith("StageLayoutProfile.cs", StringComparison.OrdinalIgnoreCase)
            || rel.EndsWith("SpriteAnimClip.cs", StringComparison.OrdinalIgnoreCase)
            || rel.EndsWith("ModularSpriteLibrary.cs", StringComparison.OrdinalIgnoreCase)
            || rel.EndsWith("ModularSpriteAnimator.cs", StringComparison.OrdinalIgnoreCase)
            || rel.EndsWith("StageParallaxController.cs", StringComparison.OrdinalIgnoreCase)
            || rel.EndsWith("DiceManager.cs", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (rel.EndsWith("ArthurianArtArchitectWindow.cs", StringComparison.OrdinalIgnoreCase))
            return IsTipArchitectBody(body);

        if (rel.EndsWith("SomersetChurchUI.cs", StringComparison.OrdinalIgnoreCase))
        {
            return body.Contains("RewireMarchButton")
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
                   && body.Contains("420f, 460f")
                   && body.Contains("onLeftClick: () => ToggleUnitSelectionMenu")
                   && body.Contains("CloseUnitSelectionMenu")
                   && body.Contains("ApplyRecruitButtonLabel")
                   && body.Contains("Recruit {hireCost}g ({purse}g)")
                   && body.Contains("58f, 8f")
                   && body.Contains("GetMouseButtonDown(1)");
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
                   && !body.Contains("(MenuListSortMode.Default, \"Default\")");
        }

        if (rel.EndsWith("CharacterDatabase.cs", StringComparison.OrdinalIgnoreCase))
        {
            // Priest/legend fuse: ingredients-only — no HasUnitType one-product lock.
            return body.Contains("TryPickIngredients")
                   && body.Contains("CreateLegendRecruit")
                   && body.Contains("LogFuseBlocked")
                   && body.Contains("CardFusionSystem.SanitizeBaseUnitFusionFlags")
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
                   && body.Contains("StageLayoutProfile");
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
                   || body.Contains("sync-complete-replace");
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
        if (!body.Contains("enum ArchitectTab { Splicer, StagePlacer }")) return false;
        if (!body.Contains("Sheet Splicer")) return false;
        if (!body.Contains("Manual Box Crop")) return false;
        if (!body.Contains("Save Splices to Folder")) return false;
        if (!body.Contains("UiBuildId")) return false;
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
            bool sprite = body.Contains("SpriteCategory") && body.Contains("enum ArchitectTab { Splicer, StagePlacer }");
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
}
#endif
