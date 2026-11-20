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
        public Button menuButton;

        [Header("HUD Layout")]
        [Tooltip("Off (default): Play keeps editor-authored HUD_Bar / button RectTransforms. On: legacy equal-slot auto layout overwrites positions.")]
        public bool forceEqualHudSlots = false;

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
        private bool hudButtonsWired;
        /// <summary>True only while the player opened Dice via HUD/hotkey T.</summary>
        private bool diceModalUserOpen;
        private bool codexModalUserOpen;
        private bool warbandModalUserOpen;
        private List<GameObject> cachedDicePanels;
        private float nextDicePanelRescan;
        private bool diceToggleWireLogged;
        private bool warbandToggleWireLogged;
        /// <summary>Re-entrancy lock — Town + Church used to both fire Hire on one click.</summary>
        private bool hireInProgress;

        private void Awake()
        {
            // Scene often serializes Panel_TavernDice Active=true — kill it before first frame.
            diceModalUserOpen = false;
            ForceNamedTavernDicePanelsInactive();
        }

        private void OnEnable()
        {
            if (!diceModalUserOpen)
                ForceNamedTavernDicePanelsInactive();
        }

        private void Start()
        {
            diceModalUserOpen = false;
            codexModalUserOpen = false;
            warbandModalUserOpen = false;
            ForceNamedTavernDicePanelsInactive();

            AutoWireDossierFromTownUI();
            EnsureDiceModalAsPopup();
            EnsureCodexModalLarge();
            EnsureWarbandModal();
            EnsureTopHubLayout();
            ApplyEnlargedMenuFonts();
            WireHudButtonsOnce();
            RewireDiceToggleButton();
            RewireWarbandToggleButton();
            MainMenuUI.EnsureExists();
            StartCoroutine(RelayoutHudNextFrame());

            if (GameLoopManager.Instance != null)
                SetNight(GameLoopManager.Instance.currentNight);

            // Dice/codex/warband start closed. Dossier bottom panel stays visible.
            diceModalUserOpen = false;
            warbandModalUserOpen = false;
            CloseFloatingModals();
            ForceNamedTavernDicePanelsInactive();
            HideAllDicePanelsHard();
            if (dossierModal != null) dossierModal.SetActive(true);
        }

        private System.Collections.IEnumerator RelayoutHudNextFrame()
        {
            yield return null;
            Canvas.ForceUpdateCanvases();
            EnsureDiceModalAsPopup();
            EnsureCodexModalLarge();
            EnsureWarbandModal();
            EnsureTopHubLayout();
            ApplyEnlargedMenuFonts();
            RewireDiceToggleButton();
            RewireWarbandToggleButton();
            RewireLegendsToggleButton();
            RewireMarchButton();
            RewireDossierActionButtons();
            if (!diceModalUserOpen)
            {
                ForceNamedTavernDicePanelsInactive();
                HideAllDicePanelsHard();
            }
            yield return null;
            RewireDiceToggleButton();
            RewireWarbandToggleButton();
            RewireLegendsToggleButton();
            RewireMarchButton();
            RewireDossierActionButtons();
            if (!diceModalUserOpen)
            {
                ForceNamedTavernDicePanelsInactive();
                HideAllDicePanelsHard();
            }
        }

        private void LateUpdate()
        {
            // Scene-serialized / other scripts may re-enable Panel_TavernDice — clamp until user opens.
            if (!diceModalUserOpen)
            {
                ForceNamedTavernDicePanelsInactive();
                HideAllDicePanelsHard();
            }
            else if (diceModal != null && !diceModal.activeSelf)
            {
                // Something else deactivated us while "open" — keep flag honest.
                diceModal.SetActive(true);
                var cg = diceModal.GetComponent<CanvasGroup>();
                if (cg != null) { cg.alpha = 1f; cg.interactable = true; cg.blocksRaycasts = true; }
            }
        }

        /// <summary>
        /// Bulletproof hide by exact panel names — does not rely on FindAllDicePanels heuristics.
        /// Only SetActive(true) happens in OpenModal when the player clicks Dice / presses T.
        /// </summary>
        public void ForceNamedTavernDicePanelsInactive()
        {
            if (diceModalUserOpen) return;

            foreach (var t in Resources.FindObjectsOfTypeAll<Transform>())
            {
                if (t == null || !t.gameObject.scene.IsValid()) continue;
                if (IsUnderHudBar(t)) continue;
                if (!IsExactTavernDicePanelName(t.name)) continue;
                if (IsDiceHudButtonOrChild(t.gameObject)) continue;
                ApplyDicePanelInactive(t.gameObject);
            }

            if (diceModal != null && !IsDiceHudButtonOrChild(diceModal))
                ApplyDicePanelInactive(diceModal);
        }

        private static bool IsExactTavernDicePanelName(string name)
        {
            if (string.IsNullOrEmpty(name)) return false;
            return name == "Panel_TavernDice"
                   || name == "TavernDice"
                   || name == "Panel_Dice"
                   || name == "DicePanel"
                   || name == "TavernDicePanel";
        }

        private static void ApplyDicePanelInactive(GameObject panel)
        {
            if (panel == null) return;
            var cg = panel.GetComponent<CanvasGroup>();
            if (cg == null) cg = panel.AddComponent<CanvasGroup>();
            cg.alpha = 0f;
            cg.interactable = false;
            cg.blocksRaycasts = false;
            // Always force inactive (scene Active flag often boots true).
            if (panel.activeSelf)
                panel.SetActive(false);
        }

        private void WireHudButtonsOnce()
        {
            if (hudButtonsWired) return;
            hudButtonsWired = true;

            // March is re-bound in RewireMarchButton (same pattern as Dice — clears Inspector persistent).
            RewireMarchButton();
            // Dice / Warband / Legends are wired in Rewire*ToggleButton (toggle open/close).
            RewireLegendsToggleButton();
            // Dossier actions share TownSplit buttons — replace events so Town + Inspector
            // cannot stack a second Hire/Train listener (double gold / double party add).
            RewireDossierActionButtons();

            if (modalCloseButtons != null)
            {
                foreach (var b in modalCloseButtons)
                    b?.onClick.AddListener(CloseFloatingModals);
            }
        }

        /// <summary>
        /// Single owner for Recruit / Train / Mediate / Intel / Tavern Round.
        /// Replaces ButtonClickedEvent (RemoveAllListeners leaves Inspector persistent).
        /// </summary>
        public void RewireDossierActionButtons()
        {
            AutoWireDossierFromTownUI();

            if (buyRoundButton != null)
            {
                buyRoundButton.onClick = new Button.ButtonClickedEvent();
                buyRoundButton.onClick.AddListener(BuyTavernRound);
            }
            if (tier1Button != null)
            {
                tier1Button.onClick = new Button.ButtonClickedEvent();
                tier1Button.onClick.AddListener(() => UnlockIntel(1));
            }
            if (tier2Button != null)
            {
                tier2Button.onClick = new Button.ButtonClickedEvent();
                tier2Button.onClick.AddListener(() => UnlockIntel(2));
            }
            if (tier3Button != null)
            {
                tier3Button.onClick = new Button.ButtonClickedEvent();
                tier3Button.onClick.AddListener(() => UnlockIntel(3));
            }
            if (trainTankButton != null)
            {
                trainTankButton.onClick = new Button.ButtonClickedEvent();
                trainTankButton.onClick.AddListener(() => Train(CombatRole.FrontlineTank));
            }
            if (trainRangedButton != null)
            {
                trainRangedButton.onClick = new Button.ButtonClickedEvent();
                trainRangedButton.onClick.AddListener(() => Train(CombatRole.BacklineRanged));
            }
            if (mediateButton != null)
            {
                mediateButton.onClick = new Button.ButtonClickedEvent();
                mediateButton.onClick.AddListener(Mediate);
            }
            if (hireButton != null)
            {
                hireButton.onClick = new Button.ButtonClickedEvent();
                hireButton.onClick.AddListener(HireSelected);
            }
        }

        /// <summary>Always (re)bind Dice HUD → ToggleDiceModal so CreateHudButton after wire still works.</summary>
        private void RewireDiceToggleButton()
        {
            EnsureDiceModalAsPopup();
            ResolveDiceHudButton();
            if (tavernDiceButton == null)
            {
                Debug.LogWarning("[DemonKnight] Dice HUD button missing after Ensure/Resolve — cannot wire toggle.");
                return;
            }

            // RemoveAllListeners does NOT clear Inspector persistent calls — replace the event.
            tavernDiceButton.onClick = new Button.ButtonClickedEvent();
            tavernDiceButton.onClick.AddListener(ToggleDiceModal);
            ApplyHudButtonLabel(tavernDiceButton, "Dice");
            if (!diceToggleWireLogged)
            {
                diceToggleWireLogged = true;
                Debug.Log($"[DemonKnight] Dice HUD wired → ToggleDiceModal on '{tavernDiceButton.name}'.");
            }
        }

        /// <summary>Public entry for TownSplit Start — isolate + hide + wire Dice HUD.</summary>
        public void EnsureDiceHudAndHidden()
        {
            diceModalUserOpen = false;
            IsolateTownSplitDiceIntoModal();
            EnsureDiceModalAsPopup();
            RewireDiceToggleButton();
            ForceNamedTavernDicePanelsInactive();
            HideAllDicePanelsHard();
        }

        /// <summary>Find DiceButton / TavernDiceButton / any HUD button labeled Dice.</summary>
        private void ResolveDiceHudButton()
        {
            Transform hud = FindHudBar();
            if (tavernDiceButton == null && hud != null)
            {
                var existing = hud.Find("DiceButton") ?? hud.Find("TavernDiceButton") ?? hud.Find("Btn_Dice");
                if (existing != null) tavernDiceButton = existing.GetComponent<Button>();
            }

            if (tavernDiceButton == null && hud != null)
            {
                foreach (var btn in hud.GetComponentsInChildren<Button>(true))
                {
                    if (btn == null) continue;
                    string n = btn.gameObject.name;
                    var label = btn.GetComponentInChildren<TextMeshProUGUI>();
                    string text = label != null ? label.text : "";
                    if (n.IndexOf("Dice", System.StringComparison.OrdinalIgnoreCase) >= 0
                        || text.IndexOf("Dice", System.StringComparison.OrdinalIgnoreCase) >= 0
                        || text.IndexOf("Tavern", System.StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        // Skip Start/Roll/Cash inside the modal.
                        if (n.IndexOf("Start", System.StringComparison.OrdinalIgnoreCase) >= 0) continue;
                        if (n.IndexOf("Roll", System.StringComparison.OrdinalIgnoreCase) >= 0) continue;
                        if (n.IndexOf("Cash", System.StringComparison.OrdinalIgnoreCase) >= 0) continue;
                        tavernDiceButton = btn;
                        break;
                    }
                }
            }

            if (tavernDiceButton == null && hud != null)
            {
                tavernDiceButton = CreateHudButton(hud, "DiceButton", "Dice",
                    new Color(0.35f, 0.22f, 0.12f));
                Debug.Log("[DemonKnight] Created missing Dice HUD button.");
            }
        }

        public void ToggleDiceModal()
        {
            EnsureDiceModalAsPopup();
            IsolateTownSplitDiceIntoModal();
            if (diceModal == null)
            {
                var panels = FindAllDicePanels();
                if (panels.Count > 0) diceModal = panels[0];
            }
            if (diceModal == null)
            {
                Debug.LogWarning("[DemonKnight] No tavern/dice panel found to toggle. Check Panel_TavernDice / TownSplit dice refs.");
                return;
            }

            bool open = diceModalUserOpen && diceModal.activeInHierarchy;
            Debug.Log($"[DemonKnight] ToggleDiceModal → {(open ? "CLOSE" : "OPEN")} '{diceModal.name}'");
            if (open)
            {
                CloseFloatingModals();
                return;
            }

            OpenModal(diceModal);
        }

        /// <summary>HUD Warband button → toggle Camelot Crowd panel (same pattern as Dice).</summary>
        private void RewireWarbandToggleButton()
        {
            EnsureWarbandModal();
            ResolveWarbandHudButton();
            if (warbandButton == null)
            {
                Debug.LogWarning("[DemonKnight] Warband HUD button missing — cannot wire Camelot Crowd toggle.");
                return;
            }

            warbandButton.onClick = new Button.ButtonClickedEvent();
            warbandButton.onClick.AddListener(ToggleWarbandModal);
            if (!warbandToggleWireLogged)
            {
                warbandToggleWireLogged = true;
                Debug.Log($"[DemonKnight] Warband HUD wired → ToggleWarbandModal on '{warbandButton.name}'.");
            }
        }

        private void ResolveWarbandHudButton()
        {
            Transform hud = FindHudBar();
            if (warbandButton == null && hud != null)
            {
                var existing = hud.Find("WarbandButton") ?? hud.Find("Btn_Warband") ?? hud.Find("CrowdButton");
                if (existing != null) warbandButton = existing.GetComponent<Button>();
            }
            if (warbandButton == null && hud != null)
            {
                warbandButton = CreateHudButton(hud, "WarbandButton", "Warband (0)",
                    new Color(0.22f, 0.28f, 0.22f));
            }
            if (warbandButton != null && warbandCountText == null)
                warbandCountText = warbandButton.GetComponentInChildren<TextMeshProUGUI>();
        }

        public void ToggleWarbandModal()
        {
            EnsureWarbandModal();
            if (warbandModal == null)
            {
                Debug.LogWarning("[DemonKnight] No WarbandPanel / Camelot Crowd panel to toggle.");
                return;
            }

            bool open = warbandModalUserOpen && warbandModal.activeInHierarchy;
            Debug.Log($"[DemonKnight] ToggleWarbandModal → {(open ? "CLOSE" : "OPEN")} '{warbandModal.name}'");
            if (open)
            {
                CloseFloatingModals();
                return;
            }

            OpenModal(warbandModal);
            RefreshWarbandPanel();
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
        /// Dice must be a HUD button → centered popup, never a permanent overlay on pews.
        /// Hides every tavern/dice panel in the loaded scenes (including inactive duplicates).
        /// </summary>
        public void EnsureDiceModalAsPopup()
        {
            IsolateTownSplitDiceIntoModal();

            var panels = FindAllDicePanels();
            if (panels.Count > 0)
            {
                // Prefer the inspector-wired reference if it is one of the panels.
                if (diceModal == null || !panels.Contains(diceModal))
                    diceModal = PreferNamedDicePanel(panels);

                foreach (var panel in panels)
                    StyleDicePanelAsCenteredModal(panel);

                if (!diceModalUserOpen)
                    HideAllDicePanelsHard();
            }
            else
            {
                Debug.LogWarning("[DemonKnight] EnsureDiceModalAsPopup: no dice panels discovered.");
            }

            ResolveDiceHudButton();
        }

        private static GameObject PreferNamedDicePanel(List<GameObject> panels)
        {
            if (panels == null || panels.Count == 0) return null;
            foreach (var p in panels)
            {
                if (p == null) continue;
                string n = p.name;
                if (n == "Panel_TavernDice" || n == "TavernDice" || n == "Panel_Dice" || n == "DicePanel")
                    return p;
            }
            return panels[0];
        }

        private void StyleDicePanelAsCenteredModal(GameObject panel)
        {
            if (panel == null) return;
            // Never restyle / dim the HUD Dice control as if it were the game panel.
            if (IsDiceHudButtonOrChild(panel)) return;

            // Never leave the modal parented under an always-on TownSplit chrome root
            // in a way that keeps sibling dice widgets visible — keep under TownCanvas.
            Transform canvas = FindTownCanvasTransform();
            if (canvas != null && panel.transform.parent != canvas
                && panel.transform.parent != null
                && IsTownSplitChromeRoot(panel.transform.parent))
            {
                panel.transform.SetParent(canvas, false);
            }

            var rt = panel.GetComponent<RectTransform>();
            if (rt != null)
            {
                rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
                rt.pivot = new Vector2(0.5f, 0.5f);
                rt.anchoredPosition = Vector2.zero;
                rt.sizeDelta = new Vector2(560f, 440f);
                rt.SetAsLastSibling();
            }

            var cg = panel.GetComponent<CanvasGroup>();
            if (cg == null) cg = panel.AddComponent<CanvasGroup>();
            if (diceModalUserOpen && diceModal == panel)
            {
                cg.alpha = 1f;
                cg.interactable = true;
                cg.blocksRaycasts = true;
            }
            else
            {
                // Default / closed: never leave the panel Active from scene serialization.
                cg.alpha = 0f;
                cg.interactable = false;
                cg.blocksRaycasts = false;
                if (panel.activeSelf)
                    panel.SetActive(false);
            }

            EnsureModalChromeButton(panel.transform, "DiceCloseX", "X",
                new Vector2(248f, 186f), new Vector2(48f, 48f),
                new Color(0.45f, 0.12f, 0.12f), CloseFloatingModals);
            EnsureModalChromeButton(panel.transform, "DiceMinimize", "—",
                new Vector2(192f, 186f), new Vector2(48f, 48f),
                new Color(0.25f, 0.22f, 0.18f), CloseFloatingModals);

            BumpTmpUnder(panel.transform, 1.35f, 18f, 36f);
        }

        private static bool IsTownSplitChromeRoot(Transform t)
        {
            if (t == null) return false;
            return t.GetComponent<TownSplitUIController>() != null
                   || t.name.IndexOf("TownSplit", System.StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private Transform FindTownCanvasTransform()
        {
            var loop = GameLoopManager.Instance;
            if (loop != null && loop.townCanvasObj != null)
                return loop.townCanvasObj.transform;
            var town = Town;
            if (town != null)
            {
                var canvas = town.GetComponentInParent<Canvas>();
                if (canvas != null) return canvas.transform;
            }
            var found = GameObject.Find("TownCanvas");
            return found != null ? found.transform : null;
        }

        /// <summary>
        /// Downloads-era scenes leave StartDice / DiceResult as always-on TownSplit children.
        /// Scoop them into Panel_TavernDice so Hide/Toggle can own visibility.
        /// </summary>
        public void IsolateTownSplitDiceIntoModal()
        {
            var town = Town ?? FindFirstObjectByType<TownSplitUIController>();
            if (town == null) return;

            var widgets = new List<Transform>(8);
            if (town.diceResultText != null) widgets.Add(town.diceResultText.transform);
            if (town.currentPotText != null) widgets.Add(town.currentPotText.transform);
            if (town.startDiceButton != null) widgets.Add(town.startDiceButton.transform);
            if (town.rollDiceButton != null) widgets.Add(town.rollDiceButton.transform);
            if (town.cashOutButton != null) widgets.Add(town.cashOutButton.transform);
            if (widgets.Count == 0) return;

            // Already under a dedicated dice panel?
            Transform sharedParent = widgets[0].parent;
            bool allSameParent = true;
            for (int i = 1; i < widgets.Count; i++)
            {
                if (widgets[i].parent != sharedParent) { allSameParent = false; break; }
            }
            if (allSameParent && sharedParent != null && IsDicePanelName(sharedParent.name))
            {
                diceModal = sharedParent.gameObject;
                // Prior bug: early-return left scene Active=true Panel_TavernDice visible.
                if (!diceModalUserOpen)
                    ApplyDicePanelInactive(diceModal);
                InvalidateDicePanelCache();
                return;
            }

            Transform canvas = FindTownCanvasTransform()
                               ?? (town.transform.parent != null ? town.transform.parent : town.transform);

            GameObject panel = null;
            if (diceModal != null && IsDicePanelName(diceModal.name))
                panel = diceModal;
            if (panel == null)
            {
                var existing = canvas.Find("Panel_TavernDice");
                if (existing != null) panel = existing.gameObject;
            }
            if (panel == null)
            {
                foreach (var t in Resources.FindObjectsOfTypeAll<Transform>())
                {
                    if (t == null || !t.gameObject.scene.IsValid()) continue;
                    if (IsDicePanelName(t.name)) { panel = t.gameObject; break; }
                }
            }
            if (panel == null)
            {
                panel = new GameObject("Panel_TavernDice", typeof(RectTransform), typeof(Image), typeof(CanvasGroup));
                panel.transform.SetParent(canvas, false);
                panel.GetComponent<Image>().color = new Color(0.16f, 0.10f, 0.08f, 0.97f);
                Debug.Log("[DemonKnight] Created Panel_TavernDice to host TownSplit dice widgets.");
            }

            diceModal = panel;
            foreach (var w in widgets)
            {
                if (w == null) continue;
                if (w.parent == panel.transform) continue;
                // Do not move HUD Dice toggle into the modal.
                if (w.parent != null && w.parent.name == "HUD_Bar") continue;
                w.SetParent(panel.transform, true);
            }

            if (!diceModalUserOpen)
                panel.SetActive(false);

            InvalidateDicePanelCache();
        }

        private static bool IsDicePanelName(string name)
        {
            if (string.IsNullOrEmpty(name)) return false;
            // Never treat the HUD toggle name as a panel (Dice / DiceButton / TavernDiceButton).
            if (name.Equals("Dice", System.StringComparison.OrdinalIgnoreCase)) return false;
            if (name.IndexOf("DiceButton", System.StringComparison.OrdinalIgnoreCase) >= 0) return false;

            if (name == "Panel_TavernDice" || name == "TavernDice" || name == "Panel_Dice"
                || name == "DicePanel" || name == "TavernDicePanel")
                return true;
            string lower = name.ToLowerInvariant();
            return (lower.Contains("tavern") && lower.Contains("dice"))
                   || lower.StartsWith("panel_dice")
                   || lower.StartsWith("panel_tavern");
        }

        /// <summary>True if this transform lives under the top HUD strip (never a dice modal).</summary>
        private static bool IsUnderHudBar(Transform t)
        {
            while (t != null)
            {
                if (t.name == "HUD_Bar") return true;
                t = t.parent;
            }
            return false;
        }

        private bool IsDiceHudButtonOrChild(GameObject go)
        {
            if (go == null) return false;
            if (IsUnderHudBar(go.transform)) return true;
            if (tavernDiceButton == null) return false;
            if (go == tavernDiceButton.gameObject) return true;
            return go.transform.IsChildOf(tavernDiceButton.transform);
        }

        private List<GameObject> FindAllDicePanels()
        {
            // Rescan at most ~2 Hz from LateUpdate; force-refresh when cache null.
            if (cachedDicePanels != null && Time.unscaledTime < nextDicePanelRescan)
            {
                cachedDicePanels.RemoveAll(p => p == null);
                return cachedDicePanels;
            }

            var list = new List<GameObject>();
            // Includes inactive objects (GameObject.Find does not).
            foreach (var t in Resources.FindObjectsOfTypeAll<Transform>())
            {
                if (t == null || !t.gameObject.scene.IsValid()) continue; // skip prefabs/assets
                // Never the Dice HUD button or anything under HUD_Bar (nested or not).
                if (IsUnderHudBar(t)) continue;
                if (t.GetComponent<Button>() != null
                    && t.name.IndexOf("DiceButton", System.StringComparison.OrdinalIgnoreCase) >= 0)
                    continue;

                bool named = IsDicePanelName(t.name);
                bool hasStart = t.Find("StartDice") != null || t.Find("RollDice") != null;
                bool hasDiceTitle = t.Find("DiceTitle") != null || t.Find("DiceResult") != null;
                if (!named && !hasStart && !hasDiceTitle) continue;

                // Prefer the panel root, not a nested StartDice button host that is the button itself.
                GameObject root = t.gameObject;
                if (t.GetComponent<Button>() != null && t.parent != null && IsDicePanelName(t.parent.name))
                    root = t.parent.gameObject;
                if (IsDiceHudButtonOrChild(root)) continue;

                if (!list.Contains(root)) list.Add(root);
            }

            // TownSplit refs → panel roots (Downloads trees without Panel_TavernDice name).
            CollectDiceRootsFromTownSplit(list);
            list.RemoveAll(IsDiceHudButtonOrChild);
            cachedDicePanels = list;
            nextDicePanelRescan = Time.unscaledTime + 0.5f;
            return list;
        }

        private void InvalidateDicePanelCache()
        {
            cachedDicePanels = null;
            nextDicePanelRescan = 0f;
        }

        private void CollectDiceRootsFromTownSplit(List<GameObject> list)
        {
            var town = Town ?? FindFirstObjectByType<TownSplitUIController>();
            if (town == null) return;

            void AddRootFrom(Component c)
            {
                if (c == null) return;
                var root = FindDicePanelRoot(c.transform);
                if (root != null && !list.Contains(root)) list.Add(root);
            }

            AddRootFrom(town.diceResultText);
            AddRootFrom(town.currentPotText);
            AddRootFrom(town.startDiceButton);
            AddRootFrom(town.rollDiceButton);
            AddRootFrom(town.cashOutButton);
        }

        private static GameObject FindDicePanelRoot(Transform leaf)
        {
            if (leaf == null) return null;
            // HUD Dice toggle is never a panel root.
            if (IsUnderHudBar(leaf)) return null;

            Transform best = null;
            Transform t = leaf;
            while (t != null)
            {
                string n = t.name;
                if (n == "HUD_Bar" || n == "TownCanvas" || n == "ChurchBackground")
                    break;
                if (IsTownSplitChromeRoot(t) || t.GetComponent<Canvas>() != null)
                    break;
                if (n.IndexOf("DiceButton", System.StringComparison.OrdinalIgnoreCase) >= 0)
                    return null;

                if (IsDicePanelName(n))
                    return t.gameObject;

                if (t.Find("StartDice") != null || t.Find("DiceResult") != null || t.Find("RollDice") != null)
                    best = t;
                else if (best == null && t.GetComponent<Image>() != null && t.childCount >= 2)
                    best = t;

                t = t.parent;
            }
            if (best != null && IsUnderHudBar(best)) return null;
            return best != null ? best.gameObject : null;
        }

        private void HideAllDicePanelsHard()
        {
            // Do NOT Isolate here — LateUpdate calls this every frame.
            // Do NOT hide the HUD Dice button — only modal panels / orphan game widgets.
            foreach (var panel in FindAllDicePanels())
            {
                if (panel == null) continue;
                if (IsDiceHudButtonOrChild(panel)) continue;
                if (diceModalUserOpen && panel == diceModal) continue;
                ForceDicePanelHidden(panel);
            }
            if (diceModal != null && !diceModalUserOpen && !IsDiceHudButtonOrChild(diceModal))
                ForceDicePanelHidden(diceModal);

            // Last resort: hide orphan TownSplit dice widgets still outside a modal.
            var town = Town ?? FindFirstObjectByType<TownSplitUIController>();
            if (town != null && !diceModalUserOpen)
            {
                HideIfOrphanDiceWidget(town.diceResultText != null ? town.diceResultText.gameObject : null);
                HideIfOrphanDiceWidget(town.currentPotText != null ? town.currentPotText.gameObject : null);
                HideIfOrphanDiceWidget(town.startDiceButton != null ? town.startDiceButton.gameObject : null);
                HideIfOrphanDiceWidget(town.rollDiceButton != null ? town.rollDiceButton.gameObject : null);
                HideIfOrphanDiceWidget(town.cashOutButton != null ? town.cashOutButton.gameObject : null);
            }

            // Always re-assert HUD Dice visibility after any hide pass.
            EnsureDiceHudButtonVisible();
        }

        private void HideIfOrphanDiceWidget(GameObject go)
        {
            if (go == null) return;
            if (IsDiceHudButtonOrChild(go)) return;
            if (diceModal != null && go.transform.IsChildOf(diceModal.transform)) return;
            if (IsUnderHudBar(go.transform)) return;
            if (go.activeSelf) go.SetActive(false);
        }

        private void ForceDicePanelHidden(GameObject panel)
        {
            if (panel == null) return;
            if (IsDiceHudButtonOrChild(panel)) return; // NEVER hide the HUD Dice control
            ApplyDicePanelInactive(panel);
        }

        /// <summary>HUD Dice toggle must stay active and labeled — hide only the game panel.</summary>
        public void EnsureDiceHudButtonVisible()
        {
            ResolveDiceHudButton();
            if (tavernDiceButton == null) return;

            // Keep under HUD_Bar if something reparented it into the modal.
            // worldPositionStays=true so editor placement is not zeroed by SetParent.
            Transform hud = FindHudBar();
            if (hud != null && tavernDiceButton.transform.parent != hud)
            {
                if (diceModal != null && tavernDiceButton.transform.IsChildOf(diceModal.transform))
                    tavernDiceButton.transform.SetParent(hud, true);
                else if (!IsUnderHudBar(tavernDiceButton.transform))
                    tavernDiceButton.transform.SetParent(hud, true);
            }

            if (!tavernDiceButton.gameObject.activeSelf)
                tavernDiceButton.gameObject.SetActive(true);

            // Restore any CanvasGroup that a mistaken hide pass dimmed.
            var cg = tavernDiceButton.GetComponent<CanvasGroup>();
            if (cg != null)
            {
                cg.alpha = 1f;
                cg.interactable = true;
                cg.blocksRaycasts = true;
            }

            foreach (Transform child in tavernDiceButton.transform)
            {
                if (child != null && !child.gameObject.activeSelf)
                    child.gameObject.SetActive(true);
            }

            ApplyHudButtonLabel(tavernDiceButton, "Dice");
            tavernDiceButton.interactable = true;
        }

        /// <summary>Large centered Codex so all 10 fusion rows + Fuse buttons are readable.</summary>
        public void EnsureCodexModalLarge()
        {
            if (codexModal == null)
            {
                var go = GameObject.Find("CodexPanel");
                if (go == null)
                {
                    foreach (var t in Resources.FindObjectsOfTypeAll<Transform>())
                    {
                        if (t != null && t.gameObject.scene.IsValid() && t.name == "CodexPanel")
                        {
                            go = t.gameObject;
                            break;
                        }
                    }
                }
                if (go != null) codexModal = go;
            }

            var codexUi = FindFirstObjectByType<LegendCodexUI>();
            if (codexModal == null && codexUi != null) codexModal = codexUi.codexPanel;
            if (codexUi != null && codexUi.codexPanel == null && codexModal != null)
                codexUi.codexPanel = codexModal;

            if (codexModal == null) return;

            var rt = codexModal.GetComponent<RectTransform>();
            if (rt != null)
            {
                // Near full-screen centered modal (~92% of parent canvas).
                rt.anchorMin = new Vector2(0.04f, 0.06f);
                rt.anchorMax = new Vector2(0.96f, 0.94f);
                rt.offsetMin = Vector2.zero;
                rt.offsetMax = Vector2.zero;
                rt.pivot = new Vector2(0.5f, 0.5f);
                rt.anchoredPosition = Vector2.zero;
                rt.SetAsLastSibling();
            }

            var title = codexModal.transform.Find("CodexTitle");
            if (title != null)
            {
                var titleRt = title as RectTransform;
                if (titleRt != null)
                {
                    titleRt.anchorMin = new Vector2(0.05f, 1f);
                    titleRt.anchorMax = new Vector2(0.95f, 1f);
                    titleRt.pivot = new Vector2(0.5f, 1f);
                    titleRt.anchoredPosition = new Vector2(0f, -18f);
                    titleRt.sizeDelta = new Vector2(0f, 48f);
                }
                var titleTmp = title.GetComponent<TextMeshProUGUI>();
                if (titleTmp != null)
                {
                    titleTmp.fontSize = 36f;
                    titleTmp.alignment = TextAlignmentOptions.Center;
                    titleTmp.textWrappingMode = TextWrappingModes.NoWrap;
                }
            }

            EnsureCodexScrollViewport(codexModal.transform);

            Transform recipeList = FindCodexRecipeList(codexModal.transform);
            if (recipeList != null)
                ApplyTwoColumnCodexLayout(recipeList);

            EnsureModalChromeButton(codexModal.transform, "CodexCloseX", "X",
                new Vector2(0f, 0f), new Vector2(48f, 48f),
                new Color(0.45f, 0.12f, 0.12f), CloseFloatingModals);
            // Pin X to top-right of the stretched modal.
            var closeTf = codexModal.transform.Find("CodexCloseX") as RectTransform;
            if (closeTf != null)
            {
                closeTf.anchorMin = closeTf.anchorMax = new Vector2(1f, 1f);
                closeTf.pivot = new Vector2(1f, 1f);
                closeTf.anchoredPosition = new Vector2(-12f, -12f);
                closeTf.sizeDelta = new Vector2(48f, 48f);
            }

            if (!codexModalUserOpen) codexModal.SetActive(false);
        }

        private static Transform FindCodexRecipeList(Transform codexRoot)
        {
            if (codexRoot == null) return null;
            var direct = codexRoot.Find("RecipeList");
            if (direct != null) return direct;
            var scroll = codexRoot.Find("CodexScroll");
            if (scroll != null)
            {
                var nested = scroll.Find("RecipeList");
                if (nested != null) return nested;
            }
            return null;
        }

        /// <summary>Guarantee a ScrollRect viewport so all Legends recipes are reachable.</summary>
        public static void EnsureCodexScrollViewport(Transform codexRoot)
        {
            if (codexRoot == null) return;
            Transform recipeList = FindCodexRecipeList(codexRoot);
            if (recipeList == null) return;

            Transform scrollHost = codexRoot.Find("CodexScroll");
            if (scrollHost == null)
            {
                // Wrap existing RecipeList in a scroll host.
                var hostGo = new GameObject("CodexScroll", typeof(RectTransform), typeof(Image), typeof(Mask), typeof(ScrollRect));
                hostGo.transform.SetParent(codexRoot, false);
                scrollHost = hostGo.transform;
                var hostRt = hostGo.GetComponent<RectTransform>();
                hostRt.anchorMin = new Vector2(0.04f, 0.06f);
                hostRt.anchorMax = new Vector2(0.96f, 0.88f);
                hostRt.offsetMin = Vector2.zero;
                hostRt.offsetMax = Vector2.zero;
                hostGo.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.12f);
                hostGo.GetComponent<Mask>().showMaskGraphic = false;
            }

            // RecipeList may still be a sibling of CodexScroll — reparent into the viewport.
            if (recipeList.parent != scrollHost)
                recipeList.SetParent(scrollHost, false);

            var scrollRt = scrollHost as RectTransform;
            if (scrollRt != null)
            {
                scrollRt.anchorMin = new Vector2(0.04f, 0.06f);
                scrollRt.anchorMax = new Vector2(0.96f, 0.88f);
                scrollRt.offsetMin = Vector2.zero;
                scrollRt.offsetMax = Vector2.zero;
            }

            if (scrollHost.GetComponent<Image>() == null)
                scrollHost.gameObject.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0.12f);
            if (scrollHost.GetComponent<Mask>() == null)
            {
                var mask = scrollHost.gameObject.AddComponent<Mask>();
                mask.showMaskGraphic = false;
            }

            var scroll = scrollHost.GetComponent<ScrollRect>();
            if (scroll == null) scroll = scrollHost.gameObject.AddComponent<ScrollRect>();
            var contentRt = recipeList as RectTransform;
            if (contentRt != null)
            {
                contentRt.anchorMin = new Vector2(0f, 1f);
                contentRt.anchorMax = new Vector2(1f, 1f);
                contentRt.pivot = new Vector2(0.5f, 1f);
                contentRt.anchoredPosition = Vector2.zero;
                contentRt.sizeDelta = new Vector2(0f, 0f);
            }
            scroll.content = contentRt;
            scroll.viewport = scrollHost as RectTransform;
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 40f;
        }

        /// <summary>Legends (Codex) recipes in two columns, not one long stack.</summary>
        public static void ApplyTwoColumnCodexLayout(Transform recipeList)
        {
            if (recipeList == null) return;

            // Unity allows only one LayoutGroup per GO. Destroy() is deferred to
            // end-of-frame, so AddComponent<GridLayoutGroup> NREs / throws if a
            // VerticalLayoutGroup is still present — use DestroyImmediate.
            RemoveConflictingLayoutGroups(recipeList.gameObject);

            var grid = recipeList.GetComponent<GridLayoutGroup>();
            if (grid == null) grid = recipeList.gameObject.AddComponent<GridLayoutGroup>();
            grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            grid.constraintCount = 2;
            grid.cellSize = new Vector2(440f, 120f);
            grid.spacing = new Vector2(16f, 16f);
            grid.padding = new RectOffset(10, 10, 10, 10);
            grid.childAlignment = TextAnchor.UpperCenter;
            grid.startCorner = GridLayoutGroup.Corner.UpperLeft;
            grid.startAxis = GridLayoutGroup.Axis.Horizontal;
            grid.enabled = true;

            var fitter = recipeList.GetComponent<ContentSizeFitter>();
            if (fitter == null) fitter = recipeList.gameObject.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            fitter.enabled = true;
        }

        /// <summary>Strip other LayoutGroups so GridLayoutGroup can be added same frame.</summary>
        private static void RemoveConflictingLayoutGroups(GameObject go)
        {
            if (go == null) return;

            // Keep GridLayoutGroup; remove Vertical / Horizontal / other LayoutGroups.
            var layoutGroups = go.GetComponents<LayoutGroup>();
            for (int i = 0; i < layoutGroups.Length; i++)
            {
                var lg = layoutGroups[i];
                if (lg == null || lg is GridLayoutGroup) continue;
                Object.DestroyImmediate(lg);
            }
        }

        /// <summary>
        /// Wire HUD controls for Play. By default preserves editor-authored RectTransforms
        /// (Andrew can move HUD icons in the Inspector and Play keeps them).
        /// Set <see cref="forceEqualHudSlots"/> to restore the legacy equal-slot strip.
        /// </summary>
        public void EnsureTopHubLayout()
        {
            Transform hud = FindHudBar();
            if (hud == null) return;

            // Dice PANEL may be hidden — HUD DiceButton must always exist and stay visible.
            EnsureDiceModalAsPopup();
            EnsureWarbandModal();
            ResolveDiceHudButton();
            if (tavernDiceButton == null)
            {
                tavernDiceButton = CreateHudButton(hud, "DiceButton", "Dice",
                    new Color(0.35f, 0.22f, 0.12f));
            }
            EnsureDiceHudButtonVisible();
            RewireDiceToggleButton();
            RewireWarbandToggleButton();
            RewireLegendsToggleButton();
            RewireMarchButton();
            EnsureMenuHudButton(hud);

            // Kill auto layout groups so they cannot fight authored positions.
            var oldLayout = hud.GetComponent<HorizontalLayoutGroup>();
            if (oldLayout != null) Object.Destroy(oldLayout);
            var fitter = hud.GetComponent<ContentSizeFitter>();
            if (fitter != null) Object.Destroy(fitter);

            if (nightText == null)
            {
                var existing = hud.Find("NightText");
                if (existing != null) nightText = existing.GetComponent<TextMeshProUGUI>();
            }
            if (nightText == null && forceEqualHudSlots)
                nightText = CreateHudLabel(hud, "NightText", "Night 1", Vector2.zero, new Vector2(110f, 40f), 24);

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

            Button codex = ResolveCodexButton(hud);
            if (codexButton == null) codexButton = codex;

            ResolveDiceHudButton();
            if (tavernDiceButton == null)
            {
                tavernDiceButton = CreateHudButton(hud, "DiceButton", "Dice",
                    new Color(0.35f, 0.22f, 0.12f));
            }
            EnsureDiceHudButtonVisible();

            ResolveWarbandInfo(hud);
            RewireMarchButton();

            var party = hud.Find("PartyText");
            if (party != null && warbandButton != null && party != warbandButton.transform)
                party.gameObject.SetActive(false);

            EnsureMenuHudButton(hud);
            PlaceMenuButtonFarLeft(hud);

            // Labels only — do not move other authored RectTransforms unless legacy equal-slot mode is on.
            ApplyHudButtonLabel(tavernDiceButton, "Dice");
            ApplyHudButtonLabel(codexButton, "Legends");
            ApplyHudButtonLabel(goToWarButton, "MARCH TO WAR");
            ApplyHudButtonLabel(menuButton, "☰ Esc");

            if (!forceEqualHudSlots)
            {
                Debug.Log("[DemonKnight] HUD layout preserved (editor RectTransforms). Menu→far left. Codex=Legends.");
                return;
            }

            ApplyLegacyEqualHudSlots(hud, codex);
        }

        /// <summary>Legacy: stretch bar + equal-width Night|Timer|Gold|Dice|Legends|Warband|March|Esc.</summary>
        private void ApplyLegacyEqualHudSlots(Transform hud, Button codex)
        {
            StretchHudBar(hud as RectTransform);

            RectTransform warbandRt = ResolveWarbandInfo(hud);
            Button march = goToWarButton;

            var slots = new List<RectTransform>(9);
            // Menu first = far left in legacy equal-slot strip.
            if (menuButton != null) slots.Add(menuButton.transform as RectTransform);
            if (nightText != null) slots.Add(nightText.rectTransform);
            if (timerText != null) slots.Add(timerText.rectTransform);
            if (goldText != null) slots.Add(goldText.rectTransform);
            if (tavernDiceButton != null) slots.Add(tavernDiceButton.transform as RectTransform);
            if (codex != null) slots.Add(codex.transform as RectTransform);
            if (warbandRt != null) slots.Add(warbandRt);
            if (march != null) slots.Add(march.transform as RectTransform);

            Canvas.ForceUpdateCanvases();
            var hudRt = hud as RectTransform;
            float barW = hudRt != null ? hudRt.rect.width : 0f;
            if (barW < 32f)
            {
                var canvasRt = hud.root as RectTransform;
                if (canvasRt == null && hud.parent != null) canvasRt = hud.parent as RectTransform;
                barW = canvasRt != null ? Mathf.Max(32f, canvasRt.rect.width - 24f) : Screen.width - 24f;
            }

            const float pad = 12f;
            const float gap = 10f;
            const float height = 56f;
            int n = slots.Count;
            if (n == 0) return;

            float usable = Mathf.Max(100f, barW - pad * 2f);
            float slotW = (usable - gap * (n - 1)) / n;
            float startX = -barW * 0.5f + pad + slotW * 0.5f;

            for (int i = 0; i < n; i++)
            {
                var rt = slots[i];
                if (rt == null) continue;
                rt.SetSiblingIndex(i);
                var le = rt.GetComponent<LayoutElement>();
                if (le != null) le.ignoreLayout = true;
                float x = startX + i * (slotW + gap);
                SetHudRect(rt, new Vector2(x, 0f), new Vector2(slotW, height));

                var tmp = rt.GetComponent<TextMeshProUGUI>()
                          ?? rt.GetComponentInChildren<TextMeshProUGUI>();
                if (tmp != null)
                {
                    tmp.alignment = TextAlignmentOptions.Center;
                    tmp.enableAutoSizing = true;
                    tmp.fontSizeMin = 16f;
                    tmp.fontSizeMax = 26f;
                    tmp.textWrappingMode = TextWrappingModes.NoWrap;
                    tmp.overflowMode = TextOverflowModes.Ellipsis;
                }
            }

            for (int i = 0; i < hud.childCount; i++)
            {
                var child = hud.GetChild(i) as RectTransform;
                if (child == null || slots.Contains(child)) continue;
                if (child.name == "DiceButton" || child.name == "TavernDiceButton") continue;
                if (child.name == "PartyText") child.gameObject.SetActive(false);
            }

            EnsureDiceHudButtonVisible();
            ApplyHudButtonLabel(tavernDiceButton, "Dice");
            ApplyHudButtonLabel(codexButton, "Legends");
        }

        private static void StretchHudBar(RectTransform hudRt)
        {
            if (hudRt == null) return;
            hudRt.anchorMin = new Vector2(0f, 1f);
            hudRt.anchorMax = new Vector2(1f, 1f);
            hudRt.pivot = new Vector2(0.5f, 1f);
            hudRt.anchoredPosition = Vector2.zero;
            hudRt.sizeDelta = new Vector2(0f, 84f);
            hudRt.offsetMin = new Vector2(8f, -90f);
            hudRt.offsetMax = new Vector2(-8f, -6f);
            hudRt.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, 84f);
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
            if (codexButton == null)
            {
                var codexUi = FindFirstObjectByType<LegendCodexUI>();
                if (codexUi != null && codexUi.toggleCodexButton != null)
                    codexButton = codexUi.toggleCodexButton;
            }
            if (codexButton == null && hud != null)
            {
                Transform t = hud.Find("CodexToggle") ?? hud.Find("CodexButton") ?? hud.Find("LegendsButton");
                if (t != null) codexButton = t.GetComponent<Button>();
            }
            // Visible label is Legends (object name may stay CodexToggle).
            ApplyHudButtonLabel(codexButton, "Legends");
            return codexButton;
        }

        private static void ApplyHudButtonLabel(Button button, string label)
        {
            if (button == null || string.IsNullOrEmpty(label)) return;
            var tmp = button.GetComponentInChildren<TextMeshProUGUI>();
            if (tmp != null && tmp.text != label) tmp.text = label;
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

        private Button CreateHudButton(Transform hud, string name, string label, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(hud, false);
            var img = go.GetComponent<Image>();
            img.color = color;
            var btn = go.GetComponent<Button>();
            btn.targetGraphic = img;

            var textGo = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
            textGo.transform.SetParent(go.transform, false);
            var trt = textGo.GetComponent<RectTransform>();
            trt.anchorMin = Vector2.zero;
            trt.anchorMax = Vector2.one;
            trt.offsetMin = Vector2.zero;
            trt.offsetMax = Vector2.zero;
            var tmp = textGo.GetComponent<TextMeshProUGUI>();
            tmp.text = label;
            tmp.fontSize = 22f;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.color = Color.white;
            if (timerText != null && timerText.font != null) tmp.font = timerText.font;
            else if (goldText != null && goldText.font != null) tmp.font = goldText.font;
            return btn;
        }

        /// <summary>Resolve + hard-rewire March so Inspector persistent calls cannot block StartBattlePhase.</summary>
        public void RewireMarchButton()
        {
            Transform hud = FindHudBar();
            if (goToWarButton == null && hud != null)
            {
                var m = hud.Find("GoToWarButton") ?? hud.Find("MarchButton") ?? hud.Find("Btn_March");
                if (m != null) goToWarButton = m.GetComponent<Button>();
            }
            var town = Town;
            if (goToWarButton == null && town != null && town.goToWarButton != null)
                goToWarButton = town.goToWarButton;
            if (goToWarButton == null && hud != null)
            {
                goToWarButton = CreateHudButton(hud, "GoToWarButton", "MARCH TO WAR",
                    new Color(0.45f, 0.18f, 0.16f));
            }
            if (goToWarButton == null)
            {
                Debug.LogWarning("[DemonKnight] March HUD button missing — cannot wire.");
                return;
            }

            goToWarButton.onClick = new Button.ButtonClickedEvent();
            goToWarButton.onClick.AddListener(() =>
            {
                Debug.LogError("[DemonKnight] MARCH button clicked.");
                var loop = DemonKnightCompatApi.ResolveLoop() ?? FindFirstObjectByType<GameLoopManager>();
                if (loop == null)
                {
                    Debug.LogError("[DemonKnight] MARCH failed — no GameLoopManager.");
                    return;
                }
                loop.StartBattlePhase();
            });
            ApplyHudButtonLabel(goToWarButton, "MARCH TO WAR");
            goToWarButton.gameObject.SetActive(true);
            goToWarButton.interactable = true;
        }

        /// <summary>Brief dossier/HUD feedback when March is blocked (empty party).</summary>
        public void FlashMarchBlocked(string reason)
        {
            if (dossierDialogue != null)
                dossierDialogue.text = $"<color=#FF6666>{reason}</color>";
            Debug.LogWarning($"[DemonKnight] {reason}");
        }

        /// <summary>HUD Esc / menu icon — toggles Main Menu (same as keyboard Esc).</summary>
        private void EnsureMenuHudButton(Transform hud)
        {
            if (hud == null) return;
            if (menuButton == null)
            {
                var existing = hud.Find("MenuButton") ?? hud.Find("EscButton") ?? hud.Find("Btn_Menu");
                if (existing != null) menuButton = existing.GetComponent<Button>();
            }
            if (menuButton == null)
            {
                menuButton = CreateHudButton(hud, "MenuButton", "☰ Esc",
                    new Color(0.18f, 0.22f, 0.28f));
            }
            else
            {
                ApplyHudButtonLabel(menuButton, "☰ Esc");
            }

            menuButton.onClick = new Button.ButtonClickedEvent();
            menuButton.onClick.AddListener(ToggleMainMenuFromHud);
            menuButton.gameObject.SetActive(true);
            menuButton.interactable = true;
        }

        /// <summary>
        /// Far-left of HUD_Bar only — does not reposition Dice/Legends/Warband/March.
        /// </summary>
        private void PlaceMenuButtonFarLeft(Transform hud)
        {
            if (menuButton == null || hud == null) return;
            if (menuButton.transform.parent != hud)
                menuButton.transform.SetParent(hud, true);
            menuButton.transform.SetAsFirstSibling();

            // Legacy equal-slot path places Menu via ApplyLegacyEqualHudSlots.
            if (forceEqualHudSlots) return;

            var rt = menuButton.transform as RectTransform;
            if (rt == null) return;
            rt.anchorMin = rt.anchorMax = new Vector2(0f, 0.5f);
            rt.pivot = new Vector2(0f, 0.5f);
            rt.anchoredPosition = new Vector2(10f, 0f);
            if (rt.sizeDelta.x < 90f || rt.sizeDelta.y < 40f)
                rt.sizeDelta = new Vector2(110f, 56f);
        }

        public void ToggleMainMenuFromHud()
        {
            // Prefer closing floating HUD modals first (same Esc priority).
            if (AnyFloatingModalOpen())
            {
                CloseFloatingModals();
                Debug.Log("[DemonKnight] HUD Menu/Esc → closed floating modal.");
                return;
            }

            var menu = MainMenuUI.EnsureExists();
            if (menu == null)
            {
                Debug.LogError("[DemonKnight] Menu HUD clicked but MainMenuUI missing.");
                return;
            }
            menu.ToggleMenu();
            Debug.Log($"[DemonKnight] HUD Menu/Esc button → Main Menu {(menu.IsMenuOpen ? "open" : "closed")}.");
        }

        /// <summary>Legacy alias — toggles (open or close).</summary>
        public void OpenMainMenuFromHud() => ToggleMainMenuFromHud();

        public bool AnyFloatingModalOpen()
        {
            if (diceModalUserOpen) return true;
            if (codexModalUserOpen) return true;
            if (warbandModalUserOpen) return true;
            if (diceModal != null && diceModal.activeSelf) return true;
            if (codexModal != null && codexModal.activeSelf) return true;
            if (warbandModal != null && warbandModal.activeSelf) return true;
            var codexUi = FindFirstObjectByType<LegendCodexUI>();
            if (codexUi != null && codexUi.codexPanel != null && codexUi.codexPanel.activeSelf) return true;
            return false;
        }

        /// <summary>Legends HUD / hotkey C — toggle open/close like Dice & Warband.</summary>
        public void RewireLegendsToggleButton()
        {
            Transform hud = FindHudBar();
            ResolveCodexButton(hud);
            if (codexButton == null) return;
            codexButton.onClick = new Button.ButtonClickedEvent();
            codexButton.onClick.AddListener(ToggleCodexModal);
            ApplyHudButtonLabel(codexButton, "Legends");
            codexButton.gameObject.SetActive(true);
            codexButton.interactable = true;
        }

        public void ToggleCodexModal()
        {
            EnsureCodexModalLarge();
            if (codexModal == null)
            {
                var codexUi = FindFirstObjectByType<LegendCodexUI>();
                if (codexUi != null) codexModal = codexUi.codexPanel;
            }
            bool open = codexModalUserOpen
                        || (codexModal != null && codexModal.activeSelf);
            if (open)
            {
                CloseFloatingModals();
                Debug.Log("[DemonKnight] ToggleCodexModal → CLOSE");
                return;
            }
            OpenModal(codexModal);
            FindFirstObjectByType<LegendCodexUI>()?.RefreshCodexList();
            Debug.Log("[DemonKnight] ToggleCodexModal → OPEN");
        }

        private void EnsureModalChromeButton(
            Transform modal,
            string name,
            string label,
            Vector2 pos,
            Vector2 size,
            Color color,
            UnityEngine.Events.UnityAction onClick)
        {
            if (modal == null) return;
            Transform existing = modal.Find(name);
            Button btn;
            if (existing != null)
            {
                btn = existing.GetComponent<Button>();
                if (btn == null) btn = existing.gameObject.AddComponent<Button>();
            }
            else
            {
                var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
                go.transform.SetParent(modal, false);
                var rt = go.GetComponent<RectTransform>();
                rt.anchoredPosition = pos;
                rt.sizeDelta = size;
                go.GetComponent<Image>().color = color;
                btn = go.GetComponent<Button>();

                var textGo = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
                textGo.transform.SetParent(go.transform, false);
                var trt = textGo.GetComponent<RectTransform>();
                trt.anchorMin = Vector2.zero;
                trt.anchorMax = Vector2.one;
                trt.offsetMin = Vector2.zero;
                trt.offsetMax = Vector2.zero;
                var tmp = textGo.GetComponent<TextMeshProUGUI>();
                tmp.text = label;
                tmp.fontSize = 24f;
                tmp.alignment = TextAlignmentOptions.Center;
                tmp.color = Color.white;
                if (timerText != null && timerText.font != null) tmp.font = timerText.font;
            }

            btn.onClick.RemoveListener(onClick);
            btn.onClick.AddListener(onClick);

            // Keep modalCloseButtons array aware when possible.
            if (modalCloseButtons == null || modalCloseButtons.Length == 0)
                modalCloseButtons = new[] { btn };
            else
            {
                bool known = false;
                foreach (var b in modalCloseButtons)
                    if (b == btn) { known = true; break; }
                if (!known)
                {
                    var list = new List<Button>(modalCloseButtons) { btn };
                    modalCloseButtons = list.ToArray();
                }
            }
        }

        public void HandleHotkeys()
        {
            // Esc is owned by MainMenuUI / GameLoop (toggle menu; close modals first).
            // Do NOT CloseAllModals here — that fought the Esc toggle.
            if (Input.GetKeyDown(KeyCode.T)) ToggleDiceModal();
            if (Input.GetKeyDown(KeyCode.C)) ToggleCodexModal();
            if (Input.GetKeyDown(KeyCode.R)) ToggleWarbandModal();
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
                ? $"\"I marched with you and fell. Bury-gold raised my hire by {grudge}g.\""
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
            int mediateCost = GameLoopManager.MediateCostGold;
            if (mediateButton != null)
            {
                mediateButton.interactable = Town != null && Town.playerGold >= mediateCost;
                ApplyHudButtonLabel(mediateButton,
                    GameLoopManager.Instance != null && GameLoopManager.Instance.rivalriesSuspendedThisWave
                        ? "Mediated ✓"
                        : $"Mediate ({mediateCost}g)");
            }
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
            GrudgeSaveManager.Instance?.RememberIntel(selected.unitID, tier);
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

        /// <summary>
        /// 100g — cancel rivalries for this wave so Noble/Outlaw (etc.) can recruit together.
        /// Expires on wave end (per-wave). Does not invent permanent trait rewrites.
        /// </summary>
        public void Mediate()
        {
            if (Town == null || GameLoopManager.Instance == null) return;
            int cost = GameLoopManager.MediateCostGold;
            if (Town.playerGold < cost) return;

            if (GameLoopManager.Instance.rivalriesSuspendedThisWave)
            {
                if (dossierDialogue != null)
                    dossierDialogue.text = "<color=#7CFF9A>Rivalries already mediated for this wave.</color>";
                RefreshDossier();
                return;
            }

            Town.playerGold -= cost;
            GameLoopManager.Instance.rivalriesSuspendedThisWave = true;
            if (dossierDialogue != null)
            {
                dossierDialogue.text =
                    $"<color=#7CFF9A>Mediated ({cost}g).</color> Rivalries canceled for this wave — "
                    + "formerly incompatible souls may recruit together until March ends.";
            }
            Debug.Log($"[DemonKnight] Mediate {cost}g — rivalries suspended this wave.");
            RefreshDossier();
            RefreshHUD();
        }

        /// <summary>One click → one hire / one payment. Idempotent if already in party.</summary>
        public void HireSelected()
        {
            if (hireInProgress) return;
            if (Town == null || GameLoopManager.Instance == null) return;
            if (selected == null)
            {
                // Town may own selection if pew click routed there first.
                selected = Town.PeekSelectedRecruit();
            }
            if (selected == null) return;

            var party = GameLoopManager.Instance.recruitedParty;
            if (party == null)
            {
                party = new List<RecruitInstance>();
                GameLoopManager.Instance.recruitedParty = party;
            }

            // Already hired this run-wave (double listener / double click).
            if (party.Contains(selected)
                || (!string.IsNullOrEmpty(selected.unitID)
                    && party.Exists(m => m != null && m.unitID == selected.unitID)))
            {
                Debug.Log("[DemonKnight] Hire ignored — already in warband.");
                return;
            }

            int grudge = GrudgeSaveManager.Instance != null
                ? GrudgeSaveManager.Instance.GetGrudgeCostPenalty(selected.unitID) : 0;
            int discount = MerlinResurrectionManager.Instance != null
                ? MerlinResurrectionManager.Instance.GetGlobalCostDiscount() : 0;
            int cost = selected.GetFinalHireCost(grudge, discount);
            if (Town.playerGold < cost) return;
            if (!PartySynergyEvaluator.CanJoinParty(selected, party, out string reason))
            {
                if (dossierDialogue != null) dossierDialogue.text = $"<color=red>{reason}</color>";
                return;
            }

            hireInProgress = true;
            try
            {
                if (selected.assignedRole == CombatRole.Unassigned)
                    selected.assignedRole = selected.unitType == UnitType.Peasant
                        ? CombatRole.BacklineRanged : CombatRole.FrontlineTank;

                Town.playerGold -= cost;
                // Recruited members always keep full stats visible (warband + pew return).
                selected.intelTierUnlocked = Mathf.Max(selected.intelTierUnlocked, 3);
                GrudgeSaveManager.Instance?.RememberIntel(selected.unitID, selected.intelTierUnlocked);
                party.Add(selected);
                GameLoopManager.Instance.currentTownPool?.Remove(selected);
                Town.NotifyHiredFromChurch(selected);
                DemonKnightCompatApi.AutosaveWarband(GameLoopManager.Instance, Town.playerGold);
                PopulatePews(GameLoopManager.Instance.currentTownPool);
                CloseAllModals();
                RefreshHUD();
                UpdateWarbandHudLabel();
                if (warbandModal != null && warbandModal.activeSelf)
                    RefreshWarbandPanel();
                Debug.Log($"[DemonKnight] Hired {selected.characterName} for {cost}g (party={party.Count}).");
            }
            finally
            {
                hireInProgress = false;
            }
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

        /// <summary>Ensure Camelot Crowd (Warband) modal exists, scrolls, and starts closed.</summary>
        public void EnsureWarbandModal()
        {
            if (warbandModal == null)
            {
                var found = GameObject.Find("WarbandPanel")
                            ?? GameObject.Find("Panel_Warband")
                            ?? GameObject.Find("Panel_CamelotCrowd");
                if (found == null)
                {
                    foreach (var t in Resources.FindObjectsOfTypeAll<Transform>())
                    {
                        if (t == null || !t.gameObject.scene.IsValid()) continue;
                        if (t.name == "WarbandPanel" || t.name == "Panel_Warband"
                            || t.name == "Panel_CamelotCrowd" || t.name == "CamelotCrowd")
                        {
                            found = t.gameObject;
                            break;
                        }
                    }
                }
                if (found != null) warbandModal = found;
            }

            if (warbandModal == null)
            {
                Transform parent = FindTownCanvasTransform() ?? transform;
                warbandModal = new GameObject("WarbandPanel", typeof(RectTransform), typeof(Image));
                warbandModal.transform.SetParent(parent, false);
                warbandModal.GetComponent<Image>().color = new Color(0.07f, 0.09f, 0.08f, 0.98f);
                Debug.Log("[DemonKnight] Created WarbandPanel (Camelot Crowd) at runtime.");
            }

            var rt = warbandModal.GetComponent<RectTransform>();
            if (rt != null)
            {
                rt.anchorMin = new Vector2(0.08f, 0.08f);
                rt.anchorMax = new Vector2(0.92f, 0.92f);
                rt.offsetMin = Vector2.zero;
                rt.offsetMax = Vector2.zero;
                rt.SetAsLastSibling();
            }

            var title = warbandModal.transform.Find("WarbandTitle");
            if (title == null)
            {
                var titleGo = new GameObject("WarbandTitle", typeof(RectTransform), typeof(TextMeshProUGUI));
                titleGo.transform.SetParent(warbandModal.transform, false);
                title = titleGo.transform;
                var trt = title as RectTransform;
                trt.anchorMin = new Vector2(0.05f, 1f);
                trt.anchorMax = new Vector2(0.95f, 1f);
                trt.pivot = new Vector2(0.5f, 1f);
                trt.anchoredPosition = new Vector2(0f, -16f);
                trt.sizeDelta = new Vector2(0f, 44f);
            }
            var titleTmp = title.GetComponent<TextMeshProUGUI>();
            if (titleTmp != null)
            {
                titleTmp.text = "CAMELOT CROWD — CURRENT WARBAND";
                titleTmp.fontSize = 28f;
                titleTmp.alignment = TextAlignmentOptions.Center;
                titleTmp.color = new Color(0.92f, 0.88f, 0.78f);
            }

            EnsureWarbandScrollViewport(warbandModal.transform);
            if (warbandListContainer == null)
            {
                var list = warbandModal.transform.Find("WarbandScroll/WarbandList")
                           ?? warbandModal.transform.Find("WarbandList");
                if (list != null) warbandListContainer = list;
            }

            EnsureModalChromeButton(warbandModal.transform, "WarbandCloseX", "X",
                new Vector2(0f, 0f), new Vector2(48f, 48f),
                new Color(0.45f, 0.12f, 0.12f), CloseFloatingModals);
            var closeTf = warbandModal.transform.Find("WarbandCloseX") as RectTransform;
            if (closeTf != null)
            {
                closeTf.anchorMin = closeTf.anchorMax = new Vector2(1f, 1f);
                closeTf.pivot = new Vector2(1f, 1f);
                closeTf.anchoredPosition = new Vector2(-12f, -12f);
                closeTf.sizeDelta = new Vector2(48f, 48f);
            }

            if (!warbandModalUserOpen) warbandModal.SetActive(false);
        }

        private static void EnsureWarbandScrollViewport(Transform warbandRoot)
        {
            if (warbandRoot == null) return;
            Transform list = warbandRoot.Find("WarbandList");
            Transform scrollHost = warbandRoot.Find("WarbandScroll");
            if (list == null && scrollHost != null)
                list = scrollHost.Find("WarbandList");
            if (list == null)
            {
                var listGo = new GameObject("WarbandList", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
                listGo.transform.SetParent(scrollHost != null ? scrollHost : warbandRoot, false);
                list = listGo.transform;
            }

            // Always (re)apply vertical layout — existing lists often had no VLG → rows stacked at 0,0.
            var vlg = list.GetComponent<VerticalLayoutGroup>();
            if (vlg == null) vlg = list.gameObject.AddComponent<VerticalLayoutGroup>();
            vlg.spacing = 14f;
            vlg.padding = new RectOffset(10, 10, 10, 10);
            vlg.childAlignment = TextAnchor.UpperCenter;
            vlg.childForceExpandHeight = false;
            vlg.childControlHeight = true;
            vlg.childForceExpandWidth = true;
            vlg.childControlWidth = true;
            var fitter = list.GetComponent<ContentSizeFitter>();
            if (fitter == null) fitter = list.gameObject.AddComponent<ContentSizeFitter>();
            fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            if (scrollHost == null)
            {
                var hostGo = new GameObject("WarbandScroll", typeof(RectTransform), typeof(Image), typeof(Mask), typeof(ScrollRect));
                hostGo.transform.SetParent(warbandRoot, false);
                scrollHost = hostGo.transform;
                hostGo.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.15f);
                hostGo.GetComponent<Mask>().showMaskGraphic = false;
                list.SetParent(scrollHost, false);
            }
            else if (list.parent != scrollHost)
            {
                list.SetParent(scrollHost, false);
            }

            var scrollRt = scrollHost as RectTransform;
            if (scrollRt != null)
            {
                scrollRt.anchorMin = new Vector2(0.04f, 0.06f);
                scrollRt.anchorMax = new Vector2(0.96f, 0.86f);
                scrollRt.offsetMin = Vector2.zero;
                scrollRt.offsetMax = Vector2.zero;
            }

            var scroll = scrollHost.GetComponent<ScrollRect>() ?? scrollHost.gameObject.AddComponent<ScrollRect>();
            var contentRt = list as RectTransform;
            if (contentRt != null)
            {
                contentRt.anchorMin = new Vector2(0f, 1f);
                contentRt.anchorMax = new Vector2(1f, 1f);
                contentRt.pivot = new Vector2(0.5f, 1f);
                contentRt.anchoredPosition = Vector2.zero;
                contentRt.sizeDelta = new Vector2(0f, 0f);
            }
            scroll.content = contentRt;
            scroll.viewport = scrollHost as RectTransform;
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 40f;
            Canvas.ForceUpdateCanvases();
            LayoutRebuilder.ForceRebuildLayoutImmediate(contentRt);
        }

        public void RefreshWarbandPanel()
        {
            EnsureWarbandModal();
            if (warbandListContainer == null)
            {
                var list = warbandModal != null
                    ? (warbandModal.transform.Find("WarbandScroll/WarbandList")
                       ?? warbandModal.transform.Find("WarbandList"))
                    : null;
                if (list != null) warbandListContainer = list;
            }
            if (warbandListContainer == null || GameLoopManager.Instance == null) return;

            foreach (Transform c in warbandListContainer) Destroy(c.gameObject);
            EnsureWarbandScrollViewport(warbandModal != null ? warbandModal.transform : warbandListContainer.parent);

            var party = GameLoopManager.Instance.recruitedParty;
            if (party == null || party.Count == 0)
            {
                BuildWarbandFallbackRow(warbandListContainer,
                    "<i>No recruits yet.</i> Hire townsfolk from the pews — they appear here.");
                Canvas.ForceUpdateCanvases();
                LayoutRebuilder.ForceRebuildLayoutImmediate(warbandListContainer as RectTransform);
                return;
            }

            foreach (var m in party)
            {
                if (m == null) continue;
                string body = BuildCrowdMemberSummary(m, party);
                GameObject row;
                if (warbandRowPrefab != null)
                {
                    row = Instantiate(warbandRowPrefab, warbandListContainer);
                    EnsureWarbandRowLayout(row);
                    var label = row.GetComponentInChildren<TextMeshProUGUI>();
                    if (label != null)
                    {
                        label.text = body;
                        label.fontSize = 18f;
                        label.textWrappingMode = TextWrappingModes.Normal;
                    }
                    var buttons = row.GetComponentsInChildren<Button>();
                    if (buttons.Length >= 2)
                    {
                        var captured = m;
                        buttons[0].onClick.RemoveAllListeners();
                        buttons[1].onClick.RemoveAllListeners();
                        buttons[0].onClick.AddListener(() => { captured.TrainRole(CombatRole.FrontlineTank); RefreshWarbandPanel(); });
                        buttons[1].onClick.AddListener(() => { captured.TrainRole(CombatRole.BacklineRanged); RefreshWarbandPanel(); });
                    }
                }
                else
                {
                    row = BuildWarbandFallbackRow(warbandListContainer, body);
                    AddTrainButtonsToRow(row, m);
                }
            }

            Canvas.ForceUpdateCanvases();
            LayoutRebuilder.ForceRebuildLayoutImmediate(warbandListContainer as RectTransform);
        }

        private static void EnsureWarbandRowLayout(GameObject row)
        {
            if (row == null) return;
            var le = row.GetComponent<LayoutElement>();
            if (le == null) le = row.AddComponent<LayoutElement>();
            le.minHeight = 120f;
            le.preferredHeight = 132f;
            le.flexibleHeight = 0f;
            var rt = row.transform as RectTransform;
            if (rt != null)
            {
                rt.anchorMin = new Vector2(0f, 1f);
                rt.anchorMax = new Vector2(1f, 1f);
                rt.pivot = new Vector2(0.5f, 1f);
            }
        }

        private static string BuildCrowdMemberSummary(RecruitInstance m, List<RecruitInstance> party)
        {
            int grudge = GrudgeSaveManager.Instance != null
                ? GrudgeSaveManager.Instance.GetGrudgeCostPenalty(m.unitID) : 0;
            string mood = DescribeMood(m, grudge);
            string friends = DescribeFriends(m, party);
            string rivals = DescribeRivals(m, party);

            return $"<b>{m.characterName}</b>  ({m.unitType})  ·  {m.assignedRole}\n"
                   + $"HP {m.maxHealth}  |  ATK {m.attackDamage}  |  SPD {m.attackSpeed:F1}/s\n"
                   + $"Mood: {mood}  ·  Trait: {m.primaryTrait}\n"
                   + $"<color=#7CFF9A>Friends:</color> {friends}\n"
                   + $"<color=#FF8888>Rivals:</color> {rivals}";
        }

        private static string DescribeMood(RecruitInstance m, int grudge)
        {
            if (grudge > 0) return $"Grudging (+{grudge}g hire)";
            if (m.primaryTrait == TraitTag.Loyalist) return "Eager (Loyalist)";
            if (m.primaryTrait == TraitTag.Coward) return "Fearful (Coward)";
            if (m.primaryTrait == TraitTag.Drunkard) return "Merry (Drunkard)";
            if (m.primaryTrait == TraitTag.Zealot) return "Fervent (Zealot)";
            if (m.bodyPosture == BodyPosture.EagerForward) return "Eager";
            if (m.bodyPosture == BodyPosture.SlouchedExit) return "Uneasy";
            return "Steady";
        }

        private static string DescribeFriends(RecruitInstance m, List<RecruitInstance> party)
        {
            if (m.lovedTrait == TraitTag.None) return "— (no bond preference)";
            var names = new List<string>();
            foreach (var ally in party)
            {
                if (ally == null || ally == m) continue;
                if (ally.primaryTrait == m.lovedTrait)
                    names.Add(ally.characterName);
            }
            if (names.Count == 0) return $"wants {m.lovedTrait} allies (none in crowd)";
            return string.Join(", ", names) + $" (loves {m.lovedTrait})";
        }

        private static string DescribeRivals(RecruitInstance m, List<RecruitInstance> party)
        {
            if (m.hatedTrait == TraitTag.None) return "— (no rivalry)";
            var names = new List<string>();
            foreach (var ally in party)
            {
                if (ally == null || ally == m) continue;
                if (ally.primaryTrait == m.hatedTrait)
                    names.Add(ally.characterName);
            }
            if (names.Count == 0) return $"resents {m.hatedTrait} (none in crowd)";
            return string.Join(", ", names) + $" (hates {m.hatedTrait})";
        }

        private static GameObject BuildWarbandFallbackRow(Transform parent, string text)
        {
            var go = new GameObject("CrowdRow", typeof(RectTransform), typeof(Image), typeof(LayoutElement));
            go.transform.SetParent(parent, false);
            go.GetComponent<Image>().color = new Color(0.12f, 0.14f, 0.13f, 0.95f);
            var le = go.GetComponent<LayoutElement>();
            le.minHeight = 120f;
            le.preferredHeight = 132f;
            le.flexibleHeight = 0f;

            var tGo = new GameObject("Summary", typeof(RectTransform), typeof(TextMeshProUGUI));
            tGo.transform.SetParent(go.transform, false);
            var trt = tGo.GetComponent<RectTransform>();
            trt.anchorMin = Vector2.zero;
            trt.anchorMax = Vector2.one;
            trt.offsetMin = new Vector2(12f, 8f);
            trt.offsetMax = new Vector2(-120f, -8f);
            var tmp = tGo.GetComponent<TextMeshProUGUI>();
            tmp.text = text;
            tmp.fontSize = 17f;
            tmp.color = new Color(0.92f, 0.88f, 0.78f);
            tmp.textWrappingMode = TextWrappingModes.Normal;
            tmp.richText = true;
            return go;
        }

        private void AddTrainButtonsToRow(GameObject row, RecruitInstance member)
        {
            if (row == null || member == null) return;
            void MakeTrain(string name, string label, CombatRole role, float y)
            {
                var btnGo = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
                btnGo.transform.SetParent(row.transform, false);
                var brt = btnGo.GetComponent<RectTransform>();
                brt.anchorMin = brt.anchorMax = new Vector2(1f, 0.5f);
                brt.pivot = new Vector2(1f, 0.5f);
                brt.anchoredPosition = new Vector2(-10f, y);
                brt.sizeDelta = new Vector2(100f, 36f);
                btnGo.GetComponent<Image>().color = new Color(0.25f, 0.32f, 0.40f);
                var labelGo = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
                labelGo.transform.SetParent(btnGo.transform, false);
                var lrt = labelGo.GetComponent<RectTransform>();
                lrt.anchorMin = Vector2.zero;
                lrt.anchorMax = Vector2.one;
                lrt.offsetMin = Vector2.zero;
                lrt.offsetMax = Vector2.zero;
                var ltmp = labelGo.GetComponent<TextMeshProUGUI>();
                ltmp.text = label;
                ltmp.fontSize = 14f;
                ltmp.alignment = TextAlignmentOptions.Center;
                ltmp.color = Color.white;
                var captured = member;
                btnGo.GetComponent<Button>().onClick.AddListener(() =>
                {
                    captured.TrainRole(role);
                    RefreshWarbandPanel();
                });
            }
            MakeTrain("TrainTank", "Tank", CombatRole.FrontlineTank, 22f);
            MakeTrain("TrainRanged", "Ranged", CombatRole.BacklineRanged, -22f);
        }

        public void OpenModal(GameObject modal)
        {
            if (modal == null) return;
            CloseFloatingModals();

            bool isDice = modal == diceModal
                          || modal.name == "Panel_TavernDice"
                          || modal.name == "TavernDice"
                          || modal.name == "Panel_Dice";
            bool isCodex = modal == codexModal || modal.name.Contains("Codex");
            bool isWarband = modal == warbandModal
                             || modal.name == "WarbandPanel"
                             || modal.name == "Panel_Warband"
                             || modal.name == "Panel_CamelotCrowd"
                             || modal.name.Contains("Warband")
                             || modal.name.Contains("CamelotCrowd");
            diceModalUserOpen = isDice;
            codexModalUserOpen = isCodex;
            warbandModalUserOpen = isWarband;

            if (isDice)
            {
                diceModal = modal;
                diceModalUserOpen = true;
                StyleDicePanelAsCenteredModal(modal);
                foreach (var panel in FindAllDicePanels())
                {
                    if (panel != null && panel != modal)
                        ForceDicePanelHidden(panel);
                }
                var cg = modal.GetComponent<CanvasGroup>();
                if (cg == null) cg = modal.AddComponent<CanvasGroup>();
                cg.alpha = 1f;
                cg.interactable = true;
                cg.blocksRaycasts = true;
            }
            if (isCodex) EnsureCodexModalLarge();
            if (isWarband)
            {
                warbandModal = modal;
                warbandModalUserOpen = true;
                EnsureWarbandModal();
            }

            modal.SetActive(true);
            modal.transform.SetAsLastSibling();

            if (isCodex)
                FindFirstObjectByType<LegendCodexUI>()?.RefreshCodexList();
            if (isWarband)
                RefreshWarbandPanel();
        }

        /// <summary>Close floating overlays only — keep the bottom dossier panel visible.</summary>
        public void CloseFloatingModals()
        {
            diceModalUserOpen = false;
            codexModalUserOpen = false;
            warbandModalUserOpen = false;
            ForceNamedTavernDicePanelsInactive();
            HideAllDicePanelsHard();
            if (diceModal != null) diceModal.SetActive(false);
            if (codexModal != null) codexModal.SetActive(false);
            if (warbandModal != null) warbandModal.SetActive(false);

            // Also force-hide LegendCodexUI panel if it diverged from codexModal ref.
            var codexUi = FindFirstObjectByType<LegendCodexUI>();
            if (codexUi != null && codexUi.codexPanel != null)
                codexUi.codexPanel.SetActive(false);
        }

        /// <summary>Raise menu TMP floors (idempotent — never multiplies on re-layout).</summary>
        public void ApplyEnlargedMenuFonts()
        {
            Transform hud = FindHudBar();
            if (hud != null) RaiseTmpFloorUnder(hud, 20f, 26f);

            if (dossierModal != null) RaiseTmpFloorUnder(dossierModal.transform, 20f, 28f);
            else
            {
                RaiseTmpFloor(dossierDialogue, 22f);
                RaiseTmpFloor(dossierStats, 20f);
                RaiseTmpFloor(gossipText, 18f);
            }

            if (warbandModal != null) RaiseTmpFloorUnder(warbandModal.transform, 20f, 28f);
            if (codexModal != null) RaiseTmpFloorUnder(codexModal.transform, 20f, 36f);
            if (diceModal != null) RaiseTmpFloorUnder(diceModal.transform, 20f, 32f);

            foreach (var panel in FindAllDicePanels())
            {
                if (panel != null) RaiseTmpFloorUnder(panel.transform, 20f, 32f);
            }

            var perk = FindFirstObjectByType<PerkDraftManager>();
            if (perk != null && perk.perkDraftPanel != null)
                RaiseTmpFloorUnder(perk.perkDraftPanel.transform, 20f, 34f);

            var merlin = MerlinResurrectionManager.Instance
                         ?? FindFirstObjectByType<MerlinResurrectionManager>();
            if (merlin != null)
            {
                if (merlin.resurrectionCanvas != null)
                    RaiseTmpFloorUnder(merlin.resurrectionCanvas.transform, 22f, 40f);
                if (merlin.legacyBankPanel != null)
                    RaiseTmpFloorUnder(merlin.legacyBankPanel.transform, 20f, 30f);
                RaiseTmpFloor(merlin.loreNarrativeText, 28f);
            }

            RaiseButtonLabelFloor(tier1Button, 20f);
            RaiseButtonLabelFloor(tier2Button, 20f);
            RaiseButtonLabelFloor(tier3Button, 20f);
            RaiseButtonLabelFloor(trainTankButton, 20f);
            RaiseButtonLabelFloor(trainRangedButton, 20f);
            RaiseButtonLabelFloor(mediateButton, 20f);
            RaiseButtonLabelFloor(hireButton, 22f);
            RaiseButtonLabelFloor(buyRoundButton, 20f);
            RaiseButtonLabelFloor(goToWarButton, 22f);
            RaiseButtonLabelFloor(codexButton, 22f);
            RaiseButtonLabelFloor(warbandButton, 22f);
            RaiseButtonLabelFloor(tavernDiceButton, 22f);
        }

        private static void RaiseButtonLabelFloor(Button button, float size)
        {
            if (button == null) return;
            RaiseTmpFloor(button.GetComponentInChildren<TextMeshProUGUI>(), size);
        }

        private static void RaiseTmpFloor(TextMeshProUGUI tmp, float floor)
        {
            if (tmp == null) return;
            if (tmp.enableAutoSizing)
            {
                tmp.fontSizeMin = Mathf.Max(tmp.fontSizeMin, floor * 0.75f);
                tmp.fontSizeMax = Mathf.Max(tmp.fontSizeMax, floor);
            }
            else if (tmp.fontSize < floor)
            {
                tmp.fontSize = floor;
            }
        }

        private static void RaiseTmpFloorUnder(Transform root, float floor, float autoMax)
        {
            if (root == null) return;
            foreach (var tmp in root.GetComponentsInChildren<TextMeshProUGUI>(true))
            {
                if (tmp == null) continue;
                if (tmp.enableAutoSizing)
                {
                    tmp.fontSizeMin = Mathf.Max(tmp.fontSizeMin, floor * 0.75f);
                    tmp.fontSizeMax = Mathf.Max(tmp.fontSizeMax, autoMax);
                }
                else if (tmp.fontSize < floor)
                {
                    tmp.fontSize = floor;
                }
            }
        }

        /// <summary>Used by dice chrome styling — raise floors only (safe to call repeatedly).</summary>
        private static void BumpTmpUnder(Transform root, float unusedScale, float minFloor, float maxCap)
        {
            RaiseTmpFloorUnder(root, minFloor, maxCap);
        }

        public void CloseAllModals() => CloseFloatingModals();

        /// <summary>Boot menu so Downloads can New Game (clears stale autosave / hired party).</summary>
        public static void EnsureMainMenuExists() => MainMenuUI.EnsureExists();

        /// <summary>HUD Warband button label — live recruit count, e.g. "Warband (3)".</summary>
        public void UpdateWarbandHudLabel()
        {
            ResolveWarbandHudButton();
            if (warbandCountText == null && warbandButton != null)
                warbandCountText = warbandButton.GetComponentInChildren<TextMeshProUGUI>();
            int count = GameLoopManager.Instance != null
                ? GameLoopManager.Instance.recruitedParty.Count
                : 0;
            string label = $"Warband ({count})";
            if (warbandCountText != null)
                warbandCountText.text = label;
            else if (warbandButton != null)
            {
                var tmp = warbandButton.GetComponentInChildren<TextMeshProUGUI>();
                if (tmp != null) tmp.text = label;
            }
        }

        public void RefreshHUD()
        {
            var town = Town;
            var loop = GameLoopManager.Instance;
            if (loop != null) SetNight(loop.currentNight);
            if (goldText != null && town != null) goldText.text = $"Royal Gold: {town.playerGold}g";
            UpdateWarbandHudLabel();
            if (timerText != null && loop != null)
            {
                if (SomersetRunes.IsTownTimerDisabled())
                    timerText.text = "Horde: ∞";
                else
                {
                    float t = loop.GetRemainingTime();
                    timerText.text = $"Horde: {Mathf.FloorToInt(t / 60f):00}:{Mathf.FloorToInt(t % 60f):00}";
                }
            }

            if (buyRoundButton != null && town != null)
                buyRoundButton.interactable = town.playerGold >= 50;
        }

        private void Update()
        {
            var loop = GameLoopManager.Instance;
            if (loop == null) return;
            // Keep the Warband (N) label live whenever the party can change.
            if (loop.currentPhase == GamePhase.TownRecruit
                || loop.currentPhase == GamePhase.Rewards
                || loop.currentPhase == GamePhase.Resurrection)
            {
                RefreshHUD();
            }
        }
    }
}
