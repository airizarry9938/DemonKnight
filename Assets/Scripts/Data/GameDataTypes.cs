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
        DemonImp, DemonHellhound, DemonDreadLord, SirenEnchantress,
        BossBlackKnight, BossVortigern, BossGreenKnight, BossAccolon, BossNimue
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
        /// Wave victories while this soul marched (and returned to a pew). Stacks.
        /// Each star → −5g hire (stacks with win −10 / loss +15 grudge deltas).
        /// </summary>
        public int victoryStars;

        /// <summary>Hire discount gold per victory star.</summary>
        public const int VictoryStarHireDiscount = 5;

        /// <summary>Peasant hire band floor — Loyalist / Crown / Tavern must not push below this.</summary>
        public const int PeasantHireFloor = 10;
        public const int PeasantHireCeil = 20;
        public const int WarriorHireFloor = 50;
        public const int KnightHireFloor = 200;

        /// <summary>
        /// Pre-fusion ingredient snapshots (deep). Wave-end disband expands these back
        /// into pew seats so fused Legends never occupy seats or block return.
        /// </summary>
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
                   || t == UnitType.Captain;
        }

        public void TrainRole(CombatRole newRole)
        {
            if (assignedRole == newRole) return;
            assignedRole = newRole;
            if (newRole == CombatRole.FrontlineTank)
                maxHealth = Mathf.RoundToInt(maxHealth * 1.25f);
            else if (newRole == CombatRole.BacklineRanged)
                attackDamage = Mathf.RoundToInt(attackDamage * 1.20f);
        }

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
                info += "HP: ??? | ATK: ??? (Pay 10g to Scout)\n";

            // Hire add-ons / subtractions — same math as Recruit (Ng).
            info += $"<color=#FFE066>{hire.FormatDossierLines()}</color>\n";

            // Battle-only perk / rune effects that dossier base stats omit.
            info += FormatBattleBonusLines();

            if (intelTierUnlocked >= 2)
                info += $"<color=#FF5555>Rival: Hates {hatedTrait}</color>\n";
            else
                info += "Disadvantages: ??? (Pay 50g to Investigate)\n";

            if (intelTierUnlocked >= 3)
                info += $"<color=#55FF55>Trait: {primaryTrait} | Loves: {lovedTrait}</color>";
            else
                info += "Full Potential: ??? (Pay 100g for Dossier)";

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

        public bool IsAlive => currentHealth > 0;

        /// <summary>
        /// Battalion FRONT: Tank or Unassigned. BACK is Ranged only.
        /// Demons must kill/clear FRONT before they can hit BACK.
        /// </summary>
        public bool IsFrontRow => assignedRole != CombatRole.BacklineRanged;

        /// <summary>Battalion BACK: trained Ranged formation.</summary>
        public bool IsBackRow => assignedRole == CombatRole.BacklineRanged;

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
            int peasantCount = 0;
            foreach (var member in party)
            {
                if (member.unitType == UnitType.KingArthur) arthurPresent = true;
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
