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
            var layout = StageLayoutProfile.Load();
            layout?.EnsureSlotCounts();
            int nextSlot = 0;

            foreach (var r in recruitedParty)
            {
                if (r == null) continue;
                // Phase 5: only deployed units march (cap 52).
                if (!r.isDeployed) continue;
                if (nextSlot >= StageLayoutProfile.PlayerBattleCount) break;

                // Sequential formation slots 0..51 (columns of 6 for line-pierce).
                int slot = nextSlot;
                nextSlot++;

                int bonusHp = 0, bonusAtk = 0, bonusSpd = 0;
                if (layout != null && slot >= 0 && slot < layout.playerBattleSlots.Count
                    && layout.playerBattleSlots[slot] != null)
                {
                    var cfg = layout.playerBattleSlots[slot];
                    bonusHp = cfg.bonusHP;
                    bonusAtk = cfg.bonusATK;
                    bonusSpd = cfg.bonusSPD;
                }

                // Clone stats so permanent run buffs don't double-stack across waves
                RecruitInstance copy = new RecruitInstance
                {
                    unitID = r.unitID,
                    characterName = r.characterName,
                    unitType = r.unitType,
                    assignedRole = r.assignedRole,
                    maxHealth = r.maxHealth + bonusHp,
                    attackDamage = r.attackDamage + bonusAtk,
                    attackSpeed = r.attackSpeed + bonusSpd * 0.1f,
                    primaryTrait = r.primaryTrait,
                    hatedTrait = r.hatedTrait,
                    lovedTrait = r.lovedTrait,
                    battleSlotIndex = slot,
                    isDeployed = true,
                    isScouted = r.isScouted,
                    hiddenTraitId = r.hiddenTraitId,
                    veteranRank = r.EffectiveVeteranRank,
                    victoryStars = r.EffectiveVeteranRank,
                    // Warriors never fight from BACK — strip stale kits from older saves.
                    canAttackFromBack = r.canAttackFromBack && r.unitType != UnitType.Warrior
                };
                copy.isSirenEnchantress = r.isSirenEnchantress;
                HiddenPotentialSystem.ApplyGrowthToClone(copy, r);
                // Keep canonical slot for upkeep / next wave.
                r.battleSlotIndex = slot;
                combatRoster.Add(copy);
                if (r.randomAvatar != null) spriteLookup[r.unitID] = r.randomAvatar;
            }

            SomersetRunes.ApplyMorganasMirrorClone(combatRoster);
            PartySynergyEvaluator.ApplyBattleModifiers(combatRoster);
            PerkDraftManager.Instance?.ApplyCombatPerksToParty(combatRoster);
            ApplyLegendRivalries(combatRoster);
            ApplyUsurpationStarterLegends(combatRoster);

            // Re-stamp unique slots 0..51 after pact clones / mirror so line-pierce columns stay valid.
            for (int i = 0; i < combatRoster.Count && i < StageLayoutProfile.PlayerBattleCount; i++)
            {
                if (combatRoster[i] == null) continue;
                combatRoster[i].battleSlotIndex = i;
            }

            playerArmy.Clear();
            bool hellfire = SomersetRunes.IsPact(DemonicPactID.HellfireSteel);

            foreach (var r in combatRoster)
            {
                if (r == null) continue;
                int atk = r.attackDamage;
                float aps = Mathf.Clamp(r.attackSpeed, 1.0f, 2.0f);
                if (hellfire && (r.unitType == UnitType.Warrior || r.unitType == UnitType.Knight))
                {
                    atk *= 2;
                    aps = 2.0f;
                }

                // Sir Percival: +15% ATK per wave survived (victory stars).
                if (r.unitType == UnitType.SirPercival && r.victoryStars > 0)
                    atk = Mathf.Max(1, Mathf.RoundToInt(atk * (1f + 0.15f * r.victoryStars)));

                // Sir Tristan: canAttackFromBack + 25% SPD.
                if (r.unitType == UnitType.SirTristan)
                {
                    r.canAttackFromBack = true;
                    aps = Mathf.Min(3.0f, aps * 1.25f);
                }

                // Mordred: +15% ATK per positive staged grudge.
                if (r.unitType == UnitType.Mordred)
                {
                    int grudges = GrudgeSaveManager.Instance != null
                        ? GrudgeSaveManager.Instance.CountPositiveStagedGrudges()
                        : 0;
                    if (grudges > 0)
                        atk = Mathf.Max(1, Mathf.RoundToInt(atk * (1f + 0.15f * grudges)));
                }

                // IronLung: +20% max HP before combat.
                int hp = r.maxHealth;
                if (r.hiddenTraitId == HiddenPotentialSystem.TraitIronLung)
                    hp = Mathf.Max(1, Mathf.RoundToInt(hp * 1.20f));
                // Glutton: −10% ATK.
                if (r.hiddenTraitId == HiddenPotentialSystem.TraitGlutton)
                    atk = Mathf.Max(1, Mathf.RoundToInt(atk * 0.90f));

                CombatUnit cu = new CombatUnit
                {
                    unitID = r.unitID,
                    unitName = r.characterName,
                    unitType = r.unitType,
                    assignedRole = r.assignedRole,
                    isDemon = false,
                    isSirenEnchantress = r.isSirenEnchantress,
                    primaryTrait = r.primaryTrait,
                    maxHealth = hp,
                    currentHealth = hp,
                    attackDamage = atk,
                    attacksPerSecond = aps,
                    battleSlotIndex = r.battleSlotIndex,
                    canAttackFromBack = (r.canAttackFromBack && r.unitType != UnitType.Warrior)
                                       || r.unitType == UnitType.SirTristan,
                    cannotBeHealed = hellfire && (r.unitType == UnitType.Warrior || r.unitType == UnitType.Knight),
                    maxHpDrainPerSecond = hellfire && (r.unitType == UnitType.Warrior || r.unitType == UnitType.Knight)
                        ? hp * 0.05f : 0f,
                    hiddenTraitId = r.hiddenTraitId,
                    secretArchetype = r.secretArchetype,
                    traitAwakened = r.traitAwakened,
                    martyrsWard = r.hiddenTraitId == HiddenPotentialSystem.TraitMartyrsWard,
                    sleeperSpikeArmed = r.hiddenTraitId == HiddenPotentialSystem.TraitSleeperSpike
                };
                cu.ResetAttackCooldown();
                playerArmy.Add(cu);
            }

            _combatTurnIndex = 0;

            demonArmy = hordeGenerator.GenerateHordeForBattle(currentBattleNumber);
            ApplyEndlessHourglassPressure(demonArmy);
            SpawnLustSirensIntoHorde(demonArmy);

            // Balance V2: bosses start wave 3+.
            bool spawnedBoss = false;
            if (currentBattleNumber >= 3)
            {
                float mult = 1f + ((currentBattleNumber - 1) * hordeGenerator.statScalePerBattle);
                var waveBoss = hordeGenerator.SpawnCorruptedLegendBoss(currentBattleNumber, mult);
                demonArmy.Insert(0, waveBoss);
                spawnedBoss = waveBoss != null;
                // King Lot (lore wave 5): summons 2 Frenzied Imps on spawn — keyed by UnitType, not wave#.
                if (waveBoss != null && waveBoss.unitType == UnitType.BossKingLot)
                {
                    demonArmy.Insert(1, DemonHordeGenerator.MakeImp(9001, mult));
                    demonArmy.Insert(2, DemonHordeGenerator.MakeImp(9002, mult));
                }
            }

            foreach (var d in demonArmy) d.ResetAttackCooldown();
            TriggerBattleStartPassives();
            isBattling = true;
            BattlefieldVisualizer.Instance?.EnsureParallaxHost();
            // Phase 4 — boss wave micro-shake.
            if (spawnedBoss)
                StageParallaxController.MicroShake();

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

            ApplyFifteenLegendPassives();
        }

        /// <summary>15-Legend combat passives from legend-unlocks-brief.</summary>
        void ApplyFifteenLegendPassives()
        {
            bool anyOtherLegend(CombatUnit self) =>
                playerArmy.Any(u => u != null && u != self && u.IsAlive && RecruitInstance.IsLegendUnit(u.unitType));

            // Sir Kay: +20% ATK when no other Legend deployed.
            foreach (var kay in playerArmy.Where(u => u.unitType == UnitType.SirKay && u.IsAlive))
            {
                if (!anyOtherLegend(kay))
                    kay.attackDamage = Mathf.Max(1, Mathf.RoundToInt(kay.attackDamage * 1.20f));
            }

            // Lady Guinevere: heal all allies in her row 10% max HP at battle start.
            foreach (var g in playerArmy.Where(u =>
                         (u.unitType == UnitType.Guinevere || u.unitType == UnitType.LadyGuinevere) && u.IsAlive))
            {
                bool front = g.IsFrontRow;
                foreach (var ally in playerArmy.Where(a => a.IsAlive && a.IsFrontRow == front))
                {
                    int heal = Mathf.Max(1, Mathf.RoundToInt(ally.maxHealth * 0.10f));
                    ally.currentHealth = Mathf.Min(ally.maxHealth, ally.currentHealth + heal);
                    BattlefieldVisualizer.Instance?.SpawnPopup(ally, $"+{heal}", Color.green);
                }
            }

            // Arch Priest: +15% max HP to adjacent allies (battleSlotIndex ± 1).
            foreach (var priest in playerArmy.Where(u =>
                         (u.unitType == UnitType.Priest || u.unitType == UnitType.ArchPriestOfGlastonbury)
                         && u.IsAlive))
            {
                foreach (var ally in playerArmy.Where(a =>
                             a.IsAlive && a != priest
                             && Mathf.Abs(a.battleSlotIndex - priest.battleSlotIndex) == 1))
                {
                    int bonus = Mathf.Max(1, Mathf.RoundToInt(ally.maxHealth * 0.15f));
                    ally.maxHealth += bonus;
                    ally.currentHealth += bonus;
                }
            }

            // King Arthur: +15% ATK and +15% HP to all deployed allies (Round Table).
            if (playerArmy.Any(u => u.unitType == UnitType.KingArthur && u.IsAlive))
            {
                foreach (var ally in playerArmy.Where(a => a.IsAlive))
                {
                    ally.attackDamage = Mathf.Max(1, Mathf.RoundToInt(ally.attackDamage * 1.15f));
                    int hpBonus = Mathf.Max(1, Mathf.RoundToInt(ally.maxHealth * 0.15f));
                    ally.maxHealth += hpBonus;
                    ally.currentHealth += hpBonus;
                }
            }

            // Nimue: MartyrsWard to nearest frontline ally.
            foreach (var nimue in playerArmy.Where(u => u.unitType == UnitType.Nimue && u.IsAlive))
            {
                var nearest = playerArmy
                    .Where(a => a.IsAlive && a.IsFrontRow && a != nimue)
                    .OrderBy(a => Mathf.Abs(a.battleSlotIndex - nimue.battleSlotIndex))
                    .FirstOrDefault();
                if (nearest != null)
                {
                    nearest.martyrsWard = true;
                    BattlefieldVisualizer.Instance?.SpawnPopup(nearest, "WARD!", new Color(0.6f, 0.9f, 1f));
                }
            }

            // Morgan le Fay: −15% enemy frontline ATK at combat start.
            if (playerArmy.Any(u => u.unitType == UnitType.MorganLeFay && u.IsAlive))
            {
                foreach (var foe in demonArmy.Where(d => d != null && d.IsAlive))
                {
                    // Horde has no formation; treat first frontlineSize as front.
                    foe.attackDamage = Mathf.Max(1, Mathf.RoundToInt(foe.attackDamage * 0.85f));
                }
            }

            ApplyHiddenPotentialBattleStart();
        }

        /// <summary>Baptism by Fire + MythicVessel auras + MartyrsWard self-flag.</summary>
        void ApplyHiddenPotentialBattleStart()
        {
            var loop = DemonKnightCompatApi.ResolveLoop() ?? GameLoopManager.Instance;
            var party = loop?.recruitedParty;

            foreach (var u in playerArmy)
            {
                if (u == null || !u.IsAlive || u.isDemon) continue;

                if (!u.traitAwakened && !string.IsNullOrEmpty(u.hiddenTraitId))
                {
                    bool scouted = false;
                    if (party != null)
                    {
                        foreach (var r in party)
                        {
                            if (r == null || r.unitID != u.unitID) continue;
                            scouted = r.isScouted;
                            if (!r.traitAwakened)
                            {
                                r.traitAwakened = true;
                                LegendUnlockManager.Ensure()?.NotifyHiddenPotentialAwakened(r);
                            }
                            break;
                        }
                    }
                    if (scouted || u.secretArchetype != SecretArchetype.Parishioner)
                    {
                        u.traitAwakened = true;
                        BattlefieldVisualizer.Instance?.SpawnPopup(
                            u, "AWAKENED!", new Color(1f, 0.85f, 0.2f));
                    }
                }

                if (u.hiddenTraitId == HiddenPotentialSystem.TraitMartyrsWard)
                    u.martyrsWard = true;
            }

            bool pendragon = playerArmy.Any(u =>
                u != null && u.IsAlive && u.hiddenTraitId == HiddenPotentialSystem.TraitPendragonBlood);
            bool grail = playerArmy.Any(u =>
                u != null && u.IsAlive && u.hiddenTraitId == HiddenPotentialSystem.TraitGrailTouched);
            bool fae = playerArmy.Any(u =>
                u != null && u.IsAlive && u.hiddenTraitId == HiddenPotentialSystem.TraitFaeKissed);
            if (!pendragon && !grail && !fae) return;

            foreach (var ally in playerArmy.Where(a => a != null && a.IsAlive && !a.isDemon))
            {
                if (pendragon)
                {
                    ally.attackDamage = Mathf.Max(1, Mathf.RoundToInt(ally.attackDamage * 1.10f));
                    BattlefieldVisualizer.Instance?.SpawnPopup(ally, "PENDRAGON!", new Color(1f, 0.75f, 0.2f));
                }
                if (grail)
                {
                    ally.shieldHealth += 15;
                    BattlefieldVisualizer.Instance?.SpawnPopup(ally, "GRAIL!", new Color(0.6f, 0.9f, 1f));
                }
                if (fae)
                {
                    ally.attacksPerSecond = Mathf.Min(3f, ally.attacksPerSecond * 1.10f);
                    BattlefieldVisualizer.Instance?.SpawnPopup(ally, "FAE!", new Color(0.85f, 0.6f, 1f));
                }
            }
        }

        int _combatTurnIndex;
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
            if (struck)
            {
                _combatTurnIndex++;
                ApplyGreenKnightTurnRegen();
                if (attackResolvePause > 0f)
                    _postAttackPauseTimer = attackResolvePause;
            }
        }

        void ApplyGreenKnightTurnRegen()
        {
            foreach (var gk in playerArmy.Where(u => u != null && u.IsAlive && u.unitType == UnitType.TheGreenKnight))
            {
                int regen = Mathf.Max(1, Mathf.RoundToInt(gk.maxHealth * 0.08f));
                if (gk.currentHealth >= gk.maxHealth) continue;
                gk.currentHealth = Mathf.Min(gk.maxHealth, gk.currentHealth + regen);
                BattlefieldVisualizer.Instance?.SpawnPopup(gk, $"+{regen}", new Color(0.4f, 0.9f, 0.4f));
            }
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
            // Liability Coward: 20% chance to flinch and skip the attack.
            if (!attacker.isDemon && attacker.hiddenTraitId == HiddenPotentialSystem.TraitCoward
                && Random.value < 0.20f)
            {
                BattlefieldVisualizer.Instance?.SpawnPopup(attacker, "FLINCH!", new Color(0.7f, 0.7f, 0.7f));
                return;
            }

            int dmg = attacker.attackDamage;
            // Sir Gawain Morning Sun: +35% ATK first 3 combat turns (replaces old 8s double).
            if (attacker.unitType == UnitType.SirGawain && _combatTurnIndex < 3)
                dmg = Mathf.Max(1, Mathf.RoundToInt(dmg * 1.35f));

            // SleeperSpike: first 3 hits at 50%, then +40%.
            if (!attacker.isDemon && attacker.hiddenTraitId == HiddenPotentialSystem.TraitSleeperSpike)
            {
                attacker.sleeperSpikeHits++;
                if (attacker.sleeperSpikeHits <= 3)
                    dmg = Mathf.Max(1, Mathf.RoundToInt(dmg * 0.50f));
                else
                    dmg = Mathf.Max(1, Mathf.RoundToInt(dmg * 1.40f));
            }

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
                {
                    // Merlin: hit 3 random enemies at 65% ATK each.
                    int bolt = Mathf.Max(1, Mathf.RoundToInt(dmg * 0.65f));
                    foreach (var t in enemies.Where(e => e != null && e.IsAlive).OrderBy(_ => Random.value).Take(3))
                        DealDamage(attacker, t, bolt);
                    break;
                }
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
                case UnitType.SirLancelot:
                {
                    var lanTarget = PickRandomTarget(enemies);
                    if (lanTarget == null) break;
                    int lanDmg = dmg;
                    // +30% ATK +20% SPD vs enemy with higher max HP.
                    if (lanTarget.maxHealth > attacker.maxHealth)
                    {
                        lanDmg = Mathf.Max(1, Mathf.RoundToInt(dmg * 1.30f));
                        attacker.attacksPerSecond = Mathf.Min(3.0f, attacker.attacksPerSecond * 1.20f);
                    }
                    DealDamage(attacker, lanTarget, lanDmg);
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

            // Liability Turncoat: 12% chance to strike a random living ally instead.
            if (attacker != null && !attacker.isDemon
                && attacker.hiddenTraitId == HiddenPotentialSystem.TraitTurncoat
                && Random.value < 0.12f)
            {
                var allyHit = playerArmy.Where(a => a != null && a.IsAlive && a != attacker).ToList();
                if (allyHit.Count > 0)
                {
                    target = allyHit[Random.Range(0, allyHit.Count)];
                    BattlefieldVisualizer.Instance?.SpawnPopup(attacker, "TURNCOAT!", new Color(1f, 0.3f, 0.3f));
                }
            }

            // GiantSlayer: +50% vs bosses / elites.
            if (attacker != null && !attacker.isDemon
                && attacker.hiddenTraitId == HiddenPotentialSystem.TraitGiantSlayer
                && target != null && IsEliteDemon(target))
            {
                finalDamage = Mathf.Max(1, Mathf.RoundToInt(finalDamage * 1.50f));
            }

            if (target.unitType == UnitType.BossBlackKnight
                && (attacker.unitType == UnitType.Peasant || attacker.unitType == UnitType.Warrior))
            {
                finalDamage = Mathf.Max(1, finalDamage / 2);
            }

            // Sir Bedivere: +25% damage reduction when solo in his row.
            if (target.unitType == UnitType.SirBedivere && target.IsAlive)
            {
                bool solo = !playerArmy.Any(a =>
                    a != null && a.IsAlive && a != target && a.IsFrontRow == target.IsFrontRow);
                if (solo)
                    finalDamage = Mathf.Max(1, Mathf.RoundToInt(finalDamage * 0.75f));
            }

            // The Green Knight: cannot be one-shot above 50% HP.
            if (target.unitType == UnitType.TheGreenKnight
                && target.currentHealth > target.maxHealth / 2
                && finalDamage >= target.currentHealth)
            {
                finalDamage = Mathf.Max(1, target.currentHealth - 1);
                BattlefieldVisualizer.Instance?.SpawnPopup(target, "ENDURE!", new Color(0.3f, 0.8f, 0.3f));
            }

            if (target.shieldHealth > 0)
            {
                int absorbed = Mathf.Min(target.shieldHealth, finalDamage);
                target.shieldHealth -= absorbed;
                finalDamage -= absorbed;
                BattlefieldVisualizer.Instance?.SpawnPopup(target, $"SHIELD -{absorbed}", Color.cyan);
            }

            // Sir Galahad: redirect 25% lethal damage from adjacent allies to self, then halve it.
            if (!target.isDemon
                && target.currentHealth - finalDamage <= 0
                && target.unitType != UnitType.SirGalahad)
            {
                var galahad = playerArmy.FirstOrDefault(a =>
                    a != null && a.IsAlive && a.unitType == UnitType.SirGalahad
                    && Mathf.Abs(a.battleSlotIndex - target.battleSlotIndex) == 1);
                if (galahad != null)
                {
                    int redirected = Mathf.Max(1, Mathf.RoundToInt(finalDamage * 0.25f));
                    int halved = Mathf.Max(1, redirected / 2);
                    finalDamage -= redirected;
                    galahad.currentHealth -= halved;
                    BattlefieldVisualizer.Instance?.SpawnPopup(galahad, $"WARD -{halved}", new Color(1f, 0.85f, 0.4f));
                    if (galahad.currentHealth <= 0) HandleUnitDeath(galahad);
                }
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

            // Phase 5: Wave 3+ elite line-pierce — 35% splash to unit behind (same column, slot+6).
            if (finalDamage > 0
                && attacker != null && attacker.isDemon
                && currentBattleNumber >= 3
                && IsEliteDemon(attacker)
                && target != null && !target.isDemon)
            {
                ApplyLinePierceSplash(target, finalDamage);
            }

            // Glutton: heal 5% max HP when dealing damage.
            if (attacker != null && !attacker.isDemon && attacker.IsAlive
                && attacker.hiddenTraitId == HiddenPotentialSystem.TraitGlutton
                && finalDamage > 0)
            {
                int heal = Mathf.Max(1, Mathf.RoundToInt(attacker.maxHealth * 0.05f));
                attacker.currentHealth = Mathf.Min(attacker.maxHealth, attacker.currentHealth + heal);
                BattlefieldVisualizer.Instance?.SpawnPopup(attacker, $"+{heal}", new Color(0.9f, 0.6f, 0.2f));
            }

            bool killed = target.currentHealth <= 0;
            if (killed) HandleUnitDeath(target);

            // TitheBearer: +5g on demon kill.
            if (killed && attacker != null && !attacker.isDemon
                && attacker.hiddenTraitId == HiddenPotentialSystem.TraitTitheBearer
                && target != null && target.isDemon)
            {
                var town = Object.FindFirstObjectByType<TownSplitUIController>();
                if (town != null)
                {
                    town.playerGold += 5;
                    BattlefieldVisualizer.Instance?.SpawnPopup(attacker, "+5g TITHE", new Color(1f, 0.85f, 0.3f));
                }
            }

            return finalDamage;
        }

        static bool IsEliteDemon(CombatUnit u)
        {
            if (u == null || !u.isDemon) return false;
            string n = u.unitType.ToString();
            if (n.StartsWith("Boss")) return true;
            if (u.unitType == UnitType.DemonDreadLord) return true;
            return u.powerValue >= 12;
        }

        void ApplyLinePierceSplash(CombatUnit primary, int primaryDamage)
        {
            if (primary == null) return;
            // Columns of frontlineSize (6): unit behind shares slotIndex % 6, index + 6.
            int behindSlot = primary.battleSlotIndex + frontlineSize;
            if (behindSlot % frontlineSize != primary.battleSlotIndex % frontlineSize) return;
            var behind = playerArmy.FirstOrDefault(u =>
                u != null && u.IsAlive && !u.isDemon && u.battleSlotIndex == behindSlot);
            if (behind == null) return;

            int splash = Mathf.Max(1, Mathf.RoundToInt(primaryDamage * 0.35f));
            behind.currentHealth -= splash;
            BattlefieldVisualizer.Instance?.SpawnPopup(
                behind, $"PIERCE -{splash}", new Color(1f, 0.55f, 0.2f));
            if (behind.currentHealth <= 0)
                HandleUnitDeath(behind);
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

            // Nimue MartyrsWard: survive first lethal at 1 HP.
            if (!fallen.isDemon && fallen.martyrsWard && !fallen.martyrsWardUsed)
            {
                fallen.martyrsWardUsed = true;
                fallen.currentHealth = 1;
                BattlefieldVisualizer.Instance?.SpawnPopup(fallen, "MARTYR!", new Color(0.6f, 0.9f, 1f));
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

            // Phase 4 — true fatal blow micro-shake.
            StageParallaxController.MicroShake();

            // Phase 5 Shepherd's Vow — combat deaths only (fusions never call here).
            if (!fallen.isDemon)
            {
                var loop = DemonKnightCompatApi.ResolveLoop() ?? GameLoopManager.Instance;
                if (loop != null)
                {
                    loop.soulsLostThisRun++;
                    loop.ResolveChurchUI()?.RefreshHUD();
                }

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

            // Phase 5 flock upkeep from the +200g victory base (loss uses consolation, no upkeep).
            int marched = playerArmy.Count(u => u != null && !u.isDemon);
            int basePayout = playerWon
                ? DemonKnightCompatApi.WaveVictoryGold
                : DemonKnightCompatApi.WaveLossGold;
            int flockUpkeep = playerWon ? FlockEconomy.ComputeUpkeep(marched) : 0;
            int netBase = Mathf.Max(0, basePayout - flockUpkeep);
            int goldEarned = netBase;
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
            string flockBreakdown = playerWon
                ? FlockEconomy.FormatBreakdown(marched, basePayout, flockUpkeep, netBase)
                : "";
            if (loop != null) loop.lastFlockUpkeepBreakdown = flockBreakdown;

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

            // Master Spec Phase 1: surviving battle clones → canonical veteranRank before disband.
            if (playerWon)
            {
                var surviving = new HashSet<string>();
                foreach (var u in playerArmy)
                {
                    if (u != null && !u.isDemon && u.IsAlive && !string.IsNullOrEmpty(u.unitID))
                        surviving.Add(u.unitID);
                }
                DemonKnightCompatApi.AwardWaveVictoryVeteranRanks(loop, surviving);

                int staged = GrudgeSaveManager.Instance != null
                    ? GrudgeSaveManager.Instance.CountPositiveStagedGrudges()
                    : 0;
                var unlockMgr = LegendUnlockManager.Ensure();
                unlockMgr?.NotifyWaveCleared(
                    currentBattleNumber,
                    loop != null ? loop.soulsLostThisRun : 0);
                // Liability / grudge Morgan unlock — use marched roster before disband.
                var marchedRoster = new List<RecruitInstance>();
                if (loop?.recruitedParty != null)
                    marchedRoster.AddRange(loop.recruitedParty);
                unlockMgr?.NotifyWaveWonWithParty(marchedRoster, staged);
                unlockMgr?.MarkCursedDiceOwnedThisRun();
            }

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
                    $"[DemonKnight] Wave gold +{goldEarned}g (won={playerWon}, base={netBase}, upkeep={flockUpkeep}, survivors={survivors}, bounty={bounty}) → purse {uiGold.playerGold}g."
                    + (string.IsNullOrEmpty(flockBreakdown) ? "" : " " + flockBreakdown));
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
                LegendUnlockManager.Ensure()?.NotifyFullRunVictory();
                int endGrudges = GrudgeSaveManager.Instance != null
                    ? GrudgeSaveManager.Instance.CountPositiveStagedGrudges()
                    : 0;
                LegendUnlockManager.Ensure()?.NotifyRunEnded(endGrudges);
                MerlinResurrectionManager.Instance?.TriggerEndOfRunResurrection(
                    currentBattleNumber, true,
                    $"VICTORY — Nimue falls on Wave {currentBattleNumber}. (+{goldEarned}g)\n\n"
                    + "The abyss stirs, but Camelot endures this night. Merlin turns the hourglass in the Somerset church.");
            }
            else if (!playerWon)
            {
                int wavesCleared = Mathf.Max(0, currentBattleNumber - 1);
                int endGrudges = GrudgeSaveManager.Instance != null
                    ? GrudgeSaveManager.Instance.CountPositiveStagedGrudges()
                    : 0;
                LegendUnlockManager.Ensure()?.NotifyRunEnded(endGrudges);
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
                    merlin.TriggerWaveVictoryScreen(clearedWave, goldEarned, flockBreakdown);
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
                        // ★ already applied via AwardWaveVictoryVeteranRanks on product/leaves.
                        r.SyncVeteranRankFields();
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
