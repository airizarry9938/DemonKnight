using System.Collections.Generic;
using UnityEngine;

namespace ArthurianAutobattler
{
    public class GameLoopManager : MonoBehaviour
    {
        public static GameLoopManager Instance;

        [Header("Timer Settings")]
        [Tooltip("Town recruitment countdown. Start between 300–600s; perk drafts can shrink toward 180s.")]
        [Range(180f, 600f)]
        public float townTimeLimit = 420f;
        private float currentTimer;
        public bool isTimerRunning = false;
        public bool disableTownTimer = false;
        public float townSecondsSpentThisPhase;

        [Header("Scene References")]
        public TownRNGGenerator rngGenerator;
        public TownSplitUIController townUI;
        /// <summary>
        /// Prefer SomersetChurchUI (has UpdateWarbandHudLabel / FlashMarchBlocked).
        /// ChurchStageUIController is only a thin subclass alias — do not call those APIs
        /// through a compile-time ChurchStageUIController when Downloads has a fat stub.
        /// </summary>
        public SomersetChurchUI churchUI;
        public AutoBattlerEngine battleEngine;
        public GameObject townCanvasObj;
        public GameObject battlefieldArenaObj;

        [Header("Active Run Roster")]
        public List<RecruitInstance> recruitedParty = new List<RecruitInstance>();
        public List<RecruitInstance> currentTownPool = new List<RecruitInstance>();
        public GamePhase currentPhase;

        [Header("Timeline")]
        [Tooltip("Which night of the endless Merlin timeline the player is on.")]
        public int currentNight = 1;

        /// <summary>
        /// Set true when a March/AutoBattle wave starts. Cleared only after
        /// <see cref="EnsureDisbandedAfterWave"/> runs. Lets StartTownPhase / perk
        /// return paths force a disband even if a stale AutoBattlerEngine skipped it.
        /// Per-WAVE (every AutoBattle), not only full-run Merlin wipe.
        /// </summary>
        public bool mustDisbandPartyAfterCurrentWave;

        /// <summary>Last wave outcome for hire pricing on disband (true=win discount, false=loss penalty).</summary>
        public bool? lastWaveWon;

        /// <summary>
        /// Mediate (100g) suspends Noble/Outlaw + hatedTrait rivalries until this wave ends.
        /// Cleared on <see cref="EnsureDisbandedAfterWave"/> / new night — per-wave, not permanent.
        /// </summary>
        public bool rivalriesSuspendedThisWave;

        /// <summary>
        /// Buy Tavern Round (50g) → −10g hire for every recruit until this wave ends.
        /// Cleared on <see cref="EnsureDisbandedAfterWave"/> / new night.
        /// </summary>
        public bool tavernRoundHireDiscountActive;

        /// <summary>Gold cost to Mediate (wave-scoped rivalry cancel).</summary>
        public const int MediateCostGold = 100;

        /// <summary>Gold cost to Buy Tavern Round.</summary>
        public const int TavernRoundCostGold = 50;

        /// <summary>Hire discount for all units after Buy Tavern Round (this wave).</summary>
        public const int TavernRoundHireDiscount = 10;

        private Coroutine seatPopulateRoutine;
        private const string NightPrefsKey = "SomersetNight";
        private GamePhase previousPhase;

        /// <summary>Base purse for a fresh night / New Game (before bank rune bonus).</summary>
        public const int StartingGold = 200;
        public const int WaveVictoryGold = 200;
        public const int WaveLossGold = 50;

        public static GameLoopManager Resolve()
        {
            if (Instance != null) return Instance;
            Instance = FindFirstObjectByType<GameLoopManager>();
            return Instance;
        }

        /// <summary>
        /// Always resolve as SomersetChurchUI so UpdateWarbandHudLabel / FlashMarchBlocked
        /// compile against the class that actually declares them (CS1061-safe for Downloads).
        /// </summary>
        public SomersetChurchUI ResolveChurchUI()
        {
            if (churchUI != null) return churchUI;
            churchUI = FindFirstObjectByType<SomersetChurchUI>();
            if (churchUI == null)
            {
                var alias = FindFirstObjectByType<ChurchStageUIController>();
                if (alias != null) churchUI = alias;
            }
            return churchUI;
        }

