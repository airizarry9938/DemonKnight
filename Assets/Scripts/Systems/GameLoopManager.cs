using System.Collections.Generic;
using UnityEngine;
using System.Collections;
using UnityEngine.UI;

namespace ArthurianAutobattler
{
    public class GameLoopManager : MonoBehaviour
    {
        public static GameLoopManager Instance;

#if UNITY_EDITOR
        /// <summary>
        /// DA-59: DELETE Phase 2–5 standalones if they contain public class (CS0101).
        /// Types live in GameDataTypes / PaperDollSeatView / GameLoopManager / TavernDiceMinigame.
        /// Sync no longer downloads these paths; this cleans leftovers from old pins.
        /// </summary>
        [UnityEditor.InitializeOnLoadMethod]
        static void DkDeleteDuplicatePhase25Standalones()
        {
            try
            {
                string data = Application.dataPath;
                string[] rels =
                {
                    "Scripts/Data/SpriteAnimClip.cs",
                    // tip: fix-profiles-in-gdt-always — Profiles in GameDataTypes; nuke leftover named files.
                    "Scripts/Data/StageLayoutProfile.cs",
                    "Scripts/Data/SceneBackgroundProfile.cs",
                    "Scripts/Data/ClassSpritePoolProfile.cs",
                    "Scripts/Data/LegendPewPortraitProfile.cs",
                    "Scripts/Data/BattleGoodEvilSpritePoolProfile.cs",
                    "Scripts/Data/ModularSpriteLibrary.cs",
                    "Scripts/UI/ModularSpriteAnimator.cs",
                    "Scripts/Systems/StageParallaxController.cs",
                    "Scripts/Systems/DiceManager.cs",
                };
                bool any = false;
                foreach (string rel in rels)
                {
                    string path = System.IO.Path.Combine(data, rel.Replace('/', System.IO.Path.DirectorySeparatorChar));
                    if (!System.IO.File.Exists(path)) continue;
                    string cur = System.IO.File.ReadAllText(path);
                    bool hasType = cur.IndexOf("public class ", System.StringComparison.Ordinal) >= 0
                                   || cur.IndexOf("public partial class ", System.StringComparison.Ordinal) >= 0
                                   || cur.IndexOf("class ModularSpriteLibrary", System.StringComparison.Ordinal) >= 0
                                   || cur.IndexOf("class StageLayoutProfile", System.StringComparison.Ordinal) >= 0
                                   || cur.IndexOf("class SpriteAnimClip", System.StringComparison.Ordinal) >= 0
                                   || cur.IndexOf("class ModularSpriteAnimator", System.StringComparison.Ordinal) >= 0
                                   || cur.IndexOf("class StageParallaxController", System.StringComparison.Ordinal) >= 0
                                   || cur.IndexOf("class DiceManager", System.StringComparison.Ordinal) >= 0;
                    if (!hasType) continue;
                    try
                    {
                        System.IO.File.SetAttributes(path, System.IO.FileAttributes.Normal);
                        System.IO.File.Delete(path);
                        string meta = path + ".meta";
                        if (System.IO.File.Exists(meta))
                        {
                            try { System.IO.File.SetAttributes(meta, System.IO.FileAttributes.Normal); } catch { /* ignore */ }
                            try { System.IO.File.Delete(meta); } catch { /* ignore */ }
                        }
                        any = true;
                        Debug.Log("[DemonKnight] DA-59 DELETED duplicate standalone " + rel);
                    }
                    catch (System.Exception ex)
                    {
                        Debug.LogWarning("[DemonKnight] DA-59 delete failed " + rel + ": " + ex.Message);
                    }
                }
                if (any)
                    UnityEditor.AssetDatabase.Refresh();
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning("[DemonKnight] DA-59 delete pass skipped: " + ex.Message);
            }
        }
#endif

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
        /// Buy Tavern Round (30g) → −10g hire for every recruit until this wave ends.
        /// Cleared on <see cref="EnsureDisbandedAfterWave"/> / new night.
        /// </summary>
        public bool tavernRoundHireDiscountActive;

        /// <summary>Gold cost to Mediate (wave-scoped rivalry cancel). Balance V2: 50g.</summary>
        public const int MediateCostGold = 50;

        /// <summary>Gold cost to Buy Tavern Round. Balance V2: 30g.</summary>
        public const int TavernRoundCostGold = 30;

        /// <summary>Hire discount for all units after Buy Tavern Round (this wave).</summary>
        public const int TavernRoundHireDiscount = 10;

        /// <summary>Balance V2: +10g per living marcher at wave end.</summary>
        public const int SurvivorBountyGold = 10;

        /// <summary>Phase 5 Dice Big Win — free Train purchases.</summary>
        public int trainingVouchers;

        /// <summary>Phase 5 Shepherd's Vow — combat deaths this run (fusions do not count).</summary>
        public int soulsLostThisRun;

        /// <summary>
        /// tip: fix-survivor-stats-victory-morale — cumulative combat deaths that tax party morale.
        /// Each death → −5% HP/ATK on remaining recruits (capped at −25%).
        /// </summary>
        public int moraleDeathStacksThisRun;

        public const float MoraleLossPerDeath = 0.05f;
        public const float MoraleLossCap = 0.25f;

        /// <summary>Last wave flock upkeep breakdown (Victory UI).</summary>
        [System.NonSerialized] public string lastFlockUpkeepBreakdown = "";

        /// <summary>Last wave death / survivor rows for DeathsSurvivorsMorale panel.</summary>
        [System.NonSerialized] public readonly List<WaveCasualtyEntry> lastWaveDeaths = new List<WaveCasualtyEntry>();
        [System.NonSerialized] public readonly List<WaveCasualtyEntry> lastWaveSurvivors = new List<WaveCasualtyEntry>();
        /// <summary>This wave's morale hit as percent points (e.g. 10 = −10%), before cumulative cap display.</summary>
        [System.NonSerialized] public float lastWaveMoraleLossPercent;

        private Coroutine seatPopulateRoutine;
        private const string NightPrefsKey = "SomersetNight";
        private GamePhase previousPhase;

        /// <summary>Base purse for a fresh night / New Game (before bank rune bonus). Balance V2: 250g.</summary>
        public const int StartingGold = 250;
        public const int WaveVictoryGold = 200;
        public const int WaveLossGold = 100;

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
        /// perks, relics, sins, pacts) and starts at exactly <see cref="StartingGold"/> (250g).
        /// </summary>
        public void InitializeFreshCampaign()
        {
            ResetEconomyForNewGame();
            recruitedParty.Clear();
            WarbandAutosave.Clear();
            currentNight = 1;
            pendingMidRunWaveVictory = false;
            PlayerPrefs.SetInt(NightPrefsKey, 1);
            PlayerPrefs.Save();
            if (battleEngine == null) battleEngine = FindFirstObjectByType<AutoBattlerEngine>();
            if (battleEngine != null)
            {
                battleEngine.isBattling = false;
                battleEngine.currentBattleNumber = 1;
            }
            BeginTownNight(
                keepSurvivors: false,
                restoreGoldFromSave: false,
                veterans: null,
                applyBankStartingBonus: false);

            // Belt-and-suspenders: New Game purse is always exactly StartingGold.
            ForceStartingGoldExact();
            Debug.LogError(
                $"[DemonKnight] New Game purse locked at {StartingGold}g (bank/perks/relics wiped).");
        }

