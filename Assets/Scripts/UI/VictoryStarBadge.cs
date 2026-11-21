using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace ArthurianAutobattler
{
    /// <summary>
    /// Visible ★ badge on pew / paper-doll seats for wave-victory experience
    /// (stacks; −5g hire each). Drawn gold star + count so TMP fonts without a
    /// ★ glyph still show experience. Anchored inside the seat so pew overlap /
    /// masks cannot clip it off the top edge.
    /// </summary>
    public static class VictoryStarBadge
    {
        public const string ChildName = "VictoryStars";
        private static Sprite s_starSprite;
        private static Sprite s_pillSprite;

        public static void Apply(Transform seatRoot, RecruitInstance recruit)
        {
            if (seatRoot == null) return;
            int stars = recruit != null ? recruit.EffectiveVeteranRank : 0;
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
                starImg.gameObject.SetActive(true);
            }
            if (label != null)
            {
                // ASCII digits always render; ★ when the TMP font has the glyph.
                label.text = stars <= 1 ? "★" : $"★×{stars}";
                label.color = new Color(1f, 0.95f, 0.45f, 1f);
                label.fontSize = 14f;
                label.fontStyle = FontStyles.Bold;
                label.outlineWidth = 0.25f;
                label.outlineColor = new Color(0.12f, 0.06f, 0f, 1f);
                label.enabled = true;
                label.gameObject.SetActive(true);
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
                // Top-right INSIDE the seat — front pews / masks used to clip the
                // old above-seat placement, so veterans looked starless.
                rt.anchorMin = new Vector2(1f, 1f);
                rt.anchorMax = new Vector2(1f, 1f);
                rt.pivot = new Vector2(1f, 1f);
                rt.anchoredPosition = new Vector2(-2f, -2f);
                rt.sizeDelta = new Vector2(48f, 22f);

                var canvas = go.GetComponent<Canvas>();
                canvas.overrideSorting = true;
                canvas.sortingOrder = 520;

                // Dark pill so the gold star reads on busy paper-doll art.
                var bgGo = new GameObject("StarBg", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                bgGo.transform.SetParent(go.transform, false);
                var bgRt = bgGo.GetComponent<RectTransform>();
                bgRt.anchorMin = Vector2.zero;
                bgRt.anchorMax = Vector2.one;
                bgRt.offsetMin = Vector2.zero;
                bgRt.offsetMax = Vector2.zero;
                var bg = bgGo.GetComponent<Image>();
                bg.sprite = PillSprite();
                bg.color = new Color(0.08f, 0.05f, 0.02f, 0.82f);
                bg.raycastTarget = false;
                bg.type = Image.Type.Sliced;

                // Gold star icon (drawn sprite — visible even if TMP lacks ★).
                var iconGo = new GameObject("StarIcon", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                iconGo.transform.SetParent(go.transform, false);
                var irt = iconGo.GetComponent<RectTransform>();
                irt.anchorMin = new Vector2(0f, 0.5f);
                irt.anchorMax = new Vector2(0f, 0.5f);
                irt.pivot = new Vector2(0f, 0.5f);
                irt.anchoredPosition = new Vector2(3f, 0f);
                irt.sizeDelta = new Vector2(16f, 16f);
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
                trt.offsetMin = new Vector2(18f, 0f);
                trt.offsetMax = new Vector2(-2f, 0f);
                var tmp = txtGo.GetComponent<TextMeshProUGUI>();
                tmp.alignment = TextAlignmentOptions.MidlineLeft;
                tmp.textWrappingMode = TextWrappingModes.NoWrap;
                tmp.overflowMode = TextOverflowModes.Overflow;
                tmp.raycastTarget = false;
            }

            // Migrate legacy above-seat badges (anchor top-center, outside bounds).
            var migrate = go.GetComponent<RectTransform>();
            if (migrate != null)
            {
                migrate.anchorMin = new Vector2(1f, 1f);
                migrate.anchorMax = new Vector2(1f, 1f);
                migrate.pivot = new Vector2(1f, 1f);
                migrate.anchoredPosition = new Vector2(-2f, -2f);
                migrate.sizeDelta = new Vector2(48f, 22f);
            }

            var c = go.GetComponent<Canvas>();
            if (c != null)
            {
                c.overrideSorting = true;
                c.sortingOrder = 520;
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

        private static Sprite PillSprite()
        {
            if (s_pillSprite != null) return s_pillSprite;
            const int size = 16;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            tex.filterMode = FilterMode.Bilinear;
            tex.wrapMode = TextureWrapMode.Clamp;
            var pixels = new Color[size * size];
            float radius = (size - 1) * 0.5f;
            float cx = radius, cy = radius;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = x - cx;
                    float dy = y - cy;
                    float d = Mathf.Sqrt(dx * dx + dy * dy);
                    pixels[y * size + x] = d <= radius
                        ? Color.white
                        : new Color(1f, 1f, 1f, 0f);
                }
            }
            tex.SetPixels(pixels);
            tex.Apply(false, false);
            // 4px border so Image.Type.Sliced can stretch the middle.
            s_pillSprite = Sprite.Create(
                tex,
                new Rect(0, 0, size, size),
                new Vector2(0.5f, 0.5f),
                16f,
                0,
                SpriteMeshType.FullRect,
                new Vector4(4f, 4f, 4f, 4f));
            s_pillSprite.name = "DK_VictoryStarPill";
            return s_pillSprite;
        }
    }
}
