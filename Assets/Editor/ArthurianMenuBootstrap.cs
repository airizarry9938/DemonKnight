#if UNITY_EDITOR
using System;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Always-available menu entries that do not depend on TextMeshPro or gameplay scripts.
/// If you can see Arthurian → Diagnose Project but not Build Complete Scene Hierarchy,
/// open Console — gameplay scripts failed to compile (usually missing TMP).
/// </summary>
public static class ArthurianMenuBootstrap
{
    [MenuItem("Arthurian/Diagnose Project", false, 1)]
    public static void DiagnoseProject()
    {
        bool hasTmp = Type.GetType("TMPro.TextMeshProUGUI, Unity.TextMeshPro") != null
                      || Type.GetType("TMPro.TextMeshProUGUI, Unity.ugui") != null;

        // Fallback: any loaded assembly that contains TextMeshProUGUI
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
                catch
                {
                    // ignore dynamic/unloadable assemblies
                }
            }
        }

        bool hasGameplay = Type.GetType("ArthurianAutobattler.GameLoopManager, Assembly-CSharp") != null;
        if (!hasGameplay)
        {
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                try
                {
                    if (asm.GetType("ArthurianAutobattler.GameLoopManager", false) != null)
                    {
                        hasGameplay = true;
                        break;
                    }
                }
                catch
                {
                    // ignore
                }
            }
        }

        bool hasBuilder = Type.GetType("ArthurianSceneBuilder, Assembly-CSharp-Editor") != null;
        if (!hasBuilder)
        {
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                try
                {
                    if (asm.GetType("ArthurianSceneBuilder", false) != null)
                    {
                        hasBuilder = true;
                        break;
                    }
                }
                catch
                {
                    // ignore
                }
            }
        }

        string msg =
            "DemonKnight / Arthurian diagnose\n\n" +
            $"TextMeshPro (TMPro) loaded: {(hasTmp ? "YES" : "NO")}\n" +
            $"Gameplay scripts compiled: {(hasGameplay ? "YES" : "NO")}\n" +
            $"Scene builder compiled: {(hasBuilder ? "YES" : "NO")}\n\n";

        if (!hasTmp)
        {
            msg +=
                "FIX: In Unity menu go to Window → TextMeshPro → Import TMP Essential Resources.\n" +
                "Also confirm Packages/manifest.json includes com.unity.ugui.\n" +
                "Then wait for compile and check Console for red errors.";
        }
        else if (!hasGameplay || !hasBuilder)
        {
            msg +=
                "FIX: Open Window → General → Console and clear red compile errors.\n" +
                "After scripts compile, Arthurian → Build Complete Scene Hierarchy will appear.";
        }
        else
        {
            msg += "All good — use Arthurian → Build Complete Scene Hierarchy.";
        }

        Debug.Log(msg);
        EditorUtility.DisplayDialog("Arthurian Diagnose", msg, "OK");
    }

    [MenuItem("Arthurian/Open Setup Guide", false, 2)]
    public static void OpenSetupGuide()
    {
        var path = "Assets/../README.md";
        var full = System.IO.Path.GetFullPath(path);
        EditorUtility.RevealInFinder(Application.dataPath);
        EditorUtility.DisplayDialog(
            "DemonKnight Setup",
            "1) Confirm this project folder contains Assets/Scripts and Assets/Editor.\n" +
            "2) Wait for compile (bottom-right spinner).\n" +
            "3) Run Arthurian → Diagnose Project.\n" +
            "4) Then Arthurian → Build Complete Scene Hierarchy.\n\n" +
            "Project Assets path:\n" + Application.dataPath,
            "OK");
        Debug.Log("[DemonKnight] Assets path: " + Application.dataPath + " | README: " + full);
    }
}
#endif