        /// <summary>Legacy name — fresh start only (does not wipe an in-progress Merlin timeline).</summary>
        public void InitializeNewRun() => InitializeFreshCampaign();

        /// <summary>
        /// Wipe run + meta economy so New Game cannot inherit Merlin Treasury, Writ,
        /// bank runes, sin runes, relics, perks, pacts, or autosaved gold.
        /// Clears <b>every</b> scene manager copy (not only Instance) so Inspector defaults
        /// / duplicate components cannot keep modifiers alive.
        /// </summary>
        public void ResetEconomyForNewGame()
        {
            GrudgeSaveManager.Instance?.WipeAllSavedMemory();
            WarbandAutosave.Clear();

            // PlayerPrefs first — survives even if MerlinResurrectionManager is missing.
            MerlinResurrectionManager.WipeBankPlayerPrefs();

            var merlins = FindObjectsByType<MerlinResurrectionManager>(FindObjectsSortMode.None);
            if (merlins != null)
            {
                foreach (var m in merlins)
                    m?.ResetBankForNewGame();
            }

            PerkDraftManager.SuppressAllPerkUi();
            var perks = FindObjectsByType<PerkDraftManager>(FindObjectsSortMode.None);
            if (perks != null)
            {
                foreach (var p in perks)
                {
                    if (p == null) continue;
                    p.unlockedPerks ??= new HashSet<RunPerkID>();
                    p.unlockedPerks.Clear();
                    if (p.perkDraftPanel != null) p.perkDraftPanel.SetActive(false);
                }
            }

            var relics = FindObjectsByType<RelicManager>(FindObjectsSortMode.None);
            if (relics != null)
            {
                foreach (var r in relics)
                    r?.ClearAllRelics();
            }

            var sins = FindObjectsByType<SinRuneManager>(FindObjectsSortMode.None);
            if (sins != null)
            {
                foreach (var s in sins)
                    s?.ClearAllSins();
            }

            var pacts = FindObjectsByType<DemonicPactManager>(FindObjectsSortMode.None);
            if (pacts != null)
            {
                foreach (var d in pacts)
                    d?.ClearPact();
            }

            SomersetRunes.ClearPact();
            SomersetRunes.SetGrudgeDeltaMultiplier(1f);

            rivalriesSuspendedThisWave = false;
            tavernRoundHireDiscountActive = false;
            disableTownTimer = false;
            lastWaveWon = null;
            mustDisbandPartyAfterCurrentWave = false;
            pendingMidRunWaveVictory = false;
            townSecondsSpentThisPhase = 0f;
            trainingVouchers = 0;
            soulsLostThisRun = 0;
            moraleDeathStacksThisRun = 0;
            lastWaveMoraleLossPercent = 0f;
            lastWaveDeaths.Clear();
            lastWaveSurvivors.Clear();
            lastFlockUpkeepBreakdown = "";
            townTimeLimit = UnityEngine.Random.Range(300f, 600f);
            DiceManager.Ensure().freeRollsRemaining = 0;
            // Meta unlocks persist; only per-run counters reset.
            LegendUnlockManager.Ensure()?.ResetRunCounters();
            LegendUnlockManager.Ensure()?.LoadOrCreate();

            if (battleEngine == null) battleEngine = FindFirstObjectByType<AutoBattlerEngine>();
            if (battleEngine != null)
            {
                battleEngine.isBattling = false;
                battleEngine.currentBattleNumber = 1;
            }

            LogNewGameModifierAudit();
        }

        /// <summary>Console proof that New Game left runes/perks/relics/sins/pacts disabled.</summary>
        public void LogNewGameModifierAudit()
        {
            var merlin = MerlinResurrectionManager.Instance
                         ?? FindFirstObjectByType<MerlinResurrectionManager>();
            int gossip = SomersetRunes.GetRank(BankRuneID.Gossip);
            int gambler = SomersetRunes.GetRank(BankRuneID.Gambler);
            int blood = SomersetRunes.GetRank(BankRuneID.BloodOath);
            int crown = SomersetRunes.GetRank(BankRuneID.PeasantsCrown);
            int writ = merlin != null ? merlin.GetGlobalCostDiscount() : 0;
            int treasury = merlin != null ? merlin.GetBonusStartingGold() : 0;
            int perkCount = PerkDraftManager.Instance != null
                ? PerkDraftManager.Instance.unlockedPerks.Count : 0;
            int relicCount = RelicManager.Instance != null
                ? RelicManager.Instance.ownedRelics.Count : 0;
            bool jealousy = SomersetRunes.IsSinActive(SinRuneID.Jealousy);
            bool lust = SomersetRunes.IsSinActive(SinRuneID.Lust);
            var pact = SomersetRunes.GetActivePact();

            bool clean = treasury == 0 && writ == 0
                         && gossip == 0 && gambler == 0 && blood == 0 && crown == 0
                         && perkCount == 0 && relicCount == 0
                         && !jealousy && !lust
                         && pact == DemonicPactID.None
                         && !disableTownTimer
                         && (merlin == null || merlin.IsFullyClearedForNewGame());

            Debug.LogError(
                $"[DemonKnight] New Game modifier audit: clean={clean} | "
                + $"Treasury+{treasury}g Writ-{writ}g | "
                + $"Runes G{gossip}/Gm{gambler}/B{blood}/C{crown} | "
                + $"perks={perkCount} relics={relicCount} | "
                + $"Jealousy={jealousy} Lust={lust} Pact={pact} timerOff={disableTownTimer}");
        }

        /// <summary>Force HUD / town purse to exactly StartingGold (250).</summary>
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

        /// <summary>Blocks nested AdvanceNight when Merlin Next Timeline fires twice in one click.</summary>
        private bool advancingNight;

        /// <summary>
        /// Set by mid-run wave victory screen. Next Night must keep
        /// <see cref="AutoBattlerEngine.currentBattleNumber"/> (already bumped in EndBattle)
        /// instead of calling defeat-style <see cref="AdvanceNightAndRestart"/> which resets wave→1.
        /// </summary>
        public bool pendingMidRunWaveVictory;

        /// <summary>
        /// Mid-wave victory fallback (Merlin missing): sync Night HUD to combat wave.
        /// Does not invent a separate calendar night — mirrors currentBattleNumber.
        /// </summary>
        public void AdvanceNightCounterAfterWaveWin()
        {
            int from = Mathf.Max(1, currentNight);
            SyncNightToCombatWave();
            RefreshNightHUD();
            var church = ResolveChurchUI();
            church?.SetNight(CurrentWaveNumber());
            Debug.LogError($"[DemonKnight] Wave win HUD sync night {from} → {currentNight} (wave={CurrentWaveNumber()})");
        }