        private void Awake()
        {
            if (Instance == null) Instance = this;
            else Destroy(gameObject);
            currentNight = Mathf.Max(1, PlayerPrefs.GetInt(NightPrefsKey, 1));
            previousPhase = currentPhase;
            // Earliest possible menu spawn (Downloads often never rebuilt the scene).
            MainMenuUI.EnsureExists();
            Debug.LogError("[DemonKnight] GameLoopManager.Awake — MainMenuUI.EnsureExists() called.");
        }

        private void Start()
        {
            ResolveChurchUI();
            if (townUI == null) townUI = FindFirstObjectByType<TownSplitUIController>();

            var menu = MainMenuUI.EnsureExists();
            if (townCanvasObj != null) townCanvasObj.SetActive(true);
            if (menu != null)
            {
                menu.ShowMenu(boot: true);
                Debug.LogError("[DemonKnight] GameLoopManager.Start — MAIN MENU OPEN. Esc = Quit/Restart/New/Load.");
                return;
            }

            Debug.LogError("[DemonKnight] MainMenuUI MISSING after EnsureExists — BootWithoutMenu fallback.");
            BootWithoutMenu();
        }

        /// <summary>Used when no MainMenuUI exists, or after Continue from a missing-menu build.</summary>
        public void BootWithoutMenu()
        {
            if (WarbandAutosave.HasAutosave())
            {
                Debug.Log("[DemonKnight] Resuming timeline from automatic warband save.");
                ContinueTimelineFromAutosave();
                // Wave-disband design: never boot with a hired party still in the warband.
                EnsureDisbandedAfterWave();
            }
            else
            {
                InitializeFreshCampaign();
            }
        }

        /// <summary>
        /// Brand-new campaign. Wipes leftover economy (autosave, grudges, Merlin bank,
        /// perks, relics, sins, pacts) and starts at exactly <see cref="StartingGold"/> (200g).
        /// </summary>
        public void InitializeFreshCampaign()
        {
            ResetEconomyForNewGame();
            recruitedParty.Clear();
            WarbandAutosave.Clear();
            currentNight = 1;
            PlayerPrefs.SetInt(NightPrefsKey, 1);
            PlayerPrefs.Save();
            BeginTownNight(
                keepSurvivors: false,
                restoreGoldFromSave: false,
                veterans: null,
                applyBankStartingBonus: false);

            // Belt-and-suspenders: New Game purse is always exactly 200g.
            ForceStartingGoldExact();
            Debug.LogError(
                $"[DemonKnight] New Game purse locked at {StartingGold}g (bank/perks/relics wiped).");
        }

        /// <summary>Legacy name — fresh start only (does not wipe an in-progress Merlin timeline).</summary>
        public void InitializeNewRun() => InitializeFreshCampaign();

        /// <summary>
        /// Wipe run + meta economy so New Game cannot inherit Merlin Treasury, Writ,
        /// runes, perks, relics, sins, or autosaved gold.
        /// </summary>
        public void ResetEconomyForNewGame()
        {
            GrudgeSaveManager.Instance?.WipeAllSavedMemory();
            WarbandAutosave.Clear();

            MerlinResurrectionManager.Instance?.ResetBankForNewGame();
            if (PerkDraftManager.Instance != null)
                PerkDraftManager.Instance.unlockedPerks.Clear();
            RelicManager.Instance?.ClearAllRelics();
            SinRuneManager.Instance?.ClearAllSins();
            SomersetRunes.ClearPact();
            SomersetRunes.SetGrudgeDeltaMultiplier(1f);

            rivalriesSuspendedThisWave = false;
            tavernRoundHireDiscountActive = false;
            disableTownTimer = false;
            lastWaveWon = null;
            mustDisbandPartyAfterCurrentWave = false;
            townSecondsSpentThisPhase = 0f;
            townTimeLimit = UnityEngine.Random.Range(300f, 600f);

            if (battleEngine == null) battleEngine = FindFirstObjectByType<AutoBattlerEngine>();
            if (battleEngine != null)
            {
                battleEngine.isBattling = false;
                battleEngine.currentBattleNumber = 1;
            }
        }

