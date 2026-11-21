using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace ArthurianAutobattler
{
    /// <summary>Shared sort modes for Legends recipes and Warband roster lists.</summary>
    public enum MenuListSortMode
    {
        Default = 0,
        HighestHp = 1,
        LowestHp = 2,
        HighestSpeed = 3,
        LowestSpeed = 4,
    }

    /// <summary>
    /// Runtime sort chip row: Highest HP / Lowest HP / Highest SPD / Lowest SPD / Default.
    /// Used by Legends (codex) and Warband (Camelot Crowd) menus.
    /// </summary>
    public static class MenuListSortBar
    {
        public const string RootName = "MenuSortBar";

        static readonly (MenuListSortMode mode, string label)[] Chips =
        {
            (MenuListSortMode.Default, "Default"),
            (MenuListSortMode.HighestHp, "Highest HP"),
            (MenuListSortMode.LowestHp, "Lowest HP"),
            (MenuListSortMode.HighestSpeed, "Highest SPD"),
            (MenuListSortMode.LowestSpeed, "Lowest SPD"),
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
                    tmp.fontSize = 15f;
                    tmp.alignment = TextAlignmentOptions.Center;
                    tmp.color = Color.white;
                    tmp.enableAutoSizing = true;
                    tmp.fontSizeMin = 11f;
                    tmp.fontSizeMax = 15f;
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

            // Wire + paint active chip.
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
                    _ => 0
                };
                if (cmp != 0) return cmp;
                return string.Compare(a.characterName, b.characterName, StringComparison.OrdinalIgnoreCase);
            });
            return list;
        }
    }
}
