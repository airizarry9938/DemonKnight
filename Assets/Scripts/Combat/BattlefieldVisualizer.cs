using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Video;

namespace ArthurianAutobattler
{
    /// <summary>
    /// World-space battle presentation under Managers (NOT parented under BattlefieldArena
    /// so toggling the arena cannot destroy this singleton).
    ///
    /// Andrew — where to put art:
    /// 1) Still background: assign <see cref="battleBackgroundSprite"/> OR place a SpriteRenderer
    ///    named "BattleBackground" under BattlefieldArena.
    ///    Path: Assets/Art/Backgrounds/Battlefield.jpg (Texture Type = Sprite).
    /// 2) Video loop (preferred for fight scene): assign <see cref="battleBackgroundVideo"/> OR
    ///    drop a clip at Assets/Art/Backgrounds/BattlefieldLoop.mp4 and leave the field empty —
    ///    runtime tries Resources/Art/Backgrounds/BattlefieldLoop then StreamingAssets.
    ///    See Assets/Art/Backgrounds/README-BATTLE-VIDEO.txt.
    /// 3) Player slots: child Transforms under "PlayerSlots" (Slot_0, Slot_1, …) on the arena
    ///    OR leave empty and positions use <see cref="humanOrigin"/> grid math.
    /// 4) Enemy slots: same under "EnemySlots".
    /// Hierarchy expected (scene builder creates this):
    ///   BattlefieldArena (toggled on March)
    ///     BattleBackground      ← SpriteRenderer (still art / tint fallback)
    ///     BattleVideoBackground ← Quad + VideoPlayer → RenderTexture (optional)
    ///     PlayerSlots/Slot_0..N
    ///     EnemySlots/Slot_0..N
    ///   Managers/BattlefieldVisualizer  ← this component (prefabs + sprite / video refs)
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

