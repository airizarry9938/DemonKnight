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
    }

    public class GrudgeSaveManager : MonoBehaviour
    {
        public static GrudgeSaveManager Instance;

        private Dictionary<string, int> committedGrudges = new Dictionary<string, int>();
        private Dictionary<string, int> stagedGrudges = new Dictionary<string, int>();

        /// <summary>Demonic Pacts set this to 2 to double staged grudge deltas.</summary>
        public float grudgeDeltaMultiplier = 1f;

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

        public int GetGrudgeCostPenalty(string unitID)
        {
            return stagedGrudges.TryGetValue(unitID, out int penalty) ? Mathf.Max(0, penalty) : 0;
        }

        public void RecordBattleOutcome(string unitID, bool unitDied, bool partyLostWar)
        {
            int delta = 0;
            if (unitDied) delta += 15;
            if (partyLostWar) delta += 25;
            if (!unitDied && !partyLostWar) delta -= 10;
            ModifyGrudgeStaging(unitID, delta);
        }

        public void ModifyGrudgeStaging(string unitID, int delta)
        {
            if (!stagedGrudges.ContainsKey(unitID))
                stagedGrudges[unitID] = 0;
            int scaled = Mathf.RoundToInt(delta * Mathf.Max(0.1f, grudgeDeltaMultiplier));
            stagedGrudges[unitID] = Mathf.Max(0, stagedGrudges[unitID] + scaled);
        }

        /// <summary>
        /// Called ONLY when a full run finishes (Victory over Nimue or Priced-Out Game Over).
        /// </summary>
        public void CommitStagedGrudgesToDisk()
        {
            committedGrudges = new Dictionary<string, int>(stagedGrudges);
            GrudgeSaveData data = new GrudgeSaveData();
            foreach (var kvp in committedGrudges)
            {
                if (kvp.Value > 0)
                    data.records.Add(new GrudgeRecord { unitID = kvp.Key, goldPenalty = kvp.Value });
            }

            File.WriteAllText(SaveFilePath, JsonUtility.ToJson(data, true));
        }

        public void LoadGrudgesFromDisk()
        {
            committedGrudges.Clear();
            stagedGrudges.Clear();
            if (!File.Exists(SaveFilePath)) return;

            GrudgeSaveData data = JsonUtility.FromJson<GrudgeSaveData>(File.ReadAllText(SaveFilePath));
            if (data != null && data.records != null)
            {
                foreach (var rec in data.records)
                {
                    committedGrudges[rec.unitID] = rec.goldPenalty;
                    stagedGrudges[rec.unitID] = rec.goldPenalty;
                }
            }
        }

        public void WipeAllSavedMemory()
        {
            committedGrudges.Clear();
            stagedGrudges.Clear();
            if (File.Exists(SaveFilePath)) File.Delete(SaveFilePath);
        }
    }
}
