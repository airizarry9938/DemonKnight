using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace ArthurianAutobattler
{
    /// <summary>
    /// Visible ★ badge on pew / paper-doll seats for wave-victory experience
    /// (stacks; −5g hire each). Uses a drawn gold star + count so TMP fonts
    /// without a ★ glyph still show experience.
    /// </summary>
    public static class VictoryStarBadge
    {
        public const string ChildName = "VictoryStars";
        private static Sprite s_starSprite;

        public static void Apply(Transform seatRoot, RecruitInstance recruit)
        {
            if (seatRoot == null) return;
            int stars = recruit != null ? Mathf.Max(0, recruit.victoryStars) : 0;
            if (stars <= 0)
            {
                Clear(seatRoot);
                return;
            }

            var root = EnsureRoot(seatRoot);
            if (root == null) return;

            var starImg = root.transform.Find("StarIcon")?.GetComponent<Image>();
            var label = root.transform.Find("StarCount")?.GetComponent<TextMeshProUGUI>();
            if (starImg != null)
            {
                starImg.sprite = StarSprite();
                starImg.color = new Color(1f, 0.84f, 0.15f, 1f);
                starImg.enabled = true;
            }
            if (label != null)
            {
                // ASCII-safe count so any TMP font shows; gold ★ in label when glyph exists.
                label.text = stars <= 1 ? "★" : $"★{stars}";
                label.color = new Color(1f, 0.92f, 0.35f, 1f);
                label.fontSize = 13f;
                label.fontStyle = FontStyles.Bold;
                label.outlineWidth = 0.2f;
                label.outlineColor = new Color(0.15f, 0.08f, 0f, 1f);
            }

            root.SetActive(true);
            root.transform.SetAsLastSibling();
        }

        public static void Clear(Transform seatRoot)
        {
            if (seatRoot == null) return;
            var existing = seatRoot.Find(ChildName);
            if (existing != null)
                existing.gameObject.SetActive(false);
        }

        private static GameObject EnsureRoot(Transform seatRoot)
        {
            Transform existing = seatRoot.Find(ChildName);
            GameObject go;
            if (existing != null)
            {
                go = existing.gameObject;
            }
            else
            {
                go = new GameObject(ChildName, typeof(RectTransform), typeof(Canvas), typeof(CanvasRenderer));
                go.transform.SetParent(seatRoot, false);
                var rt = go.GetComponent<RectTransform>();
                // Top-center, slightly above seat so paper-doll layers don't cover it.
                rt.anchorMin = new Vector2(0.5f, 1f);
                rt.anchorMax = new Vector2(0.5f, 1f);
                rt.pivot = new Vector2(0.5f, 0f);
                rt.anchoredPosition = new Vector2(0f, 2f);
                rt.sizeDelta = new Vector2(42f, 18f);

                var canvas = go.GetComponent<Canvas>();
                canvas.overrideSorting = true;
                canvas.sortingOrder = 80;

                // Gold star icon (drawn sprite — visible even if TMP lacks ★).
                var iconGo = new GameObject("StarIcon", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                iconGo.transform.SetParent(go.transform, false);
                var irt = iconGo.GetComponent<RectTransform>();
                irt.anchorMin = new Vector2(0f, 0.5f);
                irt.anchorMax = new Vector2(0f, 0.5f);
                irt.pivot = new Vector2(0f, 0.5f);
                irt.anchoredPosition = new Vector2(1f, 0f);
                irt.sizeDelta = new Vector2(14f, 14f);
                var img = iconGo.GetComponent<Image>();
                img.sprite = StarSprite();
                img.raycastTarget = false;
                img.preserveAspect = true;

                // Count label
                var txtGo = new GameObject("StarCount", typeof(RectTransform), typeof(TextMeshProUGUI));
                txtGo.transform.SetParent(go.transform, false);
                var trt = txtGo.GetComponent<RectTransform>();
                trt.anchorMin = new Vector2(0f, 0f);
                trt.anchorMax = new Vector2(1f, 1f);
                trt.offsetMin = new Vector2(14f, 0f);
                trt.offsetMax = new Vector2(-1f, 0f);
                var tmp = txtGo.GetComponent<TextMeshProUGUI>();
                tmp.alignment = TextAlignmentOptions.MidlineLeft;
                tmp.textWrappingMode = TextWrappingModes.NoWrap;
                tmp.overflowMode = TextOverflowModes.Overflow;
                tmp.raycastTarget = false;
            }

            // Always re-assert sorting / sibling so Bind refresh can't bury the badge.
            var c = go.GetComponent<Canvas>();
            if (c != null)
            {
                c.overrideSorting = true;
                c.sortingOrder = 80;
            }
            go.transform.SetAsLastSibling();
            return go;
        }

        private static Sprite StarSprite()
        {
            if (s_starSprite != null) return s_starSprite;
            const int size = 32;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            tex.filterMode = FilterMode.Bilinear;
            tex.wrapMode = TextureWrapMode.Clamp;
            var clear = new Color(0f, 0f, 0f, 0f);
            var gold = new Color(1f, 0.84f, 0.12f, 1f);
            var pixels = new Color[size * size];
            for (int i = 0; i < pixels.Length; i++) pixels[i] = clear;

            float cx = (size - 1) * 0.5f;
            float cy = (size - 1) * 0.5f;
            float outer = size * 0.48f;
            float inner = outer * 0.42f;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = x - cx;
                    float dy = y - cy;
                    float ang = Mathf.Atan2(dy, dx);
                    // 5-point star: compare radius to star edge.
                    float a = Mathf.Repeat(ang + Mathf.PI * 0.5f, Mathf.PI * 2f);
                    float sector = Mathf.PI * 2f / 5f;
                    float local = Mathf.Abs(Mathf.Repeat(a + sector * 0.5f, sector) - sector * 0.5f);
                    float edge = Mathf.Lerp(outer, inner, local / (sector * 0.5f));
                    float r = Mathf.Sqrt(dx * dx + dy * dy);
                    if (r <= edge)
                        pixels[y * size + x] = gold;
                }
            }
            tex.SetPixels(pixels);
            tex.Apply(false, false);
            s_starSprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 32f);
            s_starSprite.name = "DK_VictoryStar";
            return s_starSprite;
        }
    }
}
