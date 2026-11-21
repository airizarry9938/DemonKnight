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
    private const string FallbackCommitSha = "81f25276281f2aa02439a4ab56fc81bcc8766528";

    private const string ExpectedPackageId = "sync-host-da37";

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
        "Assets/Scripts/Data/GameDataTypes.cs",
        "Assets/Scripts/Data/TownsfolkAppearance.cs",
        "Assets/Scripts/Data/ModularSpriteLibrary.cs",
        "Assets/Scripts/Data/Characters/CharacterDefinition.cs",
        "Assets/Scripts/Data/Characters/CharacterDatabase.cs",
        "Assets/Scripts/Data/SomersetRunes.cs",
        "Assets/Scripts/Data/SomersetVisualDatabase.cs",
        "Assets/Scripts/Persistence/GrudgeSaveManager.cs",
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
        "Assets/Scripts/Systems/GameLoopManager.cs",
        "Assets/Scripts/UI/SomersetChurchUI.cs",
        "Assets/Scripts/UI/ChurchStageUIController.cs",
        "Assets/Scripts/UI/WaveBossHudView.cs",
        "Assets/Scripts/UI/TownSplitUIController.cs",
        "Assets/Scripts/UI/LegendCodexUI.cs",
        "Assets/Scripts/UI/MainMenuUI.cs",
        "Assets/Scripts/Combat/AutoBattlerEngine.cs",
        "Assets/Scripts/Combat/BattlefieldVisualizer.cs",
        "Assets/Scripts/Combat/BattleBossDialogUI.cs",
        "Assets/Scripts/Persistence/MerlinResurrectionManager.cs",
        "Assets/Scripts/Systems/PerkDraftManager.cs",
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
    /// Chicken-egg fix: overwrite Sync + MenuBootstrap + PackageVersion + CompatApi from a pinned
    /// commit without requiring ExpectedPackageId to already match.
    /// </summary>
    [MenuItem("Arthurian/Bootstrap CompatApi + Sync Tools (pinned)", false, 2)]
    public static void BootstrapCompatAndSyncTools()
    {
        if (!EditorUtility.DisplayDialog(
                "DemonKnight Bootstrap",
                "Overwrite Sync menu hosts + CompatApi from pinned commit " + FallbackCommitSha + " (or tip of main):\n\n" +
                "• ArthurianSyncFromGitHub.cs (Bootstrap / Force Delete+Sync menus)\n" +
                "• ArthurianMenuBootstrap.cs (Diagnose menu)\n" +
                "• DemonKnightPackageVersion.cs\n" +
                "• DemonKnightCompatApi.cs\n\n" +
                "Use when menus are missing OR Diagnose shows cs1061-compat-api.",
                "Bootstrap Now", "Cancel"))
            return;

        ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12;
        // Prefer tip of main (landed stack); pin is emergency fallback only.
        string sha = ResolveMainSha() ?? FallbackCommitSha;
        string projectRoot = Directory.GetParent(Application.dataPath)!.FullName;
        string[] bootstrap =
        {
            "Assets/Editor/ArthurianSyncFromGitHub.cs",
            "Assets/Editor/ArthurianMenuBootstrap.cs",
            "Assets/Editor/DemonKnightPackageVersion.cs",
            "Assets/Scripts/Systems/DemonKnightCompatApi.cs",
            // Pull wave boss HUD with bootstrap so night label never CS0103 after partial Sync.
            "Assets/Scripts/UI/WaveBossHudView.cs",
        };

        var errors = new List<string>();
        int ok = 0;
        try
        {
            for (int i = 0; i < bootstrap.Length; i++)
            {
                string rel = bootstrap[i];
                EditorUtility.DisplayProgressBar("DemonKnight Bootstrap", rel, (float)i / bootstrap.Length);
                try
                {
                    string body = DownloadPinned(rel, sha, out string source, relaxPackageId: true);
                    // CompatApi MUST have ResolveLoop after bootstrap.
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

        AssetDatabase.Refresh();
        string msg = $"Bootstrapped {ok}/{bootstrap.Length} @ {sha}.";
        if (errors.Count > 0) msg += "\n\nFailures:\n" + string.Join("\n", errors);
        else msg += "\n\nWait for compile, then run Force Delete+Sync ALL Scripts.";
        EditorUtility.DisplayDialog("DemonKnight Bootstrap", msg, "OK");
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
            if (preDeleteAll)
            {
                EditorUtility.DisplayProgressBar("DemonKnight Sync", "Deleting local script copies…", 0f);
                foreach (string rel in ScriptPaths)
                {
                    // NEVER wipe menu hosts first — that is what made Bootstrap/Force Sync disappear.
                    if (NeverBulkDeleteEditorMenus.Contains(rel)) continue;
                    TryDeleteFile(Path.Combine(projectRoot, rel.Replace('/', Path.DirectorySeparatorChar)));
                }
            }

            for (int i = 0; i < ScriptPaths.Length; i++)
            {
                string rel = ScriptPaths[i];
                EditorUtility.DisplayProgressBar("DemonKnight Sync", rel + " @ " + sha, (float)i / ScriptPaths.Length);
                string dest = Path.Combine(projectRoot, rel.Replace('/', Path.DirectorySeparatorChar));
                try
                {
                    // Menu hosts: relax package-id so tip Sync always lands even when local
                    // ExpectedPackageId lags one Force Sync behind.
                    bool protectMenu = NeverBulkDeleteEditorMenus.Contains(rel);
                    string text = DownloadPinned(rel, sha, out string source, relaxPackageId: protectMenu);
                    if (!protectMenu) ValidateCritical(rel, text);
                    // Menu hosts: in-place replace only (no delete-first gap).
                    WriteReplacing(dest, text, deleteFirst: deleteFirst && !protectMenu);
                    VerifyOnDisk(rel, dest, relaxPackageId: protectMenu);
                    okFiles.Add(rel);
                    Debug.Log($"[DemonKnight] Synced {rel} via {source}");
                }
                catch (Exception ex)
                {
                    errors.Add($"{rel}: {ex.Message}");
                    Debug.LogError($"[DemonKnight] Sync FAILED {rel}: {ex.Message}");
                }
            }

            // If a mid-pass failure still left a menu host missing, restore it now.
            EnsureEditorMenuHostsPresent(projectRoot, sha, errors, okFiles);
        }
        finally
        {
            EditorUtility.ClearProgressBar();
        }

        ArthurianFixMisplacedScripts.Run();
        AssetDatabase.Refresh();

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
            msg += $"\n\nWait for compile. Diagnose must show `{ExpectedPackageId}`.\n"
                   + "Arthurian menus kept: Bootstrap / Force Delete+Sync / Diagnose.\n"
                   + "Do NOT rebuild the scene.";
        }

        Debug.Log("[DemonKnight] " + msg);
        if (showDialog)
            EditorUtility.DisplayDialog(
                missingCritical.Count > 0 ? "DemonKnight Sync INCOMPLETE" : "DemonKnight Sync",
                msg, "OK");
    }

    /// <summary>Resolve tip SHA of main so jsDelivr is commit-pinned (never @main).</summary>
    private static string ResolveMainSha()
    {
        try
        {
            string json = DownloadString("https://api.github.com/repos/" + Repo + "/commits/main");
            var m = Regex.Match(json, "\"sha\"\\s*:\\s*\"([0-9a-f]{40})\"");
            // Full SHA for jsDelivr pin (short SHAs can miss on some CDN edges).
            if (m.Success) return m.Groups[1].Value;
        }
        catch (Exception ex)
        {
            Debug.LogWarning("[DemonKnight] Could not resolve main SHA via API: " + ex.Message);
        }
        return null;
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
            string[] urls =
            {
                RawBase + sha + "/" + rel + "?" + bust,
                RawBase + sha + "/" + rel,
                RawBase + FallbackCommitSha + "/" + rel + "?" + bust,
                RawBase + FallbackCommitSha + "/" + rel,
                RawBase + "main/" + rel + "?" + bust,
                RawBase + "main/" + rel,
                JsDelivrBase + sha + "/" + rel + "?" + bust,
                JsDelivrBase + sha + "/" + rel,
                JsDelivrBase + FallbackCommitSha + "/" + rel + "?" + bust,
            };
            string[] labels =
            {
                "raw@" + sha + "+bust",
                "raw@" + sha,
                "raw@" + FallbackCommitSha + "+bust",
                "raw@" + FallbackCommitSha,
                "raw@main+bust",
                "raw@main",
                "jsdelivr@" + sha + "+bust",
                "jsdelivr@" + sha,
                "jsdelivr@" + FallbackCommitSha + "+bust",
            };

            for (int i = 0; i < urls.Length; i++)
            {
                try
                {
                    string body = DownloadString(urls[i]);
                    if (string.IsNullOrEmpty(body) || body.StartsWith("404")
                        || body.Contains("404: Not Found")
                        || body.Contains("Failed to fetch")
                        || body.Contains("Package size exceeded")
                        || body.TrimStart().StartsWith("<!DOCTYPE", StringComparison.OrdinalIgnoreCase))
                        throw new Exception("Empty or 404");
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
                VerifyOnDisk(rel, dest);
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
                   && body.Contains("mustDisbandPartyAfterCurrentWave");
        }

        if (rel.EndsWith("DemonKnightCompatApi.cs", StringComparison.OrdinalIgnoreCase))
        {
            bool core = body.Contains("ResolveLoop")
                        && body.Contains("AutosaveWarband")
                        && body.Contains("ChurchRewireMarch")
                        && body.Contains("WaveVictoryGold");
            if (!core) return false;
            // Bootstrap may land an older CompatApi; Force Sync wants the soft HUD helper.
            if (relaxPackageId) return true;
            return body.Contains("RefreshWaveBossHud");
        }

        if (rel.EndsWith("SomersetChurchUI.cs", StringComparison.OrdinalIgnoreCase))
        {
            return body.Contains("RewireMarchButton")
                   && body.Contains("ForceNamedTavernDicePanelsInactive")
                   && body.Contains("forceEqualHudSlots");
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
                   && body.Contains("waveBoss"); // CS0136 fix: no shadowed local `boss`
        }

        if (rel.EndsWith("TownSplitUIController.cs", StringComparison.OrdinalIgnoreCase)
            || rel.EndsWith("LegendCodexUI.cs", StringComparison.OrdinalIgnoreCase)
            || rel.EndsWith("MainMenuUI.cs", StringComparison.OrdinalIgnoreCase)
            || rel.EndsWith("PerkDraftManager.cs", StringComparison.OrdinalIgnoreCase)
            || rel.EndsWith("MerlinResurrectionManager.cs", StringComparison.OrdinalIgnoreCase))
        {
            return body.Contains("DemonKnightCompatApi");
        }

        if (rel.EndsWith("BattlefieldVisualizer.cs", StringComparison.OrdinalIgnoreCase))
        {
            return body.Contains("ResolveArenaBindings")
                   && body.Contains("ApplyBackgroundArt")
                   && body.Contains("ApplyVideoBackground");
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
                   && body.Contains("BootstrapCompatAndSyncTools");
        }

        if (rel.EndsWith("ArthurianMenuBootstrap.cs", StringComparison.OrdinalIgnoreCase))
        {
            return body.Contains("Diagnose Project") && body.Contains("DemonKnightPackageVersion");
        }

        return true;
    }
}
#endif
