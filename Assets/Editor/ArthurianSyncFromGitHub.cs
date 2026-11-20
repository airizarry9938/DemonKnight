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
/// Pull every DemonKnight gameplay + editor script from GitHub main into Assets/.
/// Always delete-then-write so Windows/Unity cannot keep a stale .cs handle.
/// Critical files are verified on disk after write; Sync fails loudly if incomplete.
/// </summary>
public static class ArthurianSyncFromGitHub
{
    private const string Repo = "airizarry9938/DemonKnight";
    private const string RawMain = "https://raw.githubusercontent.com/" + Repo + "/main/";
    private const string JsDelivrMain = "https://cdn.jsdelivr.net/gh/" + Repo + "@main/";

    private const string ExpectedPackageId = "sync-complete-replace";

    /// <summary>Complete script set — keep in sync with repo Assets/Scripts + required Editor tools.</summary>
    private static readonly string[] ScriptPaths =
    {
        "Assets/Scripts/Combat/AutoBattlerEngine.cs",
        "Assets/Scripts/Combat/BattlefieldVisualizer.cs",
        "Assets/Scripts/Combat/DemonHordeGenerator.cs",
        "Assets/Scripts/Combat/FloatingCombatText.cs",
        "Assets/Scripts/Combat/UnitVisualActor.cs",
        "Assets/Scripts/Data/GameDataTypes.cs",
        "Assets/Scripts/Data/SomersetRunes.cs",
        "Assets/Scripts/Data/SomersetVisualDatabase.cs",
        "Assets/Scripts/Persistence/GrudgeSaveManager.cs",
        "Assets/Scripts/Persistence/MerlinResurrectionManager.cs",
        "Assets/Scripts/Persistence/WarbandAutosave.cs",
        "Assets/Scripts/Systems/CardFusionSystem.cs",
        "Assets/Scripts/Systems/DemonicPactManager.cs",
        "Assets/Scripts/Systems/DemonKnightCompatApi.cs",
        "Assets/Scripts/Systems/GameLoopManager.cs",
        "Assets/Scripts/Systems/PerkDraftManager.cs",
        "Assets/Scripts/Systems/RelicManager.cs",
        "Assets/Scripts/Systems/SinRuneManager.cs",
        "Assets/Scripts/Systems/TavernDiceMinigame.cs",
        "Assets/Scripts/Systems/TownRNGGenerator.cs",
        "Assets/Scripts/UI/SomersetPewLayout.cs",
        "Assets/Scripts/UI/ChurchPewLayout.cs",
        "Assets/Scripts/UI/ChurchStageUIController.cs",
        "Assets/Scripts/UI/SomersetChurchUI.cs",
        "Assets/Scripts/UI/MainMenuUI.cs",
        "Assets/Scripts/UI/LegendCodexUI.cs",
        "Assets/Scripts/UI/PaperDollSeatView.cs",
        "Assets/Scripts/UI/TownSplitUIController.cs",
        "Assets/Editor/ArthurianAlignCrowdPews.cs",
        "Assets/Editor/ArthurianFixMisplacedScripts.cs",
        "Assets/Editor/ArthurianFixMissingScripts.cs",
        "Assets/Editor/ArthurianRepairChurchStage.cs",
        "Assets/Editor/ArthurianMenuBootstrap.cs",
        "Assets/Editor/ArthurianSceneBuilder.cs",
        "Assets/Editor/DemonKnightPackageVersion.cs",
        "Assets/Editor/SomersetAssetStudioWindow.cs",
        "Assets/Editor/ArthurianSyncFromGitHub.cs",
    };

    private static readonly string[] AtomicCritical =
    {
        "Assets/Scripts/Systems/GameLoopManager.cs",
        "Assets/Scripts/Systems/DemonKnightCompatApi.cs",
        "Assets/Scripts/UI/SomersetChurchUI.cs",
        "Assets/Scripts/UI/ChurchStageUIController.cs",
        "Assets/Scripts/UI/TownSplitUIController.cs",
        "Assets/Scripts/UI/LegendCodexUI.cs",
        "Assets/Scripts/UI/MainMenuUI.cs",
        "Assets/Scripts/Combat/AutoBattlerEngine.cs",
        "Assets/Scripts/Combat/BattlefieldVisualizer.cs",
        "Assets/Scripts/Persistence/MerlinResurrectionManager.cs",
        "Assets/Scripts/Systems/PerkDraftManager.cs",
        "Assets/Editor/DemonKnightPackageVersion.cs",
        "Assets/Editor/ArthurianSyncFromGitHub.cs",
    };

