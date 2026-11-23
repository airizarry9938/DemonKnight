using System;
using System.Collections.Generic;
using UnityEngine;

namespace ArthurianAutobattler
{
    // tip: fix-named-profile-sync-cs0246 — named MonoScript for Unity SO binding
    [Serializable]
    public class SlotTransformConfig
    {
        public int slotNumber;
        public Vector2 position;
        public Vector2 scale = Vector2.one;
        public Vector2 aspectMultiplier = Vector2.one;
        public int sortingOrder;
        public int bonusHP;
        public int bonusATK;
        public int bonusSPD;
        /// <summary>Editor/runtime preview sprite override for this numbered handle.</summary>
        public Sprite previewOrOverrideSprite;
        /// <summary>Path backup so seat sprites rematerialize in player builds (Resources/StreamingAssets).</summary>
        public string previewAssetPath;
        /// <summary>Optional CharacterDefinition.characterId pinned to this slot.</summary>
        public string assignedCharacterId;
    }

    [Serializable]
    public class StageParallaxLayer
    {
        public string layerName = "Layer";
        public Sprite layerSprite;
        public int sortingOrder;
        public Vector2 scrollSpeed;
        public Vector2 swayAmplitude;
        public float swayFrequency = 1f;
        public Color layerTint = Color.white;
    }

    /// <summary>
    /// Numbered Somerset church pews (52), player battle slots (52), enemy slots (20),
    /// plus parallax / atmosphere toggles. Resources path: StageLayoutProfile.
    /// </summary>
    [CreateAssetMenu(fileName = "StageLayoutProfile", menuName = "DemonKnight/Stage Layout Profile")]
    public class StageLayoutProfile : ScriptableObject
    {
        public const string ResourcesPath = "StageLayoutProfile";
        public const int ChurchPewCount = 52;
        public const int PlayerBattleCount = 52;
        public const int EnemyBattleCount = 20;
        /// <summary>Gossip / pew row size (pewSeatIndex / 13).</summary>
        public const int PewRowSize = 13;

        public List<SlotTransformConfig> churchPewSlots = new List<SlotTransformConfig>();
        public List<SlotTransformConfig> playerBattleSlots = new List<SlotTransformConfig>();
        public List<SlotTransformConfig> enemyBattleSlots = new List<SlotTransformConfig>();
        public List<StageParallaxLayer> parallaxLayers = new List<StageParallaxLayer>();

        public bool enableRainFX;
        public bool enableLightningFlashes;
        public bool enableCandleFlicker = true;

        /// <summary>True when this is a CreateInstance stand-in (not a Resources asset).</summary>
        [System.NonSerialized] public bool isRuntimeStandIn;

        public static StageLayoutProfile Load()
        {
            StageLayoutProfile p = null;
            try { p = Resources.Load<StageLayoutProfile>(ResourcesPath); }
            catch { /* missing MonoScript GUID after embed — fall through */ }
            if (p != null)
            {
                p.EnsureSlotCounts();
                p.isRuntimeStandIn = false;
                p.RematerializeSpritesFromPaths();
                return p;
            }
            try
            {
                var all = Resources.LoadAll<StageLayoutProfile>("");
                if (all != null)
                {
                    for (int i = 0; i < all.Length; i++)
                    {
                        if (all[i] == null) continue;
                        all[i].EnsureSlotCounts();
                        all[i].isRuntimeStandIn = false;
                        all[i].RematerializeSpritesFromPaths();
                        return all[i];
                    }
                }
            }
            catch { /* broken Resources asset */ }
            // Runtime fallback when the Resources asset has a stale script GUID (Force Sync embed).
            // Never wipe authored Resources — stand-in is last resort only.
            p = CreateInstance<StageLayoutProfile>();
            p.name = "StageLayoutProfile_Runtime";
            p.isRuntimeStandIn = true;
            p.EnsureSlotCounts();
            return p;
        }

        /// <summary>
        /// Restore Missing pew/battle seat sprites from path backups.
        /// Works in player builds via Resources.Load / StreamingAssets (not Editor-only).
        /// Never clears path backups when sprites are temporarily null.
        /// </summary>
        public int RematerializeSpritesFromPaths()
        {
            EnsureSlotCounts();
            int n = 0;
            n += RematerializeSlotList(churchPewSlots);
            n += RematerializeSlotList(playerBattleSlots);
            n += RematerializeSlotList(enemyBattleSlots);
            return n;
        }

        static int RematerializeSlotList(List<SlotTransformConfig> slots)
        {
            if (slots == null) return 0;
            int restored = 0;
            for (int i = 0; i < slots.Count; i++)
            {
                var s = slots[i];
                if (s == null || s.previewOrOverrideSprite != null) continue;
                if (string.IsNullOrEmpty(s.previewAssetPath)) continue;
                Sprite sp = ClassSpritePoolProfile.TryLoadSpriteAtPath(s.previewAssetPath);
                if (sp == null) continue;
                s.previewOrOverrideSprite = sp;
                restored++;
            }
            return restored;
        }

