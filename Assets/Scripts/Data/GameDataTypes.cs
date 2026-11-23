using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Video;

namespace ArthurianAutobattler
{
    public enum GamePhase { TownRecruit, AutoBattle, Rewards, Resurrection, PactSelect }

    public enum UnitType
    {
        Peasant, Warrior, Knight,
        Priest, Captain,
        KingArthur, Merlin, SirLancelot, MorganLeFay, LadyOfTheLake, SirGawain, Mordred, SirGalahad,
        SirTristan, SirPercival, SirKay, SirBedivere, Guinevere, LadyGuinevere,
        ArchPriestOfGlastonbury, Nimue, TheGreenKnight,
        DemonImp, DemonHellhound, DemonDreadLord, SirenEnchantress,
        BossBlackKnight, BossVortigern, BossGreenKnight, BossAccolon,
        BossMeleagant, BossKingLot, BossQuestingBeast, BossNimue
    }

    public enum TraitTag { None, Noble, Outlaw, Drunkard, Zealot, Coward, Loyalist }
    public enum CombatRole { Unassigned, FrontlineTank, BacklineRanged }
    public enum BodyPosture { EagerForward, UprightFolded, SlouchedExit }

    public enum BankRuneID { Gossip, Gambler, BloodOath, PeasantsCrown }
    public enum RelicID
    {
        None, ScabbardOfExcalibur, ChaliceOfTheGrail, MorganasBlackMirror, HornOfCamelot, CursedDiceOfCaerleon
    }
    public enum DemonicPactID { None, SilverTongue, HellfireSteel, Usurpation, EndlessHourglass }
    public enum SinRuneID { None, Jealousy, Lust }

    /// <summary>
    /// Legends / Warband list sort chips. Lives in GameDataTypes so Force Sync lands the
    /// type with early ScriptPaths (avoids CS0246 when MenuListSortBar.cs lags a Sync).
    /// </summary>
    public enum MenuListSortMode
    {
        Default = 0,
        HighestHp = 1,
        LowestHp = 2,
        HighestSpeed = 3,
        LowestSpeed = 4,
        HighestAttack = 5,
        LowestAttack = 6,
    }

    /// <summary>5-tier Hidden Potential secret archetype (TownRNG weighted roll).</summary>
    public enum SecretArchetype
    {
        Parishioner = 0,
        Liability = 1,
        Darkhorse = 2,
        GoldenEgg = 3,
        MythicVessel = 4
    }

    /// <summary>
    /// Single source of truth for hire add-ons / subtractions.
    /// UI (dossier, dialogue, mood) and payment both use <see cref="RecruitInstance.ComputeHireBreakdown"/>.
    /// </summary>
    public struct HireCostBreakdown
    {
        public int baseHire;
        public int rawGrudge;
        public int effectiveGrudge;
        public bool bloodOathApplied;
        public int bloodOathRank;
        public int writDiscount;
        public int crownCut;
        public int crownRank;
        public int jealousyTax;
        public int legendCount;
        public int starDiscount;
        public int victoryStars;
        public int tavernCut;
        public bool loyalistHalf;
        public int preFloorCost;
        public int floorApplied;
        public int finalCost;
        /// <summary>Phase 5 scout: gold shaved by 30% (min 5g) after other mods.</summary>
        public int scoutCut;
        public bool jackpotFree;

        /// <summary>Net vs base hire before Loyalist ÷2 / floor (for dialogue).</summary>
        public int NetModsBeforeLoyalistAndFloor =>
            effectiveGrudge - writDiscount - crownCut + jealousyTax - starDiscount - tavernCut;

        public string FormatDossierLines()
        {
            var sb = new System.Text.StringBuilder();
            sb.Append($"Hire: <b>{finalCost}g</b>  (base {baseHire}g");
            if (jackpotFree)
            {
                sb.Append("; Jackpot FREE");
                sb.Append(')');
                return sb.ToString();
            }
            if (bloodOathApplied)
                sb.Append($"; Blood-Oath R{bloodOathRank} −{bloodOathRank * 5}g (waived +{rawGrudge}g grudge)");
            else if (rawGrudge != 0)
                sb.Append(rawGrudge > 0 ? $"; grudge +{rawGrudge}g" : $"; win −{-rawGrudge}g");
            if (writDiscount > 0) sb.Append($"; Writ −{writDiscount}g");
            if (crownCut > 0) sb.Append($"; Crown R{crownRank} −{crownCut}g");
            if (jealousyTax > 0) sb.Append($"; Jealousy +{jealousyTax}g ({legendCount} legend{(legendCount == 1 ? "" : "s")})");
            if (starDiscount > 0) sb.Append($"; ★×{victoryStars} −{starDiscount}g");
            if (tavernCut > 0) sb.Append($"; Tavern −{tavernCut}g");
            if (loyalistHalf) sb.Append("; Loyalist ÷2");
            if (floorApplied > 0 && finalCost == floorApplied && preFloorCost < floorApplied)
                sb.Append($"; floor ≥{floorApplied}g");
            if (scoutCut > 0) sb.Append($"; Scout −30% (−{scoutCut}g, min {RecruitInstance.ScoutHireMinGold}g)");
            sb.Append(')');
            return sb.ToString();
        }
    }

    [Serializable]
    public class RecruitInstance
    {
        public string unitID;
        public string characterName;
        public UnitType unitType;
        public Sprite randomAvatar;
        public CombatRole assignedRole = CombatRole.Unassigned;
        public int maxHealth;
        public int attackDamage;
        public float attackSpeed;
        public int baseHireCost;
        public TraitTag primaryTrait;
        public TraitTag hatedTrait;
        public TraitTag lovedTrait;
        public int intelTierUnlocked = 0;

        /// <summary>Persistent seat in the Somerset church (0–51).</summary>
        public int pewSeatIndex = 0;

        /// <summary>Battle formation slot (0–51) on StageLayoutProfile.playerBattleSlots.</summary>
        public int battleSlotIndex;

        /// <summary>When false, unit stays in pew reserve and does not march.</summary>
        public bool isDeployed = true;

        /// <summary>Intel / gossip scouted — reveals Potential, archetype, trait + hire discount.</summary>
        public bool isScouted;

        /// <summary>Combat trait id (MartyrsWard, SleeperSpike, PendragonBlood, …).</summary>
        public string hiddenTraitId;

        /// <summary>5-tier secret archetype from TownRNG.</summary>
        public SecretArchetype secretArchetype = SecretArchetype.Parishioner;

        /// <summary>Growth multiplier applied to trained + veteran-star pools in combat.</summary>
        public float potentialMultiplier = 1f;

        /// <summary>Baptism by Fire — trait first awoke in combat (or on scout for unlocks).</summary>
        public bool traitAwakened;

        /// <summary>3-wave ascension consumed (+25% base HP/ATK/SPD).</summary>
        public bool hasAscended;

        /// <summary>Wave victories survived while recruited (ascension counter).</summary>
        public int wavesSurvived;

        /// <summary>Paid Train bonuses (+20 per purchase into one chosen pool).</summary>
        public int trainedHP;
        public int trainedATK;
        public int trainedSPD;

        /// <summary>
        /// Wave number of the last successful Train purchase on this unit (−1 = never).
        /// One Train (ATK / HP / SPD) per unit per wave.
        /// </summary>
        public int lastTrainWaveNumber = -1;

        /// <summary>Dice Jackpot — hire is free regardless of floors / mods.</summary>
        public bool hireCostForcedZero;

        /// <summary>Legacy Phase 5 spark id (Darkhorse-class) — migrated to SecretArchetype.</summary>
        public const string LegendSparkId = "Legend Spark";
        /// <summary>Hidden Potential — Percival unlock when Darkhorse awakened/ascended.</summary>
        public const string DarkhorseId = "Darkhorse";
        /// <summary>Hidden Potential — GoldenEgg (Merlin no longer unlocks; narrative-only).</summary>
        public const string GoldenEggId = "Golden Egg";
        /// <summary>Hidden Potential — Nimue unlock when GrailTouched fused (MythicVessel trait).</summary>
        public const string GrailTouchedId = "GrailTouched";
        public const float ScoutHireMult = 0.7f;
        public const int ScoutHireMinGold = 5;

        public BodyPosture bodyPosture = BodyPosture.UprightFolded;
        public Sprite bodySprite;
        public Sprite headSprite;
        public Sprite moodOverlaySprite;
        public bool isSirenEnchantress = false;

        /// <summary>
        /// Modular pew/dossier look (deterministic seed). When set, PaperDoll + portrait
        /// resolve through ModularSpriteLibrary instead of randomAvatar alone.
        /// </summary>
        public TownsfolkAppearance appearance;

        /// <summary>Modular part ids (string form of library indices / sheet keys).</summary>
        public string modularPoseId;
        public string modularOutfitId;
        public string modularHeadId;
        public string modularWeaponId;

        /// <summary>Paper-doll tint overrides (a≤0 → resolve from appearance / library).</summary>
        public Color skinTint;
        public Color hairTint;
        public Color armorTint;

        /// <summary>
        /// Wave-victory experience rank (Master Spec). Serialized. Stacks; each → −5g hire.
        /// </summary>
        public int veteranRank;

        /// <summary>
        /// Legacy alias kept for hire/JSON compat. Prefer <see cref="EffectiveVeteranRank"/>.
        /// Synced with veteranRank on award / load.
        /// </summary>
        public int victoryStars;

        /// <summary>Max(veteranRank, victoryStars) — single source for ★ UI + hire discount.</summary>
        public int EffectiveVeteranRank => Mathf.Max(0, Mathf.Max(veteranRank, victoryStars));

        /// <summary>
        /// Train Ranged equips a crossbow — Knights (and other non-Warrior melee) may
        /// attack from BACK (no NO REACH). Warriors never receive this kit.
        /// </summary>
        public bool canAttackFromBack;

        /// <summary>Hire discount gold per victory star.</summary>
        public const int VictoryStarHireDiscount = 5;

        /// <summary>Align veteranRank ↔ victoryStars after load or partial writes.</summary>
        public void SyncVeteranRankFields()
        {
            int e = EffectiveVeteranRank;
            veteranRank = e;
            victoryStars = e;
        }

        /// <summary>WaveVictory award — increments both rank fields together.</summary>
        public void AddVeteranRank(int delta = 1)
        {
            SyncVeteranRankFields();
            veteranRank = Mathf.Max(0, veteranRank) + Mathf.Max(0, delta);
            victoryStars = veteranRank;
        }