    [MenuItem("Arthurian/Sync All Scripts From GitHub", false, 0)]
    public static void SyncAll()
    {
        if (!EditorUtility.DisplayDialog(
                "DemonKnight Sync",
                "Download and REPLACE all DemonKnight scripts from GitHub main?\n\n" +
                "• Deletes each .cs then rewrites it (fixes Windows/Unity partial overwrite)\n" +
                "• Does NOT touch scenes, art, or ProjectSettings\n" +
                "• Diagnose must show: " + ExpectedPackageId,
                "Sync Now", "Cancel"))
            return;

        RunSync(showDialog: true, deleteFirst: true);
    }

    [MenuItem("Arthurian/Force Delete+Sync ALL Scripts", false, 1)]
    public static void ForceDeleteSyncAll()
    {
        if (!EditorUtility.DisplayDialog(
                "DemonKnight FORCE Sync",
                "DELETE every listed DemonKnight .cs on disk, then re-download the full set from GitHub main.\n\n" +
                "Use this when Diagnose still shows CS1061 after a normal Sync.\n" +
                "Scenes/art are NOT deleted.",
                "Delete + Sync", "Cancel"))
            return;

        RunSync(showDialog: true, deleteFirst: true, preDeleteAll: true);
    }

    [MenuItem("Arthurian/Force Re-Sync Night/HUD Fix", false, 2)]
    public static void ForceResyncNightFix()
    {
        // Legacy menu — now runs the full atomic critical set (not just 5 files).
        ForceDeleteSyncAll();
    }

