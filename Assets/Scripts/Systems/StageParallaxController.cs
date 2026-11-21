using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace ArthurianAutobattler
{
    /// <summary>
    /// Master Spec Phase 4 — parallax scroll/sway + atmosphere FX from StageLayoutProfile.
    /// Attach to Church stage root and/or Churchyard/Battlefield arena root.
    /// Also hosts a 0.15s micro-screen-shake used on Legend fuse, boss wave, and fatal blows.
    /// </summary>
    [DisallowMultipleComponent]
    public class StageParallaxController : MonoBehaviour
    {
        public static StageParallaxController Instance { get; private set; }

        [Tooltip("Defaults to Resources/StageLayoutProfile.")]
        public StageLayoutProfile profile;

        readonly List<LayerRuntime> layers = new List<LayerRuntime>();
        Transform layerRoot;
        ParticleSystem rainFx;
        Image lightningFlash;
        readonly List<Component> candleLights = new List<Component>();
        readonly List<SpriteRenderer> candleSprites = new List<SpriteRenderer>();

        Vector3 shakeOrigin;
        bool shaking;
        float shakeTimer;
        const float ShakeDuration = 0.15f;
        const float ShakeMagnitude = 0.12f;

        class LayerRuntime
        {
            public StageParallaxLayer data;
            public Transform transform;
            public SpriteRenderer sr;
            public RawImage uiImage;
            public Vector3 basePos;
            public Vector2 uvOffset;
            public Material runtimeMat;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Bootstrap()
        {
            // Ensure at least one controller exists near town / battlefield when Play starts.
            if (Object.FindFirstObjectByType<StageParallaxController>() != null) return;
            var host = new GameObject("StageParallaxController");
            host.AddComponent<StageParallaxController>();
        }

        void Awake()
        {
            if (Instance == null) Instance = this;
            if (profile == null) profile = StageLayoutProfile.Load();
            shakeOrigin = transform.localPosition;
            RebuildFromProfile();
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
            foreach (var layer in layers)
            {
                if (layer.runtimeMat != null)
                    Destroy(layer.runtimeMat);
            }
        }

        void Update()
        {
            float dt = Time.deltaTime;
            TickLayers(dt);
            TickCandles(dt);
            TickLightning(dt);
            TickShake(dt);
        }

        /// <summary>0.15s micro-screen-shake — Legend fuse / boss wave / fatal blow.</summary>
        public static void MicroShake()
        {
            var c = Instance ?? Object.FindFirstObjectByType<StageParallaxController>();
            if (c == null)
            {
                var cam = Camera.main;
                if (cam != null)
                {
                    c = cam.GetComponent<StageParallaxController>();
                    if (c == null) c = cam.gameObject.AddComponent<StageParallaxController>();
                }
            }
            c?.BeginShake();
            // Also nudge every other controller in scene (church + churchyard).
            foreach (var other in Object.FindObjectsByType<StageParallaxController>(FindObjectsSortMode.None))
            {
                if (other != null && other != c) other.BeginShake();
            }
        }

        public void BeginShake()
        {
            if (!shaking) shakeOrigin = transform.localPosition;
            shaking = true;
            shakeTimer = ShakeDuration;
        }

        public void RebuildFromProfile()
        {
            profile ??= StageLayoutProfile.Load();
            if (profile == null) return;
            profile.EnsureSlotCounts();

            if (layerRoot != null) Destroy(layerRoot.gameObject);
            layers.Clear();
            candleLights.Clear();
            candleSprites.Clear();

            layerRoot = new GameObject("ParallaxLayers").transform;
            layerRoot.SetParent(transform, false);
            layerRoot.SetAsFirstSibling();

            if (profile.parallaxLayers != null)
            {
                for (int i = 0; i < profile.parallaxLayers.Count; i++)
                {
                    var data = profile.parallaxLayers[i];
                    if (data == null) continue;
                    SpawnLayer(data, i);
                }
            }

            ConfigureRain(profile.enableRainFX);
            ConfigureLightning(profile.enableLightningFlashes);
            ConfigureCandles(profile.enableCandleFlicker);
        }

        void SpawnLayer(StageParallaxLayer data, int index)
        {
            var go = new GameObject(string.IsNullOrEmpty(data.layerName) ? $"Parallax_{index}" : data.layerName);
            go.transform.SetParent(layerRoot, false);
            var rt = new LayerRuntime
            {
                data = data,
                transform = go.transform,
                basePos = new Vector3(data.scrollSpeed.x * 0f, data.scrollSpeed.y * 0f, 0f)
            };

            // Prefer SpriteRenderer for world stages; fall back to UI RawImage under a Canvas.
            var canvas = GetComponentInParent<Canvas>();
            if (canvas != null && canvas.renderMode != RenderMode.WorldSpace)
            {
                var img = go.AddComponent<RawImage>();
                img.color = data.layerTint;
                img.raycastTarget = false;
                if (data.layerSprite != null && data.layerSprite.texture != null)
                {
                    img.texture = data.layerSprite.texture;
                    var mat = new Material(Shader.Find("UI/Default"));
                    img.material = mat;
                    rt.runtimeMat = mat;
                }
                var rect = go.GetComponent<RectTransform>() ?? go.AddComponent<RectTransform>();
                rect.anchorMin = Vector2.zero;
                rect.anchorMax = Vector2.one;
                rect.offsetMin = Vector2.zero;
                rect.offsetMax = Vector2.zero;
                rt.uiImage = img;
            }
            else
            {
                var sr = go.AddComponent<SpriteRenderer>();
                sr.sprite = data.layerSprite;
                sr.color = data.layerTint;
                sr.sortingOrder = data.sortingOrder;
                rt.sr = sr;
            }

            go.transform.localPosition = rt.basePos;
            layers.Add(rt);
        }

        void TickLayers(float dt)
        {
            float time = Time.time;
            for (int i = 0; i < layers.Count; i++)
            {
                var layer = layers[i];
                if (layer?.data == null || layer.transform == null) continue;
                var d = layer.data;
                layer.uvOffset += d.scrollSpeed * dt;
                Vector2 sway = new Vector2(
                    Mathf.Sin(time * d.swayFrequency + i) * d.swayAmplitude.x,
                    Mathf.Cos(time * d.swayFrequency * 0.85f + i * 0.7f) * d.swayAmplitude.y);
                Vector3 pos = layer.basePos + new Vector3(sway.x, sway.y, 0f);
                // Position scroll (seamless-ish wrap for world sprites).
                pos.x += Mathf.Repeat(layer.uvOffset.x, 20f) - 10f;
                pos.y += Mathf.Repeat(layer.uvOffset.y, 12f) - 6f;
                layer.transform.localPosition = pos;

                if (layer.uiImage != null && layer.uiImage.texture != null)
                {
                    // UV scroll via uvRect when possible.
                    var uv = layer.uiImage.uvRect;
                    uv.x = Mathf.Repeat(layer.uvOffset.x * 0.05f, 1f);
                    uv.y = Mathf.Repeat(layer.uvOffset.y * 0.05f, 1f);
                    layer.uiImage.uvRect = uv;
                }
            }
        }

        void ConfigureRain(bool enable)
        {
            if (rainFx != null)
            {
                Destroy(rainFx.gameObject);
                rainFx = null;
            }
            if (!enable) return;

            var go = new GameObject("RainFX");
            go.transform.SetParent(transform, false);
            go.transform.localPosition = new Vector3(0f, 4f, 0f);
            rainFx = go.AddComponent<ParticleSystem>();
            var main = rainFx.main;
            main.startLifetime = 0.9f;
            main.startSpeed = 8f;
            main.startSize = 0.05f;
            main.startColor = new Color(0.7f, 0.8f, 0.95f, 0.55f);
            main.maxParticles = 400;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            var emission = rainFx.emission;
            emission.rateOverTime = 120f;
            var shape = rainFx.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(14f, 0.2f, 1f);
            var vel = rainFx.velocityOverLifetime;
            vel.enabled = true;
            vel.x = new ParticleSystem.MinMaxCurve(-0.5f, 0.5f);
            vel.y = new ParticleSystem.MinMaxCurve(-6f, -10f);
            rainFx.Play();
        }

        float lightningTimer = 3f;
        void ConfigureLightning(bool enable)
        {
            if (lightningFlash != null)
            {
                Destroy(lightningFlash.gameObject);
                lightningFlash = null;
            }
            if (!enable) return;

            var canvas = GetComponentInParent<Canvas>();
            if (canvas == null)
            {
                // World flash via SpriteRenderer
                var go = new GameObject("LightningFlash");
                go.transform.SetParent(transform, false);
                var sr = go.AddComponent<SpriteRenderer>();
                sr.sprite = WhiteSprite();
                sr.color = new Color(1f, 1f, 1f, 0f);
                sr.sortingOrder = 500;
                go.transform.localScale = new Vector3(40f, 24f, 1f);
                lightningWorld = sr;
            }
            else
            {
                var go = new GameObject("LightningFlash", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                go.transform.SetParent(canvas.transform, false);
                var rt = go.GetComponent<RectTransform>();
                rt.anchorMin = Vector2.zero;
                rt.anchorMax = Vector2.one;
                rt.offsetMin = Vector2.zero;
                rt.offsetMax = Vector2.zero;
                lightningFlash = go.GetComponent<Image>();
                lightningFlash.color = new Color(1f, 1f, 1f, 0f);
                lightningFlash.raycastTarget = false;
            }
            lightningTimer = Random.Range(2.5f, 5.5f);
        }

        SpriteRenderer lightningWorld;

        void TickLightning(float dt)
        {
            if (lightningFlash == null && lightningWorld == null) return;
            lightningTimer -= dt;
            if (lightningTimer > 0f) return;
            lightningTimer = Random.Range(3f, 7f);
            StartCoroutine(LightningPulse());
        }

        IEnumerator LightningPulse()
        {
            for (int i = 0; i < 2; i++)
            {
                SetLightningAlpha(0.55f);
                yield return new WaitForSeconds(0.04f);
                SetLightningAlpha(0f);
                yield return new WaitForSeconds(0.05f);
            }
            SetLightningAlpha(0.35f);
            yield return new WaitForSeconds(0.06f);
            SetLightningAlpha(0f);
        }

        void SetLightningAlpha(float a)
        {
            if (lightningFlash != null)
            {
                var c = lightningFlash.color;
                c.a = a;
                lightningFlash.color = c;
            }
            if (lightningWorld != null)
            {
                var c = lightningWorld.color;
                c.a = a;
                lightningWorld.color = c;
            }
        }

        void ConfigureCandles(bool enable)
        {
            foreach (var sr in candleSprites)
                if (sr != null) Destroy(sr.gameObject);
            candleSprites.Clear();
            candleLights.Clear();
            if (!enable) return;

            // Up to 4 candle flickers along the stage.
            for (int i = 0; i < 4; i++)
            {
                var go = new GameObject($"Candle_{i}");
                go.transform.SetParent(transform, false);
                go.transform.localPosition = new Vector3(-4.5f + i * 3f, -1.2f, 0f);
                var sr = go.AddComponent<SpriteRenderer>();
                sr.sprite = WhiteSprite();
                sr.color = new Color(1f, 0.72f, 0.35f, 0.85f);
                sr.sortingOrder = 40;
                go.transform.localScale = new Vector3(0.15f, 0.35f, 1f);
                candleSprites.Add(sr);

                // Optional URP Light2D via reflection (no hard asmdef dependency).
                var lightType = System.Type.GetType(
                    "UnityEngine.Rendering.Universal.Light2D, Unity.RenderPipelines.Universal.Runtime");
                if (lightType != null)
                {
                    var light = go.AddComponent(lightType);
                    candleLights.Add(light);
                    TrySetLightFloat(light, "intensity", 0.8f);
                    TrySetLightColor(light, new Color(1f, 0.7f, 0.35f, 1f));
                }
            }
        }

        void TickCandles(float dt)
        {
            float time = Time.time;
            for (int i = 0; i < candleSprites.Count; i++)
            {
                var sr = candleSprites[i];
                if (sr == null) continue;
                float flicker = 0.75f + 0.25f * Mathf.PerlinNoise(time * 3.5f, i * 1.7f);
                var c = sr.color;
                c.a = 0.55f + 0.35f * flicker;
                sr.color = c;
                sr.transform.localScale = new Vector3(0.15f * flicker, 0.35f * flicker, 1f);
                if (i < candleLights.Count && candleLights[i] != null)
                    TrySetLightFloat(candleLights[i], "intensity", 0.5f + 0.6f * flicker);
            }
        }

        void TickShake(float dt)
        {
            if (!shaking) return;
            shakeTimer -= dt;
            if (shakeTimer <= 0f)
            {
                shaking = false;
                transform.localPosition = shakeOrigin;
                return;
            }
            float damp = shakeTimer / ShakeDuration;
            Vector2 offset = Random.insideUnitCircle * ShakeMagnitude * damp;
            transform.localPosition = shakeOrigin + new Vector3(offset.x, offset.y, 0f);
        }

        static void TrySetLightFloat(Component light, string prop, float value)
        {
            if (light == null) return;
            var p = light.GetType().GetProperty(prop);
            if (p != null && p.CanWrite) p.SetValue(light, value);
        }

        static void TrySetLightColor(Component light, Color color)
        {
            if (light == null) return;
            var p = light.GetType().GetProperty("color");
            if (p != null && p.CanWrite) p.SetValue(light, color);
        }

        static Sprite s_white;
        static Sprite WhiteSprite()
        {
            if (s_white != null) return s_white;
            var tex = new Texture2D(4, 4, TextureFormat.RGBA32, false);
            var px = new Color[16];
            for (int i = 0; i < 16; i++) px[i] = Color.white;
            tex.SetPixels(px);
            tex.Apply();
            s_white = Sprite.Create(tex, new Rect(0, 0, 4, 4), new Vector2(0.5f, 0.5f), 4f);
            return s_white;
        }

        /// <summary>Ensure controllers on known church / battlefield roots.</summary>
        public static void EnsureOnStageRoots()
        {
            TryAttach(GameObject.Find("SomersetChurch"));
            TryAttach(GameObject.Find("ChurchStage"));
            TryAttach(GameObject.Find("BattlefieldArena"));
            TryAttach(GameObject.Find("Churchyard"));
            var loop = GameLoopManager.Instance;
            if (loop != null)
            {
                if (loop.townCanvasObj != null) TryAttach(loop.townCanvasObj);
                if (loop.battlefieldArenaObj != null) TryAttach(loop.battlefieldArenaObj);
            }
        }

        static void TryAttach(GameObject go)
        {
            if (go == null) return;
            if (go.GetComponent<StageParallaxController>() == null)
                go.AddComponent<StageParallaxController>();
        }
    }
}
