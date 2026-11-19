using System.Collections.Generic;
using UnityEngine;

namespace ArthurianAutobattler
{
    public class BattlefieldVisualizer : MonoBehaviour
    {
        public static BattlefieldVisualizer Instance;

        public UnitVisualActor unitActorPrefab;
        public FloatingCombatText combatTextPrefab;
        public Sprite impSprite;
        public Sprite hellhoundSprite;
        public Sprite dreadLordSprite;
        public Sprite bossSprite;

        public Vector2 humanOrigin = new Vector2(-3.5f, 2.0f);
        public Vector2 demonOrigin = new Vector2(3.5f, 2.0f);
        public float rowSpacing = 1.1f;
        public float colOffset = 1.2f;

        private Dictionary<string, UnitVisualActor> activeActors = new Dictionary<string, UnitVisualActor>();

        private void Awake()
        {
            if (Instance == null) Instance = this;
        }

        public void SyncFrontlineVisuals(
            List<CombatUnit> aliveHumans,
            List<CombatUnit> aliveDemons,
            Dictionary<string, Sprite> humanSprites)
        {
            CleanupDeadActors();

            for (int i = 0; i < aliveHumans.Count; i++)
            {
                CombatUnit u = aliveHumans[i];
                if (!activeActors.ContainsKey(u.unitID))
                {
                    UnitVisualActor actor = Instantiate(unitActorPrefab, transform);
                    Sprite art = humanSprites.ContainsKey(u.unitID) ? humanSprites[u.unitID] : null;
                    actor.Initialize(u, art, CalculateSlotPosition(humanOrigin, i, true), false);
                    activeActors[u.unitID] = actor;
                }
            }

            for (int i = 0; i < aliveDemons.Count; i++)
            {
                CombatUnit d = aliveDemons[i];
                if (!activeActors.ContainsKey(d.unitID))
                {
                    UnitVisualActor actor = Instantiate(unitActorPrefab, transform);
                    actor.Initialize(d, GetDemonSprite(d.unitType), CalculateSlotPosition(demonOrigin, i, false), true);
                    activeActors[d.unitID] = actor;
                }
            }
        }

        public void TriggerAttackVisual(CombatUnit attacker, CombatUnit target)
        {
            if (activeActors.TryGetValue(attacker.unitID, out var atk)
                && activeActors.TryGetValue(target.unitID, out var def))
            {
                atk.PlayAttackLunge(def.transform.position);
                def.PlayHitFlash();
            }
        }

        public void SpawnPopup(CombatUnit targetUnit, string text, Color color)
        {
            if (combatTextPrefab == null || targetUnit == null) return;
            if (activeActors.TryGetValue(targetUnit.unitID, out var actor) && actor != null)
            {
                FloatingCombatText popup = Instantiate(combatTextPrefab, actor.transform.position, Quaternion.identity);
                popup.Setup(text, color);
            }
        }

        private Vector3 CalculateSlotPosition(Vector2 origin, int index, bool isLeft)
        {
            int row = index % 3;
            int col = index / 3;
            float xDir = isLeft ? -1f : 1f;
            return new Vector3(origin.x + (col * colOffset * xDir), origin.y - (row * rowSpacing), 0f);
        }

        private Sprite GetDemonSprite(UnitType type)
        {
            if (type.ToString().StartsWith("Boss")) return bossSprite != null ? bossSprite : dreadLordSprite;
            if (type == UnitType.DemonDreadLord) return dreadLordSprite;
            if (type == UnitType.DemonHellhound) return hellhoundSprite;
            return impSprite;
        }

        private void CleanupDeadActors()
        {
            List<string> toRemove = new List<string>();
            foreach (var kvp in activeActors)
            {
                if (kvp.Value == null || !kvp.Value.boundUnit.IsAlive)
                    toRemove.Add(kvp.Key);
            }

            foreach (string k in toRemove) activeActors.Remove(k);
        }
    }
}
