#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Moves/deletes gameplay scripts that were dropped into Assets/Editor by mistake.
/// </summary>
[InitializeOnLoad]
public static class ArthurianFixMisplacedScripts
{
    static ArthurianFixMisplacedScripts()
    {
        EditorApplication.delayCall += Run;
    }

    [MenuItem("Arthurian/Fix Misplaced Scripts", false, 12)]
    public static void Run()
    {
        string assets = Application.dataPath;
        string editorDir = Path.Combine(assets, "Editor");

        // Unity default stubs often get pasted over with our tools → duplicate type errors.
        DeleteIfContains(Path.Combine(editorDir, "NewMonoBehaviourScript.cs"),
            "ArthurianFixMisplacedScripts", "ArthurianSyncFromGitHub", "ArthurianAlignCrowdPews",
            "GameLoopManager", "ChurchStageUIController");

        // Gameplay scripts must NEVER live under Assets/Editor/.
        var moves = new (string fileName, string toRel)[]
        {
            ("ChurchStageUIController.cs", "Scripts/UI/ChurchStageUIController.cs"),
            ("ChurchPewLayout.cs", "Scripts/UI/ChurchPewLayout.cs"),
            ("PaperDollSeatView.cs", "Scripts/UI/PaperDollSeatView.cs"),
            ("TownSplitUIController.cs", "Scripts/UI/TownSplitUIController.cs"),
            ("LegendCodexUI.cs", "Scripts/UI/LegendCodexUI.cs"),
            ("GameLoopManager.cs", "Scripts/Systems/GameLoopManager.cs"),
            ("TownRNGGenerator.cs", "Scripts/Systems/TownRNGGenerator.cs"),
            ("TavernDiceMinigame.cs", "Scripts/Systems/TavernDiceMinigame.cs"),
            ("CardFusionSystem.cs", "Scripts/Systems/CardFusionSystem.cs"),
            ("PerkDraftManager.cs", "Scripts/Systems/PerkDraftManager.cs"),
            ("DemonicPactManager.cs", "Scripts/Systems/DemonicPactManager.cs"),
            ("RelicManager.cs", "Scripts/Systems/RelicManager.cs"),
            ("SinRuneManager.cs", "Scripts/Systems/SinRuneManager.cs"),
            ("GameDataTypes.cs", "Scripts/Data/GameDataTypes.cs"),
            ("SomersetRunes.cs", "Scripts/Data/SomersetRunes.cs"),
            ("SomersetVisualDatabase.cs", "Scripts/Data/SomersetVisualDatabase.cs"),
            ("GrudgeSaveManager.cs", "Scripts/Persistence/GrudgeSaveManager.cs"),
            ("MerlinResurrectionManager.cs", "Scripts/Persistence/MerlinResurrectionManager.cs"),
            ("AutoBattlerEngine.cs", "Scripts/Combat/AutoBattlerEngine.cs"),
            ("BattlefieldVisualizer.cs", "Scripts/Combat/BattlefieldVisualizer.cs"),
            ("DemonHordeGenerator.cs", "Scripts/Combat/DemonHordeGenerator.cs"),
            ("FloatingCombatText.cs", "Scripts/Combat/FloatingCombatText.cs"),
            ("UnitVisualActor.cs", "Scripts/Combat/UnitVisualActor.cs"),
        };

        int fixedCount = 0;
        foreach (var (fileName, toRel) in moves)
        {
            string from = Path.Combine(editorDir, fileName);
            if (!File.Exists(from)) continue;

            string to = Path.Combine(assets, toRel.Replace('/', Path.DirectorySeparatorChar));
            Directory.CreateDirectory(Path.GetDirectoryName(to)!);

            // Always prefer Scripts/ copy: delete the Editor misplaced file.
            // If Scripts/ is missing, move Editor → Scripts.
            if (File.Exists(to))
            {
                File.Delete(from);
                string meta = from + ".meta";
                if (File.Exists(meta)) File.Delete(meta);
                Debug.Log($"[DemonKnight] Deleted misplaced Assets/Editor/{fileName} (use {toRel}).");
            }
            else
            {
                File.Move(from, to);
                string meta = from + ".meta";
                if (File.Exists(meta)) File.Move(meta, to + ".meta");
                Debug.Log($"[DemonKnight] Moved Editor/{fileName} → {toRel}");
            }
            fixedCount++;
        }

        if (fixedCount > 0)
        {
            AssetDatabase.Refresh();
            EditorUtility.DisplayDialog(
                "DemonKnight",
                $"Removed/moved {fixedCount} gameplay script(s) out of Assets/Editor/.\nWait for compile.",
                "OK");
        }
    }

    private static void DeleteIfContains(string path, params string[] needles)
    {
        if (!File.Exists(path)) return;
        string text = File.ReadAllText(path);
        foreach (string needle in needles)
        {
            if (text.IndexOf(needle, System.StringComparison.Ordinal) < 0) continue;
            File.Delete(path);
            string meta = path + ".meta";
            if (File.Exists(meta)) File.Delete(meta);
            Debug.Log($"[DemonKnight] Deleted duplicate stub: {path}");
            return;
        }
    }
}
#endif
