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
        public float townTimeLimit = 420f; // 7 minutes default
        private float currentTimer;
        public bool isTimerRunning = false;

        [Header("Scene References")]
        public TownRNGGenerator rngGenerator;
        public TownSplitUIController townUI;
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
            InitializeNewRun();
        }

        public void InitializeNewRun()
        {
            recruitedParty.Clear();
            currentTownPool = rngGenerator.GenerateNewRunTown();
            int bonusGold = MerlinResurrectionManager.Instance != null
                ? MerlinResurrectionManager.Instance.GetBonusStartingGold()
                : 0;
            townUI.playerGold = 75 + bonusGold;

            if (IsTownCompletelyPricedOut(townUI.playerGold))
            {
                MerlinResurrectionManager.Instance?.TriggerEndOfRunResurrection(
                    0, false,
                    "CAMELOT ABANDONED: Every citizen fears your leadership and priced you out. Merlin rewinds time..."
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
            if (currentPhase == GamePhase.TownRecruit && isTimerRunning)
            {
                currentTimer -= Time.deltaTime;
                if (currentTimer <= 0f)
                {
                    currentTimer = 0f;
                    StartBattlePhase();
                }
            }
        }

        public void StartTownPhase()
        {
            currentPhase = GamePhase.TownRecruit;
            currentTimer = townTimeLimit;
            isTimerRunning = true;
            if (townCanvasObj != null) townCanvasObj.SetActive(true);
            if (battlefieldArenaObj != null) battlefieldArenaObj.SetActive(false);
            townUI.PopulateTownGrid(currentTownPool);
            townUI.UpdateHUD();
        }

        public void StartBattlePhase()
        {
            isTimerRunning = false;
            currentPhase = GamePhase.AutoBattle;
            if (townCanvasObj != null) townCanvasObj.SetActive(false);
            if (battlefieldArenaObj != null) battlefieldArenaObj.SetActive(true);
            battleEngine.StartAutoBattle(recruitedParty);
        }

        public float GetRemainingTime() => currentTimer;
    }
}
