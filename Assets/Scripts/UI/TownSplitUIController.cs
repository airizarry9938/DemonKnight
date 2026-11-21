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
        public int playerGold = 200;

        [Header("Top-Left: Tavern Dice")]
        public TavernDiceMinigame diceGame;
        public TextMeshProUGUI diceResultText;
        public TextMeshProUGUI currentPotText;
        public Button startDiceButton;
        public Button rollDiceButton;
        public Button cashOutButton;

        [Header("Town Crowd → Church Pews")]
        [Tooltip("Parent for seat buttons. Prefer PewSeatLayer under ChurchBackground.")]
        public Transform townGridContainer;
        public GameObject recruitAvatarButtonPrefab;
        public SomersetPewLayout pewLayout;
        public Vector2 baseSeatSize = new Vector2(48f, 64f);

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
        public Button mediateRivalryButton; // 100g: Cancel rivalries for this wave
        public Button tavernRoundButton;    // 50g: Reduce grudges across Peasants & Drunkards

        private RecruitInstance selectedRecruit;
        private GameObject selectedButtonObj;
        private List<RecruitInstance> activeTownPool = new List<RecruitInstance>();

        private void Start()
        {
            SetupButtons();
            ClearBottomDossier();
            // Church stage owns dice modal visibility — scoop always-on corner widgets
            // into Panel_TavernDice and force them hidden until the HUD Dice toggle opens.
            var church = FindFirstObjectByType<SomersetChurchUI>()
                         ?? (SomersetChurchUI)FindFirstObjectByType<ChurchStageUIController>();
            if (church != null)
            {
                DemonKnightCompatApi.ChurchEnsureDiceHudAndHidden(church);
            }
            else
            {
                HideLegacyDiceCornerIfPresent();
                ForcePanelTavernDiceInactiveLocal();
            }
        }

        /// <summary>Direct SetActive(false) on Panel_TavernDice when church UI is absent.</summary>
        private static void ForcePanelTavernDiceInactiveLocal()
        {
            foreach (var t in Resources.FindObjectsOfTypeAll<Transform>())
            {
                if (t == null || !t.gameObject.scene.IsValid()) continue;
                if (t.name != "Panel_TavernDice" && t.name != "TavernDice" && t.name != "Panel_Dice")
                    continue;
                // Never the HUD strip.
                Transform p = t;
                bool underHud = false;
                while (p != null)
                {
                    if (p.name == "HUD_Bar") { underHud = true; break; }
                    p = p.parent;
                }
                if (underHud) continue;
                if (t.gameObject.activeSelf)
                    t.gameObject.SetActive(false);
            }
        }

        /// <summary>Fallback when church UI is missing — hide top-left dice chrome (never HUD_Bar).</summary>
        private void HideLegacyDiceCornerIfPresent()
        {
            void HideGo(Component c)
            {
                if (c == null) return;
                // Never touch the top HUD Dice toggle.
                Transform walk = c.transform;
                while (walk != null)
                {
                    if (walk.name == "HUD_Bar") return;
                    walk = walk.parent;
                }

                Transform p = c.transform;
                while (p != null && p.parent != null && p.parent.GetComponent<Canvas>() == null
                       && p.GetComponent<TownSplitUIController>() == null)
                {
                    string n = p.name;
                    // HUD toggle names — skip.
                    if (n.IndexOf("DiceButton", System.StringComparison.OrdinalIgnoreCase) >= 0)
                        return;
                    if (n.IndexOf("Dice", System.StringComparison.OrdinalIgnoreCase) >= 0
                        || n.IndexOf("Tavern", System.StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        p.gameObject.SetActive(false);
                        return;
                    }
                    p = p.parent;
                }
                c.gameObject.SetActive(false);
            }

            HideGo(diceResultText);
            HideGo(currentPotText);
            HideGo(startDiceButton);
            HideGo(rollDiceButton);
            HideGo(cashOutButton);
        }

        private void Update()
        {
            if (GameLoopManager.Instance != null && GameLoopManager.Instance.isTimerRunning)
            {
                float t = GameLoopManager.Instance.GetRemainingTime();
                if (timerText != null)
                    timerText.text = $"Horde: {Mathf.FloorToInt(t / 60f):00}:{Mathf.FloorToInt(t % 60f):00}";
            }
        }

        public void PopulateTownGrid(List<RecruitInstance> generatedTown)
        {
            activeTownPool = generatedTown ?? new List<RecruitInstance>();
            if (townGridContainer == null) return;

            // Disable legacy GridLayoutGroup — seats are absolute on the church art.
            var grid = townGridContainer.GetComponent<GridLayoutGroup>();
            if (grid != null) grid.enabled = false;

            if (pewLayout == null) pewLayout = GetComponent<SomersetPewLayout>();
            if (pewLayout == null) pewLayout = FindFirstObjectByType<SomersetPewLayout>();
            if (pewLayout != null) pewLayout.EnsureValid();

            foreach (Transform child in townGridContainer) Destroy(child.gameObject);

            // Index recruits by pew seat so each of the 52 boxes can receive a character later.
            var bySeat = new Dictionary<int, RecruitInstance>();
            foreach (var recruit in activeTownPool)
            {
                int seat = Mathf.Clamp(recruit.pewSeatIndex, 0, SomersetPewLayout.TotalSeats - 1);
                if (!bySeat.ContainsKey(seat)) bySeat[seat] = recruit;
            }

            if (pewLayout != null)
            {
                // Back rows first so front pews draw on top.
                var order = new List<int>(SomersetPewLayout.TotalSeats);
                for (int i = 0; i < SomersetPewLayout.TotalSeats; i++) order.Add(i);
                order.Sort((a, b) =>
                {
                    int rowA = pewLayout.GetPewRow(pewLayout.GetPewIndexForSeat(a));
                    int rowB = pewLayout.GetPewRow(pewLayout.GetPewIndexForSeat(b));
                    int cmp = rowB.CompareTo(rowA);
                    return cmp != 0 ? cmp : a.CompareTo(b);
                });

                foreach (int seatIndex in order)
                {
                    bySeat.TryGetValue(seatIndex, out RecruitInstance recruit);
                    CreatePewSeatButton(seatIndex, recruit);
                }
            }
            else
            {
                // Fallback: flat list if pew layout is missing.
                foreach (RecruitInstance recruit in activeTownPool)
                    CreatePewSeatButton(recruit.pewSeatIndex, recruit);
            }
        }

        private void CreatePewSeatButton(int seatIndex, RecruitInstance recruit)
        {
            if (recruitAvatarButtonPrefab == null || townGridContainer == null) return;

            GameObject btnObj = Instantiate(recruitAvatarButtonPrefab, townGridContainer);
            btnObj.name = recruit != null
                ? $"Seat_{seatIndex:00}_{recruit.characterName}"
                : $"Seat_{seatIndex:00}_Empty";

            var rt = btnObj.GetComponent<RectTransform>();
            if (rt == null) rt = btnObj.AddComponent<RectTransform>();

            if (pewLayout != null)
                pewLayout.ApplySeatTransform(rt, seatIndex, baseSeatSize);

            Image img = btnObj.GetComponent<Image>();
            if (img == null) img = btnObj.AddComponent<Image>();
            if (recruit != null && recruit.randomAvatar != null)
            {
                img.sprite = recruit.randomAvatar;
                img.color = Color.white;
            }
            else if (recruit != null)
            {
                img.sprite = null;
                img.color = recruit.unitType switch
                {
                    UnitType.Knight => new Color(0.55f, 0.48f, 0.25f, 0.92f),
                    UnitType.Warrior => new Color(0.40f, 0.38f, 0.45f, 0.92f),
                    UnitType.Peasant => new Color(0.35f, 0.42f, 0.28f, 0.92f),
                    _ => new Color(0.45f, 0.40f, 0.35f, 0.90f)
                };
            }
            else
            {
                img.sprite = null;
                img.color = new Color(0.12f, 0.10f, 0.09f, 0.40f);
            }

            VictoryStarBadge.Apply(btnObj.transform, recruit);

            Button btn = btnObj.GetComponent<Button>();
            if (btn != null)
            {
                btn.onClick.RemoveAllListeners();
                btn.interactable = recruit != null;
                var colors = btn.colors;
                colors.highlightedColor = new Color(0.95f, 0.9f, 0.55f, 1f);
                colors.selectedColor = new Color(0.85f, 0.78f, 0.4f, 1f);
                btn.colors = colors;
                if (recruit != null)
                {
                    RecruitInstance captured = recruit;
                    GameObject capturedObj = btnObj;
                    var church = FindFirstObjectByType<SomersetChurchUI>()
                                 ?? (SomersetChurchUI)FindFirstObjectByType<ChurchStageUIController>();
                    // LMB toggles unit stats; RMB closes/deselects; hover = bottom quotes only.
                    btn.onClick.AddListener(() =>
                    {
                        if (church != null) church.ToggleUnitSelectionMenu(captured);
                        else SelectRecruit(captured, capturedObj);
                    });
                    PewSeatHoverHighlight.Ensure(
                        btnObj, true,
                        onHoverInfo: () =>
                        {
                            if (church != null) church.ShowHoverPreview(captured);
                            else SelectRecruit(captured, capturedObj);
                        },
                        onRightClick: () =>
                        {
                            if (church != null) church.CloseUnitSelectionMenu();
                        });
                }
                else
                    PewSeatHoverHighlight.Ensure(btnObj, false, null, null);
            }
            else
                PewSeatHoverHighlight.Ensure(btnObj, recruit != null, null, null);
        }

        public void SelectRecruit(RecruitInstance recruit, GameObject btnObj)
        {
            if (recruit == null) return;
            selectedRecruit = recruit;
            selectedButtonObj = btnObj;

            if (selectedAvatarImage != null)
            {
                var portrait = TownsfolkPortraitView.EnsureOn(
                    selectedAvatarImage,
                    selectedAvatarImage.transform.parent);
                if (portrait != null)
                    portrait.Apply(recruit);
                else
                {
                    selectedAvatarImage.enabled = true;
                    selectedAvatarImage.sprite = recruit.randomAvatar;
                    selectedAvatarImage.color = recruit.randomAvatar != null
                        ? Color.white
                        : TownsfolkVisualApplier.PlaceholderColor(recruit.unitType);
                }
            }

            int grudge = GrudgeSaveManager.Instance != null
                ? GrudgeSaveManager.Instance.GetGrudgeCostPenalty(recruit.unitID)
                : 0;
            if (characterDialogueText != null)
                characterDialogueText.text = GenerateDialogue(recruit, grudge);
            RefreshDossierUI(grudge);
        }

        private string GenerateDialogue(RecruitInstance r, int grudge)
        {
            int writ = MerlinResurrectionManager.Instance != null
                ? MerlinResurrectionManager.Instance.GetGlobalCostDiscount() : 0;
            return r.FormatHireDialogue(grudge, writ);
        }

        public void UnlockIntelTier(int tier)
        {
            if (selectedRecruit == null || selectedRecruit.intelTierUnlocked >= tier) return;

            int cost = RecruitInstance.IntelCostForTier(tier);
            if (playerGold < cost)
            {
                characterDialogueText.text = "<color=yellow>Not enough gold!</color>";
                return;
            }

            playerGold -= cost;
            selectedRecruit.intelTierUnlocked = tier;
            GrudgeSaveManager.Instance?.RememberIntel(selectedRecruit.unitID, tier);
            UpdateHUD();
            RefreshDossierUI(GrudgeSaveManager.Instance != null
                ? GrudgeSaveManager.Instance.GetGrudgeCostPenalty(selectedRecruit.unitID)
                : 0);
        }

        public void AttemptHireSelected()
        {
            // Prefer single church path (one click = one hire / one payment).
            var church = FindFirstObjectByType<SomersetChurchUI>()
                         ?? (SomersetChurchUI)FindFirstObjectByType<ChurchStageUIController>();
            if (church != null)
            {
                church.HireSelected();
                return;
            }

            if (selectedRecruit == null || GameLoopManager.Instance == null) return;

            var party = GameLoopManager.Instance.recruitedParty;
            if (party == null)
            {
                party = new List<RecruitInstance>();
                GameLoopManager.Instance.recruitedParty = party;
            }
            if (party.Contains(selectedRecruit)
                || (!string.IsNullOrEmpty(selectedRecruit.unitID)
                    && party.Exists(m => m != null && m.unitID == selectedRecruit.unitID)))
            {
                Debug.Log("[DemonKnight] Town hire ignored — already in warband.");
                return;
            }

            int grudge = GrudgeSaveManager.Instance != null
                ? GrudgeSaveManager.Instance.GetGrudgeCostPenalty(selectedRecruit.unitID)
                : 0;
            int discount = MerlinResurrectionManager.Instance != null
                ? MerlinResurrectionManager.Instance.GetGlobalCostDiscount()
                : 0;
            int finalCost = selectedRecruit.GetFinalHireCost(grudge, discount);

            if (playerGold < finalCost)
            {
                if (characterDialogueText != null)
                    characterDialogueText.text = "<color=red>Your purse is too light.</color>";
                return;
            }

            if (!PartySynergyEvaluator.CanJoinParty(selectedRecruit, party, out string refusal))
            {
                if (characterDialogueText != null)
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
            selectedRecruit.intelTierUnlocked = Mathf.Max(selectedRecruit.intelTierUnlocked, 3);
            GrudgeSaveManager.Instance?.RememberIntel(selectedRecruit.unitID, selectedRecruit.intelTierUnlocked);
            party.Add(selectedRecruit);
            activeTownPool.Remove(selectedRecruit);
            GameLoopManager.Instance.currentTownPool?.Remove(selectedRecruit);
            DemonKnightCompatApi.AutosaveWarband(GameLoopManager.Instance, playerGold);
            if (selectedButtonObj != null) Destroy(selectedButtonObj);
            UpdateHUD();
            ClearBottomDossier();
            if (characterDialogueText != null)
                characterDialogueText.text = "<color=green>Agreed. I will march with your warband.</color>";
        }

        public void TrainSelectedRole(CombatRole role)
        {
            if (selectedRecruit == null || playerGold < 15) return;
            if (role == CombatRole.BacklineRanged && !selectedRecruit.AllowsTrainRangedKit)
            {
                if (characterDialogueText != null)
                    characterDialogueText.text =
                        "<color=#FF6644>Warriors cannot Train Ranged.</color> Move them to BACK in the warband — they keep NO REACH there.";
                RefreshDossierUI(GrudgeSaveManager.Instance != null
                    ? GrudgeSaveManager.Instance.GetGrudgeCostPenalty(selectedRecruit.unitID)
                    : 0);
                return;
            }
            if (!selectedRecruit.TrainRole(role, out string bonus))
            {
                if (characterDialogueText != null)
                    characterDialogueText.text = "<color=#FFAA66>Already trained that role.</color>";
                RefreshDossierUI(GrudgeSaveManager.Instance != null
                    ? GrudgeSaveManager.Instance.GetGrudgeCostPenalty(selectedRecruit.unitID)
                    : 0);
                return;
            }
            playerGold -= 15;
            UpdateHUD();
            RefreshDossierUI(GrudgeSaveManager.Instance != null
                ? GrudgeSaveManager.Instance.GetGrudgeCostPenalty(selectedRecruit.unitID)
                : 0);
            characterDialogueText.text = $"<color=#00FFFF>Trained as {role} — {bonus}!</color>";
        }

        public void MediateSelectedRecruit()
        {
            // Prefer church Mediate (100g, wave-scoped rivalry cancel).
            var church = FindFirstObjectByType<SomersetChurchUI>()
                         ?? (SomersetChurchUI)FindFirstObjectByType<ChurchStageUIController>();
            if (church != null)
            {
                DemonKnightCompatApi.ChurchInvokeMediate(church);
                UpdateHUD();
                return;
            }

            int cost = GameLoopManager.MediateCostGold;
            if (playerGold < cost || GameLoopManager.Instance == null) return;
            if (GameLoopManager.Instance.rivalriesSuspendedThisWave)
            {
                if (characterDialogueText != null)
                    characterDialogueText.text = "<color=#7CFF9A>Rivalries already mediated for this wave.</color>";
                return;
            }

            playerGold -= cost;
            GameLoopManager.Instance.rivalriesSuspendedThisWave = true;
            UpdateHUD();
            if (characterDialogueText != null)
            {
                characterDialogueText.text =
                    $"<color=green>Mediated ({cost}g).</color> Rivalries canceled for this wave.";
            }
        }

        public void BuyTavernRound()
        {
            int cost = GameLoopManager.TavernRoundCostGold;
            if (playerGold < cost) return;
            // One Round per wave — −10g hire only (no stacked −15 grudge staging).
            if (GameLoopManager.Instance != null
                && GameLoopManager.Instance.tavernRoundHireDiscountActive)
                return;
            playerGold -= cost;

            if (GameLoopManager.Instance != null)
                GameLoopManager.Instance.tavernRoundHireDiscountActive = true;

            UpdateHUD();
            if (characterDialogueText != null)
            {
                characterDialogueText.text =
                    $"<color=yellow>Tavern Round ({cost}g)!</color> All hires −{GameLoopManager.TavernRoundHireDiscount}g this wave.";
            }
            if (selectedRecruit != null)
            {
                int grudge = GrudgeSaveManager.Instance != null
                    ? GrudgeSaveManager.Instance.GetGrudgeCostPenalty(selectedRecruit.unitID) : 0;
                RefreshDossierUI(grudge);
            }
        }

        private void RefreshDossierUI(int grudge)
        {
            if (selectedRecruit == null) return;
            int discount = MerlinResurrectionManager.Instance != null
                ? MerlinResurrectionManager.Instance.GetGlobalCostDiscount()
                : 0;

            if (dossierStatsText != null)
                dossierStatsText.text = selectedRecruit.GetDisplayDescription(grudge, discount);
            if (hireButtonText != null)
            {
                int cost = selectedRecruit.GetFinalHireCost(grudge, discount);
                hireButtonText.text = $"Recruit {cost}g ({playerGold}g)";
            }

            if (tier1RevealButton != null)
            {
                tier1RevealButton.interactable = selectedRecruit.intelTierUnlocked < 1;
                SetButtonLabel(tier1RevealButton,
                    selectedRecruit.intelTierUnlocked >= 1 ? "Scout done" : "Scout (Free)");
            }
            if (tier2RevealButton != null)
            {
                tier2RevealButton.interactable = selectedRecruit.intelTierUnlocked < 2;
                SetButtonLabel(tier2RevealButton,
                    selectedRecruit.intelTierUnlocked >= 2
                        ? "Investigate done"
                        : $"Investigate ({RecruitInstance.IntelTier2Cost}g)");
            }
            if (tier3RevealButton != null)
            {
                tier3RevealButton.interactable = selectedRecruit.intelTierUnlocked < 3;
                SetButtonLabel(tier3RevealButton,
                    selectedRecruit.intelTierUnlocked >= 3
                        ? "Full Report done"
                        : $"Full Report ({RecruitInstance.IntelTier3Cost}g)");
            }
            if (hireRecruitButton != null) hireRecruitButton.interactable = true;
            if (trainTankButton != null) trainTankButton.interactable = playerGold >= 15;
            if (trainRangedButton != null)
            {
                bool warriorBlocked = selectedRecruit != null && !selectedRecruit.AllowsTrainRangedKit;
                trainRangedButton.interactable = playerGold >= 15 && !warriorBlocked;
            }
            if (mediateRivalryButton != null)
            {
                mediateRivalryButton.interactable = playerGold >= GameLoopManager.MediateCostGold;
                var medLbl = mediateRivalryButton.GetComponentInChildren<TextMeshProUGUI>();
                if (medLbl != null)
                    medLbl.text = $"Mediate ({GameLoopManager.MediateCostGold}g)";
            }
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

        /// <summary>Church HireSelected reads selection when pew click landed here first.</summary>
        public RecruitInstance PeekSelectedRecruit() => selectedRecruit;

        /// <summary>Church completed a hire — drop town-side selection / pool ref.</summary>
        public void NotifyHiredFromChurch(RecruitInstance hired)
        {
            if (hired != null)
                activeTownPool.Remove(hired);
            if (selectedRecruit == hired || (hired != null && selectedRecruit != null
                                             && selectedRecruit.unitID == hired.unitID))
                ClearBottomDossier();
            UpdateHUD();
        }

        private void SetupButtons()
        {
            // Prefer church RewireMarchButton (clears persistent Inspector listeners).
            var church = FindFirstObjectByType<SomersetChurchUI>()
                         ?? (SomersetChurchUI)FindFirstObjectByType<ChurchStageUIController>();
            if (church != null)
                DemonKnightCompatApi.ChurchRewireMarch(church);
            else if (goToWarButton != null)
            {
                goToWarButton.onClick = new Button.ButtonClickedEvent();
                goToWarButton.onClick.AddListener(() =>
                    DemonKnightCompatApi.ResolveLoop()?.StartBattlePhase());
            }

            // Church owns dossier Hire/Train/Mediate/Intel/Tavern — avoid double-fire (2× gold).
            if (church != null)
            {
                DemonKnightCompatApi.ChurchRewireDossierActions(church);
                // Dice still owned here when no church wire; church hides panels.
            }
            else
            {
                tier1RevealButton?.onClick.AddListener(() => UnlockIntelTier(1));
                tier2RevealButton?.onClick.AddListener(() => UnlockIntelTier(2));
                tier3RevealButton?.onClick.AddListener(() => UnlockIntelTier(3));
                hireRecruitButton?.onClick.AddListener(AttemptHireSelected);
                trainTankButton?.onClick.AddListener(() => TrainSelectedRole(CombatRole.FrontlineTank));
                trainRangedButton?.onClick.AddListener(() => TrainSelectedRole(CombatRole.BacklineRanged));
                mediateRivalryButton?.onClick.AddListener(MediateSelectedRecruit);
                tavernRoundButton?.onClick.AddListener(BuyTavernRound);
            }

            startDiceButton?.onClick.AddListener(() =>
            {
                diceGame.StartGame(ref playerGold);
                UpdateHUD();
                UpdateDiceUI("Rolled in!");
            });
            rollDiceButton?.onClick.AddListener(() => diceGame.RollDie());
            cashOutButton?.onClick.AddListener(() =>
            {
                int gambler = SomersetRunes.GetRank(BankRuneID.Gambler);
                diceGame.CashOut(ref playerGold);
                UpdateHUD();
                UpdateDiceUI(gambler >= 3
                    ? "Cashed out! <color=#B8E986>Gambler R3: +15s town</color>"
                    : "Cashed out!");
            });

            if (diceGame != null)
            {
                diceGame.OnRollSuccess.AddListener((roll, pot) =>
                {
                    int gambler = SomersetRunes.GetRank(BankRuneID.Gambler);
                    string extra = (gambler >= 2 && roll == 6)
                        ? " <color=#B8E986>Gambler: ×2 on 6!</color>"
                        : "";
                    UpdateDiceUI($"Rolled {roll}! Pot: {pot}g{extra}");
                });
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

        private static void SetButtonLabel(Button button, string label)
        {
            if (button == null) return;
            var tmp = button.GetComponentInChildren<TextMeshProUGUI>();
            if (tmp != null) tmp.text = label;
        }

        public void UpdateHUD()
        {
            if (goldText != null) goldText.text = $"Royal Gold: {playerGold}g";
            int count = GameLoopManager.Instance != null
                ? GameLoopManager.Instance.recruitedParty.Count
                : 0;
            if (partyCountText != null)
                partyCountText.text = $"Warband ({count})";
            if (tavernRoundButton != null)
            {
                bool active = GameLoopManager.Instance != null
                              && GameLoopManager.Instance.tavernRoundHireDiscountActive;
                tavernRoundButton.interactable = !active
                    && playerGold >= GameLoopManager.TavernRoundCostGold;
                var lbl = tavernRoundButton.GetComponentInChildren<TextMeshProUGUI>();
                if (lbl != null)
                {
                    lbl.text = active
                        ? $"Round (−{GameLoopManager.TavernRoundHireDiscount}g hire ✓)"
                        : $"Buy Round ({GameLoopManager.TavernRoundCostGold}g)";
                }
            }

            DemonKnightCompatApi.UpdateWarbandHudLabel(FindFirstObjectByType<SomersetChurchUI>());
            FindFirstObjectByType<LegendCodexUI>()?.RefreshCodexList();
        }
    }
}
