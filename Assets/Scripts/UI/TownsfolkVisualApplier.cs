using UnityEngine;
using UnityEngine.UI;

namespace ArthurianAutobattler
{
    /// <summary>
    /// Single-sprite pew/portrait bind from ModularSpriteLibrary.basePoses
    /// (Architect Save Splices → Assets/Art/Spliced/Sprite). Never stacks Outfit/Head.
    /// Skips Pose_/Outfit_/Head_ placeholder shapes.
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

            if (sprite == null || IsPaperdollPlaceholder(sprite))
            {
                if (!_loggedEmptyLibrary)
                {
                    _loggedEmptyLibrary = true;
                    int poses = lib != null && lib.basePoses != null ? lib.basePoses.Length : -1;
                    int filled = CountUsablePoses(lib);
                    Debug.LogWarning(
                        "[DemonKnight] Pew single-sprite library empty (no Spliced/Sprite). " +
                        $"lib={(lib != null ? "found" : "NULL")} posesArray={poses} usableSpliced={filled}. " +
                        "Architect Tab 1 → Save Splices, or Escape Breakglass → Tip Hosts + Spliced Pew Library.");
                }
                ApplyEmptySilhouette(layers, fallback);
                DisableExtraLayers(layers);
                return;
            }

            SetLayer(layers.body, sprite, Color.white, enabled: true);
            // Optional face-only overlay from Tab 5 (Head layer) — never replaces body.
            Sprite face = fallback != null ? LegendPewPortraitApplier.ResolvePewFaceOverlay(fallback) : null;
            if (IsUsablePewSprite(face) && !LegendPewPortraitApplier.IsBodyPoolSprite(face))
            {
                SetLayer(layers.head, face, Color.white, enabled: true);
                SetLayer(layers.outfit, null, Color.white, enabled: false);
                SetLayer(layers.prop, null, Color.white, enabled: false);
            }
            else
                DisableExtraLayers(layers);
        }

        /// <summary>
        /// Prefer Architect class pools (Peasant/Warrior/Knight) when a recruit is known;
        /// else spliced library poses; never Pose_/Outfit_/Head_ placeholders.
        /// </summary>
        public static Sprite ResolveSingleSprite(
            TownsfolkAppearance appearance,
            ModularSpriteLibrary lib,
            RecruitInstance fallback = null)
        {
            if (fallback != null)
            {
                // BODY only — Tab 4 ClassSpritePool. Never Tab 5 face pools as bodySprite.
                Sprite pool = ClassSpritePoolApplier.PickForRecruit(fallback);
                if (IsUsablePewSprite(pool) && !LegendPewPortraitApplier.IsFacePoolSprite(pool))
                    return pool;
            }
            if (lib != null && lib.basePoses != null && lib.basePoses.Length > 0)
            {
                int idx = appearance != null ? appearance.poseIndex : 0;
                idx = Mathf.Clamp(idx, 0, lib.basePoses.Length - 1);
                Sprite sp = lib.basePoses[idx];
                if (IsUsablePewSprite(sp)) return sp;
                for (int i = 0; i < lib.basePoses.Length; i++)
                {
                    sp = lib.basePoses[i];
                    if (IsUsablePewSprite(sp)) return sp;
                }
            }
            if (fallback != null)
            {
                if (IsUsablePewSprite(fallback.bodySprite)) return fallback.bodySprite;
                if (IsUsablePewSprite(fallback.randomAvatar)) return fallback.randomAvatar;
            }
            return null;
        }

        public static bool IsUsablePewSprite(Sprite sp)
        {
            if (sp == null) return false;
            return !IsPaperdollPlaceholder(sp);
        }

        public static bool IsPaperdollPlaceholder(Sprite sp)
        {
            if (sp == null) return true;
            string name = sp.name ?? "";
            if (name.StartsWith("Pose_", System.StringComparison.OrdinalIgnoreCase)) return true;
            if (name.StartsWith("Outfit_", System.StringComparison.OrdinalIgnoreCase)) return true;
            if (name.StartsWith("Head_", System.StringComparison.OrdinalIgnoreCase)) return true;
            if (name.StartsWith("Prop_", System.StringComparison.OrdinalIgnoreCase)) return true;
            if (name.StartsWith("DK_Pose_", System.StringComparison.OrdinalIgnoreCase)) return true;
            if (name.StartsWith("DK_Outfit_", System.StringComparison.OrdinalIgnoreCase)) return true;
            if (name.StartsWith("DK_Head_", System.StringComparison.OrdinalIgnoreCase)) return true;
            if (name.StartsWith("DK_Sprite_", System.StringComparison.OrdinalIgnoreCase)) return true;
            if (name.IndexOf("Prepared_Pose", System.StringComparison.OrdinalIgnoreCase) >= 0) return true;
            if (name.IndexOf("Prepared_Outfit", System.StringComparison.OrdinalIgnoreCase) >= 0) return true;
            if (name.IndexOf("Prepared_Head", System.StringComparison.OrdinalIgnoreCase) >= 0) return true;
            return false;
        }

        static int CountUsablePoses(ModularSpriteLibrary lib)
        {
            if (lib?.basePoses == null) return 0;
            int n = 0;
            for (int i = 0; i < lib.basePoses.Length; i++)
                if (IsUsablePewSprite(lib.basePoses[i])) n++;
            return n;
        }

        static void DisableExtraLayers(LayerSet layers)
        {
            SetLayer(layers.outfit, null, Color.white, enabled: false);
            SetLayer(layers.head, null, Color.white, enabled: false);
            SetLayer(layers.prop, null, Color.white, enabled: false);
        }

        /// <summary>Solid color box — never paperdoll silhouette placeholders.</summary>
        static void ApplyEmptySilhouette(LayerSet layers, RecruitInstance recruit)
        {
            if (layers.body == null) return;
            layers.body.enabled = true;
            layers.body.sprite = null;
            layers.body.color = recruit != null
                ? PlaceholderColor(recruit.unitType)
                : new Color(0.35f, 0.32f, 0.28f, 0.85f);
        }

        static void SetLayer(Image img, Sprite sprite, Color color, bool enabled)
        {
            if (img == null) return;
            img.type = Image.Type.Simple;
            img.preserveAspect = true;
            img.sprite = sprite;
            img.color = color;
            img.enabled = enabled && (sprite != null || color.a > 0.01f);
            if (sprite == null && !enabled)
                img.enabled = false;
        }

        public static Color PlaceholderColor(UnitType type) => type switch
        {
            UnitType.Knight => new Color(0.55f, 0.48f, 0.25f, 0.92f),
            UnitType.Warrior => new Color(0.40f, 0.38f, 0.45f, 0.92f),
            UnitType.Peasant => new Color(0.35f, 0.42f, 0.28f, 0.92f),
            _ => new Color(0.45f, 0.40f, 0.35f, 0.90f)
        };

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