        /// <summary>Force HUD / town purse to exactly StartingGold (200).</summary>
        public void ForceStartingGoldExact()
        {
            if (townUI == null) townUI = FindFirstObjectByType<TownSplitUIController>();
            if (townUI != null) townUI.playerGold = StartingGold;
            var church = ResolveChurchUI();
            church?.SyncGoldFromTownUI();
            church?.RefreshHUD();
            townUI?.UpdateHUD();
            AutosaveWarband(StartingGold);
        }

        /// <summary>
        /// Called from Merlin's "Enter Next Timeline" after defeat/victory.
        /// Autosaves surviving warband, advances night, restores them into the next town phase.
        /// </summary>
        public void AdvanceNightAndRestart()
        {
            currentNight = Mathf.Max(1, currentNight) + 1;
            PlayerPrefs.SetInt(NightPrefsKey, currentNight);
            PlayerPrefs.Save();

            // Capture survivors AFTER night bump — automatic, no Save menu.
            int gold = townUI != null ? townUI.playerGold : StartingGold;
            AutosaveWarband(gold);

            if (battleEngine != null)
            {
                battleEngine.isBattling = false;
                battleEngine.currentBattleNumber = 1;
            }

            if (PerkDraftManager.Instance != null)
                PerkDraftManager.Instance.unlockedPerks.Clear();

            townTimeLimit = UnityEngine.Random.Range(300f, 600f);

            if (battlefieldArenaObj != null) battlefieldArenaObj.SetActive(false);
            if (townCanvasObj != null) townCanvasObj.SetActive(true);

            ContinueTimelineFromAutosave();
        }

        /// <summary>
        /// Load autosave and start the next town night.
        /// Post-March design: party is usually empty; veterans merge into pews for rehire
        /// with kept stats/upgrades/intel. Any leftover hired party is also restored.
        /// </summary>
        public void ContinueTimelineFromAutosave()
        {
            var data = WarbandAutosave.Load();
            recruitedParty.Clear();
            List<RecruitInstance> veterans = null;

            if (data != null)
            {
                if (data.night > 0) currentNight = data.night;
                PlayerPrefs.SetInt(NightPrefsKey, currentNight);
                PlayerPrefs.Save();

                recruitedParty = WarbandAutosave.ToRecruitList(data);
                veterans = WarbandAutosave.ToVeteranList(data);
                // Legacy saves: if veterans empty but survivors present after an old
                // "keep party across nights" build, treat survivors as rehireable pew veterans
                // when we want empty party — but if party was intentionally saved, keep it.
                // After march-disband packs, survivors should be empty and veterans populated.
                foreach (var r in recruitedParty)
                {
                    if (r == null) continue;
                    r.intelTierUnlocked = Mathf.Max(r.intelTierUnlocked, 3);
                    GrudgeSaveManager.Instance?.RememberIntel(r.unitID, r.intelTierUnlocked);
                    RebindPaperDoll(r);
                }

                Debug.Log(
                    $"[DemonKnight] Night {currentNight}: restored party {recruitedParty.Count}, veterans {veterans?.Count ?? 0}.");
            }

            BeginTownNight(
                keepSurvivors: recruitedParty.Count > 0,
                restoreGoldFromSave: true,
                savedGold: data != null ? data.playerGold : -1,
                veterans: veterans);
        }

