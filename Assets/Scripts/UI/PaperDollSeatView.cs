using UnityEngine;
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

        public void Bind(RecruitInstance recruit, System.Action<RecruitInstance> onClick)
        {
            Bound = recruit;
            var layers = EnsureModularLayers();
            HideNonModularImages(layers);

            if (recruit != null && recruit.appearance != null)
                recruit.appearance.BindSeatSalt(recruit.pewSeatIndex);

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
            seatButton.onClick.RemoveAllListeners();
            seatButton.onClick.AddListener(() => onClick?.Invoke(recruit));

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
}
