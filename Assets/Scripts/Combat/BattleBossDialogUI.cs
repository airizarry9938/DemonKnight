using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace ArthurianAutobattler
{
    /// <summary>
    /// Full-width bottom dialog strip shown when AutoBattle starts against a boss.
    /// Built at runtime (no scene wiring required). Call <see cref="ShowForBoss"/>.
    /// </summary>
    public class BattleBossDialogUI : MonoBehaviour
    {
        public static BattleBossDialogUI Instance { get; private set; }

        [Tooltip("Seconds the bar stays up before auto-dismiss (click also dismisses).")]
        public float autoDismissSeconds = 4.5f;

        private Canvas canvas;
        private GameObject root;
        private TextMeshProUGUI speakerText;
        private TextMeshProUGUI bodyText;
        private Coroutine dismissRoutine;
        private bool isShowing;

        /// <summary>True while the intro bar is visible — AutoBattlerEngine holds combat.</summary>
        public static bool IsBlockingCombat =>
            Instance != null && Instance.isShowing;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        public static BattleBossDialogUI Ensure()
        {
            if (Instance != null) return Instance;
            var go = new GameObject("BattleBossDialogUI");
            return go.AddComponent<BattleBossDialogUI>();
        }

        public static void ShowForBoss(CombatUnit boss)
        {
            if (boss == null || !IsBossType(boss.unitType)) return;
            Ensure().Present(boss.unitName, PickLine(boss.unitType));
        }

        public static void Hide()
        {
            if (Instance != null) Instance.Dismiss();
        }

        public static bool IsBossType(UnitType t) =>
            t.ToString().StartsWith("Boss");

        private void Present(string speaker, string line)
        {
            EnsureUi();
            if (root != null) root.SetActive(true);
            if (speakerText != null) speakerText.text = speaker;
            if (bodyText != null) bodyText.text = line;
            isShowing = true;

            if (dismissRoutine != null) StopCoroutine(dismissRoutine);
            dismissRoutine = StartCoroutine(AutoDismiss());
        }

        private IEnumerator AutoDismiss()
        {
            float t = Mathf.Max(1.5f, autoDismissSeconds);
            yield return new WaitForSecondsRealtime(t);
            Dismiss();
        }

        private void Dismiss()
        {
            if (dismissRoutine != null)
            {
                StopCoroutine(dismissRoutine);
                dismissRoutine = null;
            }
            isShowing = false;
            if (root != null) root.SetActive(false);
        }

        private void EnsureUi()
        {
            if (canvas != null) return;

            var canvasGo = new GameObject("BossDialogCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasGo.transform.SetParent(transform, false);
            canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 320;

            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            root = new GameObject("BossDialogBar", typeof(RectTransform), typeof(Image), typeof(Button));
            root.transform.SetParent(canvasGo.transform, false);
            var rt = root.GetComponent<RectTransform>();
            // Full width across the bottom of the frame.
            rt.anchorMin = new Vector2(0f, 0f);
            rt.anchorMax = new Vector2(1f, 0f);
            rt.pivot = new Vector2(0.5f, 0f);
            rt.sizeDelta = new Vector2(0f, 168f);
            rt.anchoredPosition = Vector2.zero;

            var img = root.GetComponent<Image>();
            img.color = new Color(0.06f, 0.05f, 0.08f, 0.92f);
            img.raycastTarget = true;

            var btn = root.GetComponent<Button>();
            btn.transition = Selectable.Transition.None;
            btn.onClick.AddListener(Dismiss);

            // Top accent rule
            var rule = new GameObject("Accent", typeof(RectTransform), typeof(Image));
            rule.transform.SetParent(root.transform, false);
            var ruleRt = rule.GetComponent<RectTransform>();
            ruleRt.anchorMin = new Vector2(0f, 1f);
            ruleRt.anchorMax = new Vector2(1f, 1f);
            ruleRt.pivot = new Vector2(0.5f, 1f);
            ruleRt.sizeDelta = new Vector2(0f, 3f);
            ruleRt.anchoredPosition = Vector2.zero;
            rule.GetComponent<Image>().color = new Color(0.72f, 0.22f, 0.28f, 1f);
            rule.GetComponent<Image>().raycastTarget = false;

            speakerText = MakeLabel(root.transform, "Speaker",
                new Vector2(0.04f, 0.58f), new Vector2(0.96f, 0.92f), 28f, FontStyles.Bold,
                new Color(0.95f, 0.78f, 0.45f));
            bodyText = MakeLabel(root.transform, "Body",
                new Vector2(0.04f, 0.12f), new Vector2(0.96f, 0.58f), 22f, FontStyles.Normal,
                new Color(0.90f, 0.88f, 0.84f));

            var hint = MakeLabel(root.transform, "Hint",
                new Vector2(0.70f, 0.02f), new Vector2(0.98f, 0.18f), 14f, FontStyles.Italic,
                new Color(0.55f, 0.55f, 0.52f));
            hint.alignment = TextAlignmentOptions.Right;
            hint.text = "Click to continue";
        }

        private static TextMeshProUGUI MakeLabel(
            Transform parent, string name, Vector2 aMin, Vector2 aMax, float size, FontStyles style, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = aMin;
            rt.anchorMax = aMax;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            var tmp = go.GetComponent<TextMeshProUGUI>();
            tmp.fontSize = size;
            tmp.fontStyle = style;
            tmp.color = color;
            tmp.alignment = TextAlignmentOptions.Left;
            tmp.textWrappingMode = TextWrappingModes.Normal;
            tmp.raycastTarget = false;
            return tmp;
        }

        private static string PickLine(UnitType boss)
        {
            switch (boss)
            {
                case UnitType.BossBlackKnight:
                    return "\"None shall pass the churchyard while I stand. Draw steel — or kneel.\"";
                case UnitType.BossVortigern:
                    return "\"Arthur's crown was never his. Watch your warband burn for a usurper's throne.\"";
                case UnitType.BossGreenKnight:
                    return "\"Strike true, Camelot. My head returns — does yours?\"";
                case UnitType.BossAccolon:
                    return "\"Excalibur's twin thirsts. Bleed for Morgan's favor.\"";
                case UnitType.BossNimue:
                    return "\"Thorns for the faithful. Come closer, little knights — the lake remembers.\"";
                default:
                    return "\"The horde parts. A legend corrupted bars your path.\"";
            }
        }
    }
}