        private void BeginTownNight(
            bool keepSurvivors,
            bool restoreGoldFromSave,
            int savedGold = -1,
            List<RecruitInstance> veterans = null,
            bool applyBankStartingBonus = true)
        {
            disableTownTimer = false;
            townSecondsSpentThisPhase = 0f;
            SomersetRunes.SetGrudgeDeltaMultiplier(1f);
            SomersetRunes.ClearPact();
            rivalriesSuspendedThisWave = false;
            tavernRoundHireDiscountActive = false;

            if (rngGenerator == null) rngGenerator = FindFirstObjectByType<TownRNGGenerator>();
            currentTownPool = rngGenerator != null
                ? rngGenerator.GenerateNewRunTown()
                : new List<RecruitInstance>();

            // Overlay veterans onto pews (kept stats / upgrades / intel for rehire).
            if (veterans != null && veterans.Count > 0)
            {
                foreach (var v in veterans)
                {
                    if (v == null) continue;
                    v.intelTierUnlocked = Mathf.Max(v.intelTierUnlocked, 3);
                    GrudgeSaveManager.Instance?.RememberIntel(v.unitID, v.intelTierUnlocked);
                    RebindPaperDoll(v);
                }
                WarbandAutosave.MergeVeteransIntoTown(currentTownPool, veterans);
            }

            // Anyone still hired stays out of the pew pool.
            if (keepSurvivors && recruitedParty != null && recruitedParty.Count > 0)
            {
                var partyIds = new HashSet<string>();
                foreach (var m in recruitedParty)
                {
                    if (m != null && !string.IsNullOrEmpty(m.unitID))
                        partyIds.Add(m.unitID);
                }
                currentTownPool.RemoveAll(p => p != null && partyIds.Contains(p.unitID));
            }

            // Bank Treasury only applies to a true fresh start — never re-add onto autosaved gold
            // (that compounded +15g×level every Merlin night / Continue).
            int bonusGold = 0;
            if (applyBankStartingBonus && MerlinResurrectionManager.Instance != null)
                bonusGold = MerlinResurrectionManager.Instance.GetBonusStartingGold();

            if (townUI != null)
            {
                if (restoreGoldFromSave && savedGold >= 0)
                    townUI.playerGold = Mathf.Max(0, savedGold);
                else
                    townUI.playerGold = StartingGold + Mathf.Max(0, bonusGold);
            }
            if (churchUI != null) churchUI.SyncGoldFromTownUI();

            int gold = townUI != null
                ? townUI.playerGold
                : (restoreGoldFromSave && savedGold >= 0
                    ? Mathf.Max(0, savedGold)
                    : StartingGold + Mathf.Max(0, bonusGold));
            if (currentTownPool.Count > 0 && IsTownCompletelyPricedOut(gold) && recruitedParty.Count == 0)
            {
                MerlinResurrectionManager.Instance?.TriggerEndOfRunResurrection(
                    0, false,
                    "CAMELOT ABANDONED: Every soul in the Somerset pews priced you out. Merlin rewinds time..."
                );
                return;
            }

            StartTownPhase();
            RefreshNightHUD();
            AutosaveWarband(gold);
        }

        /// <summary>Automatic snapshot — call after hire, battle, or Merlin advance. No UI.</summary>
        public void AutosaveWarband(int playerGold = -1)
        {
            if (playerGold < 0)
                playerGold = townUI != null ? townUI.playerGold : StartingGold;
            WarbandAutosave.SaveFromLoop(this, playerGold);
        }

        /// <summary>
        /// After every AutoBattle WAVE: entire party returns to recruitment pews with
        /// kept stats/upgrades/intel. Living: win → hire discount, loss → higher cost.
        /// Dead already recorded via RecordArmyDeath.
        /// </summary>
        public void DisbandPartyAfterMarch(HashSet<string> deadUnitIds = null, bool? waveWon = null)
        {
            if (waveWon.HasValue) lastWaveWon = waveWon;
            bool won = lastWaveWon ?? false;

            if (recruitedParty == null) recruitedParty = new List<RecruitInstance>();
            if (recruitedParty.Count == 0)
            {
                mustDisbandPartyAfterCurrentWave = false;
                rivalriesSuspendedThisWave = false;
                tavernRoundHireDiscountActive = false;
                return;
            }

            int before = recruitedParty.Count;

            // Combat deaths on fused products → leaf pew ids (for hire tax only).
            var combatDead = deadUnitIds ?? new HashSet<string>();
            var fusionLeafDead = new HashSet<string>();
            CollectFusionLeafDeaths(combatDead, fusionLeafDead);

            // Fused Legends → original pre-fusion pew units (recursive). Product discarded.
            var returning = CardFusionSystem.UnfusePartyToOriginals(recruitedParty);
            recruitedParty.Clear();

            if (currentTownPool == null)
                currentTownPool = new List<RecruitInstance>();

            // Living returnees: win −10 / loss +15 (GrudgeSaveManager constants).
            int livingDelta = won
                ? GrudgeSaveManager.MarchWinHireDiscount
                : GrudgeSaveManager.MarchLossHirePenalty;

            var seenIds = new HashSet<string>();
            int returned = 0;
            foreach (var r in returning)
            {
                if (r == null) continue;
                if (r.IsFusedLegend)
                {
                    foreach (var leaf in CardFusionSystem.UnfusePartyToOriginals(
                                 new List<RecruitInstance> { r }))
                    {
                        if (leaf != null)
                            ReturnOneToPew(leaf, combatDead, fusionLeafDead, livingDelta, won, seenIds, ref returned);
                    }
                    continue;
                }
                ReturnOneToPew(r, combatDead, fusionLeafDead, livingDelta, won, seenIds, ref returned);
            }

            // Rivalries / Mediate / Tavern Round hire discount are per-wave — expire when the wave ends.
            rivalriesSuspendedThisWave = false;
            tavernRoundHireDiscountActive = false;

            mustDisbandPartyAfterCurrentWave = false;
            Debug.Log(
                $"[DemonKnight] WAVE disband ({(won ? "WIN −10 hire" : "LOSS +15 hire")}): party {before} → pews += {returned} (unfused, full stats).");
            RefreshTownAfterDisband();
        }

