using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
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
        public int startingGoldBankLevel = 0;      // +15g starting purse per level
        public int recruitmentDiscountLevel = 0;   // -3g base cost per level
        public int startingSupplyRationsLevel = 0; // +10% base HP per level

        [Header("Bank UI Labels")]
        public TextMeshProUGUI pointsAvailableText;
        public TextMeshProUGUI goldBankText;
        public TextMeshProUGUI discountBankText;
        public TextMeshProUGUI rationsBankText;

        private void Awake()
        {
            if (Instance == null) Instance = this;
            LoadBankData();
            if (resurrectionCanvas != null) resurrectionCanvas.SetActive(false);
        }

        public void TriggerEndOfRunResurrection(int wavesCleared, bool defeatedNimue, string customReason = "")
        {
            int earnedPoints = Mathf.Max(1, wavesCleared * 2 + (defeatedNimue ? 10 : 0));
            unspentLegacyPoints += earnedPoints;
            SaveBankData();
            GrudgeSaveManager.Instance?.CommitStagedGrudgesToDisk();
            StartCoroutine(PlayResurrectionSequence(earnedPoints, defeatedNimue, customReason));
        }

        private IEnumerator PlayResurrectionSequence(int earnedPoints, bool defeatedNimue, string customReason)
        {
            if (resurrectionCanvas != null) resurrectionCanvas.SetActive(true);
            if (legacyBankPanel != null) legacyBankPanel.SetActive(false);

            if (merlinCutsceneAnimator != null)
                merlinCutsceneAnimator.SetTrigger("CastResurrection");

            if (!string.IsNullOrEmpty(customReason))
                loreNarrativeText.text = customReason;
            else
                loreNarrativeText.text = defeatedNimue
                    ? "Nimue falls, yet the abyss stirs... Merlin turns the hourglass to prepare Camelot once more."
                    : "The horde overwhelms the gate... Merlin shatters the hourglass, pulling you and the townsfolk back from death. They remember the slaughter.";

            yield return new WaitForSeconds(3.5f);

            loreNarrativeText.text = $"You gained <b>+{earnedPoints} Arcane Legacy Points</b>. Allocate supplies to your Royal Bank before re-entering Camelot.";
            if (legacyBankPanel != null) legacyBankPanel.SetActive(true);
            RefreshBankUI();
        }

        public void UpgradeStartingGold()
        {
            if (unspentLegacyPoints <= 0) return;
            unspentLegacyPoints--;
            startingGoldBankLevel++;
            SaveBankData();
            RefreshBankUI();
        }

        public void UpgradeRecruitmentDiscount()
        {
            if (unspentLegacyPoints <= 0) return;
            unspentLegacyPoints--;
            recruitmentDiscountLevel++;
            SaveBankData();
            RefreshBankUI();
        }

        public void UpgradeSupplyRations()
        {
            if (unspentLegacyPoints <= 0) return;
            unspentLegacyPoints--;
            startingSupplyRationsLevel++;
            SaveBankData();
            RefreshBankUI();
        }

        public void StartNextTimelineRun()
        {
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
        }

        public int GetBonusStartingGold() => startingGoldBankLevel * 15;
        public int GetGlobalCostDiscount() => recruitmentDiscountLevel * 3;
        public float GetBonusHealthMultiplier() => 1f + (startingSupplyRationsLevel * 0.10f);

        private void RefreshBankUI()
        {
            if (pointsAvailableText != null) pointsAvailableText.text = $"Legacy Points: {unspentLegacyPoints}";
            if (goldBankText != null) goldBankText.text = $"Royal Treasury (+{GetBonusStartingGold()}g Start)";
            if (discountBankText != null) discountBankText.text = $"Royal Writ (-{GetGlobalCostDiscount()}g Cost)";
            if (rationsBankText != null) rationsBankText.text = $"War Rations (+{startingSupplyRationsLevel * 10}% HP)";
        }

        private void SaveBankData()
        {
            PlayerPrefs.SetInt("LegacyPoints", unspentLegacyPoints);
            PlayerPrefs.SetInt("BankGoldLvl", startingGoldBankLevel);
            PlayerPrefs.SetInt("BankDiscountLvl", recruitmentDiscountLevel);
            PlayerPrefs.SetInt("BankRationsLvl", startingSupplyRationsLevel);
            PlayerPrefs.Save();
        }

        private void LoadBankData()
        {
            unspentLegacyPoints = PlayerPrefs.GetInt("LegacyPoints", 0);
            startingGoldBankLevel = PlayerPrefs.GetInt("BankGoldLvl", 0);
            recruitmentDiscountLevel = PlayerPrefs.GetInt("BankDiscountLvl", 0);
            startingSupplyRationsLevel = PlayerPrefs.GetInt("BankRationsLvl", 0);
        }
    }
}
