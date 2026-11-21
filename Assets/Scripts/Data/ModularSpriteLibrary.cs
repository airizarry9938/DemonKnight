using System;
using System.Collections.Generic;
using UnityEngine;

namespace ArthurianAutobattler
{
    [Serializable]
    public class ModularHeadEntry
    {
        public Sprite sprite;
        [Tooltip("If true, tint with hairColorIndex. Hoods/helmets leave Color.white.")]
        public bool isHair = true;
    }

    /// <summary>One registered multi-frame clip on a modular part category.</summary>
    [Serializable]
    public class ModularAnimRegistration
    {
        [Tooltip("Pose, Outfit, Head, Weapon, or EnemyBoss.")]
        public string category = "Pose";
        public SpriteAnimClip clip = new SpriteAnimClip();
    }

    /// <summary>
    /// Sprite arrays for modular townsfolk. Body poses are grayscale (skin-tinted);
    /// outfits are pre-colored with transparent skin cutouts; heads are hair (tint)
    /// or helms (untinted); props are pre-colored.
    /// </summary>
    [CreateAssetMenu(fileName = "ModularSpriteLibrary", menuName = "DemonKnight/Modular Sprite Library")]
    public class ModularSpriteLibrary : ScriptableObject
    {
        public const string ResourcesPath = "Art/Modular/ModularSpriteLibrary";

        [Header("Base poses (grayscale) — ArmsCrossed, HandsOnKnees, LeaningForward, HoldingProp")]
        public Sprite[] basePoses = new Sprite[4];

        [Header("Outfits (pre-colored, transparent skin) — 0–1 Peasant, 2–3 Warrior, 4–5 Knight")]
        public Sprite[] outfits = new Sprite[6];

        [Header("Heads — hair (tint) or hoods/helmets (no tint)")]
        public ModularHeadEntry[] heads = Array.Empty<ModularHeadEntry>();

        [Header("Props — index 0 = None")]
        public Sprite[] props = Array.Empty<Sprite>();

        [Header("Animation clips (Art Architect / ModularSpriteAnimator)")]
        [Tooltip("Registered Idle/Attack/Hit/Death clips keyed by part category.")]
        public List<ModularAnimRegistration> animClips = new List<ModularAnimRegistration>();

        public int HeadCount => heads != null ? heads.Length : 0;
        public int PropCount => props != null ? props.Length : 0;
        public int PoseCount => basePoses != null ? basePoses.Length : 0;
        public int OutfitCount => outfits != null ? outfits.Length : 0;

        /// <summary>Upsert a clip by category + clipName (Pose/Outfit/Head/Weapon/EnemyBoss).</summary>
        public void RegisterAnimClip(string category, SpriteAnimClip clip)
        {
            if (clip == null || string.IsNullOrEmpty(clip.clipName)) return;
            animClips ??= new List<ModularAnimRegistration>();
            string cat = string.IsNullOrEmpty(category) ? "Pose" : category;
            for (int i = 0; i < animClips.Count; i++)
            {
                var e = animClips[i];
                if (e == null) continue;
                if (!string.Equals(e.category, cat, StringComparison.OrdinalIgnoreCase)) continue;
                if (e.clip == null || !string.Equals(e.clip.clipName, clip.clipName, StringComparison.OrdinalIgnoreCase))
                    continue;
                e.clip = clip;
                return;
            }
            animClips.Add(new ModularAnimRegistration { category = cat, clip = clip });
        }

        public SpriteAnimClip FindAnimClip(string category, string clipName)
        {
            if (animClips == null) return null;
            for (int i = 0; i < animClips.Count; i++)
            {
                var e = animClips[i];
                if (e?.clip == null) continue;
                if (!string.Equals(e.category, category, StringComparison.OrdinalIgnoreCase)) continue;
                if (string.Equals(e.clip.clipName, clipName, StringComparison.OrdinalIgnoreCase))
                    return e.clip;
            }
            return null;
        }

        public static ModularSpriteLibrary Load()
        {
            var lib = Resources.Load<ModularSpriteLibrary>(ResourcesPath);
            if (lib != null) return lib;
            // Fallbacks: any ModularSpriteLibrary in Resources.
            var all = Resources.LoadAll<ModularSpriteLibrary>("");
            return all != null && all.Length > 0 ? all[0] : null;
        }

        public Sprite GetPose(int poseIndex)
        {
            if (basePoses == null || basePoses.Length == 0) return null;
            return basePoses[Mathf.Clamp(poseIndex, 0, basePoses.Length - 1)];
        }

        public Sprite GetOutfit(int outfitIndex)
        {
            if (outfits == null || outfits.Length == 0) return null;
            return outfits[Mathf.Clamp(outfitIndex, 0, outfits.Length - 1)];
        }

        public Sprite GetHead(int headIndex)
        {
            if (heads == null || heads.Length == 0) return null;
            var e = heads[Mathf.Clamp(headIndex, 0, heads.Length - 1)];
            return e != null ? e.sprite : null;
        }

        public bool HeadIsHair(int headIndex)
        {
            if (heads == null || heads.Length == 0) return true;
            var e = heads[Mathf.Clamp(headIndex, 0, heads.Length - 1)];
            return e == null || e.isHair;
        }

        public Sprite GetProp(int propIndex)
        {
            if (props == null || props.Length == 0 || propIndex <= 0) return null;
            if (propIndex >= props.Length) return null;
            return props[propIndex];
        }

        public void EnsureSizedArrays()
        {
            if (basePoses == null || basePoses.Length < 4)
                Array.Resize(ref basePoses, 4);
            if (outfits == null || outfits.Length < 6)
                Array.Resize(ref outfits, 6);
            if (heads == null) heads = Array.Empty<ModularHeadEntry>();
            if (props == null) props = Array.Empty<Sprite>();
        }

        public void AssignHeadsFromSprites(IList<Sprite> sprites, IList<bool> isHairFlags = null)
        {
            if (sprites == null) { heads = Array.Empty<ModularHeadEntry>(); return; }
            heads = new ModularHeadEntry[sprites.Count];
            for (int i = 0; i < sprites.Count; i++)
            {
                bool hair = isHairFlags != null && i < isHairFlags.Count ? isHairFlags[i] : GuessIsHair(sprites[i], i);
                heads[i] = new ModularHeadEntry { sprite = sprites[i], isHair = hair };
            }
        }

        /// <summary>Heuristic: first half of a heads sheet often hair; names containing Helm/Hood are not.</summary>
        public static bool GuessIsHair(Sprite s, int index)
        {
            if (s == null) return true;
            string n = s.name ?? "";
            if (n.IndexOf("helm", StringComparison.OrdinalIgnoreCase) >= 0) return false;
            if (n.IndexOf("hood", StringComparison.OrdinalIgnoreCase) >= 0) return false;
            if (n.IndexOf("hat", StringComparison.OrdinalIgnoreCase) >= 0) return false;
            // Even indices in 2×6 head sheet row0 often hair; row1 helms — importer can override.
            return index < 6;
        }
    }
}
