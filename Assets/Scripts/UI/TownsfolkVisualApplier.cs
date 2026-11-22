using UnityEngine;
using UnityEngine.UI;

namespace ArthurianAutobattler
{
    /// <summary>
    /// Single-sprite pew/portrait bind (no paperdoll Body/Outfit/Head stack).
    /// Matches Art Architect Category: Sprite → ModularSpriteLibrary.basePoses.
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

        /// <summary>One sprite only on Body; Outfit/Head/Prop forced off.</summary>
        public static void Apply(TownsfolkAppearance appearance, LayerSet layers, RecruitInstance fallback = null)
        {
            var lib = Library;
            Sprite sprite = ResolveSingleSprite(appearance, lib, fallback);

            if (sprite == null)
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
                        "[DemonKnight] Pew boxes = single-sprite library empty at runtime. " +
                        $"lib={(lib != null ? "found" : "NULL")} posesArray={poses} posesFilled={filled}. " +
                        "Save Splices (Category Sprite) or Verify & Sync Modular Library, then New Game.");
                }
                ApplyLegacyFallback(layers, fallback);
                DisableExtraLayers(layers);
                return;
            }

            // Full-color sprite — no paperdoll skin/hair tint stacking.
            SetLayer(layers.body, sprite, Color.white, enabled: true);
            DisableExtraLayers(layers);
        }

        /// <summary>Pick one sprite: library pose index, then recruit body/avatar.</summary>
        public static Sprite ResolveSingleSprite(
            TownsfolkAppearance appearance,
            ModularSpriteLibrary lib,
            RecruitInstance fallback = null)
        {
            if (lib != null)
            {
                int idx = appearance != null ? appearance.poseIndex : 0;
                Sprite sp = lib.GetPose(idx);
                if (sp != null) return sp;
                // First non-null pose if index empty.
                if (lib.basePoses != null)
                {
                    for (int i = 0; i < lib.basePoses.Length; i++)
                        if (lib.basePoses[i] != null) return lib.basePoses[i];
                }
            }
            if (fallback != null)
            {
                if (fallback.bodySprite != null) return fallback.bodySprite;
                if (fallback.randomAvatar != null) return fallback.randomAvatar;
            }
            return null;
        }

        static void DisableExtraLayers(LayerSet layers)
        {
            SetLayer(layers.outfit, null, Color.white, enabled: false);
            SetLayer(layers.head, null, Color.white, enabled: false);
            SetLayer(layers.prop, null, Color.white, enabled: false);
        }

        static void ApplyLegacyFallback(LayerSet layers, RecruitInstance recruit)
        {
            Sprite body = recruit != null
                ? (recruit.bodySprite != null ? recruit.bodySprite : recruit.randomAvatar)
                : null;

            if (layers.body != null)
            {
                layers.body.enabled = true;
                layers.body.sprite = body;
                if (body != null)
                    layers.body.color = Color.white;
                else if (recruit != null)
                    layers.body.color = PlaceholderColor(recruit.unitType);
            }
        }

        static void SetLayer(Image img, Sprite sprite, Color color, bool enabled)
        {
            if (img == null) return;
            img.type = Image.Type.Simple;
            img.preserveAspect = true;
            img.sprite = sprite;
            img.color = color;
            img.enabled = enabled && sprite != null;
            if (sprite == null)
                img.enabled = false;
        }

        public static Color PlaceholderColor(UnitType type) => type switch
        {
            UnitType.Knight => new Color(0.55f, 0.48f, 0.25f, 0.92f),
            UnitType.Warrior => new Color(0.40f, 0.38f, 0.45f, 0.92f),
            UnitType.Peasant => new Color(0.35f, 0.42f, 0.28f, 0.92f),
            _ => new Color(0.45f, 0.40f, 0.35f, 0.90f)
        };

        /// <summary>Ensure Body (+ unused Outfit/Head/Prop children kept disabled by Apply).</summary>
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