        private void ReturnOneToPew(
            RecruitInstance r,
            HashSet<string> combatDead,
            HashSet<string> fusionLeafDead,
            int livingDelta,
            bool waveWon,
            HashSet<string> seenIds,
            ref int returned)
        {
            if (r == null) return;
            // Mordred is fuse/Legend only — never seats in pews (must fuse again next run).
            if (r.unitType == UnitType.Mordred)
            {
                Debug.Log("[DemonKnight] Mordred discarded on disband — never returns to a pew.");
                return;
            }
            if (!string.IsNullOrEmpty(r.unitID) && !seenIds.Add(r.unitID))
                return;

            // Full stats / roles / intel visible on pew return (win or loss).
            r.intelTierUnlocked = Mathf.Max(r.intelTierUnlocked, 3);
            r.fusionIngredients = null;
            GrudgeSaveManager.Instance?.RememberIntel(r.unitID, r.intelTierUnlocked);

            // Wave victory → +1 experience star (stacks; each star −5g hire).
            if (waveWon)
                r.victoryStars = Mathf.Max(0, r.victoryStars) + 1;

            bool diedInCombat = combatDead != null && !string.IsNullOrEmpty(r.unitID)
                                && combatDead.Contains(r.unitID);
            bool diedAsFusionLeaf = fusionLeafDead != null && !string.IsNullOrEmpty(r.unitID)
                                    && fusionLeafDead.Contains(r.unitID);

            if (diedInCombat)
            {
                // EndBattle already applied ArmyDeathHirePenalty (+15) for this unitID.
            }
            else if (diedAsFusionLeaf)
            {
                // Fused product died; leaf was not separately tagged — +15 death hire.
                GrudgeSaveManager.Instance?.RecordArmyDeath(r.unitID);
            }
            else
            {
                // Living original: win → −10 hire, loss → +15 hire (stacks with star −5 each).
                GrudgeSaveManager.Instance?.ModifyGrudgeStaging(r.unitID, livingDelta);
            }

            int grudge = GrudgeSaveManager.Instance != null
                ? GrudgeSaveManager.Instance.GetGrudgeCostPenalty(r.unitID) : 0;
            r.RefreshBodyPostureFromTraits(Mathf.Max(0, grudge));

            if (!string.IsNullOrEmpty(r.unitID) && currentTownPool != null)
                currentTownPool.RemoveAll(t => t != null && t.unitID == r.unitID && t != r);

            if (!currentTownPool.Contains(r))
                currentTownPool.Add(r);
            returned++;
        }

        /// <summary>
        /// When a fused Legend's product unitID is in combatDead, add its leaf pew unitIDs
        /// (not already in combatDead) so they receive death hire tax after unfuse.
        /// </summary>
        private void CollectFusionLeafDeaths(HashSet<string> combatDead, HashSet<string> intoLeaves)
        {
            if (combatDead == null || intoLeaves == null || recruitedParty == null) return;
            foreach (var r in recruitedParty)
            {
                if (r == null || string.IsNullOrEmpty(r.unitID) || !r.IsFusedLegend) continue;
                if (!combatDead.Contains(r.unitID)) continue;
                CollectLeafUnitIds(r, intoLeaves);
            }
            intoLeaves.RemoveWhere(id => string.IsNullOrEmpty(id) || combatDead.Contains(id));
        }

