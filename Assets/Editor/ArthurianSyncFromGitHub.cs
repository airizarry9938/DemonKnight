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
/// One-click: pull every gameplay script from the DemonKnight GitHub main branch
/// into the correct Assets/Scripts folders. Use this instead of hand-copying files.
///
/// IMPORTANT: raw.githubusercontent.com caches /main aggressively. We always
/// append a cache-buster and Prefer jsDelivr as a secondary source when sanity
/// checks fail, otherwise Unity can re-download a stale GameLoopManager forever.
/// </summary>
public static class ArthurianSyncFromGitHub
{
    private const string Repo = "airizarry9938/DemonKnight";
    private const string RawMain = "https://raw.githubusercontent.com/" + Repo + "/main/";
    private const string JsDelivrMain = "https://cdn.jsdelivr.net/gh/" + Repo + "@main/";

    // Marker that must appear in DemonKnightPackageVersion.cs after a good sync.
    private const string ExpectedPackageId = "hud-preserve-authored";

    // Every runtime script the church/pew package needs (relative to repo root).
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

    [MenuItem("Arthurian/Sync All Scripts From GitHub", false, 0)]
    public static void SyncAll()
    {
        if (!EditorUtility.DisplayDialog(
                "DemonKnight Sync",
                "Download and overwrite all DemonKnight scripts from GitHub main?\n\n" +
                "Uses cache-busting (raw.githubusercontent.com often serves stale /main).",
                "Sync Now", "Cancel"))
            return;

        RunSync(showDialog: true);
    }

