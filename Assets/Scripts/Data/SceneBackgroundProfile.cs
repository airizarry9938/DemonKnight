using System;
using System.Collections.Generic;
using UnityEngine;

namespace ArthurianAutobattler
{
    // tip: fix-gdt-profile-always-compile — named MonoScript SO only (SceneBackgroundSlot/Entry live in GameDataTypes)
    /// <summary>
    /// Architect-assigned scene/screen backgrounds (image or video). Resources path: SceneBackgroundProfile.
    /// Assign-only — empty slots keep each screen's built-in default art.
    /// </summary>
    [CreateAssetMenu(fileName = "SceneBackgroundProfile", menuName = "DemonKnight/Scene Background Profile")]
    public class SceneBackgroundProfile : ScriptableObject
    {
        public const string ResourcesPath = "SceneBackgroundProfile";

        public List<SceneBackgroundEntry> entries = new List<SceneBackgroundEntry>();

        public static SceneBackgroundProfile Load()
        {
            SceneBackgroundProfile p = null;
            try { p = Resources.Load<SceneBackgroundProfile>(ResourcesPath); }
            catch { /* missing MonoScript GUID/fileID after embed — fall through */ }
            if (p != null)
            {
                p.EnsureDefaultEntries();
                p.RematerializeSpritesFromPaths();
                return p;
            }
            try
            {
                var all = Resources.LoadAll<SceneBackgroundProfile>("");
                if (all != null)
                {
                    for (int i = 0; i < all.Length; i++)
                    {
                        if (all[i] == null) continue;
                        all[i].EnsureDefaultEntries();
                        all[i].RematerializeSpritesFromPaths();
                        return all[i];
                    }
                }
            }
            catch { /* broken Resources asset / missing script spam — soft stand-in */ }
            p = CreateInstance<SceneBackgroundProfile>();
            p.name = "SceneBackgroundProfile_Runtime";
            p.EnsureDefaultEntries();
            return p;
        }

        /// <summary>
        /// Restore Missing image sprites from path backups (Resources / StreamingAssets).
        /// tip: fix-class-pool-kind-cs0019
        /// </summary>
        public int RematerializeSpritesFromPaths()
        {
            EnsureDefaultEntries();
            if (entries == null) return 0;
            int restored = 0;
            for (int i = 0; i < entries.Count; i++)
            {
                var e = entries[i];
                if (e == null || e.imageSprite != null) continue;
                if (string.IsNullOrEmpty(e.imageAssetPath)) continue;
                Sprite sp = ClassSpritePoolProfile.TryLoadSpriteAtPath(e.imageAssetPath);
                if (sp == null) continue;
                e.imageSprite = sp;
                restored++;
            }
            return restored;
        }

        public void EnsureDefaultEntries()
        {
            entries ??= new List<SceneBackgroundEntry>();
            EnsureEntry(SceneBackgroundSlot.MainMenu, "Main Menu");
            EnsureEntry(SceneBackgroundSlot.SomersetChurch, "Somerset Church");
            EnsureEntry(SceneBackgroundSlot.ChurchyardBattle, "Churchyard Battle");
            EnsureEntry(SceneBackgroundSlot.VictoryOverlay, "Victory Overlay");
            EnsureEntry(SceneBackgroundSlot.MerlinResurrection, "Merlin Resurrection");
            EnsureEntry(SceneBackgroundSlot.LegendCodex, "Legends Codex");
        }

        void EnsureEntry(SceneBackgroundSlot slot, string label)
        {
            for (int i = 0; i < entries.Count; i++)
            {
                if (entries[i] != null && entries[i].slot == slot)
                {
                    if (string.IsNullOrEmpty(entries[i].displayName))
                        entries[i].displayName = label;
                    return;
                }
            }
            entries.Add(new SceneBackgroundEntry { slot = slot, displayName = label });
        }

        public SceneBackgroundEntry GetEntry(SceneBackgroundSlot slot)
        {
            EnsureDefaultEntries();
            for (int i = 0; i < entries.Count; i++)
            {
                if (entries[i] != null && entries[i].slot == slot)
                    return entries[i];
            }
            return null;
        }

        public bool HasAssignment(SceneBackgroundSlot slot)
        {
            var e = GetEntry(slot);
            return e != null && (e.imageSprite != null || e.videoClip != null
                                 || !string.IsNullOrEmpty(e.imageAssetPath)
                                 || !string.IsNullOrEmpty(e.videoAssetPath));
        }
    }
}
