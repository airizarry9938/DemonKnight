using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace ArthurianAutobattler
{
    /// <summary>
    /// Automatic warband + veteran persistence. No Save menu — writes JSON whenever
    /// the party changes, a wave ends, or Merlin advances the timeline.
    /// After March, the party disbands to pews; upgraded veterans (stats/roles/intel)
    /// are stored so the next night can rehire them at higher prices with progress kept.
    /// </summary>
    [Serializable]
    public class WarbandMemberSave
    {
        public string unitID;
        public string characterName;
        public UnitType unitType;
        public CombatRole assignedRole;
        public int maxHealth;
        public int attackDamage;
        public float attackSpeed;
        public int baseHireCost;
        public TraitTag primaryTrait;
        public TraitTag hatedTrait;
        public TraitTag lovedTrait;
        public int intelTierUnlocked;
        public int pewSeatIndex;
        public BodyPosture bodyPosture;
        public bool isSirenEnchantress;
        /// <summary>Train Ranged crossbow — can attack from BACK.</summary>
        public bool canAttackFromBack;
        /// <summary>Wave-victory experience stars (−5g hire each). Run-scoped via autosave.</summary>
        public int victoryStars;
        /// <summary>Pre-fusion ingredients (run-scoped). Wave-end expands these back to pews.</summary>
        public List<WarbandMemberSave> fusionIngredients;
    }

    [Serializable]
    public class WarbandAutosaveData
    {
        public int night = 1;
        public int battleNumber = 1;
        public int playerGold = 250;
        /// <summary>Currently hired party (usually empty after post-March disband).</summary>
        public List<WarbandMemberSave> survivors = new List<WarbandMemberSave>();
        /// <summary>Town veterans with kept stats/upgrades/intel for rehire next night.</summary>
        public List<WarbandMemberSave> veterans = new List<WarbandMemberSave>();
    }

    public static class WarbandAutosave
    {
        private static string FilePath => Path.Combine(Application.persistentDataPath, "warband_autosave.json");
        private const string PrefsNight = "SomersetNight";
        private const string PrefsHasWarband = "SomersetHasWarbandAutosave";

        public static bool HasAutosave()
        {
            return File.Exists(FilePath) && PlayerPrefs.GetInt(PrefsHasWarband, 0) == 1;
        }

        public static void SaveFromLoop(GameLoopManager loop, int playerGold)
        {
            if (loop == null) return;

            var data = new WarbandAutosaveData
            {
                night = Mathf.Max(1, loop.currentNight),
                battleNumber = loop.battleEngine != null ? Mathf.Max(1, loop.battleEngine.currentBattleNumber) : 1,
                playerGold = Mathf.Max(0, playerGold),
                survivors = new List<WarbandMemberSave>(),
                veterans = new List<WarbandMemberSave>()
            };

            var seen = new HashSet<string>();

            if (loop.recruitedParty != null)
            {
                foreach (var r in loop.recruitedParty)
                {
                    if (r == null || string.IsNullOrEmpty(r.unitID)) continue;
                    data.survivors.Add(ToSave(r));
                    seen.Add(r.unitID);
                    GrudgeSaveManager.Instance?.RememberIntel(r.unitID, Mathf.Max(r.intelTierUnlocked, 3));
                }
            }

            // Pew veterans: anyone previously hired / fused / marched keeps stats for rehire.
            if (loop.currentTownPool != null)
            {
                foreach (var r in loop.currentTownPool)
                {
                    if (r == null || string.IsNullOrEmpty(r.unitID) || seen.Contains(r.unitID)) continue;
                    if (!IsVeteran(r)) continue;
                    data.veterans.Add(ToSave(r));
                    seen.Add(r.unitID);
                    GrudgeSaveManager.Instance?.RememberIntel(r.unitID, Mathf.Max(r.intelTierUnlocked, 1));
                }
            }

            try
            {
                File.WriteAllText(FilePath, JsonUtility.ToJson(data, true));
                PlayerPrefs.SetInt(PrefsNight, data.night);
                PlayerPrefs.SetInt(PrefsHasWarband, 1);
                PlayerPrefs.Save();
                Debug.Log(
                    $"[DemonKnight] Autosaved night {data.night}: party {data.survivors.Count}, veterans {data.veterans.Count} → {FilePath}");
            }
            catch (Exception e)
            {
                Debug.LogError($"[DemonKnight] Warband autosave failed: {e.Message}");
            }
        }

        /// <summary>Hired, trained, fused, or fully scouted townsfolk keep progress across nights.</summary>
        public static bool IsVeteran(RecruitInstance r)
        {
            if (r == null) return false;
            if (r.intelTierUnlocked >= 3) return true;
            if (r.assignedRole != CombatRole.Unassigned) return true;
            if (RecruitInstance.IsLegendUnit(r.unitType)) return true;
            return false;
        }

        public static WarbandAutosaveData Load()
        {
            if (!File.Exists(FilePath)) return null;
            try
            {
                var data = JsonUtility.FromJson<WarbandAutosaveData>(File.ReadAllText(FilePath));
                if (data == null) return null;
                if (data.survivors == null) data.survivors = new List<WarbandMemberSave>();
                if (data.veterans == null) data.veterans = new List<WarbandMemberSave>();
                return data;
            }
            catch (Exception e)
            {
                Debug.LogError($"[DemonKnight] Warband autoload failed: {e.Message}");
                return null;
            }
        }

        public static List<RecruitInstance> ToRecruitList(WarbandAutosaveData data)
        {
            var list = new List<RecruitInstance>();
            if (data?.survivors == null) return list;
            foreach (var s in data.survivors)
            {
                if (s == null || string.IsNullOrEmpty(s.unitID)) continue;
                list.Add(FromSave(s));
            }
            return list;
        }

        public static List<RecruitInstance> ToVeteranList(WarbandAutosaveData data)
        {
            var list = new List<RecruitInstance>();
            if (data?.veterans == null) return list;
            foreach (var s in data.veterans)
            {
                if (s == null || string.IsNullOrEmpty(s.unitID)) continue;
                list.Add(FromSave(s));
            }
            return list;
        }

        /// <summary>Overlay / insert saved veterans into the generated town pool (stats kept for rehire).</summary>
        public static void MergeVeteransIntoTown(List<RecruitInstance> townPool, List<RecruitInstance> veterans)
        {
            if (townPool == null || veterans == null || veterans.Count == 0) return;
            foreach (var v in veterans)
            {
                if (v == null || string.IsNullOrEmpty(v.unitID)) continue;
                int idx = townPool.FindIndex(t => t != null && t.unitID == v.unitID);
                if (idx >= 0)
                {
                    var dest = townPool[idx];
                    ApplyVeteranStats(v, dest);
                }
                else
                {
                    // Fused legends / unique IDs not in the RNG pew list — seat them in town.
                    townPool.Add(v);
                }
            }
        }

        public static void ApplyVeteranStats(RecruitInstance src, RecruitInstance dest)
        {
            if (src == null || dest == null) return;
            dest.characterName = src.characterName;
            dest.unitType = src.unitType;
            dest.assignedRole = src.assignedRole;
            dest.maxHealth = src.maxHealth;
            dest.attackDamage = src.attackDamage;
            dest.attackSpeed = src.attackSpeed;
            dest.baseHireCost = src.baseHireCost;
            dest.primaryTrait = src.primaryTrait;
            dest.hatedTrait = src.hatedTrait;
            dest.lovedTrait = src.lovedTrait;
            dest.intelTierUnlocked = Mathf.Max(dest.intelTierUnlocked, src.intelTierUnlocked, 3);
            dest.bodyPosture = src.bodyPosture;
            dest.isSirenEnchantress = src.isSirenEnchantress;
            dest.canAttackFromBack = src.canAttackFromBack || dest.canAttackFromBack;
            dest.victoryStars = Mathf.Max(dest.victoryStars, src.victoryStars);
            if (src.pewSeatIndex > 0) dest.pewSeatIndex = src.pewSeatIndex;
            if (src.randomAvatar != null) dest.randomAvatar = src.randomAvatar;
            if (src.headSprite != null) dest.headSprite = src.headSprite;
            if (src.IsFusedLegend)
                dest.fusionIngredients = CardFusionSystem.CloneForFusionMemory(src)?.fusionIngredients;
            int grudge = GrudgeSaveManager.Instance != null
                ? GrudgeSaveManager.Instance.GetGrudgeCostPenalty(dest.unitID) : 0;
            dest.RefreshBodyPostureFromTraits(grudge);
        }

        public static void Clear()
        {
            try
            {
                if (File.Exists(FilePath)) File.Delete(FilePath);
            }
            catch { /* ignore */ }
            PlayerPrefs.SetInt(PrefsHasWarband, 0);
            PlayerPrefs.Save();
        }

        private static string SlotPath(int slot) =>
            Path.Combine(Application.persistentDataPath, $"warband_slot_{Mathf.Clamp(slot, 1, 3)}.json");

        private static string SlotPrefsKey(int slot) => $"SomersetSlot{Mathf.Clamp(slot, 1, 3)}";

        public static bool HasSlot(int slot)
        {
            slot = Mathf.Clamp(slot, 1, 3);
            return File.Exists(SlotPath(slot)) && PlayerPrefs.GetInt(SlotPrefsKey(slot), 0) == 1;
        }

        public static string DescribeSlot(int slot)
        {
            slot = Mathf.Clamp(slot, 1, 3);
            if (!HasSlot(slot)) return $"Slot {slot}: empty";
            try
            {
                var data = JsonUtility.FromJson<WarbandAutosaveData>(File.ReadAllText(SlotPath(slot)));
                if (data == null) return $"Slot {slot}: empty";
                int vets = data.veterans != null ? data.veterans.Count : 0;
                int party = data.survivors != null ? data.survivors.Count : 0;
                return $"Slot {slot}: Night {data.night} | {data.playerGold}g | vets {vets} party {party}";
            }
            catch
            {
                return $"Slot {slot}: (unreadable)";
            }
        }

        public static bool CopyAutosaveToSlot(int slot)
        {
            slot = Mathf.Clamp(slot, 1, 3);
            if (!File.Exists(FilePath)) return false;
            try
            {
                File.Copy(FilePath, SlotPath(slot), overwrite: true);
                PlayerPrefs.SetInt(SlotPrefsKey(slot), 1);
                PlayerPrefs.Save();
                return true;
            }
            catch (Exception e)
            {
                Debug.LogError($"[DemonKnight] Save slot {slot} failed: {e.Message}");
                return false;
            }
        }

        public static bool LoadSlotIntoAutosave(int slot)
        {
            slot = Mathf.Clamp(slot, 1, 3);
            if (!HasSlot(slot)) return false;
            try
            {
                File.Copy(SlotPath(slot), FilePath, overwrite: true);
                PlayerPrefs.SetInt(PrefsHasWarband, 1);
                PlayerPrefs.Save();
                return true;
            }
            catch (Exception e)
            {
                Debug.LogError($"[DemonKnight] Load slot {slot} failed: {e.Message}");
                return false;
            }
        }

        private static WarbandMemberSave ToSave(RecruitInstance r, int depth = 0)
        {
            var s = new WarbandMemberSave
            {
                unitID = r.unitID,
                characterName = r.characterName,
                unitType = r.unitType,
                assignedRole = r.assignedRole,
                maxHealth = r.maxHealth,
                attackDamage = r.attackDamage,
                attackSpeed = r.attackSpeed,
                baseHireCost = r.baseHireCost,
                primaryTrait = r.primaryTrait,
                hatedTrait = r.hatedTrait,
                lovedTrait = r.lovedTrait,
                intelTierUnlocked = Mathf.Max(r.intelTierUnlocked, 3),
                pewSeatIndex = r.pewSeatIndex,
                bodyPosture = r.bodyPosture,
                isSirenEnchantress = r.isSirenEnchantress,
                canAttackFromBack = r.canAttackFromBack,
                victoryStars = Mathf.Max(0, r.victoryStars)
            };
            if (depth < 6 && r.fusionIngredients != null && r.fusionIngredients.Count > 0)
            {
                s.fusionIngredients = new List<WarbandMemberSave>(r.fusionIngredients.Count);
                foreach (var ing in r.fusionIngredients)
                {
                    if (ing != null)
                        s.fusionIngredients.Add(ToSave(ing, depth + 1));
                }
            }
            return s;
        }

        private static RecruitInstance FromSave(WarbandMemberSave s, int depth = 0)
        {
            var r = new RecruitInstance
            {
                unitID = s.unitID,
                characterName = s.characterName,
                unitType = s.unitType,
                assignedRole = s.assignedRole,
                maxHealth = s.maxHealth,
                attackDamage = s.attackDamage,
                attackSpeed = s.attackSpeed,
                baseHireCost = s.baseHireCost,
                primaryTrait = s.primaryTrait,
                hatedTrait = s.hatedTrait,
                lovedTrait = s.lovedTrait,
                intelTierUnlocked = Mathf.Max(s.intelTierUnlocked, 3),
                pewSeatIndex = s.pewSeatIndex,
                bodyPosture = s.bodyPosture,
                isSirenEnchantress = s.isSirenEnchantress,
                canAttackFromBack = s.canAttackFromBack,
                victoryStars = Mathf.Max(0, s.victoryStars)
            };
            if (depth < 6 && s.fusionIngredients != null && s.fusionIngredients.Count > 0)
            {
                r.fusionIngredients = new List<RecruitInstance>(s.fusionIngredients.Count);
                foreach (var ing in s.fusionIngredients)
                {
                    if (ing != null)
                        r.fusionIngredients.Add(FromSave(ing, depth + 1));
                }
            }
            int grudge = GrudgeSaveManager.Instance != null
                ? GrudgeSaveManager.Instance.GetGrudgeCostPenalty(r.unitID) : 0;
            r.RefreshBodyPostureFromTraits(grudge);
            return r;
        }
    }
}
