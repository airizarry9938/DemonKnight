using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace ArthurianAutobattler
{
    /// <summary>
    /// World-space battle token: colored block (fallback art) + HP bar + name.
    /// Builds missing UI at runtime so a stale prefab still shows health.
    /// </summary>
    public class UnitVisualActor : MonoBehaviour
    {
        public SpriteRenderer spriteRenderer;
        public SpriteRenderer blockRenderer;
        public Slider healthBar;
        public TextMeshProUGUI nameLabel;
        public TextMeshProUGUI hpLabel;
        public CombatUnit boundUnit;

        private Vector3 homePosition;
        private Image healthFill;
        private bool deathAnimPlayed;

        public void Initialize(CombatUnit unit, Sprite artwork, Vector3 spawnPos, bool flipX, float visualScale = 1f)
        {
            boundUnit = unit;
            homePosition = spawnPos;
            transform.position = spawnPos;
            float scale = Mathf.Max(0.1f, visualScale);
            transform.localScale = new Vector3(scale, scale, 1f);

            EnsureVisualChrome(unit, flipX);

            if (spriteRenderer != null)
            {
                spriteRenderer.sprite = artwork;
                spriteRenderer.flipX = flipX;
                spriteRenderer.enabled = artwork != null;
            }

            if (blockRenderer != null)
            {
                // Solid color block when no portrait sprite (heroes/demons/bosses still readable).
                blockRenderer.enabled = artwork == null;
                blockRenderer.color = ColorForUnit(unit);
                blockRenderer.flipX = flipX;
            }

            ApplyYSorting();

            if (healthBar != null && unit != null)
            {
                healthBar.minValue = 0;
                healthBar.maxValue = Mathf.Max(1, unit.maxHealth);
                healthBar.value = Mathf.Max(0, unit.currentHealth);
            }

            RefreshHpText();
            if (nameLabel != null && unit != null)
                nameLabel.text = string.IsNullOrEmpty(unit.unitName) ? unit.unitType.ToString() : unit.unitName;

            // Phase 4 — battle token Idle/Attack/Hit/Death juice.
            var anim = ModularSpriteAnimator.EnsureOn(gameObject);
            anim?.ConfigureBattleActor(this);
        }

        private void Update()
        {
            if (boundUnit == null) return;
            ApplyYSorting();
            if (healthBar != null)
            {
                healthBar.maxValue = Mathf.Max(1, boundUnit.maxHealth);
                healthBar.value = Mathf.Max(0, boundUnit.currentHealth);
                if (healthFill != null)
                {
                    float pct = boundUnit.maxHealth > 0
                        ? (float)boundUnit.currentHealth / boundUnit.maxHealth
                        : 0f;
                    healthFill.color = Color.Lerp(new Color(0.75f, 0.15f, 0.12f), new Color(0.25f, 0.75f, 0.3f), pct);
                }
            }
            RefreshHpText();
            if (!boundUnit.IsAlive)
            {
                if (!deathAnimPlayed)
                {
                    deathAnimPlayed = true;
                    var anim = GetComponent<ModularSpriteAnimator>();
                    anim?.Play(ModularSpriteAnimator.AnimState.Death);
                    Destroy(gameObject, 0.4f);
                }
            }
        }

        /// <summary>
        /// Front units (lower Y) draw above back units so isometric rows overlap naturally.
        /// </summary>
        public void ApplyYSorting()
        {
            int order = Mathf.RoundToInt(-transform.position.y * 100f);
            if (spriteRenderer != null) spriteRenderer.sortingOrder = order;
            if (blockRenderer != null) blockRenderer.sortingOrder = order - 1;
        }

        private void RefreshHpText()
        {
            if (hpLabel == null || boundUnit == null) return;
            hpLabel.text = $"{Mathf.Max(0, boundUnit.currentHealth)}/{Mathf.Max(1, boundUnit.maxHealth)}";
        }

        public void PlayAttackLunge(Vector3 targetPos)
        {
            if (!gameObject.activeInHierarchy) return;
            var anim = GetComponent<ModularSpriteAnimator>();
            if (anim != null)
            {
                anim.Play(ModularSpriteAnimator.AnimState.Attack);
                return;
            }
            StopAllCoroutines();
            StartCoroutine(LungeRoutine(targetPos));
        }

        public void PlayHitFlash()
        {
            if (!gameObject.activeInHierarchy) return;
            var anim = GetComponent<ModularSpriteAnimator>();
            if (anim != null)
            {
                anim.Play(ModularSpriteAnimator.AnimState.Hit);
                return;
            }
            StartCoroutine(FlashRedRoutine());
        }

        private IEnumerator LungeRoutine(Vector3 targetPos)
        {
            Vector3 strike = Vector3.Lerp(homePosition, targetPos, 0.45f);
            float t = 0f;
            while (t < 0.12f)
            {
                transform.position = Vector3.Lerp(homePosition, strike, t / 0.12f);
                t += Time.deltaTime;
                yield return null;
            }

            t = 0f;
            while (t < 0.12f)
            {
                transform.position = Vector3.Lerp(strike, homePosition, t / 0.12f);
                t += Time.deltaTime;
                yield return null;
            }

            transform.position = homePosition;
        }

        private IEnumerator FlashRedRoutine()
        {
            var targets = new[] { spriteRenderer, blockRenderer };
            foreach (var sr in targets)
            {
                if (sr != null) sr.color = Color.red;
            }
            yield return new WaitForSeconds(0.1f);
            foreach (var sr in targets)
            {
                if (sr == null) continue;
                sr.color = sr == blockRenderer && boundUnit != null ? ColorForUnit(boundUnit) : Color.white;
            }
        }

        private void EnsureVisualChrome(CombatUnit unit, bool flipX)
        {
            if (spriteRenderer == null)
            {
                spriteRenderer = GetComponent<SpriteRenderer>();
                if (spriteRenderer == null) spriteRenderer = gameObject.AddComponent<SpriteRenderer>();
            }

            if (blockRenderer == null)
            {
                var blockGo = transform.Find("ColorBlock");
                if (blockGo == null)
                {
                    blockGo = new GameObject("ColorBlock").transform;
                    blockGo.SetParent(transform, false);
                    blockGo.localPosition = Vector3.zero;
                    blockGo.localScale = new Vector3(0.85f, 1.1f, 1f);
                }
                blockRenderer = blockGo.GetComponent<SpriteRenderer>();
                if (blockRenderer == null) blockRenderer = blockGo.gameObject.AddComponent<SpriteRenderer>();
                if (blockRenderer.sprite == null)
                    blockRenderer.sprite = SoftQuadSprite();
            }

            Transform canvasTf = transform.Find("HPCanvas");
            Canvas canvas;
            if (canvasTf == null)
            {
                var canvasGo = new GameObject("HPCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
                canvasGo.transform.SetParent(transform, false);
                canvasGo.transform.localPosition = new Vector3(0f, 0.95f, 0f);
                canvasGo.transform.localScale = Vector3.one * 0.012f;
                canvas = canvasGo.GetComponent<Canvas>();
                canvas.renderMode = RenderMode.WorldSpace;
                canvas.sortingOrder = 20;
                canvasTf = canvasGo.transform;
            }
            else canvas = canvasTf.GetComponent<Canvas>();

            if (healthBar == null)
            {
                var barTf = canvasTf.Find("HealthBar");
                if (barTf == null)
                {
                    var barGo = new GameObject("HealthBar", typeof(RectTransform), typeof(Slider));
                    barGo.transform.SetParent(canvasTf, false);
                    var brt = barGo.GetComponent<RectTransform>();
                    brt.sizeDelta = new Vector2(110f, 14f);
                    brt.anchoredPosition = new Vector2(0f, 8f);

                    var bg = new GameObject("Background", typeof(RectTransform), typeof(Image));
                    bg.transform.SetParent(barGo.transform, false);
                    Stretch(bg.GetComponent<RectTransform>());
                    bg.GetComponent<Image>().color = new Color(0.1f, 0.1f, 0.1f, 0.85f);

                    var fillArea = new GameObject("Fill Area", typeof(RectTransform));
                    fillArea.transform.SetParent(barGo.transform, false);
                    Stretch(fillArea.GetComponent<RectTransform>(), 2f, 2f);

                    var fill = new GameObject("Fill", typeof(RectTransform), typeof(Image));
                    fill.transform.SetParent(fillArea.transform, false);
                    Stretch(fill.GetComponent<RectTransform>());
                    healthFill = fill.GetComponent<Image>();
                    healthFill.color = new Color(0.25f, 0.75f, 0.3f);

                    var slider = barGo.GetComponent<Slider>();
                    slider.fillRect = fill.GetComponent<RectTransform>();
                    slider.targetGraphic = healthFill;
                    slider.direction = Slider.Direction.LeftToRight;
                    slider.minValue = 0f;
                    slider.maxValue = 100f;
                    healthBar = slider;
                }
                else
                {
                    healthBar = barTf.GetComponent<Slider>();
                    if (healthBar != null && healthBar.fillRect != null)
                        healthFill = healthBar.fillRect.GetComponent<Image>();
                }
            }

            if (hpLabel == null)
            {
                var hpTf = canvasTf.Find("HpText");
                if (hpTf == null)
                {
                    var hpGo = new GameObject("HpText", typeof(RectTransform), typeof(TextMeshProUGUI));
                    hpGo.transform.SetParent(canvasTf, false);
                    var hrt = hpGo.GetComponent<RectTransform>();
                    hrt.sizeDelta = new Vector2(120f, 24f);
                    hrt.anchoredPosition = new Vector2(0f, -10f);
                    hpLabel = hpGo.GetComponent<TextMeshProUGUI>();
                    hpLabel.fontSize = 16f;
                    hpLabel.alignment = TextAlignmentOptions.Center;
                    hpLabel.color = Color.white;
                }
                else hpLabel = hpTf.GetComponent<TextMeshProUGUI>();
            }

            if (nameLabel == null)
            {
                var nTf = canvasTf.Find("NameText");
                if (nTf == null)
                {
                    var nGo = new GameObject("NameText", typeof(RectTransform), typeof(TextMeshProUGUI));
                    nGo.transform.SetParent(canvasTf, false);
                    var nrt = nGo.GetComponent<RectTransform>();
                    nrt.sizeDelta = new Vector2(140f, 22f);
                    nrt.anchoredPosition = new Vector2(0f, 28f);
                    nameLabel = nGo.GetComponent<TextMeshProUGUI>();
                    nameLabel.fontSize = 15f;
                    nameLabel.alignment = TextAlignmentOptions.Center;
                    nameLabel.color = new Color(0.95f, 0.9f, 0.8f);
                }
                else nameLabel = nTf.GetComponent<TextMeshProUGUI>();
            }
        }

        private static void Stretch(RectTransform rt, float padX = 0f, float padY = 0f)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(padX, padY);
            rt.offsetMax = new Vector2(-padX, -padY);
        }

        private static Sprite _softQuad;
        private static Sprite SoftQuadSprite()
        {
            if (_softQuad != null) return _softQuad;
            var tex = new Texture2D(8, 8, TextureFormat.RGBA32, false);
            var c = Color.white;
            for (int y = 0; y < 8; y++)
                for (int x = 0; x < 8; x++)
                    tex.SetPixel(x, y, c);
            tex.Apply();
            _softQuad = Sprite.Create(tex, new Rect(0, 0, 8, 8), new Vector2(0.5f, 0.5f), 8f);
            return _softQuad;
        }

        public static Color ColorForUnit(CombatUnit u)
        {
            if (u == null) return Color.gray;
            if (u.isDemon)
            {
                string n = u.unitType.ToString();
                if (n.StartsWith("Boss")) return new Color(0.55f, 0.12f, 0.55f);
                if (u.unitType == UnitType.DemonDreadLord) return new Color(0.45f, 0.08f, 0.1f);
                if (u.unitType == UnitType.DemonHellhound) return new Color(0.7f, 0.35f, 0.1f);
                if (u.isSirenEnchantress) return new Color(0.85f, 0.35f, 0.65f);
                return new Color(0.55f, 0.15f, 0.15f);
            }

            if (RecruitInstance.IsLegendUnit(u.unitType))
                return new Color(0.85f, 0.7f, 0.25f);
            return u.unitType switch
            {
                UnitType.Knight => new Color(0.35f, 0.45f, 0.7f),
                UnitType.Warrior => new Color(0.55f, 0.35f, 0.25f),
                UnitType.Peasant => new Color(0.35f, 0.5f, 0.3f),
                _ => new Color(0.4f, 0.45f, 0.5f)
            };
        }
    }
}
