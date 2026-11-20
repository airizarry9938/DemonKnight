#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using ArthurianAutobattler;

/// <summary>
/// Clears "The referenced script (Unknown) on this Behaviour is missing!" after
/// zip/overwrite GUID churn, and remaps common church components when possible.
/// </summary>
public static class ArthurianFixMissingScripts
{
    [MenuItem("Arthurian/Fix Missing Scripts + Remap Church Stage", false, 11)]
    public static void Run()
    {
        // 1) Kill gameplay scripts wrongly sitting under Assets/Editor (GUID chaos).
        ArthurianFixMisplacedScripts.Run();

        int removed = 0;
        int remapped = 0;

        // 2) Prefer open scenes; also scan all scene assets under Assets/Scenes.
        var scenes = new List<Scene>();
        for (int i = 0; i < SceneManager.sceneCount; i++)
            scenes.Add(SceneManager.GetSceneAt(i));

        if (scenes.Count == 0 || !scenes[0].IsValid())
        {
            string[] guids = AssetDatabase.FindAssets("t:Scene", new[] { "Assets/Scenes", "Assets" });
            foreach (string g in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(g);
                if (string.IsNullOrEmpty(path) || !path.EndsWith(".unity", StringComparison.OrdinalIgnoreCase))
                    continue;
                var opened = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
                scenes.Add(opened);
            }
        }

        var churchType = typeof(ChurchStageUIController);
        var pewType = typeof(ChurchPewLayout);
        var somersetPewType = typeof(SomersetPewLayout);
        var townType = typeof(TownSplitUIController);
        var loopType = typeof(GameLoopManager);
        var merlinType = typeof(MerlinResurrectionManager);
        var codexType = typeof(LegendCodexUI);

        foreach (var scene in scenes)
        {
            if (!scene.IsValid() || !scene.isLoaded) continue;
            foreach (var root in scene.GetRootGameObjects())
            {
                foreach (var t in root.GetComponentsInChildren<Transform>(true))
                {
                    removed += GameObjectUtility.RemoveMonoBehavioursWithMissingScript(t.gameObject);

                    // Remap by GameObject name when the component is simply gone.
                    string n = t.name;
                    if (n.IndexOf("ChurchStage", StringComparison.OrdinalIgnoreCase) >= 0
                        || n.IndexOf("TownCanvas", StringComparison.OrdinalIgnoreCase) >= 0
                        || n.Equals("ChurchUI", StringComparison.OrdinalIgnoreCase)
                        || n.Equals("SomersetChurch", StringComparison.OrdinalIgnoreCase))
                    {
                        if (t.GetComponent<ChurchStageUIController>() == null
                            && t.GetComponent<SomersetChurchUI>() == null)
                        {
                            // TownCanvas often hosts church UI in our builder.
                            if (n.IndexOf("TownCanvas", StringComparison.OrdinalIgnoreCase) >= 0
                                || n.IndexOf("Church", StringComparison.OrdinalIgnoreCase) >= 0)
                            {
                                t.gameObject.AddComponent(churchType);
                                remapped++;
                                Debug.Log($"[DemonKnight] Remapped ChurchStageUIController onto {GetPath(t)}");
                            }
                        }
                    }

                    if (n.IndexOf("PewLayout", StringComparison.OrdinalIgnoreCase) >= 0
                        || n.Equals("SomersetPewLayout", StringComparison.OrdinalIgnoreCase)
                        || n.Equals("ChurchPewLayout", StringComparison.OrdinalIgnoreCase))
                    {
                        if (t.GetComponent<SomersetPewLayout>() == null)
                        {
                            t.gameObject.AddComponent(pewType);
                            remapped++;
                            Debug.Log($"[DemonKnight] Remapped ChurchPewLayout onto {GetPath(t)}");
                        }
                    }

                    if (n.Equals("TownSplitUI", StringComparison.OrdinalIgnoreCase)
                        || n.Equals("TownUI", StringComparison.OrdinalIgnoreCase))
                    {
                        if (t.GetComponent<TownSplitUIController>() == null)
                        {
                            t.gameObject.AddComponent(townType);
                            remapped++;
                        }
                    }

                    if (n.Equals("GameLoopManager", StringComparison.OrdinalIgnoreCase))
                    {
                        if (t.GetComponent<GameLoopManager>() == null)
                        {
                            t.gameObject.AddComponent(loopType);
                            remapped++;
                        }
                    }

                    if (n.Equals("MerlinResurrectionManager", StringComparison.OrdinalIgnoreCase)
                        || n.IndexOf("Merlin", StringComparison.OrdinalIgnoreCase) >= 0
                           && n.IndexOf("Manager", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        if (t.GetComponent<MerlinResurrectionManager>() == null
                            && n.IndexOf("MerlinResurrection", StringComparison.OrdinalIgnoreCase) >= 0)
                        {
                            t.gameObject.AddComponent(merlinType);
                            remapped++;
                        }
                    }

                    if (n.Equals("LegendCodexUI", StringComparison.OrdinalIgnoreCase)
                        || n.IndexOf("Codex", StringComparison.OrdinalIgnoreCase) >= 0
                           && t.GetComponent<Canvas>() != null)
                    {
                        // Don't auto-add LegendCodexUI to every canvas — only exact name.
                        if (n.Equals("LegendCodexUI", StringComparison.OrdinalIgnoreCase)
                            && t.GetComponent<LegendCodexUI>() == null)
                        {
                            t.gameObject.AddComponent(codexType);
                            remapped++;
                        }
                    }
                }

                // TownCanvas from scene builder: ensure church stage component exists.
                if (root.name == "TownCanvas" || root.GetComponent<Canvas>() != null)
                {
                    var canvas = root.name == "TownCanvas" ? root : null;
                    if (canvas == null)
                    {
                        foreach (Transform c in root.GetComponentsInChildren<Transform>(true))
                        {
                            if (c.name == "TownCanvas") { canvas = c.gameObject; break; }
                        }
                    }
                    if (canvas != null
                        && canvas.GetComponent<ChurchStageUIController>() == null
                        && canvas.GetComponent<SomersetChurchUI>() == null)
                    {
                        // Builder put ChurchStageUIController on TownCanvas.
                        canvas.AddComponent(churchType);
                        remapped++;
                        Debug.Log($"[DemonKnight] Added ChurchStageUIController on {canvas.name}");
                    }
                    if (canvas != null && canvas.GetComponent<SomersetPewLayout>() == null
                        && canvas.GetComponent<ChurchPewLayout>() == null)
                    {
                        // Pew layout may live on TownCanvas too.
                        if (canvas.transform.Find("ChurchBackground") != null
                            || canvas.GetComponentInChildren<SomersetPewLayout>(true) == null)
                        {
                            // Only add if a ChurchBackground exists and no pew layout anywhere under canvas.
                            if (canvas.GetComponentInChildren<SomersetPewLayout>(true) == null
                                && canvas.transform.Find("ChurchBackground") != null)
                            {
                                canvas.AddComponent(pewType);
                                remapped++;
                            }
                        }
                    }
                }
            }

            if (scene.isDirty)
                EditorSceneManager.MarkSceneDirty(scene);
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        EditorUtility.DisplayDialog(
            "DemonKnight Missing Scripts",
            $"Removed {removed} missing script slot(s).\n" +
            $"Remapped/added {remapped} church-related component(s).\n\n" +
            "Also ran Fix Misplaced Scripts (Editor folder cleanup).\n\n" +
            "If a component is still Missing: select the GameObject → " +
            "Add Component → Church Stage UI Controller (or Somerset Pew Layout).\n" +
            "Save the scene. Diagnose should show the current package id.",
            "OK");

        Debug.Log($"[DemonKnight] Fix Missing Scripts done — removed={removed} remapped={remapped}");
    }

    private static string GetPath(Transform t)
    {
        string p = t.name;
        while (t.parent != null)
        {
            t = t.parent;
            p = t.name + "/" + p;
        }
        return p;
    }
}
#endif