        [Header("Video background (fight scene)")]
        [Tooltip("Drop BattlefieldLoop.mp4 here, or leave empty and use the documented asset path.")]
        public VideoClip battleBackgroundVideo;
        [Tooltip("Resources path without extension (optional). Default: Art/Backgrounds/BattlefieldLoop")]
        public string videoResourcesPath = "Art/Backgrounds/BattlefieldLoop";
        [Tooltip("Created at runtime if null — VideoPlayer renders into this.")]
        public RenderTexture battleVideoRT;
        public int videoRTWidth = 1280;
        public int videoRTHeight = 720;

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
        private VideoPlayer videoPlayer;
        private MeshRenderer videoQuadRenderer;
        private Transform arenaRoot;

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
            StopBattleVideo();
        }

        private void OnEnable()
        {
            ResolveArenaBindings();
            ApplyBackgroundArt();
            ApplyVideoBackground();
        }

        /// <summary>Find BattlefieldArena children and apply background sprite/tint + video.</summary>
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
            arenaRoot = arena;

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
            ApplyVideoBackground();
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

        /// <summary>
        /// Start looping fight-scene video into a RenderTexture on a world quad.
        /// No clip? Still builds the quad path and logs the drop location for Andrew.
        /// </summary>
        public void ApplyVideoBackground()
        {
            if (arenaRoot == null)
            {
                ResolveArenaBindings();
                if (arenaRoot == null) return;
            }

            EnsureVideoQuad(arenaRoot);

            VideoClip clip = ResolveVideoClip();
            string streamingUrl = ResolveStreamingVideoUrl();

            if (clip == null && string.IsNullOrEmpty(streamingUrl))
            {
                Debug.Log(
                    "[DemonKnight] No battle video clip yet. Drop a loop at "
                    + "Assets/Art/Backgrounds/BattlefieldLoop.mp4 (also copy to "
                    + "Assets/Resources/Art/Backgrounds/BattlefieldLoop.mp4 for auto-load), "
                    + "or assign BattlefieldVisualizer.battleBackgroundVideo. "
                    + "Still background / tint remains active.");
                if (videoQuadRenderer != null)
                    videoQuadRenderer.gameObject.SetActive(false);
                return;
            }

            if (battleVideoRT == null)
            {
                battleVideoRT = new RenderTexture(Mathf.Max(640, videoRTWidth), Mathf.Max(360, videoRTHeight), 0);
                battleVideoRT.name = "BattleVideoRT";
            }

            if (videoPlayer == null)
            {
                videoPlayer = gameObject.GetComponent<VideoPlayer>();
                if (videoPlayer == null) videoPlayer = gameObject.AddComponent<VideoPlayer>();
            }

            videoPlayer.playOnAwake = false;
            videoPlayer.isLooping = true;
            videoPlayer.audioOutputMode = VideoAudioOutputMode.None;
            videoPlayer.renderMode = VideoRenderMode.RenderTexture;
            videoPlayer.targetTexture = battleVideoRT;
            videoPlayer.skipOnDrop = true;

            if (clip != null)
            {
                videoPlayer.source = VideoSource.VideoClip;
                videoPlayer.clip = clip;
                videoPlayer.url = string.Empty;
            }
            else
            {
                videoPlayer.source = VideoSource.Url;
                videoPlayer.clip = null;
                videoPlayer.url = streamingUrl;
            }

            if (videoQuadRenderer != null)
            {
                videoQuadRenderer.gameObject.SetActive(true);
                if (videoQuadRenderer.material != null)
                    videoQuadRenderer.material.mainTexture = battleVideoRT;
            }

            // Dim still sprite so video reads as the primary plane.
            if (battleBackground != null)
                battleBackground.color = new Color(0.15f, 0.12f, 0.10f, 0.35f);

            if (!videoPlayer.isPlaying)
                videoPlayer.Play();
        }

        public void StopBattleVideo()
        {
            if (videoPlayer != null && videoPlayer.isPlaying)
                videoPlayer.Stop();
        }

        private VideoClip ResolveVideoClip()
        {
            if (battleBackgroundVideo != null) return battleBackgroundVideo;

            if (!string.IsNullOrEmpty(videoResourcesPath))
            {
                var fromRes = Resources.Load<VideoClip>(videoResourcesPath);
                if (fromRes != null) return fromRes;
            }

            return null;
        }

        private static string ResolveStreamingVideoUrl()
        {
            string streaming = System.IO.Path.Combine(Application.streamingAssetsPath, "BattlefieldLoop.mp4");
            if (!System.IO.File.Exists(streaming)) return null;
            return "file://" + streaming.Replace("\\", "/");
        }

        private void EnsureVideoQuad(Transform arena)
        {
            Transform existing = arena.Find("BattleVideoBackground");
            GameObject quadGo;
            if (existing != null)
            {
                quadGo = existing.gameObject;
            }
            else
            {
                quadGo = GameObject.CreatePrimitive(PrimitiveType.Quad);
                quadGo.name = "BattleVideoBackground";
                quadGo.transform.SetParent(arena, false);
                quadGo.transform.localPosition = new Vector3(0f, 0.2f, 1.5f);
                quadGo.transform.localRotation = Quaternion.identity;
                quadGo.transform.localScale = new Vector3(14f, 8f, 1f);
                var col = quadGo.GetComponent<Collider>();
                if (col != null) Destroy(col);
            }

            videoQuadRenderer = quadGo.GetComponent<MeshRenderer>();
            if (videoQuadRenderer == null) return;

            // Unlit texture material so the RT shows without scene lights.
            Shader sh = Shader.Find("Unlit/Texture");
            if (sh == null) sh = Shader.Find("Sprites/Default");
            if (sh == null) sh = Shader.Find("UI/Default");
            if (videoQuadRenderer.sharedMaterial == null || videoQuadRenderer.sharedMaterial.shader != sh)
            {
                var mat = new Material(sh != null ? sh : Shader.Find("Hidden/InternalErrorShader"));
                mat.name = "BattleVideoMat";
                videoQuadRenderer.material = mat;
            }
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
                if (art == null)
                    art = ResolveBattleSprite(u.unitType);
                float scale = ResolveVisualScale(u.unitType);
                actor.Initialize(u, art, CalculateSlotPosition(true, i), false, scale);
                activeActors[u.unitID] = actor;
            }

            for (int i = 0; i < (aliveDemons?.Count ?? 0); i++)
            {
                CombatUnit d = aliveDemons[i];
                if (d == null || string.IsNullOrEmpty(d.unitID)) continue;
                if (activeActors.ContainsKey(d.unitID)) continue;

                UnitVisualActor actor = SpawnActor();
                Sprite art = ResolveBattleSprite(d.unitType) ?? GetDemonSprite(d.unitType);
                float scale = ResolveVisualScale(d.unitType);
                // Enemies / bosses face left (flipX = true).
                actor.Initialize(d, art, CalculateSlotPosition(false, i), true, scale);
                activeActors[d.unitID] = actor;
            }
        }

        static Sprite ResolveBattleSprite(UnitType type)
        {
            var db = CharacterDatabase.Load();
            return db != null ? db.GetBattleSprite(type) : null;
        }

        static float ResolveVisualScale(UnitType type)
        {
            var db = CharacterDatabase.Load();
            return db != null ? db.GetVisualScale(type) : 1f;
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
