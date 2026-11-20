using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace ArthurianAutobattler
{
    /// <summary>
    /// Full-screen Somerset church stage: characters seated on the 12 painted pews
    /// (3 columns × 4 rows) via SomersetPewLayout. Modals: T/C/R/B/Esc.
    /// </summary>
    public class SomersetChurchUI : MonoBehaviour
    {
        [Header("HUD")]
        public TextMeshProUGUI nightText;
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
        public SomersetPewLayout pewLayout;
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
            AutoWireDossierFromTownUI();
            EnsureTopHubLayout();

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

            if (GameLoopManager.Instance != null)
                SetNight(GameLoopManager.Instance.currentNight);

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
                    b?.onClick.AddListener(CloseFloatingModals);
            }

            // Dice/codex/warband start closed. Dossier bottom panel stays visible.
            CloseFloatingModals();
            if (dossierModal != null) dossierModal.SetActive(true);
        }

        /// <summary>
        /// Scene builder wires texts onto TownSplitUIController; copy them onto church stage
        /// so pew clicks actually fill the dossier panel.
        /// </summary>
        private void AutoWireDossierFromTownUI()
        {
            var town = Town;
            if (town == null) return;

            if (dossierModal == null)
            {
                var found = transform.Find("Panel_Dossier");
                if (found != null) dossierModal = found.gameObject;
            }

            if (dossierDialogue == null) dossierDialogue = town.characterDialogueText;
            if (dossierStats == null) dossierStats = town.dossierStatsText;
            if (dossierPortrait == null) dossierPortrait = town.selectedAvatarImage;
            if (hireButton == null) hireButton = town.hireRecruitButton;
            if (hireButtonLabel == null) hireButtonLabel = town.hireButtonText;
            if (tier1Button == null) tier1Button = town.tier1RevealButton;
            if (tier2Button == null) tier2Button = town.tier2RevealButton;
            if (tier3Button == null) tier3Button = town.tier3RevealButton;
            if (trainTankButton == null) trainTankButton = town.trainTankButton;
            if (trainRangedButton == null) trainRangedButton = town.trainRangedButton;
            if (mediateButton == null) mediateButton = town.mediateRivalryButton;
            if (goToWarButton == null) goToWarButton = town.goToWarButton;
            if (buyRoundButton == null) buyRoundButton = town.tavernRoundButton;
            if (timerText == null) timerText = town.timerText;
            if (goldText == null) goldText = town.goldText;
            if (warbandCountText == null) warbandCountText = town.partyCountText;
        }

        /// <summary>
        /// Night label + even spacing for Codex / Warband info / March to War.
        /// Safe to call on already-built scenes — repositions existing HUD children.
        /// </summary>
        public void EnsureTopHubLayout()
        {
            Transform hud = FindHudBar();
            if (hud == null) return;

            if (nightText == null)
            {
                var existing = hud.Find("NightText");
                if (existing != null) nightText = existing.GetComponent<TextMeshProUGUI>();
            }
            if (nightText == null)
                nightText = CreateHudLabel(hud, "NightText", "Night 1", new Vector2(-540f, 0f), new Vector2(130f, 40f), 22);

            if (timerText == null)
            {
                var t = hud.Find("TimerText");
                if (t != null) timerText = t.GetComponent<TextMeshProUGUI>();
            }
            if (goldText == null)
            {
                var g = hud.Find("GoldText");
                if (g != null) goldText = g.GetComponent<TextMeshProUGUI>();
            }

            // Status cluster on the left.
            SetHudRect(timerText != null ? timerText.rectTransform : null, new Vector2(-390f, 0f), new Vector2(200f, 40f));
            SetHudRect(goldText != null ? goldText.rectTransform : null, new Vector2(-220f, 0f), new Vector2(150f, 40f));
            SetHudRect(nightText != null ? nightText.rectTransform : null, new Vector2(-540f, 0f), new Vector2(130f, 40f));

            // Codex / Warband / March evenly spaced across the right two-thirds.
            const float trioLeft = -40f;
            const float trioRight = 520f;
            float Span(int i) => trioLeft + (trioRight - trioLeft) * (i / 2f);

            Button codex = ResolveCodexButton(hud);
            RectTransform warbandRt = ResolveWarbandInfo(hud);
            Button march = goToWarButton;
            if (march == null)
            {
                var m = hud.Find("GoToWarButton");
                if (m != null) march = m.GetComponent<Button>();
                goToWarButton = march;
            }

            SetHudRect(codex != null ? codex.transform as RectTransform : null, new Vector2(Span(0), 0f), new Vector2(140f, 44f));
            SetHudRect(warbandRt, new Vector2(Span(1), 0f), new Vector2(180f, 44f));
            SetHudRect(march != null ? march.transform as RectTransform : null, new Vector2(Span(2), 0f), new Vector2(190f, 48f));

            if (codexButton == null) codexButton = codex;
        }

        public void SetNight(int night)
        {
            if (nightText == null)
            {
                var hud = FindHudBar();
                if (hud != null)
                {
                    var existing = hud.Find("NightText");
                    if (existing != null) nightText = existing.GetComponent<TextMeshProUGUI>();
                }
            }
            if (nightText != null)
                nightText.text = $"Night {Mathf.Max(1, night)}";
        }

        private Transform FindHudBar()
        {
            var loop = GameLoopManager.Instance;
            if (loop != null && loop.townCanvasObj != null)
            {
                var hud = loop.townCanvasObj.transform.Find("HUD_Bar");
                if (hud != null) return hud;
            }
            var town = Town;
            if (town != null)
            {
                var hud = town.transform.Find("HUD_Bar");
                if (hud != null) return hud;
                if (town.transform.parent != null)
                {
                    hud = town.transform.parent.Find("HUD_Bar");
                    if (hud != null) return hud;
                }
            }
            var found = GameObject.Find("HUD_Bar");
            return found != null ? found.transform : null;
        }

        private Button ResolveCodexButton(Transform hud)
        {
            if (codexButton != null) return codexButton;
            var codexUi = FindFirstObjectByType<LegendCodexUI>();
            if (codexUi != null && codexUi.toggleCodexButton != null)
            {
                codexButton = codexUi.toggleCodexButton;
                return codexButton;
            }
            Transform t = hud.Find("CodexToggle") ?? hud.Find("CodexButton");
            if (t != null)
            {
                codexButton = t.GetComponent<Button>();
                return codexButton;
            }
            return null;
        }

        private RectTransform ResolveWarbandInfo(Transform hud)
        {
            if (warbandButton == null)
            {
                var wb = hud.Find("WarbandButton");
                if (wb != null) warbandButton = wb.GetComponent<Button>();
            }
            if (warbandButton != null)
            {
                if (warbandCountText == null)
                    warbandCountText = warbandButton.GetComponentInChildren<TextMeshProUGUI>();
                return warbandButton.transform as RectTransform;
            }

            if (warbandCountText == null)
            {
                var party = hud.Find("PartyText");
                if (party != null) warbandCountText = party.GetComponent<TextMeshProUGUI>();
            }
            return warbandCountText != null ? warbandCountText.rectTransform : null;
        }

        private TextMeshProUGUI CreateHudLabel(Transform hud, string name, string text, Vector2 pos, Vector2 size, float fontSize)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
            go.transform.SetParent(hud, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
            var tmp = go.GetComponent<TextMeshProUGUI>();
            tmp.text = text;
            tmp.fontSize = fontSize;
            tmp.alignment = TextAlignmentOptions.Left;
            tmp.color = new Color(0.92f, 0.88f, 0.78f);
            if (timerText != null && timerText.font != null) tmp.font = timerText.font;
            else if (goldText != null && goldText.font != null) tmp.font = goldText.font;
            return tmp;
        }

        private static void SetHudRect(RectTransform rt, Vector2 pos, Vector2 size)
        {
            if (rt == null) return;
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
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
            if (pewLayout == null) pewLayout = GetComponent<SomersetPewLayout>();
            if (pewLayout == null) pewLayout = gameObject.AddComponent<SomersetPewLayout>();
            pewLayout.ApplyLatestDefaultsIfNeeded();
            pewLayout.EnsureValid();

            if (pewSeatLayer == null)
            {
                if (pewLayout.backgroundRect != null) pewSeatLayer = pewLayout.backgroundRect;
                else pewSeatLayer = transform as RectTransform;
            }

            foreach (Transform child in pewSeatLayer) Destroy(child.gameObject);
            seats.Clear();

            town ??= new List<RecruitInstance>();
            var bySeat = new Dictionary<int, RecruitInstance>();
            foreach (var recruit in town)
            {
                int seat = Mathf.Clamp(recruit.pewSeatIndex, 0, SomersetPewLayout.TotalSeats - 1);
                if (!bySeat.ContainsKey(seat)) bySeat[seat] = recruit;
            }

            // Draw back rows first so front pew characters sit on top.
            var order = new List<int>(SomersetPewLayout.TotalSeats);
            for (int i = 0; i < SomersetPewLayout.TotalSeats; i++) order.Add(i);
            order.Sort((a, b) =>
            {
                int rowA = pewLayout.GetPewRow(pewLayout.GetPewIndexForSeat(a));
                int rowB = pewLayout.GetPewRow(pewLayout.GetPewIndexForSeat(b));
                int cmp = rowB.CompareTo(rowA);
                return cmp != 0 ? cmp : a.CompareTo(b);
            });

            var townUI = Town;
            GameObject prefab = paperDollSeatPrefab;
            if (prefab == null && townUI != null) prefab = townUI.recruitAvatarButtonPrefab;
            if (prefab == null)
            {
                Debug.LogError("[DemonKnight] No seat prefab — assign PaperDollSeat or RecruitAvatarButton.");
                return;
            }

            Canvas.ForceUpdateCanvases();
            if (pewLayout.backgroundRect == null && pewSeatLayer != null)
                pewLayout.backgroundRect = pewSeatLayer.parent as RectTransform;

            foreach (int seatIndex in order)
            {
                bySeat.TryGetValue(seatIndex, out RecruitInstance recruit);

                GameObject go = Instantiate(prefab, pewSeatLayer);
                go.name = recruit != null
                    ? $"Seat_{seatIndex:00}_{recruit.characterName}"
                    : $"Seat_{seatIndex:00}_Empty";

                var rt = go.GetComponent<RectTransform>();
                if (rt == null) rt = go.AddComponent<RectTransform>();
                pewLayout.ApplySeatTransform(rt, seatIndex, baseSeatSize);

                var view = go.GetComponent<PaperDollSeatView>();
                if (view != null)
                {
                    if (recruit != null) view.Bind(recruit, OnSeatClicked);
                    else view.BindEmpty();
                    seats.Add(view);
                }
                else
                {
                    EnsureSeatImage(go, recruit);
                    Button btn = go.GetComponent<Button>();
                    if (btn != null)
                    {
                        btn.onClick.RemoveAllListeners();
                        btn.interactable = recruit != null;
                        if (recruit != null)
                        {
                            RecruitInstance captured = recruit;
                            btn.onClick.AddListener(() => OnSeatClicked(captured));
                        }
                    }
                }
            }

            Debug.Log($"[DemonKnight] Populated {SomersetPewLayout.TotalSeats} pew seats ({bySeat.Count} occupied).");
            RefreshHUD();
        }

        private static void EnsureSeatImage(GameObject go, RecruitInstance recruit)
        {
            Image img = go.GetComponent<Image>();
            if (img == null) img = go.AddComponent<Image>();
            if (recruit != null && recruit.randomAvatar != null)
            {
                img.sprite = recruit.randomAvatar;
                img.color = Color.white;
            }
            else if (recruit != null)
            {
                img.sprite = null;
                img.color = SeatColorForType(recruit.unitType);
            }
            else
            {
                img.sprite = null;
                img.color = new Color(0.12f, 0.10f, 0.09f, 0.40f);
            }
            img.raycastTarget = recruit != null;
        }

        private static Color SeatColorForType(UnitType type) => type switch
        {
            UnitType.Knight => new Color(0.55f, 0.48f, 0.25f, 0.92f),
            UnitType.Warrior => new Color(0.40f, 0.38f, 0.45f, 0.92f),
            UnitType.Peasant => new Color(0.35f, 0.42f, 0.28f, 0.92f),
            _ => new Color(0.45f, 0.40f, 0.35f, 0.90f)
        };

        private void OnSeatClicked(RecruitInstance recruit)
        {
            if (recruit == null) return;
            AutoWireDossierFromTownUI();

            selected = recruit;
            if (dossierModal != null)
            {
                dossierModal.SetActive(true);
                dossierModal.transform.SetAsLastSibling(); // draw above pew seats
            }

            RefreshDossier();

            // Also drive the town dossier path (same Panel_Dossier texts).
            var town = Town;
            if (town != null)
                town.SelectRecruit(recruit, null);

            Debug.Log($"[DemonKnight] Selected {recruit.characterName} (pew seat {recruit.pewSeatIndex}).");
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
                Sprite portrait = selected.headSprite != null ? selected.headSprite : selected.randomAvatar;
                dossierPortrait.sprite = portrait;
                if (portrait == null)
                    dossierPortrait.color = selected.unitType switch
                    {
                        UnitType.Knight => new Color(0.55f, 0.48f, 0.25f, 1f),
                        UnitType.Warrior => new Color(0.40f, 0.38f, 0.45f, 1f),
                        UnitType.Peasant => new Color(0.35f, 0.42f, 0.28f, 1f),
                        _ => new Color(0.45f, 0.40f, 0.35f, 1f)
                    };
                else
                    dossierPortrait.color = Color.white;
            }

            string dialogue = grudge > 0
                ? $"\"I died in this pew last timeline. My price rose by {grudge}g.\""
                : selected.primaryTrait == TraitTag.Loyalist
                    ? "\"For King Arthur! My blade is yours at half price.\""
                    : selected.primaryTrait == TraitTag.Coward
                        ? "\"I'm no hero... unless you pay to train me or steel my nerves.\""
                        : "\"Demons claw the churchyard. Show gold — or a plan.\"";

            if (dossierDialogue != null) dossierDialogue.text = dialogue;
            if (dossierStats != null) dossierStats.text = selected.GetDisplayDescription();
            if (gossipText != null) gossipText.text = BuildGossip(selected);

            int cost = selected.GetFinalHireCost(grudge, discount);
            if (hireButtonLabel != null) hireButtonLabel.text = $"Recruit ({cost}g)";

            if (tier1Button != null) tier1Button.interactable = selected.intelTierUnlocked < 1;
            if (tier2Button != null) tier2Button.interactable = selected.intelTierUnlocked < 2;
            if (tier3Button != null) tier3Button.interactable = selected.intelTierUnlocked < 3;
            if (hireButton != null) hireButton.interactable = true;
            if (trainTankButton != null) trainTankButton.interactable = Town != null && Town.playerGold >= 15;
            if (trainRangedButton != null) trainRangedButton.interactable = Town != null && Town.playerGold >= 15;
            if (mediateButton != null) mediateButton.interactable = Town != null && Town.playerGold >= 40;
        }

        private string BuildGossip(RecruitInstance center)
        {
            int rank = SomersetRunes.GetRank(BankRuneID.Gossip);
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
            CloseFloatingModals();
            modal.SetActive(true);
            modal.transform.SetAsLastSibling();
        }

        /// <summary>Close floating overlays only — keep the bottom dossier panel visible.</summary>
        public void CloseFloatingModals()
        {
            if (diceModal != null) diceModal.SetActive(false);
            if (codexModal != null) codexModal.SetActive(false);
            if (warbandModal != null) warbandModal.SetActive(false);
        }

        public void CloseAllModals() => CloseFloatingModals();

        public void RefreshHUD()
        {
            var town = Town;
            var loop = GameLoopManager.Instance;
            if (loop != null) SetNight(loop.currentNight);
            if (goldText != null && town != null) goldText.text = $"Royal Gold: {town.playerGold}g";
            if (warbandCountText != null && loop != null)
                warbandCountText.text = $"Warband: {loop.recruitedParty.Count}/52";
            if (timerText != null && loop != null)
            {
                if (SomersetRunes.IsTownTimerDisabled())
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
