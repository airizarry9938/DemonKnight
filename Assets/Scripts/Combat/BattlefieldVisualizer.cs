using System.Collections.Generic;
using UnityEngine;

namespace ArthurianAutobattler
{
    /// <summary>
    /// World-space battle presentation under Managers (NOT parented under BattlefieldArena
    /// so toggling the arena cannot destroy this singleton).
    ///
    /// Andrew — where to put art:
    /// 1) Background: assign <see cref="battleBackgroundSprite"/> OR place a SpriteRenderer
    ///    named "BattleBackground" under the BattlefieldArena object in the scene.
    ///    Suggested path: Assets/Art/Backgrounds/Battlefield.jpg (Texture Type = Sprite).
    /// 2) Player slots: child Transforms under "PlayerSlots" (Slot_0, Slot_1, …) on the arena
    ///    OR leave empty and positions use <see cref="humanOrigin"/> grid math.
    /// 3) Enemy slots: same under "EnemySlots".
    /// Hierarchy expected (scene builder creates this):
    ///   BattlefieldArena (toggled on March)
    ///     BattleBackground   ← SpriteRenderer (your art)
    ///     PlayerSlots/Slot_0..N
    ///     EnemySlots/Slot_0..N
    ///   Managers/BattlefieldVisualizer  ← this component (prefabs + sprite refs)
    /// </summary>
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

        [Header("Prefabs")]
        public UnitVisualActor unitActorPrefab;
        public FloatingCombatText combatTextPrefab;

        [Header("Demon sprites (fallback if no slot art)")]
        public Sprite impSprite;
        public Sprite hellhoundSprite;
        public Sprite dreadLordSprite;
        public Sprite bossSprite;

        [Header("Battlefield background (Andrew)")]
        [Tooltip("Optional. If set, applied to BattleBackground under BattlefieldArena on battle start.")]
        public Sprite battleBackgroundSprite;
        [Tooltip("Tint used when no sprite is assigned (avoids pure black).")]
        public Color fallbackGroundColor = new Color(0.22f, 0.16f, 0.12f, 1f);

        [Header("Formation origins (used when no PlayerSlots/EnemySlots)")]
        public Vector2 humanOrigin = new Vector2(-3.5f, 2.0f);
        public Vector2 demonOrigin = new Vector2(3.5f, 2.0f);
        public float rowSpacing = 1.1f;
        public float colOffset = 1.2f;

        [Header("Optional explicit slots (drag arena children here)")]
        public Transform[] playerSlots;
        public Transform[] enemySlots;
        public SpriteRenderer battleBackground;

        private readonly Dictionary<string, UnitVisualActor> activeActors = new Dictionary<string, UnitVisualActor>();
        private Transform[] resolvedPlayerSlots;
        private Transform[] resolvedEnemySlots;

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            ResolveArenaBindings();
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

        private void OnEnable()
        {
            ResolveArenaBindings();
            ApplyBackgroundArt();
        }

        /// <summary>Find BattlefieldArena children and apply background sprite/tint.</summary>
        public void ResolveArenaBindings()
        {
            var loop = GameLoopManager.Instance;
            Transform arena = null;
            if (loop != null && loop.battlefieldArenaObj != null)
                arena = loop.battlefieldArenaObj.transform;
            if (arena == null)
            {
                var found = GameObject.Find("BattlefieldArena");
                if (found != null) arena = found.transform;
            }
            if (arena == null) return;

            if (battleBackground == null)
            {
                var bg = arena.Find("BattleBackground");
                if (bg != null) battleBackground = bg.GetComponent<SpriteRenderer>();
            }

            if (playerSlots == null || playerSlots.Length == 0)
                playerSlots = CollectSlots(arena.Find("PlayerSlots"));
            if (enemySlots == null || enemySlots.Length == 0)
                enemySlots = CollectSlots(arena.Find("EnemySlots"));

            resolvedPlayerSlots = playerSlots;
            resolvedEnemySlots = enemySlots;
            ApplyBackgroundArt();
        }

        private static Transform[] CollectSlots(Transform parent)
        {
            if (parent == null || parent.childCount == 0) return null;
            var list = new List<Transform>(parent.childCount);
            for (int i = 0; i < parent.childCount; i++)
                list.Add(parent.GetChild(i));
            return list.ToArray();
        }

        public void ApplyBackgroundArt()
        {
            if (battleBackground == null) return;
            if (battleBackgroundSprite != null)
            {
                battleBackground.sprite = battleBackgroundSprite;
                battleBackground.color = Color.white;
            }
            else if (battleBackground.sprite == null)
            {
                battleBackground.color = fallbackGroundColor;
            }
            // Ensure it draws behind unit actors (sorting).
            battleBackground.sortingOrder = -20;
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

        /// <summary>
        /// Spawn/refresh visual tokens for ALL living units (not only the 6 combat frontliners).
        /// Each gets an HP bar + color block (portrait sprite when available).
        /// </summary>
        public void SyncFrontlineVisuals(
            List<CombatUnit> aliveHumans,
            List<CombatUnit> aliveDemons,
            Dictionary<string, Sprite> humanSprites)
        {
            if (this == null || !isActiveAndEnabled) return;

            ResolveArenaBindings();
            CleanupDeadActors();

            // Prefab optional — runtime UnitVisualActor still builds HP bar + block.
            for (int i = 0; i < (aliveHumans?.Count ?? 0); i++)
            {
                CombatUnit u = aliveHumans[i];
                if (u == null || string.IsNullOrEmpty(u.unitID)) continue;
                if (activeActors.ContainsKey(u.unitID)) continue;

                UnitVisualActor actor = SpawnActor();
                Sprite art = humanSprites != null && humanSprites.ContainsKey(u.unitID)
                    ? humanSprites[u.unitID]
                    : null;
                actor.Initialize(u, art, CalculateSlotPosition(true, i), false);
                activeActors[u.unitID] = actor;
            }

            for (int i = 0; i < (aliveDemons?.Count ?? 0); i++)
            {
                CombatUnit d = aliveDemons[i];
                if (d == null || string.IsNullOrEmpty(d.unitID)) continue;
                if (activeActors.ContainsKey(d.unitID)) continue;

                UnitVisualActor actor = SpawnActor();
                actor.Initialize(d, GetDemonSprite(d.unitType), CalculateSlotPosition(false, i), true);
                activeActors[d.unitID] = actor;
            }
        }

        private UnitVisualActor SpawnActor()
        {
            if (unitActorPrefab != null)
                return Instantiate(unitActorPrefab, transform);
            var go = new GameObject("UnitVisualActor");
            go.transform.SetParent(transform, false);
            return go.AddComponent<UnitVisualActor>();
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

        private Vector3 CalculateSlotPosition(bool isPlayer, int index)
        {
            var slots = isPlayer ? resolvedPlayerSlots : resolvedEnemySlots;
            if (slots != null && index >= 0 && index < slots.Length && slots[index] != null)
                return slots[index].position;

            Vector2 origin = isPlayer ? humanOrigin : demonOrigin;
            int row = index % 3;
            int col = index / 3;
            float xDir = isPlayer ? -1f : 1f;
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
