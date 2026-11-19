using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace ArthurianAutobattler
{
    /// <summary>3-layer paper-doll seat used in the Somerset church pews.</summary>
    public class PaperDollSeatView : MonoBehaviour
    {
        public Image bodyLayer;
        public Image headLayer;
        public Image moodLayer;
        public Button seatButton;
        public TextMeshProUGUI seatLabel;
        public RecruitInstance Bound { get; private set; }

        public void Bind(RecruitInstance recruit, System.Action<RecruitInstance> onClick)
        {
            Bound = recruit;
            EnsureLayers();

            Sprite body = recruit.bodySprite != null ? recruit.bodySprite : recruit.randomAvatar;
            Sprite head = recruit.headSprite != null ? recruit.headSprite : recruit.randomAvatar;

            if (bodyLayer != null)
            {
                bodyLayer.sprite = body;
                bodyLayer.enabled = true;
                bodyLayer.raycastTarget = true;
                if (body != null)
                {
                    bodyLayer.color = recruit.bodyPosture switch
                    {
                        BodyPosture.EagerForward => new Color(1f, 0.95f, 0.75f),
                        BodyPosture.SlouchedExit => new Color(0.75f, 0.78f, 0.85f),
                        _ => Color.white
                    };
                }
                else
                {
                    // No art assigned yet — solid pew placeholder so all 52 seats stay visible.
                    bodyLayer.color = PlaceholderColor(recruit.unitType);
                }
            }

            if (headLayer != null)
            {
                headLayer.sprite = head;
                headLayer.enabled = head != null;
            }

            if (moodLayer != null)
            {
                moodLayer.sprite = recruit.moodOverlaySprite;
                moodLayer.enabled = recruit.moodOverlaySprite != null;
            }

            if (seatLabel != null)
            {
                if (recruit.isSirenEnchantress) seatLabel.text = "♪";
                else if (body == null) seatLabel.text = recruit.unitType.ToString()[..1];
                else seatLabel.text = "";
            }

            if (seatButton == null) seatButton = GetComponent<Button>() ?? gameObject.AddComponent<Button>();
            seatButton.interactable = true;
            if (seatButton.targetGraphic == null && bodyLayer != null)
                seatButton.targetGraphic = bodyLayer;
            seatButton.onClick.RemoveAllListeners();
            seatButton.onClick.AddListener(() => onClick?.Invoke(recruit));
        }

        public void BindEmpty()
        {
            Bound = null;
            EnsureLayers();
            if (bodyLayer != null)
            {
                bodyLayer.sprite = null;
                bodyLayer.color = new Color(0.12f, 0.10f, 0.09f, 0.40f);
                bodyLayer.enabled = true;
            }
            if (headLayer != null) headLayer.enabled = false;
            if (moodLayer != null) moodLayer.enabled = false;
            if (seatLabel != null) seatLabel.text = "";
            if (seatButton != null)
            {
                seatButton.onClick.RemoveAllListeners();
                seatButton.interactable = false;
            }
        }

        private void EnsureLayers()
        {
            if (bodyLayer == null)
            {
                var img = GetComponent<Image>();
                if (img == null) img = gameObject.AddComponent<Image>();
                bodyLayer = img;
            }
            if (seatButton == null) seatButton = GetComponent<Button>();
        }

        private static Color PlaceholderColor(UnitType type) => type switch
        {
            UnitType.Knight => new Color(0.55f, 0.48f, 0.25f, 0.92f),
            UnitType.Warrior => new Color(0.40f, 0.38f, 0.45f, 0.92f),
            UnitType.Peasant => new Color(0.35f, 0.42f, 0.28f, 0.92f),
            _ => new Color(0.45f, 0.40f, 0.35f, 0.90f)
        };
    }
}
