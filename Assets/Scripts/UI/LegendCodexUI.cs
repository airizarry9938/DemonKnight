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

        [Header("Fusion preview (runtime)")]
        public Transform fusionPreviewRoot;
        public Image fusionPreviewPortrait;
        public TextMeshProUGUI fusionPreviewStats;
        public TextMeshProUGUI fusionPreviewPassive;

        private void Start()
        {
            if (codexPanel != null) codexPanel.SetActive(false);

            toggleCodexButton?.onClick.RemoveAllListeners();
            toggleCodexButton?.onClick.AddListener(OpenViaChurchOrToggle);
        }

        private void OpenViaChurchOrToggle()
        {
            var church = FindFirstObjectByType<SomersetChurchUI>()
                         ?? (SomersetChurchUI)FindFirstObjectByType<ChurchStageUIController>();
            if (church != null)
            {
                if (codexPanel != null) church.codexModal = codexPanel;
                church.ToggleCodexModal();
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

            if (codexPanel != null)
                DemonKnightCompatApi.EnsureCodexScrollViewport(codexPanel.transform);
            DemonKnightCompatApi.ApplyTwoColumnCodexLayout(recipeListContainer);

            foreach (Transform child in recipeListContainer) Destroy(child.gameObject);

            var party = GameLoopManager.Instance != null
                ? GameLoopManager.Instance.recruitedParty
                : new List<RecruitInstance>();

            EnsureRecipesFromDatabase();
            RefreshFusionPreview(party);

            foreach (var entry in allRecipes)
            {
                GameObject row;
                if (recipeRowPrefab != null)
                    row = Instantiate(recipeRowPrefab, recipeListContainer);
                else
                    row = BuildFallbackRow(recipeListContainer);

                var le = row.GetComponent<LayoutElement>();
                if (le == null) le = row.AddComponent<LayoutElement>();
                le.minHeight = 110f;
                le.preferredHeight = 120f;
                le.minWidth = 400f;
                le.preferredWidth = 440f;

                TextMeshProUGUI[] texts = row.GetComponentsInChildren<TextMeshProUGUI>();
                Button fuseBtn = row.GetComponentInChildren<Button>();
                Image[] images = row.GetComponentsInChildren<Image>();
                bool ready = CheckRecipeReady(entry.resultingType, party);

                // Portrait: prefer first Image that isn't the row background / button.
                if (entry.heroPortrait != null && images != null)
                {
                    foreach (var img in images)
                    {
                        if (img == null) continue;
                        if (fuseBtn != null && img.gameObject == fuseBtn.gameObject) continue;
                        if (img.gameObject == row) continue;
                        if (img.GetComponentInParent<Button>() == fuseBtn && img.transform.parent == fuseBtn.transform)
                            continue;
                        img.sprite = entry.heroPortrait;
                        img.preserveAspect = true;
                        img.color = ready ? Color.white : new Color(1f, 1f, 1f, 0.55f);
                        break;
                    }
                }

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

                // Highlight ready cards.
                var bg = row.GetComponent<Image>();
                if (bg != null)
                {
                    bg.color = ready
                        ? new Color(0.16f, 0.28f, 0.18f, 0.98f)
                        : new Color(0.12f, 0.14f, 0.18f, 0.95f);
                }

                if (fuseBtn != null)
                {
                    UnitType type = entry.resultingType;
                    fuseBtn.interactable = ready;
                    var label = fuseBtn.GetComponentInChildren<TextMeshProUGUI>();
                    if (label != null) label.text = ready ? "Fuse" : "Locked";
                    fuseBtn.onClick.RemoveAllListeners();
                    fuseBtn.onClick.AddListener(() => ExecuteFusion(type));
                }
            }
        }

        /// <summary>
        /// When pew party matches a fusion recipe, show that CharacterDefinition's
        /// portrait, resulting stats, and passive before confirm.
        /// </summary>
        void RefreshFusionPreview(List<RecruitInstance> party)
        {
            var db = CharacterDatabase.Load();
            CharacterDefinition ready = null;
            if (db?.legends != null)
            {
                foreach (var def in db.legends)
                {
                    if (def != null && def.HasFusionRecipe && db.CanFuse(def, party))
                    {
                        ready = def;
                        break;
                    }
                }
            }

            EnsureFusionPreviewWidgets();
            if (fusionPreviewRoot == null) return;

            if (ready == null)
            {
                fusionPreviewRoot.gameObject.SetActive(false);
                return;
            }

            fusionPreviewRoot.gameObject.SetActive(true);
            Sprite sp = ready.portraitSprite != null ? ready.portraitSprite : ready.battleSprite;
            if (fusionPreviewPortrait != null)
            {
                fusionPreviewPortrait.sprite = sp;
                fusionPreviewPortrait.enabled = sp != null;
                fusionPreviewPortrait.preserveAspect = true;
            }
            if (fusionPreviewStats != null)
            {
                fusionPreviewStats.text =
                    $"<b>{ready.displayName}</b>\nHP {ready.baseHP} · ATK {ready.baseATK} · APS {ready.baseAPS:F1}\n" +
                    $"Recipe: {ready.FormatRecipeLine()}";
            }
            if (fusionPreviewPassive != null)
            {
                fusionPreviewPassive.text = string.IsNullOrWhiteSpace(ready.passiveDescription)
                    ? ready.passiveType.ToString()
                    : ready.passiveDescription;
            }
        }

        void EnsureFusionPreviewWidgets()
        {
            if (codexPanel == null) return;
            if (fusionPreviewRoot != null) return;

            var go = new GameObject("FusionPreview", typeof(RectTransform), typeof(Image));
            go.transform.SetParent(codexPanel.transform, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(1f, 1f);
            rt.pivot = new Vector2(0.5f, 1f);
            rt.anchoredPosition = new Vector2(0f, -8f);
            rt.sizeDelta = new Vector2(-24f, 96f);
            go.GetComponent<Image>().color = new Color(0.14f, 0.22f, 0.16f, 0.95f);
            fusionPreviewRoot = go.transform;

            var portraitGo = new GameObject("Portrait", typeof(RectTransform), typeof(Image));
            portraitGo.transform.SetParent(go.transform, false);
            var prt = portraitGo.GetComponent<RectTransform>();
            prt.anchorMin = new Vector2(0f, 0.5f);
            prt.anchorMax = new Vector2(0f, 0.5f);
            prt.pivot = new Vector2(0f, 0.5f);
            prt.anchoredPosition = new Vector2(12f, 0f);
            prt.sizeDelta = new Vector2(72f, 72f);
            fusionPreviewPortrait = portraitGo.GetComponent<Image>();
            fusionPreviewPortrait.preserveAspect = true;

            var statsGo = new GameObject("Stats", typeof(RectTransform), typeof(TextMeshProUGUI));
            statsGo.transform.SetParent(go.transform, false);
            var srt = statsGo.GetComponent<RectTransform>();
            srt.anchorMin = new Vector2(0f, 0.35f);
            srt.anchorMax = new Vector2(1f, 1f);
            srt.offsetMin = new Vector2(96f, 4f);
            srt.offsetMax = new Vector2(-12f, -6f);
            fusionPreviewStats = statsGo.GetComponent<TextMeshProUGUI>();
            fusionPreviewStats.fontSize = 16f;
            fusionPreviewStats.color = new Color(0.95f, 0.92f, 0.82f);

            var passGo = new GameObject("Passive", typeof(RectTransform), typeof(TextMeshProUGUI));
            passGo.transform.SetParent(go.transform, false);
            var prt2 = passGo.GetComponent<RectTransform>();
            prt2.anchorMin = new Vector2(0f, 0f);
            prt2.anchorMax = new Vector2(1f, 0.4f);
            prt2.offsetMin = new Vector2(96f, 6f);
            prt2.offsetMax = new Vector2(-12f, -2f);
            fusionPreviewPassive = passGo.GetComponent<TextMeshProUGUI>();
            fusionPreviewPassive.fontSize = 15f;
            fusionPreviewPassive.color = new Color(0.75f, 0.9f, 0.75f);
        }

        void EnsureRecipesFromDatabase()
        {
            var db = CharacterDatabase.Load();
            if (db?.legends == null || db.legends.Count == 0) return;

            allRecipes = new List<CodexRecipeEntry>();
            foreach (var def in db.legends)
            {
                if (def == null || !def.HasFusionRecipe) continue;
                allRecipes.Add(new CodexRecipeEntry
                {
                    heroName = string.IsNullOrEmpty(def.displayName) ? def.unitType.ToString() : def.displayName,
                    heroPortrait = def.portraitSprite != null ? def.portraitSprite : def.battleSprite,
                    ingredientFormula = def.FormatRecipeLine(),
                    combatPerkSummary = string.IsNullOrWhiteSpace(def.passiveDescription)
                        ? def.passiveType.ToString()
                        : def.passiveDescription,
                    resultingType = def.unitType
                });
            }
        }

        private static GameObject BuildFallbackRow(Transform parent)
        {
            var go = new GameObject("CodexRow", typeof(RectTransform), typeof(Image), typeof(LayoutElement));
            go.transform.SetParent(parent, false);
            go.GetComponent<Image>().color = new Color(0.12f, 0.14f, 0.18f, 0.95f);
            go.GetComponent<LayoutElement>().preferredHeight = 110f;

            var portraitGo = new GameObject("Portrait", typeof(RectTransform), typeof(Image));
            portraitGo.transform.SetParent(go.transform, false);
            var prt = portraitGo.GetComponent<RectTransform>();
            prt.anchorMin = new Vector2(0f, 0.5f);
            prt.anchorMax = new Vector2(0f, 0.5f);
            prt.pivot = new Vector2(0f, 0.5f);
            prt.anchoredPosition = new Vector2(10f, 0f);
            prt.sizeDelta = new Vector2(64f, 64f);
            portraitGo.GetComponent<Image>().color = new Color(0.2f, 0.22f, 0.28f);

            void MakeLabel(string name, Vector2 anchorMin, Vector2 anchorMax)
            {
                var tGo = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
                tGo.transform.SetParent(go.transform, false);
                var rt = tGo.GetComponent<RectTransform>();
                rt.anchorMin = anchorMin;
                rt.anchorMax = anchorMax;
                rt.offsetMin = new Vector2(84f, 4f);
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
            return f.CanFuseType(t, p);
        }

        private void ExecuteFusion(UnitType t)
        {
            var f = CardFusionSystem.Instance;
            var loop = GameLoopManager.Instance;
            if (f == null || loop == null) return;

            List<RecruitInstance> p = loop.recruitedParty;
            int before = p.Count;
            bool ok = f.FuseType(t, p);

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
