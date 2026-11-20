using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace ArthurianAutobattler
{
    /// <summary>
    /// Legend fusion recipes. Each fuse selects DISTINCT ingredient instances
    /// (no double-counting overlapping type/trait), removes exactly that many,
    /// then adds one product. Partial consumes are aborted.
    /// </summary>
    public class CardFusionSystem : MonoBehaviour
    {
        public static CardFusionSystem Instance;

        [Header("Hero Sprites")]
        public Sprite priestSprite;
        public Sprite captainSprite;
        public Sprite kingArthurSprite;
        public Sprite merlinSprite;
        public Sprite lancelotSprite;
        public Sprite morganSprite;
        public Sprite ladyOfLakeSprite;
        public Sprite gawainSprite;
        public Sprite mordredSprite;
        public Sprite galahadSprite;
        public Sprite tristanSprite;
        public Sprite percivalSprite;
        public Sprite kaySprite;
        public Sprite bedivereSprite;
        public Sprite guinevereSprite;

        private void Awake()
        {
            if (Instance == null) Instance = this;
        }

        public bool CanFusePriest(List<RecruitInstance> p) => CountType(p, UnitType.Peasant) >= 2;
        public bool CanFuseCaptain(List<RecruitInstance> p) => CountType(p, UnitType.Warrior) >= 2;

        public bool CanFuseKingArthur(List<RecruitInstance> p) =>
            CountType(p, UnitType.Knight) >= 2
            && CountType(p, UnitType.Captain) >= 1
            && !HasType(p, UnitType.KingArthur);

        // Balance V2: 2 Priests + 1 Knight
        public bool CanFuseMerlin(List<RecruitInstance> p) =>
            CountType(p, UnitType.Priest) >= 2
            && CountType(p, UnitType.Knight) >= 1
            && !HasType(p, UnitType.Merlin);

        // Balance V2: 2 Knights (incl. 1 Noble) + 1 Warrior
        public bool CanFuseLancelot(List<RecruitInstance> p) =>
            CountType(p, UnitType.Knight) >= 2
            && CountType(p, UnitType.Warrior) >= 1
            && p != null && p.Any(u => u != null && u.unitType == UnitType.Knight && u.primaryTrait == TraitTag.Noble)
            && !HasType(p, UnitType.SirLancelot);

        // Balance V2: 2 Priests + 1 Outlaw
        public bool CanFuseMorganLeFay(List<RecruitInstance> p) =>
            CanPickDistinct(p,
                r => r.unitType == UnitType.Priest,
                2,
                r => r.primaryTrait == TraitTag.Outlaw,
                1)
            && !HasType(p, UnitType.MorganLeFay);

        // Balance V2: 2 Priests + 1 Captain
        public bool CanFuseLadyOfTheLake(List<RecruitInstance> p) =>
            CountType(p, UnitType.Priest) >= 2
            && CountType(p, UnitType.Captain) >= 1
            && !HasType(p, UnitType.LadyOfTheLake);

        // Balance V2: 2 Warriors + 1 Knight
        public bool CanFuseGawain(List<RecruitInstance> p) =>
            CountType(p, UnitType.Warrior) >= 2
            && CountType(p, UnitType.Knight) >= 1
            && !HasType(p, UnitType.SirGawain);

        // Balance V2: 2 Knights + 1 Outlaw
        public bool CanFuseMordred(List<RecruitInstance> p) =>
            CanPickDistinct(p,
                r => r.unitType == UnitType.Knight,
                2,
                r => r.primaryTrait == TraitTag.Outlaw,
                1)
            && !HasType(p, UnitType.Mordred);

        // Balance V2: 1 Zealot/Loyalist Knight + 1 Priest + 1 Warrior
        public bool CanFuseGalahad(List<RecruitInstance> p) =>
            CountWhere(p, r => r.unitType == UnitType.Knight
                               && (r.primaryTrait == TraitTag.Zealot || r.primaryTrait == TraitTag.Loyalist)) >= 1
            && CountType(p, UnitType.Priest) >= 1
            && CountType(p, UnitType.Warrior) >= 1
            && !HasType(p, UnitType.SirGalahad);

        public bool CanFuseTristan(List<RecruitInstance> p) =>
            CountType(p, UnitType.Knight) >= 1
            && CountWhere(p, r => r.unitType == UnitType.Peasant
                                  && (r.primaryTrait == TraitTag.Outlaw || r.primaryTrait == TraitTag.Noble)) >= 1
            && !HasType(p, UnitType.SirTristan);

        public bool CanFusePercival(List<RecruitInstance> p) =>
            CountWhere(p, r => r.unitType == UnitType.Knight && r.primaryTrait == TraitTag.Zealot) >= 1
            && CountType(p, UnitType.Peasant) >= 1
            && !HasType(p, UnitType.SirPercival);

        public bool CanFuseKay(List<RecruitInstance> p) =>
            CountType(p, UnitType.Warrior) >= 1
            && CountType(p, UnitType.Peasant) >= 2
            && !HasType(p, UnitType.SirKay);

        public bool CanFuseBedivere(List<RecruitInstance> p) =>
            CountType(p, UnitType.Knight) >= 1
            && CountType(p, UnitType.Warrior) >= 1
            && !HasType(p, UnitType.SirBedivere);

        public bool CanFuseGuinevere(List<RecruitInstance> p) =>
            CountWhere(p, r => r.primaryTrait == TraitTag.Noble) >= 1
            && CountType(p, UnitType.Priest) >= 1
            && !HasType(p, UnitType.Guinevere);

        public bool FusePriest(List<RecruitInstance> p) =>
            TryFuse(p, CanFusePriest, () => CreateHero("unit_priest", "Town Priest", UnitType.Priest, priestSprite, 40, 9, 1.4f, TraitTag.Zealot, CombatRole.BacklineRanged),
                new IngredientPick(r => r.unitType == UnitType.Peasant, 2));

        public bool FuseCaptain(List<RecruitInstance> p) =>
            TryFuse(p, CanFuseCaptain, () => CreateHero("unit_captain", "Vanguard Captain", UnitType.Captain, captainSprite, 110, 16, 1.6f, TraitTag.Noble, CombatRole.FrontlineTank),
                new IngredientPick(r => r.unitType == UnitType.Warrior, 2));

        public bool FuseKingArthur(List<RecruitInstance> p) =>
            TryFuse(p, CanFuseKingArthur, () => CreateHero("hero_arthur", "King Arthur", UnitType.KingArthur, kingArthurSprite, 290, 34, 1.9f, TraitTag.Loyalist, CombatRole.FrontlineTank),
                new IngredientPick(r => r.unitType == UnitType.Knight, 2),
                new IngredientPick(r => r.unitType == UnitType.Captain, 1));

        public bool FuseMerlin(List<RecruitInstance> p) =>
            TryFuse(p, CanFuseMerlin, () => CreateHero("hero_merlin", "Merlin the Archmage", UnitType.Merlin, merlinSprite, 195, 30, 1.5f, TraitTag.Zealot, CombatRole.BacklineRanged),
                new IngredientPick(r => r.unitType == UnitType.Priest, 2),
                new IngredientPick(r => r.unitType == UnitType.Knight, 1));

        public bool FuseLancelot(List<RecruitInstance> p) =>
            TryFuse(p, CanFuseLancelot, () => CreateHero("hero_lancelot", "Sir Lancelot", UnitType.SirLancelot, lancelotSprite, 240, 29, 2.0f, TraitTag.Noble, CombatRole.FrontlineTank),
                new IngredientPick(r => r.unitType == UnitType.Knight && r.primaryTrait == TraitTag.Noble, 1),
                new IngredientPick(r => r.unitType == UnitType.Knight, 1),
                new IngredientPick(r => r.unitType == UnitType.Warrior, 1));

        public bool FuseMorganLeFay(List<RecruitInstance> p) =>
            TryFuse(p, CanFuseMorganLeFay, () => CreateHero("hero_morgan", "Morgan le Fay", UnitType.MorganLeFay, morganSprite, 175, 24, 1.5f, TraitTag.Outlaw, CombatRole.BacklineRanged),
                new IngredientPick(r => r.unitType == UnitType.Priest, 2),
                new IngredientPick(r => r.primaryTrait == TraitTag.Outlaw, 1));

        public bool FuseLadyOfTheLake(List<RecruitInstance> p) =>
            TryFuse(p, CanFuseLadyOfTheLake, () => CreateHero("hero_lady_lake", "Lady of the Lake", UnitType.LadyOfTheLake, ladyOfLakeSprite, 210, 18, 1.4f, TraitTag.Zealot, CombatRole.BacklineRanged),
                new IngredientPick(r => r.unitType == UnitType.Priest, 2),
                new IngredientPick(r => r.unitType == UnitType.Captain, 1));

        public bool FuseGawain(List<RecruitInstance> p) =>
            TryFuse(p, CanFuseGawain, () => CreateHero("hero_gawain", "Sir Gawain", UnitType.SirGawain, gawainSprite, 245, 26, 1.6f, TraitTag.Loyalist, CombatRole.FrontlineTank),
                new IngredientPick(r => r.unitType == UnitType.Warrior, 2),
                new IngredientPick(r => r.unitType == UnitType.Knight, 1));

        public bool FuseMordred(List<RecruitInstance> p) =>
            TryFuse(p, CanFuseMordred, () => CreateHero("hero_mordred", "Mordred", UnitType.Mordred, mordredSprite, 260, 36, 1.8f, TraitTag.Outlaw, CombatRole.FrontlineTank),
                new IngredientPick(r => r.unitType == UnitType.Knight, 2),
                new IngredientPick(r => r.primaryTrait == TraitTag.Outlaw, 1));

        public bool FuseGalahad(List<RecruitInstance> p) =>
            TryFuse(p, CanFuseGalahad, () => CreateHero("hero_galahad", "Sir Galahad", UnitType.SirGalahad, galahadSprite, 270, 27, 1.7f, TraitTag.Loyalist, CombatRole.FrontlineTank),
                new IngredientPick(r => r.unitType == UnitType.Knight
                                        && (r.primaryTrait == TraitTag.Zealot || r.primaryTrait == TraitTag.Loyalist), 1),
                new IngredientPick(r => r.unitType == UnitType.Priest, 1),
                new IngredientPick(r => r.unitType == UnitType.Warrior, 1));

        public bool FuseTristan(List<RecruitInstance> p) =>
            TryFuse(p, CanFuseTristan, () =>
                {
                    var h = CreateHero("hero_tristan", "Sir Tristan", UnitType.SirTristan, tristanSprite, 185, 23, 1.8f, TraitTag.Noble, CombatRole.BacklineRanged);
                    h.canAttackFromBack = true;
                    return h;
                },
                new IngredientPick(r => r.unitType == UnitType.Knight, 1),
                new IngredientPick(r => r.unitType == UnitType.Peasant
                                        && (r.primaryTrait == TraitTag.Outlaw || r.primaryTrait == TraitTag.Noble), 1));

        public bool FusePercival(List<RecruitInstance> p) =>
            TryFuse(p, CanFusePercival, () => CreateHero("hero_percival", "Sir Percival", UnitType.SirPercival, percivalSprite, 200, 22, 1.6f, TraitTag.Zealot, CombatRole.FrontlineTank),
                new IngredientPick(r => r.unitType == UnitType.Knight && r.primaryTrait == TraitTag.Zealot, 1),
                new IngredientPick(r => r.unitType == UnitType.Peasant, 1));

        public bool FuseKay(List<RecruitInstance> p) =>
            TryFuse(p, CanFuseKay, () => CreateHero("hero_kay", "Sir Kay", UnitType.SirKay, kaySprite, 135, 17, 1.5f, TraitTag.Loyalist, CombatRole.FrontlineTank),
                new IngredientPick(r => r.unitType == UnitType.Warrior, 1),
                new IngredientPick(r => r.unitType == UnitType.Peasant, 2));

        public bool FuseBedivere(List<RecruitInstance> p) =>
            TryFuse(p, CanFuseBedivere, () => CreateHero("hero_bedivere", "Sir Bedivere", UnitType.SirBedivere, bedivereSprite, 190, 21, 1.7f, TraitTag.Loyalist, CombatRole.FrontlineTank),
                new IngredientPick(r => r.unitType == UnitType.Knight, 1),
                new IngredientPick(r => r.unitType == UnitType.Warrior, 1));

        public bool FuseGuinevere(List<RecruitInstance> p) =>
            TryFuse(p, CanFuseGuinevere, () => CreateHero("hero_guinevere", "Guinevere", UnitType.Guinevere, guinevereSprite, 140, 12, 1.4f, TraitTag.Noble, CombatRole.BacklineRanged),
                new IngredientPick(r => r.primaryTrait == TraitTag.Noble, 1),
                new IngredientPick(r => r.unitType == UnitType.Priest, 1));

        private readonly struct IngredientPick
        {
            public readonly Func<RecruitInstance, bool> Match;
            public readonly int Count;
            public IngredientPick(Func<RecruitInstance, bool> match, int count)
            {
                Match = match;
                Count = count;
            }
        }

        /// <summary>
        /// Pick distinct units for each ingredient group in order, remove exactly those, add product.
        /// Aborts with no mutation if any group cannot be filled from remaining candidates.
        /// </summary>
        private bool TryFuse(
            List<RecruitInstance> party,
            Func<List<RecruitInstance>, bool> canFuse,
            Func<RecruitInstance> createProduct,
            params IngredientPick[] picks)
        {
            if (party == null || createProduct == null || picks == null || picks.Length == 0)
                return false;
            if (!canFuse(party)) return false;

            var chosen = new List<RecruitInstance>();
            var used = new HashSet<RecruitInstance>();

            foreach (var pick in picks)
            {
                int need = pick.Count;
                int got = 0;
                for (int i = party.Count - 1; i >= 0 && got < need; i--)
                {
                    var r = party[i];
                    if (r == null || used.Contains(r)) continue;
                    if (!pick.Match(r)) continue;
                    chosen.Add(r);
                    used.Add(r);
                    got++;
                }

                if (got < need)
                {
                    Debug.LogWarning(
                        $"[DemonKnight] Fusion aborted — needed {need} ingredient(s) for a pick, only found {got}. No units consumed.");
                    return false;
                }
            }

            int expected = 0;
            foreach (var pick in picks) expected += pick.Count;
            if (chosen.Count != expected)
            {
                Debug.LogWarning(
                    $"[DemonKnight] Fusion aborted — expected {expected} distinct ingredients, selected {chosen.Count}.");
                return false;
            }

            foreach (var r in chosen)
                party.Remove(r);

            var product = createProduct();
            if (product == null)
            {
                // Restore if product factory failed (should never happen).
                party.AddRange(chosen);
                return false;
            }

            // Remember exact pre-fusion units (stats / pew seats / nested fusions) for wave-end unfuse.
            product.fusionIngredients = new List<RecruitInstance>(chosen.Count);
            foreach (var r in chosen)
                product.fusionIngredients.Add(CloneForFusionMemory(r));

            party.Add(product);
            Debug.Log(
                $"[DemonKnight] Fused {product.characterName}: consumed {chosen.Count} ingredient(s), party size now {party.Count}.");
            return true;
        }

        /// <summary>
        /// Wave-end: replace every fused Legend with its original ingredients (recursive).
        /// Base pew townsfolk keep identity/stats; fused products are discarded.
        /// </summary>
        public static List<RecruitInstance> UnfusePartyToOriginals(List<RecruitInstance> party)
        {
            var result = new List<RecruitInstance>();
            if (party == null) return result;
            foreach (var r in party)
            {
                if (r == null) continue;
                ExpandFusionRecursive(r, result);
            }
            return result;
        }

        private static void ExpandFusionRecursive(RecruitInstance r, List<RecruitInstance> into)
        {
            if (r == null) return;
            if (r.IsFusedLegend)
            {
                foreach (var ing in r.fusionIngredients)
                    ExpandFusionRecursive(ing, into);
                return;
            }
            // Mordred is Legend/fuse-only — never a pew leaf (Usurpation or stray product).
            if (r.unitType == UnitType.Mordred)
                return;
            into.Add(r);
        }

        /// <summary>Deep clone including nested fusionIngredients (sprites shared by reference).</summary>
        public static RecruitInstance CloneForFusionMemory(RecruitInstance src)
        {
            if (src == null) return null;
            var c = new RecruitInstance
            {
                unitID = src.unitID,
                characterName = src.characterName,
                unitType = src.unitType,
                randomAvatar = src.randomAvatar,
                assignedRole = src.assignedRole,
                maxHealth = src.maxHealth,
                attackDamage = src.attackDamage,
                attackSpeed = src.attackSpeed,
                baseHireCost = src.baseHireCost,
                primaryTrait = src.primaryTrait,
                hatedTrait = src.hatedTrait,
                lovedTrait = src.lovedTrait,
                intelTierUnlocked = src.intelTierUnlocked,
                pewSeatIndex = src.pewSeatIndex,
                bodyPosture = src.bodyPosture,
                bodySprite = src.bodySprite,
                headSprite = src.headSprite,
                moodOverlaySprite = src.moodOverlaySprite,
                isSirenEnchantress = src.isSirenEnchantress,
                victoryStars = src.victoryStars,
                canAttackFromBack = src.canAttackFromBack,
                appearance = src.appearance == null ? null : new TownsfolkAppearance
                {
                    tier = src.appearance.tier,
                    seed = src.appearance.seed,
                    skinToneIndex = src.appearance.skinToneIndex,
                    hairColorIndex = src.appearance.hairColorIndex,
                    poseIndex = src.appearance.poseIndex,
                    outfitIndex = src.appearance.outfitIndex,
                    headIndex = src.appearance.headIndex,
                    propIndex = src.appearance.propIndex
                }
            };
            if (src.fusionIngredients != null && src.fusionIngredients.Count > 0)
            {
                c.fusionIngredients = new List<RecruitInstance>(src.fusionIngredients.Count);
                foreach (var ing in src.fusionIngredients)
                    c.fusionIngredients.Add(CloneForFusionMemory(ing));
            }
            return c;
        }

        private static bool CanPickDistinct(
            List<RecruitInstance> p,
            Func<RecruitInstance, bool> first,
            int firstCount,
            Func<RecruitInstance, bool> second,
            int secondCount)
        {
            if (p == null) return false;
            var used = new HashSet<RecruitInstance>();
            int got = 0;
            for (int i = p.Count - 1; i >= 0 && got < firstCount; i--)
            {
                var r = p[i];
                if (r == null || used.Contains(r)) continue;
                if (!first(r)) continue;
                used.Add(r);
                got++;
            }
            if (got < firstCount) return false;
            got = 0;
            for (int i = p.Count - 1; i >= 0 && got < secondCount; i--)
            {
                var r = p[i];
                if (r == null || used.Contains(r)) continue;
                if (!second(r)) continue;
                used.Add(r);
                got++;
            }
            return got >= secondCount;
        }

        private static int CountType(List<RecruitInstance> p, UnitType t) =>
            CountWhere(p, r => r.unitType == t);

        private static int CountWhere(List<RecruitInstance> p, Func<RecruitInstance, bool> pred)
        {
            if (p == null || pred == null) return 0;
            int n = 0;
            foreach (var r in p)
            {
                if (r != null && pred(r)) n++;
            }
            return n;
        }

        private static bool HasType(List<RecruitInstance> p, UnitType t) =>
            p != null && p.Any(u => u != null && u.unitType == t);

        private RecruitInstance CreateHero(string id, string name, UnitType type, Sprite sp, int hp, int atk, float aps, TraitTag trait, CombatRole role)
        {
            return new RecruitInstance
            {
                unitID = id,
                characterName = name,
                unitType = type,
                randomAvatar = sp,
                headSprite = sp,
                maxHealth = hp,
                attackDamage = atk,
                attackSpeed = aps,
                primaryTrait = trait,
                assignedRole = role,
                intelTierUnlocked = 3,
                baseHireCost = Mathf.Max(40, hp / 2),
                canAttackFromBack = RecruitInstance.IsRangedUnitType(type) || role == CombatRole.BacklineRanged
            };
        }
    }
}
