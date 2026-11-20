using System.Collections.Generic;
using UnityEngine;

namespace ArthurianAutobattler
{
    public class RelicManager : MonoBehaviour
    {
        public static RelicManager Instance;

        public readonly HashSet<RelicID> ownedRelics = new HashSet<RelicID>();

        private static readonly RelicID[] DropTable =
        {
            RelicID.ScabbardOfExcalibur,
            RelicID.ChaliceOfTheGrail,
            RelicID.MorganasBlackMirror,
            RelicID.HornOfCamelot,
            RelicID.CursedDiceOfCaerleon
        };

        private void Awake()
        {
            if (Instance == null) Instance = this;
        }

        public bool Has(RelicID id) => ownedRelics.Contains(id);

        /// <summary>New Game — drop all run relics.</summary>
        public void ClearAllRelics()
        {
            ownedRelics.Clear();
            Debug.Log("[DemonKnight] Relics cleared for New Game.");
        }

        public void TryAwardRelicForWave(int battleNumberJustCleared)
        {
            if (battleNumberJustCleared != 2 && battleNumberJustCleared != 4) return;

            List<RelicID> pool = new List<RelicID>();
            foreach (var id in DropTable)
            {
                if (!ownedRelics.Contains(id)) pool.Add(id);
            }

            if (pool.Count == 0) return;
            RelicID pick = pool[Random.Range(0, pool.Count)];
            ownedRelics.Add(pick);
            ApplyImmediateRelicEffects(pick);
            Debug.Log($"[Relic] Awarded {pick}");
        }

        private void ApplyImmediateRelicEffects(RelicID id)
        {
            if (id == RelicID.HornOfCamelot && GameLoopManager.Instance != null)
            {
                GameLoopManager.Instance.townTimeLimit = Mathf.Min(600f, GameLoopManager.Instance.townTimeLimit + 90f);
                RerollThreeRandomTraits();
            }
        }

        private void RerollThreeRandomTraits()
        {
            if (GameLoopManager.Instance == null) return;
            var pool = GameLoopManager.Instance.currentTownPool;
            if (pool == null || pool.Count == 0) return;

            TraitTag[] traits = { TraitTag.Noble, TraitTag.Outlaw, TraitTag.Drunkard, TraitTag.Zealot, TraitTag.Coward, TraitTag.Loyalist };
            for (int n = 0; n < 3 && pool.Count > 0; n++)
            {
                var r = pool[Random.Range(0, pool.Count)];
                r.primaryTrait = traits[Random.Range(0, traits.Length)];
            }
        }

        public void ApplyMorganasMirrorClone(List<RecruitInstance> combatRoster)
        {
            if (!Has(RelicID.MorganasBlackMirror) || combatRoster == null || combatRoster.Count == 0) return;

            RecruitInstance best = null;
            int bestScore = int.MinValue;
            foreach (var r in combatRoster)
            {
                if (r.unitType != UnitType.Peasant) continue;
                int score = r.maxHealth + r.attackDamage * 3;
                if (score > bestScore)
                {
                    bestScore = score;
                    best = r;
                }
            }

            if (best == null) return;
            combatRoster.Add(new RecruitInstance
            {
                unitID = best.unitID + "_mirror",
                characterName = best.characterName + " (Mirror)",
                unitType = best.unitType,
                assignedRole = best.assignedRole,
                maxHealth = best.maxHealth,
                attackDamage = best.attackDamage,
                attackSpeed = best.attackSpeed,
                primaryTrait = best.primaryTrait,
                pewSeatIndex = best.pewSeatIndex,
                intelTierUnlocked = 3,
                canAttackFromBack = best.canAttackFromBack
            });
        }
    }
}
