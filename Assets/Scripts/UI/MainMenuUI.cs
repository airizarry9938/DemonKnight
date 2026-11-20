using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using TMPro;

namespace ArthurianAutobattler
{
    /// <summary>
    /// Boot / pause menu: New Game, Continue, Load/Save slots, Restart, Quit.
    /// Auto-spawns after scene load even if scene/builder never added it.
    /// Esc always toggles this menu (except during AutoBattle).
    /// </summary>
    public class MainMenuUI : MonoBehaviour
    {
        public static MainMenuUI Instance;

        public GameObject menuRoot;
        public Button newGameButton;
        public Button continueButton;
        public Button loadGameButton;
        public Button saveGameButton;
        public Button restartButton;
        public Button quitButton;
        public Button closeMenuButton;
        public GameObject slotsPanel;
        public Button[] slotLoadButtons = new Button[3];
        public Button[] slotSaveButtons = new Button[3];
        public TextMeshProUGUI[] slotLabels = new TextMeshProUGUI[3];
        public TextMeshProUGUI statusText;

        private bool slotsAreLoadMode = true;

        /// <summary>Spawns MainMenuCanvas if missing — called from GameLoop Awake / Church Start / scene load.</summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AutoSpawnAfterSceneLoad()
        {
            EnsureExists();
            Debug.Log("[DemonKnight] MainMenuUI AutoSpawnAfterSceneLoad — Esc opens Quit/Restart/New/Load.");
        }

        public static MainMenuUI EnsureExists()
        {
            if (Instance != null)
            {
                Instance.EnsureBuilt();
                return Instance;
            }

            var existing = Object.FindFirstObjectByType<MainMenuUI>();
            if (existing != null)
            {
                Instance = existing;
                existing.EnsureBuilt();
                return existing;
            }

            var go = new GameObject("MainMenuCanvas");
            var menu = go.AddComponent<MainMenuUI>();
            menu.EnsureBuilt();
            Debug.Log("[DemonKnight] MainMenuUI.EnsureExists — created MainMenuCanvas.");
            return menu;
        }

        private void Awake()
        {
            if (Instance == null) Instance = this;
            else if (Instance != this) { Destroy(gameObject); return; }
            EnsureBuilt();
        }

        private void Start()
        {
            EnsureBuilt();
            WireButtons();
            ShowMenu(boot: true);
            Debug.Log("[DemonKnight] MainMenuUI Start — menu shown. Esc toggles. Diagnose package must include MainMenuUI.cs.");
        }

        private void Update()
        {
            if (!Input.GetKeyDown(KeyCode.Escape)) return;
            HandleEscapeKey();
        }

        /// <summary>Shared by keyboard Esc and HUD ☰ Esc button.</summary>
        public void HandleEscapeKey()
        {
            var loop = DemonKnightCompatApi.ResolveLoop();
            if (loop != null && loop.currentPhase == GamePhase.AutoBattle)
            {
                Debug.Log("[DemonKnight] Esc ignored during AutoBattle.");
                return;
            }

            // Close Dice/Legends/Warband first — same priority as HUD menu button.
            var church = FindFirstObjectByType<SomersetChurchUI>()
                         ?? (SomersetChurchUI)FindFirstObjectByType<ChurchStageUIController>();
            if (church != null && church.AnyFloatingModalOpen())
            {
                church.CloseFloatingModals();
                Debug.Log("[DemonKnight] Esc → closed floating modal.");
                return;
            }

            ToggleMenu();
        }

        public void ToggleMenu()
        {
            EnsureBuilt();
            var loop = DemonKnightCompatApi.ResolveLoop();
            if (menuRoot != null && menuRoot.activeSelf)
            {
                // At boot with no campaign yet, Esc should not hide without starting.
                if (loop == null || loop.currentPhase == default && !WarbandAutosave.HasAutosave())
                {
                    SetStatus("Pick New Game or Load a slot.");
                    return;
                }
                HideMenu();
                Debug.Log("[DemonKnight] Esc/Menu → Main Menu closed.");
            }
            else
            {
                ShowMenu(boot: false);
                Debug.Log("[DemonKnight] Esc/Menu → Main Menu open (Quit / Restart / New / Load).");
            }
        }

