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

        private void Awake()
        {
            if (Instance == null) Instance = this;
            else Destroy(gameObject);
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
            if (GrudgeSaveManager.Instance != null) GrudgeSaveManager.Instance.grudgeDeltaMultiplier = 1f;
            DemonicPactManager.Instance?.ClearPact();

            currentTownPool = rngGenerator.GenerateNewRunTown();
            int bonusGold = MerlinResurrectionManager.Instance != null
                ? MerlinResurrectionManager.Instance.GetBonusStartingGold()
                : 0;

            if (townUI != null) townUI.playerGold = 75 + bonusGold;
            if (churchUI != null) churchUI.SyncGoldFromTownUI();

            int gold = townUI != null ? townUI.playerGold : 75 + bonusGold;
            if (IsTownCompletelyPricedOut(gold))
            {
                MerlinResurrectionManager.Instance?.TriggerEndOfRunResurrection(
                    0, false,
                    "CAMELOT ABANDONED: Every soul in the Somerset pews priced you out. Merlin rewinds time..."
                );
                return;
            }

            StartTownPhase();
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

            if (churchUI != null)
                churchUI.PopulatePews(currentTownPool);
            else
                townUI?.PopulateTownGrid(currentTownPool);

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