        private static void CollectLeafUnitIds(RecruitInstance r, HashSet<string> into)
        {
            if (r == null) return;
            if (r.IsFusedLegend)
            {
                foreach (var ing in r.fusionIngredients)
                    CollectLeafUnitIds(ing, into);
                return;
            }
            if (!string.IsNullOrEmpty(r.unitID))
                into.Add(r.unitID);
        }

        /// <summary>
        /// Idempotent wave-end hook. Always ends with recruitedParty.Count == 0;
        /// members sit in pews with stats intact for rehire.
        /// </summary>
        public void EnsureDisbandedAfterWave(HashSet<string> deadUnitIds = null, bool? waveWon = null)
        {
            if (waveWon.HasValue) lastWaveWon = waveWon;
            if (recruitedParty == null) recruitedParty = new List<RecruitInstance>();
            int before = recruitedParty.Count;
            Debug.Log(
                $"[DemonKnight] EnsureDisbandedAfterWave START (party={before}, flag={mustDisbandPartyAfterCurrentWave}, won={lastWaveWon}).");

            // Clear every GameLoopManager copy (Downloads sometimes has stale duplicates).
            var allLoops = FindObjectsByType<GameLoopManager>(FindObjectsSortMode.None);
            if (allLoops == null || allLoops.Length == 0)
                allLoops = new[] { this };

            foreach (var g in allLoops)
            {
                if (g == null) continue;
                if (waveWon.HasValue) g.lastWaveWon = waveWon;
                if (g.recruitedParty != null && g.recruitedParty.Count > 0)
                    g.DisbandPartyAfterMarch(deadUnitIds, g.lastWaveWon);
                else
                    g.mustDisbandPartyAfterCurrentWave = false;
            }

            // Nuclear: if Disband early-returned somehow, still unfuse + empty the list.
            if (recruitedParty.Count > 0)
            {
                Debug.LogError(
                    $"[DemonKnight] Disband left {recruitedParty.Count} hired — force unfuse + pews.");
                DisbandPartyAfterMarch(deadUnitIds, lastWaveWon);
            }

            mustDisbandPartyAfterCurrentWave = false;
            rivalriesSuspendedThisWave = false;
            tavernRoundHireDiscountActive = false;
            // Mordred never belongs in pews (fuse/Legend only).
            currentTownPool?.RemoveAll(r => r != null && r.unitType == UnitType.Mordred);
            AutosaveWarband();
            RefreshTownAfterDisband();

            int after = recruitedParty.Count;
            Debug.Log($"[DemonKnight] EnsureDisbandedAfterWave END (party={after}).");
            if (after != 0)
                Debug.LogError("[DemonKnight] WAVE DISBAND FAILED — party still hired after Ensure.");
        }

        private void RefreshTownAfterDisband()
        {
            var church = ResolveChurchUI();
            if (townUI == null) townUI = FindFirstObjectByType<TownSplitUIController>();
            if (currentTownPool != null)
            {
                currentTownPool.RemoveAll(r => r != null && r.unitType == UnitType.Mordred);
                if (church != null && church.pewSeatLayer != null)
                    church.PopulatePews(currentTownPool);
                else
                    townUI?.PopulateTownGrid(currentTownPool);
            }
            church?.RefreshHUD();
            // Compat bridge avoids CS1061/CS0122 when Downloads has a partial/old church .cs.
            DemonKnightCompatApi.UpdateWarbandHudLabel(church);
            DemonKnightCompatApi.RefreshWarbandPanel(church);
            townUI?.UpdateHUD();
        }

        private void RebindPaperDoll(RecruitInstance r)
        {
            if (r == null || rngGenerator == null) return;
            int grudge = GrudgeSaveManager.Instance != null
                ? GrudgeSaveManager.Instance.GetGrudgeCostPenalty(r.unitID) : 0;
            r.RefreshBodyPostureFromTraits(grudge);
            // Best-effort avatar from type pools (sprites are not stored in JSON).
            List<Sprite> pool = null;
            if (r.unitType == UnitType.Knight) pool = rngGenerator.knightAvatars;
            else if (r.unitType == UnitType.Warrior) pool = rngGenerator.warriorAvatars;
            else if (r.unitType == UnitType.Peasant) pool = rngGenerator.peasantAvatars;
            if (pool != null && pool.Count > 0 && r.randomAvatar == null)
                r.randomAvatar = pool[UnityEngine.Random.Range(0, pool.Count)];
            if (r.headSprite == null) r.headSprite = r.randomAvatar;
        }