        /// <summary>Copy modular ids + tints from appearance (spawn / rebind).</summary>
        public void SyncModularIdsFromAppearance()
        {
            if (appearance == null) return;
            modularPoseId = appearance.poseIndex.ToString();
            modularOutfitId = appearance.outfitIndex.ToString();
            modularHeadId = appearance.headIndex.ToString();
            modularWeaponId = appearance.propIndex.ToString();
            if (skinTint.a <= 0.01f && appearance.skinToneIndex >= 0
                && appearance.skinToneIndex < TownsfolkAppearance.SkinTones.Length)
                skinTint = TownsfolkAppearance.SkinTones[appearance.skinToneIndex];
            if (hairTint.a <= 0.01f && appearance.hairColorIndex >= 0
                && appearance.hairColorIndex < TownsfolkAppearance.HairColors.Length)
                hairTint = TownsfolkAppearance.HairColors[appearance.hairColorIndex];
            if (armorTint.a <= 0.01f)
                armorTint = Color.white;
        }

        /// <summary>Base troop types that must never carry fusion memory.</summary>
        public static bool IsBaseTroopType(UnitType t) =>
            t == UnitType.Peasant || t == UnitType.Warrior || t == UnitType.Knight;

        /// <summary>Balance V2 hire bands.</summary>
        public const int PeasantHireFloor = 10;
        public const int PeasantHireCeil = 15;
        public const int WarriorHireFloor = 35;
        public const int WarriorHireCeil = 55;
        public const int KnightHireFloor = 110;
        public const int KnightHireCeil = 160;

        /// <summary>Intel costs: Scout 10g, Investigate 20g, Full Report 30g.</summary>
        public const int IntelTier1Cost = 10;
        public const int IntelTier2Cost = 20;
        public const int IntelTier3Cost = 30;

        /// <summary>Paid Train (ATK / HP / SPD) — 50g, +20 to the chosen pool, once per unit per wave.</summary>
        public const int TrainStatCostGold = 50;
        public const int TrainStatBonus = 20;

        public static int IntelCostForTier(int tier) => tier switch
        {
            1 => IntelTier1Cost,
            2 => IntelTier2Cost,
            3 => IntelTier3Cost,
            _ => 0
        };

        /// <summary>
        /// Pre-fusion ingredient snapshots (deep). Wave-end disband expands these back
        /// into pew seats so fused Legends never occupy seats or block return.
        /// Must be NonSerialized — List&lt;RecruitInstance&gt; inside RecruitInstance is a Unity
        /// serialization depth cycle (limit 10) via selected/recruitedParty/PaperDoll.
        /// Persistence uses flattened WarbandAutosave fusionParts, not this field.
        /// </summary>
        [System.NonSerialized]
        public List<RecruitInstance> fusionIngredients;

        public bool IsFusedLegend =>
            fusionIngredients != null && fusionIngredients.Count > 0;

        public int GetFinalHireCost(int grudgePenalty, int globalDiscount = 0) =>
            ComputeHireBreakdown(grudgePenalty, globalDiscount).finalCost;

        /// <summary>
        /// Compute every hire add-on / subtraction once. Dossier UI and payment share this.
        /// </summary>
        public HireCostBreakdown ComputeHireBreakdown(int grudgePenalty, int globalDiscount = 0)
        {
            var b = new HireCostBreakdown
            {
                baseHire = baseHireCost,
                rawGrudge = grudgePenalty,
                writDiscount = Mathf.Max(0, globalDiscount),
                victoryStars = EffectiveVeteranRank,
                loyalistHalf = primaryTrait == TraitTag.Loyalist,
                floorApplied = HireFloorFor(unitType)
            };

            b.bloodOathRank = SomersetRunes.GetRank(BankRuneID.BloodOath);
            b.bloodOathApplied = b.bloodOathRank > 0
                && (primaryTrait == TraitTag.Outlaw || primaryTrait == TraitTag.Coward)
                && grudgePenalty > 0;
            // Blood-Oath: replace positive grudge with a fixed discount (−5g × rank).
            b.effectiveGrudge = b.bloodOathApplied
                ? -(b.bloodOathRank * 5)
                : grudgePenalty;

            b.crownRank = SomersetRunes.GetRank(BankRuneID.PeasantsCrown);
            if (unitType == UnitType.Peasant)
                b.crownCut = b.crownRank * 2;

            if (SomersetRunes.IsSinActive(SinRuneID.Jealousy) && GameLoopManager.Instance != null)
            {
                foreach (var m in GameLoopManager.Instance.recruitedParty)
                {
                    if (m != null && IsLegendUnit(m.unitType)) b.legendCount++;
                }
                b.jealousyTax = b.legendCount * 10;
            }

            b.starDiscount = b.victoryStars * VictoryStarHireDiscount;

            if (GameLoopManager.Instance != null && GameLoopManager.Instance.tavernRoundHireDiscountActive)
                b.tavernCut = GameLoopManager.TavernRoundHireDiscount;

            if (hireCostForcedZero)
            {
                b.jackpotFree = true;
                b.preFloorCost = 0;
                b.finalCost = 0;
                return b;
            }

            int cost = Mathf.Max(1,
                b.baseHire + b.effectiveGrudge - b.writDiscount - b.crownCut
                + b.jealousyTax - b.starDiscount - b.tavernCut);
            if (b.loyalistHalf) cost = Mathf.Max(1, cost / 2);
            b.preFloorCost = cost;
            // Tier floors after ALL modifiers so Loyalist / Crown / Writ / Tavern
            // cannot undercut band floors on Fresh New Game townsfolk.
            b.finalCost = Mathf.Max(cost, b.floorApplied);

            // Phase 5: scouted → −30% hire (min 5g), applied after floors.
            if (isScouted)
            {
                int afterScout = Mathf.Max(ScoutHireMinGold, Mathf.RoundToInt(b.finalCost * ScoutHireMult));
                b.scoutCut = Mathf.Max(0, b.finalCost - afterScout);
                b.finalCost = afterScout;
            }
            return b;
        }

        /// <summary>Minimum displayed/paid hire after modifiers (matches initial hire bands).</summary>
        public static int HireFloorFor(UnitType type) => type switch
        {
            UnitType.Peasant => PeasantHireFloor,
            UnitType.Warrior => WarriorHireFloor,
            UnitType.Knight => KnightHireFloor,
            _ => 1
        };

        public static bool IsLegendUnit(UnitType t)
        {
            return t == UnitType.KingArthur || t == UnitType.Merlin || t == UnitType.SirLancelot
                   || t == UnitType.MorganLeFay || t == UnitType.LadyOfTheLake || t == UnitType.SirGawain
                   || t == UnitType.Mordred || t == UnitType.SirGalahad || t == UnitType.Priest
                   || t == UnitType.Captain || t == UnitType.SirTristan || t == UnitType.SirPercival
                   || t == UnitType.SirKay || t == UnitType.SirBedivere || t == UnitType.Guinevere
                   || t == UnitType.LadyGuinevere || t == UnitType.ArchPriestOfGlastonbury
                   || t == UnitType.Nimue || t == UnitType.TheGreenKnight;
        }

        /// <summary>
        /// Warriors may sit in BACK (formation) but cannot Train Ranged / gain a crossbow.
        /// Knights and other non-Warrior types still may.
        /// </summary>
        public static bool CanTrainRangedKit(UnitType t) => t != UnitType.Warrior;

        /// <summary>True when this recruit may buy Train Ranged (crossbow kit).</summary>
        public bool AllowsTrainRangedKit => CanTrainRangedKit(unitType);

        /// <summary>
        /// Formation only — move FRONT/BACK without kit, gold, or stat training.
        /// Warriors may use this to sit in BACK (they keep NO REACH without a kit).
        /// </summary>
        public void SetFormationRole(CombatRole newRole)
        {
            if (newRole == CombatRole.Unassigned) return;
            assignedRole = newRole;
            // Strip illegal Warrior crossbow kits from older saves / UI paths.
            if (unitType == UnitType.Warrior)
                canAttackFromBack = false;
        }

        /// <summary>Which combat pool a paid Train purchase boosts.</summary>
        public enum TrainStatKind { Attack, HP, Speed }

        /// <summary>True when this unit already bought Train this wave (one purchase max).</summary>
        public bool HasTrainedThisWave(int waveNumber) =>
            waveNumber > 0 && lastTrainWaveNumber == waveNumber;

        /// <summary>
        /// Paid Train: +20 to the chosen ATK / HP / SPD pool. One purchase per unit per wave.
        /// Does not grant Train Ranged / canAttackFromBack (Warriors keep NO REACH in BACK).
        /// </summary>
        public bool TrainChosenStat(TrainStatKind kind, int waveNumber, out string bonusLabel)
        {
            bonusLabel = null;
            if (waveNumber > 0 && HasTrainedThisWave(waveNumber)) return false;
            switch (kind)
            {
                case TrainStatKind.HP:
                    trainedHP += TrainStatBonus;
                    bonusLabel = $"+{TrainStatBonus} HP";
                    break;
                case TrainStatKind.Attack:
                    trainedATK += TrainStatBonus;
                    bonusLabel = $"+{TrainStatBonus} ATK";
                    break;
                case TrainStatKind.Speed:
                    trainedSPD += TrainStatBonus;
                    bonusLabel = $"+{TrainStatBonus} SPD";
                    break;
                default:
                    return false;
            }
            if (waveNumber > 0) lastTrainWaveNumber = waveNumber;
            return true;
        }

        /// <summary>
        /// Legacy Tank / Ranged training (removed from church UI). Kept for old saves/callers.
        /// Warriors are blocked from the ranged kit — use SetFormationRole to move them BACK.
        /// Prefer <see cref="TrainChosenStat"/> for new Train purchases.
        /// </summary>
        public bool TrainRole(CombatRole newRole) => TrainRole(newRole, out _);

        /// <param name="bonusLabel">e.g. "+20 HP" when the purchase succeeds.</param>
        public bool TrainRole(CombatRole newRole, out string bonusLabel)
        {
            bonusLabel = null;
            if (newRole == CombatRole.BacklineRanged)
            {
                if (!CanTrainRangedKit(unitType)) return false;
                // Already BACK with kit — nothing to spend.
                if (assignedRole == CombatRole.BacklineRanged && canAttackFromBack) return false;
                assignedRole = CombatRole.BacklineRanged;
                if (!canAttackFromBack)
                {
                    attackDamage = Mathf.RoundToInt(attackDamage * 1.20f);
                    canAttackFromBack = true;
                }
                bonusLabel = ApplyRandomTrainStatBonus();
                return true;
            }

            if (assignedRole == newRole) return false;
            assignedRole = newRole;
            if (newRole == CombatRole.FrontlineTank)
                maxHealth = Mathf.RoundToInt(maxHealth * 1.25f);
            bonusLabel = ApplyRandomTrainStatBonus();
            return true;
        }

