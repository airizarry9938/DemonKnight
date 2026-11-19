using System.Collections.Generic;
using UnityEngine;

namespace ArthurianAutobattler
{
    public class BattlefieldVisualizer : MonoBehaviour
    {
        private static BattlefieldVisualizer _instance;

        /// <summary>
        /// Unity-safe singleton accessor. Destroyed objects are treated as null
        /// (unlike C# ?. which does not use Unity's overloaded ==).
        /// </summary>
        public static BattlefieldVisualizer Instance
        {
            get
            {
                // Unity's overloaded == treats destroyed objects as null; clear the stale ref.
                if (_instance == null)
                {
                    _instance = null;
                    return null;
                }

                return _instance;
            }
            private set => _instance = value;
        }

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

        private readonly Dictionary<string, UnitVisualActor> activeActors = new Dictionary<string, UnitVisualActor>();

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
        }

        private void OnDestroy()
        {
            if (_instance == this)
                _instance = null;
            activeActors.Clear();
        }

        private void OnDisable()
        {
            // Arena is toggled off between town/battle — clear actors so we don't keep stale refs.
            ClearAllActors();
        }

        public void ClearAllActors()
        {
            foreach (var kvp in activeActors)
            {
                if (kvp.Value != null)
                    Destroy(kvp.Value.gameObject);
            }
            activeActors.Clear();
        }

        public void SyncFrontlineVisuals(
            List<CombatUnit> aliveHumans,
            List<CombatUnit> aliveDemons,
            Dictionary<string, Sprite> humanSprites)
        {
            if (this == null || !isActiveAndEnabled) return;
            if (unitActorPrefab == null) return;

            CleanupDeadActors();

            for (int i = 0; i < aliveHumans.Count; i++)
            {
                CombatUnit u = aliveHumans[i];
                if (u == null || string.IsNullOrEmpty(u.unitID)) continue;
                if (activeActors.ContainsKey(u.unitID)) continue;

                UnitVisualActor actor = Instantiate(unitActorPrefab, transform);
                Sprite art = humanSprites != null && humanSprites.ContainsKey(u.unitID)
                    ? humanSprites[u.unitID]
                    : null;
                actor.Initialize(u, art, CalculateSlotPosition(humanOrigin, i, true), false);
                activeActors[u.unitID] = actor;
            }

            for (int i = 0; i < aliveDemons.Count; i++)
            {
                CombatUnit d = aliveDemons[i];
                if (d == null || string.IsNullOrEmpty(d.unitID)) continue;
                if (activeActors.ContainsKey(d.unitID)) continue;

                UnitVisualActor actor = Instantiate(unitActorPrefab, transform);
                actor.Initialize(d, GetDemonSprite(d.unitType), CalculateSlotPosition(demonOrigin, i, false), true);
                activeActors[d.unitID] = actor;
            }
        }

        public void TriggerAttackVisual(CombatUnit attacker, CombatUnit target)
        {
            if (this == null || !isActiveAndEnabled || attacker == null || target == null) return;
            if (activeActors.TryGetValue(attacker.unitID, out var atk)
                && activeActors.TryGetValue(target.unitID, out var def)
                && atk != null && def != null)
            {
                atk.PlayAttackLunge(def.transform.position);
                def.PlayHitFlash();
            }
        }

        public void SpawnPopup(CombatUnit targetUnit, string text, Color color)
        {
            if (this == null || !isActiveAndEnabled) return;
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
                if (kvp.Value == null)
                {
                    toRemove.Add(kvp.Key);
                    continue;
                }

                if (kvp.Value.boundUnit == null || !kvp.Value.boundUnit.IsAlive)
                    toRemove.Add(kvp.Key);
            }

            foreach (string k in toRemove)
                activeActors.Remove(k);
        }
    }
}