        /// <summary>
        /// Mid-run Victory → Next Night: advance night HUD, keep wave index
        /// (EndBattle already did currentBattleNumber++), return to church.
        /// Must NOT reset currentBattleNumber to 1 — that stuck players on Wave 1 forever.
        /// tip: fix-survivor-stats-victory-morale — keep pew town + trained stats (no GenerateNewRunTown).
        /// </summary>
        public void ContinueAfterWaveVictory()
        {
            if (advancingNight)
            {
                Debug.LogWarning(
                    "[DemonKnight] ContinueAfterWaveVictory ignored — already advancing.");
                return;
            }
            advancingNight = true;
            pendingMidRunWaveVictory = false;
            try
            {
                if (battleEngine == null) battleEngine = FindFirstObjectByType<AutoBattlerEngine>();
                if (battleEngine != null)
                    battleEngine.isBattling = false;

                int waveKeep = CurrentWaveNumber();
                int fromNight = Mathf.Max(1, currentNight);
                // Night HUD mirrors combat wave (same index as horde/boss spawn).
                currentNight = waveKeep;
                PlayerPrefs.SetInt(NightPrefsKey, currentNight);
                PlayerPrefs.Save();
                Debug.LogError(
                    $"[DemonKnight] Wave victory continue — HUD Night/Wave → {waveKeep} "
                    + $"(from night {fromNight}; NOT reset to 1).");

                int gold = townUI != null ? townUI.playerGold : StartingGold;
                AutosaveWarband(gold);

                // Mid-run: keep perks; only defeat/New Game clears them.
                townTimeLimit = UnityEngine.Random.Range(300f, 600f);

                if (battlefieldArenaObj != null) battlefieldArenaObj.SetActive(false);
                if (townCanvasObj != null) townCanvasObj.SetActive(true);

                // Keep current pews + trained/upgraded veterans already returned by ForceWaveDisband.
                // ContinueTimelineFromAutosave → BeginTownNight → GenerateNewRunTown was resetting
                // experienced soldiers across wave transitions.
                ContinueMidRunKeepTownAndTrainedStats(waveKeep);

                RefreshNightHUD();
                var church = ResolveChurchUI();
                church?.SetNight(CurrentWaveNumber());
                church?.RefreshHUD();
                DemonKnightCompatApi.RefreshWarbandPanel(church);
            }
            finally
            {
                advancingNight = false;
            }
        }

        /// <summary>
        /// Mid-run wave win: re-assert trained/★ from autosave onto the LIVE town pool.
        /// Does not call GenerateNewRunTown / BeginTownNight.
        /// tip: fix-survivor-stats-victory-morale
        /// </summary>
        public void ContinueMidRunKeepTownAndTrainedStats(int waveKeep)
        {
            waveKeep = Mathf.Max(1, waveKeep);
            if (battleEngine == null) battleEngine = FindFirstObjectByType<AutoBattlerEngine>();

            var data = WarbandAutosave.Load();
            if (data != null)
            {
                int savedWave = data.battleNumber > 0 ? data.battleNumber : waveKeep;
                if (battleEngine != null)
                    battleEngine.currentBattleNumber = Mathf.Max(waveKeep, savedWave);

                if (currentTownPool == null)
                    currentTownPool = new List<RecruitInstance>();

                var veterans = WarbandAutosave.ToVeteranList(data);
                if (veterans != null && veterans.Count > 0)
                {
                    foreach (var v in veterans)
                    {
                        if (v == null) continue;
                        v.intelTierUnlocked = Mathf.Max(v.intelTierUnlocked, 3);
                        GrudgeSaveManager.Instance?.RememberIntel(v.unitID, v.intelTierUnlocked);
                        RebindPaperDoll(v);
                    }
                    // Overlay trained/upgraded stats onto matching pew instances (or insert).
                    WarbandAutosave.MergeVeteransIntoTown(currentTownPool, veterans);
                }

                if (recruitedParty == null)
                    recruitedParty = new List<RecruitInstance>();

                // Persist-across-waves legends (Mordred) stay hired — restore if missing.
                var survivors = WarbandAutosave.ToRecruitList(data);
                if (survivors != null)
                {
                    foreach (var s in survivors)
                    {
                        if (s == null || string.IsNullOrEmpty(s.unitID)) continue;
                        if (!UnitPersistsAcrossWaves(s.unitType)) continue;
                        var existing = recruitedParty.Find(p => p != null && p.unitID == s.unitID);
                        if (existing != null)
                            WarbandAutosave.ApplyVeteranStats(s, existing);
                        else
                        {
                            RebindPaperDoll(s);
                            recruitedParty.Add(s);
                        }
                    }
                }

                Debug.LogError(
                    $"[DemonKnight] Mid-run keep-town: pews={currentTownPool.Count}, "
                    + $"hired={recruitedParty.Count}, veteransMerged={veterans?.Count ?? 0} "
                    + $"(trained stats preserved; no GenerateNewRunTown).");
            }
            else if (battleEngine != null)
            {
                battleEngine.currentBattleNumber = waveKeep;
            }

            if (battleEngine != null && battleEngine.currentBattleNumber < waveKeep)
                battleEngine.currentBattleNumber = waveKeep;

            StartTownPhase();
            RedrawVeteranStarIcons();
            AutosaveWarband();
        }

        /// <summary>1.0 = full strength; each combat death −5% HP/ATK, capped at −25%.</summary>
        public float GetMoraleStatMultiplier()
        {
            float loss = Mathf.Min(MoraleLossCap, Mathf.Max(0, moraleDeathStacksThisRun) * MoraleLossPerDeath);
            return Mathf.Clamp(1f - loss, 1f - MoraleLossCap, 1f);
        }

        /// <summary>Cumulative morale loss as percent points (0–25).</summary>
        public float GetMoraleLossPercent()
        {
            return (1f - GetMoraleStatMultiplier()) * 100f;
        }

        /// <summary>
        /// Stack death morale after a wave. Returns this wave's loss percent points.
        /// tip: fix-survivor-stats-victory-morale
        /// </summary>
        public float ApplyDeathMoraleFromWave(int deathCount)
        {
            if (deathCount <= 0)
            {
                lastWaveMoraleLossPercent = 0f;
                return 0f;
            }
            moraleDeathStacksThisRun = Mathf.Max(0, moraleDeathStacksThisRun) + deathCount;
            float uncapped = deathCount * MoraleLossPerDeath * 100f;
            lastWaveMoraleLossPercent = uncapped;
            Debug.LogError(
                $"[DemonKnight] Morale −{uncapped:0}% this wave from {deathCount} death(s) "
                + $"(run stacks={moraleDeathStacksThisRun}, effective −{GetMoraleLossPercent():0}% HP/ATK cap {MoraleLossCap * 100f:0}%).");
            return uncapped;
        }

