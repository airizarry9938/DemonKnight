using System.Collections.Generic;
using UnityEngine;

namespace ArthurianAutobattler
{
    /// <summary>
    /// 5-tier Hidden Potential / Secret Archetypes / Bloodline Fusion helpers.
    /// Rolls, growth math, combat trait hooks, and fusion inheritance.
    /// </summary>
    public static class HiddenPotentialSystem
    {
        public const string TraitCoward = "Coward";
        public const string TraitTurncoat = "Turncoat";
        public const string TraitGlutton = "Glutton";
        public const string TraitSleeperSpike = "SleeperSpike";
        public const string TraitIronLung = "IronLung";
        public const string TraitMartyrsWard = "MartyrsWard";
        public const string TraitGiantSlayer = "GiantSlayer";
        public const string TraitTitheBearer = "TitheBearer";
        public const string TraitPendragonBlood = "PendragonBlood";
        public const string TraitGrailTouched = "GrailTouched";
        public const string TraitFaeKissed = "FaeKissed";

        static readonly string[] LiabilityTraits = { TraitCoward, TraitTurncoat, TraitGlutton };
        static readonly string[] DarkhorseTraits = { TraitSleeperSpike, TraitIronLung };
        static readonly string[] GoldenEggTraits =
            { TraitMartyrsWard, TraitGiantSlayer, TraitTitheBearer };
        static readonly string[] MythicVesselTraits =
            { TraitPendragonBlood, TraitGrailTouched, TraitFaeKissed };

        /// <summary>Weighted pew roll: Parishioner 62 / Liability 16 / Darkhorse 12 / GoldenEgg 7 / MythicVessel 3.</summary>
        public static void RollOnto(RecruitInstance r)
        {
            if (r == null) return;
            float roll = Random.value;
            if (roll < 0.62f)
            {
                ApplyTier(r, SecretArchetype.Parishioner, Random.Range(0.95f, 1.10f), "");
            }
            else if (roll < 0.78f)
            {
                ApplyTier(r, SecretArchetype.Liability, Random.Range(0.80f, 0.95f),
                    LiabilityTraits[Random.Range(0, LiabilityTraits.Length)]);
            }
            else if (roll < 0.90f)
            {
                ApplyTier(r, SecretArchetype.Darkhorse, Random.Range(1.35f, 1.60f),
                    DarkhorseTraits[Random.Range(0, DarkhorseTraits.Length)]);
            }
            else if (roll < 0.97f)
            {
                ApplyTier(r, SecretArchetype.GoldenEgg, Random.Range(1.80f, 2.00f),
                    GoldenEggTraits[Random.Range(0, GoldenEggTraits.Length)]);
            }
            else
            {
                ApplyTier(r, SecretArchetype.MythicVessel, Random.Range(2.15f, 2.50f),
                    MythicVesselTraits[Random.Range(0, MythicVesselTraits.Length)]);
            }
        }

        static void ApplyTier(RecruitInstance r, SecretArchetype arch, float mult, string trait)
        {
            r.secretArchetype = arch;
            r.potentialMultiplier = mult;
            r.hiddenTraitId = trait ?? "";
            // Liability combat tags also mirror onto primaryTrait for Morgan unlock / rivalry.
            if (arch == SecretArchetype.Liability)
            {
                if (trait == TraitCoward) r.primaryTrait = TraitTag.Coward;
                else if (trait == TraitTurncoat) r.primaryTrait = TraitTag.Outlaw;
                else if (trait == TraitGlutton) r.primaryTrait = TraitTag.Drunkard;
            }
        }

