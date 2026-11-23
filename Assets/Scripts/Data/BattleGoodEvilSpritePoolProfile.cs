using System;
using System.Collections.Generic;
using UnityEngine;

namespace ArthurianAutobattler
{
    // tip: fix-gdt-profile-always-compile — named MonoScript SO only (BattleSpriteSlotEntry lives in GameDataTypes)
    /// <summary>
    /// Architect Tab 6 — Autobattle Good vs Evil battle sprites for UnitVisualActor.
    /// Distinct from Tab 4 pew BODY pools and Tab 5 FACE portraits. Resources path:
    /// BattleGoodEvilSpritePoolProfile. tip: battle-good-evil-sprite-pools
    /// </summary>
    [CreateAssetMenu(fileName = "BattleGoodEvilSpritePoolProfile", menuName = "DemonKnight/Battle Good Evil Sprite Pool Profile")]
    public class BattleGoodEvilSpritePoolProfile : ScriptableObject
    {
        public const string ResourcesPath = "BattleGoodEvilSpritePoolProfile";

        /// <summary>14 playable legends + soft Merlin (same order as Tab 5 CanonicalLegendSlots).</summary>
        public static readonly UnitType[] CanonicalGoodLegendSlots = LegendPewPortraitProfile.CanonicalLegendSlots;

        /// <summary>Wave 3–10+ bosses (lore-boss-wave-order).</summary>
        public static readonly UnitType[] CanonicalBossSlots =
        {
            UnitType.BossBlackKnight,   // Wave 3
            UnitType.BossMeleagant,     // Wave 4
            UnitType.BossKingLot,       // Wave 5
            UnitType.BossVortigern,     // Wave 6
            UnitType.BossAccolon,       // Wave 7
            UnitType.BossQuestingBeast, // Wave 8
            UnitType.BossGreenKnight,   // Wave 9
            UnitType.BossNimue,         // Wave 10+
        };

        [Header("Good — troop battle pools (multi-sprite)")]
        public List<Sprite> peasantBattleSprites = new List<Sprite>();
        public List<Sprite> warriorBattleSprites = new List<Sprite>();
        public List<Sprite> knightBattleSprites = new List<Sprite>();
        public List<Sprite> priestBattleSprites = new List<Sprite>();

        public List<string> peasantBattleAssetPaths = new List<string>();
        public List<string> warriorBattleAssetPaths = new List<string>();
        public List<string> knightBattleAssetPaths = new List<string>();
        public List<string> priestBattleAssetPaths = new List<string>();

        [Header("Good — playable Legend battle slots (single sprite each)")]
        public List<BattleSpriteSlotEntry> goodLegendSlots = new List<BattleSpriteSlotEntry>();

        [Header("Evil — wave-unit battle pools")]
        public List<Sprite> demonImpBattleSprites = new List<Sprite>();
        public List<Sprite> hellhoundBattleSprites = new List<Sprite>();
        public List<Sprite> dreadLordBattleSprites = new List<Sprite>();

        public List<string> demonImpBattleAssetPaths = new List<string>();
        public List<string> hellhoundBattleAssetPaths = new List<string>();
        public List<string> dreadLordBattleAssetPaths = new List<string>();

        [Header("Evil — Wave 3–10+ boss battle slots")]
        public List<BattleSpriteSlotEntry> evilBossSlots = new List<BattleSpriteSlotEntry>();

        [System.NonSerialized] public bool isRuntimeStandIn;

        public static BattleGoodEvilSpritePoolProfile Load()
        {
            BattleGoodEvilSpritePoolProfile p = null;
            try { p = Resources.Load<BattleGoodEvilSpritePoolProfile>(ResourcesPath); }
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
                var all = Resources.LoadAll<BattleGoodEvilSpritePoolProfile>("");
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
            catch { /* broken Resources asset */ }
            p = CreateInstance<BattleGoodEvilSpritePoolProfile>();
            p.name = "BattleGoodEvilSpritePoolProfile_Runtime";
            p.isRuntimeStandIn = true;
            p.EnsureSlotsAndLists();
            return p;
        }

