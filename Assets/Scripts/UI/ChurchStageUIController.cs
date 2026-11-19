using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace ArthurianAutobattler
{
    /// <summary>
    /// Full-screen Somerset church stage: characters seated on the 12 painted pews
    /// (3 columns × 4 rows) via ChurchPewLayout. Modals: T/C/R/B/Esc.
    /// </summary>
    public class ChurchStageUIController : MonoBehaviour
    {
        [Header("HUD")]
        public TextMeshProUGUI timerText;
        public TextMeshProUGUI goldText;
        public TextMeshProUGUI warbandCountText;
        public Transform relicIconsBar;
        public Button goToWarButton;
        public Button tavernDiceButton;
        public Button codexButton;
        public Button warbandButton;
        public Button buyRoundButton;

        [Header("Church Stage")]
        public Image churchBackgroundImage;
        public ChurchPewLayout pewLayout;
        public RectTransform pewSeatLayer;
        public GameObject paperDollSeatPrefab;
        public Vector2 baseSeatSize = new Vector2(56f, 72f);

        [Header("Modals (mutual exclusion)")]
        public GameObject diceModal;
        public GameObject codexModal;
        public GameObject warbandModal;
        public GameObject dossierModal;
        public Button[] modalCloseButtons;

        [Header("Dossier")]
        public Image dossierPortrait;
        public TextMeshProUGUI dossierDialogue;
        public TextMeshProUGUI dossierStats;
        public TextMeshProUGUI gossipText;
        public Button tier1Button;
        public Button tier2Button;
        public Button tier3Button;
        public Button trainTankButton;
        public Button trainRangedButton;
        public Button mediateButton;
        public Button hireButton;
        public TextMeshProUGUI hireButtonLabel;

        [Header("Warband Formation")]
        public Transform warbandListContainer;
        public GameObject warbandRowPrefab;

        private RecruitInstance selected;
        private readonly List<PaperDollSeatView> seats = new List<PaperDollSeatView>();

        private void Start()
        {
            goToWarButton?.onClick.AddListener(() => GameLoopManager.Instance?.StartBattlePhase());
            tavernDiceButton?.onClick.AddListener(() => OpenModal(diceModal));
            codexButton?.onClick.AddListener(() =>
            {
                OpenModal(codexModal);
                FindFirstObjectByType<LegendCodexUI>()?.RefreshCodexList();
            });
            warbandButton?.onClick.AddListener(() =>
            {
                OpenModal(warbandModal);
                RefreshWarbandPanel();
            });
            buyRoundButton?.onClick.AddListener(BuyTavernRound);

            tier1Button?.onClick.AddListener(() => UnlockIntel(1));
            tier2Button?.onClick.AddListener(() => UnlockIntel(2));
            tier3Button?.onClick.AddListener(() => UnlockIntel(3));
            trainTankButton?.onClick.AddListener(() => Train(CombatRole.FrontlineTank));
            trainRangedButton?.onClick.AddListener(() => Train(CombatRole.BacklineRanged));
            mediateButton?.onClick.AddListener(Mediate);
            hireButton?.onClick.AddListener(HireSelected);

            if (modalCloseButtons != null)
            {
                foreach (var b in modalCloseButtons)
                    b?.onClick.AddListener(CloseAllModals);
            }

            CloseAllModals();
        }

        public void HandleHotkeys()
        {
            if (Input.GetKeyDown(KeyCode.Escape)) CloseAllModals();
            if (Input.GetKeyDown(KeyCode.T)) OpenModal(diceModal);
            if (Input.GetKeyDown(KeyCode.C))
            {
                OpenModal(codexModal);
                FindFirstObjectByType<LegendCodexUI>()?.RefreshCodexList();
            }
            if (Input.GetKeyDown(KeyCode.R))
            {
                OpenModal(warbandModal);
                RefreshWarbandPanel();
            }
            if (Input.GetKeyDown(KeyCode.B)) BuyTavernRound();
        }

        public void SyncGoldFromTownUI() { }

        public void PopulatePews(List<RecruitInstance> town)
        {
            if (paperDollSeatPrefab == null) return;

            if (pewLayout == null) pewLayout = GetComponent<ChurchPewLayout>();
            if (pewLayout == null) pewLayout = gameObject.AddComponent<ChurchPewLayout>();
            pewLayout.EnsureValid();

            if (pewSeatLayer == null)
            {
                if (pewLayout.backgroundRect != null) pewSeatLayer = pewLayout.backgroundRect;
                else pewSeatLayer = transform as RectTransform;
            }

            foreach (Transform child in pewSeatLayer) Destroy(child.gameObject);
            seats.Clear();

            // Sort by seat so draw order is back→front (higher pew row first), then left→right.
            List<RecruitInstance> ordered = new List<RecruitInstance>(town);
            ordered.Sort((a, b) =>
            {
                int pewA = pewLayout.GetPewIndexForSeat(a.pewSeatIndex);
                int pewB = pewLayout.GetPewIndexForSeat(b.pewSeatIndex);
                int rowA = pewLayout.GetPewRow(pewA);
                int rowB = pewLayout.GetPewRow(pewB);
                int rowCmp = rowB.CompareTo(rowA); // back rows first
                if (rowCmp != 0) return rowCmp;
                return a.pewSeatIndex.CompareTo(b.pewSeatIndex);
            });

            foreach (var recruit in ordered)
            {
                GameObject go = Instantiate(paperDollSeatPrefab, pewSeatLayer);
                var rt = go.GetComponent<RectTransform>();
                if (rt == null) rt = go.AddComponent<RectTransform>();

                Vector2 norm = pewLayout.GetNormalizedSeatPosition(recruit.pewSeatIndex);
                float scale = pewLayout.GetSeatScale(recruit.pewSeatIndex);
                rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
                rt.pivot = new Vector2(0.5f, 0.15f); // feet/seat near pew bench
                rt.anchoredPosition = pewLayout.NormalizedToLocal(norm);
                rt.sizeDelta = baseSeatSize * scale;
                rt.localScale = Vector3.one;

                var view = go.GetComponent<PaperDollSeatView>();
                if (view == null) view = go.AddComponent<PaperDollSeatView>();
                view.Bind(recruit, OnSeatClicked);
                seats.Add(view);
            }

            RefreshHUD();
        }

        private void OnSeatClicked(RecruitInstance recruit)
        {
            selected = recruit;
            OpenModal(dossierModal);
            RefreshDossier();
        }

        private void RefreshDossier()
        {
            if (selected == null) return;
            int grudge = GrudgeSaveManager.Instance != null
                ? GrudgeSaveManager.Instance.GetGrudgeCostPenalty(selected.unitID) : 0;
            int discount = MerlinResurrectionManager.Instance != null
                ? MerlinResurrectionManager.Instance.GetGlobalCostDiscount() : 0;

            if (dossierPortrait != null)
            {
                dossierPortrait.enabled = true;
                dossierPortrait.sprite = selected.headSprite != null ? selected.headSprite : selected.randomAvatar;
            }

            if (dossierDialogue != null)
            {
                dossierDialogue.text = grudge > 0
                    ? $"\"I died in this pew last timeline. My price rose by {grudge}g.\""
                    : "\"Demons claw the churchyard. Show gold — or a plan.\"";
            }

            if (dossierStats != null) dossierStats.text = selected.GetDisplayDescription();
            if (gossipText != null) gossipText.text = BuildGossip(selected);
            if (hireButtonLabel != null)
                hireButtonLabel.text = $"Recruit ({selected.GetFinalHireCost(grudge, discount)}g)";
        }

        private string BuildGossip(RecruitInstance center)
        {
            int rank = MerlinResurrectionManager.Instance != null
                ? MerlinResurrectionManager.Instance.GetRuneRank(BankRuneID.Gossip) : 0;
            if (rank <= 0) return "Gossip Rune inactive — upgrade it in Merlin's Bank.";

            var pool = GameLoopManager.Instance?.currentTownPool;
            if (pool == null || pewLayout == null) return "";

            int centerPew = pewLayout.GetPewIndexForSeat(center.pewSeatIndex);
            int centerRow = pewLayout.GetPewRow(centerPew);

            List<string> lines = new List<string> { $"<b>Pew whispers (row {centerRow + 1}):</b>" };
            foreach (var n in pool)
            {
                if (n.unitID == center.unitID) continue;
                int nPew = pewLayout.GetPewIndexForSeat(n.pewSeatIndex);
                if (pewLayout.GetPewRow(nPew) != centerRow) continue;

                // Same pew or adjacent pew in the same row (left/right columns).
                int colDist = Mathf.Abs(pewLayout.GetPewColumn(nPew) - pewLayout.GetPewColumn(centerPew));
                int seatDist = Mathf.Abs(n.pewSeatIndex - center.pewSeatIndex);
                bool adjacent = (nPew == centerPew && seatDist == 1) || (colDist == 1 && nPew != centerPew);
                if (rank == 1 && !adjacent) continue;
                if (rank == 2 && colDist > 1 && nPew != centerPew) continue;

                int g = GrudgeSaveManager.Instance != null
                    ? GrudgeSaveManager.Instance.GetGrudgeCostPenalty(n.unitID) : 0;

                if (rank == 1)
                    lines.Add($"{n.characterName}: Trait {n.primaryTrait}");
                else if (rank == 2)
                    lines.Add($"{n.characterName}: {n.primaryTrait}" + (g > 0 ? " [GRUDGE]" : ""));
                else
                {
                    string loyal = n.primaryTrait == TraitTag.Loyalist ? " ★LOYALIST" : "";
                    lines.Add($"{n.characterName}: {n.primaryTrait} HP {n.maxHealth} ATK {n.attackDamage}{loyal}");
                }
            }

            return string.Join("\n", lines);
        }

        private TownSplitUIController Town =>
            GameLoopManager.Instance != null ? GameLoopManager.Instance.townUI : FindFirstObjectByType<TownSplitUIController>();

        private void UnlockIntel(int tier)
        {
            if (selected == null || Town == null) return;
            int cost = tier == 1 ? 10 : (tier == 2 ? 50 : 100);
            if (Town.playerGold < cost || selected.intelTierUnlocked >= tier) return;
            Town.playerGold -= cost;
            selected.intelTierUnlocked = tier;
            RefreshDossier();
            RefreshHUD();
        }

        private void Train(CombatRole role)
        {
            if (selected == null || Town == null || Town.playerGold < 15) return;
            Town.playerGold -= 15;
            selected.TrainRole(role);
            RefreshDossier();
            RefreshHUD();
        }

        private void Mediate()
        {
            if (selected == null || Town == null || Town.playerGold < 40) return;
            Town.playerGold -= 40;
            selected.hatedTrait = TraitTag.None;
            if (selected.primaryTrait == TraitTag.Coward) selected.primaryTrait = TraitTag.Loyalist;
            GrudgeSaveManager.Instance?.ModifyGrudgeStaging(selected.unitID, -20);
            selected.RefreshBodyPostureFromTraits(0);
            RefreshDossier();
            RefreshHUD();
        }

        private void HireSelected()
        {
            if (selected == null || Town == null || GameLoopManager.Instance == null) return;
            int grudge = GrudgeSaveManager.Instance != null
                ? GrudgeSaveManager.Instance.GetGrudgeCostPenalty(selected.unitID) : 0;
            int discount = MerlinResurrectionManager.Instance != null
                ? MerlinResurrectionManager.Instance.GetGlobalCostDiscount() : 0;
            int cost = selected.GetFinalHireCost(grudge, discount);
            if (Town.playerGold < cost) return;
            if (!PartySynergyEvaluator.CanJoinParty(selected, GameLoopManager.Instance.recruitedParty, out string reason))
            {
                if (dossierDialogue != null) dossierDialogue.text = $"<color=red>{reason}</color>";
                return;
            }

            if (selected.assignedRole == CombatRole.Unassigned)
                selected.assignedRole = selected.unitType == UnitType.Peasant
                    ? CombatRole.BacklineRanged : CombatRole.FrontlineTank;

            Town.playerGold -= cost;
            GameLoopManager.Instance.recruitedParty.Add(selected);
            GameLoopManager.Instance.currentTownPool.Remove(selected);
            PopulatePews(GameLoopManager.Instance.currentTownPool);
            CloseAllModals();
            RefreshHUD();
        }

        private void BuyTavernRound()
        {
            var town = Town;
            if (town == null || town.playerGold < 50) return;
            town.playerGold -= 50;
            if (GameLoopManager.Instance != null)
            {
                foreach (var p in GameLoopManager.Instance.currentTownPool)
                {
                    if (p.unitType == UnitType.Peasant || p.primaryTrait == TraitTag.Drunkard)
                        GrudgeSaveManager.Instance?.ModifyGrudgeStaging(p.unitID, -15);
                }
            }
            RefreshHUD();
        }

        private void RefreshWarbandPanel()
        {
            if (warbandListContainer == null || GameLoopManager.Instance == null) return;
            foreach (Transform c in warbandListContainer) Destroy(c.gameObject);
            foreach (var m in GameLoopManager.Instance.recruitedParty)
            {
                if (warbandRowPrefab == null) break;
                GameObject row = Instantiate(warbandRowPrefab, warbandListContainer);
                var label = row.GetComponentInChildren<TextMeshProUGUI>();
                if (label != null) label.text = $"{m.characterName} | {m.assignedRole}";
                var buttons = row.GetComponentsInChildren<Button>();
                if (buttons.Length >= 2)
                {
                    var captured = m;
                    buttons[0].onClick.AddListener(() => { captured.TrainRole(CombatRole.FrontlineTank); RefreshWarbandPanel(); });
                    buttons[1].onClick.AddListener(() => { captured.TrainRole(CombatRole.BacklineRanged); RefreshWarbandPanel(); });
                }
            }
        }

        public void OpenModal(GameObject modal)
        {
            if (modal == null) return;
            CloseAllModals();
            modal.SetActive(true);
        }

        public void CloseAllModals()
        {
            if (diceModal != null) diceModal.SetActive(false);
            if (codexModal != null) codexModal.SetActive(false);
            if (warbandModal != null) warbandModal.SetActive(false);
            if (dossierModal != null) dossierModal.SetActive(false);
        }

        public void RefreshHUD()
        {
            var town = Town;
            var loop = GameLoopManager.Instance;
            if (goldText != null && town != null) goldText.text = $"Royal Gold: {town.playerGold}g";
            if (warbandCountText != null && loop != null)
                warbandCountText.text = $"Warband: {loop.recruitedParty.Count}/52";
            if (timerText != null && loop != null)
            {
                if (loop.disableTownTimer)
                    timerText.text = "Horde Arrives: ∞ (Endless Hourglass)";
                else
                {
                    float t = loop.GetRemainingTime();
                    timerText.text = $"Horde Arrives: {Mathf.FloorToInt(t / 60f):00}:{Mathf.FloorToInt(t % 60f):00}";
                }
            }

            if (buyRoundButton != null && town != null)
                buyRoundButton.interactable = town.playerGold >= 50;
        }

        private void Update()
        {
            if (GameLoopManager.Instance != null
                && GameLoopManager.Instance.currentPhase == GamePhase.TownRecruit)
            {
                RefreshHUD();
            }
        }
    }
}
