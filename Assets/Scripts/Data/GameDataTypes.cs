using System;
using System.Collections.Generic;
using UnityEngine;

namespace ArthurianAutobattler
{
    public enum GamePhase { TownRecruit, AutoBattle, Rewards, Resurrection }

    public enum UnitType
    {
        Peasant, Warrior, Knight,
        Priest, Captain,
        KingArthur, Merlin, SirLancelot, MorganLeFay, LadyOfTheLake, SirGawain, Mordred, SirGalahad,
        DemonImp, DemonHellhound, DemonDreadLord,
        BossBlackKnight, BossVortigern, BossGreenKnight, BossAccolon, BossNimue
    }

    public enum TraitTag { None, Noble, Outlaw, Drunkard, Zealot, Coward, Loyalist }
    public enum CombatRole { Unassigned, FrontlineTank, BacklineRanged }

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

        public int GetFinalHireCost(int grudgePenalty, int globalDiscount = 0)
        {
            int cost = Mathf.Max(1, baseHireCost + grudgePenalty - globalDiscount);
            if (primaryTrait == TraitTag.Loyalist) cost = Mathf.Max(1, cost / 2);
            return cost;
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

        public string GetDisplayDescription()
        {
            string info = $"<b>{characterName}</b> ({unitType}) | Role: <b>{assignedRole}</b>\n";
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
        public float battleElapsed = 0f;
        public float petrifiedTimer = 0f;

        public bool IsAlive => currentHealth > 0;

        public void ResetAttackCooldown()
        {
            float effectiveAPS = isHexed ? attacksPerSecond * 0.5f : attacksPerSecond;
            attackTimer = 1f / Mathf.Clamp(effectiveAPS, 0.4f, 3.0f);
        }
    }

    public static class PartySynergyEvaluator
    {
        public static bool CanJoinParty(RecruitInstance candidate, List<RecruitInstance> currentParty, out string refusalReason)
        {
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
            // King Arthur nullifies rivalry (hatedTrait) nerfs while preserving love synergies.
            bool arthurPresent = false;
            foreach (var member in party)
            {
                if (member.unitType == UnitType.KingArthur)
                {
                    arthurPresent = true;
                    break;
                }
            }

            foreach (var unit in party)
            {
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