        /// <summary>Pad / trim lists to 52 / 52 / 20 with default transforms.</summary>
        public void EnsureSlotCounts()
        {
            churchPewSlots ??= new List<SlotTransformConfig>();
            playerBattleSlots ??= new List<SlotTransformConfig>();
            enemyBattleSlots ??= new List<SlotTransformConfig>();
            parallaxLayers ??= new List<StageParallaxLayer>();

            FillSlots(churchPewSlots, ChurchPewCount, ChurchPewDefault);
            FillSlots(playerBattleSlots, PlayerBattleCount, PlayerBattleDefault);
            FillSlots(enemyBattleSlots, EnemyBattleCount, EnemyBattleDefault);
        }

        static void FillSlots(List<SlotTransformConfig> list, int count, Func<int, SlotTransformConfig> factory)
        {
            while (list.Count < count)
                list.Add(factory(list.Count));
            if (list.Count > count)
                list.RemoveRange(count, list.Count - count);
            for (int i = 0; i < list.Count; i++)
            {
                if (list[i] == null) list[i] = factory(i);
                list[i].slotNumber = i;
            }
        }

        /// <summary>
        /// Play-normalized 0–1 church seat (matches SomersetPewLayout defaults).
        /// World-space defaults (-6..6) were ignored by PopulatePews, so Stage Placer
        /// edits never showed in Play until Architect re-seeded — fixed here.
        /// </summary>
        static SlotTransformConfig ChurchPewDefault(int i)
        {
            Vector2 norm = DefaultChurchNormalizedPosition(i);
            return new SlotTransformConfig
            {
                slotNumber = i,
                position = norm,
                scale = Vector2.one,
                aspectMultiplier = Vector2.one,
                sortingOrder = -Mathf.RoundToInt(norm.y * 100f)
            };
        }

        /// <summary>Normalized 0–1 pew seat matching SomersetPewLayout.CreateDefaultPews capacities.</summary>
        public static Vector2 DefaultChurchNormalizedPosition(int globalSeatIndex)
        {
            // Mirror SomersetPewLayout.CreateDefaultPews (v4) — keep in sync.
            float[][] colXByRow =
            {
                new[] { 0.220f, 0.500f, 0.780f },
                new[] { 0.226f, 0.500f, 0.775f },
                new[] { 0.232f, 0.500f, 0.770f },
                new[] { 0.238f, 0.500f, 0.765f }
            };
            float[] rowY = { 0.400f, 0.545f, 0.690f, 0.820f };
            float[] rowSpread = { 0.175f, 0.160f, 0.145f, 0.120f };
            int[,] caps =
            {
                { 5, 5, 5 },
                { 4, 5, 4 },
                { 4, 4, 4 },
                { 4, 4, 4 }
            };

            int remaining = Mathf.Clamp(globalSeatIndex, 0, ChurchPewCount - 1);
            for (int row = 0; row < 4; row++)
            {
                for (int col = 0; col < 3; col++)
                {
                    int cap = Mathf.Max(1, caps[row, col]);
                    if (remaining < cap)
                    {
                        float t = cap == 1 ? 0.5f : remaining / (float)(cap - 1);
                        float x = colXByRow[row][col]
                                  + Mathf.Lerp(-rowSpread[row] * 0.5f, rowSpread[row] * 0.5f, t);
                        return new Vector2(x, rowY[row]);
                    }
                    remaining -= cap;
                }
            }
            return new Vector2(0.5f, 0.5f);
        }

        /// <summary>Staggered columns of 6 (frontlineSize = 6) — matches Phase 3 auto-arrange math.</summary>
        static SlotTransformConfig PlayerBattleDefault(int i)
        {
            const int frontlineSize = 6;
            int col = i / frontlineSize;
            int row = i % frontlineSize;
            float scale = col <= 1 ? 1f : Mathf.Lerp(0.95f, 0.70f, Mathf.InverseLerp(2f, 8f, col));
            float x = -3.5f + row * 1.1f;
            float y = 2.0f - col * 0.85f;
            return new SlotTransformConfig
            {
                slotNumber = i,
                position = new Vector2(x, y),
                scale = new Vector2(scale, scale),
                aspectMultiplier = Vector2.one,
                sortingOrder = Mathf.RoundToInt(-y * 10f)
            };
        }

        static SlotTransformConfig EnemyBattleDefault(int i)
        {
            int col = i / 5;
            int row = i % 5;
            float x = 3.5f + row * 1.0f;
            float y = 1.5f - col * 1.0f;
            return new SlotTransformConfig
            {
                slotNumber = i,
                position = new Vector2(x, y),
                scale = Vector2.one,
                aspectMultiplier = Vector2.one,
                sortingOrder = Mathf.RoundToInt(-y * 10f)
            };
        }
    }
}
