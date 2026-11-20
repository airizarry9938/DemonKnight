using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using TMPro;

namespace ArthurianAutobattler
{
    /// <summary>
    /// Top HUD boss portrait next to the Night/Wave label + hover tooltip
    /// (scaled HP/ATK + signature trait from CharacterDefinition).
    /// </summary>
    public class WaveBossHudView : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        public Image bossPortrait;
        public TextMeshProUGUI tooltipLabel;
        public GameObject tooltipRoot;

        CharacterDefinition _bound;
        float _mult = 1f;

        public static WaveBossHudView EnsureNearNightText(TextMeshProUGUI nightText)
        {
            if (nightText == null) return null;
            Transform hud = nightText.transform.parent;
            if (hud == null) return null;

            var existing = hud.GetComponentInChildren<WaveBossHudView>(true);
            if (existing != null) return existing;

            var go = new GameObject("WaveBossPortrait", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(WaveBossHudView));
            go.transform.SetParent(hud, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(48f, 48f);

            // Sit just to the left of NightText when possible.
            var nightRt = nightText.rectTransform;
            rt.anchoredPosition = nightRt.anchoredPosition + new Vector2(-nightRt.sizeDelta.x * 0.5f - 32f, 0f);

            var view = go.GetComponent<WaveBossHudView>();
            view.bossPortrait = go.GetComponent<Image>();
            view.bossPortrait.preserveAspect = true;
            view.bossPortrait.raycastTarget = true;
            view.BuildTooltip(go.transform);
            return view;
        }

        void BuildTooltip(Transform parent)
        {
            var tipGo = new GameObject("BossTooltip", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            tipGo.transform.SetParent(parent, false);
            var tipRt = tipGo.GetComponent<RectTransform>();
            tipRt.anchorMin = new Vector2(0.5f, 0f);
            tipRt.anchorMax = new Vector2(0.5f, 0f);
            tipRt.pivot = new Vector2(0.5f, 1f);
            tipRt.anchoredPosition = new Vector2(0f, -8f);
            tipRt.sizeDelta = new Vector2(260f, 90f);
            tipGo.GetComponent<Image>().color = new Color(0.08f, 0.09f, 0.12f, 0.96f);

            var textGo = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI));
            textGo.transform.SetParent(tipGo.transform, false);
            var trt = textGo.GetComponent<RectTransform>();
            trt.anchorMin = Vector2.zero;
            trt.anchorMax = Vector2.one;
            trt.offsetMin = new Vector2(8f, 6f);
            trt.offsetMax = new Vector2(-8f, -6f);
            tooltipLabel = textGo.GetComponent<TextMeshProUGUI>();
            tooltipLabel.fontSize = 15f;
            tooltipLabel.color = new Color(0.95f, 0.9f, 0.8f);
            tooltipLabel.textWrappingMode = TextWrappingModes.Normal;
            tooltipRoot = tipGo;
            tipGo.SetActive(false);
        }

        public void Refresh(int wave)
        {
            _bound = GameLoopManager.GetWaveBossDefinition(wave);
            float mult = 1f + (Mathf.Max(1, wave) - 1) * 0.12f;
            _mult = mult;

            if (bossPortrait == null) bossPortrait = GetComponent<Image>();
            if (_bound == null)
            {
                if (bossPortrait != null)
                {
                    bossPortrait.enabled = false;
                    bossPortrait.sprite = null;
                }
                HideTooltip();
                return;
            }

            Sprite sp = _bound.portraitSprite != null ? _bound.portraitSprite : _bound.battleSprite;
            if (bossPortrait != null)
            {
                bossPortrait.sprite = sp;
                bossPortrait.enabled = sp != null;
                bossPortrait.color = Color.white;
            }
            if (tooltipLabel != null)
                tooltipLabel.text = _bound.FormatBossTooltip(_mult);
        }

        public void OnPointerEnter(PointerEventData eventData) => ShowTooltip();
        public void OnPointerExit(PointerEventData eventData) => HideTooltip();

        void ShowTooltip()
        {
            if (_bound == null || tooltipRoot == null) return;
            if (tooltipLabel != null)
                tooltipLabel.text = _bound.FormatBossTooltip(_mult);
            tooltipRoot.SetActive(true);
        }

        void HideTooltip()
        {
            if (tooltipRoot != null) tooltipRoot.SetActive(false);
        }
    }
}
