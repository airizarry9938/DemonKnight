#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using UnityEditor;
using UnityEngine;

/// <summary>
/// One-click: pull every gameplay script from the DemonKnight GitHub main branch
/// into the correct Assets/Scripts folders. Use this instead of hand-copying files.
/// </summary>
public static class ArthurianSyncFromGitHub
{
    private const string RawRoot =
        "https://raw.githubusercontent.com/airizarry9938/DemonKnight/main/";

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
        "Assets/Scripts/Systems/CardFusionSystem.cs",
        "Assets/Scripts/Systems/DemonicPactManager.cs",
        "Assets/Scripts/Systems/GameLoopManager.cs",
        "Assets/Scripts/Systems/PerkDraftManager.cs",
        "Assets/Scripts/Systems/RelicManager.cs",
        "Assets/Scripts/Systems/SinRuneManager.cs",
        "Assets/Scripts/Systems/TavernDiceMinigame.cs",
        "Assets/Scripts/Systems/TownRNGGenerator.cs",
        "Assets/Scripts/UI/SomersetPewLayout.cs",
        "Assets/Scripts/UI/ChurchStageUIController.cs",
        "Assets/Scripts/UI/SomersetChurchUI.cs",
        "Assets/Scripts/UI/LegendCodexUI.cs",
        "Assets/Scripts/UI/PaperDollSeatView.cs",
        "Assets/Scripts/UI/TownSplitUIController.cs",
        "Assets/Editor/ArthurianAlignCrowdPews.cs",
        "Assets/Editor/ArthurianFixMisplacedScripts.cs",
        "Assets/Editor/ArthurianRepairChurchStage.cs",
        "Assets/Editor/ArthurianMenuBootstrap.cs",
        "Assets/Editor/ArthurianSceneBuilder.cs",
        "Assets/Editor/DemonKnightPackageVersion.cs",
        "Assets/Editor/SomersetAssetStudioWindow.cs",
    };

    [MenuItem("Arthurian/Sync All Scripts From GitHub", false, 0)]
    public static void SyncAll()
    {
        if (!EditorUtility.DisplayDialog(
                "DemonKnight Sync",
                "Download and overwrite all DemonKnight scripts from GitHub main?\n\n" +
                "This fixes mixed old/new copies (SinRuneManager, GetRuneRank, pew seats, etc.).",
                "Sync Now", "Cancel"))
            return;

        // TLS for GitHub raw
        ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12;

        var errors = new List<string>();
        int ok = 0;

        try
        {
            EditorUtility.DisplayProgressBar("DemonKnight Sync", "Starting…", 0f);
            for (int i = 0; i < ScriptPaths.Length; i++)
            {
                string rel = ScriptPaths[i];
                EditorUtility.DisplayProgressBar(
                    "DemonKnight Sync", rel, (float)i / ScriptPaths.Length);

                string url = RawRoot + rel;
                string dest = Path.Combine(Directory.GetParent(Application.dataPath)!.FullName, rel);
                try
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(dest)!);
                    using (var wc = new WebClient())
                    {
                        wc.Headers.Add("User-Agent", "DemonKnight-Unity-Sync");
                        string text = wc.DownloadString(url);
                        if (string.IsNullOrEmpty(text) || text.StartsWith("404"))
                            throw new Exception("Empty or 404 response");
                        File.WriteAllText(dest, text);
                    }
                    ok++;
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

        // Remove gameplay scripts wrongly left under Assets/Editor/
        ArthurianFixMisplacedScripts.Run();

        AssetDatabase.Refresh();

        string msg = $"Synced {ok}/{ScriptPaths.Length} files from GitHub.";
        if (errors.Count > 0)
            msg += "\n\nFailures:\n" + string.Join("\n", errors);

        Debug.Log("[DemonKnight] " + msg);
        EditorUtility.DisplayDialog("DemonKnight Sync", msg + "\n\nWait for compile, then Play.", "OK");
    }
}
#endif