        /// <summary>
        /// Legacy random roll (+2). New church Train uses <see cref="TrainChosenStat"/> (+20 chosen).
        /// SPD is stored as attacks/sec — +2 SPD = +0.2 APS (0.1 APS per SPD point).
        /// </summary>
        public string ApplyRandomTrainStatBonus()
        {
            // Only trained* pools — combat applies EffectiveGrowth = Round((trained+stars)*mult).
            int roll = UnityEngine.Random.Range(0, 3);
            switch (roll)
            {
                case 0:
                    trainedHP += 2;
                    return "+2 HP";
                case 1:
                    trainedATK += 2;
                    return "+2 ATK";
                default:
                    trainedSPD += 2;
                    return "+2 SPD";
            }
        }

        /// <summary>Effective combat HP after trained+star growth × potentialMultiplier.</summary>
        public int GetEffectiveMaxHealth()
        {
            int g = HiddenPotentialSystem.EffectiveGrowth(trainedHP, EffectiveVeteranRank, potentialMultiplier);
            return Mathf.Max(1, maxHealth + g);
        }

        public int GetEffectiveAttackDamage()
        {
            int g = HiddenPotentialSystem.EffectiveGrowth(trainedATK, EffectiveVeteranRank, potentialMultiplier);
            return Mathf.Max(1, attackDamage + g);
        }

        public float GetEffectiveAttackSpeed()
        {
            int g = HiddenPotentialSystem.EffectiveGrowth(trainedSPD, EffectiveVeteranRank, potentialMultiplier);
            return Mathf.Clamp(attackSpeed + g * 0.1f, 0.4f, 3.0f);
        }

        /// <summary>
        /// Naturally ranged / caster types that fight from BACK without a crossbow kit.
        /// </summary>
        public static bool IsRangedUnitType(UnitType t) => t switch
        {
            UnitType.Peasant => true,
            UnitType.Priest => true,
            UnitType.Merlin => true,
            UnitType.MorganLeFay => true,
            UnitType.LadyOfTheLake => true,
            UnitType.SirTristan => true,
            UnitType.Guinevere => true,
            UnitType.LadyGuinevere => true,
            UnitType.ArchPriestOfGlastonbury => true,
            UnitType.Nimue => true,
            UnitType.SirenEnchantress => true,
            _ => false
        };

        /// <summary>Can fight from BACK: native ranged OR Train Ranged crossbow kit (never Warriors).</summary>
        public bool CanFightFromBackRow =>
            (canAttackFromBack && unitType != UnitType.Warrior) || IsRangedUnitType(unitType);

        /// <summary>True when BACK without ranged kit — intentional for Warriors in BACK.</summary>
        public bool IsMeleeStuckInBackRow =>
            assignedRole == CombatRole.BacklineRanged && !CanFightFromBackRow;

        public void RefreshBodyPostureFromTraits(int grudgePenalty)
        {
            if (primaryTrait == TraitTag.Loyalist)
                bodyPosture = BodyPosture.EagerForward;
            else if (primaryTrait == TraitTag.Coward || grudgePenalty > 0)
                bodyPosture = BodyPosture.SlouchedExit;
            else
                bodyPosture = BodyPosture.UprightFolded;
        }

        public string GetDisplayDescription()
        {
            int grudge = GrudgeSaveManager.Instance != null
                ? GrudgeSaveManager.Instance.GetGrudgeCostPenalty(unitID) : 0;
            int writ = MerlinResurrectionManager.Instance != null
                ? MerlinResurrectionManager.Instance.GetGlobalCostDiscount() : 0;
            return GetDisplayDescription(grudge, writ);
        }

        public string GetDisplayDescription(int grudgePenalty, int globalDiscount)
        {
            var hire = ComputeHireBreakdown(grudgePenalty, globalDiscount);

            string info = $"<b>{characterName}</b> ({unitType}) | Pew {PewIndex + 1}/12 (seat {pewSeatIndex}) | Role: <b>{assignedRole}</b>\n";
            if (IsMeleeStuckInBackRow)
            {
                info += unitType == UnitType.Warrior
                    ? "<color=#FF6644><b>NO REACH</b> — Warriors cannot attack from BACK. Move to FRONT.</color>\n"
                    : "<color=#FF6644><b>NO REACH</b> — melee in BACK cannot attack. Move to FRONT.</color>\n";
            }
            else if (assignedRole == CombatRole.BacklineRanged && canAttackFromBack
                     && unitType != UnitType.Warrior && !IsRangedUnitType(unitType))
                info += "<color=#B8E986>Crossbow kit — can attack from BACK</color>\n";
            if (isSirenEnchantress)
            {
                info += "<color=#FF69B4>Siren Enchantress</color>";
                if (SomersetRunes.IsSinActive(SinRuneID.Lust))
                    info += " <color=#FF69B4>(Lust: −15g per marched Siren on win)</color>";
                info += "\n";
            }

            if (intelTierUnlocked >= 1 || isScouted)
            {
                info += $"HP: {GetEffectiveMaxHealth()} | ATK: {GetEffectiveAttackDamage()} | SPD: {GetEffectiveAttackSpeed():F1}/s\n";
                if (trainedHP + trainedATK + trainedSPD > 0 || EffectiveVeteranRank > 0)
                    info += $"<color=#B8E986>(base {maxHealth}/{attackDamage}/{attackSpeed:F1} + growth)</color>\n";
            }
            else
                info += $"HP: ??? | ATK: ??? (Scout {IntelTier1Cost}g)\n";

            if (isScouted)
            {
                info += $"<color=#FFD700>Potential: ×{potentialMultiplier:F2} ({secretArchetype})</color>\n";
                if (!string.IsNullOrEmpty(hiddenTraitId))
                    info += $"<color=#FFD700>Trait: {hiddenTraitId}</color>"
                            + (traitAwakened ? " <color=#FFE066>[AWAKENED]</color>" : "")
                            + (hasAscended ? " <color=#FFAA66>[ASCENDED]</color>" : "")
                            + "\n";
                if (trainedHP + trainedATK + trainedSPD > 0)
                    info += $"Trained: HP+{trainedHP} ATK+{trainedATK} SPD+{trainedSPD}\n";
            }
            else
            {
                info += "Potential: ??? (Scout to reveal)\n";
                info += "Trait: ???\n";
            }

            // Hire add-ons / subtractions — same math as Recruit (Ng).
            info += $"<color=#FFE066>{hire.FormatDossierLines()}</color>\n";

            // Battle-only perk / rune effects — only after Scout so HP/ATK deltas do not leak.
            if (intelTierUnlocked >= 1 || isScouted)
                info += FormatBattleBonusLines();

            if (intelTierUnlocked >= 2)
                info += $"<color=#FF5555>Rival: Hates {hatedTrait}</color>\n";
            else
                info += $"Disadvantages: ??? (Investigate {IntelTier2Cost}g)\n";

            if (intelTierUnlocked >= 3)
                info += $"<color=#55FF55>Trait: {primaryTrait} | Loves: {lovedTrait}</color>";
            else
                info += $"Full Potential: ??? (Full Report {IntelTier3Cost}g)";

            return info;
        }

        /// <summary>Combat perk / rune / sin lines so dossier matches AutoBattle apply.</summary>
        public string FormatBattleBonusLines()
        {
            var lines = new System.Text.StringBuilder();
            var perks = PerkDraftManager.Instance;
            if (perks != null)
            {
                if (perks.unlockedPerks.Contains(RunPerkID.PitchforkMilitia) && unitType == UnitType.Peasant)
                    lines.AppendLine("<color=#9AD1FF>Pitchfork (battle): +10 HP, +2 ATK</color>");
                if (perks.unlockedPerks.Contains(RunPerkID.HasteOfCamelot)
                    && (unitType == UnitType.Warrior || unitType == UnitType.Knight))
                    lines.AppendLine("<color=#9AD1FF>Haste (battle): +0.25 APS (cap 2.0)</color>");
            }

            int crown = SomersetRunes.GetRank(BankRuneID.PeasantsCrown);
            if (crown > 0 && unitType == UnitType.Peasant && GameLoopManager.Instance != null)
            {
                int peasants = 0;
                foreach (var m in GameLoopManager.Instance.recruitedParty)
                    if (m != null && m.unitType == UnitType.Peasant) peasants++;
                // Include this recruit if hired into an empty party preview: show formula.
                int stacks = peasants / 5;
                float aps = 0.15f * stacks * crown;
                lines.AppendLine(stacks > 0
                    ? $"<color=#B8E986>Crown (battle): +{aps:F2} APS (⌊{peasants}/5⌋×0.15×R{crown}, cap 2.0)</color>"
                    : $"<color=#B8E986>Crown (battle): +0.15 APS ×⌊peasants/5⌋×R{crown} (need 5+ peasants)</color>");
            }

            if (SomersetRunes.IsSinActive(SinRuneID.Jealousy) && !IsLegendUnit(unitType))
                lines.AppendLine("<color=#FF9A9A>Jealousy (battle): −3 ATK</color>");

            return lines.ToString();
        }

        /// <summary>Dialogue that matches effective hire delta (Blood-Oath aware).</summary>
        public string FormatHireDialogue(int grudgePenalty, int globalDiscount = 0)
        {
            var h = ComputeHireBreakdown(grudgePenalty, globalDiscount);
            if (h.bloodOathApplied)
            {
                return $"\"Blood-Oath burned my bury-gold (+{h.rawGrudge}g). Hire is −{h.bloodOathRank * 5}g instead.\"";
            }
            if (h.rawGrudge > 0)
                return $"\"I marched with you and fell. Bury-gold raised my hire by {h.rawGrudge}g.\"";
            if (h.rawGrudge < 0)
                return $"\"Victory softens the ledger — hire is { -h.rawGrudge}g cheaper.\"";
            if (primaryTrait == TraitTag.Loyalist)
                return "\"For King Arthur! My blade is yours at half price.\"";
            if (primaryTrait == TraitTag.Coward)
                return "\"I'm no hero... unless you pay to train me or steel my nerves.\"";
            return "\"Demons claw the churchyard. Show gold — or a plan.\"";
        }

