#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using ArthurianAutobattler;

/// <summary>
/// Last Knights of Somerset — Asset & Dialogue Studio.
/// Menu: Somerset / Asset & Dialogue Studio
/// </summary>
public class SomersetAssetStudioWindow : EditorWindow
{
    private SomersetVisualDatabase database;
    private Vector2 scroll;

    [MenuItem("Somerset/Asset & Dialogue Studio")]
    public static void Open()
    {
        var win = GetWindow<SomersetAssetStudioWindow>("Somerset Studio");
        win.minSize = new Vector2(480, 420);
    }

    private void OnGUI()
    {
        EditorGUILayout.LabelField("Last Knights of Somerset — Visual Database", EditorStyles.boldLabel);
        database = (SomersetVisualDatabase)EditorGUILayout.ObjectField(
            "Database", database, typeof(SomersetVisualDatabase), false);

        if (database == null)
        {
            EditorGUILayout.HelpBox(
                "Create via Assets → Create → Somerset → Visual Database, then assign it here.",
                MessageType.Info);
            if (GUILayout.Button("Create New Database Asset"))
            {
                string path = EditorUtility.SaveFilePanelInProject(
                    "Create Somerset Visual Database", "SomersetVisualDatabase", "asset", "");
                if (!string.IsNullOrEmpty(path))
                {
                    database = CreateInstance<SomersetVisualDatabase>();
                    AssetDatabase.CreateAsset(database, path);
                    AssetDatabase.SaveAssets();
                }
            }
            return;
        }

        scroll = EditorGUILayout.BeginScrollView(scroll);
        SerializedObject so = new SerializedObject(database);
        so.Update();
        DrawProperties(so);
        so.ApplyModifiedProperties();
        EditorGUILayout.EndScrollView();

        EditorGUILayout.Space();
        if (GUILayout.Button("Sync Database to Active Scene", GUILayout.Height(36)))
        {
            SyncToActiveScene();
        }
    }

    private void DrawProperties(SerializedObject so)
    {
        EditorGUILayout.PropertyField(so.FindProperty("bodyEager"), true);
        EditorGUILayout.PropertyField(so.FindProperty("bodyUpright"), true);
        EditorGUILayout.PropertyField(so.FindProperty("bodySlouched"), true);
        EditorGUILayout.PropertyField(so.FindProperty("knightHeads"), true);
        EditorGUILayout.PropertyField(so.FindProperty("warriorHeads"), true);
        EditorGUILayout.PropertyField(so.FindProperty("peasantHeads"), true);
        EditorGUILayout.PropertyField(so.FindProperty("moodGrudge"));
        EditorGUILayout.PropertyField(so.FindProperty("moodLoyalist"));
        EditorGUILayout.PropertyField(so.FindProperty("moodCoward"));
        EditorGUILayout.PropertyField(so.FindProperty("legendArt"), true);
        EditorGUILayout.PropertyField(so.FindProperty("bossArt"), true);
        EditorGUILayout.PropertyField(so.FindProperty("pewDialogues"), true);
        EditorGUILayout.PropertyField(so.FindProperty("gossipWhispers"), true);
        EditorGUILayout.PropertyField(so.FindProperty("pewIdleController"));
        EditorGUILayout.PropertyField(so.FindProperty("merlinResurrectionController"));
    }

    private void SyncToActiveScene()
    {
        var gen = Object.FindFirstObjectByType<TownRNGGenerator>();
        if (gen == null)
        {
            EditorUtility.DisplayDialog("Somerset Studio", "No TownRNGGenerator in the active scene.", "OK");
            return;
        }

        Undo.RecordObject(gen, "Sync Somerset Visual Database");
        database.SyncToTownGenerator(gen);
        EditorUtility.SetDirty(gen);

        var fusion = Object.FindFirstObjectByType<CardFusionSystem>();
        if (fusion != null)
        {
            Undo.RecordObject(fusion, "Sync Legend Art");
            foreach (var entry in database.legendArt)
            {
                if (entry.portrait == null) continue;
                switch (entry.unitType)
                {
                    case UnitType.Priest: fusion.priestSprite = entry.portrait; break;
                    case UnitType.Captain: fusion.captainSprite = entry.portrait; break;
                    case UnitType.KingArthur: fusion.kingArthurSprite = entry.portrait; break;
                    case UnitType.Merlin: fusion.merlinSprite = entry.portrait; break;
                    case UnitType.SirLancelot: fusion.lancelotSprite = entry.portrait; break;
                    case UnitType.MorganLeFay: fusion.morganSprite = entry.portrait; break;
                    case UnitType.LadyOfTheLake: fusion.ladyOfLakeSprite = entry.portrait; break;
                    case UnitType.SirGawain: fusion.gawainSprite = entry.portrait; break;
                    case UnitType.Mordred: fusion.mordredSprite = entry.portrait; break;
                    case UnitType.SirGalahad: fusion.galahadSprite = entry.portrait; break;
                }
            }
            EditorUtility.SetDirty(fusion);
        }

        AssetDatabase.SaveAssets();
        EditorUtility.DisplayDialog("Somerset Studio", "Synced visual database to active scene.", "OK");
    }
}
#endif
