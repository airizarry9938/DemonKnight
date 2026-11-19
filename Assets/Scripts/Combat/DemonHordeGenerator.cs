using System.Collections.Generic;
using UnityEngine;

namespace ArthurianAutobattler
{
    public class DemonHordeGenerator : MonoBehaviour
    {
        public int baseHordePowerBudget = 35;
        public int powerIncreasePerBattle = 22;
        public float statScalePerBattle = 0.14f;

        public List<CombatUnit> GenerateHordeForBattle(int battleNumber)
        {
            List<CombatUnit> horde = new List<CombatUnit>();
            int budget = baseHordePowerBudget + ((battleNumber - 1) * powerIncreasePerBattle);
            float mult = 1f + ((battleNumber - 1) * statScalePerBattle);
            int idx = 0;

            while (budget >= 3)
            {
                CombatUnit demon = PickChaoticDemon(budget, battleNumber, mult, idx++);
                horde.Add(demon);
                budget -= demon.powerValue;
            }

            return horde;
        }

        private CombatUnit PickChaoticDemon(int budget, int battleNum, float mult, int idx)
        {
            float chaosRoll = Random.value;

            if (budget >= 18 && battleNum >= 2 && chaosRoll < 0.40f)
            {
                return new CombatUnit
                {
                    unitID = $"demon_lord_{idx}",
                    unitName = "Feral Dread Lord",
                    unitType = UnitType.DemonDreadLord,
                    isDemon = true,
                    isUnorganized = true,
                    powerValue = 18,
                    maxHealth = Mathf.RoundToInt(185 * mult),
                    currentHealth = Mathf.RoundToInt(185 * mult),
                    attackDamage = Mathf.RoundToInt(26 * mult),
                    attacksPerSecond = Random.Range(0.9f, 1.8f)
                };
            }

            if (budget >= 7 && chaosRoll < 0.75f)
            {
                return new CombatUnit
                {
                    unitID = $"demon_hound_{idx}",
                    unitName = "Rabid Hellhound",
                    unitType = UnitType.DemonHellhound,
                    isDemon = true,
                    isUnorganized = true,
                    powerValue = 7,
                    maxHealth = Mathf.RoundToInt(65 * mult),
                    currentHealth = Mathf.RoundToInt(65 * mult),
                    attackDamage = Mathf.RoundToInt(12 * mult),
                    attacksPerSecond = Random.Range(1.3f, 2.0f)
                };
            }

            return new CombatUnit
            {
                unitID = $"demon_imp_{idx}",
                unitName = "Frenzied Imp",
                unitType = UnitType.DemonImp,
                isDemon = true,
                isUnorganized = true,
                powerValue = 3,
                maxHealth = Mathf.RoundToInt(22 * mult),
                currentHealth = Mathf.RoundToInt(22 * mult),
                attackDamage = Mathf.RoundToInt(6 * mult),
                attacksPerSecond = Random.Range(1.5f, 2.0f)
            };
        }

        public CombatUnit SpawnCorruptedLegendBoss(int battleNumber, float mult)
        {
            switch (battleNumber)
            {
                case 2:
                    return new CombatUnit
                    {
                        unitID = "boss_black_knight",
                        unitName = "The Black Knight",
                        unitType = UnitType.BossBlackKnight,
                        isDemon = true,
                        powerValue = 22,
                        maxHealth = Mathf.RoundToInt(210 * mult),
                        currentHealth = Mathf.RoundToInt(210 * mult),
                        attackDamage = Mathf.RoundToInt(24 * mult),
                        attacksPerSecond = 1.4f
                    };
                case 3:
                    return new CombatUnit
                    {
                        unitID = "boss_vortigern",
                        unitName = "Vortigern, Usurper King",
                        unitType = UnitType.BossVortigern,
                        isDemon = true,
                        powerValue = 28,
                        maxHealth = Mathf.RoundToInt(260 * mult),
                        currentHealth = Mathf.RoundToInt(260 * mult),
                        attackDamage = Mathf.RoundToInt(28 * mult),
                        attacksPerSecond = 1.3f
                    };
                case 4:
                    return new CombatUnit
                    {
                        unitID = "boss_green_knight",
                        unitName = "The Green Knight",
                        unitType = UnitType.BossGreenKnight,
                        isDemon = true,
                        powerValue = 34,
                        maxHealth = Mathf.RoundToInt(320 * mult),
                        currentHealth = Mathf.RoundToInt(320 * mult),
                        attackDamage = Mathf.RoundToInt(35 * mult),
                        attacksPerSecond = 1.2f
                    };
                case 5:
                    return new CombatUnit
                    {
                        unitID = "boss_accolon",
                        unitName = "Accolon of Gaul",
                        unitType = UnitType.BossAccolon,
                        isDemon = true,
                        powerValue = 40,
                        maxHealth = Mathf.RoundToInt(380 * mult),
                        currentHealth = Mathf.RoundToInt(380 * mult),
                        attackDamage = Mathf.RoundToInt(42 * mult),
                        attacksPerSecond = 1.8f
                    };
                default:
                    return new CombatUnit
                    {
                        unitID = "boss_nimue",
                        unitName = "Nimue, Queen of Thorns",
                        unitType = UnitType.BossNimue,
                        isDemon = true,
                        powerValue = 55,
                        maxHealth = Mathf.RoundToInt(550 * mult),
                        currentHealth = Mathf.RoundToInt(550 * mult),
                        attackDamage = Mathf.RoundToInt(45 * mult),
                        attacksPerSecond = 1.6f
                    };
            }
        }
    }
}
