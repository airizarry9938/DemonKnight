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
        /// <summary>Legacy ★ count — kept so old autosaves still load.</summary>
        public int victoryStars;
        /// <summary>Master Spec veteran rank (synced with victoryStars on load/save).</summary>
        public int veteranRank;
        public int battleSlotIndex;
        /// <summary>True when this row was written by schema 2+ (distinguishes missing isDeployed).</summary>
        public bool hasDeployFlag;
        public bool isDeployed = true;
        public bool isScouted;
        public string hiddenTraitId;
        public int secretArchetype;
        public float potentialMultiplier = 1f;
        public bool traitAwakened;
        public bool hasAscended;
        public int wavesSurvived;
        public int trainedHP;
        public int trainedATK;
        public int trainedSPD;
        public int lastTrainWaveNumber = -1;
        public string modularPoseId;
        public string modularOutfitId;
        public string modularHeadId;
        public string modularWeaponId;
        public float skinTintR, skinTintG, skinTintB, skinTintA;
        public float hairTintR, hairTintG, hairTintB, hairTintA;
        public float armorTintR, armorTintG, armorTintB, armorTintA;
        // NOTE: Do NOT add List<WarbandMemberSave> here — Unity JsonUtility serialization
        // depth cycle (limit 10). Fusion trees use flat fusionParts + ownerUnitId.
    }

    /// <summary>
    /// One flat fusion-ingredient row. ownerUnitId = product unitID that consumed this part.
    /// Nested fusions: a part that is itself a product also appears as owner of deeper parts.
    /// </summary>
    [Serializable]
    public class WarbandFusionPartSave
    {
        public string ownerUnitId;
        public WarbandMemberSave member;
    }

    [Serializable]
    public class WarbandAutosaveData
    {
        /// <summary>2+ = Phase 1–2 fields (veteranRank, deploy, modular ids). Missing → treat as 1.</summary>
        public int schemaVersion = 2;
        public int night = 1;
        public int battleNumber = 1;
        public int playerGold = 250;
        /// <summary>Currently hired party (usually empty after post-March disband).</summary>
        public List<WarbandMemberSave> survivors = new List<WarbandMemberSave>();
        /// <summary>Town veterans with kept stats/upgrades/intel for rehire next night.</summary>
        public List<WarbandMemberSave> veterans = new List<WarbandMemberSave>();
        /// <summary>Flat fusion ingredient snapshots (no nested List self-ref).</summary>
        public List<WarbandFusionPartSave> fusionParts = new List<WarbandFusionPartSave>();
    }

    public static class WarbandAutosave
    {
        private static string FilePath => Path.Combine(Application.persistentDataPath, "warband_autosave.json");
        private const string PrefsNight = "SomersetNight";
        private const string PrefsHasWarband = "SomersetHasWarbandAutosave";
        private const int MaxFusionDepth = 8;

        public static bool HasAutosave()
        {
            return File.Exists(FilePath) && PlayerPrefs.GetInt(PrefsHasWarband, 0) == 1;
        }

        public static void SaveFromLoop(GameLoopManager loop, int playerGold)
        {
            if (loop == null) return;

            var data = new WarbandAutosaveData
            {
                schemaVersion = 2,
                night = Mathf.Max(1, loop.currentNight),
                battleNumber = loop.battleEngine != null ? Mathf.Max(1, loop.battleEngine.currentBattleNumber) : 1,
                playerGold = Mathf.Max(0, playerGold),
                survivors = new List<WarbandMemberSave>(),
                veterans = new List<WarbandMemberSave>(),
                fusionParts = new List<WarbandFusionPartSave>()
            };

            var seen = new HashSet<string>();

            if (loop.recruitedParty != null)
            {
                foreach (var r in loop.recruitedParty)
                {
                    if (r == null || string.IsNullOrEmpty(r.unitID)) continue;
                    data.survivors.Add(ToSaveFlat(r, data.fusionParts));
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
                    data.veterans.Add(ToSaveFlat(r, data.fusionParts));
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
                    $"[DemonKnight] Autosaved night {data.night}: party {data.survivors.Count}, veterans {data.veterans.Count}, fusionParts {data.fusionParts.Count} → {FilePath}");
            }
            catch (Exception e)
            {
                Debug.LogError($"[DemonKnight] Warband autosave failed: {e.Message}");
            }
        }

        /// <summary>
        /// Hired, trained, starred, fused, or fully scouted townsfolk keep progress across nights.
        /// tip: fix-survivor-stats-victory-morale — trained* / ★ must never drop on wave continue.
        /// </summary>
        public static bool IsVeteran(RecruitInstance r)
        {
            if (r == null) return false;
            if (r.intelTierUnlocked >= 3) return true;
            if (r.assignedRole != CombatRole.Unassigned) return true;
            if (RecruitInstance.IsLegendUnit(r.unitType)) return true;
            if (r.trainedHP + r.trainedATK + r.trainedSPD > 0) return true;
            if (r.EffectiveVeteranRank > 0) return true;
            if (r.wavesSurvived > 0) return true;
            if (r.isScouted) return true;
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
                if (data.fusionParts == null) data.fusionParts = new List<WarbandFusionPartSave>();
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
                list.Add(FromSaveFlat(s, data.fusionParts));
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
                list.Add(FromSaveFlat(s, data.fusionParts));
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
            dest.veteranRank = Mathf.Max(dest.EffectiveVeteranRank, src.EffectiveVeteranRank);
            dest.victoryStars = dest.veteranRank;
            dest.battleSlotIndex = src.battleSlotIndex;
            dest.isDeployed = src.isDeployed;
            dest.isScouted = dest.isScouted || src.isScouted;
            if (!string.IsNullOrEmpty(src.hiddenTraitId)) dest.hiddenTraitId = src.hiddenTraitId;
            if (src.potentialMultiplier > dest.potentialMultiplier)
                dest.potentialMultiplier = src.potentialMultiplier;
            if ((int)src.secretArchetype > (int)dest.secretArchetype)
                dest.secretArchetype = src.secretArchetype;
            dest.traitAwakened = dest.traitAwakened || src.traitAwakened;
            dest.hasAscended = dest.hasAscended || src.hasAscended;
            dest.wavesSurvived = Mathf.Max(dest.wavesSurvived, src.wavesSurvived);
            dest.trainedHP = Mathf.Max(dest.trainedHP, src.trainedHP);
            dest.trainedATK = Mathf.Max(dest.trainedATK, src.trainedATK);
            dest.trainedSPD = Mathf.Max(dest.trainedSPD, src.trainedSPD);
            dest.lastTrainWaveNumber = Mathf.Max(dest.lastTrainWaveNumber, src.lastTrainWaveNumber);
            if (!string.IsNullOrEmpty(src.modularPoseId)) dest.modularPoseId = src.modularPoseId;
            if (!string.IsNullOrEmpty(src.modularOutfitId)) dest.modularOutfitId = src.modularOutfitId;
            if (!string.IsNullOrEmpty(src.modularHeadId)) dest.modularHeadId = src.modularHeadId;
            if (!string.IsNullOrEmpty(src.modularWeaponId)) dest.modularWeaponId = src.modularWeaponId;
            if (src.skinTint.a > 0.01f) dest.skinTint = src.skinTint;
            if (src.hairTint.a > 0.01f) dest.hairTint = src.hairTint;
            if (src.armorTint.a > 0.01f) dest.armorTint = src.armorTint;
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

        /// <summary>Scalar snapshot only — never nests List&lt;WarbandMemberSave&gt;.</summary>
        private static WarbandMemberSave ToMemberScalars(RecruitInstance r)
        {
            r.SyncVeteranRankFields();
            r.SyncModularIdsFromAppearance();
            var skin = r.skinTint.a > 0.01f ? r.skinTint : Color.clear;
            var hair = r.hairTint.a > 0.01f ? r.hairTint : Color.clear;
            var armor = r.armorTint.a > 0.01f ? r.armorTint : Color.clear;
            return new WarbandMemberSave
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
                victoryStars = r.EffectiveVeteranRank,
                veteranRank = r.EffectiveVeteranRank,
                battleSlotIndex = r.battleSlotIndex,
                hasDeployFlag = true,
                isDeployed = r.isDeployed,
                isScouted = r.isScouted,
                hiddenTraitId = r.hiddenTraitId ?? "",
                secretArchetype = (int)r.secretArchetype,
                potentialMultiplier = r.potentialMultiplier,
                traitAwakened = r.traitAwakened,
                hasAscended = r.hasAscended,
                wavesSurvived = r.wavesSurvived,
                trainedHP = r.trainedHP,
                trainedATK = r.trainedATK,
                trainedSPD = r.trainedSPD,
                lastTrainWaveNumber = r.lastTrainWaveNumber,
                modularPoseId = r.modularPoseId ?? "",
                modularOutfitId = r.modularOutfitId ?? "",
                modularHeadId = r.modularHeadId ?? "",
                modularWeaponId = r.modularWeaponId ?? "",
                skinTintR = skin.r, skinTintG = skin.g, skinTintB = skin.b, skinTintA = skin.a,
                hairTintR = hair.r, hairTintG = hair.g, hairTintB = hair.b, hairTintA = hair.a,
                armorTintR = armor.r, armorTintG = armor.g, armorTintB = armor.b, armorTintA = armor.a
            };
        }

        private static WarbandMemberSave ToSaveFlat(RecruitInstance r, List<WarbandFusionPartSave> parts)
        {
            var s = ToMemberScalars(r);
            AppendFusionParts(r, parts, depth: 0, visited: new HashSet<string>());
            return s;
        }

        private static void AppendFusionParts(
            RecruitInstance owner,
            List<WarbandFusionPartSave> parts,
            int depth,
            HashSet<string> visited)
        {
            if (owner == null || parts == null) return;
            if (depth >= MaxFusionDepth) return;
            if (owner.fusionIngredients == null || owner.fusionIngredients.Count == 0) return;
            if (!string.IsNullOrEmpty(owner.unitID) && !visited.Add(owner.unitID))
                return; // cycle guard

            foreach (var ing in owner.fusionIngredients)
            {
                if (ing == null) continue;
                parts.Add(new WarbandFusionPartSave
                {
                    ownerUnitId = owner.unitID,
                    member = ToMemberScalars(ing)
                });
                AppendFusionParts(ing, parts, depth + 1, visited);
            }
        }

        private static RecruitInstance FromMemberScalars(WarbandMemberSave s)
        {
            int rank = Mathf.Max(0, Mathf.Max(s.veteranRank, s.victoryStars));
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
                victoryStars = rank,
                veteranRank = rank,
                battleSlotIndex = s.battleSlotIndex,
                isDeployed = !s.hasDeployFlag || s.isDeployed,
                isScouted = s.isScouted || s.intelTierUnlocked >= 1,
                hiddenTraitId = s.hiddenTraitId,
                secretArchetype = (SecretArchetype)s.secretArchetype,
                potentialMultiplier = s.potentialMultiplier > 0.01f ? s.potentialMultiplier : 1f,
                traitAwakened = s.traitAwakened,
                hasAscended = s.hasAscended,
                wavesSurvived = s.wavesSurvived,
                trainedHP = s.trainedHP,
                trainedATK = s.trainedATK,
                trainedSPD = s.trainedSPD,
                lastTrainWaveNumber = s.lastTrainWaveNumber,
                modularPoseId = s.modularPoseId,
                modularOutfitId = s.modularOutfitId,
                modularHeadId = s.modularHeadId,
                modularWeaponId = s.modularWeaponId,
                skinTint = new Color(s.skinTintR, s.skinTintG, s.skinTintB, s.skinTintA),
                hairTint = new Color(s.hairTintR, s.hairTintG, s.hairTintB, s.hairTintA),
                armorTint = new Color(s.armorTintR, s.armorTintG, s.armorTintB, s.armorTintA)
            };
            CardFusionSystem.SanitizeBaseUnitFusionFlags(r);
            r.SyncVeteranRankFields();
            HiddenPotentialSystem.MigrateLegacySpark(r);
            int grudge = GrudgeSaveManager.Instance != null
                ? GrudgeSaveManager.Instance.GetGrudgeCostPenalty(r.unitID) : 0;
            r.RefreshBodyPostureFromTraits(grudge);
            return r;
        }

        private static RecruitInstance FromSaveFlat(
            WarbandMemberSave s,
            List<WarbandFusionPartSave> parts,
            int depth = 0,
            HashSet<string> assembling = null)
        {
            if (s == null) return null;
            assembling ??= new HashSet<string>();
            var r = FromMemberScalars(s);
            if (depth >= MaxFusionDepth || parts == null || parts.Count == 0)
                return r;
            if (!string.IsNullOrEmpty(s.unitID) && !assembling.Add(s.unitID))
                return r; // cycle guard

            List<RecruitInstance> ings = null;
            for (int i = 0; i < parts.Count; i++)
            {
                var part = parts[i];
                if (part == null || part.member == null) continue;
                if (!string.Equals(part.ownerUnitId, s.unitID, StringComparison.Ordinal)) continue;
                ings ??= new List<RecruitInstance>();
                ings.Add(FromSaveFlat(part.member, parts, depth + 1, assembling));
            }
            if (ings != null && ings.Count > 0)
                r.fusionIngredients = ings;
            // Phase 1 invariant: base troops must never stay classified as fused legends.
            CardFusionSystem.SanitizeBaseUnitFusionFlags(r);
            return r;
        }
    }
}
