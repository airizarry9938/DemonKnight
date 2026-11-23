using System;
using System.Collections.Generic;
using UnityEngine;

namespace ArthurianAutobattler
{
    // tip: fix-tipescape-profile-embeds — named MonoScript for Unity SO binding
    /// <summary>One Legend face-portrait slot (Architect Tab 5).</summary>
    [Serializable]
    public class LegendPortraitEntry
    {
        public UnitType unitType;
        public string displayName;
        public Sprite facePortrait;
        public string assetPath;
        [Tooltip("Merlin and other soft slots stay in the list but are not required for playable Codex.")]
        public bool softOptional;
    }

    /// <summary>
    /// Architect legend face portraits + townsfolk pew FACE-ONLY profile pools.
    /// Distinct from ClassSpritePoolProfile (full-body pew/combat avatars). Resources path:
    /// LegendPewPortraitProfile. Never assign these as bodySprite.
    /// </summary>
    [CreateAssetMenu(fileName = "LegendPewPortraitProfile", menuName = "DemonKnight/Legend Pew Portrait Profile")]
    public class LegendPewPortraitProfile : ScriptableObject
    {
        public const string ResourcesPath = "LegendPewPortraitProfile";

        /// <summary>14 playable legends + soft Merlin slot (last).</summary>
        public static readonly UnitType[] CanonicalLegendSlots =
        {
            UnitType.SirKay,
            UnitType.SirBedivere,
            UnitType.LadyGuinevere,
            UnitType.ArchPriestOfGlastonbury,
            UnitType.SirGawain,
            UnitType.SirTristan,
            UnitType.SirLancelot,
            UnitType.KingArthur,
            UnitType.SirGalahad,
            UnitType.SirPercival,
            UnitType.Nimue,
            UnitType.Mordred,
            UnitType.MorganLeFay,
            UnitType.TheGreenKnight,
            UnitType.Merlin, // soft — out of playable Codex pool
        };

        public List<LegendPortraitEntry> legendPortraits = new List<LegendPortraitEntry>();

        [Header("Townsfolk pew FACE profiles only (never bodySprite — distinct from Tab 4 ClassSpritePool)")]
        public List<Sprite> peasantFaceProfiles = new List<Sprite>();
        public List<Sprite> warriorFaceProfiles = new List<Sprite>();
        public List<Sprite> knightFaceProfiles = new List<Sprite>();

        public List<string> peasantFaceAssetPaths = new List<string>();
        public List<string> warriorFaceAssetPaths = new List<string>();
        public List<string> knightFaceAssetPaths = new List<string>();

        [System.NonSerialized] public bool isRuntimeStandIn;

        public static LegendPewPortraitProfile Load()
        {
            LegendPewPortraitProfile p = null;
            try { p = Resources.Load<LegendPewPortraitProfile>(ResourcesPath); }
            catch { /* missing MonoScript GUID/fileID after embed — fall through */ }
            if (p != null)
            {
                p.EnsureSlotsAndLists();
                p.isRuntimeStandIn = false;
                p.RematerializeSpritesFromPaths();
                return p;
            }
            try
            {
                var all = Resources.LoadAll<LegendPewPortraitProfile>("");
                if (all != null)
                {
                    for (int i = 0; i < all.Length; i++)
                    {
                        if (all[i] == null) continue;
                        all[i].EnsureSlotsAndLists();
                        all[i].isRuntimeStandIn = false;
                        all[i].RematerializeSpritesFromPaths();
                        return all[i];
                    }
                }
            }
            catch { /* broken Resources asset / missing script — soft stand-in */ }
            p = CreateInstance<LegendPewPortraitProfile>();
            p.name = "LegendPewPortraitProfile_Runtime";
            p.isRuntimeStandIn = true;
            p.EnsureSlotsAndLists();
            return p;
        }

        /// <summary>Restore Missing face/pool sprites from path backups (editor + Resources).</summary>
        public int RematerializeSpritesFromPaths()
        {
            EnsureSlotsAndLists();
            int n = 0;
            n += ClassSpritePoolProfile.RematerializeList(peasantFaceProfiles, peasantFaceAssetPaths);
            n += ClassSpritePoolProfile.RematerializeList(warriorFaceProfiles, warriorFaceAssetPaths);
            n += ClassSpritePoolProfile.RematerializeList(knightFaceProfiles, knightFaceAssetPaths);
            if (legendPortraits != null)
            {
                for (int i = 0; i < legendPortraits.Count; i++)
                {
                    var e = legendPortraits[i];
                    if (e == null || e.facePortrait != null) continue;
                    if (string.IsNullOrEmpty(e.assetPath)) continue;
                    Sprite sp = ClassSpritePoolProfile.TryLoadSpriteAtPath(e.assetPath);
                    if (sp == null) continue;
                    e.facePortrait = sp;
                    n++;
                }
            }
            return n;
        }

