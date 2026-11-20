using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace ArthurianAutobattler
{
    [Serializable]
    public class GrudgeRecord
    {
        public string unitID;
        public int goldPenalty;
    }

    [Serializable]
    public class GrudgeSaveData
    {
        public List<GrudgeRecord> records = new List<GrudgeRecord>();
        /// <summary>Revealed intel tiers by unitID — kept across nights/waves until Wipe.</summary>
        public List<GrudgeRecord> intelRecords = new List<GrudgeRecord>();
    }

    public class GrudgeSaveManager : MonoBehaviour
    {
        public static GrudgeSaveManager Instance;

        private Dictionary<string, int> committedGrudges = new Dictionary<string, int>();
        private Dictionary<string, int> stagedGrudges = new Dictionary<string, int>();
        /// <summary>Scout/dossier tiers remembered by persistent unitID until full wipe.</summary>
        private Dictionary<string, int> revealedIntel = new Dictionary<string, int>();

        /// <summary>Demonic Pacts set this to 2 to double staged grudge deltas.</summary>
        public float grudgeDeltaMultiplier = 1f;

        /// <summary>Hire-price bump when an army recruit dies in battle (next recruitment).</summary>
        public const int ArmyDeathHirePenalty = 15;

        /// <summary>
        /// Living marcher after a WAVE LOSS — higher rehire cost when returned to pews.
        /// Dead marchers already pay <see cref="ArmyDeathHirePenalty"/> via RecordArmyDeath.
        /// </summary>
        public const int MarchDisbandHirePenalty = 15;

        /// <summary>Alias — wave loss living returnee hire bump.</summary>
        public const int MarchLossHirePenalty = MarchDisbandHirePenalty;

        /// <summary>Living marcher after a WAVE WIN — hire discount when returned to pews (negative staging).</summary>
        public const int MarchWinHireDiscount = -10;

        private string SaveFilePath => Path.Combine(Application.persistentDataPath, "grudge_memory.json");

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
                DontDestroyOnLoad(gameObject);
                LoadGrudgesFromDisk();
            }
            else
            {
                Destroy(gameObject);
            }
        }

        /// <summary>Hire modifier (can be negative after a wave win discount).</summary>
        public int GetGrudgeCostPenalty(string unitID)
        {
            return stagedGrudges.TryGetValue(unitID, out int penalty) ? penalty : 0;
        }

        /// <summary>
        /// Hire-price memory: ONLY army members who died raise cost.
        /// partyLostWar no longer taxes survivors (Andrew design).
        /// </summary>
        public void RecordBattleOutcome(string unitID, bool unitDied, bool partyLostWar)
        {
            if (unitDied)
                RecordArmyDeath(unitID);
            // Living recruits: no hire-price change from win/loss alone.
        }

        /// <summary>Army recruit died in battle → +hire cost next time they can be recruited.</summary>
        public void RecordArmyDeath(string unitID)
        {
            if (string.IsNullOrEmpty(unitID)) return;
            ModifyGrudgeStaging(unitID, ArmyDeathHirePenalty);
        }

        public void ModifyGrudgeStaging(string unitID, int delta)
        {
            if (string.IsNullOrEmpty(unitID)) return;
            if (!stagedGrudges.ContainsKey(unitID))
                stagedGrudges[unitID] = 0;
            // Win discounts are negative; do not scale discounts by pact multiplier.
            int scaled = delta < 0
                ? delta
                : Mathf.RoundToInt(delta * Mathf.Max(0.1f, grudgeDeltaMultiplier));
            stagedGrudges[unitID] = Mathf.Clamp(stagedGrudges[unitID] + scaled, -50, 9999);
        }

        /// <summary>Persist scout/dossier unlocks across waves and nights (not cleared on AdvanceNight).</summary>
        public void RememberIntel(string unitID, int tier)
        {
            if (string.IsNullOrEmpty(unitID) || tier <= 0) return;
            int prev = revealedIntel.TryGetValue(unitID, out int t) ? t : 0;
            int next = Mathf.Clamp(Mathf.Max(prev, tier), 0, 3);
            if (next == prev) return;
            revealedIntel[unitID] = next;
            // Soft-save so night/wave reveals survive editor restart (cleared only by Wipe).
            WriteSaveFile();
        }

        public int GetRememberedIntel(string unitID)
        {
            if (string.IsNullOrEmpty(unitID)) return 0;
            return revealedIntel.TryGetValue(unitID, out int t) ? Mathf.Clamp(t, 0, 3) : 0;
        }

        /// <summary>
        /// Called ONLY when a full run finishes (Victory over Nimue or Priced-Out Game Over).
        /// Commits hire grudges; intel memory is also written so nights keep reveals.
        /// </summary>
        public void CommitStagedGrudgesToDisk()
        {
            committedGrudges = new Dictionary<string, int>(stagedGrudges);
            WriteSaveFile();
        }

        private void WriteSaveFile()
        {
            // Use staged (live) hire penalties so intel soft-saves never wipe uncommitted grudges.
            GrudgeSaveData data = new GrudgeSaveData();
            foreach (var kvp in stagedGrudges)
            {
                if (kvp.Value != 0)
                    data.records.Add(new GrudgeRecord { unitID = kvp.Key, goldPenalty = kvp.Value });
            }
            foreach (var kvp in revealedIntel)
            {
                if (kvp.Value > 0)
                    data.intelRecords.Add(new GrudgeRecord { unitID = kvp.Key, goldPenalty = kvp.Value });
            }
            File.WriteAllText(SaveFilePath, JsonUtility.ToJson(data, true));
        }

        public void LoadGrudgesFromDisk()
        {
            committedGrudges.Clear();
            stagedGrudges.Clear();
            revealedIntel.Clear();
            if (!File.Exists(SaveFilePath)) return;

            GrudgeSaveData data = JsonUtility.FromJson<GrudgeSaveData>(File.ReadAllText(SaveFilePath));
            if (data == null) return;
            if (data.records != null)
            {
                foreach (var rec in data.records)
                {
                    if (string.IsNullOrEmpty(rec.unitID)) continue;
                    committedGrudges[rec.unitID] = rec.goldPenalty;
                    stagedGrudges[rec.unitID] = rec.goldPenalty;
                }
            }
            if (data.intelRecords != null)
            {
                foreach (var rec in data.intelRecords)
                {
                    if (string.IsNullOrEmpty(rec.unitID)) continue;
                    revealedIntel[rec.unitID] = Mathf.Clamp(rec.goldPenalty, 0, 3);
                }
            }
        }

        /// <summary>Full game over / memory wipe — clears hire grudges, intel, AND warband autosave.</summary>
        public void WipeAllSavedMemory()
        {
            committedGrudges.Clear();
            stagedGrudges.Clear();
            revealedIntel.Clear();
            if (File.Exists(SaveFilePath)) File.Delete(SaveFilePath);
            WarbandAutosave.Clear();
        }
    }
}