        /// <summary>
        /// Updates the top-HUD Night label by GameObject name only.
        /// Deliberately does NOT call ChurchStageUIController.SetNight / SomersetChurchUI.SetNight
        /// so this file compiles even when those UI scripts are a stale partial Sync.
        /// </summary>
        public void RefreshNightHUD()
        {
            ApplyNightLabel(FormatNightWaveLabel(currentNight, CurrentWaveNumber()));
        }

        /// <summary>Upcoming / current AutoBattle wave index (1-based).</summary>
        public int CurrentWaveNumber()
        {
            if (battleEngine == null) battleEngine = FindFirstObjectByType<AutoBattlerEngine>();
            return battleEngine != null ? Mathf.Max(1, battleEngine.currentBattleNumber) : 1;
        }

        /// <summary>e.g. "Night 2 · Wave 3 (Vortigern)".</summary>
        public static string FormatNightWaveLabel(int night, int wave)
        {
            night = Mathf.Max(1, night);
            wave = Mathf.Max(1, wave);
            return $"Night {night} · {DescribeWave(wave)}";
        }

        /// <summary>Wave number + boss/horde type for HUD.</summary>
        public static string DescribeWave(int wave)
        {
            wave = Mathf.Max(1, wave);
            return wave switch
            {
                1 => "Wave 1 (Horde)",
                2 => "Wave 2 (Black Knight)",
                3 => "Wave 3 (Vortigern)",
                4 => "Wave 4 (Green Knight)",
                5 => "Wave 5 (Accolon)",
                _ => $"Wave {wave} (Nimue)"
            };
        }

        private static void ApplyNightLabel(string label)
        {
            Transform nightTf = null;
            var hud = GameObject.Find("HUD_Bar");
            if (hud != null) nightTf = hud.transform.Find("NightText");
            if (nightTf == null)
            {
                var found = GameObject.Find("NightText");
                if (found != null) nightTf = found.transform;
            }
            if (nightTf == null) return;

            var tmp = nightTf.GetComponent<TMPro.TextMeshProUGUI>();
            if (tmp != null) tmp.text = label;
        }

        private bool IsTownCompletelyPricedOut(int availableGold)
        {
            int discount = MerlinResurrectionManager.Instance != null
                ? MerlinResurrectionManager.Instance.GetGlobalCostDiscount()
                : 0;
            int cheapest = int.MaxValue;
            foreach (var person in currentTownPool)
            {
                int grudge = GrudgeSaveManager.Instance != null
                    ? GrudgeSaveManager.Instance.GetGrudgeCostPenalty(person.unitID)
                    : 0;
                int cost = person.GetFinalHireCost(grudge, discount);
                if (cost < cheapest) cheapest = cost;
            }

            return cheapest > availableGold;
        }

        private void Update()
        {
            // Esc fallback only when MainMenuUI.Update did not run (Downloads edge cases).
            // If Instance exists, MainMenuUI owns Esc (avoid double-toggle same frame).
            if (Input.GetKeyDown(KeyCode.Escape)
                && currentPhase != GamePhase.AutoBattle
                && MainMenuUI.Instance == null)
            {
                var menu = MainMenuUI.EnsureExists();
                menu?.HandleEscapeKey();
                Debug.LogError("[DemonKnight] GameLoop Esc fallback → HandleEscapeKey.");
            }

            if (currentPhase != GamePhase.TownRecruit) return;

            var openMenu = MainMenuUI.Instance;
            if (openMenu != null && openMenu.IsMenuOpen) return;

            if (isTimerRunning && !disableTownTimer)
            {
                currentTimer -= Time.deltaTime;
                townSecondsSpentThisPhase += Time.deltaTime;
                if (currentTimer <= 0f)
                {
                    currentTimer = 0f;
                    StartBattlePhase();
                }
            }
            else if (disableTownTimer)
            {
                townSecondsSpentThisPhase += Time.deltaTime;
            }

            churchUI?.HandleHotkeys();
        }