    public static void RunSync(bool showDialog, bool deleteFirst = true, bool preDeleteAll = false)
    {
        ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12;

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
                    string dest = Path.Combine(projectRoot, rel.Replace('/', Path.DirectorySeparatorChar));
                    TryDeleteFile(dest);
                }
            }

            EditorUtility.DisplayProgressBar("DemonKnight Sync", "Starting…", 0f);
            for (int i = 0; i < ScriptPaths.Length; i++)
            {
                string rel = ScriptPaths[i];
                EditorUtility.DisplayProgressBar(
                    "DemonKnight Sync", rel, (float)i / ScriptPaths.Length);

                string dest = Path.Combine(projectRoot, rel.Replace('/', Path.DirectorySeparatorChar));
                try
                {
                    string text = DownloadFresh(rel, out string source);
                    ValidateCritical(rel, text);
                    WriteReplacing(dest, text, deleteFirst);
                    VerifyOnDisk(rel, dest);
                    okFiles.Add(rel);
                    Debug.Log($"[DemonKnight] Synced {rel} via {source}");
                }
                catch (Exception ex)
                {
                    errors.Add($"{rel}: {ex.Message}");
                    Debug.LogError($"[DemonKnight] Sync FAILED {rel}: {ex.Message}");
                }
            }
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

        string msg = $"Synced {okFiles.Count}/{ScriptPaths.Length} files from GitHub.";
        if (errors.Count > 0)
            msg += "\n\nFailures:\n" + string.Join("\n", errors);
        if (missingCritical.Count > 0)
        {
            msg += "\n\nINCOMPLETE — critical files missing/failed:\n" + string.Join("\n", missingCritical)
                   + "\n\nRun Arthurian → Force Delete+Sync ALL Scripts again.";
        }
        else
        {
            msg += $"\n\nWait for compile. Diagnose must show `{ExpectedPackageId}`."
                   + "\nDo NOT rebuild the scene — HUD placements stay.";
        }

        Debug.Log("[DemonKnight] " + msg);
        if (showDialog)
            EditorUtility.DisplayDialog(
                missingCritical.Count > 0 ? "DemonKnight Sync INCOMPLETE" : "DemonKnight Sync",
                msg,
                "OK");
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
        else
        {
            File.Move(tmp, dest);
        }
        TryDeleteFile(tmp);
    }

    private static void TryDeleteFile(string path)
    {
        if (!File.Exists(path)) return;
        try { File.SetAttributes(path, FileAttributes.Normal); } catch { /* ignore */ }
        try { File.Delete(path); } catch (Exception ex)
        {
            throw new Exception("could not delete old file (close Unity lock?): " + ex.Message);
        }
    }

    private static void VerifyOnDisk(string rel, string dest)
    {
        if (!File.Exists(dest))
            throw new Exception("write vanished — file missing after sync");
        string disk = File.ReadAllText(dest);
        if (!PassesCriticalCheck(rel, disk))
            throw new Exception("on-disk file failed sanity check after write");
    }

    private static string DownloadFresh(string rel, out string source)
    {
        string bust = "t=" + DateTime.UtcNow.Ticks + "&r=" + Guid.NewGuid().ToString("N");
        Exception last = null;

        string[] urls =
        {
            JsDelivrMain + rel + "?" + bust,
            JsDelivrMain + rel,
            RawMain + rel + "?" + bust,
            RawMain + rel,
        };
        string[] labels = { "jsdelivr+bust", "jsdelivr", "raw+bust", "raw" };

        for (int i = 0; i < urls.Length; i++)
        {
            try
            {
                string body = DownloadString(urls[i]);
                if (string.IsNullOrEmpty(body) || body.StartsWith("404") || body.Contains("404: Not Found"))
                    throw new Exception("Empty or 404");
                if (IsCritical(rel) && !PassesCriticalCheck(rel, body))
                    throw new Exception("stale/cached body failed sanity check");
                source = labels[i];
                return body;
            }
            catch (Exception ex)
            {
                last = ex;
            }
        }

        throw last ?? new Exception("download failed");
    }

    private static string DownloadString(string url)
    {
        using var wc = new WebClient();
        wc.CachePolicy = new RequestCachePolicy(RequestCacheLevel.BypassCache);
        wc.Headers.Add("User-Agent", "DemonKnight-Unity-Sync/" + ExpectedPackageId);
        wc.Headers.Add("Cache-Control", "no-cache, no-store");
        wc.Headers.Add("Pragma", "no-cache");
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
        if (!PassesCriticalCheck(rel, body))
            throw new Exception("sanity check failed after download — CDN stale; use Force Delete+Sync");
    }

    private static bool PassesCriticalCheck(string rel, string body)
    {
        if (rel.EndsWith("GameLoopManager.cs", StringComparison.OrdinalIgnoreCase))
        {
            if (Regex.IsMatch(body, @"FindFirstObjectByType\s*<\s*SomersetChurchUI\s*>\s*\(\s*\)[\s\S]{0,80}?SetNight"))
                return false;
            return body.Contains("WaveVictoryGold")
                   && body.Contains("WaveLossGold")
                   && body.Contains("EnsureDisbandedAfterWave")
                   && body.Contains("mustDisbandPartyAfterCurrentWave")
                   && body.Contains("AutosaveWarband")
                   && (body.Contains("static GameLoopManager Resolve") || body.Contains("ResolveChurchUI"));
        }

        if (rel.EndsWith("DemonKnightCompatApi.cs", StringComparison.OrdinalIgnoreCase))
        {
            return body.Contains("ResolveLoop")
                   && body.Contains("WaveVictoryGold")
                   && body.Contains("ChurchRewireMarch")
                   && body.Contains("EnsureDisbandedAfterWave");
        }

        if (rel.EndsWith("SomersetChurchUI.cs", StringComparison.OrdinalIgnoreCase))
        {
            return body.Contains("RewireMarchButton")
                   && body.Contains("ForceNamedTavernDicePanelsInactive")
                   && body.Contains("EnsureDiceHudAndHidden")
                   && body.Contains("EnsureCodexScrollViewport")
                   && body.Contains("ApplyTwoColumnCodexLayout")
                   && body.Contains("forceEqualHudSlots");
        }

        if (rel.EndsWith("ChurchStageUIController.cs", StringComparison.OrdinalIgnoreCase))
        {
            return Regex.IsMatch(body, @"class\s+ChurchStageUIController\s*:\s*SomersetChurchUI")
                   && !Regex.IsMatch(body, @"void\s+SetNight\s*\(\s*int");
        }

        if (rel.EndsWith("TownSplitUIController.cs", StringComparison.OrdinalIgnoreCase))
        {
            return body.Contains("DemonKnightCompatApi");
        }

        if (rel.EndsWith("LegendCodexUI.cs", StringComparison.OrdinalIgnoreCase)
            || rel.EndsWith("MainMenuUI.cs", StringComparison.OrdinalIgnoreCase)
            || rel.EndsWith("AutoBattlerEngine.cs", StringComparison.OrdinalIgnoreCase)
            || rel.EndsWith("PerkDraftManager.cs", StringComparison.OrdinalIgnoreCase)
            || rel.EndsWith("MerlinResurrectionManager.cs", StringComparison.OrdinalIgnoreCase))
        {
            return body.Contains("DemonKnightCompatApi");
        }

        if (rel.EndsWith("BattlefieldVisualizer.cs", StringComparison.OrdinalIgnoreCase))
        {
            return body.Contains("ResolveArenaBindings") && body.Contains("ApplyBackgroundArt");
        }

        if (rel.EndsWith("DemonKnightPackageVersion.cs", StringComparison.OrdinalIgnoreCase)
            || rel.EndsWith("ArthurianSyncFromGitHub.cs", StringComparison.OrdinalIgnoreCase))
        {
            return body.Contains(ExpectedPackageId);
        }

        return true;
    }
}
#endif
