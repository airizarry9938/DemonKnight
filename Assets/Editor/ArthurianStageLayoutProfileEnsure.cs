#if UNITY_EDITOR
using System.IO;
using ArthurianAutobattler;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Ensures Assets/Resources/StageLayoutProfile.asset exists with 52/52/20 slots.
/// </summary>
public static class ArthurianStageLayoutProfileEnsure
{
    const string AssetPath = "Assets/Resources/StageLayoutProfile.asset";

    [MenuItem("Arthurian/Ensure Stage Layout Profile", false, 55)]
    public static void EnsureFromMenu()
    {
        var p = EnsureAsset();
        Selection.activeObject = p;
        EditorGUIUtility.PingObject(p);
        Debug.Log($"[DemonKnight] StageLayoutProfile ready → {AssetPath} " +
                  $"(pews={p.churchPewSlots.Count}, player={p.playerBattleSlots.Count}, enemy={p.enemyBattleSlots.Count}).");
    }

    [InitializeOnLoadMethod]
    static void EnsureOnLoad()
    {
        // Defer so AssetDatabase is ready after domain reload.
        EditorApplication.delayCall += () =>
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            EnsureAsset();
        };
    }

    public static StageLayoutProfile EnsureAsset()
    {
        if (!AssetDatabase.IsValidFolder("Assets/Resources"))
            AssetDatabase.CreateFolder("Assets", "Resources");

        var existing = AssetDatabase.LoadAssetAtPath<StageLayoutProfile>(AssetPath);
        if (existing != null)
        {
            existing.EnsureSlotCounts();
            EditorUtility.SetDirty(existing);
            AssetDatabase.SaveAssets();
            return existing;
        }

        var created = ScriptableObject.CreateInstance<StageLayoutProfile>();
        created.EnsureSlotCounts();
        AssetDatabase.CreateAsset(created, AssetPath);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        return created;
    }
}
#endif
