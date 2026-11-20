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
            var db = CharacterDatabase.Load();
            float chaosRoll = Random.value;

            CharacterDefinition dread = db?.GetByUnitType(UnitType.DemonDreadLord);
            CharacterDefinition hound = db?.GetByUnitType(UnitType.DemonHellhound);
            CharacterDefinition imp = db?.GetByUnitType(UnitType.DemonImp);

            int dreadCost = dread != null ? Mathf.Max(1, dread.powerCost) : 18;
            int houndCost = hound != null ? Mathf.Max(1, hound.powerCost) : 7;

            if (budget >= dreadCost && battleNum >= 2 && chaosRoll < 0.40f)
            {
                if (dread != null) return dread.CreateCombatUnit(mult, idx);
                return LegacyDemon("demon_lord_", "Feral Dread Lord", UnitType.DemonDreadLord, 18, 185, 26, mult, idx);
            }

            if (budget >= houndCost && chaosRoll < 0.75f)
            {
                if (hound != null) return hound.CreateCombatUnit(mult, idx);
                return LegacyDemon("demon_hound_", "Rabid Hellhound", UnitType.DemonHellhound, 7, 65, 12, mult, idx);
            }

            if (imp != null) return imp.CreateCombatUnit(mult, idx);
            return MakeImp(idx, mult);
        }

        public static CombatUnit MakeImp(int idx, float mult)
        {
            var db = CharacterDatabase.Load();
            var imp = db?.GetByUnitType(UnitType.DemonImp);
            if (imp != null) return imp.CreateCombatUnit(mult, idx);
            return LegacyDemon("demon_imp_", "Frenzied Imp", UnitType.DemonImp, 3, 22, 6, mult, idx);
        }

        static CombatUnit LegacyDemon(
            string idPrefix, string name, UnitType type, int power, int hp, int atk, float mult, int idx)
        {
            int h = Mathf.RoundToInt(hp * mult);
            int a = Mathf.RoundToInt(atk * mult);
            return new CombatUnit
            {
                unitID = $"{idPrefix}{idx}",
                unitName = name,
                unitType = type,
                isDemon = true,
                isUnorganized = true,
                powerValue = power,
                maxHealth = h,
                currentHealth = h,
                attackDamage = a,
                attacksPerSecond = Random.Range(1.2f, 2.0f)
            };
        }

        /// <summary>
        /// Balance V2 boss roster — no boss on waves 1–2; wave 3+ from CharacterDatabase.
        /// </summary>
        public CombatUnit SpawnCorruptedLegendBoss(int battleNumber, float mult)
        {
            var db = CharacterDatabase.Load();
            var def = db?.GetBossForWave(battleNumber);
            if (def != null)
                return def.CreateCombatUnit(mult);

            // Legacy fallback if CharacterDatabase not seeded yet.
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
