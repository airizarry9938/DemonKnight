using UnityEngine;
using UnityEngine.UI;

namespace ArthurianAutobattler
{
    /// <summary>
    /// Shared Body/Outfit/Head/Prop stack so pew seats and dossier portraits match.
    /// </summary>
    public static class TownsfolkVisualApplier
    {
        public static ModularSpriteLibrary Library =>
            _lib != null ? _lib : (_lib = ModularSpriteLibrary.Load());

        static ModularSpriteLibrary _lib;
        static bool _loggedEmptyLibrary;

        public static void SetLibrary(ModularSpriteLibrary lib)
        {
            _lib = lib;
            _loggedEmptyLibrary = false;
        }

        public struct LayerSet
        {
            public Image body;
            public Image outfit;
            public Image head;
            public Image prop;
        }

        public static void Apply(TownsfolkAppearance appearance, LayerSet layers, RecruitInstance fallback = null)
        {
            var lib = Library;
            if (appearance == null)
            {
                ApplyLegacyFallback(layers, fallback);
                return;
            }

            Sprite bodySp = lib != null ? lib.GetPose(appearance.poseIndex) : null;
            Sprite outfitSp = lib != null ? lib.GetOutfit(appearance.outfitIndex) : null;
            Sprite headSp = lib != null ? lib.GetHead(appearance.headIndex) : null;
            Sprite propSp = appearance.HoldsProp && lib != null ? lib.GetProp(appearance.propIndex) : null;

            bool modularReady = bodySp != null || outfitSp != null || headSp != null;

            if (!modularReady)
            {
                if (!_loggedEmptyLibrary)
                {
                    _loggedEmptyLibrary = true;
                    int poses = lib != null && lib.basePoses != null ? lib.basePoses.Length : -1;
                    int filled = 0;
                    if (lib?.basePoses != null)
                        for (int i = 0; i < lib.basePoses.Length; i++)
                            if (lib.basePoses[i] != null) filled++;
                    Debug.LogWarning(
                        "[DemonKnight] Pew boxes = modular library empty at runtime. " +
                        $"lib={(lib != null ? "found" : "NULL")} posesArray={poses} posesFilled={filled}. " +
                        "Fix: Arthurian → Art Tools → Verify & Sync Modular Library, then New Game.");
                }
                ApplyLegacyFallback(layers, fallback);
                // Still apply skin tint if we only have a grayscale body from legacy.
                if (layers.body != null && layers.body.sprite != null)
                    layers.body.color = appearance.SkinColor;
                return;
            }

            SetLayer(layers.body, bodySp, appearance.SkinColor, enabled: true);
            SetLayer(layers.outfit, outfitSp, Color.white, enabled: outfitSp != null);

            bool tintHair = lib == null || lib.HeadIsHair(appearance.headIndex);
            SetLayer(layers.head, headSp, tintHair ? appearance.HairColor : Color.white, enabled: headSp != null);

            bool showProp = appearance.HoldsProp && propSp != null;
            SetLayer(layers.prop, propSp, Color.white, enabled: showProp);
        }

        static void ApplyLegacyFallback(LayerSet layers, RecruitInstance recruit)
        {
            Sprite body = recruit != null
                ? (recruit.bodySprite != null ? recruit.bodySprite : recruit.randomAvatar)
                : null;
            Sprite head = recruit != null
                ? (recruit.headSprite != null ? recruit.headSprite : recruit.randomAvatar)
                : null;

            if (layers.body != null)
            {
                layers.body.enabled = true;
                layers.body.sprite = body;
                if (body != null)
                {
                    layers.body.color = recruit != null && recruit.bodyPosture == BodyPosture.EagerForward
                        ? new Color(1f, 0.95f, 0.75f)
                        : Color.white;
                }
                else if (recruit != null)
                {
                    layers.body.color = PlaceholderColor(recruit.unitType);
                }
            }

            if (layers.outfit != null)
            {
                layers.outfit.sprite = null;
                layers.outfit.enabled = false;
            }

            if (layers.head != null)
            {
                layers.head.sprite = head;
                layers.head.color = Color.white;
                layers.head.enabled = head != null;
            }

            if (layers.prop != null)
            {
                layers.prop.sprite = null;
                layers.prop.enabled = false;
            }
        }

        static void SetLayer(Image img, Sprite sprite, Color color, bool enabled)
        {
            if (img == null) return;
            img.sprite = sprite;
            img.color = color;
            img.enabled = enabled && (sprite != null || color.a > 0.01f);
            if (sprite == null && enabled)
            {
                // Solid placeholder only for body; callers pass enabled=false for empty overlays.
                img.enabled = false;
            }
        }

        public static Color PlaceholderColor(UnitType type) => type switch
        {
            UnitType.Knight => new Color(0.55f, 0.48f, 0.25f, 0.92f),
            UnitType.Warrior => new Color(0.40f, 0.38f, 0.45f, 0.92f),
            UnitType.Peasant => new Color(0.35f, 0.42f, 0.28f, 0.92f),
            _ => new Color(0.45f, 0.40f, 0.35f, 0.90f)
        };

        /// <summary>Ensure Body/Outfit/Head/Prop child Images exist under root (sorted bottom→top).</summary>
        public static LayerSet EnsureChildLayers(Transform root, bool createIfMissing = true)
        {
            var set = new LayerSet
            {
                body = FindOrCreate(root, "Body", 0, createIfMissing),
                outfit = FindOrCreate(root, "Outfit", 1, createIfMissing),
                head = FindOrCreate(root, "Head", 2, createIfMissing),
                prop = FindOrCreate(root, "Prop", 3, createIfMissing)
            };
            return set;
        }

        static Image FindOrCreate(Transform root, string name, int sibling, bool create)
        {
            if (root == null) return null;
            Transform t = root.Find(name);
            if (t == null)
            {
                // Also accept legacy names.
                if (name == "Body") t = root.Find("BodyLayer");
                if (name == "Head") t = root.Find("HeadLayer");
            }
            if (t == null && !create) return null;
            if (t == null)
            {
                var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                t = go.transform;
                t.SetParent(root, false);
                var rt = go.GetComponent<RectTransform>();
                rt.anchorMin = Vector2.zero;
                rt.anchorMax = Vector2.one;
                rt.offsetMin = Vector2.zero;
                rt.offsetMax = Vector2.zero;
                var img = go.GetComponent<Image>();
                img.raycastTarget = name == "Body";
                img.preserveAspect = true;
            }
            t.SetSiblingIndex(Mathf.Min(sibling, root.childCount - 1));
            return t.GetComponent<Image>();
        }
    }
}
