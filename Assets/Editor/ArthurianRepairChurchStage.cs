#if UNITY_EDITOR
using System;
using System.IO;
using System.Net;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Repairs CS0111 on ChurchStageUIController by installing:
///   SomersetChurchUI.cs          — full logic (one copy of each method)
///   ChurchStageUIController.cs   — tiny stub that only inherits
/// and deleting any other ChurchStage*.cs junk copies.
/// </summary>
public static class ArthurianRepairChurchStage
{
    private const string RawRoot =
        "https://raw.githubusercontent.com/airizarry9938/DemonKnight/main/";

    [MenuItem("Arthurian/Repair Church UI (fix CS0111)", false, 3)]
    public static void Repair()
    {
        string projectRoot = Directory.GetParent(Application.dataPath)!.FullName;
        string uiDir = Path.Combine(projectRoot, "Assets", "Scripts", "UI");
        Directory.CreateDirectory(uiDir);

        ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12;

        // Delete every ChurchStage*.cs except we'll rewrite the stub path
        int deleted = 0;
        foreach (string path in Directory.GetFiles(Application.dataPath, "*.cs", SearchOption.AllDirectories))
        {
            string name = Path.GetFileName(path);
            string text;
            try { text = File.ReadAllText(path); }
            catch { continue; }

            bool isChurchStageFile = name.StartsWith("ChurchStage", StringComparison.OrdinalIgnoreCase)
                || Regex.IsMatch(text, @"\bclass\s+ChurchStageUIController\b");
            bool isSomersetFile = name.Equals("SomersetChurchUI.cs", StringComparison.OrdinalIgnoreCase);

            // Keep nothing — we rewrite both targets below. Delete extras / broken merges.
            string norm = path.Replace('\\', '/');
            bool isTargetStub = norm.EndsWith("Assets/Scripts/UI/ChurchStageUIController.cs", StringComparison.OrdinalIgnoreCase);
            bool isTargetLogic = norm.EndsWith("Assets/Scripts/UI/SomersetChurchUI.cs", StringComparison.OrdinalIgnoreCase);

            if (isSomersetFile || isChurchStageFile)
            {
                if (isTargetStub || isTargetLogic) continue; // overwrite in place next
                try
                {
                    File.Delete(path);
                    if (File.Exists(path + ".meta")) File.Delete(path + ".meta");
                    deleted++;
                    Debug.Log($"[DemonKnight] Deleted junk: {path}");
                }
                catch (Exception ex)
                {
                    Debug.LogError(ex.Message);
                }
            }
        }

        try
        {
            using var wc = new WebClient();
            wc.Headers.Add("User-Agent", "DemonKnight-Unity-Repair");

            string logic = wc.DownloadString(RawRoot + "Assets/Scripts/UI/SomersetChurchUI.cs");
            string stub = wc.DownloadString(RawRoot + "Assets/Scripts/UI/ChurchStageUIController.cs");

            if (string.IsNullOrEmpty(logic) || logic.StartsWith("404"))
                throw new Exception("Failed to download SomersetChurchUI.cs");
            if (string.IsNullOrEmpty(stub) || stub.StartsWith("404"))
                throw new Exception("Failed to download ChurchStageUIController.cs stub");

            int opens = Regex.Matches(logic, @"void\s+OpenModal").Count;
            if (opens != 1)
                throw new Exception($"SomersetChurchUI.cs has OpenModal count={opens}, expected 1");

            File.WriteAllText(Path.Combine(uiDir, "SomersetChurchUI.cs"), logic);
            File.WriteAllText(Path.Combine(uiDir, "ChurchStageUIController.cs"), stub);

            Debug.Log("[DemonKnight] Installed SomersetChurchUI.cs + thin ChurchStageUIController stub.");
        }
        catch (Exception ex)
        {
            EditorUtility.DisplayDialog(
                "DemonKnight Repair",
                "Download failed: " + ex.Message + "\n\n" +
                "Manual fix:\n" +
                "1) Delete Assets/Scripts/UI/ChurchStageUIController.cs\n" +
                "2) Download BOTH:\n" +
                RawRoot + "Assets/Scripts/UI/SomersetChurchUI.cs\n" +
                RawRoot + "Assets/Scripts/UI/ChurchStageUIController.cs\n" +
                "into Assets/Scripts/UI/",
                "OK");
            return;
        }

        AssetDatabase.Refresh();
        EditorUtility.DisplayDialog(
            "DemonKnight Repair",
            $"Church UI repaired.\nDeleted {deleted} junk file(s).\n" +
            "Logic is now in SomersetChurchUI.cs.\nChurchStageUIController.cs is a tiny stub only.\n\nWait for compile.",
            "OK");
    }
}
#endif
