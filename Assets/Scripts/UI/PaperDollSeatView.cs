using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using TMPro;

namespace ArthurianAutobattler
{
    /// <summary>
    /// Modular pew seat: Body → Outfit → Head → Prop (bottom→top), driven by
    /// <see cref="TownsfolkAppearance"/> so dossier portraits match.
    /// </summary>
    public class PaperDollSeatView : MonoBehaviour
    {
        public Image bodyLayer;
        public Image outfitLayer;
        public Image headLayer;
        public Image propLayer;
        public Image moodLayer;
        public Button seatButton;
        public TextMeshProUGUI seatLabel;
        public RecruitInstance Bound { get; private set; }

        static bool _loggedFirstBind;

        /// <summary>Legacy: left-click = select. Prefer the hover/RMB overload.</summary>
        public void Bind(RecruitInstance recruit, System.Action<RecruitInstance> onClick) =>
            Bind(recruit, onLeftClick: onClick, onHover: null, onRightClick: onClick);

        /// <summary>
        /// Hover → bottom dialogue only (no lock). RMB → selection menu. LMB optional.
        /// </summary>
        public void Bind(
            RecruitInstance recruit,
            Action onLeftClick,
            Action onHover,
            Action onRightClick)
        {
            Bound = recruit;
            var layers = EnsureModularLayers();
            HideNonModularImages(layers);

            if (recruit != null)
            {
                // Older TownRNG / Sync copies left appearance unset → solid pew boxes.
                if (recruit.appearance == null)
                {
                    var lib = TownsfolkVisualApplier.Library;
                    recruit.appearance = TownsfolkAppearance.Generate(
                        recruit.unitType, recruit.unitID, recruit.pewSeatIndex, lib);
                }
                recruit.appearance.BindSeatSalt(recruit.pewSeatIndex);
            }

            TownsfolkVisualApplier.Apply(
                recruit?.appearance,
                layers,
                recruit);

            if (!_loggedFirstBind && recruit != null)
            {
                _loggedFirstBind = true;
                string bodyName = layers.body != null && layers.body.sprite != null
                    ? layers.body.sprite.name : "NULL";
                string outfitName = layers.outfit != null && layers.outfit.sprite != null
                    ? layers.outfit.sprite.name : "NULL";
                string headName = layers.head != null && layers.head.sprite != null
                    ? layers.head.sprite.name : "NULL";
                Debug.Log(
                    $"[DemonKnight] First pew Bind '{recruit.characterName}': " +
                    $"appearance={(recruit.appearance != null ? "yes" : "NULL")} " +
                    $"body={bodyName} outfit={outfitName} head={headName} " +
                    $"bodyEnabled={(layers.body != null && layers.body.enabled)}");
            }

            if (moodLayer != null)
            {
                moodLayer.sprite = recruit != null ? recruit.moodOverlaySprite : null;
                moodLayer.enabled = recruit != null && recruit.moodOverlaySprite != null;
                moodLayer.transform.SetAsLastSibling();
            }

            if (seatLabel != null)
            {
                bool hasArt = layers.body != null && layers.body.sprite != null;
                if (recruit != null && recruit.isSirenEnchantress) seatLabel.text = "♪";
                else if (!hasArt && recruit != null) seatLabel.text = recruit.unitType.ToString()[..1];
                else seatLabel.text = "";
            }

            if (seatButton == null) seatButton = GetComponent<Button>() ?? gameObject.AddComponent<Button>();
            seatButton.interactable = true;
            if (seatButton.targetGraphic == null && layers.body != null)
                seatButton.targetGraphic = layers.body;
            var colors = seatButton.colors;
            colors.highlightedColor = new Color(0.95f, 0.9f, 0.55f, 1f);
            colors.selectedColor = new Color(0.85f, 0.78f, 0.4f, 1f);
            seatButton.colors = colors;
            seatButton.onClick.RemoveAllListeners();
            if (onLeftClick != null)
                seatButton.onClick.AddListener(() => onLeftClick());

            PewSeatHoverHighlight.Ensure(
                gameObject,
                recruit != null,
                onHoverInfo: onHover,
                onRightClick: onRightClick);
            VictoryStarBadge.Apply(transform, recruit);
        }

        public void BindEmpty()
        {
            Bound = null;
            var layers = EnsureModularLayers();
            if (layers.body != null)
            {
                layers.body.sprite = null;
                layers.body.color = new Color(0.12f, 0.10f, 0.09f, 0.40f);
                layers.body.enabled = true;
            }
            if (layers.outfit != null) layers.outfit.enabled = false;
            if (layers.head != null) layers.head.enabled = false;
            if (layers.prop != null) layers.prop.enabled = false;
            if (moodLayer != null) moodLayer.enabled = false;
            if (seatLabel != null) seatLabel.text = "";
            VictoryStarBadge.Clear(transform);
            if (seatButton != null)
            {
                seatButton.onClick.RemoveAllListeners();
                seatButton.interactable = false;
            }
            PewSeatHoverHighlight.Ensure(gameObject, false, null, null);
        }

