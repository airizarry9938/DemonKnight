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
        public int sortHp;
        public float sortSpeed;
        public int sortAttack;
        public string loreDescription;
        public string unlockRiddle;
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

        MenuListSortMode legendsSortMode = MenuListSortMode.LowestHp;

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
            try
            {
                var img = codexPanel.GetComponent<Image>();
                if (img == null) img = codexPanel.GetComponentInChildren<Image>();
                SceneBackgroundApplier.ApplyToUiHost(
                    SceneBackgroundSlot.LegendCodex,
                    img,
                    codexPanel.GetComponent<RectTransform>());
            }
            catch { /* ignore */ }

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
            DemonKnightCompatApi.ApplyFullWidthColumnCodexLayout(recipeListContainer);

            EnsureLegendsSortBar();

            foreach (Transform child in recipeListContainer) Destroy(child.gameObject);

            var party = GameLoopManager.Instance != null
                ? GameLoopManager.Instance.recruitedParty
                : new List<RecruitInstance>();

            EnsureRecipesFromDatabase();
            if (allRecipes != null)
                allRecipes.RemoveAll(e => e == null || e.resultingType == UnitType.Merlin);
            FillRecipeSortStats();
            MenuListSortBar.SortRecipes(allRecipes, legendsSortMode);
            // Legends list only — hide party fusion preview strip (party info).
            if (fusionPreviewRoot != null)
                fusionPreviewRoot.gameObject.SetActive(false);

            foreach (var entry in allRecipes)
            {
                GameObject row;
                if (recipeRowPrefab != null)
                    row = Instantiate(recipeRowPrefab, recipeListContainer);
                else
                    row = BuildFallbackRow(recipeListContainer);

                var le = row.GetComponent<LayoutElement>();
                if (le == null) le = row.AddComponent<LayoutElement>();
                // Full-width single column — row spans the Codex scroll usable width.
                float cellW = 960f;
                float cellH = 172f;
                var grid = recipeListContainer.GetComponent<GridLayoutGroup>();
                if (grid != null)
                {
                    cellW = Mathf.Max(720f, grid.cellSize.x);
                    cellH = Mathf.Max(160f, grid.cellSize.y);
                }
                le.minHeight = cellH - 12f;
                le.preferredHeight = cellH;
                le.minWidth = cellW - 24f;
                le.preferredWidth = cellW;
                le.flexibleWidth = 1f;

                TextMeshProUGUI[] texts = row.GetComponentsInChildren<TextMeshProUGUI>();
                Button fuseBtn = row.GetComponentInChildren<Button>();
                Image[] images = row.GetComponentsInChildren<Image>();
                string legendId = LegendUnlockManager.LegendIdFor(entry.resultingType);
                bool gated = LegendUnlockManager.IsGatedLegend(entry.resultingType);
                bool unlocked = !gated || LegendUnlockManager.Ensure().IsLegendUnlocked(legendId);
                bool ready = unlocked && CheckRecipeReady(entry.resultingType, party);

                string lore = entry.loreDescription;
                if (string.IsNullOrWhiteSpace(lore))
                    lore = LegendUnlockManager.LoreFor(legendId);
                string riddle = entry.unlockRiddle;
                if (string.IsNullOrWhiteSpace(riddle))
                    riddle = LegendUnlockManager.RiddleFor(legendId);

                // Portrait: prefer first Image that isn't the row background / button.
                if (images != null)
                {
                    foreach (var img in images)
                    {
                        if (img == null) continue;
                        if (fuseBtn != null && img.gameObject == fuseBtn.gameObject) continue;
                        if (img.gameObject == row) continue;
                        if (img.GetComponentInParent<Button>() == fuseBtn && img.transform.parent == fuseBtn.transform)
                            continue;
                        if (unlocked && entry.heroPortrait != null)
                        {
                            img.sprite = entry.heroPortrait;
                            img.preserveAspect = true;
                            img.color = ready ? Color.white : new Color(1f, 1f, 1f, 0.85f);
                        }
                        else
                        {
                            // Locked silhouette #1E1E24
                            img.sprite = entry.heroPortrait;
                            img.preserveAspect = true;
                            img.color = new Color(0x1E / 255f, 0x1E / 255f, 0x24 / 255f, 1f);
                        }
                        break;
                    }
                }

                if (texts.Length >= 1)
                {
                    if (!unlocked)
                        texts[0].text = $"<color=#888899>{entry.heroName}  [LOCKED]</color>";
                    else if (ready)
                        texts[0].text = $"<color=#7CFF9A>{entry.heroName}  [READY TO FUSE]</color>";
                    else
                        texts[0].text = entry.heroName;
                    texts[0].fontSize = 22f;
                    texts[0].textWrappingMode = TextWrappingModes.Normal;
                    texts[0].overflowMode = TextOverflowModes.Overflow;
                }
                if (texts.Length >= 2)
                {
                    texts[1].text = unlocked
                        ? $"Recipe: {entry.ingredientFormula}"
                        : "Recipe: ???";
                    texts[1].fontSize = 17f;
                    texts[1].textWrappingMode = TextWrappingModes.Normal;
                    texts[1].overflowMode = TextOverflowModes.Overflow;
                }
                if (texts.Length >= 3)
                {
                    if (!unlocked)
                        texts[2].text = string.IsNullOrEmpty(riddle) ? "Locked." : riddle;
                    else
                        texts[2].text = string.IsNullOrEmpty(lore)
                            ? $"Perk: {entry.combatPerkSummary}"
                            : lore;
                    texts[2].fontSize = 16f;
                    texts[2].textWrappingMode = TextWrappingModes.Normal;
                    texts[2].overflowMode = TextOverflowModes.Overflow;
                }

                // Highlight ready / locked cards.
                var bg = row.GetComponent<Image>();
                if (bg != null)
                {
                    if (!unlocked)
                        bg.color = new Color(0x1E / 255f, 0x1E / 255f, 0x24 / 255f, 0.98f);
                    else if (ready)
                        bg.color = new Color(0.16f, 0.28f, 0.18f, 0.98f);
                    else
                        bg.color = new Color(0.12f, 0.14f, 0.18f, 0.95f);
                }

                if (fuseBtn != null)
                {
                    UnitType type = entry.resultingType;
                    fuseBtn.interactable = ready;
                    var label = fuseBtn.GetComponentInChildren<TextMeshProUGUI>();
                    if (label != null) label.text = !unlocked ? "Locked" : (ready ? "Fuse" : "Need");
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

        void EnsureLegendsSortBar()
        {
            if (codexPanel == null) return;
            // Just under Legends/Codex title — above CodexScroll (≤0.88) so it never
            // covers the first recipe row (was 0.78–0.84 and blocked click/read).
            MenuListSortBar.Ensure(
                codexPanel.transform,
                legendsSortMode,
                mode =>
                {
                    legendsSortMode = mode;
                    RefreshCodexList();
                },
                new Vector2(0.04f, 0.905f),
                new Vector2(0.96f, 0.965f));
            if (fusionPreviewRoot != null)
                fusionPreviewRoot.gameObject.SetActive(false);
            var title = codexPanel.transform.Find("CodexTitle");
            if (title != null) title.gameObject.SetActive(false);
        }

        void EnsureRecipesFromDatabase()
        {
            var db = CharacterDatabase.Load();
            if (db?.legends == null || db.legends.Count == 0) return;

            allRecipes = new List<CodexRecipeEntry>();
            foreach (var def in db.legends)
            {
                if (def == null || !def.HasFusionRecipe) continue;
                // Merlin is narrative-only — never list in playable Codex.
                if (def.unitType == UnitType.Merlin) continue;
                string id = LegendUnlockManager.LegendIdFor(def.unitType);
                allRecipes.Add(new CodexRecipeEntry
                {
                    heroName = string.IsNullOrEmpty(def.displayName) ? def.unitType.ToString() : def.displayName,
                    heroPortrait = def.portraitSprite != null ? def.portraitSprite : def.battleSprite,
                    ingredientFormula = def.FormatRecipeLine(),
                    combatPerkSummary = string.IsNullOrWhiteSpace(def.passiveDescription)
                        ? def.passiveType.ToString()
                        : def.passiveDescription,
                    resultingType = def.unitType,
                    sortHp = def.baseHP,
                    sortSpeed = def.baseAPS,
                    sortAttack = def.baseATK,
                    loreDescription = string.IsNullOrWhiteSpace(def.loreDescription)
                        ? LegendUnlockManager.LoreFor(id)
                        : def.loreDescription,
                    unlockRiddle = string.IsNullOrWhiteSpace(def.unlockRiddle)
                        ? LegendUnlockManager.RiddleFor(id)
                        : def.unlockRiddle
                });
            }
        }

        void FillRecipeSortStats()
        {
            if (allRecipes == null) return;
            var db = CharacterDatabase.Load();
            foreach (var entry in allRecipes)
            {
                if (entry == null) continue;
                if (entry.sortHp > 0 && entry.sortSpeed > 0f && entry.sortAttack > 0) continue;
                var def = db?.GetByUnitType(entry.resultingType);
                if (def != null)
                {
                    entry.sortHp = def.baseHP;
                    entry.sortSpeed = def.baseAPS;
                    entry.sortAttack = def.baseATK;
                }
            }
        }

        private static GameObject BuildFallbackRow(Transform parent)
        {
            var go = new GameObject("CodexRow", typeof(RectTransform), typeof(Image), typeof(LayoutElement));
            go.transform.SetParent(parent, false);
            go.GetComponent<Image>().color = new Color(0.12f, 0.14f, 0.18f, 0.95f);
            go.GetComponent<LayoutElement>().preferredHeight = 172f;
            go.GetComponent<LayoutElement>().flexibleWidth = 1f;

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
            if (f == null)
            {
                Debug.LogWarning("[DemonKnight] Fuse check: CardFusionSystem.Instance missing.");
                return false;
            }
            return f.CanFuseType(t, p);
        }

        private void ExecuteFusion(UnitType t)
        {
            var f = CardFusionSystem.Instance;
            var loop = GameLoopManager.Instance;
            if (f == null || loop == null) return;

            List<RecruitInstance> p = loop.recruitedParty;
            int before = p?.Count ?? 0;
            int peasants = 0;
            if (p != null)
                foreach (var r in p)
                    if (r != null && r.unitType == UnitType.Peasant) peasants++;

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
            else
            {
                Debug.LogWarning(
                    $"[DemonKnight] Fuse FAILED for {t}: partySize={before} peasantsInParty={peasants}. " +
                    "Peasants must be hired into the warband (recruitedParty), not only sitting on pews.");
            }
        }
    }

    /// <summary>
    /// Embedded in LegendCodexUI.cs so Force Sync lands sort chips with an AtomicCritical
    /// file (standalone MenuListSortBar.cs was missing on Downloads → CS0103).
    /// </summary>
    public static class MenuListSortBar
    {
        public const string RootName = "MenuSortBar";

        // Andrew order: Low HP, High HP, Low Attack, High Attack, Low Speed, High Speed.
        static readonly (MenuListSortMode mode, string label)[] Chips =
        {
            (MenuListSortMode.LowestHp, "Low HP"),
            (MenuListSortMode.HighestHp, "High HP"),
            (MenuListSortMode.LowestAttack, "Low Attack"),
            (MenuListSortMode.HighestAttack, "High Attack"),
            (MenuListSortMode.LowestSpeed, "Low Speed"),
            (MenuListSortMode.HighestSpeed, "High Speed"),
        };

        public static void Ensure(
            Transform parent,
            MenuListSortMode current,
            Action<MenuListSortMode> onChanged,
            Vector2 anchorMin,
            Vector2 anchorMax)
        {
            if (parent == null || onChanged == null) return;

            Transform root = parent.Find(RootName);
            // Rebuild when chip set/order changes (drop Default; Low-before-High Attack/Speed).
            if (root != null && !ChipsMatchRoot(root))
            {
                UnityEngine.Object.Destroy(root.gameObject);
                root = null;
            }

            if (root == null)
            {
                var go = new GameObject(RootName, typeof(RectTransform), typeof(Image), typeof(HorizontalLayoutGroup));
                go.transform.SetParent(parent, false);
                root = go.transform;
                var bg = go.GetComponent<Image>();
                bg.color = new Color(0.08f, 0.09f, 0.11f, 0.92f);
                var hlg = go.GetComponent<HorizontalLayoutGroup>();
                hlg.padding = new RectOffset(8, 8, 4, 4);
                hlg.spacing = 6f;
                hlg.childAlignment = TextAnchor.MiddleCenter;
                hlg.childForceExpandWidth = true;
                hlg.childForceExpandHeight = true;
                hlg.childControlWidth = true;
                hlg.childControlHeight = true;

                foreach (var chip in Chips)
                {
                    var btnGo = new GameObject($"Sort_{chip.mode}", typeof(RectTransform), typeof(Image), typeof(Button), typeof(LayoutElement));
                    btnGo.transform.SetParent(root, false);
                    btnGo.GetComponent<LayoutElement>().preferredHeight = 32f;
                    btnGo.GetComponent<Image>().color = new Color(0.22f, 0.24f, 0.28f, 0.98f);
                    var labelGo = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
                    labelGo.transform.SetParent(btnGo.transform, false);
                    var lrt = labelGo.GetComponent<RectTransform>();
                    lrt.anchorMin = Vector2.zero;
                    lrt.anchorMax = Vector2.one;
                    lrt.offsetMin = Vector2.zero;
                    lrt.offsetMax = Vector2.zero;
                    var tmp = labelGo.GetComponent<TextMeshProUGUI>();
                    tmp.text = chip.label;
                    tmp.fontSize = 14f;
                    tmp.alignment = TextAlignmentOptions.Center;
                    tmp.color = Color.white;
                    tmp.enableAutoSizing = true;
                    tmp.fontSizeMin = 10f;
                    tmp.fontSizeMax = 14f;
                }
            }

            var rt = root as RectTransform;
            if (rt != null)
            {
                rt.anchorMin = anchorMin;
                rt.anchorMax = anchorMax;
                rt.offsetMin = Vector2.zero;
                rt.offsetMax = Vector2.zero;
            }
            // Keep chips under the title, above scroll content (sibling draw order).
            root.SetAsLastSibling();

            for (int i = 0; i < Chips.Length; i++)
            {
                var chip = Chips[i];
                var child = root.Find($"Sort_{chip.mode}");
                if (child == null) continue;
                var btn = child.GetComponent<Button>();
                var img = child.GetComponent<Image>();
                bool active = chip.mode == current;
                if (img != null)
                    img.color = active
                        ? new Color(0.42f, 0.34f, 0.16f, 0.98f)
                        : new Color(0.22f, 0.24f, 0.28f, 0.98f);
                if (btn == null) continue;
                MenuListSortMode captured = chip.mode;
                btn.onClick.RemoveAllListeners();
                btn.onClick.AddListener(() => onChanged(captured));
            }
        }

        static bool ChipsMatchRoot(Transform root)
        {
            if (root == null || root.childCount != Chips.Length) return false;
            if (root.Find("Sort_Default") != null) return false;
            for (int i = 0; i < Chips.Length; i++)
            {
                var child = root.GetChild(i);
                if (child == null || child.name != $"Sort_{Chips[i].mode}") return false;
            }
            return true;
        }

        public static void SortRecipes(List<CodexRecipeEntry> recipes, MenuListSortMode mode)
        {
            if (recipes == null || recipes.Count < 2) return;
            if (mode == MenuListSortMode.Default) return;

            recipes.Sort((a, b) =>
            {
                if (a == null && b == null) return 0;
                if (a == null) return 1;
                if (b == null) return -1;
                int cmp = mode switch
                {
                    MenuListSortMode.HighestHp => b.sortHp.CompareTo(a.sortHp),
                    MenuListSortMode.LowestHp => a.sortHp.CompareTo(b.sortHp),
                    MenuListSortMode.HighestSpeed => b.sortSpeed.CompareTo(a.sortSpeed),
                    MenuListSortMode.LowestSpeed => a.sortSpeed.CompareTo(b.sortSpeed),
                    MenuListSortMode.HighestAttack => b.sortAttack.CompareTo(a.sortAttack),
                    MenuListSortMode.LowestAttack => a.sortAttack.CompareTo(b.sortAttack),
                    _ => 0
                };
                if (cmp != 0) return cmp;
                return string.Compare(a.heroName, b.heroName, StringComparison.OrdinalIgnoreCase);
            });
        }

        public static List<RecruitInstance> SortedParty(List<RecruitInstance> party, MenuListSortMode mode)
        {
            var list = new List<RecruitInstance>();
            if (party == null) return list;
            foreach (var r in party)
                if (r != null) list.Add(r);
            if (mode == MenuListSortMode.Default || list.Count < 2) return list;

            list.Sort((a, b) =>
            {
                int cmp = mode switch
                {
                    MenuListSortMode.HighestHp => b.maxHealth.CompareTo(a.maxHealth),
                    MenuListSortMode.LowestHp => a.maxHealth.CompareTo(b.maxHealth),
                    MenuListSortMode.HighestSpeed => b.attackSpeed.CompareTo(a.attackSpeed),
                    MenuListSortMode.LowestSpeed => a.attackSpeed.CompareTo(b.attackSpeed),
                    MenuListSortMode.HighestAttack => b.attackDamage.CompareTo(a.attackDamage),
                    MenuListSortMode.LowestAttack => a.attackDamage.CompareTo(b.attackDamage),
                    _ => 0
                };
                if (cmp != 0) return cmp;
                return string.Compare(a.characterName, b.characterName, StringComparison.OrdinalIgnoreCase);
            });
            return list;
        }
    }
}
