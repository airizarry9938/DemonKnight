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

        /// <summary>Balance V2 Taxation: immediate gold on pick.</summary>
        public const int TaxationImmediateGold = 75;
        /// <summary>Balance V2 Taxation: bonus gold on each wave win while owned.</summary>
        public const int TaxationPerWaveWin = 15;

        private readonly List<RunPerkOption> allPerks = new List<RunPerkOption>
        {
            new RunPerkOption
            {
                perkID = RunPerkID.PitchforkMilitia,
                title = "Pitchfork Militia",
                description = "In battle: all Peasants gain +10 Max HP and +2 Attack (pew dossier stays base)."
            },
            new RunPerkOption
            {
                perkID = RunPerkID.ArthursTaxation,
                title = "Arthur's Taxation",
                description = "Immediately gain +75 Royal Gold, then +15g on every wave win."
            },
            new RunPerkOption
            {
                perkID = RunPerkID.RoyalScoutingDecree,
                title = "Royal Scouting Decree",
                description = "Tier 1 Basic Stats revealed free on all current and future townsfolk."
            },
            new RunPerkOption
            {
                perkID = RunPerkID.HasteOfCamelot,
                title = "Haste of Camelot",
                description = "In battle: Warriors and Knights gain +0.25 APS (cap 2.0)."
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
            unlockedPerks ??= new HashSet<RunPerkID>();
            unlockedPerks.Clear();
            SuppressAllPerkUi();
        }

        private void OnEnable() => SuppressAllPerkUi();

        private void Start() => SuppressAllPerkUi();

        /// <summary>
        /// Perks offline till further notice — hide every PerkDraftPanel / choice card /
        /// HUD perk button even when the manager reference is null (Downloads scenes).
        /// </summary>
        public static void SuppressAllPerkUi()
        {
            foreach (var t in Resources.FindObjectsOfTypeAll<Transform>())
            {
                if (t == null || !t.gameObject.scene.IsValid()) continue;
                string n = t.gameObject.name ?? "";
                bool underPerkParent = t.parent != null
                    && (t.parent.name ?? "").IndexOf("Perk", StringComparison.OrdinalIgnoreCase) >= 0;
                bool isPerk =
                    n.Equals("PerkDraftPanel", StringComparison.OrdinalIgnoreCase)
                    || n.StartsWith("PerkChoice_", StringComparison.OrdinalIgnoreCase)
                    || n.Equals("PerkTitle", StringComparison.OrdinalIgnoreCase)
                    || (n.Equals("VictoryBanner", StringComparison.OrdinalIgnoreCase) && underPerkParent)
                    || n.IndexOf("PerkDraft", StringComparison.OrdinalIgnoreCase) >= 0
                    || n.Equals("PerkButton", StringComparison.OrdinalIgnoreCase)
                    || n.Equals("PerksButton", StringComparison.OrdinalIgnoreCase)
                    || n.Equals("PerksToggle", StringComparison.OrdinalIgnoreCase)
                    || n.Equals("OpenPerks", StringComparison.OrdinalIgnoreCase);
                if (!isPerk) continue;
                if (t.gameObject.activeSelf)
                    t.gameObject.SetActive(false);
            }

            var managers = UnityEngine.Object.FindObjectsByType<PerkDraftManager>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            if (managers == null) return;
            foreach (var p in managers)
            {
                if (p == null) continue;
                p.unlockedPerks ??= new HashSet<RunPerkID>();
                p.unlockedPerks.Clear();
                if (p.perkDraftPanel != null)
                    p.perkDraftPanel.SetActive(false);
            }
        }

        public void OpenPerkDraft() => OpenPerkDraft(0, 0);

        /// <summary>
        /// Perks removed pending redesign — always skip the draft and return to town.
        /// </summary>
        public void OpenPerkDraft(int waveCleared, int goldEarned)
        {
            SuppressAllPerkUi();
            var loopEarly = DemonKnightCompatApi.ResolveLoop() ?? FindFirstObjectByType<GameLoopManager>();
            DemonKnightCompatApi.EnsureDisbandedAfterWave(loopEarly, null, true);
            unlockedPerks?.Clear();
            Debug.LogWarning(
                $"[DemonKnight] Perk draft disabled (wave {waveCleared}, +{goldEarned}g) — returning to town.");
            FallbackToTown();
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
            // Perks offline — never build a runtime draft panel; only bind+hide scene copy.
            if (perkDraftPanel == null)
            {
                foreach (var t in Resources.FindObjectsOfTypeAll<Transform>())
                {
                    if (t != null && t.gameObject.scene.IsValid()
                        && t.name.Equals("PerkDraftPanel", StringComparison.OrdinalIgnoreCase))
                    {
                        perkDraftPanel = t.gameObject;
                        break;
                    }
                }
            }
            if (perkDraftPanel != null) perkDraftPanel.SetActive(false);
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
                if (ui != null) ui.playerGold += TaxationImmediateGold;
            }

            var loop = DemonKnightCompatApi.ResolveLoop() ?? FindFirstObjectByType<GameLoopManager>();

            // Scouting must unlock Tier 1 on the CURRENT pew pool (not only next GenerateNewRunTown).
            if (chosen.perkID == RunPerkID.RoyalScoutingDecree && loop?.currentTownPool != null)
            {
                foreach (var r in loop.currentTownPool)
                {
                    if (r == null) continue;
                    if (r.intelTierUnlocked < 1) r.intelTierUnlocked = 1;
                }
                Debug.Log("[DemonKnight] Royal Scouting: Tier 1 unlocked on current town pool.");
            }

            if (perkDraftPanel != null) perkDraftPanel.SetActive(false);

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
