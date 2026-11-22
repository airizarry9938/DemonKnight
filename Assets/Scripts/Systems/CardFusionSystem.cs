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

        /// <summary>Data-driven fuse check — prefers CharacterDatabase recipes.</summary>
        public bool CanFuseDefinition(CharacterDefinition def, List<RecruitInstance> p)
        {
            var db = CharacterDatabase.Load();
            if (db != null && def != null && def.HasFusionRecipe)
                return db.CanFuse(def, p);
            return false;
        }

        public bool FuseDefinition(CharacterDefinition def, List<RecruitInstance> p)
        {
            var db = CharacterDatabase.Load();
            if (db == null || def == null || !def.HasFusionRecipe) return false;
            // Apply inspector sprites as fallback when definition sprites are empty.
            if (def.portraitSprite == null)
                def.portraitSprite = LegacySpriteFor(def.unitType);
            return db.TryFuse(def, p, out _);
        }

        public bool CanFuseType(UnitType type, List<RecruitInstance> p)
        {
            SanitizeBaseUnitFusionFlags(p);
            // Merlin is narrative-only — never fuse/summon from the playable pool.
            if (type == UnitType.Merlin) return false;
            if (!IsFusionUnlocked(type)) return false;
            var db = CharacterDatabase.Load();
            var def = db?.GetByUnitType(type);
            if (def != null && def.HasFusionRecipe)
            {
                if (db.CanFuse(def, p)) return true;
                // DB recipe miss: still allow ingredient-only legacy (multi Priest while peasants remain).
                bool legacy = CanFuseLegacy(type, p);
                if (legacy)
                    Debug.LogWarning(
                        $"[DemonKnight] CanFuseType {type}: DB recipe blocked but legacy ingredients OK — allowing.");
                return legacy;
            }
            return CanFuseLegacy(type, p);
        }

        public bool FuseType(UnitType type, List<RecruitInstance> p)
        {
            SanitizeBaseUnitFusionFlags(p);
            if (type == UnitType.Merlin)
            {
                Debug.LogWarning("[DemonKnight] Fuse blocked — Merlin is missing (not in playable legend pool).");
                return false;
            }
            if (!IsFusionUnlocked(type))
            {
                Debug.LogWarning(
                    $"[DemonKnight] Fuse blocked — legend not unlocked: {LegendUnlockManager.LegendIdFor(type)}");
                return false;
            }
            var db = CharacterDatabase.Load();
            var def = db?.GetByUnitType(type);
            if (def != null && def.HasFusionRecipe)
            {
                if (def.portraitSprite == null)
                    def.portraitSprite = LegacySpriteFor(type);
                if (def.battleSprite == null && def.portraitSprite != null)
                    def.battleSprite = def.portraitSprite;
                if (db.TryFuse(def, p, out _)) return true;
                // Fall back to legacy fuse path when SO recipe fails but ingredients exist.
                if (CanFuseLegacy(type, p))
                {
                    Debug.LogWarning(
                        $"[DemonKnight] FuseType {type}: DB TryFuse failed — using legacy ingredient fuse.");
                    return FuseLegacy(type, p);
                }
                return false;
            }
            return FuseLegacy(type, p);
        }

        /// <summary>
        /// Gated legends require LegendUnlockManager; Captain / LadyOfTheLake stay open mid-tier.
        /// </summary>
        public static bool IsFusionUnlocked(UnitType type)
        {
            if (!LegendUnlockManager.IsGatedLegend(type)) return true;
            var mgr = LegendUnlockManager.Ensure();
            return mgr != null && mgr.IsLegendUnlocked(type);
        }

        /// <summary>
        /// Lives on CardFusionSystem so Force Sync cannot CS0117 when CharacterDatabase.cs
        /// lags one pass behind (Andrew: CardFusionSystem called missing DB method).
        /// Invariant: base troops (Peasant/Warrior/Knight) never carry fusion memory.
        /// </summary>
        public static void SanitizeBaseUnitFusionFlags(List<RecruitInstance> party)
        {
            if (party == null) return;
            for (int i = 0; i < party.Count; i++)
                SanitizeBaseUnitFusionFlags(party[i]);
        }

        /// <summary>
        /// Spawn / deserialize invariant — clear fusionIngredients on base units so they
        /// are never misclassified as fused legends. (fusionParts live only on autosave.)
        /// </summary>
        public static void SanitizeBaseUnitFusionFlags(RecruitInstance r)
        {
            if (r == null) return;
            if (!RecruitInstance.IsBaseTroopType(r.unitType)) return;
            if (r.fusionIngredients != null && r.fusionIngredients.Count > 0)
            {
                Debug.LogWarning(
                    $"[DemonKnight] Cleared bogus fusionIngredients on base unit " +
                    $"{r.characterName} ({r.unitType}) — was blocking multi-fuse.");
            }
            r.fusionIngredients = null;
        }

        public bool CanFusePriest(List<RecruitInstance> p) => CanFuseType(UnitType.Priest, p);
        public bool CanFuseCaptain(List<RecruitInstance> p) => CanFuseType(UnitType.Captain, p);
        public bool CanFuseKingArthur(List<RecruitInstance> p) => CanFuseType(UnitType.KingArthur, p);
        public bool CanFuseMerlin(List<RecruitInstance> p) => false;
        public bool CanFuseLancelot(List<RecruitInstance> p) => CanFuseType(UnitType.SirLancelot, p);
        public bool CanFuseMorganLeFay(List<RecruitInstance> p) => CanFuseType(UnitType.MorganLeFay, p);
        public bool CanFuseLadyOfTheLake(List<RecruitInstance> p) => CanFuseType(UnitType.LadyOfTheLake, p);
        public bool CanFuseGawain(List<RecruitInstance> p) => CanFuseType(UnitType.SirGawain, p);
        public bool CanFuseMordred(List<RecruitInstance> p) => CanFuseType(UnitType.Mordred, p);
        public bool CanFuseGalahad(List<RecruitInstance> p) => CanFuseType(UnitType.SirGalahad, p);
        public bool CanFuseTristan(List<RecruitInstance> p) => CanFuseType(UnitType.SirTristan, p);
        public bool CanFusePercival(List<RecruitInstance> p) => CanFuseType(UnitType.SirPercival, p);
        public bool CanFuseKay(List<RecruitInstance> p) => CanFuseType(UnitType.SirKay, p);
        public bool CanFuseBedivere(List<RecruitInstance> p) => CanFuseType(UnitType.SirBedivere, p);
        public bool CanFuseGuinevere(List<RecruitInstance> p) => CanFuseType(UnitType.Guinevere, p);

        public bool FusePriest(List<RecruitInstance> p) => FuseType(UnitType.Priest, p);
        public bool FuseCaptain(List<RecruitInstance> p) => FuseType(UnitType.Captain, p);
        public bool FuseKingArthur(List<RecruitInstance> p) => FuseType(UnitType.KingArthur, p);
        public bool FuseMerlin(List<RecruitInstance> p) => false;
        public bool FuseLancelot(List<RecruitInstance> p) => FuseType(UnitType.SirLancelot, p);
        public bool FuseMorganLeFay(List<RecruitInstance> p) => FuseType(UnitType.MorganLeFay, p);
        public bool FuseLadyOfTheLake(List<RecruitInstance> p) => FuseType(UnitType.LadyOfTheLake, p);
        public bool FuseGawain(List<RecruitInstance> p) => FuseType(UnitType.SirGawain, p);
        public bool FuseMordred(List<RecruitInstance> p) => FuseType(UnitType.Mordred, p);
        public bool FuseGalahad(List<RecruitInstance> p) => FuseType(UnitType.SirGalahad, p);
        public bool FuseTristan(List<RecruitInstance> p) => FuseType(UnitType.SirTristan, p);
        public bool FusePercival(List<RecruitInstance> p) => FuseType(UnitType.SirPercival, p);
        public bool FuseKay(List<RecruitInstance> p) => FuseType(UnitType.SirKay, p);
        public bool FuseBedivere(List<RecruitInstance> p) => FuseType(UnitType.SirBedivere, p);
        public bool FuseGuinevere(List<RecruitInstance> p) => FuseType(UnitType.Guinevere, p);

        Sprite LegacySpriteFor(UnitType type) => type switch
        {
            UnitType.Priest => priestSprite,
            UnitType.Captain => captainSprite,
            UnitType.KingArthur => kingArthurSprite,
            UnitType.Merlin => merlinSprite,
            UnitType.SirLancelot => lancelotSprite,
            UnitType.MorganLeFay => morganSprite,
            UnitType.LadyOfTheLake => ladyOfLakeSprite,
            UnitType.SirGawain => gawainSprite,
            UnitType.Mordred => mordredSprite,
            UnitType.SirGalahad => galahadSprite,
            UnitType.SirTristan => tristanSprite,
            UnitType.SirPercival => percivalSprite,
            UnitType.SirKay => kaySprite,
            UnitType.SirBedivere => bedivereSprite,
            UnitType.Guinevere => guinevereSprite,
            _ => null
        };

        /// <summary>
        /// Ingredient-only gates. Do NOT block when the product type is already in party —
        /// e.g. fuse Priest repeatedly while ≥2 Peasants remain.
        /// </summary>
        bool CanFuseLegacy(UnitType type, List<RecruitInstance> p) => type switch
        {
            UnitType.Priest => CountType(p, UnitType.Peasant) >= 2,
            UnitType.Captain => CountType(p, UnitType.Warrior) >= 2,
            UnitType.KingArthur => CountType(p, UnitType.Knight) >= 2
                                  && CountType(p, UnitType.Captain) >= 1,
            UnitType.Merlin => false, // narrative-only — not fuseable
            UnitType.SirLancelot => CountType(p, UnitType.Knight) >= 2
                                    && CountType(p, UnitType.Warrior) >= 1
                                    && p != null && p.Any(u => u != null && u.unitType == UnitType.Knight && u.primaryTrait == TraitTag.Noble),
            UnitType.MorganLeFay => CanPickDistinct(p, r => r.unitType == UnitType.Priest, 2, r => r.primaryTrait == TraitTag.Outlaw, 1),
            UnitType.LadyOfTheLake => CountType(p, UnitType.Priest) >= 2
                                      && CountType(p, UnitType.Captain) >= 1,
            UnitType.SirGawain => CountType(p, UnitType.Warrior) >= 2
                                  && CountType(p, UnitType.Knight) >= 1,
            UnitType.Mordred => CanPickDistinct(p, r => r.unitType == UnitType.Knight, 2, r => r.primaryTrait == TraitTag.Outlaw, 1),
            UnitType.SirGalahad => CountWhere(p, r => r.unitType == UnitType.Knight
                                                     && (r.primaryTrait == TraitTag.Zealot || r.primaryTrait == TraitTag.Loyalist)) >= 1
                                   && CountType(p, UnitType.Priest) >= 1
                                   && CountType(p, UnitType.Warrior) >= 1,
            UnitType.SirTristan => CountType(p, UnitType.Knight) >= 1
                                   && CountWhere(p, r => r.unitType == UnitType.Peasant
                                                         && (r.primaryTrait == TraitTag.Outlaw || r.primaryTrait == TraitTag.Noble)) >= 1,
            UnitType.SirPercival => CountWhere(p, r => r.unitType == UnitType.Knight && r.primaryTrait == TraitTag.Zealot) >= 1
                                    && CountType(p, UnitType.Peasant) >= 1,
            UnitType.SirKay => CountType(p, UnitType.Warrior) >= 1
                               && CountType(p, UnitType.Peasant) >= 2,
            UnitType.SirBedivere => CountType(p, UnitType.Knight) >= 1
                                    && CountType(p, UnitType.Warrior) >= 1,
            UnitType.Guinevere => CountWhere(p, r => r.primaryTrait == TraitTag.Noble) >= 1
                                  && CountType(p, UnitType.Priest) >= 1,
            UnitType.LadyGuinevere => CountWhere(p, r => r.primaryTrait == TraitTag.Noble) >= 1
                                      && CountType(p, UnitType.Priest) >= 1,
            UnitType.ArchPriestOfGlastonbury => CountType(p, UnitType.Peasant) >= 2,
            UnitType.Nimue => CountType(p, UnitType.Priest) >= 2
                              && CountType(p, UnitType.Captain) >= 1,
            UnitType.TheGreenKnight => CountType(p, UnitType.Warrior) >= 2
                                       && CountWhere(p, r => r.unitType == UnitType.Knight
                                                             && r.primaryTrait == TraitTag.Loyalist) >= 1,
            _ => false
        };

        bool FuseLegacy(UnitType type, List<RecruitInstance> p) => type switch
        {
            UnitType.Priest => TryFuse(p, x => CanFuseLegacy(UnitType.Priest, x),
                () => CreateHero("unit_priest", "Town Priest", UnitType.Priest, priestSprite, 40, 9, 1.4f, TraitTag.Zealot, CombatRole.BacklineRanged),
                new IngredientPick(r => r.unitType == UnitType.Peasant, 2)),
            UnitType.Captain => TryFuse(p, x => CanFuseLegacy(UnitType.Captain, x),
                () => CreateHero("unit_captain", "Vanguard Captain", UnitType.Captain, captainSprite, 110, 16, 1.6f, TraitTag.Noble, CombatRole.FrontlineTank),
                new IngredientPick(r => r.unitType == UnitType.Warrior, 2)),
            UnitType.KingArthur => TryFuse(p, x => CanFuseLegacy(UnitType.KingArthur, x),
                () => CreateHero("hero_arthur", "King Arthur", UnitType.KingArthur, kingArthurSprite, 290, 34, 1.9f, TraitTag.Loyalist, CombatRole.FrontlineTank),
                new IngredientPick(r => r.unitType == UnitType.Knight, 2),
                new IngredientPick(r => r.unitType == UnitType.Captain, 1)),
            UnitType.Merlin => false,
            UnitType.SirLancelot => TryFuse(p, x => CanFuseLegacy(UnitType.SirLancelot, x),
                () => CreateHero("hero_lancelot", "Sir Lancelot", UnitType.SirLancelot, lancelotSprite, 240, 29, 2.0f, TraitTag.Noble, CombatRole.FrontlineTank),
                new IngredientPick(r => r.unitType == UnitType.Knight && r.primaryTrait == TraitTag.Noble, 1),
                new IngredientPick(r => r.unitType == UnitType.Knight, 1),
                new IngredientPick(r => r.unitType == UnitType.Warrior, 1)),
            UnitType.MorganLeFay => TryFuse(p, x => CanFuseLegacy(UnitType.MorganLeFay, x),
                () => CreateHero("hero_morgan", "Morgan le Fay", UnitType.MorganLeFay, morganSprite, 175, 24, 1.5f, TraitTag.Outlaw, CombatRole.BacklineRanged),
                new IngredientPick(r => r.unitType == UnitType.Priest, 2),
                new IngredientPick(r => r.primaryTrait == TraitTag.Outlaw, 1)),
            UnitType.LadyOfTheLake => TryFuse(p, x => CanFuseLegacy(UnitType.LadyOfTheLake, x),
                () => CreateHero("hero_lady_lake", "Lady of the Lake", UnitType.LadyOfTheLake, ladyOfLakeSprite, 210, 18, 1.4f, TraitTag.Zealot, CombatRole.BacklineRanged),
                new IngredientPick(r => r.unitType == UnitType.Priest, 2),
                new IngredientPick(r => r.unitType == UnitType.Captain, 1)),
            UnitType.SirGawain => TryFuse(p, x => CanFuseLegacy(UnitType.SirGawain, x),
                () => CreateHero("hero_gawain", "Sir Gawain", UnitType.SirGawain, gawainSprite, 245, 26, 1.6f, TraitTag.Loyalist, CombatRole.FrontlineTank),
                new IngredientPick(r => r.unitType == UnitType.Warrior, 2),
                new IngredientPick(r => r.unitType == UnitType.Knight, 1)),
            UnitType.Mordred => TryFuse(p, x => CanFuseLegacy(UnitType.Mordred, x),
                () => CreateHero("hero_mordred", "Mordred", UnitType.Mordred, mordredSprite, 260, 36, 1.8f, TraitTag.Outlaw, CombatRole.FrontlineTank),
                new IngredientPick(r => r.unitType == UnitType.Knight, 2),
                new IngredientPick(r => r.primaryTrait == TraitTag.Outlaw, 1)),
            UnitType.SirGalahad => TryFuse(p, x => CanFuseLegacy(UnitType.SirGalahad, x),
                () => CreateHero("hero_galahad", "Sir Galahad", UnitType.SirGalahad, galahadSprite, 270, 27, 1.7f, TraitTag.Loyalist, CombatRole.FrontlineTank),
                new IngredientPick(r => r.unitType == UnitType.Knight
                                        && (r.primaryTrait == TraitTag.Zealot || r.primaryTrait == TraitTag.Loyalist), 1),
                new IngredientPick(r => r.unitType == UnitType.Priest, 1),
                new IngredientPick(r => r.unitType == UnitType.Warrior, 1)),
            UnitType.SirTristan => TryFuse(p, x => CanFuseLegacy(UnitType.SirTristan, x),
                () =>
                {
                    var h = CreateHero("hero_tristan", "Sir Tristan", UnitType.SirTristan, tristanSprite, 185, 23, 1.8f, TraitTag.Noble, CombatRole.BacklineRanged);
                    h.canAttackFromBack = true;
                    return h;
                },
                new IngredientPick(r => r.unitType == UnitType.Knight, 1),
                new IngredientPick(r => r.unitType == UnitType.Peasant
                                        && (r.primaryTrait == TraitTag.Outlaw || r.primaryTrait == TraitTag.Noble), 1)),
            UnitType.SirPercival => TryFuse(p, x => CanFuseLegacy(UnitType.SirPercival, x),
                () => CreateHero("hero_percival", "Sir Percival", UnitType.SirPercival, percivalSprite, 200, 22, 1.6f, TraitTag.Zealot, CombatRole.FrontlineTank),
                new IngredientPick(r => r.unitType == UnitType.Knight && r.primaryTrait == TraitTag.Zealot, 1),
                new IngredientPick(r => r.unitType == UnitType.Peasant, 1)),
            UnitType.SirKay => TryFuse(p, x => CanFuseLegacy(UnitType.SirKay, x),
                () => CreateHero("hero_kay", "Sir Kay", UnitType.SirKay, kaySprite, 135, 17, 1.5f, TraitTag.Loyalist, CombatRole.FrontlineTank),
                new IngredientPick(r => r.unitType == UnitType.Warrior, 1),
                new IngredientPick(r => r.unitType == UnitType.Peasant, 2)),
            UnitType.SirBedivere => TryFuse(p, x => CanFuseLegacy(UnitType.SirBedivere, x),
                () => CreateHero("hero_bedivere", "Sir Bedivere", UnitType.SirBedivere, bedivereSprite, 190, 21, 1.7f, TraitTag.Loyalist, CombatRole.FrontlineTank),
                new IngredientPick(r => r.unitType == UnitType.Knight, 1),
                new IngredientPick(r => r.unitType == UnitType.Warrior, 1)),
            UnitType.Guinevere => TryFuse(p, x => CanFuseLegacy(UnitType.Guinevere, x),
                () => CreateHero("hero_guinevere", "Lady Guinevere", UnitType.Guinevere, guinevereSprite, 140, 12, 1.4f, TraitTag.Noble, CombatRole.BacklineRanged),
                new IngredientPick(r => r.primaryTrait == TraitTag.Noble, 1),
                new IngredientPick(r => r.unitType == UnitType.Priest, 1)),
            UnitType.LadyGuinevere => TryFuse(p, x => CanFuseLegacy(UnitType.LadyGuinevere, x),
                () => CreateHero("hero_guinevere", "Lady Guinevere", UnitType.LadyGuinevere, guinevereSprite, 140, 12, 1.4f, TraitTag.Noble, CombatRole.BacklineRanged),
                new IngredientPick(r => r.primaryTrait == TraitTag.Noble, 1),
                new IngredientPick(r => r.unitType == UnitType.Priest, 1)),
            UnitType.ArchPriestOfGlastonbury => TryFuse(p, x => CanFuseLegacy(UnitType.ArchPriestOfGlastonbury, x),
                () => CreateHero("hero_archpriest", "Arch Priest of Glastonbury", UnitType.ArchPriestOfGlastonbury, priestSprite, 55, 11, 1.4f, TraitTag.Zealot, CombatRole.BacklineRanged),
                new IngredientPick(r => r.unitType == UnitType.Peasant, 2)),
            UnitType.Nimue => TryFuse(p, x => CanFuseLegacy(UnitType.Nimue, x),
                () => CreateHero("hero_nimue", "Nimue", UnitType.Nimue, ladyOfLakeSprite, 200, 20, 1.5f, TraitTag.Zealot, CombatRole.BacklineRanged),
                new IngredientPick(r => r.unitType == UnitType.Priest, 2),
                new IngredientPick(r => r.unitType == UnitType.Captain, 1)),
            UnitType.TheGreenKnight => TryFuse(p, x => CanFuseLegacy(UnitType.TheGreenKnight, x),
                () => CreateHero("hero_green_knight", "The Green Knight", UnitType.TheGreenKnight, gawainSprite, 280, 28, 1.5f, TraitTag.Loyalist, CombatRole.FrontlineTank),
                new IngredientPick(r => r.unitType == UnitType.Warrior, 2),
                new IngredientPick(r => r.unitType == UnitType.Knight && r.primaryTrait == TraitTag.Loyalist, 1)),
            _ => false
        };

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

            ApplyLegendSparkBonus(product, chosen);
            HiddenPotentialSystem.InheritBloodline(product, chosen);

            party.Add(product);
            LegendUnlockManager.Ensure()?.NotifyFusionWithIngredients(product.unitType, chosen);
            // Phase 4 — Legend fusion micro-shake.
            StageParallaxController.MicroShake();
            Debug.Log(
                $"[DemonKnight] Fused {product.characterName}: consumed {chosen.Count} ingredient(s), party size now {party.Count}.");
            return true;
        }

        /// <summary>
        /// Phase 5: matching "Legend Spark" on ≥2 ingredients → +1 veteranRank and +15% HP/ATK.
        /// Valid fusions do NOT increment soulsLostThisRun.
        /// </summary>
        public static void ApplyLegendSparkBonus(RecruitInstance product, List<RecruitInstance> ingredients)
        {
            if (product == null || ingredients == null) return;
            int sparks = 0;
            foreach (var ing in ingredients)
            {
                if (ing == null) continue;
                HiddenPotentialSystem.MigrateLegacySpark(ing);
                if (ing.secretArchetype == SecretArchetype.Darkhorse
                    || ing.secretArchetype == SecretArchetype.GoldenEgg
                    || ing.secretArchetype == SecretArchetype.MythicVessel
                    || string.Equals(ing.hiddenTraitId, RecruitInstance.LegendSparkId, System.StringComparison.Ordinal)
                    || string.Equals(ing.hiddenTraitId, RecruitInstance.DarkhorseId, System.StringComparison.Ordinal)
                    || string.Equals(ing.hiddenTraitId, RecruitInstance.GoldenEggId, System.StringComparison.Ordinal))
                    sparks++;
            }
            if (sparks < 2) return;

            product.AddVeteranRank(1);
            product.maxHealth = Mathf.RoundToInt(product.maxHealth * 1.15f);
            product.attackDamage = Mathf.Max(1, Mathf.RoundToInt(product.attackDamage * 1.15f));
            // Floor archetype to Darkhorse if spark bonus fires without a higher bloodline inherit.
            if ((int)product.secretArchetype < (int)SecretArchetype.Darkhorse)
                product.secretArchetype = SecretArchetype.Darkhorse;
            product.isScouted = true;
            product.intelTierUnlocked = Mathf.Max(product.intelTierUnlocked, 1);
            Debug.Log(
                $"[DemonKnight] Legend Spark fuse bonus on {product.characterName}: +1★ +15% HP/ATK (sparks={sparks}).");
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
            // Persist-across-waves legends (Mordred) never expand into pew leaves.
            var db = CharacterDatabase.Load();
            if (db != null ? db.PersistsAcrossWaves(r.unitType) : r.unitType == UnitType.Mordred)
                return;
            into.Add(r);
        }

        /// <summary>Deep clone including nested fusionIngredients (sprites shared by reference).</summary>
        public static RecruitInstance CloneForFusionMemory(RecruitInstance src) =>
            CloneForFusionMemory(src, 0, null);

        static RecruitInstance CloneForFusionMemory(RecruitInstance src, int depth, HashSet<string> visited)
        {
            if (src == null) return null;
            if (depth >= 8) return null;
            visited ??= new HashSet<string>();
            if (!string.IsNullOrEmpty(src.unitID) && !visited.Add(src.unitID))
                return null; // break accidental cycles

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
                victoryStars = src.EffectiveVeteranRank,
                veteranRank = src.EffectiveVeteranRank,
                battleSlotIndex = src.battleSlotIndex,
                isDeployed = src.isDeployed,
                isScouted = src.isScouted,
                hiddenTraitId = src.hiddenTraitId,
                secretArchetype = src.secretArchetype,
                potentialMultiplier = src.potentialMultiplier,
                traitAwakened = src.traitAwakened,
                hasAscended = src.hasAscended,
                wavesSurvived = src.wavesSurvived,
                trainedHP = src.trainedHP,
                trainedATK = src.trainedATK,
                trainedSPD = src.trainedSPD,
                hireCostForcedZero = src.hireCostForcedZero,
                modularPoseId = src.modularPoseId,
                modularOutfitId = src.modularOutfitId,
                modularHeadId = src.modularHeadId,
                modularWeaponId = src.modularWeaponId,
                skinTint = src.skinTint,
                hairTint = src.hairTint,
                armorTint = src.armorTint,
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
            SanitizeBaseUnitFusionFlags(c);
            if (src.fusionIngredients != null && src.fusionIngredients.Count > 0)
            {
                c.fusionIngredients = new List<RecruitInstance>(src.fusionIngredients.Count);
                foreach (var ing in src.fusionIngredients)
                {
                    var cloned = CloneForFusionMemory(ing, depth + 1, visited);
                    if (cloned != null)
                        c.fusionIngredients.Add(cloned);
                }
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

        private RecruitInstance CreateHero(string id, string name, UnitType type, Sprite sp, int hp, int atk, float aps, TraitTag trait, CombatRole role)
        {
            // Unique id per fuse so a second Priest/Captain is not collapsed with the first.
            string uniqueId = $"{id}_{System.Guid.NewGuid().ToString("N").Substring(0, 8)}";
            return new RecruitInstance
            {
                unitID = uniqueId,
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
