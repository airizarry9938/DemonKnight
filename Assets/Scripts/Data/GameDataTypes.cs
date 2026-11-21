using System;
using System.Collections.Generic;
using UnityEngine;

namespace ArthurianAutobattler
{
    public enum GamePhase { TownRecruit, AutoBattle, Rewards, Resurrection, PactSelect }

    public enum UnitType
    {
        Peasant, Warrior, Knight,
        Priest, Captain,
        KingArthur, Merlin, SirLancelot, MorganLeFay, LadyOfTheLake, SirGawain, Mordred, SirGalahad,
        SirTristan, SirPercival, SirKay, SirBedivere, Guinevere,
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

        /// <summary>Net vs base hire before Loyalist ÷2 / floor (for dialogue).</summary>
        public int NetModsBeforeLoyalistAndFloor =>
            effectiveGrudge - writDiscount - crownCut + jealousyTax - starDiscount - tavernCut;

        public string FormatDossierLines()
        {
            var sb = new System.Text.StringBuilder();
            sb.Append($"Hire: <b>{finalCost}g</b>  (base {baseHire}g");
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

        /// <summary>
        /// Wave victories while this soul marched (and returned to a pew). Stacks.
        /// Each star → −5g hire (stacks with win −10 grudge; Balance V2 removed loss/death +15).
        /// </summary>
        public int victoryStars;

        /// <summary>
        /// Train Ranged equips a crossbow — Knights (and other non-Warrior melee) may
        /// attack from BACK (no NO REACH). Warriors never receive this kit.
        /// </summary>
        public bool canAttackFromBack;

        /// <summary>Hire discount gold per victory star.</summary>
        public const int VictoryStarHireDiscount = 5;

        /// <summary>Balance V2 hire bands.</summary>
        public const int PeasantHireFloor = 10;
        public const int PeasantHireCeil = 15;
        public const int WarriorHireFloor = 35;
        public const int WarriorHireCeil = 55;
        public const int KnightHireFloor = 110;
        public const int KnightHireCeil = 160;

        /// <summary>Intel costs: T1 Scout free (still must click), Investigate 20g, Full Report 30g.</summary>
        public const int IntelTier1Cost = 0;
        public const int IntelTier2Cost = 20;
        public const int IntelTier3Cost = 30;

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
                victoryStars = Mathf.Max(0, victoryStars),
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

            int cost = Mathf.Max(1,
                b.baseHire + b.effectiveGrudge - b.writDiscount - b.crownCut
                + b.jealousyTax - b.starDiscount - b.tavernCut);
            if (b.loyalistHalf) cost = Mathf.Max(1, cost / 2);
            b.preFloorCost = cost;
            // Tier floors after ALL modifiers so Loyalist / Crown / Writ / Tavern
            // cannot undercut band floors on Fresh New Game townsfolk.
            b.finalCost = Mathf.Max(cost, b.floorApplied);
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
                   || t == UnitType.SirKay || t == UnitType.SirBedivere || t == UnitType.Guinevere;
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

        /// <summary>
        /// Paid training: Tank HP boost, or Train Ranged crossbow (+ATK, canAttackFromBack).
        /// Warriors are blocked from the ranged kit — use SetFormationRole to move them BACK.
        /// </summary>
        public void TrainRole(CombatRole newRole)
        {
            if (newRole == CombatRole.BacklineRanged)
            {
                if (!CanTrainRangedKit(unitType)) return;
                // Already BACK with kit — nothing to spend.
                if (assignedRole == CombatRole.BacklineRanged && canAttackFromBack) return;
                assignedRole = CombatRole.BacklineRanged;
                if (!canAttackFromBack)
                {
                    attackDamage = Mathf.RoundToInt(attackDamage * 1.20f);
                    canAttackFromBack = true;
                }
                return;
            }

            if (assignedRole == newRole) return;
            assignedRole = newRole;
            if (newRole == CombatRole.FrontlineTank)
                maxHealth = Mathf.RoundToInt(maxHealth * 1.25f);
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
                    ? "<color=#FF6644><b>NO REACH</b> — Warriors cannot Train Ranged. Move to FRONT to attack.</color>\n"
                    : "<color=#FF6644><b>NO REACH</b> — melee in BACK cannot attack. Train Ranged (crossbow) or move to FRONT.</color>\n";
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

            if (intelTierUnlocked >= 1)
                info += $"HP: {maxHealth} | ATK: {attackDamage} | SPD: {attackSpeed:F1}/s\n";
            else
                info += $"HP: ??? | ATK: ??? (Scout — free)\n";

            // Hire add-ons / subtractions — same math as Recruit (Ng).
            info += $"<color=#FFE066>{hire.FormatDossierLines()}</color>\n";

            // Battle-only perk / rune effects — only after Scout so HP/ATK deltas do not leak.
            if (intelTierUnlocked >= 1)
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
        /// <summary>Train Ranged crossbow — allows melee to fire from BACK.</summary>
        public bool canAttackFromBack;

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
}
