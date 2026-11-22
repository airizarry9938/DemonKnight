using System;
using System.Collections.Generic;
using UnityEngine;

namespace ArthurianAutobattler
{
    /// <summary>
    /// Picks pew/combat body sprites from Architect ClassSpritePoolProfile pools
    /// (Peasant / Warrior / Knight). Soft-fails when the profile or pool is empty —
    /// callers keep StageLayoutProfile assign-only / solid color fallbacks.
    ///
    /// Determinism: index = stable hash(unitID) % poolCount so the same recruit
    /// keeps the same sprite across PopulatePews refreshes (not reshuffled each frame).
    /// </summary>
    public static class ClassSpritePoolApplier
    {
        static ClassSpritePoolProfile _cache;

        public static void InvalidateCache() => _cache = null;

        public static ClassSpritePoolProfile Profile
        {
            get
            {
                if (_cache == null)
                {
                    try { _cache = ClassSpritePoolProfile.Load(); }
                    catch (Exception ex)
                    {
                        Debug.LogWarning("[DemonKnight] ClassSpritePoolProfile.Load soft-fail: " + ex.Message);
                        return null;
                    }
                }
                return _cache;
            }
        }

        /// <summary>
        /// Pick a sprite for this recruit from its class pool.
        /// Returns null when type has no pool, pool is empty, or profile missing.
        /// </summary>
        public static Sprite PickForRecruit(RecruitInstance recruit)
        {
            if (recruit == null) return null;
            return PickForClass(recruit.unitType, recruit.unitID, recruit.characterName, recruit.pewSeatIndex);
        }

        public static Sprite PickForClass(UnitType type, string unitId, string characterName = null, int pewSeatIndex = 0)
        {
            try
            {
                var profile = Profile;
                if (profile == null) return null;
                List<Sprite> pool = profile.GetPool(type);
                if (pool == null || pool.Count == 0) return null;

                // Compact nulls so % stays valid without reshuffling identity mapping too hard.
                int usable = 0;
                for (int i = 0; i < pool.Count; i++)
                    if (pool[i] != null) usable++;
                if (usable == 0) return null;

                int seed = StableHash(
                    !string.IsNullOrEmpty(unitId) ? unitId
                    : (!string.IsNullOrEmpty(characterName) ? characterName + "#" + pewSeatIndex
                    : type + "#" + pewSeatIndex));
                int want = Math.Abs(seed) % usable;
                int seen = 0;
                for (int i = 0; i < pool.Count; i++)
                {
                    if (pool[i] == null) continue;
                    if (seen == want) return pool[i];
                    seen++;
                }
                return null;
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[DemonKnight] ClassSpritePoolApplier.Pick soft-fail: " + ex.Message);
                return null;
            }
        }

        /// <summary>FNV-1a 32-bit — stable across sessions (not string.GetHashCode).</summary>
        public static int StableHash(string s)
        {
            unchecked
            {
                uint h = 2166136261u;
                if (string.IsNullOrEmpty(s)) return (int)h;
                for (int i = 0; i < s.Length; i++)
                {
                    h ^= s[i];
                    h *= 16777619u;
                }
                return (int)h;
            }
        }
    }
}
