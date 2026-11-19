#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Moves gameplay scripts that were dropped into Assets/Editor by mistake,
/// and flags a stale GameDataTypes (missing pewSeatIndex).
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

        // Unity default stubs often get pasted over with our tools → duplicate type errors.
        DeleteIfContains(Path.Combine(assets, "Editor", "NewMonoBehaviourScript.cs"),
            "ArthurianFixMisplacedScripts", "ArthurianSyncFromGitHub", "ArthurianAlignCrowdPews");

        var moves = new (string fromRel, string toRel)[]
        {
            ("Editor/ChurchStageUIController.cs", "Scripts/UI/ChurchStageUIController.cs"),
            ("Editor/ChurchPewLayout.cs", "Scripts/UI/ChurchPewLayout.cs"),
            ("Editor/PaperDollSeatView.cs", "Scripts/UI/PaperDollSeatView.cs"),
            ("Editor/TownSplitUIController.cs", "Scripts/UI/TownSplitUIController.cs"),
            ("Editor/GameLoopManager.cs", "Scripts/Systems/GameLoopManager.cs"),
            ("Editor/GameDataTypes.cs", "Scripts/Data/GameDataTypes.cs"),
            ("Editor/TownRNGGenerator.cs", "Scripts/Systems/TownRNGGenerator.cs"),
        };

        int moved = 0;
        foreach (var (fromRel, toRel) in moves)
        {
            string from = Path.Combine(assets, fromRel.Replace('/', Path.DirectorySeparatorChar));
            string to = Path.Combine(assets, toRel.Replace('/', Path.DirectorySeparatorChar));
            if (!File.Exists(from)) continue;

            Directory.CreateDirectory(Path.GetDirectoryName(to)!);
            if (File.Exists(to))
            {
                // Prefer the Editor drop only if destination is older / missing pew fields —
                // otherwise delete the misplaced copy.
                File.Delete(from);
                string meta = from + ".meta";
                if (File.Exists(meta)) File.Delete(meta);
                Debug.Log($"[DemonKnight] Removed misplaced {fromRel} (correct copy already at {toRel}).");
            }
            else
            {
                File.Move(from, to);
                string meta = from + ".meta";
                if (File.Exists(meta)) File.Move(meta, to + ".meta");
                Debug.Log($"[DemonKnight] Moved {fromRel} → {toRel}");
            }
            moved++;
        }

        string dataTypes = Path.Combine(assets, "Scripts", "Data", "GameDataTypes.cs");
        bool hasPew = File.Exists(dataTypes) && File.ReadAllText(dataTypes).Contains("pewSeatIndex");
        if (!hasPew)
        {
            Debug.LogError(
                "[DemonKnight] GameDataTypes.cs is the OLD first build (no pewSeatIndex).\n" +
                "Download and overwrite Assets/Scripts/Data/GameDataTypes.cs:\n" +
                "https://raw.githubusercontent.com/airizarry9938/DemonKnight/main/Assets/Scripts/Data/GameDataTypes.cs");
            EditorUtility.DisplayDialog(
                "DemonKnight — Stale GameDataTypes",
                "RecruitInstance is missing pewSeatIndex.\n\n" +
                "Overwrite Assets/Scripts/Data/GameDataTypes.cs with the GitHub file, then Refresh.\n\n" +
                "Also delete Assets/Editor/ChurchStageUIController.cs if it is still there — that script belongs in Assets/Scripts/UI/.",
                "OK");
        }
        else if (moved > 0)
        {
            AssetDatabase.Refresh();
            EditorUtility.DisplayDialog(
                "DemonKnight",
                $"Fixed {moved} misplaced script(s). Wait for compile.",
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
