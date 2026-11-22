using System;
using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;

namespace ArthurianAutobattler
{
    public class MerlinResurrectionManager : MonoBehaviour
    {
        public static MerlinResurrectionManager Instance;

        [Header("Resurrection Cutscene UI")]
        public GameObject resurrectionCanvas;
        public Animator merlinCutsceneAnimator;
        public TextMeshProUGUI loreNarrativeText;
        public GameObject legacyBankPanel;

        [Header("Persistent Bank Levels")]
        public int unspentLegacyPoints = 0;
        public int startingGoldBankLevel = 0;
        public int recruitmentDiscountLevel = 0;
        public int startingSupplyRationsLevel = 0;

        [Header("Uncapped Stackable Runes (Ranks 0–3)")]
        public int gossipRuneRank = 0;
        public int gamblerRuneRank = 0;
        public int bloodOathRuneRank = 0;
        public int peasantsCrownRuneRank = 0;

        [Header("Bank UI Labels")]
        public TextMeshProUGUI pointsAvailableText;
        public TextMeshProUGUI goldBankText;
        public TextMeshProUGUI discountBankText;
        public TextMeshProUGUI rationsBankText;
        public TextMeshProUGUI runeStatusText;

        private Button endRunButton;
        private Button nextNightButton;
        /// <summary>Blocks double AdvanceNight when scene persistent + runtime listeners both fire.</summary>
        private bool advancingNight;
        /// <summary>One wire pass per victory/resurrection show — stops EndBattle double-bind logs.</summary>
        private int victoryShowId;
        private int victoryWiredForShowId = -1;
        private Coroutine victorySequenceRoutine;
        private Coroutine resurrectionSequenceRoutine;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Debug.LogWarning("[DemonKnight] Duplicate MerlinResurrectionManager destroyed (prevents double Next Night).");
                Destroy(gameObject);
                return;
            }
            Instance = this;
            LoadBankData();
            // Runes disabled pending redesign — ranks stay zeroed.
            ZeroRuneRanks();
            if (resurrectionCanvas != null) resurrectionCanvas.SetActive(false);
            HideLegacyBankPanel();
            // Do NOT wire/promote victory UI on Awake — that activated the overlay mid-game
            // and double-fired with Start() (pause/play felt like double-click).
            CullDuplicateEventSystems();
        }

        private void Start()
        {
            HideLegacyBankPanel();
            CullDuplicateEventSystems();
            // Idle bind only (canvas stays hidden) — no PromoteVictoryCanvas.
            WireAbandonScreenButtons(promoteOverlay: false, force: false);
        }

        /// <summary>
        /// Idempotent button bind. <paramref name="promoteOverlay"/> true only when showing
        /// victory/defeat UI. Second call for the same show is a no-op.
        /// </summary>
        void WireAbandonScreenButtons(bool promoteOverlay = true, bool force = false)
        {
            if (!force && promoteOverlay && victoryWiredForShowId == victoryShowId)
                return;

            HideLegacyBankPanel();
            WireNextNightButton(promoteOverlay);
            WireEndRunButton(promoteOverlay);
            if (promoteOverlay)
                victoryWiredForShowId = victoryShowId;
        }

        void BeginVictoryShow()
        {
            victoryShowId++;
            victoryWiredForShowId = -1;
            CullDuplicateEventSystems();
        }

        /// <summary>
        /// Legacy Merlin bank UI is removed — Next Night advances the run instead.
        /// </summary>
        void HideLegacyBankPanel()
        {
            EnsureLegacyBankPanelRef();
            if (legacyBankPanel != null)
                legacyBankPanel.SetActive(false);

            foreach (var t in Resources.FindObjectsOfTypeAll<Transform>())
            {
                if (t == null || !t.gameObject.scene.IsValid()) continue;
                string n = t.gameObject.name ?? "";
                if (n.Equals("LegacyBankPanel", StringComparison.OrdinalIgnoreCase)
                    || n.Equals("BankPanel", StringComparison.OrdinalIgnoreCase))
                    t.gameObject.SetActive(false);
            }
        }

        /// <summary>
        /// Next Night → StartNextTimelineRun (mid-run keep-wave OR defeat wave→1).
        /// Single listener only — clears persistent Inspector wiring.
        /// </summary>
        private void WireNextNightButton(bool promoteOverlay)
        {
            EnsureResurrectionCanvas();
            if (promoteOverlay) PromoteVictoryCanvas();
            CullDuplicateEventSystems();
            // Parent to canvas (not the 900×520 panel) so buttons sit on screen bottom.
            Transform host = GetVictoryButtonHost();
            if (host == null) return;

            nextNightButton = FindPanelButton(host, "NextNight", "Next Night", "NextRun", "Next Timeline", "Next Run");
            if (nextNightButton == null)
                nextNightButton = FindPanelButton(GetAbandonHost(), "NextNight", "Next Night", "NextRun", "Next Timeline", "Next Run");
            if (nextNightButton == null)
                nextNightButton = CreateNextNightButton(host);
            else
                nextNightButton.transform.SetParent(host, false);

            if (nextNightButton == null) return;
            StyleVictoryActionButton(
                nextNightButton,
                "NextNight",
                "Next Night",
                new Vector2(190f, 96f),
                new Color(0.85f, 0.62f, 0.12f, 1f)); // bright amber — screen-bottom
            BindButtonOnce(nextNightButton, StartNextTimelineRun);
            nextNightButton.interactable = true;
            nextNightButton.gameObject.SetActive(true);
            nextNightButton.transform.SetAsLastSibling();
            if (promoteOverlay)
                Debug.LogError("[DemonKnight] Next Night button wired + visible on victory canvas.");
        }

        /// <summary>
        /// Camelot abandon / end-of-run screen: End Run closes the cutscene and opens Main Menu.
        /// </summary>
        private void WireEndRunButton(bool promoteOverlay)
        {
            EnsureResurrectionCanvas();
            if (promoteOverlay) PromoteVictoryCanvas();
            Transform host = GetVictoryButtonHost();
            if (host == null) return;

            endRunButton = FindPanelButton(host, "EndRun", "End Run", "Abandon Camelot", "Return to Menu");
            if (endRunButton == null)
                endRunButton = FindPanelButton(GetAbandonHost(), "EndRun", "End Run", "Abandon Camelot", "Return to Menu");
            if (endRunButton == null)
                endRunButton = CreateEndRunButton(host);
            else
                endRunButton.transform.SetParent(host, false);

            if (endRunButton == null) return;
            StyleVictoryActionButton(
                endRunButton,
                "EndRun",
                "End Run",
                new Vector2(-190f, 96f),
                new Color(0.55f, 0.18f, 0.16f, 1f));
            BindButtonOnce(endRunButton, EndRunToMainMenu);
            endRunButton.interactable = true;
            endRunButton.gameObject.SetActive(true);
            endRunButton.transform.SetAsLastSibling();
        }

        /// <summary>Canvas root — buttons use screen-bottom anchors (panel-relative hid them).</summary>
        Transform GetVictoryButtonHost()
        {
            EnsureResurrectionCanvas();
            return resurrectionCanvas != null ? resurrectionCanvas.transform : null;
        }

        /// <summary>Fresh onClick with exactly one runtime listener (no stacking across Sync/domain reloads).</summary>
        static void BindButtonOnce(Button button, UnityEngine.Events.UnityAction action)
        {
            if (button == null || action == null) return;
            button.onClick = new Button.ButtonClickedEvent();
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(action);
        }

        Transform GetAbandonHost()
        {
            EnsureResurrectionCanvas();
            if (resurrectionCanvas == null) return null;
            var panel = resurrectionCanvas.transform.Find("ResurrectionPanel");
            return panel != null ? panel : resurrectionCanvas.transform;
        }

        /// <summary>Victory overlay must beat town HUD / boss dialog / nested pew canvases.</summary>
        void PromoteVictoryCanvas()
        {
            if (resurrectionCanvas == null) return;
            // Manager must be active for coroutines / button wiring.
            if (!gameObject.activeInHierarchy)
                gameObject.SetActive(true);

            var canvas = resurrectionCanvas.GetComponent<Canvas>();
            if (canvas == null) canvas = resurrectionCanvas.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.overrideSorting = true;
            canvas.sortingOrder = 32000;
            if (resurrectionCanvas.GetComponent<GraphicRaycaster>() == null)
                resurrectionCanvas.AddComponent<GraphicRaycaster>();
            var cg = resurrectionCanvas.GetComponent<CanvasGroup>();
            if (cg == null) cg = resurrectionCanvas.AddComponent<CanvasGroup>();
            cg.alpha = 1f;
            cg.interactable = true;
            cg.blocksRaycasts = true;
            cg.ignoreParentGroups = true;
            resurrectionCanvas.SetActive(true);
            var panel = resurrectionCanvas.transform.Find("ResurrectionPanel");
            if (panel != null)
            {
                panel.gameObject.SetActive(true);
                var prt = panel as RectTransform;
                if (prt != null)
                {
                    // Stretch panel full-screen so lore + chrome aren't clipped off a tiny box.
                    prt.anchorMin = Vector2.zero;
                    prt.anchorMax = Vector2.one;
                    prt.offsetMin = Vector2.zero;
                    prt.offsetMax = Vector2.zero;
                    prt.localScale = Vector3.one;
                }
            }
            ResolveLoreNarrativeText();
        }

        void ResolveLoreNarrativeText()
        {
            if (loreNarrativeText != null) return;
            if (resurrectionCanvas == null) return;
            foreach (var tmp in resurrectionCanvas.GetComponentsInChildren<TextMeshProUGUI>(true))
            {
                if (tmp == null) continue;
                string n = tmp.gameObject.name ?? "";
                if (n.IndexOf("Lore", StringComparison.OrdinalIgnoreCase) >= 0
                    || n.IndexOf("Narrative", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    loreNarrativeText = tmp;
                    return;
                }
            }
        }

        /// <summary>
        /// Exactly one EventSystem. Duplicates (domain reload / runtime create) make every
        /// UI click fire twice — Andrew's pause/play "double-click" feel.
        /// </summary>
        static void CullDuplicateEventSystems()
        {
            var all = FindObjectsByType<EventSystem>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            if (all == null || all.Length == 0)
            {
                var go = new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
                go.name = "EventSystem";
                Debug.LogError("[DemonKnight] Created EventSystem for UI clicks.");
                return;
            }

            // Keep the first enabled (or first overall); destroy the rest.
            EventSystem keep = null;
            for (int i = 0; i < all.Length; i++)
            {
                if (all[i] != null && all[i].isActiveAndEnabled)
                {
                    keep = all[i];
                    break;
                }
            }
            if (keep == null) keep = all[0];

            int culled = 0;
            for (int i = 0; i < all.Length; i++)
            {
                if (all[i] == null || all[i] == keep) continue;
                culled++;
                if (Application.isPlaying) Destroy(all[i].gameObject);
                else DestroyImmediate(all[i].gameObject);
            }
            if (culled > 0)
                Debug.LogWarning($"[DemonKnight] Culled {culled} duplicate EventSystem(s) (stops double-clicks).");
        }

        /// <summary>
        /// Force large, high-contrast action buttons at the bottom of the victory panel
        /// so Downloads scenes / dark purple leftovers never leave Andrew with an empty screen.
        /// </summary>
        static void StyleVictoryActionButton(
            Button button, string name, string label, Vector2 anchored, Color color)
        {
            if (button == null) return;
            button.gameObject.name = name;
            var rt = button.GetComponent<RectTransform>();
            if (rt == null) return;
            // Bottom-center of host — always on-screen regardless of panel size / aspect.
            rt.anchorMin = new Vector2(0.5f, 0f);
            rt.anchorMax = new Vector2(0.5f, 0f);
            rt.pivot = new Vector2(0.5f, 0f);
            rt.anchoredPosition = anchored;
            rt.sizeDelta = new Vector2(360f, 88f);
            rt.localScale = Vector3.one;

            var img = button.GetComponent<Image>();
            if (img == null) img = button.gameObject.AddComponent<Image>();
            img.color = color;
            img.raycastTarget = true;
            button.targetGraphic = img;
            button.transition = Selectable.Transition.ColorTint;
            var colors = button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(1f, 0.95f, 0.8f, 1f);
            colors.pressedColor = new Color(0.85f, 0.75f, 0.55f, 1f);
            colors.selectedColor = Color.white;
            colors.disabledColor = new Color(0.5f, 0.5f, 0.5f, 0.6f);
            button.colors = colors;

            var tmp = button.GetComponentInChildren<TextMeshProUGUI>(true);
            if (tmp == null)
            {
                var labelGo = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
                labelGo.transform.SetParent(button.transform, false);
                var lrt = labelGo.GetComponent<RectTransform>();
                lrt.anchorMin = Vector2.zero;
                lrt.anchorMax = Vector2.one;
                lrt.offsetMin = Vector2.zero;
                lrt.offsetMax = Vector2.zero;
                tmp = labelGo.GetComponent<TextMeshProUGUI>();
            }
            tmp.gameObject.SetActive(true);
            tmp.text = label;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.fontSize = 32f;
            tmp.fontStyle = FontStyles.Bold;
            tmp.color = new Color(0.08f, 0.06f, 0.04f, 1f); // dark ink on amber
            tmp.raycastTarget = false;
            tmp.textWrappingMode = TextWrappingModes.NoWrap;
            if (tmp.fontSize < 28f) tmp.fontSize = 32f;
        }

        void EnsureLegacyBankPanelRef()
        {
            if (legacyBankPanel != null) return;
            var found = GameObject.Find("LegacyBankPanel");
            if (found == null) found = GameObject.Find("BankPanel");
            if (found != null) legacyBankPanel = found;
        }

        void EnsureResurrectionCanvas()
        {
            if (resurrectionCanvas != null)
            {
                // Keep even if inactive — PromoteVictoryCanvas will activate.
                return;
            }
            // GameObject.Find skips inactive — search all transforms for Downloads scenes.
            var all = FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int i = 0; i < all.Length; i++)
            {
                if (all[i] == null) continue;
                string n = all[i].name ?? "";
                if (n.Equals("ResurrectionCanvas", StringComparison.OrdinalIgnoreCase)
                    || n.Equals("MerlinResurrection", StringComparison.OrdinalIgnoreCase))
                {
                    resurrectionCanvas = all[i].gameObject;
                    return;
                }
            }
            BuildRuntimeVictoryCanvas();
        }

        /// <summary>Downloads scenes sometimes lack ResurrectionCanvas — build a minimal victory overlay.</summary>
        void BuildRuntimeVictoryCanvas()
        {
            var canvasGo = new GameObject("ResurrectionCanvas", typeof(RectTransform), typeof(Canvas),
                typeof(CanvasScaler), typeof(GraphicRaycaster));
            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 5000;
            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);

            var panel = new GameObject("ResurrectionPanel", typeof(RectTransform), typeof(Image));
            panel.transform.SetParent(canvasGo.transform, false);
            var prt = panel.GetComponent<RectTransform>();
            prt.anchorMin = Vector2.zero;
            prt.anchorMax = Vector2.one;
            prt.offsetMin = Vector2.zero;
            prt.offsetMax = Vector2.zero;
            panel.GetComponent<Image>().color = new Color(0.04f, 0.05f, 0.08f, 0.92f);
            try
            {
                var img = panel.GetComponent<Image>();
                var rt = panel.GetComponent<RectTransform>();
                // Shared victory / Merlin canvas — prefer Merlin slot, else Victory.
                if (!SceneBackgroundApplier.ApplyToUiHost(SceneBackgroundSlot.MerlinResurrection, img, rt))
                    SceneBackgroundApplier.ApplyToUiHost(SceneBackgroundSlot.VictoryOverlay, img, rt);
            }
            catch { /* ignore */ }

            var loreGo = new GameObject("LoreNarrative", typeof(RectTransform), typeof(TextMeshProUGUI));
            loreGo.transform.SetParent(panel.transform, false);
            var lrt = loreGo.GetComponent<RectTransform>();
            lrt.anchorMin = new Vector2(0.1f, 0.35f);
            lrt.anchorMax = new Vector2(0.9f, 0.85f);
            lrt.offsetMin = Vector2.zero;
            lrt.offsetMax = Vector2.zero;
            loreNarrativeText = loreGo.GetComponent<TextMeshProUGUI>();
            loreNarrativeText.alignment = TextAlignmentOptions.Center;
            loreNarrativeText.fontSize = 36f;
            loreNarrativeText.color = new Color(0.95f, 0.88f, 0.65f);

            var statusGo = new GameObject("RuneStatus", typeof(RectTransform), typeof(TextMeshProUGUI));
            statusGo.transform.SetParent(panel.transform, false);
            var srt = statusGo.GetComponent<RectTransform>();
            srt.anchorMin = new Vector2(0.15f, 0.18f);
            srt.anchorMax = new Vector2(0.85f, 0.32f);
            srt.offsetMin = Vector2.zero;
            srt.offsetMax = Vector2.zero;
            runeStatusText = statusGo.GetComponent<TextMeshProUGUI>();
            runeStatusText.alignment = TextAlignmentOptions.Center;
            runeStatusText.fontSize = 22f;
            runeStatusText.color = new Color(0.8f, 0.85f, 0.9f);

            resurrectionCanvas = canvasGo;
            canvasGo.SetActive(false);
            Debug.LogError("[DemonKnight] Built runtime ResurrectionCanvas for victory / Next Night.");
        }

        /// <summary>
        /// Mid-run wave win: victory message + Next Night (ContinueAfterWaveVictory — keep wave index).
        /// </summary>
        public void TriggerWaveVictoryScreen(int waveCleared, int goldEarned, string flockBreakdown = null)
        {
            if (!gameObject.activeInHierarchy)
                gameObject.SetActive(true);
            BeginVictoryShow();
            EnsureResurrectionCanvas();
            PromoteVictoryCanvas();
            CullDuplicateEventSystems();
            var loopFlag = GameLoopManager.Instance ?? FindFirstObjectByType<GameLoopManager>();
            if (loopFlag != null)
                loopFlag.pendingMidRunWaveVictory = true;
            if (string.IsNullOrEmpty(flockBreakdown))
            {
                var loop = loopFlag ?? GameLoopManager.Instance;
                flockBreakdown = loop != null ? loop.lastFlockUpkeepBreakdown : null;
            }
            string flockLine = string.IsNullOrEmpty(flockBreakdown)
                ? ""
                : $"\n{flockBreakdown}\n";
            string mythic = "";
            var loopMythic = GameLoopManager.Instance;
            if (loopMythic != null && loopMythic.lastMythicVesselAscensionBanner)
            {
                mythic = "\n<color=#FFD700><b>MYTHIC VESSEL ASCENDED</b> — a bloodline awakens (+25% HP/ATK/SPD).</color>\n";
                loopMythic.lastMythicVesselAscensionBanner = false;
            }
            string msg =
                $"<b>VICTORY</b> — Wave {waveCleared} cleared (+{goldEarned}g).{flockLine}{mythic}\n"
                + "The pews hold. Press <b>Next Night</b> to advance, or <b>End Run</b> for the Main Menu.";
            // Buttons once — coroutine must not re-wire (that logged Wire* twice per EndBattle).
            HideLegacyBankPanel();
            PerkDraftManager.SuppressAllPerkUi();
            ResolveLoreNarrativeText();
            if (loreNarrativeText != null)
                loreNarrativeText.text = msg;
            WireAbandonScreenButtons(promoteOverlay: true, force: true);
            // Hard guarantee: Next Night exists, active, on canvas, clickable.
            ForceVictoryNextNightVisible();
            RefreshBankUI();
            if (victorySequenceRoutine != null)
                StopCoroutine(victorySequenceRoutine);
            victorySequenceRoutine = StartCoroutine(PlayWaveVictorySequence(msg));
        }

        /// <summary>Last-resort: always leave a clickable Next Night on the victory canvas.</summary>
        void ForceVictoryNextNightVisible()
        {
            EnsureResurrectionCanvas();
            PromoteVictoryCanvas();
            CullDuplicateEventSystems();
            Transform host = GetVictoryButtonHost();
            if (host == null)
            {
                Debug.LogError("[DemonKnight] ForceVictoryNextNightVisible: no victory canvas host.");
                return;
            }

            if (nextNightButton == null)
            {
                nextNightButton = FindPanelButton(host, "NextNight", "Next Night");
                if (nextNightButton == null)
                    nextNightButton = CreateNextNightButton(host);
            }
            if (nextNightButton == null)
            {
                Debug.LogError("[DemonKnight] ForceVictoryNextNightVisible: FAILED to create Next Night.");
                return;
            }

            nextNightButton.transform.SetParent(host, false);
            StyleVictoryActionButton(
                nextNightButton,
                "NextNight",
                "Next Night",
                new Vector2(190f, 96f),
                new Color(0.85f, 0.62f, 0.12f, 1f));
            BindButtonOnce(nextNightButton, StartNextTimelineRun);
            nextNightButton.interactable = true;
            nextNightButton.gameObject.SetActive(true);
            nextNightButton.transform.SetAsLastSibling();

            if (endRunButton == null)
            {
                endRunButton = FindPanelButton(host, "EndRun", "End Run");
                if (endRunButton == null)
                    endRunButton = CreateEndRunButton(host);
            }
            if (endRunButton != null)
            {
                endRunButton.transform.SetParent(host, false);
                StyleVictoryActionButton(
                    endRunButton,
                    "EndRun",
                    "End Run",
                    new Vector2(-190f, 96f),
                    new Color(0.55f, 0.18f, 0.16f, 1f));
                BindButtonOnce(endRunButton, EndRunToMainMenu);
                endRunButton.interactable = true;
                endRunButton.gameObject.SetActive(true);
                endRunButton.transform.SetAsLastSibling();
            }

            Debug.LogError(
                $"[DemonKnight] Victory chrome OK: canvas={resurrectionCanvas.name} " +
                $"sort={resurrectionCanvas.GetComponent<Canvas>()?.sortingOrder} " +
                $"nextNightActive={nextNightButton.gameObject.activeInHierarchy} " +
                $"eventSystems={FindObjectsByType<EventSystem>(FindObjectsInactive.Include, FindObjectsSortMode.None).Length}");
        }

        IEnumerator PlayWaveVictorySequence(string message)
        {
            // Lore polish only — wiring already done in TriggerWaveVictoryScreen.
            yield return new WaitForSecondsRealtime(0.35f);

            if (loreNarrativeText != null && !string.IsNullOrEmpty(loreNarrativeText.text))
            {
                if (loreNarrativeText.text.IndexOf("Next Night", StringComparison.OrdinalIgnoreCase) < 0)
                    loreNarrativeText.text += "\n\nPress <b>Next Night</b> to continue.";
            }

            HideLegacyBankPanel();
            RefreshBankUI();
            PromoteVictoryCanvas();
            ForceVictoryNextNightVisible();
            Debug.LogError("[DemonKnight] Wave victory screen ready — Next Night visible.");
            victorySequenceRoutine = null;
        }

        static Button FindPanelButton(Transform root, params string[] nameOrLabels)
        {
            if (root == null || nameOrLabels == null) return null;
            foreach (string key in nameOrLabels)
            {
                if (string.IsNullOrEmpty(key)) continue;
                Transform t = root.Find(key);
                if (t != null)
                {
                    var b = t.GetComponent<Button>();
                    if (b != null) return b;
                }
            }
            foreach (var b in root.GetComponentsInChildren<Button>(true))
            {
                if (b == null) continue;
                string label = b.GetComponentInChildren<TextMeshProUGUI>()?.text ?? "";
                string n = b.gameObject.name ?? "";
                foreach (string key in nameOrLabels)
                {
                    if (string.IsNullOrEmpty(key)) continue;
                    if (n.IndexOf(key, StringComparison.OrdinalIgnoreCase) >= 0
                        || label.IndexOf(key, StringComparison.OrdinalIgnoreCase) >= 0)
                        return b;
                }
            }
            return null;
        }

        Button CreateEndRunButton(Transform parent)
        {
            return CreateAbandonButton(parent, "EndRun", "End Run",
                new Vector2(-170f, 48f), new Color(0.55f, 0.18f, 0.16f, 1f));
        }

        Button CreateNextNightButton(Transform parent)
        {
            return CreateAbandonButton(parent, "NextNight", "Next Night",
                new Vector2(170f, 48f), new Color(0.72f, 0.52f, 0.12f, 1f));
        }

        static Button CreateAbandonButton(Transform parent, string name, string label, Vector2 anchored, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);
            var btn = go.GetComponent<Button>();
            StyleVictoryActionButton(btn, name, label, anchored, color);
            return btn;
        }

        void HideRuneUpgradeUi() => HideLegacyBankPanel();

        void ZeroRuneRanks()
        {
            gossipRuneRank = 0;
            gamblerRuneRank = 0;
            bloodOathRuneRank = 0;
            peasantsCrownRuneRank = 0;
        }

        /// <summary>Close Camelot abandon screen and open the Main Menu.</summary>
        public void EndRunToMainMenu()
        {
            if (resurrectionCanvas != null) resurrectionCanvas.SetActive(false);
            if (legacyBankPanel != null) legacyBankPanel.SetActive(false);
            var loopClear = GameLoopManager.Instance ?? FindFirstObjectByType<GameLoopManager>();
            if (loopClear != null) loopClear.pendingMidRunWaveVictory = false;

            int staged = GrudgeSaveManager.Instance != null
                ? GrudgeSaveManager.Instance.CountPositiveStagedGrudges()
                : 0;
            LegendUnlockManager.Ensure()?.NotifyRunEnded(staged);

            var perk = PerkDraftManager.Instance ?? FindFirstObjectByType<PerkDraftManager>();
            if (perk != null)
            {
                perk.unlockedPerks?.Clear();
                if (perk.perkDraftPanel != null) perk.perkDraftPanel.SetActive(false);
            }

            ZeroRuneRanks();
            SaveBankData();

            var loop = DemonKnightCompatApi.ResolveLoop() ?? FindFirstObjectByType<GameLoopManager>();
            if (loop != null)
            {
                DemonKnightCompatApi.EnsureDisbandedAfterWave(loop);
                if (loop.battlefieldArenaObj != null) loop.battlefieldArenaObj.SetActive(false);
                if (loop.townCanvasObj != null) loop.townCanvasObj.SetActive(true);
            }

            var menu = MainMenuUI.EnsureExists();
            menu?.ShowMenu(boot: true);
            Debug.LogError("[DemonKnight] End Run → Main Menu (perks/runes offline).");
        }

        public void TriggerEndOfRunResurrection(int wavesCleared, bool defeatedNimue, string customReason = "")
        {
            float mult = 1f;
            // Soft lookups so first-build projects missing these managers still compile.
            Type pactType = FindType("ArthurianAutobattler.DemonicPactManager");
            if (pactType != null)
            {
                object pact = pactType.GetProperty("Instance")?.GetValue(null)
                    ?? pactType.GetField("Instance")?.GetValue(null);
                if (pact != null)
                {
                    object active = pactType.GetProperty("ActivePact")?.GetValue(pact);
                    if (active != null && active.ToString() != "None") mult *= 1.5f;
                }
            }
            mult *= SomersetRunes.GetSinLegacyPointMultiplier();

            int earnedPoints = Mathf.Max(1, Mathf.RoundToInt((wavesCleared * 2 + (defeatedNimue ? 10 : 0)) * mult));
            unspentLegacyPoints += earnedPoints;
            SaveBankData();
            // Lose / full-run end: force per-wave disband before Merlin bank (party → pews).
            var loop = DemonKnightCompatApi.ResolveLoop() ?? FindFirstObjectByType<GameLoopManager>();
            // Defeat / Nimue clear uses AdvanceNightAndRestart (wave→1), not mid-run keep-wave.
            if (loop != null) loop.pendingMidRunWaveVictory = false;
            DemonKnightCompatApi.EnsureDisbandedAfterWave(loop);
            int staged = GrudgeSaveManager.Instance != null
                ? GrudgeSaveManager.Instance.CountPositiveStagedGrudges()
                : 0;
            LegendUnlockManager.Ensure()?.NotifyRunEnded(staged);
            if (defeatedNimue)
                LegendUnlockManager.Ensure()?.NotifyFullRunVictory();
            GrudgeSaveManager.Instance?.CommitStagedGrudgesToDisk();
            if (resurrectionSequenceRoutine != null)
                StopCoroutine(resurrectionSequenceRoutine);
            resurrectionSequenceRoutine = StartCoroutine(PlayResurrectionSequence(earnedPoints, defeatedNimue, customReason));
        }

        private IEnumerator PlayResurrectionSequence(int earnedPoints, bool defeatedNimue, string customReason)
        {
            BeginVictoryShow();
            EnsureResurrectionCanvas();
            PromoteVictoryCanvas();
            HideLegacyBankPanel();
            // Wire once at show open — do not wire again after the lore wait.
            WireAbandonScreenButtons(promoteOverlay: true, force: true);

            if (merlinCutsceneAnimator != null)
                merlinCutsceneAnimator.SetTrigger("CastResurrection");

            if (loreNarrativeText != null)
            {
                if (!string.IsNullOrEmpty(customReason))
                    loreNarrativeText.text = customReason;
                else if (defeatedNimue)
                    loreNarrativeText.text =
                        "VICTORY — Nimue falls.\n\n"
                        + "From rain and mist Merlin emerges at last — gaunt, late, and grave.\n"
                        + "Somerset held. But while you stood the churchyard, the Demon Enchantress army overran Camelot and Britain.\n"
                        + "The reconquest war begins.";
                else
                    loreNarrativeText.text =
                        "THE GRAIL NIGHTMARE — Broken Vigil\n\n"
                        + "Somerset falls in a vision of rain and shattered pews. The churchyard is lost.\n"
                        + "Grudge Memory carries forward. Merlin remains missing — no hourglass turns, no rewind.\n"
                        + "Only the vigil broken, and the war still waiting.";
            }

            yield return new WaitForSecondsRealtime(2.0f);

            if (loreNarrativeText != null)
            {
                string outcome = defeatedNimue ? "VICTORY — Camelot Fallen" : "GRAIL NIGHTMARE — Broken Vigil";
                loreNarrativeText.text =
                    $"<b>{outcome}</b>\n\n"
                    + (defeatedNimue
                        ? "Merlin's news stands: Britain bleeds while Somerset held.\n"
                        : "Merlin is still missing. Grudge Memory endures.\n")
                    + "Press <b>Next Night</b> to continue, or <b>End Run</b> for the Main Menu.";
            }
            HideLegacyBankPanel();
            RefreshBankUI();
            PromoteVictoryCanvas();
            resurrectionSequenceRoutine = null;
        }

        public void UpgradeStartingGold() => SpendPointOn(() => startingGoldBankLevel++);
        public void UpgradeRecruitmentDiscount() => SpendPointOn(() => recruitmentDiscountLevel++);
        public void UpgradeSupplyRations() => SpendPointOn(() => startingSupplyRationsLevel++);

        public void UpgradeGossipRune() { /* runes offline */ }
        public void UpgradeGamblerRune() { /* runes offline */ }
        public void UpgradeBloodOathRune() { /* runes offline */ }
        public void UpgradePeasantsCrownRune() { /* runes offline */ }

        private void UpgradeRune(ref int rank)
        {
            // Runes disabled pending redesign.
        }

        private void SpendPointOn(System.Action apply)
        {
            if (unspentLegacyPoints <= 0) return;
            unspentLegacyPoints--;
            apply();
            SaveBankData();
            RefreshBankUI();
        }

        /// <summary>
        /// Begin the next night without relying on Build Settings. Soft-restarts the
        /// GameLoop when present; only falls back to a scene reload if needed.
        /// Mid-run wave victory → ContinueAfterWaveVictory (keep currentBattleNumber).
        /// Defeat / full-run end → AdvanceNightAndRestart (wave resets to 1).
        /// </summary>
        public void StartNextTimelineRun()
        {
            if (advancingNight)
            {
                Debug.LogWarning("[DemonKnight] StartNextTimelineRun ignored — night advance already in progress (duplicate click/listener).");
                return;
            }
            advancingNight = true;
            try
            {
                if (resurrectionCanvas != null) resurrectionCanvas.SetActive(false);
                if (legacyBankPanel != null) legacyBankPanel.SetActive(false);

                var loop = GameLoopManager.Instance ?? FindFirstObjectByType<GameLoopManager>();
                if (loop != null)
                {
                    if (loop.pendingMidRunWaveVictory)
                    {
                        loop.ContinueAfterWaveVictory();
                        return;
                    }
                    loop.AdvanceNightAndRestart();
                    return;
                }

                var scene = SceneManager.GetActiveScene();
                if (scene.buildIndex >= 0)
                {
                    SceneManager.LoadScene(scene.buildIndex);
                    return;
                }
                if (!string.IsNullOrEmpty(scene.name))
                {
                    SceneManager.LoadScene(scene.name);
                    return;
                }

                Debug.LogError("[DemonKnight] StartNextTimelineRun failed — no GameLoopManager and scene is not loadable.");
            }
            finally
            {
                advancingNight = false;
            }
        }

        public int GetBonusStartingGold() => startingGoldBankLevel * 15;
        public int GetGlobalCostDiscount() => recruitmentDiscountLevel * 3;
        public float GetBonusHealthMultiplier() => 1f + (startingSupplyRationsLevel * 0.10f);

        /// <summary>
        /// New Game wipe — clears Merlin bank / runes / legacy points so start gold is exactly
        /// <see cref="GameLoopManager.StartingGold"/> with no leftover meta economy.
        /// PlayerPrefs are always zeroed (even when called as a static-style wipe).
        /// </summary>
        public void ResetBankForNewGame()
        {
            unspentLegacyPoints = 0;
            startingGoldBankLevel = 0;
            recruitmentDiscountLevel = 0;
            startingSupplyRationsLevel = 0;
            gossipRuneRank = 0;
            gamblerRuneRank = 0;
            bloodOathRuneRank = 0;
            peasantsCrownRuneRank = 0;
            WipeBankPlayerPrefs();
            SaveBankData();
            if (resurrectionCanvas != null) resurrectionCanvas.SetActive(false);
            if (legacyBankPanel != null) legacyBankPanel.SetActive(false);
            RefreshBankUI();
            Debug.LogError("[DemonKnight] Merlin bank wiped for New Game (Treasury/Writ/Rations/Runes = 0).");
        }

        /// <summary>Delete Merlin bank PlayerPrefs keys so a missing Instance cannot resurrect ranks.</summary>
        public static void WipeBankPlayerPrefs()
        {
            PlayerPrefs.DeleteKey("LegacyPoints");
            PlayerPrefs.DeleteKey("BankGoldLvl");
            PlayerPrefs.DeleteKey("BankDiscountLvl");
            PlayerPrefs.DeleteKey("BankRationsLvl");
            PlayerPrefs.DeleteKey("RuneGossip");
            PlayerPrefs.DeleteKey("RuneGambler");
            PlayerPrefs.DeleteKey("RuneBloodOath");
            PlayerPrefs.DeleteKey("RunePeasantCrown");
            PlayerPrefs.Save();
        }

        /// <summary>True when every bank/rune level is zero (New Game clean slate).</summary>
        public bool IsFullyClearedForNewGame() =>
            unspentLegacyPoints == 0
            && startingGoldBankLevel == 0
            && recruitmentDiscountLevel == 0
            && startingSupplyRationsLevel == 0
            && gossipRuneRank == 0
            && gamblerRuneRank == 0
            && bloodOathRuneRank == 0
            && peasantsCrownRuneRank == 0;

        public int GetRuneRank(BankRuneID id) => 0;

        private void RefreshBankUI()
        {
            HideLegacyBankPanel();
            PerkDraftManager.SuppressAllPerkUi();
            if (pointsAvailableText != null)
                pointsAvailableText.gameObject.SetActive(false);
            if (goldBankText != null)
                goldBankText.gameObject.SetActive(false);
            if (discountBankText != null)
                discountBankText.gameObject.SetActive(false);
            if (rationsBankText != null)
                rationsBankText.gameObject.SetActive(false);
            if (runeStatusText != null)
            {
                runeStatusText.text =
                    "Press <b>Next Night</b> to continue, or <b>End Run</b> for the Main Menu.";
                if (runeStatusText.fontSize < 18f) runeStatusText.fontSize = 18f;
                runeStatusText.gameObject.SetActive(true);
            }
            if (loreNarrativeText != null && loreNarrativeText.fontSize < 28f)
                loreNarrativeText.fontSize = 28f;
        }

        private static string DescribeGossipEffect(int rank) => rank switch
        {
            0 => "off",
            1 => "adjacent pew trait whisper",
            2 => "nearby pew + grudge flag",
            _ => "full row HP/ATK whisper"
        };

        private static string DescribeGamblerEffect(int rank) => rank switch
        {
            0 => "off",
            1 => "first roll cannot bust",
            2 => "R1 + double pot on natural 6",
            _ => "R2 + +15s town on cash-out"
        };

        private static string DescribeBloodOathEffect(int rank) =>
            rank <= 0
                ? "off"
                : $"Outlaw/Coward: grudge → −{rank * 5}g hire (not +grudge)";

        private static string DescribeCrownEffect(int rank) =>
            rank <= 0
                ? "off"
                : $"Peasant hire −{rank * 2}g; battle +0.15 APS ×⌊peasants/5⌋×R{rank} (cap 2.0)";

        private void SaveBankData()
        {
            PlayerPrefs.SetInt("LegacyPoints", unspentLegacyPoints);
            PlayerPrefs.SetInt("BankGoldLvl", startingGoldBankLevel);
            PlayerPrefs.SetInt("BankDiscountLvl", recruitmentDiscountLevel);
            PlayerPrefs.SetInt("BankRationsLvl", startingSupplyRationsLevel);
            PlayerPrefs.SetInt("RuneGossip", gossipRuneRank);
            PlayerPrefs.SetInt("RuneGambler", gamblerRuneRank);
            PlayerPrefs.SetInt("RuneBloodOath", bloodOathRuneRank);
            PlayerPrefs.SetInt("RunePeasantCrown", peasantsCrownRuneRank);
            PlayerPrefs.Save();
        }

        private void LoadBankData()
        {
            unspentLegacyPoints = PlayerPrefs.GetInt("LegacyPoints", 0);
            startingGoldBankLevel = PlayerPrefs.GetInt("BankGoldLvl", 0);
            recruitmentDiscountLevel = PlayerPrefs.GetInt("BankDiscountLvl", 0);
            startingSupplyRationsLevel = PlayerPrefs.GetInt("BankRationsLvl", 0);
            // Runes offline — ignore saved ranks so old prefs cannot re-enable them.
            gossipRuneRank = 0;
            gamblerRuneRank = 0;
            bloodOathRuneRank = 0;
            peasantsCrownRuneRank = 0;
        }

        private static Type FindType(string fullName)
        {
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                try
                {
                    Type t = asm.GetType(fullName, false);
                    if (t != null) return t;
                }
                catch { /* ignore */ }
            }
            return null;
        }
    }
}
