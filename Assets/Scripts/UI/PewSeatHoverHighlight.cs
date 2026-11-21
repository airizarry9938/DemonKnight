using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace ArthurianAutobattler
{
    /// <summary>
    /// Mouse hover highlight on pew recruit avatars for fast seat selection.
    /// Bright rim + slight scale — works with modular paper-doll seats and flat avatar buttons.
    /// </summary>
    [DisallowMultipleComponent]
    public class PewSeatHoverHighlight : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        const string RimName = "PewHoverRim";
        static readonly Vector3 HoverScale = new Vector3(1.08f, 1.08f, 1f);
        static readonly Color RimIdle = new Color(0.95f, 0.85f, 0.45f, 0f);
        static readonly Color RimHover = new Color(0.95f, 0.88f, 0.35f, 0.85f);

        Image rim;
        Vector3 baseScale = Vector3.one;
        bool hovering;
        bool occupied = true;

        public static PewSeatHoverHighlight Ensure(GameObject go, bool isOccupied)
        {
            if (go == null) return null;
            var h = go.GetComponent<PewSeatHoverHighlight>();
            if (h == null) h = go.AddComponent<PewSeatHoverHighlight>();
            h.Configure(isOccupied);
            return h;
        }

        public void Configure(bool isOccupied)
        {
            occupied = isOccupied;
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
            // Soft filled plate behind the avatar so hover reads clearly.
            rim.sprite = null;
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (!occupied) return;
            hovering = true;
            Apply(true);
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            hovering = false;
            Apply(false);
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