        /// <summary>
        /// Migrate legacy saves where hiddenTraitId held archetype names
        /// (Darkhorse / Golden Egg / GrailTouched / Legend Spark).
        /// </summary>
        public static void MigrateLegacySpark(RecruitInstance r)
        {
            if (r == null) return;
            if (r.secretArchetype != SecretArchetype.Parishioner || r.potentialMultiplier > 1.01f)
                return;
            if (string.IsNullOrEmpty(r.hiddenTraitId)) return;

            string id = r.hiddenTraitId;
            if (string.Equals(id, RecruitInstance.DarkhorseId, System.StringComparison.Ordinal)
                || string.Equals(id, RecruitInstance.LegendSparkId, System.StringComparison.Ordinal))
            {
                r.secretArchetype = SecretArchetype.Darkhorse;
                r.potentialMultiplier = Random.Range(1.35f, 1.60f);
                r.hiddenTraitId = DarkhorseTraits[Random.Range(0, DarkhorseTraits.Length)];
            }
            else if (string.Equals(id, RecruitInstance.GoldenEggId, System.StringComparison.Ordinal))
            {
                r.secretArchetype = SecretArchetype.GoldenEgg;
                r.potentialMultiplier = Random.Range(1.80f, 2.00f);
                r.hiddenTraitId = GoldenEggTraits[Random.Range(0, GoldenEggTraits.Length)];
            }
            else if (string.Equals(id, RecruitInstance.GrailTouchedId, System.StringComparison.Ordinal)
                     || string.Equals(id, TraitGrailTouched, System.StringComparison.Ordinal))
            {
                r.secretArchetype = SecretArchetype.MythicVessel;
                r.potentialMultiplier = Random.Range(2.15f, 2.50f);
                r.hiddenTraitId = TraitGrailTouched;
            }
        }

        public static int EffectiveGrowth(int trainedStat, int veteranStarBonus, float potentialMultiplier)
        {
            float mult = Mathf.Max(0.01f, potentialMultiplier);
            return Mathf.RoundToInt((trainedStat + Mathf.Max(0, veteranStarBonus)) * mult);
        }

        /// <summary>Apply trained + star growth onto a battle clone (base stats already copied).</summary>
        public static void ApplyGrowthToClone(RecruitInstance clone, RecruitInstance canonical)
        {
            if (clone == null || canonical == null) return;
            int stars = canonical.EffectiveVeteranRank;
            float mult = canonical.potentialMultiplier > 0f ? canonical.potentialMultiplier : 1f;
            clone.potentialMultiplier = mult;
            clone.secretArchetype = canonical.secretArchetype;
            clone.hiddenTraitId = canonical.hiddenTraitId;
            clone.trainedHP = canonical.trainedHP;
            clone.trainedATK = canonical.trainedATK;
            clone.trainedSPD = canonical.trainedSPD;
            clone.lastTrainWaveNumber = canonical.lastTrainWaveNumber;
            clone.wavesSurvived = canonical.wavesSurvived;
            clone.hasAscended = canonical.hasAscended;
            clone.traitAwakened = canonical.traitAwakened;
            clone.isScouted = canonical.isScouted;

            clone.maxHealth = Mathf.Max(1, clone.maxHealth + EffectiveGrowth(canonical.trainedHP, stars, mult));
            clone.attackDamage = Mathf.Max(1, clone.attackDamage + EffectiveGrowth(canonical.trainedATK, stars, mult));
            // SPD: each trained point = 0.1 APS; growth multiplies the trained+star pool.
            float spdGrowth = EffectiveGrowth(canonical.trainedSPD, stars, mult) * 0.1f;
            clone.attackSpeed = Mathf.Clamp(clone.attackSpeed + spdGrowth, 0.4f, 3.0f);
        }

        public static bool IsAscensionEligible(SecretArchetype arch) =>
            arch == SecretArchetype.Darkhorse
            || arch == SecretArchetype.GoldenEgg
            || arch == SecretArchetype.MythicVessel;

        public static bool IsPositiveCombatTrait(string traitId)
        {
            if (string.IsNullOrEmpty(traitId)) return false;
            return traitId == TraitSleeperSpike || traitId == TraitIronLung
                   || traitId == TraitMartyrsWard || traitId == TraitGiantSlayer
                   || traitId == TraitTitheBearer || traitId == TraitPendragonBlood
                   || traitId == TraitGrailTouched || traitId == TraitFaeKissed;
        }

        public static bool IsLiabilityTrait(string traitId) =>
            traitId == TraitCoward || traitId == TraitTurncoat || traitId == TraitGlutton;

        public static int TraitTierRank(SecretArchetype arch) => arch switch
        {
            SecretArchetype.MythicVessel => 3,
            SecretArchetype.GoldenEgg => 2,
            SecretArchetype.Darkhorse => 1,
            _ => 0
        };