        public void EnsureSlotsAndLists()
        {
            peasantBattleSprites ??= new List<Sprite>();
            warriorBattleSprites ??= new List<Sprite>();
            knightBattleSprites ??= new List<Sprite>();
            priestBattleSprites ??= new List<Sprite>();
            peasantBattleAssetPaths ??= new List<string>();
            warriorBattleAssetPaths ??= new List<string>();
            knightBattleAssetPaths ??= new List<string>();
            priestBattleAssetPaths ??= new List<string>();

            demonImpBattleSprites ??= new List<Sprite>();
            hellhoundBattleSprites ??= new List<Sprite>();
            dreadLordBattleSprites ??= new List<Sprite>();
            demonImpBattleAssetPaths ??= new List<string>();
            hellhoundBattleAssetPaths ??= new List<string>();
            dreadLordBattleAssetPaths ??= new List<string>();

            goodLegendSlots ??= new List<BattleSpriteSlotEntry>();
            evilBossSlots ??= new List<BattleSpriteSlotEntry>();

            EnsureSlotList(goodLegendSlots, CanonicalGoodLegendSlots, softMerlin: true);
            EnsureSlotList(evilBossSlots, CanonicalBossSlots, softMerlin: false);
        }

        static void EnsureSlotList(List<BattleSpriteSlotEntry> list, UnitType[] wantTypes, bool softMerlin)
        {
            for (int i = 0; i < wantTypes.Length; i++)
            {
                UnitType want = wantTypes[i];
                BattleSpriteSlotEntry found = null;
                for (int j = 0; j < list.Count; j++)
                {
                    if (list[j] != null && list[j].unitType == want)
                    {
                        found = list[j];
                        break;
                    }
                }
                if (found == null)
                {
                    found = new BattleSpriteSlotEntry
                    {
                        unitType = want,
                        displayName = DefaultBattleDisplayName(want),
                        softOptional = softMerlin && want == UnitType.Merlin
                    };
                    list.Add(found);
                }
                else
                {
                    if (string.IsNullOrEmpty(found.displayName))
                        found.displayName = DefaultBattleDisplayName(want);
                    if (softMerlin)
                        found.softOptional = want == UnitType.Merlin;
                }
            }
        }

        public static string DefaultBattleDisplayName(UnitType t) => t switch
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
            UnitType.Priest => "Priest",
            UnitType.TheGreenKnight => "The Green Knight",
            UnitType.Merlin => "Merlin (soft)",
            UnitType.DemonImp => "Demon Imp",
            UnitType.DemonHellhound => "Hellhound",
            UnitType.DemonDreadLord => "Dread Lord",
            UnitType.BossBlackKnight => "Black Knight (W3)",
            UnitType.BossMeleagant => "Meleagant (W4)",
            UnitType.BossKingLot => "King Lot (W5)",
            UnitType.BossVortigern => "Vortigern (W6)",
            UnitType.BossAccolon => "Accolon (W7)",
            UnitType.BossQuestingBeast => "Questing Beast (W8)",
            UnitType.BossGreenKnight => "Green Knight Boss (W9)",
            UnitType.BossNimue => "Nimue (W10+)",
            _ => t.ToString()
        };

        public int RematerializeSpritesFromPaths()
        {
            EnsureSlotsAndLists();
            int n = 0;
            n += ClassSpritePoolProfile.RematerializeList(peasantBattleSprites, peasantBattleAssetPaths);
            n += ClassSpritePoolProfile.RematerializeList(warriorBattleSprites, warriorBattleAssetPaths);
            n += ClassSpritePoolProfile.RematerializeList(knightBattleSprites, knightBattleAssetPaths);
            n += ClassSpritePoolProfile.RematerializeList(priestBattleSprites, priestBattleAssetPaths);
            n += ClassSpritePoolProfile.RematerializeList(demonImpBattleSprites, demonImpBattleAssetPaths);
            n += ClassSpritePoolProfile.RematerializeList(hellhoundBattleSprites, hellhoundBattleAssetPaths);
            n += ClassSpritePoolProfile.RematerializeList(dreadLordBattleSprites, dreadLordBattleAssetPaths);
            n += RematerializeSlots(goodLegendSlots);
            n += RematerializeSlots(evilBossSlots);
            return n;
        }