        /// <summary>Mood line using effective hire delta, not raw grudge.</summary>
        public string FormatMoodLabel(int grudgePenalty, int globalDiscount = 0)
        {
            var h = ComputeHireBreakdown(grudgePenalty, globalDiscount);
            if (h.bloodOathApplied)
                return $"Blood-Oath (−{h.bloodOathRank * 5}g hire; waived +{h.rawGrudge}g)";
            if (h.effectiveGrudge > 0) return $"Grudging (+{h.effectiveGrudge}g hire)";
            if (h.effectiveGrudge < 0) return $"Favored ({h.effectiveGrudge}g hire)";
            if (primaryTrait == TraitTag.Loyalist) return "Eager (Loyalist ÷2 hire)";
            if (primaryTrait == TraitTag.Coward) return "Fearful (Coward)";
            if (primaryTrait == TraitTag.Drunkard) return "Merry (Drunkard)";
            if (primaryTrait == TraitTag.Zealot) return "Fervent (Zealot)";
            if (bodyPosture == BodyPosture.EagerForward) return "Eager";
            if (bodyPosture == BodyPosture.SlouchedExit) return "Uneasy";
            return "Steady";
        }

        /// <summary>Pew index 0–11 on the 3×4 church art grid.</summary>
        public int PewIndex => SomersetPewLayout.GetStaticPewIndex(pewSeatIndex);

        /// <summary>Pew row 0–3 (front→altar) used by Gossip Rune adjacency.</summary>
        public int PewRow => SomersetPewLayout.GetStaticPewRow(pewSeatIndex);
    }

    [Serializable]
    public class CombatUnit
    {
        public string unitID;
        public string unitName;
        public UnitType unitType;
        public CombatRole assignedRole;
        public bool isDemon;
        public bool isUnorganized;
        public bool isSirenEnchantress;
        public TraitTag primaryTrait;
        public int powerValue;
        public int maxHealth;
        public int currentHealth;
        public int shieldHealth;
        public int attackDamage;
        public float attacksPerSecond;
        public float attackTimer;
        public bool hasRevived = false;
        public bool isHexed = false;
        public bool scabbardUsed = false;
        public float battleElapsed = 0f;
        public float petrifiedTimer = 0f;
        public float charmedTimer = 0f;
        public bool cannotBeHealed = false;
        public float maxHpDrainPerSecond = 0f;
        /// <summary>Nimue / MartyrsWard trait — survive first lethal at 1 HP.</summary>
        public bool martyrsWard;
        public bool martyrsWardUsed;
        /// <summary>Secret combat trait id mirrored from RecruitInstance.hiddenTraitId.</summary>
        public string hiddenTraitId;
        public SecretArchetype secretArchetype;
        public bool traitAwakened;
        public bool sleeperSpikeArmed;
        public int sleeperSpikeHits;
        public bool gluttonHealTick;
        /// <summary>Train Ranged crossbow — allows melee to fire from BACK.</summary>
        public bool canAttackFromBack;

        /// <summary>Formation slot 0..51 — StageLayoutProfile.playerBattleSlots / line-pierce column.</summary>
        public int battleSlotIndex;

        public bool IsAlive => currentHealth > 0;

        /// <summary>
        /// Battalion FRONT: Tank or Unassigned. BACK is Ranged only.
        /// Demons must kill/clear FRONT before they can hit BACK.
        /// </summary>
        public bool IsFrontRow => assignedRole != CombatRole.BacklineRanged;

        /// <summary>Battalion BACK: trained Ranged formation.</summary>
        public bool IsBackRow => assignedRole == CombatRole.BacklineRanged;

        public bool CanFightFromBackRow =>
            (canAttackFromBack && unitType != UnitType.Warrior)
            || RecruitInstance.IsRangedUnitType(unitType);

        /// <summary>
        /// BACK row fires for ranged types or crossbow-equipped melee (not Warriors).
        /// </summary>
        public bool CanAttackFromCurrentRow =>
            isDemon || IsFrontRow || CanFightFromBackRow;

        /// <summary>Melee in BACK without crossbow — cannot reach (Warriors always if BACK).</summary>
        public bool IsMeleeStuckInBackRow =>
            !isDemon && IsBackRow && !CanFightFromBackRow;

        public void ResetAttackCooldown()
        {
            float effectiveAPS = isHexed ? attacksPerSecond * 0.5f : attacksPerSecond;
            attackTimer = 1f / Mathf.Clamp(effectiveAPS, 0.4f, 3.0f);
        }

        public static bool IsImmuneToSirenCharm(UnitType t)
        {
            return t == UnitType.MorganLeFay || t == UnitType.LadyOfTheLake
                   || t == UnitType.Merlin || t == UnitType.SirGalahad;
        }

        public static bool IsMaleFrontlineCharmable(UnitType t)
        {
            return t == UnitType.Peasant || t == UnitType.Warrior || t == UnitType.Knight
                   || t == UnitType.SirLancelot || t == UnitType.SirGawain;
        }
    }

    public static class PartySynergyEvaluator
    {
        /// <summary>True after Mediate (100g) until the current wave ends.</summary>
        public static bool RivalriesSuspendedThisWave =>
            GameLoopManager.Instance != null && GameLoopManager.Instance.rivalriesSuspendedThisWave;

        public static bool CanJoinParty(RecruitInstance candidate, List<RecruitInstance> currentParty, out string refusalReason)
        {
            if (SomersetRunes.IsPact(DemonicPactID.Usurpation))
            {
                if (candidate.unitType == UnitType.KingArthur
                    || candidate.unitType == UnitType.SirGalahad
                    || candidate.unitType == UnitType.Merlin
                    || candidate.primaryTrait == TraitTag.Loyalist)
                {
                    refusalReason = "The Pact of Usurpation bars Arthur, Galahad, Merlin, and all Loyalists.";
                    return false;
                }
            }

            // Mediate cancels recruitment rivalries for this wave (Noble ↔ Outlaw, etc.).
            if (!RivalriesSuspendedThisWave && currentParty != null)
            {
                foreach (var member in currentParty)
                {
                    if (member == null) continue;
                    if (AreTraitRivals(candidate, member))
                    {
                        refusalReason =
                            $"{candidate.characterName} refuses to serve alongside {member.characterName}! (Mediate for {GameLoopManager.MediateCostGold}g to cancel rivalries this wave.)";
                        return false;
                    }
                }
            }

            refusalReason = string.Empty;
            return true;
        }

        /// <summary>Noble refuses Outlaw (and vice versa) unless Mediate suspended rivalries.</summary>
        public static bool AreTraitRivals(RecruitInstance a, RecruitInstance b)
        {
            if (a == null || b == null) return false;
            bool aNoble = a.primaryTrait == TraitTag.Noble;
            bool bNoble = b.primaryTrait == TraitTag.Noble;
            bool aOut = a.primaryTrait == TraitTag.Outlaw;
            bool bOut = b.primaryTrait == TraitTag.Outlaw;
            return (aNoble && bOut) || (aOut && bNoble);
        }

        public static void ApplyBattleModifiers(List<RecruitInstance> party)
        {
            bool arthurPresent = false;
            bool guineverePresent = false;
            int peasantCount = 0;
            foreach (var member in party)
            {
                if (member.unitType == UnitType.KingArthur) arthurPresent = true;
                if (member.unitType == UnitType.Guinevere) guineverePresent = true;
                if (member.unitType == UnitType.Peasant) peasantCount++;
            }

            float peasantApsBonus = 0f;
            int crownRank = SomersetRunes.GetRank(BankRuneID.PeasantsCrown);
            if (crownRank > 0 && peasantCount >= 5)
                peasantApsBonus = 0.15f * (peasantCount / 5) * crownRank;

            bool jealousy = SomersetRunes.IsSinActive(SinRuneID.Jealousy);
            bool rivalriesOff = RivalriesSuspendedThisWave;

            foreach (var unit in party)
            {
                if (peasantApsBonus > 0f && unit.unitType == UnitType.Peasant)
                    unit.attackSpeed = Mathf.Min(2.0f, unit.attackSpeed + peasantApsBonus);

                if (jealousy && !RecruitInstance.IsLegendUnit(unit.unitType))
                    unit.attackDamage = Mathf.Max(1, unit.attackDamage - 3);

                // Guinevere: +15% ATK aura to Loyalist / Noble allies (not herself).
                if (guineverePresent
                    && unit.unitType != UnitType.Guinevere
                    && (unit.primaryTrait == TraitTag.Loyalist || unit.primaryTrait == TraitTag.Noble))
                {
                    unit.attackDamage = Mathf.Max(1, Mathf.RoundToInt(unit.attackDamage * 1.15f));
                }

                foreach (var ally in party)
                {
                    if (unit == ally) continue;
                    // Hated-trait rivalry ATK penalty — skipped when Mediate suspended this wave.
                    if (!rivalriesOff
                        && !arthurPresent
                        && unit.hatedTrait != TraitTag.None
                        && ally.primaryTrait == unit.hatedTrait)
                    {
                        unit.attackDamage = Mathf.Max(1, unit.attackDamage - 2);
                    }

                    if (unit.lovedTrait != TraitTag.None && ally.primaryTrait == unit.lovedTrait)
                    {
                        unit.attackDamage += 3;
                        unit.maxHealth += 10;
                    }
                }
            }
        }
    }

    /// <summary>Phase 5 — Intel purchase marks scouted + gossip-adjacent pew neighbors.</summary>
    public static class IntelGossip
    {
        /// <summary>
        /// Same as StageLayoutProfile.PewRowSize (13). Local const so GameDataTypes
        /// compiles even if StageLayoutProfile.cs is missing mid-Force-Sync (CS0103).
        /// </summary>
        public const int PewRowSize = 13;

        public static void ApplyIntelPurchase(RecruitInstance target, int tier, List<RecruitInstance> townPool)
        {
            if (target == null) return;
            target.intelTierUnlocked = Mathf.Max(target.intelTierUnlocked, tier);
            MarkScouted(target);
            GrudgeSaveManager.Instance?.RememberIntel(target.unitID, target.intelTierUnlocked);
            GossipAdjacentSameRow(target, townPool);
        }

        public static void MarkScouted(RecruitInstance r)
        {
            if (r == null) return;
            bool wasScouted = r.isScouted;
            r.isScouted = true;
            if (r.intelTierUnlocked < 1) r.intelTierUnlocked = 1;
            HiddenPotentialSystem.MigrateLegacySpark(r);
            // Reveal / awaken Hidden Potential once scouted.
            if (!wasScouted
                && (r.secretArchetype != SecretArchetype.Parishioner
                    || !string.IsNullOrEmpty(r.hiddenTraitId)))
            {
                if (!r.traitAwakened
                    && HiddenPotentialSystem.IsPositiveCombatTrait(r.hiddenTraitId))
                    r.traitAwakened = true;
                LegendUnlockManager.Ensure()?.NotifyHiddenPotentialAwakened(r);
            }
        }

