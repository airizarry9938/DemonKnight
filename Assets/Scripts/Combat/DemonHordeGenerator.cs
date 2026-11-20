using System.Collections.Generic;
using UnityEngine;

namespace ArthurianAutobattler
{
    public class DemonHordeGenerator : MonoBehaviour
    {
        // Balance V2: 20 + (wave−1)×18 ; mult 1 + (wave−1)×0.12
        public int baseHordePowerBudget = 20;
        public int powerIncreasePerBattle = 18;
        public float statScalePerBattle = 0.12f;

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

            return MakeImp(idx, mult);
        }

        public static CombatUnit MakeImp(int idx, float mult)
        {
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

        /// <summary>
        /// Balance V2 boss roster — no boss on waves 1–2; wave 3+ schedule.
        /// Base stats × mult (wave 10+ Nimue scales further via mult).
        /// </summary>
        public CombatUnit SpawnCorruptedLegendBoss(int battleNumber, float mult)
        {
            switch (battleNumber)
            {
                case 3:
                    return Boss("boss_black_knight", "The Black Knight", UnitType.BossBlackKnight,
                        22, 210, 22, 1.4f, mult);
                case 4:
                    return Boss("boss_vortigern", "Vortigern, Usurper King", UnitType.BossVortigern,
                        28, 260, 26, 1.3f, mult);
                case 5:
                    return Boss("boss_green_knight", "The Green Knight", UnitType.BossGreenKnight,
                        34, 320, 32, 1.2f, mult);
                case 6:
                    return Boss("boss_accolon", "Accolon of Gaul", UnitType.BossAccolon,
                        40, 380, 38, 1.7f, mult);
                case 7:
                    return Boss("boss_meleagant", "Sir Meleagant", UnitType.BossMeleagant,
                        44, 440, 42, 1.6f, mult);
                case 8:
                    return Boss("boss_king_lot", "King Lot of Lothian", UnitType.BossKingLot,
                        48, 510, 46, 1.5f, mult);
                case 9:
                    return Boss("boss_questing_beast", "The Questing Beast", UnitType.BossQuestingBeast,
                        52, 600, 50, 1.9f, mult);
                default:
                    // Wave 10+: Nimue (scales with mult).
                    return Boss("boss_nimue", "Nimue, Queen of Thorns", UnitType.BossNimue,
                        55, 720, 55, 1.6f, mult);
            }
        }

        private static CombatUnit Boss(
            string id, string name, UnitType type, int power, int hp, int atk, float aps, float mult)
        {
            int h = Mathf.RoundToInt(hp * mult);
            int a = Mathf.RoundToInt(atk * mult);
            return new CombatUnit
            {
                unitID = id,
                unitName = name,
                unitType = type,
                isDemon = true,
                powerValue = power,
                maxHealth = h,
                currentHealth = h,
                attackDamage = a,
                attacksPerSecond = aps
            };
        }
    }
}