        /// <summary>
        /// Bloodline inherit: max potentialMultiplier among ingredients (Liability stripped),
        /// one positive trait with Mythic &gt; Golden &gt; Darkhorse priority.
        /// </summary>
        public static void InheritBloodline(RecruitInstance product, List<RecruitInstance> ingredients)
        {
            if (product == null || ingredients == null || ingredients.Count == 0) return;

            float maxMult = 1f;
            SecretArchetype bestArch = SecretArchetype.Parishioner;
            string bestTrait = "";
            int bestRank = 0;

            foreach (var ing in ingredients)
            {
                if (ing == null) continue;
                MigrateLegacySpark(ing);
                if (ing.secretArchetype == SecretArchetype.Liability)
                    continue; // strip Liability from bloodline

                if (ing.potentialMultiplier > maxMult)
                    maxMult = ing.potentialMultiplier;

                int rank = TraitTierRank(ing.secretArchetype);
                if (rank > bestRank && IsPositiveCombatTrait(ing.hiddenTraitId))
                {
                    bestRank = rank;
                    bestArch = ing.secretArchetype;
                    bestTrait = ing.hiddenTraitId;
                }
                else if (rank == bestRank && rank > 0 && IsPositiveCombatTrait(ing.hiddenTraitId)
                         && string.IsNullOrEmpty(bestTrait))
                {
                    bestArch = ing.secretArchetype;
                    bestTrait = ing.hiddenTraitId;
                }
            }

            product.potentialMultiplier = maxMult;
            if (bestRank > 0)
            {
                product.secretArchetype = bestArch;
                product.hiddenTraitId = bestTrait;
            }
            else
            {
                product.secretArchetype = SecretArchetype.Parishioner;
                product.hiddenTraitId = "";
            }

            // Sum trained pools (bloodline memory of work).
            int tHp = 0, tAtk = 0, tSpd = 0, waves = 0;
            foreach (var ing in ingredients)
            {
                if (ing == null) continue;
                tHp += Mathf.Max(0, ing.trainedHP);
                tAtk += Mathf.Max(0, ing.trainedATK);
                tSpd += Mathf.Max(0, ing.trainedSPD);
                waves = Mathf.Max(waves, ing.wavesSurvived);
            }
            product.trainedHP = tHp;
            product.trainedATK = tAtk;
            product.trainedSPD = tSpd;
            product.wavesSurvived = waves;
            product.hasAscended = false;
            product.traitAwakened = false;
            product.isScouted = true;
            product.intelTierUnlocked = Mathf.Max(product.intelTierUnlocked, 1);
        }

        /// <summary>
        /// Wave victory: ++wavesSurvived; at 3 waves ascend Darkhorse/GoldenEgg/MythicVessel once.
        /// Returns true when a MythicVessel just ascended (victory banner).
        /// </summary>
        public static bool TryAscendSurvivor(RecruitInstance r)
        {
            if (r == null) return false;
            r.wavesSurvived = Mathf.Max(0, r.wavesSurvived) + 1;
            if (r.hasAscended) return false;
            if (!IsAscensionEligible(r.secretArchetype)) return false;
            if (r.wavesSurvived < 3) return false;

            r.hasAscended = true;
            r.maxHealth = Mathf.Max(1, Mathf.RoundToInt(r.maxHealth * 1.25f));
            r.attackDamage = Mathf.Max(1, Mathf.RoundToInt(r.attackDamage * 1.25f));
            r.attackSpeed = Mathf.Min(3f, r.attackSpeed * 1.25f);
            r.isScouted = true;
            r.intelTierUnlocked = Mathf.Max(r.intelTierUnlocked, 1);
            if (!r.traitAwakened && !string.IsNullOrEmpty(r.hiddenTraitId))
            {
                r.traitAwakened = true;
                LegendUnlockManager.Ensure()?.NotifyHiddenPotentialAwakened(r);
            }
            LegendUnlockManager.Ensure()?.NotifyHiddenPotentialAscended(r);
            return r.secretArchetype == SecretArchetype.MythicVessel;
        }

        public static void CopyFields(RecruitInstance dest, RecruitInstance src)
        {
            if (dest == null || src == null) return;
            dest.secretArchetype = src.secretArchetype;
            dest.potentialMultiplier = src.potentialMultiplier;
            dest.hiddenTraitId = src.hiddenTraitId;
            dest.isScouted = src.isScouted;
            dest.traitAwakened = src.traitAwakened;
            dest.hasAscended = src.hasAscended;
            dest.wavesSurvived = src.wavesSurvived;
            dest.trainedHP = src.trainedHP;
            dest.trainedATK = src.trainedATK;
            dest.trainedSPD = src.trainedSPD;
        }
    }
}
