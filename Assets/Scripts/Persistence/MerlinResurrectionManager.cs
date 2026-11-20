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

        private void Awake()
        {
            if (Instance == null) Instance = this;
            LoadBankData();
            if (resurrectionCanvas != null) resurrectionCanvas.SetActive(false);
            WireNextRunButton();
        }

        private void Start()
        {
            WireNextRunButton();
        }

        /// <summary>
        /// Persistent UnityEvent wiring can be missing on downloaded scenes — always
        /// attach a runtime listener to the Next Run button under the bank panel.
        /// </summary>
        private void WireNextRunButton()
        {
            if (nextRunButtonWired) return;
            if (legacyBankPanel == null)
            {
                var found = GameObject.Find("LegacyBankPanel");
                if (found == null) found = GameObject.Find("BankPanel");
                if (found != null) legacyBankPanel = found;
            }
            if (legacyBankPanel == null) return;

            Transform next = legacyBankPanel.transform.Find("NextRun");
            Button btn = next != null ? next.GetComponent<Button>() : null;
            if (btn == null)
            {
                foreach (var b in legacyBankPanel.GetComponentsInChildren<Button>(true))
                {
                    string label = b.GetComponentInChildren<TextMeshProUGUI>()?.text ?? "";
                    if (b.gameObject.name.IndexOf("NextRun", StringComparison.OrdinalIgnoreCase) >= 0
                        || label.IndexOf("Next Timeline", StringComparison.OrdinalIgnoreCase) >= 0
                        || label.IndexOf("Next Run", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        btn = b;
                        break;
                    }
                }
            }
            if (btn == null) return;

            btn.onClick.AddListener(StartNextTimelineRun);
            nextRunButtonWired = true;
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
            if (legacyBankPanel != null) legacyBankPanel.SetActive(false);

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
                    + $"You gained <b>+{earnedPoints} Arcane Legacy Points</b>.\n"
                    + "Spend them on the Royal Bank and stackable Runes, then enter the next night.";
            }
            if (legacyBankPanel != null) legacyBankPanel.SetActive(true);
            WireNextRunButton();
            RefreshBankUI();
        }

        public void UpgradeStartingGold() => SpendPointOn(() => startingGoldBankLevel++);
        public void UpgradeRecruitmentDiscount() => SpendPointOn(() => recruitmentDiscountLevel++);
        public void UpgradeSupplyRations() => SpendPointOn(() => startingSupplyRationsLevel++);

        public void UpgradeGossipRune() => UpgradeRune(ref gossipRuneRank);
        public void UpgradeGamblerRune() => UpgradeRune(ref gamblerRuneRank);
        public void UpgradeBloodOathRune() => UpgradeRune(ref bloodOathRuneRank);
        public void UpgradePeasantsCrownRune() => UpgradeRune(ref peasantsCrownRuneRank);

        private void UpgradeRune(ref int rank)
        {
            if (unspentLegacyPoints <= 0 || rank >= 3) return;
            unspentLegacyPoints--;
            rank++;
            SaveBankData();
            RefreshBankUI();
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

        public int GetBonusStartingGold() => startingGoldBankLevel * 15;
        public int GetGlobalCostDiscount() => recruitmentDiscountLevel * 3;
        public float GetBonusHealthMultiplier() => 1f + (startingSupplyRationsLevel * 0.10f);

        public int GetRuneRank(BankRuneID id)
        {
            switch (id)
            {
                case BankRuneID.Gossip: return gossipRuneRank;
                case BankRuneID.Gambler: return gamblerRuneRank;
                case BankRuneID.BloodOath: return bloodOathRuneRank;
                case BankRuneID.PeasantsCrown: return peasantsCrownRuneRank;
                default: return 0;
            }
        }

        private void RefreshBankUI()
        {
            if (pointsAvailableText != null)
            {
                pointsAvailableText.text = $"Legacy Points: {unspentLegacyPoints}";
                if (pointsAvailableText.fontSize < 24f) pointsAvailableText.fontSize = 24f;
            }
            if (goldBankText != null)
            {
                goldBankText.text = $"Royal Treasury (+{GetBonusStartingGold()}g Start)";
                if (goldBankText.fontSize < 20f) goldBankText.fontSize = 20f;
            }
            if (discountBankText != null)
            {
                discountBankText.text = $"Royal Writ (-{GetGlobalCostDiscount()}g Cost)";
                if (discountBankText.fontSize < 20f) discountBankText.fontSize = 20f;
            }
            if (rationsBankText != null)
            {
                rationsBankText.text = $"War Rations (+{startingSupplyRationsLevel * 10}% HP)";
                if (rationsBankText.fontSize < 20f) rationsBankText.fontSize = 20f;
            }
            if (runeStatusText != null)
            {
                runeStatusText.text =
                    $"Gossip R{gossipRuneRank}/3 | Gambler R{gamblerRuneRank}/3 | Blood-Oath R{bloodOathRuneRank}/3 | Peasant's Crown R{peasantsCrownRuneRank}/3";
                if (runeStatusText.fontSize < 18f) runeStatusText.fontSize = 18f;
            }
            if (loreNarrativeText != null && loreNarrativeText.fontSize < 28f)
                loreNarrativeText.fontSize = 28f;
            if (legacyBankPanel != null)
            {
                foreach (var tmp in legacyBankPanel.GetComponentsInChildren<TextMeshProUGUI>(true))
                {
                    if (tmp != null && tmp.fontSize < 18f) tmp.fontSize = 18f;
                }
            }
        }

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
            gossipRuneRank = PlayerPrefs.GetInt("RuneGossip", 0);
            gamblerRuneRank = PlayerPrefs.GetInt("RuneGambler", 0);
            bloodOathRuneRank = PlayerPrefs.GetInt("RuneBloodOath", 0);
            peasantsCrownRuneRank = PlayerPrefs.GetInt("RunePeasantCrown", 0);
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
