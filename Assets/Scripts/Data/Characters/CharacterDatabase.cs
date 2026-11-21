using System;
using System.Collections.Generic;
using UnityEngine;

namespace ArthurianAutobattler
{
    /// <summary>
    /// Central registry of CharacterDefinition assets.
    /// Stored at Resources/Data/CharacterDatabase (auto-created by editor seed/collect).
    /// </summary>
    [CreateAssetMenu(fileName = "CharacterDatabase", menuName = "DemonKnight/Character Database", order = 11)]
    public class CharacterDatabase : ScriptableObject
    {
        public const string ResourcesPath = "Data/CharacterDatabase";

        public List<CharacterDefinition> legends = new List<CharacterDefinition>();
        public List<CharacterDefinition> bosses = new List<CharacterDefinition>();
        public List<CharacterDefinition> demons = new List<CharacterDefinition>();

        static CharacterDatabase _cached;

        public static CharacterDatabase Load()
        {
            if (_cached != null) return _cached;
            _cached = Resources.Load<CharacterDatabase>(ResourcesPath);
            return _cached;
        }

        public static void InvalidateCache() => _cached = null;

        public IEnumerable<CharacterDefinition> All()
        {
            if (legends != null) foreach (var c in legends) if (c != null) yield return c;
            if (bosses != null) foreach (var c in bosses) if (c != null) yield return c;
            if (demons != null) foreach (var c in demons) if (c != null) yield return c;
        }

        public CharacterDefinition GetById(string characterId)
        {
            if (string.IsNullOrEmpty(characterId)) return null;
            foreach (var c in All())
            {
                if (c != null && string.Equals(c.characterId, characterId, StringComparison.OrdinalIgnoreCase))
                    return c;
            }
            return null;
        }

        public CharacterDefinition GetByUnitType(UnitType type)
        {
            foreach (var c in All())
            {
                if (c != null && c.unitType == type) return c;
            }
            return null;
        }

        public CharacterDefinition GetBossForWave(int waveNumber)
        {
            waveNumber = Mathf.Max(1, waveNumber);
            CharacterDefinition best = null;
            int bestWave = -1;
            // Exact match first; for wave 10+ prefer highest bossWaveNumber ≤ wave (Nimue=10).
            foreach (var b in bosses)
            {
                if (b == null || b.bossWaveNumber <= 0) continue;
                if (b.bossWaveNumber == waveNumber) return b;
                if (b.bossWaveNumber <= waveNumber && b.bossWaveNumber > bestWave)
                {
                    bestWave = b.bossWaveNumber;
                    best = b;
                }
            }
            // Waves 1–2: no boss scheduled (bossWaveNumber starts at 3).
            if (waveNumber < 3) return null;
            return best;
        }

        public Sprite GetPortrait(UnitType type)
        {
            var def = GetByUnitType(type);
            if (def == null) return null;
            return def.portraitSprite != null ? def.portraitSprite : def.battleSprite;
        }

        public Sprite GetBattleSprite(UnitType type)
        {
            var def = GetByUnitType(type);
            if (def == null) return null;
            return def.battleSprite != null ? def.battleSprite : def.portraitSprite;
        }

        public float GetVisualScale(UnitType type)
        {
            var def = GetByUnitType(type);
            return def != null ? Mathf.Max(0.1f, def.visualScale) : 1f;
        }

        public bool PersistsAcrossWaves(UnitType type)
        {
            var def = GetByUnitType(type);
            if (def != null) return def.persistsAcrossWaves;
            return type == UnitType.Mordred;
        }

        public string DescribeWave(int wave)
        {
            wave = Mathf.Max(1, wave);
            var boss = GetBossForWave(wave);
            if (wave < 3 || boss == null)
                return $"Wave {wave} (Horde)";
            string name = string.IsNullOrEmpty(boss.displayName) ? boss.unitType.ToString() : boss.displayName;
            return $"Wave {wave}: {name}";
        }

        public bool CanFuse(CharacterDefinition def, List<RecruitInstance> party)
        {
            if (def == null || !def.HasFusionRecipe || party == null) return false;
            // Allow multiple of the same legend (e.g. second Priest) whenever ingredients remain.
            // Previously HasUnitType blocked a second fuse until the first was consumed.
            return TryPickIngredients(party, def.fusionRecipe, out _);
        }

        public bool TryFuse(CharacterDefinition def, List<RecruitInstance> party, out RecruitInstance product)
        {
            product = null;
            if (def == null || party == null || !def.HasFusionRecipe) return false;
            if (!TryPickIngredients(party, def.fusionRecipe, out var chosen)) return false;

            foreach (var r in chosen)
                party.Remove(r);

            product = def.CreateLegendRecruit();
            product.fusionIngredients = new List<RecruitInstance>(chosen.Count);
            foreach (var r in chosen)
                product.fusionIngredients.Add(CardFusionSystem.CloneForFusionMemory(r));

            party.Add(product);
            Debug.Log(
                $"[DemonKnight] Fused {product.characterName} via CharacterDatabase: consumed {chosen.Count}, party={party.Count}.");
            return true;
        }

        public List<CharacterDefinition> GetReadyFusions(List<RecruitInstance> party)
        {
            var ready = new List<CharacterDefinition>();
            if (legends == null) return ready;
            foreach (var def in legends)
            {
                if (def != null && CanFuse(def, party))
                    ready.Add(def);
            }
            return ready;
        }

        public static bool TryPickIngredients(
            List<RecruitInstance> party,
            List<FusionIngredientRule> rules,
            out List<RecruitInstance> chosen)
        {
            chosen = new List<RecruitInstance>();
            if (party == null || rules == null || rules.Count == 0) return false;

            var used = new HashSet<RecruitInstance>();
            foreach (var rule in rules)
            {
                if (rule == null || rule.count <= 0) continue;
                int got = 0;
                for (int i = party.Count - 1; i >= 0 && got < rule.count; i--)
                {
                    var r = party[i];
                    if (r == null || used.Contains(r)) continue;
                    if (!rule.Matches(r)) continue;
                    chosen.Add(r);
                    used.Add(r);
                    got++;
                }
                if (got < rule.count)
                {
                    chosen.Clear();
                    return false;
                }
            }
            return chosen.Count > 0;
        }

        static bool HasUnitType(List<RecruitInstance> party, UnitType type)
        {
            if (party == null) return false;
            foreach (var r in party)
                if (r != null && r.unitType == type) return true;
            return false;
        }

        /// <summary>Register a definition into the correct category list (no duplicates).</summary>
        public void Register(CharacterDefinition def)
        {
            if (def == null) return;
            Remove(def);
            switch (def.category)
            {
                case CharacterCategory.Boss:
                    if (bosses == null) bosses = new List<CharacterDefinition>();
                    bosses.Add(def);
                    break;
                case CharacterCategory.Demon:
                    if (demons == null) demons = new List<CharacterDefinition>();
                    demons.Add(def);
                    break;
                default:
                    if (legends == null) legends = new List<CharacterDefinition>();
                    legends.Add(def);
                    break;
            }
        }

        public void Remove(CharacterDefinition def)
        {
            legends?.RemoveAll(c => c == null || c == def);
            bosses?.RemoveAll(c => c == null || c == def);
            demons?.RemoveAll(c => c == null || c == def);
        }

        public void ClearNulls()
        {
            legends?.RemoveAll(c => c == null);
            bosses?.RemoveAll(c => c == null);
            demons?.RemoveAll(c => c == null);
        }
    }
}
