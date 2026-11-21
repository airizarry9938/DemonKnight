using System;
using UnityEngine;

namespace ArthurianAutobattler
{
    public enum TownsfolkPose : byte
    {
        ArmsCrossed = 0,
        HandsOnKnees = 1,
        LeaningForward = 2,
        HoldingProp = 3
    }

    /// <summary>
    /// Deterministic modular look for a pew townsfolk. Indices resolve through
    /// <see cref="ModularSpriteLibrary"/> so pew seats and dossier portraits match.
    /// </summary>
    [Serializable]
    public class TownsfolkAppearance
    {
        public const int SkinToneCount = 6;
        public const int HairColorCount = 5;
        public const int PoseCount = 4;
        public const int OutfitSlotCount = 6; // 0–1 Peasant, 2–3 Warrior, 4–5 Knight

        public UnitType tier;
        public int seed;
        public int skinToneIndex;
        public int hairColorIndex;
        public int poseIndex;
        public int outfitIndex;
        public int headIndex;
        public int propIndex;

        public TownsfolkPose Pose => (TownsfolkPose)Mathf.Clamp(poseIndex, 0, PoseCount - 1);
        public bool HoldsProp => Pose == TownsfolkPose.HoldingProp && propIndex > 0;

        /// <summary>Fair, Warm Tan, Olive/Bronze, Rich Brown, Deep Umber, Ashen/Pale.</summary>
        public static readonly Color[] SkinTones =
        {
            new Color(0.96f, 0.82f, 0.70f, 1f), // Fair
            new Color(0.88f, 0.68f, 0.48f, 1f), // Warm Tan
            new Color(0.72f, 0.55f, 0.38f, 1f), // Olive/Bronze
            new Color(0.48f, 0.32f, 0.22f, 1f), // Rich Brown
            new Color(0.30f, 0.20f, 0.14f, 1f), // Deep Umber
            new Color(0.86f, 0.86f, 0.90f, 1f), // Ashen/Pale
        };

        /// <summary>Raven Black, Chestnut Brown, Auburn, Flaxen Gold, Weathered Gray.</summary>
        public static readonly Color[] HairColors =
        {
            new Color(0.10f, 0.09f, 0.10f, 1f), // Raven Black
            new Color(0.42f, 0.26f, 0.14f, 1f), // Chestnut Brown
            new Color(0.62f, 0.28f, 0.16f, 1f), // Auburn
            new Color(0.86f, 0.72f, 0.38f, 1f), // Flaxen Gold
            new Color(0.62f, 0.62f, 0.64f, 1f), // Weathered Gray
        };

        public Color SkinColor => SkinTones[Mathf.Clamp(skinToneIndex, 0, SkinToneCount - 1)];
        public Color HairColor => HairColors[Mathf.Clamp(hairColorIndex, 0, HairColorCount - 1)];

        /// <summary>±2px horizontal jitter for natural pew crowding (deterministic).</summary>
        public float PewJitterX
        {
            get
            {
                // Map seed into [-2, +2] pixels.
                int h = seed ^ (pewSeatSalt * 73856093);
                float t = ((h & 0xFFFF) / 65535f) * 2f - 1f;
                return t * 2f;
            }
        }

        [NonSerialized] int pewSeatSalt;

        public void BindSeatSalt(int pewSeatIndex) => pewSeatSalt = pewSeatIndex;

        public static int HashSeed(string unitId, int pewSeat)
        {
            unchecked
            {
                int h = 17;
                if (!string.IsNullOrEmpty(unitId))
                {
                    for (int i = 0; i < unitId.Length; i++)
                        h = h * 31 + unitId[i];
                }
                h = h * 397 + pewSeat;
                return h == 0 ? 1 : h;
            }
        }

        /// <summary>
        /// Build a unique-but-stable look for this unit id / seat / tier.
        /// Cross-tier outfits ~20%. Helmets weighted for Warrior/Knight.
        /// </summary>
        public static TownsfolkAppearance Generate(
            UnitType tier,
            string unitId,
            int pewSeat,
            ModularSpriteLibrary library = null)
        {
            int seed = HashSeed(unitId, pewSeat);
            var rng = new System.Random(seed);
            var a = new TownsfolkAppearance
            {
                tier = tier,
                seed = seed,
                skinToneIndex = rng.Next(0, SkinToneCount),
                hairColorIndex = rng.Next(0, HairColorCount),
                poseIndex = rng.Next(0, PoseCount)
            };
            a.BindSeatSalt(pewSeat);

            a.outfitIndex = RollOutfitIndex(tier, rng);
            a.headIndex = RollHeadIndex(tier, rng, library);
            if (a.poseIndex == (int)TownsfolkPose.HoldingProp)
                a.propIndex = RollPropIndex(rng, library);
            else
                a.propIndex = 0;

            return a;
        }

        static int RollOutfitIndex(UnitType tier, System.Random rng)
        {
            int bandStart = tier switch
            {
                UnitType.Warrior => 2,
                UnitType.Knight => 4,
                _ => 0
            };

            // ~20% cross-tier mix for town variety.
            if (rng.NextDouble() < 0.20)
                return rng.Next(0, OutfitSlotCount);

            return bandStart + rng.Next(0, 2);
        }

        static int RollHeadIndex(UnitType tier, System.Random rng, ModularSpriteLibrary library)
        {
            int count = library != null ? library.HeadCount : 6;
            if (count <= 0) return 0;

            // Prefer helmets (non-hair) for Warrior/Knight.
            bool preferHelm = tier == UnitType.Warrior || tier == UnitType.Knight;
            if (library != null && preferHelm && rng.NextDouble() < 0.65)
            {
                // Collect helm indices; fall back to any.
                int picks = 0;
                int chosen = rng.Next(0, count);
                for (int attempt = 0; attempt < count; attempt++)
                {
                    int i = (chosen + attempt) % count;
                    if (!library.HeadIsHair(i))
                    {
                        picks++;
                        if (rng.Next(0, picks) == 0) chosen = i;
                    }
                }
                return chosen;
            }

            return rng.Next(0, count);
        }

        static int RollPropIndex(System.Random rng, ModularSpriteLibrary library)
        {
            int count = library != null ? library.PropCount : 4;
            if (count <= 1) return count == 1 ? 0 : 0;
            // Index 0 reserved as None in some sheets; prefer 1..count-1 when available.
            if (count > 1) return rng.Next(1, count);
            return 0;
        }
    }
}
