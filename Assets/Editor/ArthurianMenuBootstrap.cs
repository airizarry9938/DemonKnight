#if UNITY_EDITOR
using System;
using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Always-available menu entries that do not depend on TextMeshPro or gameplay scripts.
/// If you can see Arthurian → Diagnose Project but not Align / Build menus,
/// open Console — gameplay or Editor scripts failed to compile (usually missing TMP).
/// </summary>
public static class ArthurianMenuBootstrap
{
    [MenuItem("Arthurian/Diagnose Project", false, 1)]
    public static void DiagnoseProject()
    {
        bool hasTmp = Type.GetType("TMPro.TextMeshProUGUI, Unity.TextMeshPro") != null
                      || Type.GetType("TMPro.TextMeshProUGUI, Unity.ugui") != null;

        if (!hasTmp)
        {
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                try
                {
                    if (asm.GetType("TMPro.TextMeshProUGUI", false) != null)
                    {
                        hasTmp = true;
                        break;
                    }
                }
                catch { /* ignore */ }
            }
        }

        bool hasGameplay = TypeExists("ArthurianAutobattler.GameLoopManager");
        bool hasPews = TypeExists("ArthurianAutobattler.SomersetPewLayout");
        bool hasBuilder = TypeExists("ArthurianSceneBuilder");
        bool hasAlignType = TypeExists("ArthurianAlignCrowdPews");

