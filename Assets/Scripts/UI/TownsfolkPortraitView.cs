using UnityEngine;
using UnityEngine.UI;

namespace ArthurianAutobattler
{
    /// <summary>
    /// Dossier / inspection portrait stack using the same TownsfolkAppearance as pew seats.
    /// Attach to a RectTransform under the dossier; children Body/Outfit/Head/Prop are created if missing.
    /// </summary>
    public class TownsfolkPortraitView : MonoBehaviour
    {
        public Image bodyLayer;
        public Image outfitLayer;
        public Image headLayer;
        public Image propLayer;

        public void Apply(RecruitInstance recruit)
        {
            var layers = EnsureLayers();
            if (recruit != null && recruit.appearance == null)
            {
                recruit.appearance = TownsfolkAppearance.Generate(
                    recruit.unitType, recruit.unitID, recruit.pewSeatIndex,
                    TownsfolkVisualApplier.Library);
            }
            if (recruit?.appearance != null)
                recruit.appearance.BindSeatSalt(recruit.pewSeatIndex);
            TownsfolkVisualApplier.Apply(recruit?.appearance, layers, recruit);
        }

        public void Clear()
        {
            var layers = EnsureLayers();
            if (layers.body != null) { layers.body.sprite = null; layers.body.enabled = false; }
            if (layers.outfit != null) layers.outfit.enabled = false;
            if (layers.head != null) layers.head.enabled = false;
            if (layers.prop != null) layers.prop.enabled = false;
        }

        TownsfolkVisualApplier.LayerSet EnsureLayers()
        {
            var set = TownsfolkVisualApplier.EnsureChildLayers(transform, createIfMissing: true);
            bodyLayer = set.body;
            outfitLayer = set.outfit;
            headLayer = set.head;
            propLayer = set.prop;
            return set;
        }

        /// <summary>
        /// Use existing dossier Image as host: wrap into a portrait root if needed.
        /// </summary>
        public static TownsfolkPortraitView EnsureOn(Image hostOrNull, Transform preferredParent)
        {
            if (hostOrNull != null)
            {
                var existing = hostOrNull.GetComponent<TownsfolkPortraitView>();
                if (existing != null) return existing;

                // Convert single Image into layered stack parented to same transform.
                var view = hostOrNull.gameObject.AddComponent<TownsfolkPortraitView>();
                // Hide host fill — children render the modular stack.
                hostOrNull.color = new Color(1f, 1f, 1f, 0f);
                hostOrNull.raycastTarget = false;
                view.EnsureLayers();
                return view;
            }

            if (preferredParent == null) return null;
            Transform existingTf = preferredParent.Find("ModularPortrait");
            GameObject go;
            if (existingTf != null) go = existingTf.gameObject;
            else
            {
                go = new GameObject("ModularPortrait", typeof(RectTransform), typeof(TownsfolkPortraitView));
                go.transform.SetParent(preferredParent, false);
                var rt = go.GetComponent<RectTransform>();
                rt.anchorMin = new Vector2(0f, 0.35f);
                rt.anchorMax = new Vector2(0.28f, 0.95f);
                rt.offsetMin = Vector2.zero;
                rt.offsetMax = Vector2.zero;
            }
            return go.GetComponent<TownsfolkPortraitView>();
        }
    }
}
