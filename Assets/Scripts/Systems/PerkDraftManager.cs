using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace ArthurianAutobattler
{
    public enum RunPerkID
    {
        PitchforkMilitia,
        ArthursTaxation,
        RoyalScoutingDecree,
        HasteOfCamelot,
        TavernRiggedDice
    }

    [Serializable]
    public class RunPerkOption
    {
        public RunPerkID perkID;
        public string title;
        public string description;
    }

    public class PerkDraftManager : MonoBehaviour
    {
        public static PerkDraftManager Instance;

        public GameObject perkDraftPanel;
        public Button[] choiceButtons;
        public TextMeshProUGUI[] choiceTitleTexts;
        public TextMeshProUGUI[] choiceDescTexts;

        public HashSet<RunPerkID> unlockedPerks = new HashSet<RunPerkID>();

        private readonly List<RunPerkOption> allPerks = new List<RunPerkOption>
        {
            new RunPerkOption
            {
                perkID = RunPerkID.PitchforkMilitia,
                title = "Pitchfork Militia",
                description = "All Peasants gain +10 Max HP and +2 Attack."
            },
            new RunPerkOption
            {
                perkID = RunPerkID.ArthursTaxation,
                title = "Arthur's Taxation",
                description = "Immediately gain +40 Royal Gold."
            },
            new RunPerkOption
            {
                perkID = RunPerkID.RoyalScoutingDecree,
                title = "Royal Scouting Decree",
                description = "Tier 1 Basic Stats are revealed free on all townsfolk."
            },
            new RunPerkOption
            {
                perkID = RunPerkID.HasteOfCamelot,
                title = "Haste of Camelot",
                description = "All Warriors and Knights gain +0.25 Attacks/Sec."
            },
            new RunPerkOption
            {
                perkID = RunPerkID.TavernRiggedDice,
                title = "Loaded Tavern Dice",
                description = "Your first roll in every Tavern Dice game cannot bust."
            }
        };

        private void Awake()
        {
            if (Instance == null) Instance = this;
            if (perkDraftPanel != null) perkDraftPanel.SetActive(false);
        }

        public void OpenPerkDraft()
        {
            if (perkDraftPanel != null) perkDraftPanel.SetActive(true);

            List<RunPerkOption> pool = new List<RunPerkOption>(allPerks);
            for (int i = 0; i < choiceButtons.Length; i++)
            {
                if (pool.Count == 0) break;
                int idx = UnityEngine.Random.Range(0, pool.Count);
                RunPerkOption offered = pool[idx];
                pool.RemoveAt(idx);

                choiceTitleTexts[i].text = offered.title;
                choiceDescTexts[i].text = offered.description;
                choiceButtons[i].onClick.RemoveAllListeners();
                choiceButtons[i].onClick.AddListener(() => SelectPerk(offered));
            }
        }

        private void SelectPerk(RunPerkOption chosen)
        {
            unlockedPerks.Add(chosen.perkID);

            if (chosen.perkID == RunPerkID.ArthursTaxation)
            {
                var ui = FindFirstObjectByType<TownSplitUIController>();
                if (ui != null) ui.playerGold += 40;
            }

            perkDraftPanel.SetActive(false);
            // Each wave win shortens the next town phase (floor 180s; initial range 300–600).
            GameLoopManager.Instance.townTimeLimit = Mathf.Clamp(
                GameLoopManager.Instance.townTimeLimit - 30f, 180f, 600f);
            GameLoopManager.Instance.StartTownPhase();
        }

        public void ApplyCombatPerksToParty(List<RecruitInstance> party)
        {
            foreach (var u in party)
            {
                if (unlockedPerks.Contains(RunPerkID.PitchforkMilitia) && u.unitType == UnitType.Peasant)
                {
                    u.maxHealth += 10;
                    u.attackDamage += 2;
                }

                if (unlockedPerks.Contains(RunPerkID.HasteOfCamelot)
                    && (u.unitType == UnitType.Warrior || u.unitType == UnitType.Knight))
                {
                    u.attackSpeed = Mathf.Min(2.0f, u.attackSpeed + 0.25f);
                }
            }
        }
    }
}
