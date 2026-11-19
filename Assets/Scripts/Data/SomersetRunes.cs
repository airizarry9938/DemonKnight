using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace ArthurianAutobattler
{
    /// <summary>
    /// Soft bridge to optional managers (Merlin runes, Sin, Pacts, Relics) so the project
    /// compiles even when those scripts were never copied into the Unity folder.
    /// Missing managers → safe defaults (no rune, no sin, no pact, no relic).
    /// </summary>
    public static class SomersetRunes
    {
        public static int GetRank(BankRuneID id)
        {
            var merlin = MerlinResurrectionManager.Instance;
            if (merlin == null) return 0;

            MethodInfo method = merlin.GetType().GetMethod("GetRuneRank", new[] { typeof(BankRuneID) });
            if (method != null)
            {
                try { return (int)method.Invoke(merlin, new object[] { id }); }
                catch { return 0; }
            }

            string fieldName = id switch
            {
                BankRuneID.Gossip => "gossipRuneRank",
                BankRuneID.Gambler => "gamblerRuneRank",
                BankRuneID.BloodOath => "bloodOathRuneRank",
                BankRuneID.PeasantsCrown => "peasantsCrownRuneRank",
                _ => null
            };
            if (fieldName == null) return 0;
            FieldInfo field = merlin.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.Public);
            if (field == null) return 0;
            try { return (int)field.GetValue(merlin); }
            catch { return 0; }
        }

        public static bool IsSinActive(SinRuneID id)
        {
            object instance = GetManagerInstance("ArthurianAutobattler.SinRuneManager");
            if (instance == null) return false;
            MethodInfo isActive = instance.GetType().GetMethod("IsActive", new[] { typeof(SinRuneID) });
            if (isActive == null) return false;
            try { return (bool)isActive.Invoke(instance, new object[] { id }); }
            catch { return false; }
        }

        public static bool IsTownTimerDisabled()
        {
            var loop = GameLoopManager.Instance;
            if (loop == null) return false;
            FieldInfo field = loop.GetType().GetField("disableTownTimer", BindingFlags.Instance | BindingFlags.Public);
            if (field == null) return false;
            try { return (bool)field.GetValue(loop); }
            catch { return false; }
        }

        public static float GetSinLegacyPointMultiplier()
        {
            object instance = GetManagerInstance("ArthurianAutobattler.SinRuneManager");
            if (instance == null) return 1f;
            MethodInfo method = instance.GetType().GetMethod("GetLegacyPointMultiplier", Type.EmptyTypes);
            if (method == null) return 1f;
            try { return (float)method.Invoke(instance, null); }
            catch { return 1f; }
        }

        public static void NotifyLegendFused()
        {
            object instance = GetManagerInstance("ArthurianAutobattler.SinRuneManager");
            instance?.GetType().GetMethod("OnLegendFused", Type.EmptyTypes)?.Invoke(instance, null);
        }

        public static DemonicPactID GetActivePact()
        {
            object instance = GetManagerInstance("ArthurianAutobattler.DemonicPactManager");
            if (instance == null) return DemonicPactID.None;
            object active = instance.GetType().GetProperty("ActivePact")?.GetValue(instance)
                ?? instance.GetType().GetField("ActivePact")?.GetValue(instance);
            if (active is DemonicPactID pact) return pact;
            if (active != null && Enum.TryParse(active.ToString(), out DemonicPactID parsed)) return parsed;
            return DemonicPactID.None;
        }

        public static bool IsPact(DemonicPactID id) => GetActivePact() == id;

        public static void ClearPact()
        {
            object instance = GetManagerInstance("ArthurianAutobattler.DemonicPactManager");
            instance?.GetType().GetMethod("ClearPact", Type.EmptyTypes)?.Invoke(instance, null);
        }

        public static int GetEndlessHourglassExtraDreadLords(float townSecondsSpent)
        {
            object instance = GetManagerInstance("ArthurianAutobattler.DemonicPactManager");
            if (instance == null) return 0;
            MethodInfo method = instance.GetType().GetMethod("GetEndlessHourglassExtraDreadLords", new[] { typeof(float) });
            if (method == null) return 0;
            try { return (int)method.Invoke(instance, new object[] { townSecondsSpent }); }
            catch { return 0; }
        }

        public static bool HasRelic(RelicID id)
        {
            object instance = GetManagerInstance("ArthurianAutobattler.RelicManager");
            if (instance == null) return false;
            MethodInfo method = instance.GetType().GetMethod("Has", new[] { typeof(RelicID) });
            if (method == null) return false;
            try { return (bool)method.Invoke(instance, new object[] { id }); }
            catch { return false; }
        }

        public static void ApplyMorganasMirrorClone(List<RecruitInstance> roster)
        {
            object instance = GetManagerInstance("ArthurianAutobattler.RelicManager");
            if (instance == null || roster == null) return;
            instance.GetType().GetMethod("ApplyMorganasMirrorClone", new[] { typeof(List<RecruitInstance>) })
                ?.Invoke(instance, new object[] { roster });
        }

        public static void TryAwardRelicForWave(int wave)
        {
            object instance = GetManagerInstance("ArthurianAutobattler.RelicManager");
            instance?.GetType().GetMethod("TryAwardRelicForWave", new[] { typeof(int) })
                ?.Invoke(instance, new object[] { wave });
        }

        private static object GetManagerInstance(string fullTypeName)
        {
            Type t = FindType(fullTypeName);
            if (t == null) return null;
            return t.GetProperty("Instance", BindingFlags.Public | BindingFlags.Static)?.GetValue(null)
                ?? t.GetField("Instance", BindingFlags.Public | BindingFlags.Static)?.GetValue(null);
        }

        private static Type FindType(string fullName)
        {
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                try
                {
                    Type t = asm.GetType(fullName, false);
                    if (t != null) return t;
                }
                catch { /* ignore */ }
            }
            return null;
        }
    }
}