        public void EnsureBuilt()
        {
            var canvas = gameObject.GetComponent<Canvas>();
            if (canvas == null)
            {
                canvas = gameObject.AddComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvas.sortingOrder = 500;
                var scaler = gameObject.AddComponent<CanvasScaler>();
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1920f, 1080f);
                gameObject.AddComponent<GraphicRaycaster>();
            }
            else
            {
                canvas.sortingOrder = Mathf.Max(canvas.sortingOrder, 500);
            }

            if (menuRoot != null) return;

            menuRoot = new GameObject("MenuRoot", typeof(RectTransform), typeof(Image));
            menuRoot.transform.SetParent(transform, false);
            Stretch(menuRoot.GetComponent<RectTransform>());
            menuRoot.GetComponent<Image>().color = new Color(0.04f, 0.05f, 0.07f, 0.96f);

            var title = MakeTmp(menuRoot.transform, "Title", "LAST KNIGHTS OF SOMERSET", 42f,
                new Vector2(0.5f, 0.88f), new Vector2(0.5f, 0.88f), new Vector2(980f, 60f));
            title.alignment = TextAlignmentOptions.Center;
            title.color = new Color(0.92f, 0.86f, 0.7f);

            statusText = MakeTmp(menuRoot.transform, "Status", "", 20f,
                new Vector2(0.5f, 0.78f), new Vector2(0.5f, 0.78f), new Vector2(900f, 48f));
            statusText.alignment = TextAlignmentOptions.Center;
            statusText.color = new Color(0.75f, 0.8f, 0.85f);

            newGameButton = MakeButton(menuRoot.transform, "NewGame", "New Game", new Vector2(0f, 120f));
            continueButton = MakeButton(menuRoot.transform, "Continue", "Continue", new Vector2(0f, 55f));
            loadGameButton = MakeButton(menuRoot.transform, "LoadGame", "Load Game (slots)", new Vector2(0f, -10f));
            saveGameButton = MakeButton(menuRoot.transform, "SaveGame", "Save Game (slots)", new Vector2(0f, -75f));
            restartButton = MakeButton(menuRoot.transform, "Restart", "Restart Scene", new Vector2(0f, -140f));
            quitButton = MakeButton(menuRoot.transform, "Quit", "Quit", new Vector2(0f, -205f));
            closeMenuButton = MakeButton(menuRoot.transform, "Close", "Return to Church", new Vector2(0f, -270f));

            slotsPanel = new GameObject("SlotsPanel", typeof(RectTransform), typeof(Image));
            slotsPanel.transform.SetParent(menuRoot.transform, false);
            var srt = slotsPanel.GetComponent<RectTransform>();
            srt.anchorMin = srt.anchorMax = new Vector2(0.5f, 0.5f);
            srt.sizeDelta = new Vector2(560f, 300f);
            srt.anchoredPosition = new Vector2(0f, -10f);
            slotsPanel.GetComponent<Image>().color = new Color(0.1f, 0.12f, 0.14f, 0.98f);
            slotsPanel.SetActive(false);

            slotLoadButtons = new Button[3];
            slotSaveButtons = new Button[3];
            slotLabels = new TextMeshProUGUI[3];
            for (int i = 0; i < 3; i++)
            {
                float y = 90f - i * 70f;
                slotLabels[i] = MakeTmp(slotsPanel.transform, $"SlotLabel{i}", $"Slot {i + 1}", 18f,
                    new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(220f, 28f));
                slotLabels[i].rectTransform.anchoredPosition = new Vector2(-130f, y);
                slotLoadButtons[i] = MakeButton(slotsPanel.transform, $"Load{i}", "Load", new Vector2(90f, y), new Vector2(100f, 40f));
                slotSaveButtons[i] = MakeButton(slotsPanel.transform, $"Save{i}", "Save", new Vector2(200f, y), new Vector2(100f, 40f));
            }

            var back = MakeButton(slotsPanel.transform, "SlotsBack", "Back", new Vector2(0f, -120f), new Vector2(160f, 40f));
            back.onClick.AddListener(() => { slotsPanel.SetActive(false); RefreshMainButtons(); });
            WireButtons();
        }