        private void LateUpdate()
        {
            // Failsafe for Downloads stale AutoBattlerEngine: any exit from AutoBattle
            // with a hired party forces per-wave disband (mid-wave win → town included).
            if (previousPhase == GamePhase.AutoBattle && currentPhase != GamePhase.AutoBattle)
            {
                if (recruitedParty != null && recruitedParty.Count > 0)
                {
                    Debug.LogError(
                        $"[DemonKnight] Left AutoBattle with {recruitedParty.Count} still hired — forcing EnsureDisbandedAfterWave.");
                    EnsureDisbandedAfterWave();
                }
            }

            if (mustDisbandPartyAfterCurrentWave && currentPhase != GamePhase.AutoBattle)
                EnsureDisbandedAfterWave();

            previousPhase = currentPhase;
        }

        public void StartTownPhase()
        {
            // Safety net: win → perk → town must not keep a hired party if EndBattle
            // skipped disband (stale AutoBattlerEngine). Per-wave, not full-run only.
            if (mustDisbandPartyAfterCurrentWave
                || (recruitedParty != null && recruitedParty.Count > 0
                    && (currentPhase == GamePhase.AutoBattle || currentPhase == GamePhase.Rewards)))
            {
                EnsureDisbandedAfterWave();
            }

            currentPhase = GamePhase.TownRecruit;
            currentTimer = townTimeLimit;
            isTimerRunning = !disableTownTimer;
            townSecondsSpentThisPhase = 0f;
            if (townCanvasObj != null) townCanvasObj.SetActive(true);
            if (battlefieldArenaObj != null) battlefieldArenaObj.SetActive(false);

            if (seatPopulateRoutine != null) StopCoroutine(seatPopulateRoutine);
            seatPopulateRoutine = StartCoroutine(PopulateTownSeatsNextFrame());
        }

        private System.Collections.IEnumerator PopulateTownSeatsNextFrame()
        {
            yield return null;
            Canvas.ForceUpdateCanvases();

            if (rngGenerator == null) rngGenerator = FindFirstObjectByType<TownRNGGenerator>();
            if (currentTownPool == null || currentTownPool.Count == 0)
            {
                if (rngGenerator != null)
                    currentTownPool = rngGenerator.GenerateNewRunTown();
            }

            var church = ResolveChurchUI();
            if (church != null && church.pewSeatLayer != null)
                church.PopulatePews(currentTownPool);
            else if (townUI != null)
                townUI.PopulateTownGrid(currentTownPool);
            else
                church?.PopulatePews(currentTownPool);

            townUI?.UpdateHUD();
            church?.RefreshHUD();
        }

        public void StartBattlePhase()
        {
            MainMenuUI.Instance?.HideMenu();
            var church = ResolveChurchUI();

            if (recruitedParty == null || recruitedParty.Count == 0)
            {
                Debug.LogError("[DemonKnight] MARCH blocked — hire at least one recruit first (party empty).");
                DemonKnightCompatApi.FlashMarchBlocked(church, "Hire at least one soul before March to War.");
                return;
            }

            if (battleEngine == null) battleEngine = FindFirstObjectByType<AutoBattlerEngine>();
            if (battleEngine == null)
            {
                Debug.LogError("[DemonKnight] MARCH blocked — AutoBattlerEngine missing in scene.");
                return;
            }

            isTimerRunning = false;
            currentPhase = GamePhase.AutoBattle;
            // Every wave that starts MUST end with a pew disband (win or lose).
            mustDisbandPartyAfterCurrentWave = true;
            church?.CloseAllModals();
            // Snapshot before March (pre-disband). Wave-end EnsureDisbanded overwrites this.
            AutosaveWarband();
            if (townCanvasObj != null) townCanvasObj.SetActive(false);
            if (battlefieldArenaObj != null) battlefieldArenaObj.SetActive(true);
            DemonKnightCompatApi.PrepareBattlefield();
            Debug.LogError($"[DemonKnight] MARCH TO WAR — party {recruitedParty.Count}, wave {battleEngine.currentBattleNumber}.");
            battleEngine.StartAutoBattle(recruitedParty);
        }

        public float GetRemainingTime() => currentTimer;

        public void AddTownTimeBonus(float seconds)
        {
            currentTimer = Mathf.Min(600f, currentTimer + seconds);
            townTimeLimit = Mathf.Min(600f, townTimeLimit + seconds);
        }
    }
}
