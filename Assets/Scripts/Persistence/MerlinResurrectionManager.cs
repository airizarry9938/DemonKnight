using System;
using System.Collections;
using UnityEngine;
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

        private bool nextRunButtonWired;
        private bool endRunButtonWired;
        private Button endRunButton;
        private Button nextNightButton;
        /// <summary>Blocks double AdvanceNight when scene persistent + runtime listeners both fire.</summary>
        private bool advancingNight;

        private void Awake()
        {
            if (Instance == null) Instance = this;
            LoadBankData();
            // Runes disabled pending redesign — ranks stay zeroed.
            ZeroRuneRanks();
            if (resurrectionCanvas != null) resurrectionCanvas.SetActive(false);
            HideLegacyBankPanel();
            WireAbandonScreenButtons();
        }

        private void Start()
        {
            HideLegacyBankPanel();
            WireAbandonScreenButtons();
        }

        void WireAbandonScreenButtons()
        {
            HideLegacyBankPanel();
            WireNextNightButton();
            WireEndRunButton();
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
        /// Next Night = same advance as former Enter Next Timeline (AdvanceNightAndRestart).
        /// Single listener only — clears persistent Inspector wiring.
        /// </summary>
        private void WireNextNightButton()
        {
            EnsureResurrectionCanvas();
            Transform host = GetAbandonHost();
            if (host == null) return;

            nextNightButton = FindPanelButton(host, "NextNight", "Next Night", "NextRun", "Next Timeline", "Next Run");
            if (nextNightButton == null)
                nextNightButton = CreateNextNightButton(host);

            if (nextNightButton == null) return;
            // Replace ALL listeners (persistent scene + prior runtime) with exactly one.
            nextNightButton.onClick = new Button.ButtonClickedEvent();
            nextNightButton.onClick.AddListener(StartNextTimelineRun);
            nextNightButton.gameObject.SetActive(true);
            var label = nextNightButton.GetComponentInChildren<TextMeshProUGUI>();
            if (label != null) label.text = "Next Night";
            nextRunButtonWired = true;
        }

        /// <summary>
        /// Camelot abandon / end-of-run screen: End Run closes the cutscene and opens Main Menu.
        /// </summary>
        private void WireEndRunButton()
        {
            EnsureResurrectionCanvas();
            Transform host = GetAbandonHost();
            if (host == null) return;

            endRunButton = FindPanelButton(host, "EndRun", "End Run", "Abandon Camelot", "Return to Menu");
            if (endRunButton == null)
                endRunButton = CreateEndRunButton(host);

            if (endRunButton == null) return;
            endRunButton.onClick = new Button.ButtonClickedEvent();
            endRunButton.onClick.AddListener(EndRunToMainMenu);
            endRunButton.gameObject.SetActive(true);
            endRunButtonWired = true;
        }

        Transform GetAbandonHost()
        {
            EnsureResurrectionCanvas();
            if (resurrectionCanvas == null) return null;
            var panel = resurrectionCanvas.transform.Find("ResurrectionPanel");
            return panel != null ? panel : resurrectionCanvas.transform;
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
            if (resurrectionCanvas != null) return;
            var found = GameObject.Find("ResurrectionCanvas");
            if (found == null) found = GameObject.Find("MerlinResurrection");
            if (found != null) resurrectionCanvas = found;
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
                new Vector2(-160f, 36f), new Color(0.45f, 0.16f, 0.14f, 0.98f));
        }

        Button CreateNextNightButton(Transform parent)
        {
            return CreateAbandonButton(parent, "NextNight", "Next Night",
                new Vector2(160f, 36f), new Color(0.28f, 0.18f, 0.42f, 0.98f));
        }

        static Button CreateAbandonButton(Transform parent, string name, string label, Vector2 anchored, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 0f);
            rt.anchorMax = new Vector2(0.5f, 0f);
            rt.pivot = new Vector2(0.5f, 0f);
            rt.anchoredPosition = anchored;
            rt.sizeDelta = new Vector2(280f, 56f);
            go.GetComponent<Image>().color = color;

            var labelGo = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
            labelGo.transform.SetParent(go.transform, false);
            var lrt = labelGo.GetComponent<RectTransform>();
            lrt.anchorMin = Vector2.zero;
            lrt.anchorMax = Vector2.one;
            lrt.offsetMin = Vector2.zero;
            lrt.offsetMax = Vector2.zero;
            var tmp = labelGo.GetComponent<TextMeshProUGUI>();
            tmp.text = label;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.fontSize = 26f;
            tmp.color = Color.white;
            return go.GetComponent<Button>();
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
            DemonKnightCompatApi.EnsureDisbandedAfterWave(loop);
            GrudgeSaveManager.Instance?.CommitStagedGrudgesToDisk();
            StartCoroutine(PlayResurrectionSequence(earnedPoints, defeatedNimue, customReason));
        }

        private IEnumerator PlayResurrectionSequence(int earnedPoints, bool defeatedNimue, string customReason)
        {
            if (resurrectionCanvas != null) resurrectionCanvas.SetActive(true);
            HideLegacyBankPanel();

            if (merlinCutsceneAnimator != null)
                merlinCutsceneAnimator.SetTrigger("CastResurrection");

            if (loreNarrativeText != null)
            {
                if (!string.IsNullOrEmpty(customReason))
                    loreNarrativeText.text = customReason;
                else if (defeatedNimue)
                    loreNarrativeText.text =
                        "VICTORY — Nimue is defeated.\n\n"
                        + "Yet the abyss stirs… Merlin turns the hourglass inside the Somerset church once more.";
                else
                    loreNarrativeText.text =
                        "DEFEAT — Your warband was overrun.\n\n"
                        + "The horde breaches the churchyard. Merlin shatters the hourglass. The pews remember the slaughter.";
            }

            yield return new WaitForSeconds(3.5f);

            if (loreNarrativeText != null)
            {
                string outcome = defeatedNimue ? "VICTORY" : "DEFEAT";
                loreNarrativeText.text =
                    $"<b>{outcome}</b>\n\n"
                    + "Perks & runes are offline while we redesign them.\n"
                    + "Press <b>Next Night</b> to continue, or <b>End Run</b> for the Main Menu.";
            }
            HideLegacyBankPanel();
            WireAbandonScreenButtons();
            RefreshBankUI();
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
