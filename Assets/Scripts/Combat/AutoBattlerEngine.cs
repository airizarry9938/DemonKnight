using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace ArthurianAutobattler
{
    public class AutoBattlerEngine : MonoBehaviour
    {
        public DemonHordeGenerator hordeGenerator;
        public int currentBattleNumber = 1;
        public int finalBossWave = 10;
        public bool isBattling = false;
        /// <summary>Blocks re-entrant EndBattle (double victory wire / double Next Night).</summary>
        private bool endingBattle;
        public int frontlineSize = 6;

        [Header("Pacing")]
        [Tooltip("Combat time scale. 1 = full speed; lower = slower/more tense. Default 0.38.")]
        [Range(0.15f, 1.25f)]
        public float combatPace = 0.38f;
        [Tooltip("Extra pause (seconds, unscaled by combatPace) after each attack resolves.")]
        [Range(0f, 0.75f)]
        public float attackResolvePause = 0.12f;

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
                    lovedTrait = r.lovedTrait,
                    // Warriors never fight from BACK — strip stale kits from older saves.
                    canAttackFromBack = r.canAttackFromBack && r.unitType != UnitType.Warrior
                };
                copy.isSirenEnchantress = r.isSirenEnchantress;
                combatRoster.Add(copy);
                if (r.randomAvatar != null) spriteLookup[r.unitID] = r.randomAvatar;
            }

            SomersetRunes.ApplyMorganasMirrorClone(combatRoster);
            PartySynergyEvaluator.ApplyBattleModifiers(combatRoster);
            PerkDraftManager.Instance?.ApplyCombatPerksToParty(combatRoster);
            ApplyLegendRivalries(combatRoster);
            ApplyUsurpationStarterLegends(combatRoster);

            playerArmy.Clear();
            bool hellfire = SomersetRunes.IsPact(DemonicPactID.HellfireSteel);

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
                    canAttackFromBack = r.canAttackFromBack && r.unitType != UnitType.Warrior,
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

            // Balance V2: bosses start wave 3+.
            if (currentBattleNumber >= 3)
            {
                float mult = 1f + ((currentBattleNumber - 1) * hordeGenerator.statScalePerBattle);
                var waveBoss = hordeGenerator.SpawnCorruptedLegendBoss(currentBattleNumber, mult);
                demonArmy.Insert(0, waveBoss);
                // King Lot: summons 2 Frenzied Imps on spawn.
                if (waveBoss.unitType == UnitType.BossKingLot)
                {
                    demonArmy.Insert(1, DemonHordeGenerator.MakeImp(9001, mult));
                    demonArmy.Insert(2, DemonHordeGenerator.MakeImp(9002, mult));
                }
            }

            foreach (var d in demonArmy) d.ResetAttackCooldown();
            TriggerBattleStartPassives();
            isBattling = true;

            // Boss waves: bottom-of-frame dialog before steel flies.
            // Requires Assets/Scripts/Combat/BattleBossDialogUI.cs (on Sync ScriptPaths).
            var dialogBoss = demonArmy.FirstOrDefault(d => d != null && BattleBossDialogUI.IsBossType(d.unitType));
            if (dialogBoss != null)
                BattleBossDialogUI.ShowForBoss(dialogBoss);
            else
                BattleBossDialogUI.Hide();
        }

        private void ApplyUsurpationStarterLegends(List<RecruitInstance> roster)
        {
            if (!SomersetRunes.IsPact(DemonicPactID.Usurpation)) return;
            // Scale 70% at wave 1 → 100% by wave 4 (Balance V2).
            float scale = Mathf.Lerp(0.70f, 1f, Mathf.Clamp01((currentBattleNumber - 1) / 3f));

            if (currentBattleNumber == 1 && !roster.Any(u => u.unitType == UnitType.Mordred))
            {
                roster.Add(new RecruitInstance
                {
                    unitID = "pact_mordred", characterName = "Mordred (Pact)", unitType = UnitType.Mordred,
                    maxHealth = Mathf.RoundToInt(260 * scale), attackDamage = Mathf.RoundToInt(36 * scale),
                    attackSpeed = 1.8f, primaryTrait = TraitTag.Outlaw,
                    assignedRole = CombatRole.FrontlineTank, intelTierUnlocked = 3
                });
            }
            if (currentBattleNumber == 1 && !roster.Any(u => u.unitType == UnitType.MorganLeFay && u.unitID == "pact_morgan"))
            {
                roster.Add(new RecruitInstance
                {
                    unitID = "pact_morgan", characterName = "Morgan le Fay (Pact)", unitType = UnitType.MorganLeFay,
                    maxHealth = Mathf.RoundToInt(175 * scale), attackDamage = Mathf.RoundToInt(24 * scale),
                    attackSpeed = 1.5f, primaryTrait = TraitTag.Outlaw,
                    assignedRole = CombatRole.BacklineRanged, intelTierUnlocked = 3,
                    canAttackFromBack = true
                });
            }

            // Persist pact Mordred into hired party so he survives wave disband.
            var loop = GameLoopManager.Instance;
            if (loop?.recruitedParty != null
                && currentBattleNumber == 1
                && !loop.recruitedParty.Any(u => u != null && u.unitType == UnitType.Mordred))
            {
                var m = roster.FirstOrDefault(u => u.unitType == UnitType.Mordred);
                if (m != null)
                    loop.recruitedParty.Add(CardFusionSystem.CloneForFusionMemory(m));
            }
        }

        private void ApplyEndlessHourglassPressure(List<CombatUnit> horde)
        {
            if (GameLoopManager.Instance == null) return;
            float spent = GameLoopManager.Instance.townSecondsSpentThisPhase;
            int extraLords = SomersetRunes.GetEndlessHourglassExtraDreadLords(spent);
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

        private float _postAttackPauseTimer;

        private void Update()
        {
            if (!isBattling) return;

            // Hold combat while the boss intro dialog is across the bottom of the frame.
            if (BattleBossDialogUI.IsBlockingCombat)
            {
                BattlefieldVisualizer.Instance?.SyncFrontlineVisuals(
                    playerArmy.Where(u => u.IsAlive).ToList(),
                    demonArmy.Where(u => u.IsAlive).ToList(),
                    spriteLookup);
                return;
            }

            float pace = Mathf.Clamp(combatPace, 0.15f, 1.25f);
            float dt = Time.deltaTime * pace;

            // Brief beat after an attack for readability / tension.
            if (_postAttackPauseTimer > 0f)
            {
                _postAttackPauseTimer -= Time.deltaTime;
                BattlefieldVisualizer.Instance?.SyncFrontlineVisuals(
                    playerArmy.Where(u => u.IsAlive).ToList(),
                    demonArmy.Where(u => u.IsAlive).ToList(),
                    spriteLookup);
                return;
            }

            foreach (var u in playerArmy.Where(u => u.IsAlive))
            {
                if (u.petrifiedTimer > 0f) u.petrifiedTimer -= dt;
                if (u.charmedTimer > 0f) u.charmedTimer -= dt;
                if (u.maxHpDrainPerSecond > 0f)
                {
                    u.currentHealth -= Mathf.RoundToInt(u.maxHpDrainPerSecond * dt);
                    if (u.currentHealth <= 0) HandleUnitDeath(u);
                }
            }

            TrySirenCharmTicks(dt);

            var allHumans = playerArmy.Where(u => u.IsAlive).ToList();
            var allDemons = demonArmy.Where(u => u.IsAlive).ToList();
            // Visuals: every living party member / hero / demon / boss (HP bars + color blocks).
            BattlefieldVisualizer.Instance?.SyncFrontlineVisuals(allHumans, allDemons, spriteLookup);

            // Acting slots: fill FRONT (Tank/Unassigned) first, then BACK (Ranged).
            var actingHumans = SelectActingUnitsByFormation(allHumans, frontlineSize);
            var actingDemons = allDemons.Take(frontlineSize).ToList();

            if (allHumans.Count == 0 || allDemons.Count == 0)
            {
                EndBattle(allHumans.Count > 0);
                return;
            }

            // Humans attack demons (no formation on horde).
            bool struck = ProcessSideActions(actingHumans, actingDemons, dt);
            actingDemons = demonArmy.Where(u => u.IsAlive).Take(frontlineSize).ToList();
            if (actingDemons.Count > 0)
            {
                // Demons MUST target FRONT before BACK — pass all living humans into the filter.
                struck |= ProcessSideActions(actingDemons, GetFrontRowTargets(allHumans), dt);
            }
            if (struck && attackResolvePause > 0f)
                _postAttackPauseTimer = attackResolvePause;
        }

        private void TrySirenCharmTicks(float dt)
        {
            bool anySiren = demonArmy.Any(d => d.IsAlive && d.isSirenEnchantress)
                            || playerArmy.Any(u => u.IsAlive && u.isSirenEnchantress);
            if (!anySiren) return;

            // Sirens charm the FRONT row first (same formation gate as attacks).
            foreach (var u in playerArmy.Where(u =>
                         u.IsAlive && u.IsFrontRow && CombatUnit.IsMaleFrontlineCharmable(u.unitType)))
            {
                if (CombatUnit.IsImmuneToSirenCharm(u.unitType)) continue;
                if (u.charmedTimer > 0f) continue;
                if (Random.value < 0.25f * dt) // ~chance per second scaled by combatPace
                {
                    u.charmedTimer = 1.0f;
                    BattlefieldVisualizer.Instance?.SpawnPopup(u, "CHARMED!", new Color(1f, 0.4f, 0.7f));
                }
            }
        }

        /// <summary>
        /// FRONT-row first (Tank + Unassigned). BACK (Ranged) is only legal once every
        /// front-row ally is down — so formation actually tanks hits in AutoBattle.
        /// </summary>
        private static List<CombatUnit> GetFrontRowTargets(List<CombatUnit> aliveHumans)
        {
            if (aliveHumans == null || aliveHumans.Count == 0)
                return new List<CombatUnit>();

            var living = aliveHumans.Where(u => u != null && u.IsAlive).ToList();
            var front = living.Where(u => u.IsFrontRow).ToList();
            if (front.Count > 0) return front;
            // Front wiped — backline can finally be hit.
            return living.Where(u => u.IsBackRow).ToList();
        }

        /// <summary>Prefer FRONT actors, then fill remaining slots with BACK (Ranged).</summary>
        private static List<CombatUnit> SelectActingUnitsByFormation(List<CombatUnit> alive, int cap)
        {
            var result = new List<CombatUnit>();
            if (alive == null || cap <= 0) return result;
            foreach (var u in alive)
            {
                if (u == null || !u.IsAlive || !u.IsFrontRow) continue;
                result.Add(u);
                if (result.Count >= cap) return result;
            }
            foreach (var u in alive)
            {
                if (u == null || !u.IsAlive || !u.IsBackRow) continue;
                result.Add(u);
                if (result.Count >= cap) return result;
            }
            return result;
        }

        /// <summary>Pick one living target from a list that is already front-preferred (or any pool).</summary>
        private static CombatUnit PickRandomTarget(List<CombatUnit> enemies)
        {
            if (enemies == null || enemies.Count == 0) return null;
            var living = enemies.Where(e => e != null && e.IsAlive).ToList();
            if (living.Count == 0) return null;
            return living[Random.Range(0, living.Count)];
        }

        /// <summary>Meleagant prioritizes BACK-row (ranged) targets when any remain.</summary>
        private static CombatUnit PickMeleagantTarget(List<CombatUnit> enemies)
        {
            if (enemies == null || enemies.Count == 0) return null;
            var living = enemies.Where(e => e != null && e.IsAlive).ToList();
            if (living.Count == 0) return null;
            var back = living.Where(e => e.IsBackRow).ToList();
            if (back.Count > 0) return back[Random.Range(0, back.Count)];
            return living[Random.Range(0, living.Count)];
        }

        private bool ProcessSideActions(List<CombatUnit> attackers, List<CombatUnit> defenders, float dt)
        {
            bool anyStrike = false;
            foreach (var attacker in attackers)
            {
                if (!attacker.IsAlive || attacker.petrifiedTimer > 0f || attacker.charmedTimer > 0f || defenders.Count == 0)
                    continue;

                attacker.battleElapsed += dt;
                attacker.attackTimer -= dt;
                if (attacker.attackTimer <= 0f)
                {
                    // Melee in BACK cannot reach — teach formation vs unit type.
                    if (attacker.IsMeleeStuckInBackRow)
                    {
                        BattlefieldVisualizer.Instance?.SpawnPopup(
                            attacker, "NO REACH!", new Color(1f, 0.55f, 0.2f));
                        attacker.ResetAttackCooldown();
                        continue;
                    }

                    ExecuteUnitAction(attacker, attackers, defenders);
                    attacker.ResetAttackCooldown();
                    anyStrike = true;
                }
            }
            return anyStrike;
        }

        private void ExecuteUnitAction(CombatUnit attacker, List<CombatUnit> allies, List<CombatUnit> enemies)
        {
            int dmg = attacker.attackDamage;
            if (attacker.unitType == UnitType.SirGawain && attacker.battleElapsed <= 8.0f) dmg *= 2;

            // When demons strike, `enemies` is already FRONT-only (until front is wiped).
            // When humans strike demons, pick among the living demon pool as given.
            switch (attacker.unitType)
            {
                case UnitType.Priest:
                {
                    int healTargets = SomersetRunes.HasRelic(RelicID.ChaliceOfTheGrail) ? 2 : 1;
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
                    else
                    {
                        var t = PickRandomTarget(enemies);
                        if (t != null) DealDamage(attacker, t, Mathf.Max(1, dmg / 2));
                    }
                    break;
                }
                case UnitType.LadyOfTheLake:
                {
                    // Chalice of the Grail: Lady also heals 2 allies per tick; otherwise default attack.
                    if (SomersetRunes.HasRelic(RelicID.ChaliceOfTheGrail))
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
                    else
                    {
                        var t = PickRandomTarget(enemies);
                        if (t != null) DealDamage(attacker, t, dmg);
                    }
                    break;
                }
                case UnitType.KingArthur:
                    foreach (var t in enemies.Where(e => e != null && e.IsAlive).OrderBy(_ => Random.value).Take(2))
                        DealDamage(attacker, t, dmg);
                    break;
                case UnitType.Merlin:
                    foreach (var t in enemies.Where(e => e != null && e.IsAlive))
                        DealDamage(attacker, t, dmg);
                    break;
                case UnitType.MorganLeFay:
                {
                    var hexTarget = PickRandomTarget(enemies);
                    if (hexTarget == null) break;
                    hexTarget.isHexed = true;
                    BattlefieldVisualizer.Instance?.SpawnPopup(hexTarget, "HEXED!", new Color(0.75f, 0.2f, 1f));
                    DealDamage(attacker, hexTarget, dmg);
                    break;
                }
                case UnitType.SirGalahad:
                {
                    var galTarget = PickRandomTarget(enemies);
                    if (galTarget == null) break;
                    int dealt = DealDamage(attacker, galTarget, dmg);
                    int heal = Mathf.Max(1, Mathf.RoundToInt(dealt * 0.25f));
                    foreach (var ally in allies.Where(a => a != attacker && a.IsAlive))
                        ally.currentHealth = Mathf.Min(ally.maxHealth, ally.currentHealth + heal);
                    break;
                }
                case UnitType.Mordred:
                {
                    var morTarget = PickRandomTarget(enemies);
                    if (morTarget == null) break;
                    bool isBoss = morTarget.unitType.ToString().StartsWith("Boss");
                    DealDamage(attacker, morTarget, isBoss ? dmg + 15 : dmg);
                    break;
                }
                case UnitType.BossMeleagant:
                {
                    var melTarget = PickMeleagantTarget(enemies);
                    if (melTarget != null) DealDamage(attacker, melTarget, dmg);
                    break;
                }
                case UnitType.BossAccolon:
                {
                    var accTarget = PickRandomTarget(enemies);
                    if (accTarget == null) break;
                    int accDealt = DealDamage(attacker, accTarget, dmg);
                    if (!attacker.isHexed)
                        attacker.currentHealth = Mathf.Min(attacker.maxHealth, attacker.currentHealth + Mathf.RoundToInt(accDealt * 0.30f));
                    break;
                }
                case UnitType.BossNimue:
                {
                    var nimTarget = PickRandomTarget(enemies);
                    if (nimTarget != null) DealDamage(attacker, nimTarget, dmg);

                    if (Random.value < 0.35f)
                    {
                        var strongest = enemies
                            .Where(e => e != null && e.IsAlive && e.petrifiedTimer <= 0f)
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
                        // Seduction can pull from the full living player army (not only front targets).
                        var traitor = playerArmy.FirstOrDefault(e =>
                            e != null && e.IsAlive && !e.isDemon
                            && (e.primaryTrait == TraitTag.Coward || e.primaryTrait == TraitTag.Outlaw));
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
                    // Backline Ranged / crossbow: snipe weakest among legal targets.
                    CombatUnit target;
                    if (attacker.unitType == UnitType.BossMeleagant)
                        target = PickMeleagantTarget(enemies);
                    else if (!attacker.isDemon && attacker.assignedRole == CombatRole.BacklineRanged)
                    {
                        target = enemies.Where(e => e != null && e.IsAlive)
                            .OrderBy(e => e.currentHealth).FirstOrDefault();
                    }
                    else
                    {
                        target = PickRandomTarget(enemies);
                    }
                    if (target != null) DealDamage(attacker, target, dmg);
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

            // Questing Beast: fear / grudge on hit (living humans only).
            if (attacker.unitType == UnitType.BossQuestingBeast
                && target != null && !target.isDemon && !string.IsNullOrEmpty(target.unitID)
                && finalDamage > 0)
            {
                GrudgeSaveManager.Instance?.ModifyGrudgeStaging(target.unitID, 5);
                BattlefieldVisualizer.Instance?.SpawnPopup(target, "FEAR!", new Color(0.7f, 0.45f, 0.9f));
            }

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
                && SomersetRunes.HasRelic(RelicID.ScabbardOfExcalibur)
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
            if (endingBattle)
            {
                Debug.LogWarning("[DemonKnight] EndBattle ignored — already ending (stops double victory wire).");
                return;
            }
            endingBattle = true;
            isBattling = false;
            try
            {
            EndBattleBody(playerWon);
            }
            finally
            {
                endingBattle = false;
            }
        }

        private void EndBattleBody(bool playerWon)
        {
            BattleBossDialogUI.Hide();
            BattlefieldVisualizer.Instance?.ClearAllActors();
            BattlefieldVisualizer.Instance?.StopBattleVideo();
            int goldEarned = playerWon ? DemonKnightCompatApi.WaveVictoryGold : DemonKnightCompatApi.WaveLossGold;
            // Balance V2: +10g per surviving marcher.
            int survivors = playerArmy.Count(u => u != null && !u.isDemon && u.IsAlive);
            int bounty = survivors * DemonKnightCompatApi.SurvivorBountyGold;
            goldEarned += bounty;
            // Arthur's Taxation: +15g per wave win while perk owned.
            if (playerWon
                && PerkDraftManager.Instance != null
                && PerkDraftManager.Instance.unlockedPerks.Contains(RunPerkID.ArthursTaxation))
                goldEarned += PerkDraftManager.TaxationPerWaveWin;

            var loop = DemonKnightCompatApi.ResolveLoop() ?? FindFirstObjectByType<GameLoopManager>();

            // Hire-price memory: Balance V2 death penalty is +0 (call kept for compatibility).
            var deadIds = new HashSet<string>();
            if (GrudgeSaveManager.Instance != null)
            {
                foreach (var u in playerArmy)
                {
                    if (u != null && !u.isDemon && !u.IsAlive && !string.IsNullOrEmpty(u.unitID))
                    {
                        deadIds.Add(u.unitID);
                        GrudgeSaveManager.Instance.RecordArmyDeath(u.unitID);
                    }
                }
            }

            // Silver Tongue: sacrifice highest-ATK living marcher before disband (+40g grudge).
            if (playerWon
                && SomersetRunes.IsPact(DemonicPactID.SilverTongue))
            {
                var victim = playerArmy.Where(u => u.IsAlive).OrderByDescending(u => u.attackDamage).FirstOrDefault();
                if (victim != null && !string.IsNullOrEmpty(victim.unitID))
                {
                    victim.currentHealth = 0;
                    deadIds.Add(victim.unitID);
                    GrudgeSaveManager.Instance?.RecordArmyDeath(victim.unitID);
                    GrudgeSaveManager.Instance?.ModifyGrudgeStaging(victim.unitID, 40);
                }
            }

            int sirensMarched = playerArmy.Count(u => u != null && !u.isDemon && u.isSirenEnchantress);

            // PER-WAVE disband — EVERY EndBattle (win OR lose). Loud + nuclear clear.
            // Win → pews + hire discount; loss → pews + higher hire. Stats kept.
            Debug.LogError(
                $"[DemonKnight] === END BATTLE DISBAND START won={playerWon} wave={currentBattleNumber} ===");
            ForceWaveDisband(loop, deadIds, playerWon);
            loop = DemonKnightCompatApi.ResolveLoop() ?? FindFirstObjectByType<GameLoopManager>();
            int partyNow = loop?.recruitedParty?.Count ?? -1;
            int nonMordred = loop?.recruitedParty?.Count(r => r != null && r.unitType != UnitType.Mordred) ?? 0;
            Debug.LogError($"[DemonKnight] === END BATTLE DISBAND DONE party={partyNow} (non-Mordred={nonMordred}) ===");
            if (nonMordred > 0)
                Debug.LogError("[DemonKnight] !!! DISBAND FAILED — non-Mordred still hired after ForceWaveDisband !!!");

            // Award gold: win 200 / loss 100 + survivor bounty (+ Taxation if applicable).
            var uiGold = FindFirstObjectByType<TownSplitUIController>()
                         ?? loop?.townUI;
            if (uiGold != null)
            {
                uiGold.playerGold += goldEarned;
                Debug.LogError(
                    $"[DemonKnight] Wave gold +{goldEarned}g (won={playerWon}, survivors={survivors}, bounty={bounty}) → purse {uiGold.playerGold}g.");
                uiGold.UpdateHUD();
            }
            else
            {
                Debug.LogError(
                    $"[DemonKnight] Wave gold +{goldEarned}g FAILED — no TownSplitUIController to credit.");
            }

            var churchGold = loop?.ResolveChurchUI()
                             ?? FindFirstObjectByType<SomersetChurchUI>()
                             ?? (SomersetChurchUI)FindFirstObjectByType<ChurchStageUIController>();
            churchGold?.SyncGoldFromTownUI();
            churchGold?.RefreshHUD();

            // Lust: hired Sirens drain 15g per wave (count from the march, before disband).
            if (playerWon && SomersetRunes.IsSinActive(SinRuneID.Lust) && sirensMarched > 0)
            {
                if (uiGold != null) uiGold.playerGold = Mathf.Max(0, uiGold.playerGold - sirensMarched * 15);
                churchGold?.SyncGoldFromTownUI();
                churchGold?.RefreshHUD();
                uiGold?.UpdateHUD();
            }

            // Disband again on every branch (idempotent) so win→town cannot skip.
            ForceWaveDisband(loop, deadIds, playerWon);

            if (playerWon && currentBattleNumber >= finalBossWave)
            {
                MerlinResurrectionManager.Instance?.TriggerEndOfRunResurrection(
                    currentBattleNumber, true,
                    $"VICTORY — Nimue falls on Wave {currentBattleNumber}. (+{goldEarned}g)\n\n"
                    + "The abyss stirs, but Camelot endures this night. Merlin turns the hourglass in the Somerset church.");
            }
            else if (!playerWon)
            {
                int wavesCleared = Mathf.Max(0, currentBattleNumber - 1);
                MerlinResurrectionManager.Instance?.TriggerEndOfRunResurrection(
                    wavesCleared, false,
                    $"DEFEAT — The warband fell on Wave {currentBattleNumber}. (+{goldEarned}g consolation)\n"
                    + $"Waves cleared this night: {wavesCleared}.\n\n"
                    + "The horde breaches the churchyard. Merlin shatters the hourglass — the pews remember the slaughter.");
            }
            else
            {
                // Perks/runes removed pending redesign — skip perk draft, return to church.
                SomersetRunes.TryAwardRelicForWave(currentBattleNumber);
                int clearedWave = currentBattleNumber;
                currentBattleNumber++;

                loop = DemonKnightCompatApi.ResolveLoop() ?? FindFirstObjectByType<GameLoopManager>();
                if (loop != null)
                {
                    if (loop.townCanvasObj != null) loop.townCanvasObj.SetActive(true);
                    if (loop.battlefieldArenaObj != null) loop.battlefieldArenaObj.SetActive(false);
                    if (loop.recruitedParty != null
                        && loop.recruitedParty.Any(r => r != null && r.unitType != UnitType.Mordred))
                    {
                        ForceWaveDisband(loop, deadIds, true);
                        DemonKnightCompatApi.SetMustDisband(loop, true);
                    }
                }

                Debug.LogError(
                    $"[DemonKnight] Wave {clearedWave} cleared (+{goldEarned}g). Showing victory → Next Night.");
                PerkDraftManager.SuppressAllPerkUi();
                // Victory screen with Next Night (advances night). Do not silent-return to town.
                var merlin = MerlinResurrectionManager.Instance
                             ?? FindFirstObjectByType<MerlinResurrectionManager>();
                if (merlin != null)
                    merlin.TriggerWaveVictoryScreen(clearedWave, goldEarned);
                else
                {
                    // Fallback if Merlin missing from scene: bump night and open town.
                    loop?.AdvanceNightCounterAfterWaveWin();
                    loop?.StartTownPhase();
                }
            }
        }

        /// <summary>Loud EnsureDisbandedAfterWave + nuclear recruitedParty.Clear if anything remains.</summary>
        private static void ForceWaveDisband(GameLoopManager loop, HashSet<string> deadIds, bool waveWon)
        {
            loop = loop ?? DemonKnightCompatApi.ResolveLoop() ?? Object.FindFirstObjectByType<GameLoopManager>();
            if (loop == null)
            {
                Debug.LogError("[DemonKnight] ForceWaveDisband: NO GameLoopManager — cannot clear party!");
                return;
            }

            int before = loop.recruitedParty != null ? loop.recruitedParty.Count : 0;
            Debug.LogError($"[DemonKnight] ForceWaveDisband before={before} won={waveWon}");
            DemonKnightCompatApi.EnsureDisbandedAfterWave(loop, deadIds, waveWon);

            if (loop.recruitedParty == null)
                loop.recruitedParty = new List<RecruitInstance>();

            // Balance V2: Mordred may remain hired — only clear non-Mordred leftovers.
            if (loop.recruitedParty.Count > 0
                && loop.recruitedParty.Any(r => r != null && r.unitType != UnitType.Mordred))
            {
                Debug.LogError(
                    $"[DemonKnight] NUCLEAR CLEAR — unfuse + DisbandPartyAfterMarch still needed.");
                DemonKnightCompatApi.EnsureDisbandedAfterWave(loop, deadIds, waveWon);
                if (loop.recruitedParty != null
                    && loop.recruitedParty.Any(r => r != null && r.unitType != UnitType.Mordred))
                {
                    var mordred = loop.recruitedParty.FindAll(r => r != null && r.unitType == UnitType.Mordred);
                    var leaves = CardFusionSystem.UnfusePartyToOriginals(
                        loop.recruitedParty.FindAll(r => r != null && r.unitType != UnitType.Mordred));
                    loop.recruitedParty.Clear();
                    foreach (var m in mordred) loop.recruitedParty.Add(m);
                    if (loop.currentTownPool == null)
                        loop.currentTownPool = new List<RecruitInstance>();
                    foreach (var r in leaves)
                    {
                        if (r == null) continue;
                        r.intelTierUnlocked = Mathf.Max(r.intelTierUnlocked, 3);
                        r.fusionIngredients = null;
                        // Nuclear path skipped ReturnOneToPew — still grant wave-win ★.
                        if (waveWon)
                            r.victoryStars = Mathf.Max(0, r.victoryStars) + 1;
                        if (!string.IsNullOrEmpty(r.unitID))
                            loop.currentTownPool.RemoveAll(t => t != null && t.unitID == r.unitID && t != r);
                        if (!loop.currentTownPool.Contains(r))
                            loop.currentTownPool.Add(r);
                    }
                    DemonKnightCompatApi.RefreshTownAfterDisband(loop);
                }
            }

            DemonKnightCompatApi.SetMustDisband(loop, false);
            DemonKnightCompatApi.AutosaveWarband(loop);
            Debug.LogError($"[DemonKnight] ForceWaveDisband after={loop.recruitedParty.Count}");
        }

    }
}