        TownsfolkVisualApplier.LayerSet EnsureModularLayers()
        {
            // Migrate legacy single Image on root → Body child when needed.
            if (bodyLayer != null && bodyLayer.transform == transform)
            {
                // Keep reference; EnsureChildLayers will create named children.
                bodyLayer = null;
            }

            var set = TownsfolkVisualApplier.EnsureChildLayers(transform, createIfMissing: true);
            bodyLayer = set.body;
            outfitLayer = set.outfit;
            headLayer = set.head;
            propLayer = set.prop;

            // Root Image (if any) should not fight with layered children.
            var rootImg = GetComponent<Image>();
            if (rootImg != null && rootImg != bodyLayer)
            {
                rootImg.sprite = null;
                rootImg.color = new Color(1f, 1f, 1f, 0f);
                rootImg.raycastTarget = false;
                rootImg.enabled = false;
            }

            if (seatButton == null) seatButton = GetComponent<Button>();
            return set;
        }

        /// <summary>
        /// Prefab avatar buttons often keep a solid-color Image that reads as the old
        /// "box" even after modular Body/Outfit/Head are applied — hide those.
        /// </summary>
        void HideNonModularImages(TownsfolkVisualApplier.LayerSet layers)
        {
            foreach (var img in GetComponentsInChildren<Image>(true))
            {
                if (img == null) continue;
                if (img == layers.body || img == layers.outfit || img == layers.head
                    || img == layers.prop || img == moodLayer)
                {
                    img.type = Image.Type.Simple;
                    img.preserveAspect = true;
                    continue;
                }
                img.sprite = null;
                img.enabled = false;
                img.raycastTarget = false;
            }
        }
    }

    /// <summary>
    /// Embedded in PaperDollSeatView.cs so Force Sync always lands the type with an
    /// AtomicCritical UI file (standalone PewSeatHoverHighlight.cs was missing on Downloads).
    /// </summary>
    [DisallowMultipleComponent]
    public class PewSeatHoverHighlight : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler
    {
        const string RimName = "PewHoverRim";
        static readonly Vector3 HoverScale = new Vector3(1.08f, 1.08f, 1f);
        static readonly Color RimIdle = new Color(0.95f, 0.85f, 0.45f, 0f);
        static readonly Color RimHover = new Color(0.95f, 0.88f, 0.35f, 0.85f);

        Image rim;
        Vector3 baseScale = Vector3.one;
        bool hovering;
        bool occupied = true;
        Action onHoverInfo;
        Action onRightClick;

        public static PewSeatHoverHighlight Ensure(
            GameObject go,
            bool isOccupied,
            Action onHoverInfo = null,
            Action onRightClick = null)
        {
            if (go == null) return null;
            var h = go.GetComponent<PewSeatHoverHighlight>();
            if (h == null) h = go.AddComponent<PewSeatHoverHighlight>();
            h.Configure(isOccupied, onHoverInfo, onRightClick);
            return h;
        }

        public void Configure(bool isOccupied, Action onHoverInfo = null, Action onRightClick = null)
        {
            occupied = isOccupied;
            this.onHoverInfo = onHoverInfo;
            this.onRightClick = onRightClick;
            if (!hovering) baseScale = transform.localScale;
            EnsureRim();
            Apply(false);
            if (!occupied)
            {
                hovering = false;
                transform.localScale = baseScale;
            }
        }

        void EnsureRim()
        {
            if (rim != null) return;
            var existing = transform.Find(RimName);
            if (existing != null)
            {
                rim = existing.GetComponent<Image>();
                if (rim != null) return;
            }

            var go = new GameObject(RimName, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(transform, false);
            go.transform.SetAsFirstSibling();
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(-6f, -6f);
            rt.offsetMax = new Vector2(6f, 6f);
            rim = go.GetComponent<Image>();
            rim.color = RimIdle;
            rim.raycastTarget = false;
            rim.sprite = null;
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (!occupied) return;
            hovering = true;
            Apply(true);
            // Bottom dialogue only — never opens selection menu / never locks hire select.
            onHoverInfo?.Invoke();
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            hovering = false;
            Apply(false);
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (!occupied) return;
            if (eventData != null && eventData.button == PointerEventData.InputButton.Right)
                onRightClick?.Invoke();
        }

        void OnDisable()
        {
            hovering = false;
            Apply(false);
        }

        void Apply(bool on)
        {
            EnsureRim();
            if (rim != null)
            {
                rim.enabled = occupied;
                rim.color = on && occupied ? RimHover : RimIdle;
            }
            transform.localScale = on && occupied ? HoverScale : baseScale;
        }
    }
}