        /// <summary>
        /// Auto-scout pewSeatIndex ±1 within the same pewSeatIndex / 13 row.
        /// </summary>
        public static void GossipAdjacentSameRow(RecruitInstance center, List<RecruitInstance> townPool)
        {
            if (center == null || townPool == null) return;
            int row = center.pewSeatIndex / PewRowSize;
            foreach (var other in townPool)
            {
                if (other == null || other == center) continue;
                if (other.pewSeatIndex / PewRowSize != row) continue;
                int delta = Mathf.Abs(other.pewSeatIndex - center.pewSeatIndex);
                if (delta != 1) continue;
                MarkScouted(other);
                GrudgeSaveManager.Instance?.RememberIntel(
                    other.unitID, Mathf.Max(other.intelTierUnlocked, 1));
            }
        }
    }

    /// <summary>Phase 5 — escalating flock upkeep deducted from +200g wave victory gold.</summary>
    public static class FlockEconomy
    {
        public static int ComputeUpkeep(int unitCount)
        {
            int n = Mathf.Clamp(unitCount, 0, 52);
            int upkeep = 0;
            for (int i = 1; i <= n; i++)
            {
                if (i <= 12) continue;
                if (i <= 20) upkeep += 5;
                else if (i <= 35) upkeep += 10;
                else upkeep += 20;
            }
            return upkeep;
        }

        public static string FormatBreakdown(int unitCount, int basePayout, int upkeep, int netBase)
        {
            return
                $"Flock {unitCount}: upkeep −{upkeep}g from +{basePayout}g → net base {netBase}g "
                + "(1–12:0 · 13–20:5 · 21–35:10 · 36–52:20).";
        }
    }


    // ===== fix-gdt-profile-always-compile — non-SO helpers LIVE HERE (MustRestore) =====
    // Named *Profile.cs files keep ONLY ScriptableObject classes (Unity class==filename).
    // Helpers permanently in GameDataTypes so Force Sync / GDT alone always compiles Slot/Parallax/SceneBg/BattleSprite types
    // even when named Profile MonoScripts are mid-HardWrite. tip: fix-gdt-profile-always-compile
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

    /// <summary>Named screens that Architect can assign jpg/png or mp4 backgrounds to.</summary>
    public enum SceneBackgroundSlot
    {
        MainMenu = 0,
        SomersetChurch = 1,
        ChurchyardBattle = 2,
        VictoryOverlay = 3,
        MerlinResurrection = 4,
        LegendCodex = 5,
    }

    [Serializable]
    public class SceneBackgroundEntry
    {
        public SceneBackgroundSlot slot = SceneBackgroundSlot.MainMenu;
        public string displayName = "Screen";
        [Tooltip("Still image (jpg/png Sprite). Used when no video is assigned.")]
        public Sprite imageSprite;
        [Tooltip("Optional looping video (mp4 VideoClip). Wins over image when set.")]
        public UnityEngine.Video.VideoClip videoClip;
        [Tooltip("Editor-only asset path backup so assignments survive reimports.")]
        public string imageAssetPath;
        [Tooltip("Editor-only asset path backup for VideoClip.")]
        public string videoAssetPath;
    }

    /// <summary>One Good-side Legend / Evil boss battle-sprite slot (Architect Tab 6).</summary>
    [Serializable]
    public class BattleSpriteSlotEntry
    {
        public UnitType unitType;
        public string displayName;
        public Sprite battleSprite;
        public string assetPath;
        [Tooltip("Soft slots (e.g. Merlin) stay listed but are optional.")]
        public bool softOptional;
    }


    // ===== DA-59 Phase25 helpers (Force Sync nuclear — do not remove) =====
    // Architect *Profile ScriptableObjects live in Assets/Scripts/Data/<ExactClassName>.cs
    // (Unity MonoScript requires SO class name == file name). Appliers stay embedded below.
    // tip: fix-gdt-profile-always-compile
    /// <summary>
    /// Multi-frame clip for modular paper-doll parts (Pose / Outfit / Head / Weapon / EnemyBoss).
    /// Standard clip names: "Idle", "Attack", "Hit", "Death".
    /// </summary>
    [Serializable]
    public class SpriteAnimClip
    {
        public const string Idle = "Idle";
        public const string Attack = "Attack";
        public const string Hit = "Hit";
        public const string Death = "Death";

        public string clipName = Idle;
        public float framesPerSecond = 6f;
        public bool loop = true;
        public List<Sprite> frames = new List<Sprite>();

        public int FrameCount => frames != null ? frames.Count : 0;

        public Sprite FrameAt(int index)
        {
            if (frames == null || frames.Count == 0) return null;
            return frames[Mathf.Clamp(index, 0, frames.Count - 1)];
        }
    }


    [Serializable]
    public class ModularHeadEntry
    {
        public Sprite sprite;
        [Tooltip("If true, tint with hairColorIndex. Hoods/helmets leave Color.white.")]
        public bool isHair = true;
    }

    /// <summary>One registered multi-frame clip on a modular part category.</summary>
    [Serializable]
    public class ModularAnimRegistration
    {
        [Tooltip("Pose, Outfit, Head, Weapon, or EnemyBoss.")]
        public string category = "Pose";
        public SpriteAnimClip clip = new SpriteAnimClip();
    }

    /// <summary>
    /// Sprite arrays for modular townsfolk. Body poses are grayscale (skin-tinted);
    /// outfits are pre-colored with transparent skin cutouts; heads are hair (tint)
    /// or helms (untinted); props are pre-colored.
    /// </summary>
    [CreateAssetMenu(fileName = "ModularSpriteLibrary", menuName = "DemonKnight/Modular Sprite Library")]
    public class ModularSpriteLibrary : ScriptableObject
    {
        public const string ResourcesPath = "Art/Modular/ModularSpriteLibrary";

        [Header("Base poses (grayscale) — ArmsCrossed, HandsOnKnees, LeaningForward, HoldingProp")]
        public Sprite[] basePoses = new Sprite[4];

        [Header("Outfits (pre-colored, transparent skin) — 0–1 Peasant, 2–3 Warrior, 4–5 Knight")]
        public Sprite[] outfits = new Sprite[6];

        [Header("Heads — hair (tint) or hoods/helmets (no tint)")]
        public ModularHeadEntry[] heads = Array.Empty<ModularHeadEntry>();

        [Header("Props — index 0 = None")]
        public Sprite[] props = Array.Empty<Sprite>();

        [Header("Animation clips (Art Architect / ModularSpriteAnimator)")]
        [Tooltip("Registered Idle/Attack/Hit/Death clips keyed by part category.")]
        public List<ModularAnimRegistration> animClips = new List<ModularAnimRegistration>();

        public int HeadCount => heads != null ? heads.Length : 0;
        public int PropCount => props != null ? props.Length : 0;
        public int PoseCount => basePoses != null ? basePoses.Length : 0;
        public int OutfitCount => outfits != null ? outfits.Length : 0;

        /// <summary>Upsert a clip by category + clipName (Pose/Outfit/Head/Weapon/EnemyBoss).</summary>
        public void RegisterAnimClip(string category, SpriteAnimClip clip)
        {
            if (clip == null || string.IsNullOrEmpty(clip.clipName)) return;
            animClips ??= new List<ModularAnimRegistration>();
            string cat = string.IsNullOrEmpty(category) ? "Pose" : category;
            for (int i = 0; i < animClips.Count; i++)
            {
                var e = animClips[i];
                if (e == null) continue;
                if (!string.Equals(e.category, cat, StringComparison.OrdinalIgnoreCase)) continue;
                if (e.clip == null || !string.Equals(e.clip.clipName, clip.clipName, StringComparison.OrdinalIgnoreCase))
                    continue;
                e.clip = clip;
                return;
            }
            animClips.Add(new ModularAnimRegistration { category = cat, clip = clip });
        }

        public SpriteAnimClip FindAnimClip(string category, string clipName)
        {
            if (animClips == null) return null;
            for (int i = 0; i < animClips.Count; i++)
            {
                var e = animClips[i];
                if (e?.clip == null) continue;
                if (!string.Equals(e.category, category, StringComparison.OrdinalIgnoreCase)) continue;
                if (string.Equals(e.clip.clipName, clipName, StringComparison.OrdinalIgnoreCase))
                    return e.clip;
            }
            return null;
        }

        public static ModularSpriteLibrary Load()
        {
            ModularSpriteLibrary lib = null;
            try { lib = Resources.Load<ModularSpriteLibrary>(ResourcesPath); }
            catch { /* missing script / broken asset */ }
            if (lib == null)
            {
                try
                {
                    var all = Resources.LoadAll<ModularSpriteLibrary>("");
                    if (all != null && all.Length > 0) lib = all[0];
                }
                catch { /* ignore */ }
            }
            if (lib == null)
            {
                // Runtime fallback so pews are never empty after Force Sync with no Resources asset.
                lib = CreateInstance<ModularSpriteLibrary>();
                lib.name = "ModularSpriteLibrary_Runtime";
            }
            lib.EnsureSizedArrays();
            lib.EnsurePlaceholderSpritesIfEmpty();
            return lib;
        }

        /// <summary>
        /// Do NOT seed Pose_/Outfit_/Head_ silhouette placeholders — those look like wrong
        /// paperdoll shapes in play. Leave basePoses empty until Architect Save Splices
        /// fills Assets/Art/Spliced/Sprite (then Verify / Escape rebuilds the library).
        /// </summary>
        public void EnsurePlaceholderSpritesIfEmpty()
        {
            EnsureSizedArrays();
            // Intentionally no silhouette seeds. Pew Applier uses solid color when empty.
            if (outfits == null) outfits = Array.Empty<Sprite>();
            if (heads == null) heads = Array.Empty<ModularHeadEntry>();
            if (props == null) props = Array.Empty<Sprite>();
        }

        /// <summary>
        /// Wipe paperdoll Outfit/Head/Prop arrays and non-Sprite anim clips.
        /// Keeps basePoses (Architect Category Sprite). Call from Verify / Clear menu.
        /// </summary>
        public int ClearPaperdollLayersKeepSprites()
        {
            int cleared = 0;
            if (outfits != null)
            {
                for (int i = 0; i < outfits.Length; i++)
                    if (outfits[i] != null) { outfits[i] = null; cleared++; }
                outfits = Array.Empty<Sprite>();
            }
            if (heads != null)
            {
                cleared += heads.Length;
                heads = Array.Empty<ModularHeadEntry>();
            }
            if (props != null)
            {
                for (int i = 0; i < props.Length; i++)
                    if (props[i] != null) cleared++;
                props = Array.Empty<Sprite>();
            }
            if (animClips != null && animClips.Count > 0)
            {
                var keep = new List<ModularAnimRegistration>();
                for (int i = 0; i < animClips.Count; i++)
                {
                    var e = animClips[i];
                    if (e == null) continue;
                    // Keep only Architect Category "Sprite" clips (drop Pose/Outfit/Head/Weapon stacks).
                    if (string.Equals(e.category, "Sprite", StringComparison.OrdinalIgnoreCase))
                        keep.Add(e);
                    else
                        cleared++;
                }
                animClips = keep;
            }
            EnsureSizedArrays();
            return cleared;
        }

