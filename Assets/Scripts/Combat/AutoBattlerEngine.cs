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
            BattlefieldVisualizer.Instance?.ClearAllActors();

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
                copy.isSirenEnchantress = r.isSirenEnchantress;
                combatRoster.Add(copy);
                if (r.randomAvatar != null) spriteLookup[r.unitID] = r.randomAvatar;
            }

            RelicManager.Instance?.ApplyMorganasMirrorClone(combatRoster);
            PartySynergyEvaluator.ApplyBattleModifiers(combatRoster);
            PerkDraftManager.Instance?.ApplyCombatPerksToParty(combatRoster);
            ApplyLegendRivalries(combatRoster);
            ApplyUsurpationStarterLegends(combatRoster);

            playerArmy.Clear();
            bool hellfire = DemonicPactManager.Instance != null
                            && DemonicPactManager.Instance.ActivePact == DemonicPactID.HellfireSteel;

            foreach (var r in combatRoster)
            {
                int atk = r.attackDamage;
                float aps = Mathf.Clamp(r.attackSpeed, 1.0f, 2.0f);
                if (hellfire && (r.unitType == UnitType.Warrior || r.unitType == UnitType.Knight))
                {
                    atk *= 2;
                    aps = 2.0f;
                }

                CombatUnit cu = new CombatUnit
                {
                    unitID = r.unitID,
                    unitName = r.characterName,
                    unitType = r.unitType,
                    assignedRole = r.assignedRole,
                    isDemon = false,
                    isSirenEnchantress = r.isSirenEnchantress,
                    primaryTrait = r.primaryTrait,
                    maxHealth = r.maxHealth,
                    currentHealth = r.maxHealth,
                    attackDamage = atk,
                    attacksPerSecond = aps,
                    cannotBeHealed = hellfire && (r.unitType == UnitType.Warrior || r.unitType == UnitType.Knight),
                    maxHpDrainPerSecond = hellfire && (r.unitType == UnitType.Warrior || r.unitType == UnitType.Knight)
                        ? r.maxHealth * 0.05f : 0f
                };
                cu.ResetAttackCooldown();
                playerArmy.Add(cu);
            }

            demonArmy = hordeGenerator.GenerateHordeForBattle(currentBattleNumber);
            ApplyEndlessHourglassPressure(demonArmy);
            SpawnLustSirensIntoHorde(demonArmy);

            if (currentBattleNumber >= 2)
            {
                float mult = 1f + ((currentBattleNumber - 1) * hordeGenerator.statScalePerBattle);
                demonArmy.Insert(0, hordeGenerator.SpawnCorruptedLegendBoss(currentBattleNumber, mult));
            }

            foreach (var d in demonArmy) d.ResetAttackCooldown();
            TriggerBattleStartPassives();
            isBattling = true;
        }

        private void ApplyUsurpationStarterLegends(List<RecruitInstance> roster)
        {
            if (currentBattleNumber != 1) return;
            if (DemonicPactManager.Instance == null
                || DemonicPactManager.Instance.ActivePact != DemonicPactID.Usurpation) return;
            if (roster.Any(u => u.unitType == UnitType.Mordred)) return;

            roster.Add(new RecruitInstance
            {
                unitID = "pact_mordred", characterName = "Mordred (Pact)", unitType = UnitType.Mordred,
                maxHealth = 240, attackDamage = 34, attackSpeed = 1.8f, primaryTrait = TraitTag.Outlaw,
                assignedRole = CombatRole.FrontlineTank, intelTierUnlocked = 3
            });
            roster.Add(new RecruitInstance
            {
                unitID = "pact_morgan", characterName = "Morgan le Fay (Pact)", unitType = UnitType.MorganLeFay,
                maxHealth = 165, attackDamage = 22, attackSpeed = 1.5f, primaryTrait = TraitTag.Outlaw,
                assignedRole = CombatRole.BacklineRanged, intelTierUnlocked = 3
            });
        }

        private void ApplyEndlessHourglassPressure(List<CombatUnit> horde)
        {
            if (DemonicPactManager.Instance == null || GameLoopManager.Instance == null) return;
            float spent = GameLoopManager.Instance.townSecondsSpentThisPhase;
            int extraLords = DemonicPactManager.Instance.GetEndlessHourglassExtraDreadLords(spent);
            float mult = 1f + ((currentBattleNumber - 1) * hordeGenerator.statScalePerBattle);
            for (int i = 0; i < extraLords; i++)
            {
                horde.Add(new CombatUnit
                {
                    unitID = $"hourglass_lord_{i}", unitName = "Hourglass Dread Lord",
                    unitType = UnitType.DemonDreadLord, isDemon = true, isUnorganized = true, powerValue = 18,
                    maxHealth = Mathf.RoundToInt(185 * mult), currentHealth = Mathf.RoundToInt(185 * mult),
                    attackDamage = Mathf.RoundToInt(26 * mult), attacksPerSecond = Random.Range(0.9f, 1.8f)
                });
            }
        }

        private void SpawnLustSirensIntoHorde(List<CombatUnit> horde)
        {
            if (!SomersetRunes.IsSinActive(SinRuneID.Lust)) return;
            int count = Mathf.Max(1, Mathf.RoundToInt(horde.Count * 0.25f));
            float mult = 1f + ((currentBattleNumber - 1) * hordeGenerator.statScalePerBattle);
            for (int i = 0; i < count; i++)
            {
                horde.Add(new CombatUnit
                {
                    unitID = $"siren_{i}", unitName = "Siren Enchantress",
                    unitType = UnitType.SirenEnchantress, isDemon = true, isSirenEnchantress = true,
                    isUnorganized = true, powerValue = 8,
                    maxHealth = Mathf.RoundToInt(70 * mult), currentHealth = Mathf.RoundToInt(70 * mult),
                    attackDamage = Mathf.RoundToInt(10 * mult), attacksPerSecond = Random.Range(1.2f, 1.8f)
                });
            }
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

            foreach (var u in playerArmy.Where(u => u.IsAlive))
            {
                if (u.petrifiedTimer > 0f) u.petrifiedTimer -= Time.deltaTime;
                if (u.charmedTimer > 0f) u.charmedTimer -= Time.deltaTime;
                if (u.maxHpDrainPerSecond > 0f)
                {
                    u.currentHealth -= Mathf.RoundToInt(u.maxHpDrainPerSecond * Time.deltaTime);
                    if (u.currentHealth <= 0) HandleUnitDeath(u);
                }
            }

            TrySirenCharmTicks();

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

        private void TrySirenCharmTicks()
        {
            bool anySiren = demonArmy.Any(d => d.IsAlive && d.isSirenEnchantress)
                            || playerArmy.Any(u => u.IsAlive && u.isSirenEnchantress);
            if (!anySiren) return;

            foreach (var u in playerArmy.Where(u => u.IsAlive && CombatUnit.IsMaleFrontlineCharmable(u.unitType)))
            {
                if (CombatUnit.IsImmuneToSirenCharm(u.unitType)) continue;
                if (u.charmedTimer > 0f) continue;
                if (Random.value < 0.25f * Time.deltaTime) // ~chance per second scaled
                {
                    u.charmedTimer = 1.0f;
                    BattlefieldVisualizer.Instance?.SpawnPopup(u, "CHARMED!", new Color(1f, 0.4f, 0.7f));
                }
            }
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
                if (!attacker.IsAlive || attacker.petrifiedTimer > 0f || attacker.charmedTimer > 0f || defenders.Count == 0)
                    continue;

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
                    int healTargets = (RelicManager.Instance != null
                                       && RelicManager.Instance.Has(RelicID.ChaliceOfTheGrail)) ? 2 : 1;
                    var injuredList = allies
                        .Where(a => a.IsAlive && !a.cannotBeHealed && a.currentHealth < a.maxHealth)
                        .OrderBy(a => (float)a.currentHealth / a.maxHealth)
                        .Take(healTargets)
                        .ToList();

                    if (injuredList.Count > 0)
                    {
                        foreach (var injured in injuredList)
                        {
                            injured.currentHealth = Mathf.Min(injured.maxHealth, injured.currentHealth + dmg);
                            BattlefieldVisualizer.Instance?.SpawnPopup(injured, $"+{dmg}", Color.green);
                        }
                    }
                    else if (enemies.Count > 0)
                    {
                        DealDamage(attacker, enemies[Random.Range(0, enemies.Count)], Mathf.Max(1, dmg / 2));
                    }
                    break;
                }
                case UnitType.LadyOfTheLake:
                {
                    // Chalice of the Grail: Lady also heals 2 allies per tick; otherwise default attack.
                    if (RelicManager.Instance != null && RelicManager.Instance.Has(RelicID.ChaliceOfTheGrail))
                    {
                        foreach (var injured in allies
                                     .Where(a => a.IsAlive && !a.cannotBeHealed && a.currentHealth < a.maxHealth)
                                     .OrderBy(a => (float)a.currentHealth / a.maxHealth)
                                     .Take(2))
                        {
                            injured.currentHealth = Mathf.Min(injured.maxHealth, injured.currentHealth + dmg);
                            BattlefieldVisualizer.Instance?.SpawnPopup(injured, $"+{dmg}", Color.green);
                        }
                    }
                    else if (enemies.Count > 0)
                    {
                        DealDamage(attacker, enemies[Random.Range(0, enemies.Count)], dmg);
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

            // Scabbard of Excalibur: Arthur/Knights survive fatal hit once per battle at 1 HP.
            if (!fallen.isDemon
                && RelicManager.Instance != null
                && RelicManager.Instance.Has(RelicID.ScabbardOfExcalibur)
                && !fallen.scabbardUsed
                && (fallen.unitType == UnitType.KingArthur || fallen.unitType == UnitType.Knight))
            {
                fallen.scabbardUsed = true;
                fallen.currentHealth = 1;
                BattlefieldVisualizer.Instance?.SpawnPopup(fallen, "SCABBARD!", Color.yellow);
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
            BattlefieldVisualizer.Instance?.ClearAllActors();
            int goldEarned = playerWon ? (35 + currentBattleNumber * 15) : 10;

            if (GrudgeSaveManager.Instance != null)
            {
                foreach (var u in playerArmy)
                    GrudgeSaveManager.Instance.RecordBattleOutcome(u.unitID, !u.IsAlive, !playerWon);
            }

            // Silver Tongue: sacrifice highest-ATK survivor after every wave (+40g grudge).
            if (playerWon
                && DemonicPactManager.Instance != null
                && DemonicPactManager.Instance.ActivePact == DemonicPactID.SilverTongue)
            {
                var victim = playerArmy.Where(u => u.IsAlive).OrderByDescending(u => u.attackDamage).FirstOrDefault();
                if (victim != null)
                {
                    victim.currentHealth = 0;
                    GrudgeSaveManager.Instance?.ModifyGrudgeStaging(victim.unitID, 40);
                    var real = GameLoopManager.Instance?.recruitedParty.Find(r => r.unitID == victim.unitID);
                    if (real != null) GameLoopManager.Instance.recruitedParty.Remove(real);
                }
            }

            // Lust: hired Sirens drain 15g per wave.
            if (playerWon && SomersetRunes.IsSinActive(SinRuneID.Lust))
            {
                int sirens = GameLoopManager.Instance != null
                    ? GameLoopManager.Instance.recruitedParty.Count(r => r.isSirenEnchantress) : 0;
                var uiDrain = FindFirstObjectByType<TownSplitUIController>();
                if (uiDrain != null) uiDrain.playerGold = Mathf.Max(0, uiDrain.playerGold - sirens * 15);
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
                RelicManager.Instance?.TryAwardRelicForWave(currentBattleNumber);
                currentBattleNumber++;
                var ui = FindFirstObjectByType<TownSplitUIController>();
                if (ui != null) ui.playerGold += goldEarned;
                PerkDraftManager.Instance?.OpenPerkDraft();
            }
        }
    }
}
