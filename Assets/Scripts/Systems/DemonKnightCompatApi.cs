using System;
using System.Reflection;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ArthurianAutobattler
{
    /// <summary>
    /// Downloads-safe bridge: GameLoop calls these instead of typing against
    /// ChurchStageUIController / SomersetChurchUI / BattlefieldVisualizer members
    /// that older unzipped copies may lack (CS1061 / CS0122).
    /// Prefers real public methods via reflection; falls back to HUD name lookups.
    /// </summary>
    public static class DemonKnightCompatApi
    {
        const BindingFlags InstancePublic =
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

        public static void UpdateWarbandHudLabel(Component church = null)
        {
            church ??= FindChurch();
            if (TryInvoke(church, "UpdateWarbandHudLabel")) return;
            ApplyWarbandHudLabelFallback();
        }

        public static void RefreshWarbandPanel(Component church = null)
        {
            church ??= FindChurch();
            TryInvoke(church, "RefreshWarbandPanel");
        }

        public static void FlashMarchBlocked(Component church, string reason)
        {
            if (church == null) church = FindChurch();
            if (TryInvoke(church, "FlashMarchBlocked", reason)) return;
            ApplyFlashMarchBlockedFallback(church, reason);
        }

        public static void PrepareBattlefield()
        {
            var viz = BattlefieldVisualizer.Instance
                      ?? UnityEngine.Object.FindFirstObjectByType<BattlefieldVisualizer>();
            if (viz == null)
            {
                Debug.LogWarning("[DemonKnight] BattlefieldVisualizer missing — March continues without arena bind.");
                return;
            }
            if (!TryInvoke(viz, "ResolveArenaBindings"))
                Debug.LogWarning("[DemonKnight] ResolveArenaBindings missing on BattlefieldVisualizer — overwrite that .cs from the zip.");
            if (!TryInvoke(viz, "ApplyBackgroundArt"))
                Debug.LogWarning("[DemonKnight] ApplyBackgroundArt missing on BattlefieldVisualizer — overwrite that .cs from the zip.");
        }

        static Component FindChurch()
        {
            return (Component)UnityEngine.Object.FindFirstObjectByType<SomersetChurchUI>()
                   ?? UnityEngine.Object.FindFirstObjectByType<ChurchStageUIController>();
        }

        static bool TryInvoke(object target, string methodName, params object[] args)
        {
            if (target == null) return false;
            try
            {
                Type t = target.GetType();
                MethodInfo method = null;
                while (t != null && method == null)
                {
                    method = args == null || args.Length == 0
                        ? t.GetMethod(methodName, InstancePublic, null, Type.EmptyTypes, null)
                        : t.GetMethod(methodName, InstancePublic, null, ArgTypes(args), null);
                    t = t.BaseType;
                }
                if (method == null) return false;
                method.Invoke(target, args == null || args.Length == 0 ? null : args);
                return true;
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[DemonKnight] Compat invoke {methodName} failed: {ex.Message}");
                return false;
            }
        }

        static Type[] ArgTypes(object[] args)
        {
            var types = new Type[args.Length];
            for (int i = 0; i < args.Length; i++)
                types[i] = args[i] != null ? args[i].GetType() : typeof(object);
            return types;
        }

        static void ApplyWarbandHudLabelFallback()
        {
            int count = GameLoopManager.Instance != null
                ? GameLoopManager.Instance.recruitedParty.Count
                : 0;
            string label = $"Warband ({count})";
            foreach (var tmp in UnityEngine.Object.FindObjectsByType<TextMeshProUGUI>(FindObjectsSortMode.None))
            {
                if (tmp == null) continue;
                var btn = tmp.GetComponentInParent<Button>();
                if (btn == null) continue;
                string n = btn.gameObject.name;
                if (n.IndexOf("Warband", StringComparison.OrdinalIgnoreCase) >= 0
                    || n.IndexOf("Party", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    tmp.text = label;
                    return;
                }
            }
        }

        static void ApplyFlashMarchBlockedFallback(Component church, string reason)
        {
            Debug.LogWarning($"[DemonKnight] {reason}");
            if (church != null)
            {
                foreach (var tmp in church.GetComponentsInChildren<TextMeshProUGUI>(true))
                {
                    if (tmp == null) continue;
                    string n = tmp.gameObject.name;
                    if (n.IndexOf("Dialogue", StringComparison.OrdinalIgnoreCase) >= 0
                        || n.IndexOf("Dossier", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        tmp.text = $"<color=#FF6666>{reason}</color>";
                        return;
                    }
                }
            }
        }
    }
}