        private void WireButtons()
        {
            newGameButton?.onClick.RemoveAllListeners();
            newGameButton?.onClick.AddListener(OnNewGame);
            continueButton?.onClick.RemoveAllListeners();
            continueButton?.onClick.AddListener(OnContinue);
            loadGameButton?.onClick.RemoveAllListeners();
            loadGameButton?.onClick.AddListener(() => OpenSlots(loadMode: true));
            saveGameButton?.onClick.RemoveAllListeners();
            saveGameButton?.onClick.AddListener(() => OpenSlots(loadMode: false));
            restartButton?.onClick.RemoveAllListeners();
            restartButton?.onClick.AddListener(OnRestart);
            quitButton?.onClick.RemoveAllListeners();
            quitButton?.onClick.AddListener(OnQuit);
            closeMenuButton?.onClick.RemoveAllListeners();
            closeMenuButton?.onClick.AddListener(HideMenu);

            for (int i = 0; i < 3; i++)
            {
                int slot = i + 1;
                if (slotLoadButtons != null && i < slotLoadButtons.Length && slotLoadButtons[i] != null)
                {
                    slotLoadButtons[i].onClick.RemoveAllListeners();
                    slotLoadButtons[i].onClick.AddListener(() => OnLoadSlot(slot));
                }
                if (slotSaveButtons != null && i < slotSaveButtons.Length && slotSaveButtons[i] != null)
                {
                    slotSaveButtons[i].onClick.RemoveAllListeners();
                    slotSaveButtons[i].onClick.AddListener(() => OnSaveSlot(slot));
                }
            }
        }

        public void ShowMenu(bool boot)
        {
            EnsureBuilt();
            WireButtons();
            if (menuRoot != null) menuRoot.SetActive(true);
            if (slotsPanel != null) slotsPanel.SetActive(false);
            RefreshMainButtons();
            SetStatus(boot
                ? "NEW GAME clears stale party. Esc = this menu (Quit / Restart / New / Load)."
                : "Paused — Quit / Restart / New / Load / Save, or Return to Church.");
            Debug.Log("[DemonKnight] MainMenuUI.ShowMenu visible=true");
        }

        public void HideMenu()
        {
            if (menuRoot != null) menuRoot.SetActive(false);
            if (slotsPanel != null) slotsPanel.SetActive(false);
            Debug.Log("[DemonKnight] MainMenuUI.HideMenu");
        }

        public bool IsMenuOpen => menuRoot != null && menuRoot.activeSelf;

        private void RefreshMainButtons()
        {
            bool hasAuto = WarbandAutosave.HasAutosave();
            if (continueButton != null) continueButton.interactable = hasAuto;
            if (saveGameButton != null)
            {
                var loop = DemonKnightCompatApi.ResolveLoop();
                saveGameButton.interactable = loop != null && loop.currentPhase != GamePhase.AutoBattle;
            }
            RefreshSlotLabels();
        }

        private void OpenSlots(bool loadMode)
        {
            slotsAreLoadMode = loadMode;
            if (slotsPanel != null) slotsPanel.SetActive(true);
            RefreshSlotLabels();
            for (int i = 0; i < 3; i++)
            {
                bool has = WarbandAutosave.HasSlot(i + 1);
                if (slotLoadButtons != null && i < slotLoadButtons.Length && slotLoadButtons[i] != null)
                {
                    slotLoadButtons[i].gameObject.SetActive(loadMode);
                    slotLoadButtons[i].interactable = has;
                }
                if (slotSaveButtons != null && i < slotSaveButtons.Length && slotSaveButtons[i] != null)
                    slotSaveButtons[i].gameObject.SetActive(!loadMode);
            }
            SetStatus(loadMode ? "Choose a slot to load." : "Choose a slot to save.");
        }

        private void RefreshSlotLabels()
        {
            for (int i = 0; i < 3; i++)
            {
                if (slotLabels == null || i >= slotLabels.Length || slotLabels[i] == null) continue;
                slotLabels[i].text = WarbandAutosave.DescribeSlot(i + 1);
            }
        }