        public void EnsureSlotsAndLists()
        {
            legendPortraits ??= new List<LegendPortraitEntry>();
            peasantFaceProfiles ??= new List<Sprite>();
            warriorFaceProfiles ??= new List<Sprite>();
            knightFaceProfiles ??= new List<Sprite>();
            peasantFaceAssetPaths ??= new List<string>();
            warriorFaceAssetPaths ??= new List<string>();
            knightFaceAssetPaths ??= new List<string>();

            for (int i = 0; i < CanonicalLegendSlots.Length; i++)
            {
                UnitType want = CanonicalLegendSlots[i];
                LegendPortraitEntry found = null;
                for (int j = 0; j < legendPortraits.Count; j++)
                {
                    if (legendPortraits[j] != null && legendPortraits[j].unitType == want)
                    {
                        found = legendPortraits[j];
                        break;
                    }
                }
                if (found == null)
                {
                    found = new LegendPortraitEntry
                    {
                        unitType = want,
                        displayName = DefaultDisplayName(want),
                        softOptional = want == UnitType.Merlin
                    };
                    legendPortraits.Add(found);
                }
                else
                {
                    if (string.IsNullOrEmpty(found.displayName))
                        found.displayName = DefaultDisplayName(want);
                    found.softOptional = want == UnitType.Merlin;
                }
            }
        }

        public static string DefaultDisplayName(UnitType t) => t switch
        {
            UnitType.KingArthur => "King Arthur",
            UnitType.SirLancelot => "Sir Lancelot",
            UnitType.MorganLeFay => "Morgan le Fay",
            UnitType.LadyOfTheLake => "Lady of the Lake",
            UnitType.SirGawain => "Sir Gawain",
            UnitType.SirGalahad => "Sir Galahad",
            UnitType.SirTristan => "Sir Tristan",
            UnitType.SirPercival => "Sir Percival",
            UnitType.SirKay => "Sir Kay",
            UnitType.SirBedivere => "Sir Bedivere",
            UnitType.LadyGuinevere => "Lady Guinevere",
            UnitType.Guinevere => "Lady Guinevere",
            UnitType.ArchPriestOfGlastonbury => "Arch-Priest of Glastonbury",
            UnitType.Priest => "Arch-Priest of Glastonbury",
            UnitType.TheGreenKnight => "The Green Knight",
            UnitType.Merlin => "Merlin (soft)",
            _ => t.ToString()
        };

        /// <summary>Normalize aliases so Guinevere/Priest map onto canonical slots.</summary>
        public static UnitType NormalizeLegendSlot(UnitType type) => type switch
        {
            UnitType.Guinevere => UnitType.LadyGuinevere,
            UnitType.Priest => UnitType.ArchPriestOfGlastonbury,
            _ => type
        };

        public LegendPortraitEntry FindLegendEntry(UnitType type)
        {
            EnsureSlotsAndLists();
            UnitType want = NormalizeLegendSlot(type);
            for (int i = 0; i < legendPortraits.Count; i++)
            {
                var e = legendPortraits[i];
                if (e != null && e.unitType == want) return e;
            }
            return null;
        }

        public Sprite GetLegendFace(UnitType type)
        {
            var e = FindLegendEntry(type);
            return e != null ? e.facePortrait : null;
        }

        public List<Sprite> GetPewFacePool(UnitType type)
        {
            EnsureSlotsAndLists();
            return type switch
            {
                UnitType.Peasant => peasantFaceProfiles,
                UnitType.Warrior => warriorFaceProfiles,
                UnitType.Knight => knightFaceProfiles,
                _ => null
            };
        }

        public int CountUsablePewFaces(UnitType type)
        {
            var pool = GetPewFacePool(type);
            if (pool == null) return 0;
            int n = 0;
            for (int i = 0; i < pool.Count; i++)
                if (pool[i] != null) n++;
            return n;
        }

        public int CountAssignedLegendFaces()
        {
            EnsureSlotsAndLists();
            int n = 0;
            for (int i = 0; i < legendPortraits.Count; i++)
            {
                var e = legendPortraits[i];
                if (e != null && e.facePortrait != null && !e.softOptional) n++;
            }
            return n;
        }
    }
}