    [MenuItem("Arthurian/Force Re-Sync Night/HUD Fix", false, 2)]
    public static void ForceResyncNightFix()
    {
        // Minimal set that fixes CS1061 SetNight — always pull these with dual CDN.
        string[] critical =
        {
            "Assets/Scripts/Systems/GameLoopManager.cs",
            "Assets/Scripts/UI/ChurchStageUIController.cs",
            "Assets/Scripts/UI/SomersetChurchUI.cs",
            "Assets/Editor/DemonKnightPackageVersion.cs",
            "Assets/Editor/ArthurianSyncFromGitHub.cs",
        };

        ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12;
        var errors = new List<string>();
        int ok = 0;
        string projectRoot = Directory.GetParent(Application.dataPath)!.FullName;

        try
        {
            EditorUtility.DisplayProgressBar("DemonKnight Force Sync", "Pulling night/HUD fix…", 0f);
            for (int i = 0; i < critical.Length; i++)
            {
                string rel = critical[i];
                EditorUtility.DisplayProgressBar("DemonKnight Force Sync", rel, (float)i / critical.Length);
                try
                {
                    string body = DownloadFresh(rel, out string source);
                    ValidateCritical(rel, body);
                    string dest = Path.Combine(projectRoot, rel.Replace('/', Path.DirectorySeparatorChar));
                    Directory.CreateDirectory(Path.GetDirectoryName(dest)!);
                    File.WriteAllText(dest, body);
                    ok++;
                    Debug.Log($"[DemonKnight] Force-synced {rel} via {source}");
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

        ArthurianFixMisplacedScripts.Run();
        AssetDatabase.Refresh();

        string msg = $"Force-synced {ok}/{critical.Length} night/HUD files.";
        if (errors.Count > 0) msg += "\n\nFailures:\n" + string.Join("\n", errors);
        else msg += $"\n\nDiagnose must show package `{ExpectedPackageId}` after compile.";
        EditorUtility.DisplayDialog("DemonKnight Force Sync", msg, "OK");
    }

    public static void RunSync(bool showDialog)
    {
        ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12;

        var errors = new List<string>();
        int ok = 0;
        string projectRoot = Directory.GetParent(Application.dataPath)!.FullName;

        try
        {
            EditorUtility.DisplayProgressBar("DemonKnight Sync", "Starting…", 0f);
            for (int i = 0; i < ScriptPaths.Length; i++)
            {
                string rel = ScriptPaths[i];
                EditorUtility.DisplayProgressBar(
                    "DemonKnight Sync", rel, (float)i / ScriptPaths.Length);

                string dest = Path.Combine(projectRoot, rel.Replace('/', Path.DirectorySeparatorChar));
                try
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(dest)!);
                    string text = DownloadFresh(rel, out string source);
                    ValidateCritical(rel, text);
                    File.WriteAllText(dest, text);
                    ok++;
                    Debug.Log($"[DemonKnight] Synced {rel} via {source}");
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

        ArthurianFixMisplacedScripts.Run();
        AssetDatabase.Refresh();

        string msg = $"Synced {ok}/{ScriptPaths.Length} files from GitHub.";
        if (errors.Count > 0)
            msg += "\n\nFailures:\n" + string.Join("\n", errors);
        else
            msg += $"\n\nWait for compile. Diagnose must show `{ExpectedPackageId}`.";

        Debug.Log("[DemonKnight] " + msg);
        if (showDialog)
            EditorUtility.DisplayDialog("DemonKnight Sync", msg + "\n\nWait for compile, then Play.", "OK");
    }

    /// <summary>
    /// Download with cache-bust query + BypassCache. Falls back to jsDelivr if the
    /// primary body fails a critical sanity check (stale raw CDN).
    /// </summary>
    private static string DownloadFresh(string rel, out string source)
    {
        string bust = "t=" + DateTime.UtcNow.Ticks + "&r=" + Guid.NewGuid().ToString("N");
        Exception last = null;

        // Prefer jsDelivr first — raw.githubusercontent.com/main has been observed
        // serving multi-commit-stale GameLoopManager bodies even with ?t= busting.
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
                // For critical files, reject known-stale bodies immediately and try next CDN.
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
        return rel.EndsWith("GameLoopManager.cs", StringComparison.OrdinalIgnoreCase)
               || rel.EndsWith("ChurchStageUIController.cs", StringComparison.OrdinalIgnoreCase)
               || rel.EndsWith("SomersetChurchUI.cs", StringComparison.OrdinalIgnoreCase)
               || rel.EndsWith("DemonKnightPackageVersion.cs", StringComparison.OrdinalIgnoreCase);
    }

    private static void ValidateCritical(string rel, string body)
    {
        if (!IsCritical(rel)) return;
        if (!PassesCriticalCheck(rel, body))
            throw new Exception("sanity check failed after download — CDN may still be stale; try Force Re-Sync or overwrite from commit URLs in README");
    }

    private static bool PassesCriticalCheck(string rel, string body)
    {
        if (rel.EndsWith("GameLoopManager.cs", StringComparison.OrdinalIgnoreCase))
        {
            // Must NOT be the old RefreshNightHUD that calls SomersetChurchUI.SetNight.
            if (Regex.IsMatch(body, @"FindFirstObjectByType\s*<\s*SomersetChurchUI\s*>\s*\(\s*\)[\s\S]{0,80}?SetNight"))
                return false;
            // Must have night counter + name-based NightText path (or ApplyNightLabel).
            return body.Contains("currentNight")
                   && (body.Contains("NightText") || body.Contains("ApplyNightLabel"));
        }

        if (rel.EndsWith("SomersetChurchUI.cs", StringComparison.OrdinalIgnoreCase))
        {
            return Regex.IsMatch(body, @"void\s+SetNight\s*\(\s*int");
        }
        if (rel.EndsWith("ChurchStageUIController.cs", StringComparison.OrdinalIgnoreCase))
        {
            // Stub only — must inherit SomersetChurchUI and must NOT redeclare SetNight (CS0108).
            return Regex.IsMatch(body, @"class\s+ChurchStageUIController\s*:\s*SomersetChurchUI")
                   && !Regex.IsMatch(body, @"void\s+SetNight\s*\(\s*int");
        }

        if (rel.EndsWith("DemonKnightPackageVersion.cs", StringComparison.OrdinalIgnoreCase))
        {
            return body.Contains(ExpectedPackageId);
        }

        return true;
    }
}
#endif