        private void OnNewGame()
        {
            var loop = DemonKnightCompatApi.ResolveLoop() ?? FindFirstObjectByType<GameLoopManager>();
            if (loop == null)
            {
                SetStatus("No GameLoopManager in scene.");
                return;
            }

            // Full wipe: bank runes, sin runes, relics, perks, pacts, autosave → StartingGold.
            DemonKnightCompatApi.SetMustDisband(loop, false);
            loop.recruitedParty?.Clear();
            loop.InitializeFreshCampaign();
            HideMenu();
            SetStatus(
                $"New Game — {GameLoopManager.StartingGold}g; runes/perks/relics/sins/pacts off.");
            Debug.LogError(
                $"[DemonKnight] === MAIN MENU NEW GAME — all modifiers wiped, gold={GameLoopManager.StartingGold}g ===");
        }

        private void OnContinue()
        {
            var loop = DemonKnightCompatApi.ResolveLoop() ?? FindFirstObjectByType<GameLoopManager>();
            if (loop == null || !WarbandAutosave.HasAutosave())
            {
                SetStatus("No autosave to continue.");
                return;
            }
            loop.ContinueTimelineFromAutosave();
            DemonKnightCompatApi.EnsureDisbandedAfterWave(loop);
            HideMenu();
            SetStatus("Continued from autosave (party forced empty for rehire).");
        }

        private void OnLoadSlot(int slot)
        {
            var loop = DemonKnightCompatApi.ResolveLoop() ?? FindFirstObjectByType<GameLoopManager>();
            if (loop == null || !WarbandAutosave.HasSlot(slot))
            {
                SetStatus($"Slot {slot} is empty.");
                return;
            }
            if (!WarbandAutosave.LoadSlotIntoAutosave(slot))
            {
                SetStatus($"Failed to load slot {slot}.");
                return;
            }
            loop.ContinueTimelineFromAutosave();
            DemonKnightCompatApi.EnsureDisbandedAfterWave(loop);
            HideMenu();
            SetStatus($"Loaded slot {slot}.");
        }

        private void OnSaveSlot(int slot)
        {
            var loop = DemonKnightCompatApi.ResolveLoop() ?? FindFirstObjectByType<GameLoopManager>();
            if (loop == null)
            {
                SetStatus("Nothing to save.");
                return;
            }
            DemonKnightCompatApi.AutosaveWarband(loop);
            if (WarbandAutosave.CopyAutosaveToSlot(slot))
            {
                RefreshSlotLabels();
                SetStatus($"Saved to slot {slot}.");
            }
            else SetStatus($"Failed to save slot {slot}.");
        }

        private void OnRestart()
        {
            Debug.LogError("[DemonKnight] === MAIN MENU RESTART SCENE ===");
            var scene = SceneManager.GetActiveScene();
            if (scene.buildIndex >= 0)
                SceneManager.LoadScene(scene.buildIndex);
            else
                SceneManager.LoadScene(scene.name);
        }

        private void OnQuit()
        {
            Debug.LogError("[DemonKnight] === MAIN MENU QUIT ===");
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        private void SetStatus(string msg)
        {
            if (statusText != null) statusText.text = msg ?? "";
        }

        private static void Stretch(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }

        private static TextMeshProUGUI MakeTmp(Transform parent, string name, string text, float size,
            Vector2 anchorMin, Vector2 anchorMax, Vector2 sizeDelta)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.sizeDelta = sizeDelta;
            rt.anchoredPosition = Vector2.zero;
            var tmp = go.GetComponent<TextMeshProUGUI>();
            tmp.text = text;
            tmp.fontSize = size;
            tmp.color = Color.white;
            return tmp;
        }

        private static Button MakeButton(Transform parent, string name, string label, Vector2 anchoredPos,
            Vector2? size = null)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = size ?? new Vector2(300f, 52f);
            rt.anchoredPosition = anchoredPos;
            go.GetComponent<Image>().color = new Color(0.22f, 0.28f, 0.36f, 1f);
            var tmp = MakeTmp(go.transform, "Label", label, 22f, Vector2.zero, Vector2.one, Vector2.zero);
            Stretch(tmp.rectTransform);
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.fontSize = 22f;
            return go.GetComponent<Button>();
        }
    }
}