        /// <summary>Build DeathsSurvivorsMorale rows from the marched army before/after end.</summary>
        public void CaptureWaveCasualties(
            System.Collections.Generic.IEnumerable<CombatUnit> playerArmy,
            HashSet<string> deadIds,
            bool playerWon)
        {
            lastWaveDeaths.Clear();
            lastWaveSurvivors.Clear();
            if (playerArmy == null) return;

            var dead = deadIds ?? new HashSet<string>();
            foreach (var u in playerArmy)
            {
                if (u == null || u.isDemon || string.IsNullOrEmpty(u.unitID)) continue;
                bool died = !u.IsAlive || dead.Contains(u.unitID);
                var entry = new WaveCasualtyEntry
                {
                    unitID = u.unitID,
                    characterName = u.unitName,
                    unitType = u.unitType,
                    effectiveHp = u.maxHealth,
                    effectiveAtk = u.attackDamage,
                    effectiveSpd = u.attacksPerSecond,
                    died = died
                };

                // Pull trained / ★ from canonical recruit when available.
                RecruitInstance canonical = FindCanonicalRecruit(u.unitID);
                if (canonical != null)
                {
                    entry.trainedHP = canonical.trainedHP;
                    entry.trainedATK = canonical.trainedATK;
                    entry.trainedSPD = canonical.trainedSPD;
                    entry.veteranRank = canonical.EffectiveVeteranRank;
                    if (string.IsNullOrEmpty(entry.characterName))
                        entry.characterName = canonical.characterName;
                }

                if (died)
                {
                    entry.rewardNote = $"Fallen — morale −{MoraleLossPerDeath * 100f:0}% HP/ATK";
                    lastWaveDeaths.Add(entry);
                }
                else
                {
                    if (playerWon)
                    {
                        int stars = entry.veteranRank + 1; // award lands around the same beat
                        entry.rewardNote = $"Survived — +1★ (now ★{Mathf.Max(1, stars)}) · trained kept";
                    }
                    else
                        entry.rewardNote = "Survived the fall — trained stats kept";
                    lastWaveSurvivors.Add(entry);
                }
            }
        }

        RecruitInstance FindCanonicalRecruit(string unitId)
        {
            if (string.IsNullOrEmpty(unitId)) return null;
            if (recruitedParty != null)
            {
                foreach (var r in recruitedParty)
                {
                    if (r != null && r.unitID == unitId) return r;
                    if (r != null && r.IsFusedLegend && r.fusionIngredients != null)
                    {
                        foreach (var leaf in r.fusionIngredients)
                            if (leaf != null && leaf.unitID == unitId) return leaf;
                    }
                }
            }
            if (currentTownPool != null)
            {
                foreach (var r in currentTownPool)
                    if (r != null && r.unitID == unitId) return r;
            }
            return null;
        }

        /// <summary>Rich text block for victory / defeat / Merlin end screens.</summary>
        public string FormatDeathsSurvivorsMoralePanel()
        {
            var sb = new System.Text.StringBuilder();
            sb.AppendLine("<b>DEATHS / SURVIVORS / MORALE</b>");
            float waveLoss = lastWaveMoraleLossPercent;
            float runLoss = GetMoraleLossPercent();
            if (lastWaveDeaths.Count == 0 && lastWaveSurvivors.Count == 0)
            {
                sb.AppendLine("<i>No march roster recorded.</i>");
                return sb.ToString();
            }

            sb.AppendLine($"<color=#FF8888>Deaths ({lastWaveDeaths.Count})</color>");
            if (lastWaveDeaths.Count == 0)
                sb.AppendLine("  — none —");
            else
            {
                foreach (var d in lastWaveDeaths)
                {
                    if (d == null) continue;
                    sb.AppendLine(
                        $"  ✕ <b>{d.characterName}</b> ({d.unitType}) — {d.rewardNote}");
                }
            }

            sb.AppendLine($"<color=#7CFF9A>Survivors ({lastWaveSurvivors.Count})</color>");
            if (lastWaveSurvivors.Count == 0)
                sb.AppendLine("  — none —");
            else
            {
                foreach (var s in lastWaveSurvivors)
                {
                    if (s == null) continue;
                    string train = (s.trainedHP + s.trainedATK + s.trainedSPD) > 0
                        ? $" · trained HP+{s.trainedHP}/ATK+{s.trainedATK}/SPD+{s.trainedSPD}"
                        : "";
                    sb.AppendLine(
                        $"  ✓ <b>{s.characterName}</b> ({s.unitType})  "
                        + $"HP {s.effectiveHp} | ATK {s.effectiveAtk} | SPD {s.effectiveSpd:F1}/s"
                        + $"{train}\n     {s.rewardNote}");
                }
            }

            if (waveLoss > 0.01f || runLoss > 0.01f)
            {
                sb.AppendLine(
                    $"<color=#FFAA66>Lost Morale: −{waveLoss:0}% this wave "
                    + $"(run −{runLoss:0}% HP/ATK on remaining, cap −{MoraleLossCap * 100f:0}%)</color>");
            }
            else
                sb.AppendLine("<color=#B8E986>Morale steady — no deaths this wave.</color>");

            return sb.ToString().TrimEnd();
        }