        static int RematerializeSlots(List<BattleSpriteSlotEntry> slots)
        {
            if (slots == null) return 0;
            int n = 0;
            for (int i = 0; i < slots.Count; i++)
            {
                var e = slots[i];
                if (e == null || e.battleSprite != null) continue;
                if (string.IsNullOrEmpty(e.assetPath)) continue;
                Sprite sp = ClassSpritePoolProfile.TryLoadSpriteAtPath(e.assetPath);
                if (sp == null) continue;
                e.battleSprite = sp;
                n++;
            }
            return n;
        }

        public List<Sprite> GetTroopOrWavePool(UnitType type)
        {
            EnsureSlotsAndLists();
            return type switch
            {
                UnitType.Peasant => peasantBattleSprites,
                UnitType.Warrior => warriorBattleSprites,
                UnitType.Knight => knightBattleSprites,
                UnitType.Priest => priestBattleSprites,
                UnitType.DemonImp => demonImpBattleSprites,
                UnitType.DemonHellhound => hellhoundBattleSprites,
                UnitType.DemonDreadLord => dreadLordBattleSprites,
                _ => null
            };
        }

        public List<string> GetTroopOrWavePaths(UnitType type)
        {
            EnsureSlotsAndLists();
            return type switch
            {
                UnitType.Peasant => peasantBattleAssetPaths,
                UnitType.Warrior => warriorBattleAssetPaths,
                UnitType.Knight => knightBattleAssetPaths,
                UnitType.Priest => priestBattleAssetPaths,
                UnitType.DemonImp => demonImpBattleAssetPaths,
                UnitType.DemonHellhound => hellhoundBattleAssetPaths,
                UnitType.DemonDreadLord => dreadLordBattleAssetPaths,
                _ => null
            };
        }

        public BattleSpriteSlotEntry FindLegendSlot(UnitType type)
        {
            EnsureSlotsAndLists();
            // LadyGuinevere / Guinevere alias
            for (int i = 0; i < goodLegendSlots.Count; i++)
            {
                var e = goodLegendSlots[i];
                if (e == null) continue;
                if (e.unitType == type) return e;
                if ((type == UnitType.Guinevere || type == UnitType.LadyGuinevere)
                    && (e.unitType == UnitType.Guinevere || e.unitType == UnitType.LadyGuinevere))
                    return e;
            }
            return null;
        }

        public BattleSpriteSlotEntry FindBossSlot(UnitType type)
        {
            EnsureSlotsAndLists();
            for (int i = 0; i < evilBossSlots.Count; i++)
            {
                var e = evilBossSlots[i];
                if (e != null && e.unitType == type) return e;
            }
            return null;
        }

        public Sprite GetLegendBattleSprite(UnitType type)
        {
            var e = FindLegendSlot(type);
            return e != null ? e.battleSprite : null;
        }

        public Sprite GetBossBattleSprite(UnitType type)
        {
            var e = FindBossSlot(type);
            return e != null ? e.battleSprite : null;
        }

        public int CountUsablePool(UnitType type)
        {
            var pool = GetTroopOrWavePool(type);
            if (pool == null) return 0;
            int n = 0;
            for (int i = 0; i < pool.Count; i++)
                if (pool[i] != null) n++;
            return n;
        }

        public int CountAssignedLegendBattles()
        {
            EnsureSlotsAndLists();
            int n = 0;
            for (int i = 0; i < goodLegendSlots.Count; i++)
            {
                var e = goodLegendSlots[i];
                if (e == null || e.softOptional) continue;
                if (e.battleSprite != null) n++;
            }
            return n;
        }

        public int CountAssignedBossBattles()
        {
            EnsureSlotsAndLists();
            int n = 0;
            for (int i = 0; i < evilBossSlots.Count; i++)
            {
                var e = evilBossSlots[i];
                if (e != null && e.battleSprite != null) n++;
            }
            return n;
        }
    }
}
