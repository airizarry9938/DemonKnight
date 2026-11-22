using System;
using System.Collections.Generic;
using UnityEngine;

namespace ArthurianAutobattler
{
    public enum CharacterCategory
    {
        Legend,
        Boss,
        Demon
    }

    public enum CharacterCombatRole
    {
        Tank,
        Backline,
        Hybrid
    }

    public enum CharacterPassiveType
    {
        None,
        SurviveFirstLethalAt1Hp,
        HealAlliesOnHit,
        ExtraDamageVsFront,
        CharmChance,
        PetrifyChance,
        AuraAtkBuff,
        AuraHpBuff,
        ReviveOnce,
        PersistAcrossWaves,
        Custom
    }

    /// <summary>
    /// One ingredient line for a fusion recipe (type and/or trait match).
    /// </summary>
    [Serializable]
    public class FusionIngredientRule
    {
        [Tooltip("When false, any unit type matches (trait-only pick).")]
        public bool matchUnitType = true;
        public UnitType unitType = UnitType.Peasant;
        [Tooltip("TraitTag.None = ignore trait.")]
        public TraitTag requiredTrait = TraitTag.None;
        [Tooltip("Optional second trait (OR). TraitTag.None = unused.")]
        public TraitTag alternateTrait = TraitTag.None;
        public int count = 1;

        public bool Matches(RecruitInstance r)
        {
            if (r == null) return false;
            if (matchUnitType && r.unitType != unitType) return false;
            if (requiredTrait == TraitTag.None) return true;
            if (r.primaryTrait == requiredTrait) return true;
            if (alternateTrait != TraitTag.None && r.primaryTrait == alternateTrait) return true;
            return false;
        }

        public string FormatShort()
        {
            string typePart = matchUnitType ? unitType.ToString() : "Any";
            if (requiredTrait == TraitTag.None) return count > 1 ? $"{count}× {typePart}" : typePart;
            string traitPart = alternateTrait != TraitTag.None
                ? $"{requiredTrait}/{alternateTrait}"
                : requiredTrait.ToString();
            return count > 1 ? $"{count}× {typePart} ({traitPart})" : $"{typePart} ({traitPart})";
        }
    }

    /// <summary>
    /// Plug-and-play character data for Legends, Wave Bosses, and Demons.
    /// Create via Assets → Create → DemonKnight → Character Definition.
    /// </summary>
    [CreateAssetMenu(fileName = "CharacterDefinition", menuName = "DemonKnight/Character Definition", order = 10)]
    public class CharacterDefinition : ScriptableObject
    {
        [Header("Identity")]
        public string characterId;
        public string displayName;
        public CharacterCategory category = CharacterCategory.Legend;
        public UnitType unitType;
        public CharacterCombatRole role = CharacterCombatRole.Tank;
        public TraitTag trait = TraitTag.None;

        [Header("Visuals")]
        public Sprite portraitSprite;
        public Sprite battleSprite;
        [Tooltip("1.0 standard; 1.8–2.2 towering wave bosses.")]
        public float visualScale = 1f;

        [Header("Base Stats")]
        public int baseHP = 100;
        public int baseATK = 10;
        public float baseAPS = 1.5f;
        public bool canAttackFromBack;
        [Tooltip("Horde spawn budget cost (demons).")]
        public int powerCost = 3;

        [Header("Fusion / Wave Rules")]
        public List<FusionIngredientRule> fusionRecipe = new List<FusionIngredientRule>();
        [Tooltip("True for Mordred — stays hired across waves.")]
        public bool persistsAcrossWaves;
        [Tooltip("Boss schedule wave (3=Black Knight … 10=Nimue). 0 = not a scheduled boss.")]
        public int bossWaveNumber;

        [Header("Passive")]
        public CharacterPassiveType passiveType = CharacterPassiveType.None;
        public float passiveValue;
        [TextArea(2, 4)]
        public string passiveDescription;

        [Header("Codex / Meta-Unlock")]
        [TextArea(2, 5)]
        public string loreDescription;
        [TextArea(1, 3)]
        public string unlockRiddle;

        public bool HasFusionRecipe => fusionRecipe != null && fusionRecipe.Count > 0;

        public CombatRole ToCombatRole() => role switch
        {
            CharacterCombatRole.Backline => CombatRole.BacklineRanged,
            CharacterCombatRole.Hybrid => CombatRole.BacklineRanged,
            _ => CombatRole.FrontlineTank
        };

        public string FormatRecipeLine()
        {
            if (!HasFusionRecipe) return "(no fusion)";
            var parts = new List<string>();
            foreach (var rule in fusionRecipe)
            {
                if (rule == null) continue;
                parts.Add(rule.FormatShort());
            }
            return string.Join(" + ", parts);
        }

        public string FormatBossTooltip(float mult = 1f)
        {
            int hp = Mathf.RoundToInt(baseHP * mult);
            int atk = Mathf.RoundToInt(baseATK * mult);
            string passive = string.IsNullOrWhiteSpace(passiveDescription)
                ? (passiveType == CharacterPassiveType.None ? "No special trait." : passiveType.ToString())
                : passiveDescription;
            return $"<b>{displayName}</b>\nHP {hp} · ATK {atk} · APS {baseAPS:F1}\n{passive}";
        }

        public RecruitInstance CreateLegendRecruit(Sprite fallbackSprite = null)
        {
            var sp = portraitSprite != null ? portraitSprite : (battleSprite != null ? battleSprite : fallbackSprite);
            var roleCombat = ToCombatRole();
            bool ranged = canAttackFromBack
                          || roleCombat == CombatRole.BacklineRanged
                          || RecruitInstance.IsRangedUnitType(unitType);
            // Unique id every fuse — repeated Priest/etc. must not share one unitID.
            string baseId = string.IsNullOrEmpty(characterId) ? "hero_" + unitType : characterId;
            string uniqueId = $"{baseId}_{System.Guid.NewGuid().ToString("N").Substring(0, 8)}";
            return new RecruitInstance
            {
                unitID = uniqueId,
                characterName = string.IsNullOrEmpty(displayName) ? unitType.ToString() : displayName,
                unitType = unitType,
                randomAvatar = sp,
                headSprite = sp,
                bodySprite = battleSprite != null ? battleSprite : sp,
                maxHealth = baseHP,
                attackDamage = baseATK,
                attackSpeed = baseAPS,
                primaryTrait = trait,
                assignedRole = roleCombat,
                intelTierUnlocked = 3,
                baseHireCost = Mathf.Max(40, baseHP / 2),
                canAttackFromBack = ranged
            };
        }

        public CombatUnit CreateCombatUnit(float mult, int index = 0)
        {
            int h = Mathf.RoundToInt(baseHP * mult);
            int a = Mathf.RoundToInt(baseATK * mult);
            string id = string.IsNullOrEmpty(characterId)
                ? $"{category.ToString().ToLowerInvariant()}_{unitType}_{index}"
                : $"{characterId}_{index}";
            return new CombatUnit
            {
                unitID = id,
                unitName = string.IsNullOrEmpty(displayName) ? unitType.ToString() : displayName,
                unitType = unitType,
                isDemon = category == CharacterCategory.Boss || category == CharacterCategory.Demon,
                isUnorganized = category == CharacterCategory.Demon,
                powerValue = Mathf.Max(1, powerCost),
                maxHealth = h,
                currentHealth = h,
                attackDamage = a,
                attacksPerSecond = baseAPS,
                primaryTrait = trait,
                assignedRole = ToCombatRole(),
                canAttackFromBack = canAttackFromBack
            };
        }
    }
}