        /// <summary>
        /// Defeat / full-run Merlin "Next Night" (or legacy Enter Next Timeline):
        /// restart combat at Wave 1; HUD Night mirrors Wave 1 (same SoT).
        /// Mid-run wave wins must use <see cref="ContinueAfterWaveVictory"/> instead.
        /// </summary>
        public void AdvanceNightAndRestart()
        {
            if (advancingNight)
            {
                Debug.LogWarning(
                    $"[DemonKnight] AdvanceNightAndRestart ignored — already advancing (would skip to Night {currentNight + 1}).");
                return;
            }
            advancingNight = true;
            pendingMidRunWaveVictory = false;
            try
            {
                int from = Mathf.Max(1, currentNight);
                // Defeat / timeline restart: next fight is Wave 1 — HUD Night mirrors that
                // (no "Night 2 · Wave 1" mismatch vs DemonHordeGenerator).
                if (battleEngine == null) battleEngine = FindFirstObjectByType<AutoBattlerEngine>();
                if (battleEngine != null)
                {
                    battleEngine.isBattling = false;
                    battleEngine.currentBattleNumber = 1;
                }
                currentNight = 1;
                PlayerPrefs.SetInt(NightPrefsKey, currentNight);
                PlayerPrefs.Save();
                Debug.Log(
                    $"[DemonKnight] Defeat/timeline restart — HUD Night/Wave 1 "
                    + $"(was night {from}; combat wave reset to 1).");

                int gold = townUI != null ? townUI.playerGold : StartingGold;
                AutosaveWarband(gold);

                if (PerkDraftManager.Instance != null)
                    PerkDraftManager.Instance.unlockedPerks.Clear();

                townTimeLimit = UnityEngine.Random.Range(300f, 600f);

                if (battlefieldArenaObj != null) battlefieldArenaObj.SetActive(false);
                if (townCanvasObj != null) townCanvasObj.SetActive(true);

                ContinueTimelineFromAutosave();
            }
            finally
            {
                advancingNight = false;
            }
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

                // Restore wave index from autosave (mid-run victory keeps battleNumber > 1).
                if (battleEngine == null) battleEngine = FindFirstObjectByType<AutoBattlerEngine>();
                if (battleEngine != null && data.battleNumber > 0)
                    battleEngine.currentBattleNumber = Mathf.Max(1, data.battleNumber);

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
                    "CAMELOT ABANDONED: Every soul in the Somerset pews priced you out.\n"
                    + "THE GRAIL NIGHTMARE — Broken Vigil. Merlin remains missing. Grudge Memory endures."
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
        /// Master Spec Phase 1: map surviving battle clones → canonical party recruits by
        /// unitID and increment veteranRank. Fused products also push +1 onto leaf clones
        /// so unfuse keeps the stars.
        /// </summary>
        public void AwardWaveVictoryVeteranRanks(HashSet<string> survivingUnitIds)
        {
            if (survivingUnitIds == null || survivingUnitIds.Count == 0) return;
            if (recruitedParty == null) return;
            int awarded = 0;
            bool mythicBanner = false;
            foreach (var r in recruitedParty)
            {
                if (r == null || string.IsNullOrEmpty(r.unitID)) continue;
                if (!survivingUnitIds.Contains(r.unitID)) continue;
                r.AddVeteranRank(1);
                awarded++;
                if (HiddenPotentialSystem.TryAscendSurvivor(r))
                    mythicBanner = true;
                if (r.IsFusedLegend && r.fusionIngredients != null)
                {
                    foreach (var leaf in r.fusionIngredients)
                    {
                        if (leaf == null) continue;
                        leaf.AddVeteranRank(1);
                        if (HiddenPotentialSystem.TryAscendSurvivor(leaf))
                            mythicBanner = true;
                    }
                }
            }
            if (mythicBanner)
                lastMythicVesselAscensionBanner = true;
            Debug.Log($"[DemonKnight] WaveVictory veteranRank +1 on {awarded} canonical recruit(s).");
        }

        /// <summary>Set when a MythicVessel ascends this wave — victory UI may append a banner line.</summary>
        public bool lastMythicVesselAscensionBanner;

        /// <summary>Spec alias — same as <see cref="StartTownPhase"/>.</summary>
        public void StartTownRecruitPhase() => StartTownPhase();

        /// <summary>
        /// Force ★ badges on pews + warband after Next Night / StartTownRecruitPhase.
        /// </summary>
        public void RedrawVeteranStarIcons()
        {
            var church = ResolveChurchUI();
            if (townUI == null) townUI = FindFirstObjectByType<TownSplitUIController>();
            if (currentTownPool != null)
            {
                if (church != null && church.pewSeatLayer != null)
                    church.PopulatePews(currentTownPool);
                else
                    townUI?.PopulateTownGrid(currentTownPool);
            }
            church?.RefreshHUD();
            DemonKnightCompatApi.UpdateWarbandHudLabel(church);
            DemonKnightCompatApi.RefreshWarbandPanel(church);
            townUI?.UpdateHUD();
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

            // Persist-across-waves legends (Mordred by default) stay hired until killed.
            var persistKeepers = new List<RecruitInstance>();
            for (int i = recruitedParty.Count - 1; i >= 0; i--)
            {
                var r = recruitedParty[i];
                if (r == null || !UnitPersistsAcrossWaves(r.unitType)) continue;
                bool dead = !string.IsNullOrEmpty(r.unitID) && combatDead.Contains(r.unitID);
                if (!dead)
                    persistKeepers.Add(r);
                recruitedParty.RemoveAt(i);
            }

            // Fused Legends → original pre-fusion pew units (recursive). Product discarded.
            var returning = CardFusionSystem.UnfusePartyToOriginals(recruitedParty);
            recruitedParty.Clear();
            foreach (var m in persistKeepers)
                recruitedParty.Add(m);

            if (currentTownPool == null)
                currentTownPool = new List<RecruitInstance>();

            // Living returnees: win −10 / loss +0 (Balance V2 removed loss inflation).
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
                $"[DemonKnight] WAVE disband ({(won ? "WIN −10 hire" : "LOSS +0 hire")}; persist kept={persistKeepers.Count}): party {before} → pews += {returned}, hired={recruitedParty.Count}.");
            RefreshTownAfterDisband();
        }

