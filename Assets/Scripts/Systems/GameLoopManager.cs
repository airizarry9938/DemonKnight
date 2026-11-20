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
        public ChurchStageUIController churchUI;
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

        private Coroutine seatPopulateRoutine;
        private const string NightPrefsKey = "SomersetNight";

        private void Awake()
        {
            if (Instance == null) Instance = this;
            else Destroy(gameObject);
            currentNight = Mathf.Max(1, PlayerPrefs.GetInt(NightPrefsKey, 1));
        }

        private void Start()
        {
            if (churchUI == null) churchUI = FindFirstObjectByType<ChurchStageUIController>();
            if (townUI == null) townUI = FindFirstObjectByType<TownSplitUIController>();
            InitializeNewRun();
        }

        public void InitializeNewRun()
        {
            recruitedParty.Clear();
            disableTownTimer = false;
            townSecondsSpentThisPhase = 0f;
            SomersetRunes.SetGrudgeDeltaMultiplier(1f);
            SomersetRunes.ClearPact();

            if (rngGenerator == null) rngGenerator = FindFirstObjectByType<TownRNGGenerator>();
            currentTownPool = rngGenerator != null
                ? rngGenerator.GenerateNewRunTown()
                : new List<RecruitInstance>();
            int bonusGold = MerlinResurrectionManager.Instance != null
                ? MerlinResurrectionManager.Instance.GetBonusStartingGold()
                : 0;

            if (townUI != null) townUI.playerGold = 75 + bonusGold;
            if (churchUI != null) churchUI.SyncGoldFromTownUI();

            int gold = townUI != null ? townUI.playerGold : 75 + bonusGold;
            if (currentTownPool.Count > 0 && IsTownCompletelyPricedOut(gold))
            {
                MerlinResurrectionManager.Instance?.TriggerEndOfRunResurrection(
                    0, false,
                    "CAMELOT ABANDONED: Every soul in the Somerset pews priced you out. Merlin rewinds time..."
                );
                return;
            }

            StartTownPhase();
            RefreshNightHUD();
        }

        /// <summary>
        /// Called from Merlin's "Enter Next Timeline" after a run ends. Advances the
        /// night counter, resets battle/perk state, and returns to town recruit.
        /// </summary>
        public void AdvanceNightAndRestart()
        {
            currentNight = Mathf.Max(1, currentNight) + 1;
            PlayerPrefs.SetInt(NightPrefsKey, currentNight);
            PlayerPrefs.Save();

            if (battleEngine != null)
            {
                battleEngine.isBattling = false;
                battleEngine.currentBattleNumber = 1;
            }

            if (PerkDraftManager.Instance != null)
                PerkDraftManager.Instance.unlockedPerks.Clear();

            // Fresh town phase length each night (perk drafts may have shortened it).
            townTimeLimit = UnityEngine.Random.Range(300f, 600f);

            if (battlefieldArenaObj != null) battlefieldArenaObj.SetActive(false);
            if (townCanvasObj != null) townCanvasObj.SetActive(true);

            InitializeNewRun();
        }

        public void RefreshNightHUD()
        {
            if (churchUI != null)
                churchUI.SetNight(currentNight);
            else
            {
                var church = FindFirstObjectByType<SomersetChurchUI>();
                church?.SetNight(currentNight);
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
            if (currentPhase != GamePhase.TownRecruit) return;

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

        public void StartTownPhase()
        {
            currentPhase = GamePhase.TownRecruit;
            currentTimer = townTimeLimit;
            isTimerRunning = !disableTownTimer;
            townSecondsSpentThisPhase = 0f;
            if (townCanvasObj != null) townCanvasObj.SetActive(true);
            if (battlefieldArenaObj != null) battlefieldArenaObj.SetActive(false);

            // Wait one frame so TownCanvas RectTransforms have real sizes, then seat everyone.
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

            if (churchUI != null && churchUI.pewSeatLayer != null)
                churchUI.PopulatePews(currentTownPool);
            else if (townUI != null)
                townUI.PopulateTownGrid(currentTownPool);
            else
                churchUI?.PopulatePews(currentTownPool);

            townUI?.UpdateHUD();
            churchUI?.RefreshHUD();
        }

        public void StartBattlePhase()
        {
            isTimerRunning = false;
            currentPhase = GamePhase.AutoBattle;
            churchUI?.CloseAllModals();
            if (townCanvasObj != null) townCanvasObj.SetActive(false);
            if (battlefieldArenaObj != null) battlefieldArenaObj.SetActive(true);
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
