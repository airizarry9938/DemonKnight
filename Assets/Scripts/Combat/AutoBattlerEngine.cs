using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace ArthurianAutobattler
{
    public class AutoBattlerEngine : MonoBehaviour
    {
        public DemonHordeGenerator hordeGenerator;
        public int currentBattleNumber = 1;
        public int finalBossWave = 6;
        public bool isBattling = false;
        public int frontlineSize = 6;

        private List<CombatUnit> playerArmy = new List<CombatUnit>();
        private List<CombatUnit> demonArmy = new List<CombatUnit>();
        private Dictionary<string, Sprite> spriteLookup = new Dictionary<string, Sprite>();

        public void StartAutoBattle(List<RecruitInstance> recruitedParty)
        {
            List<RecruitInstance> combatRoster = new List<RecruitInstance>();
            spriteLookup.Clear();

            foreach (var r in recruitedParty)
            {
                // Clone stats so permanent run buffs don't double-stack across waves
                RecruitInstance copy = new RecruitInstance
                {
                    unitID = r.unitID,
                    characterName = r.characterName,
                    unitType = r.unitType,
                    assignedRole = r.assignedRole,
                    maxHealth = r.maxHealth,
                    attackDamage = r.attackDamage,
                    attackSpeed = r.attackSpeed,
                    primaryTrait = r.primaryTrait,
                    hatedTrait = r.hatedTrait,
                    lovedTrait = r.lovedTrait
                };
                combatRoster.Add(copy);
                if (r.randomAvatar != null) spriteLookup[r.unitID] = r.randomAvatar;
            }

            PartySynergyEvaluator.ApplyBattleModifiers(combatRoster);
            PerkDraftManager.Instance?.ApplyCombatPerksToParty(combatRoster);
            ApplyLegendRivalries(combatRoster);

            playerArmy.Clear();
            foreach (var r in combatRoster)
            {
                CombatUnit cu = new CombatUnit
                {
                    unitID = r.unitID,
                    unitName = r.characterName,
                    unitType = r.unitType,
                    assignedRole = r.assignedRole,
                    isDemon = false,
                    primaryTrait = r.primaryTrait,
                    maxHealth = r.maxHealth,
                    currentHealth = r.maxHealth,
                    attackDamage = r.attackDamage,
                    attacksPerSecond = Mathf.Clamp(r.attackSpeed, 1.0f, 2.0f)
                };
                cu.ResetAttackCooldown();
                playerArmy.Add(cu);
            }

            demonArmy = hordeGenerator.GenerateHordeForBattle(currentBattleNumber);
            if (currentBattleNumber >= 2)
            {
                float mult = 1f + ((currentBattleNumber - 1) * hordeGenerator.statScalePerBattle);
                demonArmy.Insert(0, hordeGenerator.SpawnCorruptedLegendBoss(currentBattleNumber, mult));
            }

            foreach (var d in demonArmy) d.ResetAttackCooldown();
            TriggerBattleStartPassives();
            isBattling = true;
        }

        private void ApplyLegendRivalries(List<RecruitInstance> party)
        {
            bool hasArthur = party.Any(u => u.unitType == UnitType.KingArthur);
            bool hasLancelot = party.Any(u => u.unitType == UnitType.SirLancelot);
            bool hasMorgan = party.Any(u => u.unitType == UnitType.MorganLeFay);
            bool hasMordred = party.Any(u => u.unitType == UnitType.Mordred);

            foreach (var u in party)
            {
                if (hasArthur && hasLancelot)
                {
                    if (hasMorgan && (u.unitType == UnitType.KingArthur || u.unitType == UnitType.SirLancelot))
                        u.attackDamage = Mathf.Max(1, u.attackDamage - 4);
                    else if (u.unitType == UnitType.SirLancelot)
                        u.attackSpeed = Mathf.Min(2.0f, u.attackSpeed + 0.2f);
                }

                if (hasMordred && (u.primaryTrait == TraitTag.Loyalist || u.primaryTrait == TraitTag.Noble))
                    u.attackDamage = Mathf.Max(1, u.attackDamage - 3);
            }
        }

        private void TriggerBattleStartPassives()
        {
            if (playerArmy.Any(u => u.unitType == UnitType.LadyOfTheLake))
            {
                foreach (var ally in playerArmy.OrderBy(u => u.maxHealth).Take(3))
                    ally.shieldHealth += 45;
            }

            if (playerArmy.Any(u => u.unitType == UnitType.Captain))
            {
                foreach (var ally in playerArmy)
                    ally.attacksPerSecond = Mathf.Min(2.0f, ally.attacksPerSecond + 0.25f);
            }

            if (demonArmy.Any(d => d.unitType == UnitType.BossVortigern))
            {
                foreach (var ally in playerArmy.Where(u =>
                             u.primaryTrait == TraitTag.Coward || u.primaryTrait == TraitTag.Drunkard))
                    ally.attacksPerSecond = Mathf.Max(0.7f, ally.attacksPerSecond - 0.3f);
            }
        }

        private void Update()
        {
            if (!isBattling) return;

            foreach (var u in playerArmy.Where(u => u.IsAlive && u.petrifiedTimer > 0f))
                u.petrifiedTimer -= Time.deltaTime;

            var aliveHumans = playerArmy.Where(u => u.IsAlive).Take(frontlineSize).ToList();
            var aliveDemons = demonArmy.Where(u => u.IsAlive).Take(frontlineSize).ToList();

            BattlefieldVisualizer.Instance?.SyncFrontlineVisuals(aliveHumans, aliveDemons, spriteLookup);

            if (aliveHumans.Count == 0 || aliveDemons.Count == 0)
            {
                EndBattle(aliveHumans.Count > 0);
                return;
            }

            ProcessSideActions(aliveHumans, aliveDemons);
            aliveDemons = demonArmy.Where(u => u.IsAlive).Take(frontlineSize).ToList();
            if (aliveDemons.Count > 0)
                ProcessSideActions(aliveDemons, GetValidTargetsForDemons(aliveHumans));
        }

        private List<CombatUnit> GetValidTargetsForDemons(List<CombatUnit> aliveHumans)
        {
            var tanks = aliveHumans.Where(u => u.assignedRole == CombatRole.FrontlineTank).ToList();
            return tanks.Count > 0 ? tanks : aliveHumans;
        }

        private void ProcessSideActions(List<CombatUnit> attackers, List<CombatUnit> defenders)
        {
            foreach (var attacker in attackers)
            {
                if (!attacker.IsAlive || attacker.petrifiedTimer > 0f || defenders.Count == 0) continue;

                attacker.battleElapsed += Time.deltaTime;
                attacker.attackTimer -= Time.deltaTime;
                if (attacker.attackTimer <= 0f)
                {
                    ExecuteUnitAction(attacker, attackers, defenders);
                    attacker.ResetAttackCooldown();
                }
            }
        }

        private void ExecuteUnitAction(CombatUnit attacker, List<CombatUnit> allies, List<CombatUnit> enemies)
        {
            int dmg = attacker.attackDamage;
            if (attacker.unitType == UnitType.SirGawain && attacker.battleElapsed <= 8.0f) dmg *= 2;

            switch (attacker.unitType)
            {
                case UnitType.Priest:
                {
                    var injured = allies
                        .Where(a => a.IsAlive && a.currentHealth < a.maxHealth)
                        .OrderBy(a => (float)a.currentHealth / a.maxHealth)
                        .FirstOrDefault();
                    if (injured != null)
                    {
                        injured.currentHealth = Mathf.Min(injured.maxHealth, injured.currentHealth + dmg);
                        BattlefieldVisualizer.Instance?.SpawnPopup(injured, $"+{dmg}", Color.green);
                    }
                    else
                    {
                        DealDamage(attacker, enemies[Random.Range(0, enemies.Count)], Mathf.Max(1, dmg / 2));
                    }
                    break;
                }
                case UnitType.KingArthur:
                    foreach (var t in enemies.OrderBy(_ => Random.value).Take(2))
                        DealDamage(attacker, t, dmg);
                    break;
                case UnitType.Merlin:
                    foreach (var t in enemies)
                        DealDamage(attacker, t, dmg);
                    break;
                case UnitType.MorganLeFay:
                {
                    var hexTarget = enemies[Random.Range(0, enemies.Count)];
                    hexTarget.isHexed = true;
                    BattlefieldVisualizer.Instance?.SpawnPopup(hexTarget, "HEXED!", new Color(0.75f, 0.2f, 1f));
                    DealDamage(attacker, hexTarget, dmg);
                    break;
                }
                case UnitType.SirGalahad:
                {
                    var galTarget = enemies[Random.Range(0, enemies.Count)];
                    int dealt = DealDamage(attacker, galTarget, dmg);
                    int heal = Mathf.Max(1, Mathf.RoundToInt(dealt * 0.25f));
                    foreach (var ally in allies.Where(a => a != attacker && a.IsAlive))
                        ally.currentHealth = Mathf.Min(ally.maxHealth, ally.currentHealth + heal);
                    break;
                }
                case UnitType.Mordred:
                {
                    var morTarget = enemies[Random.Range(0, enemies.Count)];
                    bool isBoss = morTarget.unitType.ToString().StartsWith("Boss");
                    DealDamage(attacker, morTarget, isBoss ? dmg + 15 : dmg);
                    break;
                }
                case UnitType.BossAccolon:
                {
                    var accTarget = enemies[Random.Range(0, enemies.Count)];
                    int accDealt = DealDamage(attacker, accTarget, dmg);
                    if (!attacker.isHexed)
                        attacker.currentHealth = Mathf.Min(attacker.maxHealth, attacker.currentHealth + Mathf.RoundToInt(accDealt * 0.30f));
                    break;
                }
                case UnitType.BossNimue:
                {
                    DealDamage(attacker, enemies[Random.Range(0, enemies.Count)], dmg);

                    if (Random.value < 0.35f)
                    {
                        var strongest = enemies
                            .Where(e => e.IsAlive && e.petrifiedTimer <= 0f)
                            .OrderByDescending(e => e.attackDamage)
                            .FirstOrDefault();
                        if (strongest != null)
                        {
                            strongest.petrifiedTimer = 2.5f;
                            BattlefieldVisualizer.Instance?.SpawnPopup(strongest, "PETRIFIED!", Color.gray);
                        }
                    }

                    bool immune = allies.Any(a =>
                        a.unitType == UnitType.KingArthur || a.unitType == UnitType.SirGalahad);
                    if (!immune && Random.value < 0.20f)
                    {
                        var traitor = enemies.FirstOrDefault(e =>
                            e.IsAlive && (e.primaryTrait == TraitTag.Coward || e.primaryTrait == TraitTag.Outlaw));
                        if (traitor != null)
                        {
                            traitor.isDemon = true;
                            playerArmy.Remove(traitor);
                            demonArmy.Add(traitor);
                            BattlefieldVisualizer.Instance?.SpawnPopup(traitor, "SEDUCED!", Color.magenta);
                        }
                    }
                    break;
                }
                default:
                {
                    CombatUnit target = (!attacker.isDemon && attacker.assignedRole == CombatRole.BacklineRanged)
                        ? enemies.OrderBy(e => e.currentHealth).First()
                        : enemies[Random.Range(0, enemies.Count)];
                    DealDamage(attacker, target, dmg);
                    break;
                }
            }
        }

        private int DealDamage(CombatUnit attacker, CombatUnit target, int rawDamage)
        {
            int finalDamage = rawDamage;

            if (target.unitType == UnitType.BossBlackKnight
                && (attacker.unitType == UnitType.Peasant || attacker.unitType == UnitType.Warrior))
            {
                finalDamage = Mathf.Max(1, finalDamage / 2);
            }

            if (target.shieldHealth > 0)
            {
                int absorbed = Mathf.Min(target.shieldHealth, finalDamage);
                target.shieldHealth -= absorbed;
                finalDamage -= absorbed;
                BattlefieldVisualizer.Instance?.SpawnPopup(target, $"SHIELD -{absorbed}", Color.cyan);
            }

            target.currentHealth -= finalDamage;
            BattlefieldVisualizer.Instance?.TriggerAttackVisual(attacker, target);
            if (finalDamage > 0)
                BattlefieldVisualizer.Instance?.SpawnPopup(target, $"-{finalDamage}", Color.red);

            if (target.currentHealth <= 0) HandleUnitDeath(target);
            return finalDamage;
        }

        private void HandleUnitDeath(CombatUnit fallen)
        {
            if (fallen.unitType == UnitType.BossGreenKnight && !fallen.hasRevived)
            {
                fallen.hasRevived = true;
                fallen.currentHealth = Mathf.RoundToInt(fallen.maxHealth * 0.40f);
                fallen.attackDamage += 10;
                BattlefieldVisualizer.Instance?.SpawnPopup(fallen, "REVIVED!", Color.yellow);
                return;
            }

            if (!fallen.isDemon)
            {
                foreach (var lancelot in playerArmy.Where(u => u.IsAlive && u.unitType == UnitType.SirLancelot))
                    lancelot.attackDamage += 5;
            }
        }

        private void EndBattle(bool playerWon)
        {
            isBattling = false;
            int goldEarned = playerWon ? (35 + currentBattleNumber * 15) : 10;

            if (GrudgeSaveManager.Instance != null)
            {
                foreach (var u in playerArmy)
                    GrudgeSaveManager.Instance.RecordBattleOutcome(u.unitID, !u.IsAlive, !playerWon);
            }

            if (playerWon && currentBattleNumber >= finalBossWave)
            {
                MerlinResurrectionManager.Instance?.TriggerEndOfRunResurrection(currentBattleNumber, true);
            }
            else if (!playerWon)
            {
                MerlinResurrectionManager.Instance?.TriggerEndOfRunResurrection(currentBattleNumber - 1, false);
            }
            else
            {
                currentBattleNumber++;
                var ui = FindFirstObjectByType<TownSplitUIController>();
                if (ui != null) ui.playerGold += goldEarned;
                PerkDraftManager.Instance?.OpenPerkDraft();
            }
        }
    }
}
