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
            if (bodyLayer != null)
            {
                bodyLayer.sprite = recruit.bodySprite != null ? recruit.bodySprite : recruit.randomAvatar;
                bodyLayer.enabled = bodyLayer.sprite != null;
                // Posture tint tells without scouting gold.
                bodyLayer.color = recruit.bodyPosture switch
                {
                    BodyPosture.EagerForward => new Color(1f, 0.95f, 0.75f),
                    BodyPosture.SlouchedExit => new Color(0.75f, 0.78f, 0.85f),
                    _ => Color.white
                };
            }

            if (headLayer != null)
            {
                headLayer.sprite = recruit.headSprite != null ? recruit.headSprite : recruit.randomAvatar;
                headLayer.enabled = headLayer.sprite != null;
            }

            if (moodLayer != null)
            {
                moodLayer.sprite = recruit.moodOverlaySprite;
                moodLayer.enabled = recruit.moodOverlaySprite != null;
            }

            if (seatLabel != null)
                seatLabel.text = recruit.isSirenEnchantress ? "♪" : "";

            if (seatButton != null)
            {
                seatButton.onClick.RemoveAllListeners();
                seatButton.onClick.AddListener(() => onClick?.Invoke(recruit));
            }
        }
    }
}
