using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace ArthurianAutobattler
{
    [Serializable]
    public class CodexRecipeEntry
    {
        public string heroName;
        public Sprite heroPortrait;
        public string ingredientFormula;
        public string combatPerkSummary;
        public UnitType resultingType;
    }

    public class LegendCodexUI : MonoBehaviour
    {
        public GameObject codexPanel;
        public Button toggleCodexButton;
        public Transform recipeListContainer;
        public GameObject recipeRowPrefab;
        public List<CodexRecipeEntry> allRecipes = new List<CodexRecipeEntry>();

        private void Start()
        {
            if (codexPanel != null) codexPanel.SetActive(false);

            // Route through church OpenModal so Esc/X and mutual exclusion stay consistent.
            // Do NOT locally toggle activeSelf — that fought SomersetChurchUI and left tiny panels.
            toggleCodexButton?.onClick.RemoveAllListeners();
            toggleCodexButton?.onClick.AddListener(OpenViaChurchOrToggle);
        }

        private void OpenViaChurchOrToggle()
        {
            var church = FindFirstObjectByType<SomersetChurchUI>()
                         ?? (SomersetChurchUI)FindFirstObjectByType<ChurchStageUIController>();
            if (church != null && codexPanel != null)
            {
                church.OpenModal(codexPanel);
                return;
            }

            if (codexPanel == null) return;
            codexPanel.SetActive(!codexPanel.activeSelf);
            if (codexPanel.activeSelf) RefreshCodexList();
        }

        public void RefreshCodexList()
        {
            if (codexPanel == null || !codexPanel.activeSelf) return;

            if (recipeListContainer == null)
            {
                var found = codexPanel.transform.Find("RecipeList");
                if (found == null)
                {
                    var scroll = codexPanel.transform.Find("CodexScroll");
                    if (scroll != null) found = scroll.Find("RecipeList");
                }
                if (found != null) recipeListContainer = found;
            }
            if (recipeListContainer == null) return;

            // Ensure RecipeList sits in a scroll viewport (Legends options overflow).
            if (codexPanel != null)
                DemonKnightCompatApi.EnsureCodexScrollViewport(codexPanel.transform);
            DemonKnightCompatApi.ApplyTwoColumnCodexLayout(recipeListContainer);

            foreach (Transform child in recipeListContainer) Destroy(child.gameObject);

            var party = GameLoopManager.Instance != null
                ? GameLoopManager.Instance.recruitedParty
                : new List<RecruitInstance>();

            // If prefab missing, build a readable text+button row so options aren't cramped.
            foreach (var entry in allRecipes)
            {
                GameObject row;
                if (recipeRowPrefab != null)
                    row = Instantiate(recipeRowPrefab, recipeListContainer);
                else
                    row = BuildFallbackRow(recipeListContainer);

                // Prefer a tall readable cell (two-column grid).
                var le = row.GetComponent<LayoutElement>();
                if (le == null) le = row.AddComponent<LayoutElement>();
                le.minHeight = 110f;
                le.preferredHeight = 120f;
                le.minWidth = 400f;
                le.preferredWidth = 440f;

                TextMeshProUGUI[] texts = row.GetComponentsInChildren<TextMeshProUGUI>();
                Button fuseBtn = row.GetComponentInChildren<Button>();
                bool ready = CheckRecipeReady(entry.resultingType, party);

                if (texts.Length >= 1)
                {
                    texts[0].text = ready
                        ? $"<color=#7CFF9A>{entry.heroName}  [READY TO FUSE]</color>"
                        : entry.heroName;
                    texts[0].fontSize = 24f;
                    texts[0].textWrappingMode = TextWrappingModes.Normal;
                    texts[0].overflowMode = TextOverflowModes.Overflow;
                }
                if (texts.Length >= 2)
                {
                    texts[1].text = $"Recipe: {entry.ingredientFormula}";
                    texts[1].fontSize = 18f;
                    texts[1].textWrappingMode = TextWrappingModes.Normal;
                }
                if (texts.Length >= 3)
                {
                    texts[2].text = $"Perk: {entry.combatPerkSummary}";
                    texts[2].fontSize = 17f;
                    texts[2].textWrappingMode = TextWrappingModes.Normal;
                }

                if (fuseBtn != null)
                {
                    UnitType type = entry.resultingType;
                    fuseBtn.interactable = ready;
                    var label = fuseBtn.GetComponentInChildren<TextMeshProUGUI>();
                    if (label != null) label.text = ready ? "Fuse" : "Locked";
                    fuseBtn.onClick.RemoveAllListeners();
                    // Always fuse against the live recruitedParty list (exact ingredient subtract).
                    fuseBtn.onClick.AddListener(() => ExecuteFusion(type));
                }
            }
        }

        private static GameObject BuildFallbackRow(Transform parent)
        {
            var go = new GameObject("CodexRow", typeof(RectTransform), typeof(Image), typeof(LayoutElement));
            go.transform.SetParent(parent, false);
            go.GetComponent<Image>().color = new Color(0.12f, 0.14f, 0.18f, 0.95f);
            go.GetComponent<LayoutElement>().preferredHeight = 110f;

            void MakeLabel(string name, Vector2 anchorMin, Vector2 anchorMax)
            {
                var tGo = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
                tGo.transform.SetParent(go.transform, false);
                var rt = tGo.GetComponent<RectTransform>();
                rt.anchorMin = anchorMin;
                rt.anchorMax = anchorMax;
                rt.offsetMin = new Vector2(12f, 4f);
                rt.offsetMax = new Vector2(-120f, -4f);
                var tmp = tGo.GetComponent<TextMeshProUGUI>();
                tmp.fontSize = 20f;
                tmp.color = new Color(0.92f, 0.88f, 0.78f);
                tmp.textWrappingMode = TextWrappingModes.Normal;
            }

            MakeLabel("Name", new Vector2(0f, 0.55f), new Vector2(1f, 1f));
            MakeLabel("Recipe", new Vector2(0f, 0.28f), new Vector2(1f, 0.55f));
            MakeLabel("Perk", new Vector2(0f, 0f), new Vector2(1f, 0.28f));

            var btnGo = new GameObject("Fuse", typeof(RectTransform), typeof(Image), typeof(Button));
            btnGo.transform.SetParent(go.transform, false);
            var brt = btnGo.GetComponent<RectTransform>();
            brt.anchorMin = new Vector2(1f, 0.5f);
            brt.anchorMax = new Vector2(1f, 0.5f);
            brt.pivot = new Vector2(1f, 0.5f);
            brt.anchoredPosition = new Vector2(-12f, 0f);
            brt.sizeDelta = new Vector2(110f, 48f);
            btnGo.GetComponent<Image>().color = new Color(0.25f, 0.35f, 0.55f);
            var btnLabel = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
            btnLabel.transform.SetParent(btnGo.transform, false);
            var lrt = btnLabel.GetComponent<RectTransform>();
            lrt.anchorMin = Vector2.zero;
            lrt.anchorMax = Vector2.one;
            lrt.offsetMin = Vector2.zero;
            lrt.offsetMax = Vector2.zero;
            var ltmp = btnLabel.GetComponent<TextMeshProUGUI>();
            ltmp.text = "Fuse";
            ltmp.alignment = TextAlignmentOptions.Center;
            ltmp.fontSize = 20f;
            ltmp.color = Color.white;

            return go;
        }

        private bool CheckRecipeReady(UnitType t, List<RecruitInstance> p)
        {
            var f = CardFusionSystem.Instance;
            if (f == null) return false;

            switch (t)
            {
                case UnitType.Priest: return f.CanFusePriest(p);
                case UnitType.Captain: return f.CanFuseCaptain(p);
                case UnitType.KingArthur: return f.CanFuseKingArthur(p);
                case UnitType.Merlin: return f.CanFuseMerlin(p);
                case UnitType.SirLancelot: return f.CanFuseLancelot(p);
                case UnitType.MorganLeFay: return f.CanFuseMorganLeFay(p);
                case UnitType.LadyOfTheLake: return f.CanFuseLadyOfTheLake(p);
                case UnitType.SirGawain: return f.CanFuseGawain(p);
                case UnitType.Mordred: return f.CanFuseMordred(p);
                case UnitType.SirGalahad: return f.CanFuseGalahad(p);
                default: return false;
            }
        }

        private void ExecuteFusion(UnitType t)
        {
            var f = CardFusionSystem.Instance;
            var loop = GameLoopManager.Instance;
            if (f == null || loop == null) return;

            // Live party reference — Fuse* removes exact distinct ingredients then adds product.
            List<RecruitInstance> p = loop.recruitedParty;
            int before = p.Count;

            bool ok = false;
            switch (t)
            {
                case UnitType.Priest: ok = f.FusePriest(p); break;
                case UnitType.Captain: ok = f.FuseCaptain(p); break;
                case UnitType.KingArthur: ok = f.FuseKingArthur(p); break;
                case UnitType.Merlin: ok = f.FuseMerlin(p); break;
                case UnitType.SirLancelot: ok = f.FuseLancelot(p); break;
                case UnitType.MorganLeFay: ok = f.FuseMorganLeFay(p); break;
                case UnitType.LadyOfTheLake: ok = f.FuseLadyOfTheLake(p); break;
                case UnitType.SirGawain: ok = f.FuseGawain(p); break;
                case UnitType.Mordred: ok = f.FuseMordred(p); break;
                case UnitType.SirGalahad: ok = f.FuseGalahad(p); break;
            }

            if (ok)
            {
                Debug.Log($"[DemonKnight] Legend fuse {t}: party {before} → {p.Count}");
                SomersetRunes.NotifyLegendFused();
                DemonKnightCompatApi.AutosaveWarband(loop);
                RefreshCodexList();
                FindFirstObjectByType<TownSplitUIController>()?.UpdateHUD();
                var church = FindFirstObjectByType<SomersetChurchUI>()
                             ?? (SomersetChurchUI)FindFirstObjectByType<ChurchStageUIController>();
                church?.RefreshHUD();
                DemonKnightCompatApi.UpdateWarbandHudLabel(church);
            }
        }
    }
}
