using System;
using System.Collections.Generic;
using System.Reflection;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ArthurianAutobattler
{
    /// <summary>
    /// Downloads-safe bridge so callers compile even when Sync only partially
    /// refreshed GameLoopManager / SomersetChurchUI / BattlefieldVisualizer.
    /// Prefer real members via reflection; fall back where possible.
    /// </summary>
    public static class DemonKnightCompatApi
    {
        public const int WaveVictoryGold = 200;
        public const int WaveLossGold = 100;
        public const int SurvivorBountyGold = 10;

        const BindingFlags InstancePublic =
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        const BindingFlags StaticPublic =
            BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;

        public static GameLoopManager ResolveLoop()
        {
            if (GameLoopManager.Instance != null) return GameLoopManager.Instance;
            // Prefer GameLoopManager.Resolve when present (current package).
            try
            {
                var m = typeof(GameLoopManager).GetMethod("Resolve", StaticPublic, null, Type.EmptyTypes, null);
                if (m != null && m.ReturnType == typeof(GameLoopManager))
                {
                    var resolved = m.Invoke(null, null) as GameLoopManager;
                    if (resolved != null) return resolved;
                }
            }
            catch { /* fall through */ }
            return UnityEngine.Object.FindFirstObjectByType<GameLoopManager>();
        }

        public static void AutosaveWarband(GameLoopManager loop = null, int playerGold = -1)
        {
            loop ??= ResolveLoop();
            if (loop == null) return;
            // GameLoopManager only declares AutosaveWarband(int) — never a 0-arg overload.
            // Always invoke with int so reflection matches (default -1 resolves gold inside).
            if (TryInvoke(loop, "AutosaveWarband", playerGold)) return;
            try
            {
                WarbandAutosave.SaveFromLoop(loop, playerGold);
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[DemonKnight] AutosaveWarband fallback failed: " + ex.Message
                    + " — Sync GameLoopManager.cs / WarbandAutosave.cs.");
            }
        }

        public static void EnsureDisbandedAfterWave(
            GameLoopManager loop = null,
            HashSet<string> deadUnitIds = null,
            bool? waveWon = null)
        {
            loop ??= ResolveLoop();
            if (loop == null) return;
            if (TryInvokeOverload(loop, "EnsureDisbandedAfterWave", deadUnitIds, waveWon)) return;
            if (TryInvoke(loop, "EnsureDisbandedAfterWave")) return;
            Debug.LogWarning("[DemonKnight] EnsureDisbandedAfterWave missing — Sync GameLoopManager.cs.");
        }

        /// <summary>Repopulate pews after nuclear disband (private RefreshTownAfterDisband).</summary>
        public static void RefreshTownAfterDisband(GameLoopManager loop = null)
        {
            loop ??= ResolveLoop();
            if (loop == null) return;
            if (TryInvoke(loop, "RefreshTownAfterDisband")) return;
            try
            {
                var church = loop.ResolveChurchUI()
                             ?? UnityEngine.Object.FindFirstObjectByType<SomersetChurchUI>();
                if (church != null && loop.currentTownPool != null)
                    church.PopulatePews(loop.currentTownPool);
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[DemonKnight] RefreshTownAfterDisband fallback failed: " + ex.Message);
            }
        }

        /// <summary>WaveVictory: map surviving combat unitIDs → canonical veteranRank.</summary>
        public static void AwardWaveVictoryVeteranRanks(
            GameLoopManager loop,
            HashSet<string> survivingUnitIds)
        {
            loop ??= ResolveLoop();
            if (loop == null) return;
            if (TryInvoke(loop, "AwardWaveVictoryVeteranRanks", survivingUnitIds)) return;
            Debug.LogWarning("[DemonKnight] AwardWaveVictoryVeteranRanks missing — Sync GameLoopManager.cs.");
        }

        public static void RedrawVeteranStarIcons(GameLoopManager loop = null)
        {
            loop ??= ResolveLoop();
            if (loop == null) return;
            if (TryInvoke(loop, "RedrawVeteranStarIcons")) return;
            RefreshTownAfterDisband(loop);
        }

        public static void SetMustDisband(GameLoopManager loop, bool value)
        {
            loop ??= ResolveLoop();
            if (loop == null) return;
            var f = typeof(GameLoopManager).GetField("mustDisbandPartyAfterCurrentWave", InstancePublic);
            if (f != null) f.SetValue(loop, value);
        }

        public static void ChurchEnsureDiceHudAndHidden(Component church = null)
        {
            church ??= FindChurch();
            TryInvoke(church, "EnsureDiceHudAndHidden");
            TryInvoke(church, "ForceNamedTavernDicePanelsInactive");
        }

        public static void ChurchForceNamedTavernDicePanelsInactive(Component church = null)
        {
            church ??= FindChurch();
            TryInvoke(church, "ForceNamedTavernDicePanelsInactive");
        }

        public static void ChurchRewireMarch(Component church = null)
        {
            church ??= FindChurch();
            if (!TryInvoke(church, "RewireMarchButton"))
                Debug.LogWarning("[DemonKnight] RewireMarchButton missing — Sync SomersetChurchUI.cs.");
        }

        public static void ChurchRewireDossierActions(Component church = null)
        {
            church ??= FindChurch();
            if (!TryInvoke(church, "RewireDossierActionButtons"))
                Debug.LogWarning("[DemonKnight] RewireDossierActionButtons missing — Sync SomersetChurchUI.cs.");
        }

        public static void ChurchInvokeMediate(Component church = null)
        {
            church ??= FindChurch();
            if (!TryInvoke(church, "Mediate"))
                Debug.LogWarning("[DemonKnight] Mediate missing — Sync SomersetChurchUI.cs.");
        }

        public static void EnsureCodexScrollViewport(Transform codexRoot)
        {
            if (codexRoot == null) return;
            if (TryInvokeStatic(typeof(SomersetChurchUI), "EnsureCodexScrollViewport", codexRoot)) return;
            Debug.LogWarning("[DemonKnight] EnsureCodexScrollViewport missing — Sync SomersetChurchUI.cs.");
        }

        public static void ApplyTwoColumnCodexLayout(Transform recipeList)
        {
            if (recipeList == null) return;
            if (TryInvokeStatic(typeof(SomersetChurchUI), "ApplyTwoColumnCodexLayout", recipeList)) return;
            Debug.LogWarning("[DemonKnight] ApplyTwoColumnCodexLayout missing — Sync SomersetChurchUI.cs.");
        }

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
                Debug.LogWarning("[DemonKnight] ResolveArenaBindings missing — Sync BattlefieldVisualizer.cs.");
            if (!TryInvoke(viz, "ApplyBackgroundArt"))
                Debug.LogWarning("[DemonKnight] ApplyBackgroundArt missing — Sync BattlefieldVisualizer.cs.");
            if (!TryInvoke(viz, "ApplyVideoBackground"))
                Debug.LogWarning("[DemonKnight] ApplyVideoBackground missing — Sync BattlefieldVisualizer.cs.");
        }

        /// <summary>
        /// Soft-bind the wave boss portrait next to NightText. Compiles even when
        /// WaveBossHudView.cs has not landed yet (Force Sync ScriptPaths is compiled in-memory).
        /// </summary>
        public static void RefreshWaveBossHud(TextMeshProUGUI nightText, int wave)
        {
            if (nightText == null) return;
            try
            {
                Type t = Type.GetType("ArthurianAutobattler.WaveBossHudView, Assembly-CSharp")
                         ?? Type.GetType("ArthurianAutobattler.WaveBossHudView");
                if (t == null) return;
                MethodInfo ensure = t.GetMethod("EnsureNearNightText", StaticPublic, null,
                    new[] { typeof(TextMeshProUGUI) }, null);
                if (ensure == null) return;
                object hud = ensure.Invoke(null, new object[] { nightText });
                if (hud == null) return;
                MethodInfo refresh = hud.GetType().GetMethod("Refresh", InstancePublic, null,
                    new[] { typeof(int) }, null);
                refresh?.Invoke(hud, new object[] { Mathf.Max(1, wave) });
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[DemonKnight] RefreshWaveBossHud soft-call failed: " + ex.Message
                    + " — Force Sync again after compile so WaveBossHudView.cs lands.");
            }
        }

        static Component FindChurch()
        {
            return (Component)UnityEngine.Object.FindFirstObjectByType<SomersetChurchUI>()
                   ?? UnityEngine.Object.FindFirstObjectByType<ChurchStageUIController>();
        }

        static bool TryInvokeStatic(Type type, string methodName, params object[] args)
        {
            if (type == null) return false;
            try
            {
                MethodInfo method = args == null || args.Length == 0
                    ? type.GetMethod(methodName, StaticPublic, null, Type.EmptyTypes, null)
                    : type.GetMethod(methodName, StaticPublic, null, ArgTypes(args), null);
                if (method == null) return false;
                method.Invoke(null, args == null || args.Length == 0 ? null : args);
                return true;
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[DemonKnight] Compat static invoke {methodName} failed: {ex.Message}");
                return false;
            }
        }

        static bool TryInvokeOverload(object target, string methodName, HashSet<string> deadIds, bool? waveWon)
        {
            if (target == null) return false;
            try
            {
                foreach (var method in target.GetType().GetMethods(InstancePublic))
                {
                    if (method.Name != methodName) continue;
                    var ps = method.GetParameters();
                    if (ps.Length == 2
                        && typeof(HashSet<string>).IsAssignableFrom(ps[0].ParameterType)
                        && (ps[1].ParameterType == typeof(bool?) || ps[1].ParameterType == typeof(Nullable<bool>)))
                    {
                        method.Invoke(target, new object[] { deadIds, waveWon });
                        return true;
                    }
                    if (ps.Length == 0)
                    {
                        method.Invoke(target, null);
                        return true;
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[DemonKnight] Compat overload {methodName} failed: {ex.Message}");
            }
            return false;
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
            if (church == null) return;
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
