using UnityEngine;
using TMPro;

namespace ArthurianAutobattler
{
    /// <summary>
    /// Tiny ★ badge on pew seats for wave-victory experience (stacks; −5g hire each).
    /// </summary>
    public static class VictoryStarBadge
    {
        public const string ChildName = "VictoryStars";

        public static void Apply(Transform seatRoot, RecruitInstance recruit)
        {
            if (seatRoot == null) return;
            int stars = recruit != null ? Mathf.Max(0, recruit.victoryStars) : 0;
            if (stars <= 0)
            {
                Clear(seatRoot);
                return;
            }

            var label = EnsureLabel(seatRoot);
            if (label == null) return;

            // Compact: ★3 or ★★★ for 1–3; ★×N when more so pews stay readable.
            if (stars <= 3)
                label.text = new string('★', stars);
            else
                label.text = $"★{stars}";
            label.gameObject.SetActive(true);
        }

        public static void Clear(Transform seatRoot)
        {
            if (seatRoot == null) return;
            var existing = seatRoot.Find(ChildName);
            if (existing != null)
                existing.gameObject.SetActive(false);
        }

        private static TextMeshProUGUI EnsureLabel(Transform seatRoot)
        {
            Transform existing = seatRoot.Find(ChildName);
            GameObject go;
            if (existing != null)
            {
                go = existing.gameObject;
            }
            else
            {
                go = new GameObject(ChildName, typeof(RectTransform), typeof(TextMeshProUGUI));
                go.transform.SetParent(seatRoot, false);
                var rt = go.GetComponent<RectTransform>();
                rt.anchorMin = new Vector2(1f, 1f);
                rt.anchorMax = new Vector2(1f, 1f);
                rt.pivot = new Vector2(1f, 1f);
                rt.anchoredPosition = new Vector2(-2f, -1f);
                rt.sizeDelta = new Vector2(36f, 16f);
            }

            var tmp = go.GetComponent<TextMeshProUGUI>();
            if (tmp == null) tmp = go.AddComponent<TextMeshProUGUI>();
            tmp.fontSize = 11f;
            tmp.fontStyle = FontStyles.Bold;
            tmp.alignment = TextAlignmentOptions.TopRight;
            tmp.color = new Color(1f, 0.85f, 0.2f, 1f);
            tmp.raycastTarget = false;
            tmp.enableWordWrapping = false;
            tmp.overflowMode = TextOverflowModes.Overflow;
            // Sit above seat art.
            go.transform.SetAsLastSibling();
            return tmp;
        }
    }
}