        /// <summary>True when library still has paperdoll outfits/heads or legacy Pose/Outfit clips.</summary>
        public bool HasPaperdollResidue()
        {
            if (outfits != null)
                for (int i = 0; i < outfits.Length; i++)
                    if (outfits[i] != null) return true;
            if (heads != null && heads.Length > 0)
                for (int i = 0; i < heads.Length; i++)
                    if (heads[i] != null && heads[i].sprite != null) return true;
            if (animClips != null)
            {
                for (int i = 0; i < animClips.Count; i++)
                {
                    var e = animClips[i];
                    if (e == null || string.IsNullOrEmpty(e.category)) continue;
                    if (!string.Equals(e.category, "Sprite", StringComparison.OrdinalIgnoreCase))
                        return true;
                }
            }
            return false;
        }

        static Sprite MakePlaceholderSprite(string name, int w, int h, Color fill, bool bodySilhouette)
        {
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
                name = name + "_Tex",
                hideFlags = HideFlags.HideAndDontSave
            };
            var pixels = new Color[w * h];
            for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                bool inside;
                if (bodySilhouette)
                {
                    // Simple capsule torso
                    float nx = (x + 0.5f) / w * 2f - 1f;
                    float ny = (y + 0.5f) / h * 2f - 1f;
                    float torso = (nx * nx) / 0.35f + ((ny + 0.1f) * (ny + 0.1f)) / 0.85f;
                    float head = (nx * nx) / 0.18f + ((ny - 0.55f) * (ny - 0.55f)) / 0.18f;
                    inside = torso <= 1f || head <= 1f;
                }
                else
                {
                    float nx = (x + 0.5f) / w * 2f - 1f;
                    float ny = (y + 0.5f) / h * 2f - 1f;
                    inside = nx * nx + ny * ny <= 0.85f;
                }
                pixels[y * w + x] = inside ? fill : Color.clear;
            }
            tex.SetPixels(pixels);
            tex.Apply(false, false);
            var sp = Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0f), 32f);
            sp.name = name;
            return sp;
        }

        public Sprite GetPose(int poseIndex)
        {
            if (basePoses == null || basePoses.Length == 0) return null;
            return basePoses[Mathf.Clamp(poseIndex, 0, basePoses.Length - 1)];
        }

        public Sprite GetOutfit(int outfitIndex)
        {
            if (outfits == null || outfits.Length == 0) return null;
            return outfits[Mathf.Clamp(outfitIndex, 0, outfits.Length - 1)];
        }

        public Sprite GetHead(int headIndex)
        {
            if (heads == null || heads.Length == 0) return null;
            var e = heads[Mathf.Clamp(headIndex, 0, heads.Length - 1)];
            return e != null ? e.sprite : null;
        }

        public bool HeadIsHair(int headIndex)
        {
            if (heads == null || heads.Length == 0) return true;
            var e = heads[Mathf.Clamp(headIndex, 0, heads.Length - 1)];
            return e == null || e.isHair;
        }

        public Sprite GetProp(int propIndex)
        {
            if (props == null || props.Length == 0 || propIndex <= 0) return null;
            if (propIndex >= props.Length) return null;
            return props[propIndex];
        }

        public void EnsureSizedArrays()
        {
            if (basePoses == null || basePoses.Length < 4)
                Array.Resize(ref basePoses, 4);
            // Single-sprite: do not force 6 outfit slots (paperdoll leftover).
            if (outfits == null) outfits = Array.Empty<Sprite>();
            if (heads == null) heads = Array.Empty<ModularHeadEntry>();
            if (props == null) props = Array.Empty<Sprite>();
        }

        public void AssignHeadsFromSprites(IList<Sprite> sprites, IList<bool> isHairFlags = null)
        {
            if (sprites == null) { heads = Array.Empty<ModularHeadEntry>(); return; }
            heads = new ModularHeadEntry[sprites.Count];
            for (int i = 0; i < sprites.Count; i++)
            {
                bool hair = isHairFlags != null && i < isHairFlags.Count ? isHairFlags[i] : GuessIsHair(sprites[i], i);
                heads[i] = new ModularHeadEntry { sprite = sprites[i], isHair = hair };
            }
        }

        /// <summary>Heuristic: first half of a heads sheet often hair; names containing Helm/Hood are not.</summary>
        public static bool GuessIsHair(Sprite s, int index)
        {
            if (s == null) return true;
            string n = s.name ?? "";
            if (n.IndexOf("helm", StringComparison.OrdinalIgnoreCase) >= 0) return false;
            if (n.IndexOf("hood", StringComparison.OrdinalIgnoreCase) >= 0) return false;
            if (n.IndexOf("hat", StringComparison.OrdinalIgnoreCase) >= 0) return false;
            // Even indices in 2×6 head sheet row0 often hair; row1 helms — importer can override.
            return index < 6;
        }
    }

    // ===== force-sync-appliers-hardfix EMBEDDED Appliers (Force Sync nuclear — do not remove) =====
    // Standalone SceneBackgroundApplier.cs / ClassSpritePoolApplier.cs are stubs only.
    // MustRestore of those paths still fails when Force Delete runs under a STALE compiled Sync
    // host (ScriptPaths/MustRestore arrays baked into the old assembly). GameDataTypes has been
    // MustRestore since DA-59 — even old Sync hosts re-land this file from tip → types exist.
    // tip: fix-gdt-profile-always-compile — Applier Load soft-fails when Profile MonoScripts/assets still missing after Sync.

    /// <summary>
    /// Applies Architect-assigned SceneBackgroundProfile entries (jpg/png or mp4)
    /// onto UI Images / SpriteRenderers / optional VideoPlayer hosts. Assign-only:
    /// empty slots leave the screen's existing default art alone.
    /// </summary>
    public static class SceneBackgroundApplier
    {
        static SceneBackgroundProfile _cache;

        public static void InvalidateCache() => _cache = null;

        public static SceneBackgroundProfile Profile
        {
            get
            {
                if (_cache == null)
                {
                    try
                    {
                        _cache = SceneBackgroundProfile.Load();
                        if (_cache != null)
                        {
                            int restored = _cache.RematerializeSpritesFromPaths();
                            if (restored > 0)
                                Debug.Log($"[DemonKnight] SceneBackground rematerialized {restored} image(s) from path backups.");
                        }
                    }
                    catch (Exception ex)
                    {
                        Debug.LogWarning("[DemonKnight] SceneBackgroundProfile.Load soft-fail: " + ex.Message);
                        _cache = null;
                        return null;
                    }
                }
                return _cache;
            }
        }

        public static SceneBackgroundEntry Get(SceneBackgroundSlot slot)
        {
            try { return Profile?.GetEntry(slot); }
            catch (Exception ex)
            {
                Debug.LogWarning("[DemonKnight] SceneBackgroundApplier.Get soft-fail: " + ex.Message);
                return null;
            }
        }

        /// <summary>Apply still sprite to a UI Image when the slot has an image assignment. Soft-fails on missing script.</summary>
        public static bool ApplyToImage(SceneBackgroundSlot slot, Image target, bool preserveColor = true)
        {
            if (target == null) return false;
            try
            {
                var e = Get(slot);
                Sprite sp = ResolveSprite(e);
                if (sp == null) return false;
                target.sprite = sp;
                target.type = Image.Type.Simple;
                target.preserveAspect = true;
                if (!preserveColor) target.color = Color.white;
                else if (target.color.a < 0.05f) target.color = Color.white;
                return true;
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[DemonKnight] SceneBackgroundApplier.ApplyToImage soft-fail: " + ex.Message);
                return false;
            }
        }

        /// <summary>Apply still sprite to a world SpriteRenderer. Soft-fails on missing script.</summary>
        public static bool ApplyToSpriteRenderer(SceneBackgroundSlot slot, SpriteRenderer target)
        {
            if (target == null) return false;
            try
            {
                var e = Get(slot);
                Sprite sp = ResolveSprite(e);
                if (sp == null) return false;
                target.sprite = sp;
                target.color = Color.white;
                return true;
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[DemonKnight] SceneBackgroundApplier.ApplyToSpriteRenderer soft-fail: " + ex.Message);
                return false;
            }
        }

        /// <summary>
        /// Prefer video when assigned; otherwise still image. Returns true if anything applied.
        /// Soft-fails when profile MonoScript GUID/fileID is stale after embed.
        /// Video plays into <paramref name="videoHost"/> (adds VideoPlayer + RawImage child if needed).
        /// </summary>
        public static bool ApplyToUiHost(
            SceneBackgroundSlot slot,
            Image imageTarget,
            RectTransform videoHostParent = null)
        {
            try
            {
                var e = Get(slot);
                if (e == null) return false;

                VideoClip clip = ResolveVideo(e);
                if (clip != null && videoHostParent != null)
                {
                    EnsureUiVideo(videoHostParent, clip);
                    if (imageTarget != null)
                        imageTarget.color = new Color(0.1f, 0.1f, 0.1f, 0.35f);
                    return true;
                }

                return ApplyToImage(slot, imageTarget, preserveColor: false);
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[DemonKnight] SceneBackgroundApplier.ApplyToUiHost soft-fail: " + ex.Message);
                return false;
            }
        }

        public static Sprite ResolveSprite(SceneBackgroundEntry e)
        {
            if (e == null) return null;
            if (e.imageSprite != null) return e.imageSprite;
            // Player builds: rematerialize from path via Resources / StreamingAssets (no AssetDatabase wipe).
            if (!string.IsNullOrEmpty(e.imageAssetPath))
            {
                Sprite sp = ClassSpritePoolProfile.TryLoadSpriteAtPath(e.imageAssetPath);
                if (sp != null)
                {
                    e.imageSprite = sp;
                    return sp;
                }
            }
            return null;
        }

        public static VideoClip ResolveVideo(SceneBackgroundEntry e)
        {
            if (e == null) return null;
            if (e.videoClip != null) return e.videoClip;
#if UNITY_EDITOR
            if (!string.IsNullOrEmpty(e.videoAssetPath))
            {
                var clip = UnityEditor.AssetDatabase.LoadAssetAtPath<VideoClip>(e.videoAssetPath);
                if (clip != null) return clip;
            }
#endif
            if (!string.IsNullOrEmpty(e.videoAssetPath))
            {
                string res = e.videoAssetPath.Replace("\\", "/");
                int ri = res.IndexOf("/Resources/", StringComparison.OrdinalIgnoreCase);
                if (ri >= 0)
                {
                    res = res.Substring(ri + "/Resources/".Length);
                    if (res.EndsWith(".mp4", StringComparison.OrdinalIgnoreCase))
                        res = res.Substring(0, res.Length - 4);
                    var clip = Resources.Load<VideoClip>(res);
                    if (clip != null) return clip;
                }
            }
            return null;
        }

        static void EnsureUiVideo(RectTransform parent, VideoClip clip)
        {
            if (parent == null || clip == null) return;
            Transform existing = parent.Find("ArchitectVideoBg");
            GameObject go;
            RawImage raw;
            if (existing != null)
            {
                go = existing.gameObject;
                raw = go.GetComponent<RawImage>();
            }
            else
            {
                go = new GameObject("ArchitectVideoBg", typeof(RectTransform), typeof(RawImage));
                go.transform.SetParent(parent, false);
                go.transform.SetAsFirstSibling();
                var rt = go.GetComponent<RectTransform>();
                rt.anchorMin = Vector2.zero;
                rt.anchorMax = Vector2.one;
                rt.offsetMin = Vector2.zero;
                rt.offsetMax = Vector2.zero;
                raw = go.GetComponent<RawImage>();
            }

            go.SetActive(true);
            var vp = go.GetComponent<VideoPlayer>();
            if (vp == null) vp = go.AddComponent<VideoPlayer>();

            const int w = 1280, h = 720;
            var rtTex = raw.texture as RenderTexture;
            if (rtTex == null || rtTex.width != w || rtTex.height != h)
            {
                rtTex = new RenderTexture(w, h, 0)
                {
                    name = "ArchitectUiVideoRT_" + clip.name
                };
                raw.texture = rtTex;
            }

            vp.playOnAwake = false;
            vp.isLooping = true;
            vp.audioOutputMode = VideoAudioOutputMode.None;
            vp.renderMode = VideoRenderMode.RenderTexture;
            vp.targetTexture = rtTex;
            vp.source = VideoSource.VideoClip;
            vp.clip = clip;
            vp.skipOnDrop = true;
            if (!vp.isPlaying) vp.Play();
        }
    }

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
                // Never keep an empty CreateInstance stand-in forever — retry Resources.Load
                // (Enter Play Mode without domain reload / Ensure remap after Force Sync).
                if (_cache != null && _cache.isRuntimeStandIn && _cache.CountUsable(UnitType.Peasant)
                    + _cache.CountUsable(UnitType.Warrior) + _cache.CountUsable(UnitType.Knight) == 0)
                    _cache = null;

                if (_cache == null)
                {
                    try
                    {
                        // SoftPromote belt: Load soft-fails when Profile MonoScript still missing after Sync.
                        // tip: fix-gdt-profile-always-compile
                        _cache = ClassSpritePoolProfile.Load();
                        if (_cache != null)
                        {
                            int restored = _cache.RematerializeSpritesFromPaths();
                            if (restored > 0)
                                Debug.Log($"[DemonKnight] ClassSpritePool rematerialized {restored} sprite(s) from path backups.");
                        }
                    }
                    catch (Exception ex)
                    {
                        Debug.LogWarning("[DemonKnight] ClassSpritePoolProfile.Load soft-fail: " + ex.Message);
                        _cache = null;
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
                if (profile == null || profile.isRuntimeStandIn)
                {
                    // Soft-fail empty stand-in — callers keep StageLayout / solid fallbacks.
                    if (profile == null || profile.CountUsable(type) == 0) return null;
                }
                List<Sprite> pool = profile.GetPool(type);
                if (pool == null || pool.Count == 0) return null;

                // Body pipeline only — skip any sprite that also lives in Tab 5 face pools.
                var usableList = new List<Sprite>();
                for (int i = 0; i < pool.Count; i++)
                {
                    Sprite sp = pool[i];
                    if (sp == null) continue;
                    if (LegendPewPortraitApplier.IsFacePoolSprite(sp))
                    {
                        Debug.LogWarning(
                            "[DemonKnight] Soft-fail: face-pool sprite '" + sp.name
                            + "' found in ClassSpritePool body pool — skipped for bodySprite.");
                        continue;
                    }
                    usableList.Add(sp);
                }
                if (usableList.Count == 0) return null;

                int seed = StableHash(
                    !string.IsNullOrEmpty(unitId) ? unitId
                    : (!string.IsNullOrEmpty(characterName) ? characterName + "#" + pewSeatIndex
                    : type + "#" + pewSeatIndex));
                int want = Math.Abs(seed) % usableList.Count;
                return usableList[want];
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

    /// <summary>
    /// Resolves Architect LegendPewPortraitProfile FACE assignments for Legends FacePortrait
    /// and optional townsfolk pew face overlays. Body avatars stay on ClassSpritePoolApplier.
    /// Soft-fails when empty / cross-assigned — callers keep CharacterDatabase / StageLayout fallbacks.
    /// Embedded in GameDataTypes (PR #33 Force Sync CS0103 lesson — no standalone applier file).
    /// tip: battle-good-evil-sprite-pools (Tab 5 FACE stays; Tab 6 is BattleGoodEvil)
    /// </summary>
    public static class LegendPewPortraitApplier
    {
        static LegendPewPortraitProfile _cache;

        public static void InvalidateCache() => _cache = null;

        public static LegendPewPortraitProfile Profile
        {
            get
            {
                if (_cache != null && _cache.isRuntimeStandIn
                    && _cache.CountUsablePewFaces(UnitType.Peasant)
                    + _cache.CountUsablePewFaces(UnitType.Warrior)
                    + _cache.CountUsablePewFaces(UnitType.Knight) == 0
                    && _cache.CountAssignedLegendFaces() == 0)
                    _cache = null;

                if (_cache == null)
                {
                    try
                    {
                        _cache = LegendPewPortraitProfile.Load();
                        if (_cache != null)
                        {
                            int restored = _cache.RematerializeSpritesFromPaths();
                            if (restored > 0)
                                Debug.Log($"[DemonKnight] LegendPewPortrait rematerialized {restored} sprite(s) from path backups.");
                        }
                    }
                    catch (Exception ex)
                    {
                        Debug.LogWarning("[DemonKnight] LegendPewPortraitProfile.Load soft-fail: " + ex.Message);
                        _cache = null;
                        return null;
                    }
                }
                return _cache;
            }
        }

        /// <summary>Architect-assigned face sprite for a Legend (null = use CharacterDatabase).</summary>
        public static Sprite GetLegendFace(UnitType type)
        {
            try
            {
                var profile = Profile;
                return profile != null ? profile.GetLegendFace(type) : null;
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[DemonKnight] LegendPewPortraitApplier.GetLegendFace soft-fail: " + ex.Message);
                return null;
            }
        }

        /// <summary>
        /// Pick a townsfolk pew face/body profile from the Architect pools
        /// (distinct from ClassSpritePool full-body). Deterministic by unitID.
        /// </summary>
        public static Sprite PickPewProfile(RecruitInstance recruit)
        {
            if (recruit == null) return null;
            return PickPewProfile(recruit.unitType, recruit.unitID, recruit.characterName, recruit.pewSeatIndex);
        }

        public static Sprite PickPewProfile(UnitType type, string unitId, string characterName = null, int pewSeatIndex = 0)
        {
            try
            {
                var profile = Profile;
                if (profile == null) return null;
                List<Sprite> pool = profile.GetPewFacePool(type);
                if (pool == null || pool.Count == 0) return null;

                // Face pipeline only — never return a Tab 4 body-pool sprite as a face overlay.
                var usableList = new List<Sprite>();
                for (int i = 0; i < pool.Count; i++)
                {
                    Sprite sp = pool[i];
                    if (sp == null) continue;
                    if (IsBodyPoolSprite(sp))
                    {
                        Debug.LogWarning(
                            "[DemonKnight] Soft-fail: body-pool sprite '" + sp.name
                            + "' found in LegendPew face pool — skipped for face overlay.");
                        continue;
                    }
                    usableList.Add(sp);
                }
                if (usableList.Count == 0) return null;

                int seed = ClassSpritePoolApplier.StableHash(
                    !string.IsNullOrEmpty(unitId) ? "pewface:" + unitId
                    : (!string.IsNullOrEmpty(characterName) ? "pewface:" + characterName + "#" + pewSeatIndex
                    : "pewface:" + type + "#" + pewSeatIndex));
                int want = Math.Abs(seed) % usableList.Count;
                return usableList[want];
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[DemonKnight] LegendPewPortraitApplier.PickPewProfile soft-fail: " + ex.Message);
                return null;
            }
        }

        /// <summary>
        /// BODY only — Tab 4 ClassSpritePool full pew/combat avatar.
        /// Rematerializes from peasant/warrior/knightAssetPaths when Sprite refs are Missing.
        /// Optional fallback: Tab 6 Good battle troop pools ONLY when Tab 4 body pool is empty
        /// (documented — battle tokens are not the preferred pew pipeline).
        /// Never returns Tab 5 face-pool sprites as bodySprite (soft-fail skip).
        /// tip: fix-class-pool-kind-cs0019
        /// </summary>
        public static Sprite ResolvePewVisual(RecruitInstance recruit)
        {
            Sprite body = ClassSpritePoolApplier.PickForRecruit(recruit);
            if (body == null)
            {
                // Tab 4 empty (sprites + paths) → optional Tab 6 Good troop body fallback.
                body = BattleGoodEvilSpritePoolApplier.TryPewBodyFallbackFromGoodTroops(recruit);
            }
            if (body != null && TownsfolkVisualApplier.IsUsablePewSprite(body))
            {
                if (IsFacePoolSprite(body))
                {
                    Debug.LogWarning(
                        "[DemonKnight] Soft-fail: refusing face-pool sprite as bodySprite ("
                        + body.name + ").");
                    return null;
                }
                return body;
            }
            return null;
        }

        /// <summary>
        /// FACE only — Tab 5 townsfolk pew face overlay (optional Head layer).
        /// Legends use <see cref="GetLegendFace"/> for FacePortraitFrame. Never a bodySprite.
        /// </summary>
        public static Sprite ResolvePewFaceOverlay(RecruitInstance recruit)
        {
            Sprite face = PickPewProfile(recruit);
            if (face != null && TownsfolkVisualApplier.IsUsablePewSprite(face))
            {
                if (IsBodyPoolSprite(face))
                {
                    Debug.LogWarning(
                        "[DemonKnight] Soft-fail: refusing body-pool sprite as face overlay ("
                        + face.name + ").");
                    return null;
                }
                return face;
            }
            return null;
        }

        /// <summary>True when sprite is listed in Tab 5 Legend/Pew face pools (or legend face slots).</summary>
        public static bool IsFacePoolSprite(Sprite sp)
        {
            if (sp == null) return false;
            try
            {
                var profile = Profile;
                if (profile == null) return false;
                if (ListContainsSprite(profile.peasantFaceProfiles, sp)) return true;
                if (ListContainsSprite(profile.warriorFaceProfiles, sp)) return true;
                if (ListContainsSprite(profile.knightFaceProfiles, sp)) return true;
                if (profile.legendPortraits != null)
                {
                    for (int i = 0; i < profile.legendPortraits.Count; i++)
                    {
                        var e = profile.legendPortraits[i];
                        if (e != null && e.facePortrait == sp) return true;
                    }
                }
            }
            catch { /* soft-fail */ }
            return false;
        }

        /// <summary>True when sprite is listed in Tab 4 ClassSpritePool body pools.</summary>
        public static bool IsBodyPoolSprite(Sprite sp)
        {
            if (sp == null) return false;
            try
            {
                var profile = ClassSpritePoolApplier.Profile;
                if (profile == null) return false;
                if (ListContainsSprite(profile.peasantSprites, sp)) return true;
                if (ListContainsSprite(profile.warriorSprites, sp)) return true;
                if (ListContainsSprite(profile.knightSprites, sp)) return true;
            }
            catch { /* soft-fail */ }
            return false;
        }

        static bool ListContainsSprite(List<Sprite> list, Sprite sp)
        {
            if (list == null || sp == null) return false;
            for (int i = 0; i < list.Count; i++)
                if (list[i] == sp) return true;
            return false;
        }
    }

    /// <summary>
    /// Resolves Architect Tab 6 battle sprites for BattlefieldVisualizer / UnitVisualActor.
    /// Soft-fails when empty — callers keep CharacterDatabase / demon fallbacks.
    /// Embedded in GameDataTypes (Force Sync CS0103 lesson). tip: battle-good-evil-sprite-pools
    /// </summary>
    public static class BattleGoodEvilSpritePoolApplier
    {
        static BattleGoodEvilSpritePoolProfile _cache;

        public static void InvalidateCache() => _cache = null;

        public static BattleGoodEvilSpritePoolProfile Profile
        {
            get
            {
                if (_cache != null && _cache.isRuntimeStandIn
                    && _cache.CountUsablePool(UnitType.Peasant)
                    + _cache.CountUsablePool(UnitType.Warrior)
                    + _cache.CountUsablePool(UnitType.Knight)
                    + _cache.CountUsablePool(UnitType.Priest)
                    + _cache.CountUsablePool(UnitType.DemonImp)
                    + _cache.CountUsablePool(UnitType.DemonHellhound)
                    + _cache.CountUsablePool(UnitType.DemonDreadLord)
                    + _cache.CountAssignedLegendBattles()
                    + _cache.CountAssignedBossBattles() == 0)
                    _cache = null;

                if (_cache == null)
                {
                    try
                    {
                        _cache = BattleGoodEvilSpritePoolProfile.Load();
                        if (_cache != null)
                        {
                            int restored = _cache.RematerializeSpritesFromPaths();
                            if (restored > 0)
                                Debug.Log($"[DemonKnight] BattleGoodEvil rematerialized {restored} sprite(s) from path backups.");
                        }
                    }
                    catch (Exception ex)
                    {
                        Debug.LogWarning("[DemonKnight] BattleGoodEvilSpritePoolProfile.Load soft-fail: " + ex.Message);
                        _cache = null;
                        return null;
                    }
                }
                return _cache;
            }
        }

        /// <summary>
        /// Prefer Architect Tab 6 battle sprite for this unit type.
        /// Pools use deterministic hash(unitId); legend/boss slots are 1:1.
        /// Returns null when empty — Visualizer falls back to CharacterDatabase / demon art.
        /// </summary>
        public static Sprite ResolveBattleSprite(UnitType type, string unitId = null)
        {
            try
            {
                var profile = Profile;
                if (profile == null) return null;

                // Boss slots first (UnitType.Boss*)
                string typeName = type.ToString();
                if (typeName.StartsWith("Boss", StringComparison.Ordinal))
                {
                    Sprite boss = profile.GetBossBattleSprite(type);
                    if (boss != null && !IsForbiddenForBattle(boss)) return boss;
                    return null;
                }

                // Legend single slots
                if (RecruitInstance.IsLegendUnit(type) && type != UnitType.Priest && type != UnitType.Captain)
                {
                    Sprite legend = profile.GetLegendBattleSprite(type);
                    if (legend != null && !IsForbiddenForBattle(legend)) return legend;
                    return null;
                }

                // Troop / wave-unit pools
                List<Sprite> pool = profile.GetTroopOrWavePool(type);
                if (pool == null || pool.Count == 0) return null;
                var usable = new List<Sprite>();
                for (int i = 0; i < pool.Count; i++)
                {
                    Sprite sp = pool[i];
                    if (sp == null) continue;
                    if (IsForbiddenForBattle(sp))
                    {
                        Debug.LogWarning(
                            "[DemonKnight] Soft-fail: face/body-cross sprite '" + sp.name
                            + "' skipped in battle pool for " + type + ".");
                        continue;
                    }
                    usable.Add(sp);
                }
                if (usable.Count == 0) return null;
                int seed = ClassSpritePoolApplier.StableHash(
                    !string.IsNullOrEmpty(unitId) ? "battle:" + unitId : "battle:" + type);
                int want = Math.Abs(seed) % usable.Count;
                return usable[want];
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[DemonKnight] BattleGoodEvilSpritePoolApplier.Resolve soft-fail: " + ex.Message);
                return null;
            }
        }

        /// <summary>
        /// Pew BODY emergency fallback when Tab 4 ClassSpritePool is empty (sprites + paths).
        /// Uses Tab 6 Good troop pools (Peasant/Warrior/Knight) ONLY — never Evil, never faces.
        /// Documented optional path; preferred pipeline remains Tab 4. tip: fix-class-pool-kind-cs0019
        /// </summary>
        public static Sprite TryPewBodyFallbackFromGoodTroops(RecruitInstance recruit)
        {
            if (recruit == null) return null;
            UnitType type = recruit.unitType;
            if (type != UnitType.Peasant && type != UnitType.Warrior && type != UnitType.Knight)
                return null;
            try
            {
                var bodyProfile = ClassSpritePoolApplier.Profile;
                if (bodyProfile != null)
                {
                    bodyProfile.RematerializeSpritesFromPaths();
                    if (bodyProfile.CountUsable(type) > 0)
                        return null; // Tab 4 has entries — do not steal from battle pools
                }

                var profile = Profile;
                if (profile == null) return null;
                profile.RematerializeSpritesFromPaths();
                List<Sprite> pool = type switch
                {
                    UnitType.Peasant => profile.peasantBattleSprites,
                    UnitType.Warrior => profile.warriorBattleSprites,
                    UnitType.Knight => profile.knightBattleSprites,
                    _ => null
                };
                if (pool == null || pool.Count == 0) return null;
                var usable = new List<Sprite>();
                for (int i = 0; i < pool.Count; i++)
                {
                    Sprite sp = pool[i];
                    if (sp == null) continue;
                    if (LegendPewPortraitApplier.IsFacePoolSprite(sp)) continue;
                    if (!TownsfolkVisualApplier.IsUsablePewSprite(sp)) continue;
                    usable.Add(sp);
                }
                if (usable.Count == 0) return null;
                int seed = ClassSpritePoolApplier.StableHash(
                    !string.IsNullOrEmpty(recruit.unitID)
                        ? "pewbody-fallback:" + recruit.unitID
                        : "pewbody-fallback:" + type + "#" + recruit.pewSeatIndex);
                int want = Math.Abs(seed) % usable.Count;
                Debug.LogWarning(
                    "[DemonKnight] Tab 4 body pool empty for " + type
                    + " — using Tab 6 Good battle pool as pew BODY fallback ("
                    + usable[want].name + "). Fill Architect Tab 4 to prefer class pools.");
                return usable[want];
            }
            catch (Exception ex)
            {
                Debug.LogWarning(
                    "[DemonKnight] Tab6 pew-body fallback soft-fail: " + ex.Message);
                return null;
            }
        }

        /// <summary>True when sprite belongs to Tab 5 FACE pools (must not drive battle tokens).</summary>
        public static bool IsForbiddenForBattle(Sprite sp)
        {
            if (sp == null) return true;
            try
            {
                if (LegendPewPortraitApplier.IsFacePoolSprite(sp)) return true;
            }
            catch { /* soft-fail */ }
            return false;
        }

        /// <summary>True when sprite is listed in any Tab 6 battle pool/slot.</summary>
        public static bool IsBattlePoolSprite(Sprite sp)
        {
            if (sp == null) return false;
            try
            {
                var profile = Profile;
                if (profile == null) return false;
                if (ListContains(profile.peasantBattleSprites, sp)) return true;
                if (ListContains(profile.warriorBattleSprites, sp)) return true;
                if (ListContains(profile.knightBattleSprites, sp)) return true;
                if (ListContains(profile.priestBattleSprites, sp)) return true;
                if (ListContains(profile.demonImpBattleSprites, sp)) return true;
                if (ListContains(profile.hellhoundBattleSprites, sp)) return true;
                if (ListContains(profile.dreadLordBattleSprites, sp)) return true;
                if (SlotListContains(profile.goodLegendSlots, sp)) return true;
                if (SlotListContains(profile.evilBossSlots, sp)) return true;
            }
            catch { /* soft-fail */ }
            return false;
        }

        static bool ListContains(List<Sprite> list, Sprite sp)
        {
            if (list == null || sp == null) return false;
            for (int i = 0; i < list.Count; i++)
                if (list[i] == sp) return true;
            return false;
        }

        static bool SlotListContains(List<BattleSpriteSlotEntry> list, Sprite sp)
        {
            if (list == null || sp == null) return false;
            for (int i = 0; i < list.Count; i++)
                if (list[i] != null && list[i].battleSprite == sp) return true;
            return false;
        }
    }

}
