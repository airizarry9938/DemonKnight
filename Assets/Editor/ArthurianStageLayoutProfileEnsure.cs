#if UNITY_EDITOR
using System;
using System.IO;
using ArthurianAutobattler;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Ensures Assets/Resources/StageLayoutProfile.asset exists with 52/52/20 slots.
/// After DA-59 embed into GameDataTypes.cs, old assets keep a stale MonoScript GUID →
/// "referenced script is missing". We delete + recreate when Load fails.
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
        EditorApplication.delayCall += () =>
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            try { EnsureAsset(); }
            catch (System.Exception ex)
            {
                Debug.LogWarning("[DemonKnight] StageLayoutProfile ensure skipped: " + ex.Message);
            }
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

        // File exists but script GUID is stale (missing Behaviour) — wipe and recreate.
        // Old standalone StageLayoutProfile.cs.meta was c4a8f2e19b6d4078a5e31c0d9f7b2a64.
        string full = Path.Combine(Directory.GetParent(Application.dataPath)!.FullName,
            AssetPath.Replace('/', Path.DirectorySeparatorChar));
        bool staleOnDisk = File.Exists(full) || AssetDatabase.LoadMainAssetAtPath(AssetPath) != null;
        if (File.Exists(full))
        {
            try
            {
                if (File.ReadAllText(full).IndexOf("c4a8f2e19b6d4078a5e31c0d9f7b2a64", StringComparison.OrdinalIgnoreCase) >= 0)
                    staleOnDisk = true;
            }
            catch { staleOnDisk = true; }
        }
        if (staleOnDisk)
        {
            Debug.LogWarning("[DemonKnight] Recreating StageLayoutProfile.asset (stale MonoScript GUID after embed).");
            AssetDatabase.DeleteAsset(AssetPath);
            if (File.Exists(full))
            {
                try { File.Delete(full); } catch { /* ignore */ }
                string meta = full + ".meta";
                if (File.Exists(meta)) try { File.Delete(meta); } catch { /* ignore */ }
            }
            AssetDatabase.Refresh();
        }

        var created = ScriptableObject.CreateInstance<StageLayoutProfile>();
        created.EnsureSlotCounts();
        AssetDatabase.CreateAsset(created, AssetPath);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        return AssetDatabase.LoadAssetAtPath<StageLayoutProfile>(AssetPath) ?? created;
    }
}
#endif
