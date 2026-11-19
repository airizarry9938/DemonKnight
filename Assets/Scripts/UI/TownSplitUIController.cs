using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace ArthurianAutobattler
{
    public class TownSplitUIController : MonoBehaviour
    {
        [Header("Global HUD")]
        public TextMeshProUGUI timerText;
        public TextMeshProUGUI goldText;
        public TextMeshProUGUI partyCountText;
        public Button goToWarButton;
        public int playerGold = 75;

        [Header("Top-Left: Tavern Dice")]
        public TavernDiceMinigame diceGame;
        public TextMeshProUGUI diceResultText;
        public TextMeshProUGUI currentPotText;
        public Button startDiceButton;
        public Button rollDiceButton;
        public Button cashOutButton;

        [Header("Top-Right: Town Crowd Grid")]
        public Transform townGridContainer;
        public GameObject recruitAvatarButtonPrefab;

        [Header("Bottom: Dossier, Role Training & Mediation")]
        public Image selectedAvatarImage;
        public TextMeshProUGUI characterDialogueText;
        public TextMeshProUGUI dossierStatsText;
        public Button tier1RevealButton;
        public Button tier2RevealButton;
        public Button tier3RevealButton;
        public Button hireRecruitButton;
        public TextMeshProUGUI hireButtonText;

        [Header("Role Training & Morale Buttons")]
        public Button trainTankButton;      // 15g: Train Frontline Tank (+25% HP)
        public Button trainRangedButton;    // 15g: Train Backline Ranged (+20% ATK)
        public Button mediateRivalryButton; // 40g: Remove rivalry / cure cowardice
        public Button tavernRoundButton;    // 50g: Reduce grudges across Peasants & Drunkards

        private RecruitInstance selectedRecruit;
        private GameObject selectedButtonObj;
        private List<RecruitInstance> activeTownPool = new List<RecruitInstance>();

        private void Start()
        {
            SetupButtons();
            ClearBottomDossier();
        }

        private void Update()
        {
            if (GameLoopManager.Instance != null && GameLoopManager.Instance.isTimerRunning)
            {
                float t = GameLoopManager.Instance.GetRemainingTime();
                if (timerText != null)
                    timerText.text = $"Horde Arrives: {Mathf.FloorToInt(t / 60f):00}:{Mathf.FloorToInt(t % 60f):00}";
            }
        }

        public void PopulateTownGrid(List<RecruitInstance> generatedTown)
        {
            activeTownPool = generatedTown;
            foreach (Transform child in townGridContainer) Destroy(child.gameObject);

            foreach (RecruitInstance recruit in activeTownPool)
            {
                GameObject btnObj = Instantiate(recruitAvatarButtonPrefab, townGridContainer);
                Image img = btnObj.GetComponent<Image>();
                if (img != null) img.sprite = recruit.randomAvatar;

                Button btn = btnObj.GetComponent<Button>();
                if (btn != null)
                {
                    RecruitInstance captured = recruit;
                    GameObject capturedObj = btnObj;
                    btn.onClick.AddListener(() => SelectRecruit(captured, capturedObj));
                }
            }
        }

        public void SelectRecruit(RecruitInstance recruit, GameObject btnObj)
        {
            selectedRecruit = recruit;
            selectedButtonObj = btnObj;
            selectedAvatarImage.enabled = true;
            selectedAvatarImage.sprite = recruit.randomAvatar;

            int grudge = GrudgeSaveManager.Instance != null
                ? GrudgeSaveManager.Instance.GetGrudgeCostPenalty(recruit.unitID)
                : 0;
            characterDialogueText.text = GenerateDialogue(recruit, grudge);
            RefreshDossierUI(grudge);
        }

        private string GenerateDialogue(RecruitInstance r, int grudge)
        {
            if (grudge > 0)
                return $"\"I remember dying in the last timeline, Commander. My price rose by {grudge}g.\"";
            if (r.primaryTrait == TraitTag.Loyalist)
                return "\"For King Arthur! My blade is yours at half price.\"";
            if (r.primaryTrait == TraitTag.Coward)
                return "\"I'm no hero... unless you pay to train me or steel my nerves.\"";
            return "\"Demons at the gate? Show me gold or prove we have a plan.\"";
        }

        public void UnlockIntelTier(int tier)
        {
            if (selectedRecruit == null || selectedRecruit.intelTierUnlocked >= tier) return;

            int cost = tier == 1 ? 10 : (tier == 2 ? 50 : 100);
            if (playerGold < cost)
            {
                characterDialogueText.text = "<color=yellow>Not enough gold!</color>";
                return;
            }

            playerGold -= cost;
            selectedRecruit.intelTierUnlocked = tier;
            UpdateHUD();
            RefreshDossierUI(GrudgeSaveManager.Instance != null
                ? GrudgeSaveManager.Instance.GetGrudgeCostPenalty(selectedRecruit.unitID)
                : 0);
        }

        public void AttemptHireSelected()
        {
            if (selectedRecruit == null) return;

            int grudge = GrudgeSaveManager.Instance != null
                ? GrudgeSaveManager.Instance.GetGrudgeCostPenalty(selectedRecruit.unitID)
                : 0;
            int discount = MerlinResurrectionManager.Instance != null
                ? MerlinResurrectionManager.Instance.GetGlobalCostDiscount()
                : 0;
            int finalCost = selectedRecruit.GetFinalHireCost(grudge, discount);

            if (playerGold < finalCost)
            {
                characterDialogueText.text = "<color=red>Your purse is too light.</color>";
                return;
            }

            if (!PartySynergyEvaluator.CanJoinParty(selectedRecruit, GameLoopManager.Instance.recruitedParty, out string refusal))
            {
                characterDialogueText.text = $"<color=red>{refusal}</color>";
                return;
            }

            if (selectedRecruit.assignedRole == CombatRole.Unassigned)
            {
                selectedRecruit.assignedRole = selectedRecruit.unitType == UnitType.Peasant
                    ? CombatRole.BacklineRanged
                    : CombatRole.FrontlineTank;
            }

            playerGold -= finalCost;
            GameLoopManager.Instance.recruitedParty.Add(selectedRecruit);
            activeTownPool.Remove(selectedRecruit);
            Destroy(selectedButtonObj);
            UpdateHUD();
            ClearBottomDossier();
            characterDialogueText.text = "<color=green>Agreed. I will march with your warband.</color>";
        }

        public void TrainSelectedRole(CombatRole role)
        {
            if (selectedRecruit == null || playerGold < 15) return;
            playerGold -= 15;
            selectedRecruit.TrainRole(role);
            UpdateHUD();
            RefreshDossierUI(GrudgeSaveManager.Instance != null
                ? GrudgeSaveManager.Instance.GetGrudgeCostPenalty(selectedRecruit.unitID)
                : 0);
            characterDialogueText.text = $"<color=#00FFFF>Trained as {role}!</color>";
        }

        public void MediateSelectedRecruit()
        {
            if (selectedRecruit == null || playerGold < 40) return;
            playerGold -= 40;
            selectedRecruit.hatedTrait = TraitTag.None;
            if (selectedRecruit.primaryTrait == TraitTag.Coward)
                selectedRecruit.primaryTrait = TraitTag.Loyalist;

            GrudgeSaveManager.Instance?.ModifyGrudgeStaging(selectedRecruit.unitID, -20);
            UpdateHUD();
            RefreshDossierUI(GrudgeSaveManager.Instance != null
                ? GrudgeSaveManager.Instance.GetGrudgeCostPenalty(selectedRecruit.unitID)
                : 0);
            characterDialogueText.text = "<color=green>Old grudges set aside. I will stand firm.</color>";
        }

        public void BuyTavernRound()
        {
            if (playerGold < 50) return;
            playerGold -= 50;

            foreach (var p in activeTownPool)
            {
                if (p.unitType == UnitType.Peasant || p.primaryTrait == TraitTag.Drunkard)
                    GrudgeSaveManager.Instance?.ModifyGrudgeStaging(p.unitID, -15);
            }

            UpdateHUD();
            characterDialogueText.text = "<color=yellow>A round for the tavern! Spirits rise across Camelot!</color>";
        }

        private void RefreshDossierUI(int grudge)
        {
            int discount = MerlinResurrectionManager.Instance != null
                ? MerlinResurrectionManager.Instance.GetGlobalCostDiscount()
                : 0;

            dossierStatsText.text = selectedRecruit.GetDisplayDescription();
            hireButtonText.text = $"Recruit ({selectedRecruit.GetFinalHireCost(grudge, discount)}g)";

            tier1RevealButton.interactable = selectedRecruit.intelTierUnlocked < 1;
            tier2RevealButton.interactable = selectedRecruit.intelTierUnlocked < 2;
            tier3RevealButton.interactable = selectedRecruit.intelTierUnlocked < 3;
            hireRecruitButton.interactable = true;
            trainTankButton.interactable = playerGold >= 15;
            trainRangedButton.interactable = playerGold >= 15;
            mediateRivalryButton.interactable = playerGold >= 40;
        }

        private void ClearBottomDossier()
        {
            selectedRecruit = null;
            if (selectedAvatarImage != null) selectedAvatarImage.enabled = false;
            if (dossierStatsText != null)
                dossierStatsText.text = "Select a citizen on the right to inspect, train, mediate, or recruit.";
            if (hireButtonText != null) hireButtonText.text = "Recruit";

            if (tier1RevealButton != null) tier1RevealButton.interactable = false;
            if (tier2RevealButton != null) tier2RevealButton.interactable = false;
            if (tier3RevealButton != null) tier3RevealButton.interactable = false;
            if (hireRecruitButton != null) hireRecruitButton.interactable = false;
            if (trainTankButton != null) trainTankButton.interactable = false;
            if (trainRangedButton != null) trainRangedButton.interactable = false;
            if (mediateRivalryButton != null) mediateRivalryButton.interactable = false;
        }

        private void SetupButtons()
        {
            goToWarButton?.onClick.AddListener(() => GameLoopManager.Instance.StartBattlePhase());
            tier1RevealButton?.onClick.AddListener(() => UnlockIntelTier(1));
            tier2RevealButton?.onClick.AddListener(() => UnlockIntelTier(2));
            tier3RevealButton?.onClick.AddListener(() => UnlockIntelTier(3));
            hireRecruitButton?.onClick.AddListener(AttemptHireSelected);
            trainTankButton?.onClick.AddListener(() => TrainSelectedRole(CombatRole.FrontlineTank));
            trainRangedButton?.onClick.AddListener(() => TrainSelectedRole(CombatRole.BacklineRanged));
            mediateRivalryButton?.onClick.AddListener(MediateSelectedRecruit);
            tavernRoundButton?.onClick.AddListener(BuyTavernRound);

            startDiceButton?.onClick.AddListener(() =>
            {
                diceGame.StartGame(ref playerGold);
                UpdateHUD();
                UpdateDiceUI("Rolled in!");
            });
            rollDiceButton?.onClick.AddListener(() => diceGame.RollDie());
            cashOutButton?.onClick.AddListener(() =>
            {
                diceGame.CashOut(ref playerGold);
                UpdateHUD();
                UpdateDiceUI("Cashed out!");
            });

            if (diceGame != null)
            {
                diceGame.OnRollSuccess.AddListener((roll, pot) => UpdateDiceUI($"Rolled {roll}! Pot: {pot}g"));
                diceGame.OnBust.AddListener(() => UpdateDiceUI("<color=red>ROLLED A 1! BUST!</color>"));
            }
        }

        private void UpdateDiceUI(string msg)
        {
            if (diceResultText != null) diceResultText.text = msg;
            if (currentPotText != null) currentPotText.text = $"Pot: {diceGame.currentPot}g";
            if (startDiceButton != null)
                startDiceButton.interactable = !diceGame.isPlaying && playerGold >= diceGame.entryFee;
            if (rollDiceButton != null) rollDiceButton.interactable = diceGame.isPlaying;
            if (cashOutButton != null)
                cashOutButton.interactable = diceGame.isPlaying && diceGame.currentPot > 0;
        }

        public void UpdateHUD()
        {
            if (goldText != null) goldText.text = $"Royal Gold: {playerGold}g";
            if (partyCountText != null && GameLoopManager.Instance != null)
                partyCountText.text = $"Warband: {GameLoopManager.Instance.recruitedParty.Count} Fighters";
            if (tavernRoundButton != null) tavernRoundButton.interactable = playerGold >= 50;

            FindFirstObjectByType<LegendCodexUI>()?.RefreshCodexList();
        }
    }
}
