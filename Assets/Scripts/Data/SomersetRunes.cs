using System;
using System.Reflection;
using UnityEngine;

namespace ArthurianAutobattler
{
    /// <summary>
    /// Soft bridge to Merlin runes / Sin runes so GameDataTypes & UI compile even when
    /// the Unity project still has a first-build MerlinResurrectionManager or is missing
    /// SinRuneManager.cs. Returns safe defaults until those scripts are updated.
    /// </summary>
    public static class SomersetRunes
    {
        public static int GetRank(BankRuneID id)
        {
            var merlin = MerlinResurrectionManager.Instance;
            if (merlin == null) return 0;

            // Preferred API (current package).
            MethodInfo method = merlin.GetType().GetMethod("GetRuneRank", new[] { typeof(BankRuneID) });
            if (method != null)
            {
                try { return (int)method.Invoke(merlin, new object[] { id }); }
                catch { return 0; }
            }

            // First-build Merlin: read public rank fields if present.
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
            Type t = FindType("ArthurianAutobattler.SinRuneManager");
            if (t == null) return false;

            PropertyInfo instanceProp = t.GetProperty("Instance", BindingFlags.Public | BindingFlags.Static);
            object instance = instanceProp?.GetValue(null);
            if (instance == null)
            {
                FieldInfo instanceField = t.GetField("Instance", BindingFlags.Public | BindingFlags.Static);
                instance = instanceField?.GetValue(null);
            }
            if (instance == null) return false;

            MethodInfo isActive = t.GetMethod("IsActive", new[] { typeof(SinRuneID) });
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
            Type t = FindType("ArthurianAutobattler.SinRuneManager");
            if (t == null) return 1f;
            PropertyInfo instanceProp = t.GetProperty("Instance", BindingFlags.Public | BindingFlags.Static);
            object instance = instanceProp?.GetValue(null)
                ?? t.GetField("Instance", BindingFlags.Public | BindingFlags.Static)?.GetValue(null);
            if (instance == null) return 1f;
            MethodInfo method = t.GetMethod("GetLegacyPointMultiplier", Type.EmptyTypes);
            if (method == null) return 1f;
            try { return (float)method.Invoke(instance, null); }
            catch { return 1f; }
        }

        public static void NotifyLegendFused()
        {
            Type t = FindType("ArthurianAutobattler.SinRuneManager");
            if (t == null) return;
            object instance = t.GetProperty("Instance", BindingFlags.Public | BindingFlags.Static)?.GetValue(null)
                ?? t.GetField("Instance", BindingFlags.Public | BindingFlags.Static)?.GetValue(null);
            if (instance == null) return;
            t.GetMethod("OnLegendFused", Type.EmptyTypes)?.Invoke(instance, null);
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
