#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Keeps Assets/Editor clean: only Arthurian/Somerset editor tools are allowed.
/// Any gameplay script dropped into Editor (TownSplitUIController, GameLoopManager, …)
/// is moved to Scripts/ or deleted if Scripts/ already has it — stops CS0029 duplicate-type errors.
/// </summary>
[InitializeOnLoad]
public static class ArthurianFixMisplacedScripts
{
    private static readonly HashSet<string> AllowedEditorScripts = new HashSet<string>
    {
        "ArthurianAlignCrowdPews.cs",
        "ArthurianFixMisplacedScripts.cs",
        "ArthurianMenuBootstrap.cs",
        "ArthurianSceneBuilder.cs",
        "ArthurianSyncFromGitHub.cs",
        "DemonKnightPackageVersion.cs",
        "SomersetAssetStudioWindow.cs",
    };

    private static readonly Dictionary<string, string> KnownGameplayTargets = new Dictionary<string, string>
    {
        { "ChurchStageUIController.cs", "Scripts/UI/ChurchStageUIController.cs" },
        { "ChurchPewLayout.cs", "Scripts/UI/ChurchPewLayout.cs" },
        { "PaperDollSeatView.cs", "Scripts/UI/PaperDollSeatView.cs" },
        { "TownSplitUIController.cs", "Scripts/UI/TownSplitUIController.cs" },
        { "LegendCodexUI.cs", "Scripts/UI/LegendCodexUI.cs" },
        { "GameLoopManager.cs", "Scripts/Systems/GameLoopManager.cs" },
        { "TownRNGGenerator.cs", "Scripts/Systems/TownRNGGenerator.cs" },
        { "TavernDiceMinigame.cs", "Scripts/Systems/TavernDiceMinigame.cs" },
        { "CardFusionSystem.cs", "Scripts/Systems/CardFusionSystem.cs" },
        { "PerkDraftManager.cs", "Scripts/Systems/PerkDraftManager.cs" },
        { "DemonicPactManager.cs", "Scripts/Systems/DemonicPactManager.cs" },
        { "RelicManager.cs", "Scripts/Systems/RelicManager.cs" },
        { "SinRuneManager.cs", "Scripts/Systems/SinRuneManager.cs" },
        { "GameDataTypes.cs", "Scripts/Data/GameDataTypes.cs" },
        { "SomersetRunes.cs", "Scripts/Data/SomersetRunes.cs" },
        { "SomersetVisualDatabase.cs", "Scripts/Data/SomersetVisualDatabase.cs" },
        { "GrudgeSaveManager.cs", "Scripts/Persistence/GrudgeSaveManager.cs" },
        { "MerlinResurrectionManager.cs", "Scripts/Persistence/MerlinResurrectionManager.cs" },
        { "AutoBattlerEngine.cs", "Scripts/Combat/AutoBattlerEngine.cs" },
        { "BattlefieldVisualizer.cs", "Scripts/Combat/BattlefieldVisualizer.cs" },
        { "DemonHordeGenerator.cs", "Scripts/Combat/DemonHordeGenerator.cs" },
        { "FloatingCombatText.cs", "Scripts/Combat/FloatingCombatText.cs" },
        { "UnitVisualActor.cs", "Scripts/Combat/UnitVisualActor.cs" },
    };

    static ArthurianFixMisplacedScripts()
    {
        EditorApplication.delayCall += Run;
    }

    [MenuItem("Arthurian/Fix Misplaced Scripts", false, 12)]
    public static void Run()
    {
        string assets = Application.dataPath;
        string editorDir = Path.Combine(assets, "Editor");
        if (!Directory.Exists(editorDir)) return;

        int fixedCount = 0;

        foreach (string path in Directory.GetFiles(editorDir, "*.cs", SearchOption.TopDirectoryOnly))
        {
            string fileName = Path.GetFileName(path);
            if (AllowedEditorScripts.Contains(fileName)) continue;

            // Known gameplay → Scripts/ (delete Editor copy if Scripts already has it)
            if (KnownGameplayTargets.TryGetValue(fileName, out string toRel))
            {
                string to = Path.Combine(assets, toRel.Replace('/', Path.DirectorySeparatorChar));
                Directory.CreateDirectory(Path.GetDirectoryName(to)!);
                if (!File.Exists(to))
                {
                    File.Move(path, to);
                    string meta = path + ".meta";
                    if (File.Exists(meta)) File.Move(meta, to + ".meta");
                    Debug.Log($"[DemonKnight] Moved Editor/{fileName} → {toRel}");
                }
                else
                {
                    DeleteFileAndMeta(path);
                    Debug.Log($"[DemonKnight] Deleted Editor/{fileName} (duplicate of {toRel}) — this was causing CS0029.");
                }
                fixedCount++;
                continue;
            }

            // Anything else unexpected in Editor (NewMonoBehaviourScript, random pastes)
            DeleteFileAndMeta(path);
            Debug.LogWarning($"[DemonKnight] Deleted unexpected Editor script: {fileName}");
            fixedCount++;
        }

        if (fixedCount > 0)
        {
            AssetDatabase.Refresh();
            EditorUtility.DisplayDialog(
                "DemonKnight",
                $"Cleaned {fixedCount} file(s) out of Assets/Editor/.\n\n" +
                "Only Arthurian* / Somerset* tools belong there.\n" +
                "Gameplay scripts belong under Assets/Scripts/.\n\n" +
                "Wait for compile.",
                "OK");
        }
    }

    private static void DeleteFileAndMeta(string path)
    {
        if (File.Exists(path)) File.Delete(path);
        string meta = path + ".meta";
        if (File.Exists(meta)) File.Delete(meta);
    }
}
#endif
