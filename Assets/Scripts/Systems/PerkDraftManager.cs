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
        public TextMeshProUGUI victoryBannerText;

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

        public void OpenPerkDraft() => OpenPerkDraft(0, 0);

        /// <summary>Wave win → show victory banner on the perk panel, then offer picks.</summary>
        public void OpenPerkDraft(int waveCleared, int goldEarned)
        {
            EnsurePanelWired();

            // Wave win → rewards: party must already be back in pews (per-wave disband).
            var loopEarly = DemonKnightCompatApi.ResolveLoop() ?? FindFirstObjectByType<GameLoopManager>();
            DemonKnightCompatApi.EnsureDisbandedAfterWave(loopEarly, null, true);

            if (perkDraftPanel == null
                || choiceButtons == null || choiceButtons.Length == 0
                || choiceTitleTexts == null || choiceDescTexts == null)
            {
                Debug.LogWarning("[DemonKnight] Perk draft UI missing — skipping to town.");
                FallbackToTown();
                return;
            }

            // Parent TownCanvas may have been off during battle.
            if (perkDraftPanel.transform.root != null)
                perkDraftPanel.transform.root.gameObject.SetActive(true);
            var loop = DemonKnightCompatApi.ResolveLoop() ?? FindFirstObjectByType<GameLoopManager>();
            if (loop != null && loop.townCanvasObj != null)
                loop.townCanvasObj.SetActive(true);

            perkDraftPanel.SetActive(true);
            perkDraftPanel.transform.SetAsLastSibling();

            ShowVictoryBanner(waveCleared, goldEarned);

            List<RunPerkOption> pool = new List<RunPerkOption>(allPerks);
            int slots = Mathf.Min(choiceButtons.Length, choiceTitleTexts.Length, choiceDescTexts.Length);
            for (int i = 0; i < slots; i++)
            {
                if (choiceButtons[i] == null || choiceTitleTexts[i] == null || choiceDescTexts[i] == null)
                    continue;
                if (pool.Count == 0) break;
                int idx = UnityEngine.Random.Range(0, pool.Count);
                RunPerkOption offered = pool[idx];
                pool.RemoveAt(idx);

                choiceTitleTexts[i].text = offered.title;
                if (choiceTitleTexts[i].fontSize < 24f) choiceTitleTexts[i].fontSize = 24f;
                choiceDescTexts[i].text = offered.description;
                if (choiceDescTexts[i].fontSize < 18f) choiceDescTexts[i].fontSize = 18f;
                choiceButtons[i].onClick.RemoveAllListeners();
                RunPerkOption captured = offered;
                choiceButtons[i].onClick.AddListener(() => SelectPerk(captured));
                choiceButtons[i].interactable = true;
                var btnLabel = choiceButtons[i].GetComponentInChildren<TextMeshProUGUI>();
                if (btnLabel != null && btnLabel.fontSize < 22f) btnLabel.fontSize = 22f;
            }

            var panelTitle = perkDraftPanel.transform.Find("PerkTitle")?.GetComponent<TextMeshProUGUI>();
            if (panelTitle != null)
            {
                if (panelTitle.fontSize < 34f) panelTitle.fontSize = 34f;
                if (waveCleared > 0)
                    panelTitle.text = "VICTORY — Choose a Perk";
            }
        }

        private void ShowVictoryBanner(int waveCleared, int goldEarned)
        {
            if (perkDraftPanel == null) return;
            if (victoryBannerText == null)
            {
                var existing = perkDraftPanel.transform.Find("VictoryBanner");
                if (existing != null) victoryBannerText = existing.GetComponent<TextMeshProUGUI>();
            }
            if (victoryBannerText == null)
            {
                var go = new GameObject("VictoryBanner", typeof(RectTransform), typeof(TextMeshProUGUI));
                go.transform.SetParent(perkDraftPanel.transform, false);
                var rt = go.GetComponent<RectTransform>();
                rt.anchorMin = new Vector2(0.5f, 1f);
                rt.anchorMax = new Vector2(0.5f, 1f);
                rt.pivot = new Vector2(0.5f, 1f);
                rt.anchoredPosition = new Vector2(0f, -18f);
                rt.sizeDelta = new Vector2(900f, 56f);
                victoryBannerText = go.GetComponent<TextMeshProUGUI>();
                victoryBannerText.alignment = TextAlignmentOptions.Center;
                victoryBannerText.fontSize = 28f;
                victoryBannerText.color = new Color(0.95f, 0.85f, 0.35f);
            }

            string waveBit = waveCleared > 0 ? $"Wave {waveCleared} cleared" : "Wave cleared";
            string goldBit = goldEarned > 0 ? $" — +{goldEarned}g" : "";
            victoryBannerText.text =
                $"VICTORY! {waveBit}{goldBit}. Warband returned to the pews (rehire discount). Choose a perk.";
            victoryBannerText.gameObject.SetActive(true);
            Debug.Log($"[DemonKnight] Perk draft victory banner: {victoryBannerText.text}");
        }

        private void EnsurePanelWired()
        {
            if (perkDraftPanel == null)
            {
                var found = GameObject.Find("PerkDraftPanel");
                if (found == null)
                {
                    foreach (var t in Resources.FindObjectsOfTypeAll<Transform>())
                    {
                        if (t != null && t.gameObject.scene.IsValid() && t.name == "PerkDraftPanel")
                        {
                            found = t.gameObject;
                            break;
                        }
                    }
                }
                if (found != null) perkDraftPanel = found;
            }

            if (perkDraftPanel == null)
            {
                perkDraftPanel = BuildRuntimePerkPanel();
            }

            if (perkDraftPanel == null) return;

            if (choiceButtons == null || choiceButtons.Length < 3
                || choiceTitleTexts == null || choiceDescTexts == null
                || choiceButtons[0] == null)
            {
                WireChoicesFromChildren();
            }
        }

        private void WireChoicesFromChildren()
        {
            var buttons = new List<Button>();
            var titles = new List<TextMeshProUGUI>();
            var descs = new List<TextMeshProUGUI>();
            for (int i = 0; i < 3; i++)
            {
                Transform card = perkDraftPanel.transform.Find($"PerkChoice_{i}");
                if (card == null) continue;
                var title = card.Find("Title")?.GetComponent<TextMeshProUGUI>();
                var desc = card.Find("Desc")?.GetComponent<TextMeshProUGUI>();
                var select = card.Find("Select")?.GetComponent<Button>()
                             ?? card.GetComponentInChildren<Button>();
                if (title == null || desc == null || select == null) continue;
                titles.Add(title);
                descs.Add(desc);
                buttons.Add(select);
            }
            if (buttons.Count > 0)
            {
                choiceButtons = buttons.ToArray();
                choiceTitleTexts = titles.ToArray();
                choiceDescTexts = descs.ToArray();
            }
        }

        private GameObject BuildRuntimePerkPanel()
        {
            var loop = GameLoopManager.Instance;
            Transform parent = loop != null && loop.townCanvasObj != null
                ? loop.townCanvasObj.transform
                : null;
            if (parent == null)
            {
                var canvas = FindFirstObjectByType<Canvas>();
                if (canvas != null) parent = canvas.transform;
            }
            if (parent == null) return null;

            var panel = new GameObject("PerkDraftPanel", typeof(RectTransform), typeof(Image));
            panel.transform.SetParent(parent, false);
            var rt = panel.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.1f, 0.2f);
            rt.anchorMax = new Vector2(0.9f, 0.85f);
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            panel.GetComponent<Image>().color = new Color(0.06f, 0.08f, 0.12f, 0.98f);

            var titleGo = new GameObject("PerkTitle", typeof(RectTransform), typeof(TextMeshProUGUI));
            titleGo.transform.SetParent(panel.transform, false);
            var titleRt = titleGo.GetComponent<RectTransform>();
            titleRt.anchorMin = new Vector2(0.05f, 1f);
            titleRt.anchorMax = new Vector2(0.95f, 1f);
            titleRt.pivot = new Vector2(0.5f, 1f);
            titleRt.anchoredPosition = new Vector2(0f, -16f);
            titleRt.sizeDelta = new Vector2(0f, 40f);
            var titleTmp = titleGo.GetComponent<TextMeshProUGUI>();
            titleTmp.text = "WAVE CLEAR — CHOOSE A RUN PERK";
            titleTmp.fontSize = 34f;
            titleTmp.alignment = TextAlignmentOptions.Center;
            titleTmp.color = new Color(0.92f, 0.88f, 0.78f);

            choiceButtons = new Button[3];
            choiceTitleTexts = new TextMeshProUGUI[3];
            choiceDescTexts = new TextMeshProUGUI[3];
            for (int i = 0; i < 3; i++)
            {
                float x = -0.33f + i * 0.33f;
                var card = new GameObject($"PerkChoice_{i}", typeof(RectTransform), typeof(Image));
                card.transform.SetParent(panel.transform, false);
                var crt = card.GetComponent<RectTransform>();
                crt.anchorMin = new Vector2(0.08f + i * 0.3f, 0.12f);
                crt.anchorMax = new Vector2(0.08f + i * 0.3f + 0.28f, 0.78f);
                crt.offsetMin = Vector2.zero;
                crt.offsetMax = Vector2.zero;
                card.GetComponent<Image>().color = new Color(0.14f, 0.16f, 0.22f, 1f);

                choiceTitleTexts[i] = MakeTmp(card.transform, "Title", "Perk", 24f, new Vector2(0.5f, 0.82f));
                choiceDescTexts[i] = MakeTmp(card.transform, "Desc", "Description", 18f, new Vector2(0.5f, 0.45f));
                choiceButtons[i] = MakeButton(card.transform, "Select", "Choose");
            }

            panel.SetActive(false);
            return panel;
        }

        private static TextMeshProUGUI MakeTmp(Transform parent, string name, string text, float size, Vector2 anchor)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.08f, anchor.y - 0.2f);
            rt.anchorMax = new Vector2(0.92f, anchor.y + 0.15f);
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            var tmp = go.GetComponent<TextMeshProUGUI>();
            tmp.text = text;
            tmp.fontSize = size;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.color = new Color(0.92f, 0.88f, 0.78f);
            tmp.textWrappingMode = TextWrappingModes.Normal;
            return tmp;
        }

        private static Button MakeButton(Transform parent, string name, string label)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.2f, 0.06f);
            rt.anchorMax = new Vector2(0.8f, 0.22f);
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            go.GetComponent<Image>().color = new Color(0.25f, 0.35f, 0.55f);
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
            tmp.fontSize = 22f;
            tmp.color = Color.white;
            return go.GetComponent<Button>();
        }

        private void SelectPerk(RunPerkOption chosen)
        {
            unlockedPerks.Add(chosen.perkID);

            if (chosen.perkID == RunPerkID.ArthursTaxation)
            {
                var ui = FindFirstObjectByType<TownSplitUIController>();
                if (ui != null) ui.playerGold += 40;
            }

            if (perkDraftPanel != null) perkDraftPanel.SetActive(false);

            var loop = DemonKnightCompatApi.ResolveLoop() ?? FindFirstObjectByType<GameLoopManager>();
            if (loop == null)
            {
                Debug.LogError("[DemonKnight] No GameLoopManager after perk pick.");
                return;
            }

            // Each wave win shortens the next town phase (floor 180s; initial range 300–600).
            loop.townTimeLimit = Mathf.Clamp(loop.townTimeLimit - 30f, 180f, 600f);
            DemonKnightCompatApi.EnsureDisbandedAfterWave(loop);
            loop.StartTownPhase();
        }

        private void FallbackToTown()
        {
            var loop = DemonKnightCompatApi.ResolveLoop() ?? FindFirstObjectByType<GameLoopManager>();
            DemonKnightCompatApi.EnsureDisbandedAfterWave(loop);
            loop?.StartTownPhase();
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