        string packageId = "(no version stamp — old first build)";
        string packageGit = "?";
        var verType = Type.GetType("DemonKnightPackageVersion")
                      ?? Type.GetType("DemonKnightPackageVersion, Assembly-CSharp-Editor");
        if (verType == null)
        {
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                try
                {
                    verType = asm.GetType("DemonKnightPackageVersion", false);
                    if (verType != null) break;
                }
                catch { /* ignore */ }
            }
        }
        if (verType != null)
        {
            packageId = verType.GetField("Id")?.GetValue(null) as string ?? packageId;
            packageGit = verType.GetField("GitHint")?.GetValue(null) as string ?? packageGit;
        }

        string editorDir = Path.Combine(Application.dataPath, "Editor");
        string alignPath = Path.Combine(editorDir, "ArthurianAlignCrowdPews.cs");
        string builderPath = Path.Combine(editorDir, "ArthurianSceneBuilder.cs");
        string syncPath = Path.Combine(editorDir, "ArthurianSyncFromGitHub.cs");
        string menuBootPath = Path.Combine(editorDir, "ArthurianMenuBootstrap.cs");
        bool alignFileOnDisk = File.Exists(alignPath);
        bool syncOnDisk = File.Exists(syncPath);
        bool menuBootOnDisk = File.Exists(menuBootPath);
        bool hasSyncType = TypeExists("ArthurianSyncFromGitHub");
        string builderStamp = File.Exists(builderPath)
            ? File.GetLastWriteTimeUtc(builderPath).ToString("u") + " UTC"
            : "MISSING";

        bool looksLikeFirstBuild = !hasPews || packageId.StartsWith("(no version");

        string msg =
            "DemonKnight / Arthurian diagnose\n\n" +
            $"Package: {packageId} ({packageGit})\n" +
            $"Assets path:\n{Application.dataPath}\n" +
            $"Sync menus on disk: {(syncOnDisk ? "YES" : "NO — Bootstrap/Force Sync MISSING")}\n" +
            $"Sync menus compiled: {(hasSyncType ? "YES" : "NO")}\n" +
            $"Diagnose host on disk: {(menuBootOnDisk ? "YES" : "NO")}\n" +
            $"Align script on disk: {(alignFileOnDisk ? "YES" : "NO — Unity never got the update")}\n" +
            $"Align menu compiled: {(hasAlignType ? "YES" : "NO")}\n" +
            $"ArthurianSceneBuilder.cs mtime: {builderStamp}\n\n" +
            $"TextMeshPro loaded: {(hasTmp ? "YES" : "NO")}\n" +
            $"Gameplay scripts: {(hasGameplay ? "YES" : "NO")}\n" +
            $"Church pew layout: {(hasPews ? "YES" : "NO")}\n" +
            $"Scene builder: {(hasBuilder ? "YES" : "NO")}\n\n";

        if (!syncOnDisk || !hasSyncType)
        {
            msg +=
                "SYNC MENUS MISSING.\n" +
                "Overwrite Assets/Editor/ArthurianSyncFromGitHub.cs + ArthurianMenuBootstrap.cs from jsDelivr (pinned SHA),\n" +
                "then Assets → Refresh. See ANDREW-SYNC-KEEP-EDITOR-MENUS.md.\n\n";
        }

        if (!alignFileOnDisk)
        {
            msg +=
                "ALIGN SCRIPT MISSING FROM THIS UNITY FOLDER.\n" +
                "Cloud pushes do not land here until you pull or copy the file.\n\n" +
                "FIX (pick one):\n" +
                "A) In a terminal at this project root: git pull\n" +
                "B) Download into Assets/Editor/ then Refresh:\n" +
                "https://raw.githubusercontent.com/airizarry9938/DemonKnight/main/Assets/Editor/ArthurianAlignCrowdPews.cs\n\n" +
                "Expected path:\n" + alignPath;
        }
        else if (looksLikeFirstBuild)
        {
            msg +=
                "THIS IS THE OLD FIRST BUILD.\n" +
                "Unity is not reading the updated DemonKnight package.\n\n" +
                "FIX: git pull in the Unity Hub project folder, then Assets → Refresh.";
        }
        else if (!hasTmp)
        {
            msg +=
                "FIX: Window → TextMeshPro → Import TMP Essential Resources.\n" +
                "Wait for compile, then check Console for red errors.";
        }
        else if (!hasGameplay || !hasBuilder)
        {
            msg +=
                "FIX: Open Window → General → Console and clear red compile errors.\n" +
                "After scripts compile you should see:\n" +
                "• Arthurian → Align Crowd Grid To Church Pews\n" +
                "• Arthurian → Build Complete Scene Hierarchy";
        }
        else
        {
            msg +=
                "Package looks current.\n" +
                "• Already have ChurchBackground? → Arthurian → Align Crowd Grid To Church Pews\n" +
                "• Need a fresh scene? → Arthurian → Build Complete Scene Hierarchy\n" +
                "(Full Build replaces the scene — that feels like “defaulting to first build.”)";
        }

        Debug.Log(msg);
        EditorUtility.DisplayDialog("Arthurian Diagnose", msg, "OK");
    }

    private static bool TypeExists(string fullName)
    {
        foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
        {
            try
            {
                if (asm.GetType(fullName, false) != null) return true;
            }
            catch { /* ignore */ }
        }
        return false;
    }

    [MenuItem("Arthurian/Open Setup Guide", false, 2)]
    public static void OpenSetupGuide()
    {
        EditorUtility.RevealInFinder(Application.dataPath);
        EditorUtility.DisplayDialog(
            "DemonKnight Setup",
            "1) Confirm Assets/Scripts/UI/SomersetPewLayout.cs exists.\n" +
            "2) Wait for compile (bottom-right spinner).\n" +
            "3) Arthurian → Diagnose Project (must show character-registry-battle-sprites).\n" +
            "   Then DemonKnight → Characters → Seed Default Character Definitions.\n" +
            "   If Bootstrap / Force Delete+Sync missing: overwrite ArthurianSyncFromGitHub.cs\n" +
            "   + ArthurianMenuBootstrap.cs from jsDelivr (pinned SHA) — see README.\n" +
            "4) If you already added ChurchBackground: Arthurian → Align Crowd Grid To Church Pews.\n" +
            "5) Only use Build Complete Scene Hierarchy for a brand-new scene.\n\n" +
            "Project Assets path:\n" + Application.dataPath,
            "OK");
        Debug.Log("[DemonKnight] Assets path: " + Application.dataPath);
    }
}
#endif
