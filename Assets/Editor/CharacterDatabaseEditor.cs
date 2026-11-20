#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using ArthurianAutobattler;

[CustomEditor(typeof(CharacterDatabase))]
public class CharacterDatabaseEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();
        EditorGUILayout.Space(12);
        if (GUILayout.Button("Auto-Collect All Character Definitions", GUILayout.Height(36)))
        {
            AutoCollect((CharacterDatabase)target);
        }
        EditorGUILayout.HelpBox(
            "Scans Assets/Data/Characters/ (and Resources) for CharacterDefinition assets and registers them by category.",
            MessageType.Info);
    }

    public static void AutoCollect(CharacterDatabase db)
    {
        if (db == null) return;
        string[] guids = AssetDatabase.FindAssets("t:CharacterDefinition", new[]
        {
            "Assets/Data/Characters",
            "Assets/Resources/Data/Characters",
            "Assets/Resources"
        });

        var found = new List<CharacterDefinition>();
        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            var def = AssetDatabase.LoadAssetAtPath<CharacterDefinition>(path);
            if (def != null) found.Add(def);
        }

        Undo.RecordObject(db, "Auto-Collect Character Definitions");
        db.legends = new List<CharacterDefinition>();
        db.bosses = new List<CharacterDefinition>();
        db.demons = new List<CharacterDefinition>();
        foreach (var def in found)
            db.Register(def);

        EditorUtility.SetDirty(db);
        AssetDatabase.SaveAssets();
        CharacterDatabase.InvalidateCache();
        Debug.Log($"[DemonKnight] CharacterDatabase collected {found.Count} definitions " +
                  $"(L={db.legends.Count} B={db.bosses.Count} D={db.demons.Count}).");
        EditorUtility.DisplayDialog(
            "Character Database",
            $"Collected {found.Count} CharacterDefinition assets.\n" +
            $"Legends {db.legends.Count} · Bosses {db.bosses.Count} · Demons {db.demons.Count}",
            "OK");
    }

    [MenuItem("Arthurian/Art Tools/Auto-Collect Character Database", false, 44)]
    [MenuItem("DemonKnight/Characters/Auto-Collect Character Database", false, 44)]
    public static void MenuAutoCollect()
    {
        var db = EnsureDatabaseAsset();
        AutoCollect(db);
        Selection.activeObject = db;
    }

    public static CharacterDatabase EnsureDatabaseAsset()
    {
        const string resourcesDir = "Assets/Resources/Data";
        const string path = "Assets/Resources/Data/CharacterDatabase.asset";
        EnsureFolder(resourcesDir);
        var db = AssetDatabase.LoadAssetAtPath<CharacterDatabase>(path);
        if (db != null) return db;
        db = ScriptableObject.CreateInstance<CharacterDatabase>();
        AssetDatabase.CreateAsset(db, path);
        AssetDatabase.SaveAssets();
        return db;
    }

    static void EnsureFolder(string path)
    {
        path = path.Replace('\\', '/');
        if (AssetDatabase.IsValidFolder(path)) return;
        string[] parts = path.Split('/');
        string cur = parts[0];
        for (int i = 1; i < parts.Length; i++)
        {
            string next = cur + "/" + parts[i];
            if (!AssetDatabase.IsValidFolder(next))
                AssetDatabase.CreateFolder(cur, parts[i]);
            cur = next;
        }
    }
}
#endif
