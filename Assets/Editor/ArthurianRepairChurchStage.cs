#if UNITY_EDITOR
using System;
using System.IO;
using System.Net;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

/// <summary>
/// One-click repair for the common download mess:
/// wrong content in ChurchPewLayout.cs / duplicated ChurchStageUIController methods.
/// Installs Somerset* logic files + tiny stubs.
/// </summary>
public static class ArthurianRepairChurchStage
{
    private const string RawRoot =
        "https://raw.githubusercontent.com/airizarry9938/DemonKnight/main/";
    private const string JsDelivrRoot =
        "https://cdn.jsdelivr.net/gh/airizarry9938/DemonKnight@main/";

    private static readonly (string rel, string check)[] Required =
    {
        ("Assets/Scripts/UI/SomersetPewLayout.cs", @"class\s+SomersetPewLayout"),
        ("Assets/Scripts/UI/SomersetChurchUI.cs", @"void\s+SetNight\s*\(\s*int"),
        ("Assets/Scripts/UI/ChurchPewLayout.cs", @"class\s+ChurchPewLayout\s*:\s*SomersetPewLayout"),
        ("Assets/Scripts/UI/ChurchStageUIController.cs", @"class\s+ChurchStageUIController\s*:\s*SomersetChurchUI"),
        ("Assets/Scripts/Data/SomersetRunes.cs", @"class\s+SomersetRunes"),
        ("Assets/Scripts/Data/GameDataTypes.cs", @"class\s+RecruitInstance"),
        ("Assets/Scripts/UI/PaperDollSeatView.cs", @"class\s+PaperDollSeatView"),
        ("Assets/Scripts/UI/TownSplitUIController.cs", @"class\s+TownSplitUIController"),
        // ApplyNightLabel = new RefreshNightHUD that does NOT call SetNight (CS1061-safe).
        ("Assets/Scripts/Systems/GameLoopManager.cs", @"ApplyNightLabel"),
        ("Assets/Scripts/Persistence/MerlinResurrectionManager.cs", @"void\s+StartNextTimelineRun"),
        ("Assets/Editor/DemonKnightPackageVersion.cs", @"setnight-sync-cachebust"),
    };

    [MenuItem("Arthurian/Repair Church UI (fix CS0111/CS0263)", false, 3)]
    public static void Repair()
    {
        string projectRoot = Directory.GetParent(Application.dataPath)!.FullName;
        ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12;

        // Delete any .cs that wrongly declares ChurchStageUIController outside the stub file,
        // or declares ChurchPewLayout as something other than the stub.
        int deleted = 0;
        foreach (string path in Directory.GetFiles(Application.dataPath, "*.cs", SearchOption.AllDirectories))
        {
            string norm = path.Replace('\\', '/');
            string text;
            try { text = File.ReadAllText(path); }
            catch { continue; }

            bool isStubChurch = norm.EndsWith("Assets/Scripts/UI/ChurchStageUIController.cs", StringComparison.OrdinalIgnoreCase);
            bool isStubPew = norm.EndsWith("Assets/Scripts/UI/ChurchPewLayout.cs", StringComparison.OrdinalIgnoreCase);
            bool isLogicChurch = norm.EndsWith("Assets/Scripts/UI/SomersetChurchUI.cs", StringComparison.OrdinalIgnoreCase);
            bool isLogicPew = norm.EndsWith("Assets/Scripts/UI/SomersetPewLayout.cs", StringComparison.OrdinalIgnoreCase);

            bool badChurchInWrongFile = Regex.IsMatch(text, @"\bclass\s+ChurchStageUIController\b") && !isStubChurch;
            bool badPartialInPewFile = isStubPew && Regex.IsMatch(text, @"\bChurchStageUIController\b");
            bool hugeStub = (isStubChurch || isStubPew) && text.Length > 800;

            if (badChurchInWrongFile || badPartialInPewFile || hugeStub)
            {
                // Don't delete the Somerset logic files
                if (isLogicChurch || isLogicPew) continue;
                try
                {
                    File.Delete(path);
                    if (File.Exists(path + ".meta")) File.Delete(path + ".meta");
                    deleted++;
                    Debug.Log($"[DemonKnight] Deleted corrupted script: {path}");
                }
                catch (Exception ex) { Debug.LogError(ex.Message); }
            }
        }

        var failures = new System.Collections.Generic.List<string>();
        int ok = 0;
        string bust = "?t=" + DateTime.UtcNow.Ticks;
        foreach (var (rel, check) in Required)
        {
            string dest = Path.Combine(projectRoot, rel.Replace('/', Path.DirectorySeparatorChar));
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(dest)!);
                string body = null;
                Exception last = null;
                foreach (string root in new[] { RawRoot, JsDelivrRoot })
                {
                    try
                    {
                        using var wc = new WebClient();
                        wc.CachePolicy = new System.Net.Cache.RequestCachePolicy(
                            System.Net.Cache.RequestCacheLevel.BypassCache);
                        wc.Headers.Add("User-Agent", "DemonKnight-Unity-Repair");
                        wc.Headers.Add("Cache-Control", "no-cache");
                        body = wc.DownloadString(root + rel + bust);
                        if (string.IsNullOrEmpty(body) || body.StartsWith("404"))
                            throw new Exception("404/empty");
                        if (!Regex.IsMatch(body, check))
                            throw new Exception("sanity check failed (likely stale CDN): " + check);
                        break;
                    }
                    catch (Exception ex)
                    {
                        last = ex;
                        body = null;
                    }
                }
                if (body == null) throw last ?? new Exception("download failed");
                File.WriteAllText(dest, body);
                ok++;
                Debug.Log($"[DemonKnight] Wrote {rel}");
            }
            catch (Exception ex)
            {
                failures.Add($"{rel}: {ex.Message}");
            }
        }

        AssetDatabase.Refresh();

        string msg = $"Wrote {ok}/{Required.Length} files. Deleted {deleted} corrupted file(s).";
        if (failures.Count > 0)
            msg += "\n\nFailures:\n" + string.Join("\n", failures);
        else
            msg += "\n\nChurchPewLayout.cs and ChurchStageUIController.cs are now tiny stubs.\nLogic is in SomersetPewLayout.cs + SomersetChurchUI.cs.\nWait for compile.";

        EditorUtility.DisplayDialog("DemonKnight Repair", msg, "OK");
    }
}
#endif
