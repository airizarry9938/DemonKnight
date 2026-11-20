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
        /// Pre-fusion ingredient snapshots (deep). Wave-end disband expands these back
        /// into pew seats so fused Legends never occupy seats or block return.
        /// </summary>
        public List<RecruitInstance> fusionIngredients;

        public bool IsFusedLegend =>
            fusionIngredients != null && fusionIngredients.Count > 0;

        public int GetFinalHireCost(int grudgePenalty, int globalDiscount = 0)
        {
            int effectiveGrudge = grudgePenalty;
            int bloodRank = SomersetRunes.GetRank(BankRuneID.BloodOath);
            if (bloodRank > 0
                && (primaryTrait == TraitTag.Outlaw || primaryTrait == TraitTag.Coward)
                && grudgePenalty > 0)
            {
                // Blood-Oath reverses grudge into a discount for Outlaws/Cowards.
                effectiveGrudge = -(bloodRank * 5);
            }

            int peasantCut = 0;
            if (unitType == UnitType.Peasant)
                peasantCut = SomersetRunes.GetRank(BankRuneID.PeasantsCrown) * 2;

            int jealousyTax = 0;
            if (SomersetRunes.IsSinActive(SinRuneID.Jealousy) && GameLoopManager.Instance != null)
            {
                int legendCount = 0;
                foreach (var m in GameLoopManager.Instance.recruitedParty)
                {
                    if (IsLegendUnit(m.unitType)) legendCount++;
                }
                jealousyTax = legendCount * 10;
            }

            int cost = Mathf.Max(1, baseHireCost + effectiveGrudge - globalDiscount - peasantCut + jealousyTax);
            if (primaryTrait == TraitTag.Loyalist) cost = Mathf.Max(1, cost / 2);
            return cost;
        }

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
            string info = $"<b>{characterName}</b> ({unitType}) | Pew {PewIndex + 1}/12 (seat {pewSeatIndex}) | Role: <b>{assignedRole}</b>\n";
            if (isSirenEnchantress) info += "<color=#FF69B4>Siren Enchantress</color>\n";

            if (intelTierUnlocked >= 1)
                info += $"HP: {maxHealth} | ATK: {attackDamage} | SPD: {attackSpeed:F1}/s\n";
            else
                info += "HP: ??? | ATK: ??? (Pay 10g to Scout)\n";

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

            foreach (var member in currentParty)
            {
                if (candidate.primaryTrait == TraitTag.Noble && member.primaryTrait == TraitTag.Outlaw)
                {
                    refusalReason = $"{candidate.characterName} refuses to serve alongside an Outlaw like {member.characterName}!";
                    return false;
                }
            }

            refusalReason = string.Empty;
            return true;
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

            foreach (var unit in party)
            {
                if (peasantApsBonus > 0f && unit.unitType == UnitType.Peasant)
                    unit.attackSpeed = Mathf.Min(2.0f, unit.attackSpeed + peasantApsBonus);

                if (jealousy && !RecruitInstance.IsLegendUnit(unit.unitType))
                    unit.attackDamage = Mathf.Max(1, unit.attackDamage - 3);

                foreach (var ally in party)
                {
                    if (unit == ally) continue;
                    if (!arthurPresent
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
