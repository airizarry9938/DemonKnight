#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Fixes CS0111 duplicate OpenModal/CloseFloatingModals/RefreshHUD in ChurchStageUIController
/// caused by paste-merging downloads into an old file.
/// </summary>
public static class ArthurianRepairChurchStage
{
    private const string RawUrl =
        "https://raw.githubusercontent.com/airizarry9938/DemonKnight/main/Assets/Scripts/UI/ChurchStageUIController.cs";

    [MenuItem("Arthurian/Repair ChurchStageUIController (fix CS0111)", false, 3)]
    public static void Repair()
    {
        string projectRoot = Directory.GetParent(Application.dataPath)!.FullName;
        string correctPath = Path.Combine(projectRoot, "Assets", "Scripts", "UI", "ChurchStageUIController.cs");

        // 1) Find every .cs that declares ChurchStageUIController
        var hits = new List<string>();
        foreach (string path in Directory.GetFiles(Application.dataPath, "*.cs", SearchOption.AllDirectories))
        {
            string text;
            try { text = File.ReadAllText(path); }
            catch { continue; }
            if (Regex.IsMatch(text, @"\bclass\s+ChurchStageUIController\b"))
                hits.Add(path);
        }

        int deleted = 0;
        foreach (string path in hits)
        {
            string norm = path.Replace('\\', '/');
            bool isCorrect = norm.EndsWith("Assets/Scripts/UI/ChurchStageUIController.cs", StringComparison.OrdinalIgnoreCase);
            if (isCorrect) continue;
            try
            {
                File.Delete(path);
                string meta = path + ".meta";
                if (File.Exists(meta)) File.Delete(meta);
                deleted++;
                Debug.Log($"[DemonKnight] Deleted extra ChurchStageUIController at: {path}");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[DemonKnight] Could not delete {path}: {ex.Message}");
            }
        }

        // 2) Always overwrite the correct path from GitHub (clean single copy)
        Directory.CreateDirectory(Path.GetDirectoryName(correctPath)!);
        try
        {
            ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12;
            using var wc = new WebClient();
            wc.Headers.Add("User-Agent", "DemonKnight-Unity-Repair");
            string fresh = wc.DownloadString(RawUrl);
            if (string.IsNullOrEmpty(fresh) || fresh.StartsWith("404"))
                throw new Exception("Empty/404 from GitHub");

            // Sanity: clean file must mention CloseFloatingModals exactly once
            int closes = Regex.Matches(fresh, @"void\s+CloseFloatingModals").Count;
            if (closes != 1)
                throw new Exception($"GitHub file unexpected CloseFloatingModals count={closes}");

            File.WriteAllText(correctPath, fresh);
            Debug.Log($"[DemonKnight] Wrote clean ChurchStageUIController.cs ({fresh.Length} chars) → {correctPath}");
        }
        catch (Exception ex)
        {
            EditorUtility.DisplayDialog(
                "DemonKnight Repair",
                "Could not download clean file from GitHub.\n\n" +
                ex.Message + "\n\n" +
                "Manually DELETE Assets/Scripts/UI/ChurchStageUIController.cs then download:\n" + RawUrl,
                "OK");
            return;
        }

        AssetDatabase.Refresh();
        EditorUtility.DisplayDialog(
            "DemonKnight Repair",
            $"Repaired ChurchStageUIController.\nDeleted {deleted} extra copy(ies).\nOverwrote Scripts/UI/ChurchStageUIController.cs from GitHub.\n\nWait for compile.",
            "OK");
    }
}
#endif
