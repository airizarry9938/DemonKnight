using System;
using System.Collections.Generic;
using UnityEngine;

namespace ArthurianAutobattler
{
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

        public static StageLayoutProfile Load()
        {
            var p = Resources.Load<StageLayoutProfile>(ResourcesPath);
            if (p != null)
            {
                p.EnsureSlotCounts();
                return p;
            }
            var all = Resources.LoadAll<StageLayoutProfile>("");
            if (all != null && all.Length > 0)
            {
                all[0].EnsureSlotCounts();
                return all[0];
            }
            // Runtime fallback when the Resources asset is not yet imported.
            p = CreateInstance<StageLayoutProfile>();
            p.name = "StageLayoutProfile_Runtime";
            p.EnsureSlotCounts();
            return p;
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

        static SlotTransformConfig ChurchPewDefault(int i)
        {
            int row = i / PewRowSize;
            int col = i % PewRowSize;
            return new SlotTransformConfig
            {
                slotNumber = i,
                position = new Vector2(-6f + col * 1.0f, 2.5f - row * 1.2f),
                scale = Vector2.one,
                aspectMultiplier = Vector2.one,
                sortingOrder = -Mathf.RoundToInt((2.5f - row * 1.2f) * 10f)
            };
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