        public static bool UnitPersistsAcrossWaves(UnitType type)
        {
            var db = CharacterDatabase.Load();
            if (db != null) return db.PersistsAcrossWaves(type);
            return type == UnitType.Mordred;
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
            // Persist legends are fuse-only — never seat in pews.
            if (UnitPersistsAcrossWaves(r.unitType))
            {
                Debug.Log($"[DemonKnight] {r.characterName} discarded on disband — never returns to a pew.");
                return;
            }
            if (!string.IsNullOrEmpty(r.unitID) && !seenIds.Add(r.unitID))
                return;

            // Full stats / roles / intel visible on pew return (win or loss).
            r.intelTierUnlocked = Mathf.Max(r.intelTierUnlocked, 3);
            r.fusionIngredients = null;
            GrudgeSaveManager.Instance?.RememberIntel(r.unitID, r.intelTierUnlocked);

            // WaveVictory ★ is awarded via AwardWaveVictoryVeteranRanks (clone→canonical)
            // before disband so fused products can push rank onto leaves. Sync here only.
            r.SyncVeteranRankFields();

            bool diedInCombat = combatDead != null && !string.IsNullOrEmpty(r.unitID)
                                && combatDead.Contains(r.unitID);
            bool diedAsFusionLeaf = fusionLeafDead != null && !string.IsNullOrEmpty(r.unitID)
                                    && fusionLeafDead.Contains(r.unitID);

            if (diedInCombat)
            {
                // Balance V2: death hire inflation removed (RecordArmyDeath is +0).
            }
            else if (diedAsFusionLeaf)
            {
                GrudgeSaveManager.Instance?.RecordArmyDeath(r.unitID);
            }
            else
            {
                // Living: win → −10 hire, loss → +0 (Balance V2).
                if (livingDelta != 0)
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
            // Persist legends never belong in pews (fuse/Legend only).
            currentTownPool?.RemoveAll(r => r != null && UnitPersistsAcrossWaves(r.unitType));
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
                currentTownPool.RemoveAll(r => r != null && UnitPersistsAcrossWaves(r.unitType));
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
            // Restore modular look (indices survive JSON; sprites rebind from library).
            var lib = rngGenerator.modularLibrary != null
                ? rngGenerator.modularLibrary
                : ModularSpriteLibrary.Load();
            if (r.appearance == null)
                r.appearance = TownsfolkAppearance.Generate(r.unitType, r.unitID, r.pewSeatIndex, lib);
            else
                r.appearance.BindSeatSalt(r.pewSeatIndex);
            if (lib != null)
                TownsfolkVisualApplier.SetLibrary(lib);

            // BODY = Tab 4 ClassSpritePool. FACE overlay = Tab 5 → headSprite (never bodySprite).
            Sprite architectBody = LegendPewPortraitApplier.ResolvePewVisual(r);
            if (architectBody != null && TownsfolkVisualApplier.IsUsablePewSprite(architectBody)
                && !LegendPewPortraitApplier.IsFacePoolSprite(architectBody))
            {
                r.bodySprite = architectBody;
                r.randomAvatar = architectBody;
            }
            else if (lib != null)
            {
                var pose = lib.GetPose(r.appearance.poseIndex);
                if (pose != null && !LegendPewPortraitApplier.IsFacePoolSprite(pose))
                {
                    r.bodySprite = pose;
                    if (r.randomAvatar == null) r.randomAvatar = pose;
                }
            }
            Sprite face = LegendPewPortraitApplier.ResolvePewFaceOverlay(r);
            r.headSprite = (face != null && !LegendPewPortraitApplier.IsBodyPoolSprite(face)) ? face : null;
            // Best-effort avatar from type pools (sprites are not stored in JSON).
            List<Sprite> pool = null;
            if (r.unitType == UnitType.Knight) pool = rngGenerator.knightAvatars;
            else if (r.unitType == UnitType.Warrior) pool = rngGenerator.warriorAvatars;
            else if (r.unitType == UnitType.Peasant) pool = rngGenerator.peasantAvatars;
            if (pool != null && pool.Count > 0 && r.randomAvatar == null)
                r.randomAvatar = pool[UnityEngine.Random.Range(0, pool.Count)];
            if (r.bodySprite == null) r.bodySprite = r.randomAvatar;
        }

        /// <summary>
        /// HUD Night number = combat wave (<see cref="AutoBattlerEngine.currentBattleNumber"/>).
        /// Keeps church bar / PlayerPrefs aligned with horde budget + boss spawn.
        /// </summary>
        public void SyncNightToCombatWave()
        {
            int wave = CurrentWaveNumber();
            if (currentNight == wave) return;
            currentNight = wave;
            PlayerPrefs.SetInt(NightPrefsKey, currentNight);
            PlayerPrefs.Save();
        }

        /// <summary>
        /// Updates the top-HUD Night/Wave label by GameObject name only.
        /// Deliberately does NOT call ChurchStageUIController.SetNight / SomersetChurchUI.SetNight
        /// so this file compiles even when those UI scripts are a stale partial Sync.
        /// Label always uses combat wave SoT (never a stale calendar Night alone).
        /// </summary>
        public void RefreshNightHUD()
        {
            SyncNightToCombatWave();
            int wave = CurrentWaveNumber();
            ApplyNightLabel(FormatNightWaveLabel(wave, wave));
        }

        /// <summary>
        /// Upcoming / current AutoBattle wave index (1-based).
        /// Same value DemonHordeGenerator / GetBossForWave / EndBattle bump use.
        /// </summary>
        public int CurrentWaveNumber()
        {
            if (battleEngine == null) battleEngine = FindFirstObjectByType<AutoBattlerEngine>();
            return battleEngine != null ? Mathf.Max(1, battleEngine.currentBattleNumber) : 1;
        }

        /// <summary>
        /// HUD bar label. Night and Wave numbers are the same combat index
        /// (<c>currentBattleNumber</c>) — Night is not a separate calendar that can drift.
        /// e.g. "Night 3 · Wave 3: The Black Knight".
        /// </summary>
        public static string FormatNightWaveLabel(int night, int wave)
        {
            // Prefer combat wave when callers pass a mismatched night (stale SetNight arg).
            int shown = Mathf.Max(1, wave > 0 ? wave : night);
            return $"Night {shown} · {DescribeWave(shown)}";
        }

        /// <summary>Wave number + boss/horde type for HUD (CharacterDatabase schedule).</summary>
        public static string DescribeWave(int wave)
        {
            wave = Mathf.Max(1, wave);
            var db = CharacterDatabase.Load();
            if (db != null) return db.DescribeWave(wave);
            // Legacy fallback aligned with DemonHordeGenerator lore order (boss from wave 3).
            return wave switch
            {
                1 => "Wave 1 (Horde)",
                2 => "Wave 2 (Horde)",
                3 => "Wave 3: The Black Knight",
                4 => "Wave 4: Sir Meleagant",
                5 => "Wave 5: King Lot of Lothian",
                6 => "Wave 6: Vortigern, Usurper King",
                7 => "Wave 7: Accolon of Gaul",
                8 => "Wave 8: The Questing Beast",
                9 => "Wave 9: The Green Knight",
                _ => $"Wave {wave}: Nimue, Queen of Thorns"
            };
        }

        /// <summary>Upcoming/active wave boss definition (null on horde-only waves).</summary>
        public static CharacterDefinition GetWaveBossDefinition(int wave = -1)
        {
            if (wave < 0)
            {
                var loop = Instance;
                wave = loop != null ? loop.CurrentWaveNumber() : 1;
            }
            return CharacterDatabase.Load()?.GetBossForWave(wave);
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
            if (tmp != null)
            {
                tmp.text = label;
                // Soft call — WaveBossHudView may lag one Force Sync behind (ScriptPaths is compiled).
                int wave = Instance != null ? Instance.CurrentWaveNumber() : 1;
                DemonKnightCompatApi.RefreshWaveBossHud(tmp, wave);
            }
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
            StageParallaxController.EnsureOnStageRoots();
            // tip: fix-architect-apply-persist-facing — StageLayout + SceneBackground on town enter.
            try
            {
                var church = ResolveChurchUI();
                church?.ApplyArchitectOnTownEnter();
            }
            catch { /* soft */ }

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
            // Master Spec Phase 5: 1 free stakes roll per StartTownRecruitPhase.
            DiceManager.Ensure().GrantTownPhaseFreeRoll();
            townUI?.UpdateHUD();
            // Master Spec: redraw veteran ★ after Next Night / StartTownRecruitPhase.
            RedrawVeteranStarIcons();
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
            // HUD must match the fight about to spawn (horde/boss use currentBattleNumber).
            SyncNightToCombatWave();
            RefreshNightHUD();
            church?.SetNight(CurrentWaveNumber());
            // Snapshot before March (pre-disband). Wave-end EnsureDisbanded overwrites this.
            AutosaveWarband();
            if (townCanvasObj != null) townCanvasObj.SetActive(false);
            if (battlefieldArenaObj != null) battlefieldArenaObj.SetActive(true);
            DemonKnightCompatApi.PrepareBattlefield();
            // tip: fix-architect-apply-persist-facing — SceneBackground + battle pools on battle enter.
            try
            {
                SceneBackgroundApplier.InvalidateCache();
                BattleGoodEvilSpritePoolApplier.InvalidateCache();
                var viz = BattlefieldVisualizer.Instance
                          ?? FindFirstObjectByType<BattlefieldVisualizer>();
                viz?.ResolveArenaBindings();
            }
            catch { /* soft */ }
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

    // ===== DA-59 EMBEDDED StageParallaxController (Force Sync nuclear — do not remove) =====
    /// <summary>
    /// Master Spec Phase 4 — parallax scroll/sway + atmosphere FX from StageLayoutProfile.
    /// Attach to Church stage root and/or Churchyard/Battlefield arena root.
    /// Also hosts a 0.15s micro-screen-shake used on Legend fuse, boss wave, and fatal blows.
    /// </summary>
    [DisallowMultipleComponent]
    public class StageParallaxController : MonoBehaviour
    {
        public static StageParallaxController Instance { get; private set; }

        [Tooltip("Defaults to Resources/StageLayoutProfile.")]
        public StageLayoutProfile profile;

        readonly List<LayerRuntime> layers = new List<LayerRuntime>();
        Transform layerRoot;
        ParticleSystem rainFx;
        Image lightningFlash;
        readonly List<Component> candleLights = new List<Component>();
        readonly List<SpriteRenderer> candleSprites = new List<SpriteRenderer>();

        Vector3 shakeOrigin;
        bool shaking;
        float shakeTimer;
        const float ShakeDuration = 0.15f;
        const float ShakeMagnitude = 0.12f;

        class LayerRuntime
        {
            public StageParallaxLayer data;
            public Transform transform;
            public SpriteRenderer sr;
            public RawImage uiImage;
            public Vector3 basePos;
            public Vector2 uvOffset;
            public Material runtimeMat;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Bootstrap()
        {
            // Ensure at least one controller exists near town / battlefield when Play starts.
            if (Object.FindFirstObjectByType<StageParallaxController>() != null) return;
            var host = new GameObject("StageParallaxController");
            host.AddComponent<StageParallaxController>();
        }

        void Awake()
        {
            if (Instance == null) Instance = this;
            if (profile == null) profile = StageLayoutProfile.Load();
            shakeOrigin = transform.localPosition;
            RebuildFromProfile();
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
            foreach (var layer in layers)
            {
                if (layer.runtimeMat != null)
                    Destroy(layer.runtimeMat);
            }
        }

        void Update()
        {
            float dt = Time.deltaTime;
            TickLayers(dt);
            TickCandles(dt);
            TickLightning(dt);
            TickShake(dt);
        }

        /// <summary>0.15s micro-screen-shake — Legend fuse / boss wave / fatal blow.</summary>
        public static void MicroShake()
        {
            var c = Instance ?? Object.FindFirstObjectByType<StageParallaxController>();
            if (c == null)
            {
                var cam = Camera.main;
                if (cam != null)
                {
                    c = cam.GetComponent<StageParallaxController>();
                    if (c == null) c = cam.gameObject.AddComponent<StageParallaxController>();
                }
            }
            c?.BeginShake();
            // Also nudge every other controller in scene (church + churchyard).
            foreach (var other in Object.FindObjectsByType<StageParallaxController>(FindObjectsSortMode.None))
            {
                if (other != null && other != c) other.BeginShake();
            }
        }

        public void BeginShake()
        {
            if (!shaking) shakeOrigin = transform.localPosition;
            shaking = true;
            shakeTimer = ShakeDuration;
        }

        public void RebuildFromProfile()
        {
            profile ??= StageLayoutProfile.Load();
            if (profile == null) return;
            profile.EnsureSlotCounts();

            if (layerRoot != null) Destroy(layerRoot.gameObject);
            layers.Clear();
            candleLights.Clear();
            candleSprites.Clear();

            layerRoot = new GameObject("ParallaxLayers").transform;
            layerRoot.SetParent(transform, false);
            layerRoot.SetAsFirstSibling();

            if (profile.parallaxLayers != null)
            {
                for (int i = 0; i < profile.parallaxLayers.Count; i++)
                {
                    var data = profile.parallaxLayers[i];
                    if (data == null) continue;
                    SpawnLayer(data, i);
                }
            }

            ConfigureRain(profile.enableRainFX);
            ConfigureLightning(profile.enableLightningFlashes);
            ConfigureCandles(profile.enableCandleFlicker);
        }

        void SpawnLayer(StageParallaxLayer data, int index)
        {
            var go = new GameObject(string.IsNullOrEmpty(data.layerName) ? $"Parallax_{index}" : data.layerName);
            go.transform.SetParent(layerRoot, false);
            var rt = new LayerRuntime
            {
                data = data,
                transform = go.transform,
                basePos = new Vector3(data.scrollSpeed.x * 0f, data.scrollSpeed.y * 0f, 0f)
            };

            // Prefer SpriteRenderer for world stages; fall back to UI RawImage under a Canvas.
            var canvas = GetComponentInParent<Canvas>();
            if (canvas != null && canvas.renderMode != RenderMode.WorldSpace)
            {
                var img = go.AddComponent<RawImage>();
                img.color = data.layerTint;
                img.raycastTarget = false;
                if (data.layerSprite != null && data.layerSprite.texture != null)
                {
                    img.texture = data.layerSprite.texture;
                    var mat = new Material(Shader.Find("UI/Default"));
                    img.material = mat;
                    rt.runtimeMat = mat;
                }
                var rect = go.GetComponent<RectTransform>() ?? go.AddComponent<RectTransform>();
                rect.anchorMin = Vector2.zero;
                rect.anchorMax = Vector2.one;
                rect.offsetMin = Vector2.zero;
                rect.offsetMax = Vector2.zero;
                rt.uiImage = img;
            }
            else
            {
                var sr = go.AddComponent<SpriteRenderer>();
                sr.sprite = data.layerSprite;
                sr.color = data.layerTint;
                sr.sortingOrder = data.sortingOrder;
                rt.sr = sr;
            }

            go.transform.localPosition = rt.basePos;
            layers.Add(rt);
        }

        void TickLayers(float dt)
        {
            float time = Time.time;
            for (int i = 0; i < layers.Count; i++)
            {
                var layer = layers[i];
                if (layer?.data == null || layer.transform == null) continue;
                var d = layer.data;
                layer.uvOffset += d.scrollSpeed * dt;
                Vector2 sway = new Vector2(
                    Mathf.Sin(time * d.swayFrequency + i) * d.swayAmplitude.x,
                    Mathf.Cos(time * d.swayFrequency * 0.85f + i * 0.7f) * d.swayAmplitude.y);
                Vector3 pos = layer.basePos + new Vector3(sway.x, sway.y, 0f);
                // Position scroll (seamless-ish wrap for world sprites).
                pos.x += Mathf.Repeat(layer.uvOffset.x, 20f) - 10f;
                pos.y += Mathf.Repeat(layer.uvOffset.y, 12f) - 6f;
                layer.transform.localPosition = pos;

                if (layer.uiImage != null && layer.uiImage.texture != null)
                {
                    // UV scroll via uvRect when possible.
                    var uv = layer.uiImage.uvRect;
                    uv.x = Mathf.Repeat(layer.uvOffset.x * 0.05f, 1f);
                    uv.y = Mathf.Repeat(layer.uvOffset.y * 0.05f, 1f);
                    layer.uiImage.uvRect = uv;
                }
            }
        }

        void ConfigureRain(bool enable)
        {
            if (rainFx != null)
            {
                Destroy(rainFx.gameObject);
                rainFx = null;
            }
            if (!enable) return;

            var go = new GameObject("RainFX");
            go.transform.SetParent(transform, false);
            go.transform.localPosition = new Vector3(0f, 4f, 0f);
            rainFx = go.AddComponent<ParticleSystem>();
            var main = rainFx.main;
            main.startLifetime = 0.9f;
            main.startSpeed = 8f;
            main.startSize = 0.05f;
            main.startColor = new Color(0.7f, 0.8f, 0.95f, 0.55f);
            main.maxParticles = 400;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            var emission = rainFx.emission;
            emission.rateOverTime = 120f;
            var shape = rainFx.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(14f, 0.2f, 1f);
            var vel = rainFx.velocityOverLifetime;
            vel.enabled = true;
            vel.x = new ParticleSystem.MinMaxCurve(-0.5f, 0.5f);
            vel.y = new ParticleSystem.MinMaxCurve(-6f, -10f);
            rainFx.Play();
        }

        float lightningTimer = 3f;
        void ConfigureLightning(bool enable)
        {
            if (lightningFlash != null)
            {
                Destroy(lightningFlash.gameObject);
                lightningFlash = null;
            }
            if (!enable) return;

            var canvas = GetComponentInParent<Canvas>();
            if (canvas == null)
            {
                // World flash via SpriteRenderer
                var go = new GameObject("LightningFlash");
                go.transform.SetParent(transform, false);
                var sr = go.AddComponent<SpriteRenderer>();
                sr.sprite = WhiteSprite();
                sr.color = new Color(1f, 1f, 1f, 0f);
                sr.sortingOrder = 500;
                go.transform.localScale = new Vector3(40f, 24f, 1f);
                lightningWorld = sr;
            }
            else
            {
                var go = new GameObject("LightningFlash", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                go.transform.SetParent(canvas.transform, false);
                var rt = go.GetComponent<RectTransform>();
                rt.anchorMin = Vector2.zero;
                rt.anchorMax = Vector2.one;
                rt.offsetMin = Vector2.zero;
                rt.offsetMax = Vector2.zero;
                lightningFlash = go.GetComponent<Image>();
                lightningFlash.color = new Color(1f, 1f, 1f, 0f);
                lightningFlash.raycastTarget = false;
            }
            lightningTimer = Random.Range(2.5f, 5.5f);
        }

        SpriteRenderer lightningWorld;

        void TickLightning(float dt)
        {
            if (lightningFlash == null && lightningWorld == null) return;
            lightningTimer -= dt;
            if (lightningTimer > 0f) return;
            lightningTimer = Random.Range(3f, 7f);
            StartCoroutine(LightningPulse());
        }

        IEnumerator LightningPulse()
        {
            for (int i = 0; i < 2; i++)
            {
                SetLightningAlpha(0.55f);
                yield return new WaitForSeconds(0.04f);
                SetLightningAlpha(0f);
                yield return new WaitForSeconds(0.05f);
            }
            SetLightningAlpha(0.35f);
            yield return new WaitForSeconds(0.06f);
            SetLightningAlpha(0f);
        }

        void SetLightningAlpha(float a)
        {
            if (lightningFlash != null)
            {
                var c = lightningFlash.color;
                c.a = a;
                lightningFlash.color = c;
            }
            if (lightningWorld != null)
            {
                var c = lightningWorld.color;
                c.a = a;
                lightningWorld.color = c;
            }
        }

        void ConfigureCandles(bool enable)
        {
            foreach (var sr in candleSprites)
                if (sr != null) Destroy(sr.gameObject);
            candleSprites.Clear();
            candleLights.Clear();
            if (!enable) return;

            // Up to 4 candle flickers along the stage.
            for (int i = 0; i < 4; i++)
            {
                var go = new GameObject($"Candle_{i}");
                go.transform.SetParent(transform, false);
                go.transform.localPosition = new Vector3(-4.5f + i * 3f, -1.2f, 0f);
                var sr = go.AddComponent<SpriteRenderer>();
                sr.sprite = WhiteSprite();
                sr.color = new Color(1f, 0.72f, 0.35f, 0.85f);
                sr.sortingOrder = 40;
                go.transform.localScale = new Vector3(0.15f, 0.35f, 1f);
                candleSprites.Add(sr);

                // Optional URP Light2D via reflection (no hard asmdef dependency).
                var lightType = System.Type.GetType(
                    "UnityEngine.Rendering.Universal.Light2D, Unity.RenderPipelines.Universal.Runtime");
                if (lightType != null)
                {
                    var light = go.AddComponent(lightType);
                    candleLights.Add(light);
                    TrySetLightFloat(light, "intensity", 0.8f);
                    TrySetLightColor(light, new Color(1f, 0.7f, 0.35f, 1f));
                }
            }
        }

        void TickCandles(float dt)
        {
            float time = Time.time;
            for (int i = 0; i < candleSprites.Count; i++)
            {
                var sr = candleSprites[i];
                if (sr == null) continue;
                float flicker = 0.75f + 0.25f * Mathf.PerlinNoise(time * 3.5f, i * 1.7f);
                var c = sr.color;
                c.a = 0.55f + 0.35f * flicker;
                sr.color = c;
                sr.transform.localScale = new Vector3(0.15f * flicker, 0.35f * flicker, 1f);
                if (i < candleLights.Count && candleLights[i] != null)
                    TrySetLightFloat(candleLights[i], "intensity", 0.5f + 0.6f * flicker);
            }
        }

        void TickShake(float dt)
        {
            if (!shaking) return;
            shakeTimer -= dt;
            if (shakeTimer <= 0f)
            {
                shaking = false;
                transform.localPosition = shakeOrigin;
                return;
            }
            float damp = shakeTimer / ShakeDuration;
            Vector2 offset = Random.insideUnitCircle * ShakeMagnitude * damp;
            transform.localPosition = shakeOrigin + new Vector3(offset.x, offset.y, 0f);
        }

        static void TrySetLightFloat(Component light, string prop, float value)
        {
            if (light == null) return;
            var p = light.GetType().GetProperty(prop);
            if (p != null && p.CanWrite) p.SetValue(light, value);
        }

        static void TrySetLightColor(Component light, Color color)
        {
            if (light == null) return;
            var p = light.GetType().GetProperty("color");
            if (p != null && p.CanWrite) p.SetValue(light, color);
        }

        static Sprite s_white;
        static Sprite WhiteSprite()
        {
            if (s_white != null) return s_white;
            var tex = new Texture2D(4, 4, TextureFormat.RGBA32, false);
            var px = new Color[16];
            for (int i = 0; i < 16; i++) px[i] = Color.white;
            tex.SetPixels(px);
            tex.Apply();
            s_white = Sprite.Create(tex, new Rect(0, 0, 4, 4), new Vector2(0.5f, 0.5f), 4f);
            return s_white;
        }

        /// <summary>Ensure controllers on known church / battlefield roots.</summary>
        public static void EnsureOnStageRoots()
        {
            TryAttach(GameObject.Find("SomersetChurch"));
            TryAttach(GameObject.Find("ChurchStage"));
            TryAttach(GameObject.Find("BattlefieldArena"));
            TryAttach(GameObject.Find("Churchyard"));
            var loop = GameLoopManager.Instance;
            if (loop != null)
            {
                if (loop.townCanvasObj != null) TryAttach(loop.townCanvasObj);
                if (loop.battlefieldArenaObj != null) TryAttach(loop.battlefieldArenaObj);
            }
        }

        static void TryAttach(GameObject go)
        {
            if (go == null) return;
            if (go.GetComponent<StageParallaxController>() == null)
                go.AddComponent<StageParallaxController>();
        }
    }

}
