using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using TMPro;
using System.Collections;
using System.Collections.Generic;

namespace ArthurianAutobattler
{
    /// <summary>
    /// Pew seat: single Sprite on Body (no paperdoll Outfit/Head/Prop stack).
    /// Driven by <see cref="TownsfolkAppearance"/>.poseIndex → ModularSpriteLibrary.basePoses.
    /// </summary>
    public class PaperDollSeatView : MonoBehaviour
    {
        public Image bodyLayer;
        public Image outfitLayer;
        public Image headLayer;
        public Image propLayer;
        public Image moodLayer;
        public Button seatButton;
        public TextMeshProUGUI seatLabel;
        public RecruitInstance Bound { get; private set; }

        static bool _loggedFirstBind;

        /// <summary>Legacy: left-click = select. Prefer the hover/RMB overload.</summary>
        public void Bind(RecruitInstance recruit, System.Action<RecruitInstance> onClick) =>
            Bind(
                recruit,
                onLeftClick: () => onClick?.Invoke(recruit),
                onHover: null,
                onRightClick: () => onClick?.Invoke(recruit));

        /// <summary>
        /// Hover → bottom dialogue only (no lock). RMB → selection menu. LMB optional.
        /// </summary>
        public void Bind(
            RecruitInstance recruit,
            Action onLeftClick,
            Action onHover,
            Action onRightClick)
            => Bind(recruit, onLeftClick, onHover, onRightClick, useLibraryArt: true);

        /// <param name="useLibraryArt">
        /// False = church pews: caller paints body via class pool / StageLayout assign-guide
        /// (no library / no clip paint). Prefer <see cref="ClassSpritePoolApplier"/> then
        /// StageLayoutProfile previewOrOverrideSprite fallback.
        /// </param>
        public void Bind(
            RecruitInstance recruit,
            Action onLeftClick,
            Action onHover,
            Action onRightClick,
            bool useLibraryArt)
        {
            Bound = recruit;
            var layers = EnsureModularLayers();
            HideNonModularImages(layers);

            if (recruit != null)
            {
                if (recruit.appearance == null && useLibraryArt)
                {
                    var lib = TownsfolkVisualApplier.Library;
                    recruit.appearance = TownsfolkAppearance.Generate(
                        recruit.unitType, recruit.unitID, recruit.pewSeatIndex, lib);
                }
                recruit.appearance?.BindSeatSalt(recruit.pewSeatIndex);
            }

            if (useLibraryArt)
            {
                TownsfolkVisualApplier.Apply(recruit?.appearance, layers, recruit);
            }
            else
            {
                if (layers.body != null)
                {
                    layers.body.sprite = null;
                    layers.body.color = new Color(0.12f, 0.10f, 0.09f, 0.40f);
                    layers.body.enabled = true;
                }
                if (layers.outfit != null) { layers.outfit.sprite = null; layers.outfit.enabled = false; }
                if (layers.head != null) { layers.head.sprite = null; layers.head.enabled = false; }
                if (layers.prop != null) { layers.prop.sprite = null; layers.prop.enabled = false; }
            }

            if (!_loggedFirstBind && recruit != null)
            {
                _loggedFirstBind = true;
                string bodyName = layers.body != null && layers.body.sprite != null
                    ? layers.body.sprite.name : "NULL";
                Debug.Log(
                    $"[DemonKnight] First pew Bind '{recruit.characterName}' useLibraryArt={useLibraryArt} body={bodyName}");
            }

            if (moodLayer != null)
            {
                moodLayer.sprite = recruit != null ? recruit.moodOverlaySprite : null;
                moodLayer.enabled = recruit != null && recruit.moodOverlaySprite != null;
                moodLayer.transform.SetAsLastSibling();
            }

            if (seatLabel != null)
            {
                bool hasArt = layers.body != null && layers.body.sprite != null;
                if (recruit != null && recruit.isSirenEnchantress) seatLabel.text = "♪";
                else if (!hasArt && recruit != null) seatLabel.text = recruit.unitType.ToString()[..1];
                else seatLabel.text = "";
            }

            if (seatButton == null) seatButton = GetComponent<Button>() ?? gameObject.AddComponent<Button>();
            seatButton.interactable = true;
            if (seatButton.targetGraphic == null && layers.body != null)
                seatButton.targetGraphic = layers.body;
            var colors = seatButton.colors;
            colors.highlightedColor = new Color(0.95f, 0.9f, 0.55f, 1f);
            colors.selectedColor = new Color(0.85f, 0.78f, 0.4f, 1f);
            seatButton.colors = colors;
            seatButton.onClick.RemoveAllListeners();
            if (onLeftClick != null)
                seatButton.onClick.AddListener(() => onLeftClick());

            PewSeatHoverHighlight.Ensure(
                gameObject,
                recruit != null,
                onHoverInfo: onHover,
                onRightClick: onRightClick);
            VictoryStarBadge.Apply(transform, recruit);

            var anim = ModularSpriteAnimator.EnsureOn(gameObject);
            if (useLibraryArt)
                anim?.ConfigurePaperDoll(this, recruit);
            else
                anim?.ConfigureAssignedOnlyPaperDoll(this, recruit);
        }

        /// <summary>Force body to Stage Placer–assigned sprite (no library RNG / animator clip).</summary>
        public void ApplyAssignedBodySprite(Sprite sp)
        {
            var layers = EnsureModularLayers();
            HideNonModularImages(layers);
            if (layers.body == null) return;
            layers.body.sprite = sp;
            layers.body.color = Color.white;
            layers.body.enabled = sp != null;
            layers.body.preserveAspect = true;
            if (layers.outfit != null) { layers.outfit.sprite = null; layers.outfit.enabled = false; }
            if (layers.head != null) { layers.head.sprite = null; layers.head.enabled = false; }
            if (layers.prop != null) { layers.prop.sprite = null; layers.prop.enabled = false; }
            var anim = GetComponent<ModularSpriteAnimator>();
            if (anim != null) anim.LockBodySprite(true);
        }

        /// <summary>Solid / empty seat — no library copy.</summary>
        public void ClearBodyToSolid()
        {
            var layers = EnsureModularLayers();
            HideNonModularImages(layers);
            if (layers.body == null) return;
            layers.body.sprite = null;
            layers.body.color = new Color(0.12f, 0.10f, 0.09f, 0.40f);
            layers.body.enabled = true;
            if (layers.outfit != null) { layers.outfit.sprite = null; layers.outfit.enabled = false; }
            if (layers.head != null) { layers.head.sprite = null; layers.head.enabled = false; }
            if (layers.prop != null) { layers.prop.sprite = null; layers.prop.enabled = false; }
            var anim = GetComponent<ModularSpriteAnimator>();
            if (anim != null) anim.LockBodySprite(true);
        }

        public void BindEmpty()
        {
            Bound = null;
            var layers = EnsureModularLayers();
            if (layers.body != null)
            {
                layers.body.sprite = null;
                layers.body.color = new Color(0.12f, 0.10f, 0.09f, 0.40f);
                layers.body.enabled = true;
            }
            if (layers.outfit != null) layers.outfit.enabled = false;
            if (layers.head != null) layers.head.enabled = false;
            if (layers.prop != null) layers.prop.enabled = false;
            if (moodLayer != null) moodLayer.enabled = false;
            if (seatLabel != null) seatLabel.text = "";
            VictoryStarBadge.Clear(transform);
            var anim = GetComponent<ModularSpriteAnimator>();
            if (anim != null) anim.enabled = false;
            if (seatButton != null)
            {
                seatButton.onClick.RemoveAllListeners();
                seatButton.interactable = false;
            }
            PewSeatHoverHighlight.Ensure(gameObject, false, null, null);
        }

        TownsfolkVisualApplier.LayerSet EnsureModularLayers()
        {
            // Migrate legacy single Image on root → Body child when needed.
            if (bodyLayer != null && bodyLayer.transform == transform)
            {
                // Keep reference; EnsureChildLayers will create named children.
                bodyLayer = null;
            }

            var set = TownsfolkVisualApplier.EnsureChildLayers(transform, createIfMissing: true);
            bodyLayer = set.body;
            outfitLayer = set.outfit;
            headLayer = set.head;
            propLayer = set.prop;

            // Root Image (if any) should not fight with layered children.
            var rootImg = GetComponent<Image>();
            if (rootImg != null && rootImg != bodyLayer)
            {
                rootImg.sprite = null;
                rootImg.color = new Color(1f, 1f, 1f, 0f);
                rootImg.raycastTarget = false;
                rootImg.enabled = false;
            }

            if (seatButton == null) seatButton = GetComponent<Button>();
            return set;
        }

        /// <summary>
        /// Prefab avatar buttons often keep a solid-color Image that reads as the old
        /// "box" even after modular Body/Outfit/Head are applied — hide those.
        /// Never strip victory-star / hover-rim chrome (Bind can refresh twice).
        /// </summary>
        void HideNonModularImages(TownsfolkVisualApplier.LayerSet layers)
        {
            foreach (var img in GetComponentsInChildren<Image>(true))
            {
                if (img == null) continue;
                if (img == layers.body || img == layers.outfit || img == layers.head
                    || img == layers.prop || img == moodLayer)
                {
                    img.type = Image.Type.Simple;
                    img.preserveAspect = true;
                    continue;
                }
                // Victory ★ badge + pew hover rim must stay visible across Bind refreshes.
                if (IsProtectedSeatChrome(img))
                    continue;
                img.sprite = null;
                img.enabled = false;
                img.raycastTarget = false;
            }
        }

        static bool IsProtectedSeatChrome(Image img)
        {
            if (img == null) return false;
            Transform t = img.transform;
            while (t != null)
            {
                string n = t.name;
                if (n == VictoryStarBadge.ChildName
                    || n == "StarIcon"
                    || n == "StarBg"
                    || n == "PewHoverRim")
                    return true;
                t = t.parent;
            }
            return false;
        }
    }

    /// <summary>
    /// Embedded in PaperDollSeatView.cs so Force Sync always lands the type with an
    /// AtomicCritical UI file (standalone PewSeatHoverHighlight.cs was missing on Downloads).
    /// </summary>
    [DisallowMultipleComponent]
    public class PewSeatHoverHighlight : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler
    {
        const string RimName = "PewHoverRim";
        static readonly Vector3 HoverScale = new Vector3(1.08f, 1.08f, 1f);
        static readonly Color RimIdle = new Color(0.95f, 0.85f, 0.45f, 0f);
        static readonly Color RimHover = new Color(0.95f, 0.88f, 0.35f, 0.85f);

        Image rim;
        Vector3 baseScale = Vector3.one;
        bool hovering;
        bool menuLocked;
        bool occupied = true;
        Action onHoverInfo;
        Action onRightClick;

        public static PewSeatHoverHighlight Ensure(
            GameObject go,
            bool isOccupied,
            Action onHoverInfo = null,
            Action onRightClick = null)
        {
            if (go == null) return null;
            var h = go.GetComponent<PewSeatHoverHighlight>();
            if (h == null) h = go.AddComponent<PewSeatHoverHighlight>();
            h.Configure(isOccupied, onHoverInfo, onRightClick);
            return h;
        }

        public void Configure(bool isOccupied, Action onHoverInfo = null, Action onRightClick = null)
        {
            occupied = isOccupied;
            this.onHoverInfo = onHoverInfo;
            this.onRightClick = onRightClick;
            if (!hovering && !menuLocked) baseScale = transform.localScale;
            EnsureRim();
            ApplyVisual();
            if (!occupied)
            {
                hovering = false;
                menuLocked = false;
                transform.localScale = baseScale;
            }
        }

        /// <summary>Keep pew/avatar highlight while the RMB unit stats menu is open.</summary>
        public void SetMenuLocked(bool locked)
        {
            menuLocked = locked && occupied;
            ApplyVisual();
        }

        public bool IsMenuLocked => menuLocked;

        void EnsureRim()
        {
            if (rim == null)
            {
                var existing = transform.Find(RimName);
                if (existing != null)
                    rim = existing.GetComponent<Image>();
            }
            if (rim != null)
            {
                rim.enabled = occupied;
                rim.raycastTarget = false;
                return;
            }

            var go = new GameObject(RimName, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(transform, false);
            go.transform.SetAsFirstSibling();
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(-6f, -6f);
            rt.offsetMax = new Vector2(6f, 6f);
            rim = go.GetComponent<Image>();
            rim.color = RimIdle;
            rim.raycastTarget = false;
            rim.sprite = null;
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (!occupied) return;
            hovering = true;
            ApplyVisual();
            // Bottom dialogue only — never opens selection menu / never locks hire select.
            onHoverInfo?.Invoke();
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            hovering = false;
            // Menu lock keeps rim/scale while stats panel is open (RMB toggle).
            ApplyVisual();
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (!occupied) return;
            if (eventData != null && eventData.button == PointerEventData.InputButton.Right)
                onRightClick?.Invoke();
        }

        void OnDisable()
        {
            hovering = false;
            // Keep menuLocked so re-enable after brief disable still shows selection.
            ApplyVisual();
        }

        void ApplyVisual()
        {
            EnsureRim();
            bool lit = occupied && (hovering || menuLocked);
            if (rim != null)
            {
                rim.enabled = occupied;
                rim.color = lit ? RimHover : RimIdle;
            }
            transform.localScale = lit ? HoverScale : baseScale;
        }
    }

    // ===== DA-59 EMBEDDED ModularSpriteAnimator (Force Sync nuclear — do not remove) =====
    /// <summary>
    /// Master Spec Phase 4 — synchronized modular clip playback + procedural fallbacks.
    /// Attach to <see cref="PaperDollSeatView"/> (UI Images) and <see cref="UnitVisualActor"/> (SpriteRenderer).
    /// </summary>
    [DisallowMultipleComponent]
    public class ModularSpriteAnimator : MonoBehaviour
    {
        public enum AnimState { Idle, Attack, Hit, Death }

        static readonly Color UnscoutedTint = new Color(0xA8 / 255f, 0xB0 / 255f, 0xC0 / 255f, 1f);

        Image bodyImg, outfitImg, headImg, propImg;
        SpriteRenderer bodySr, outfitSr, headSr, propSr;
        ModularSpriteLibrary library;
        RecruitInstance boundRecruit;
        bool uiMode;

        AnimState state = AnimState.Idle;
        float stateTime;
        float startOffset;
        Vector3 baseScale = Vector3.one;
        Vector3 baseLocalPos;
        bool baseCaptured;
        Color[] layerBaseColors = new Color[4];
        bool playingReveal;
        Coroutine proceduralRoutine;

        SpriteAnimClip poseClip;
        bool poseMulti;
        /// <summary>When true, never overwrite body Image from library clips (StageLayout assign-only).</summary>
        bool lockBodySprite;

        public static ModularSpriteAnimator EnsureOn(GameObject go)
        {
            if (go == null) return null;
            var a = go.GetComponent<ModularSpriteAnimator>();
            if (a == null) a = go.AddComponent<ModularSpriteAnimator>();
            return a;
        }

        public void LockBodySprite(bool locked) => lockBodySprite = locked;

        /// <summary>Pew assign-only: breath bob OK, no library clip frames on body.</summary>
        public void ConfigureAssignedOnlyPaperDoll(PaperDollSeatView seat, RecruitInstance recruit)
        {
            if (seat == null) return;
            uiMode = true;
            boundRecruit = recruit;
            library = null;
            lockBodySprite = true;
            bodyImg = seat.bodyLayer;
            outfitImg = null;
            headImg = null;
            propImg = null;
            if (seat.outfitLayer != null) { seat.outfitLayer.sprite = null; seat.outfitLayer.enabled = false; }
            if (seat.headLayer != null) { seat.headLayer.sprite = null; seat.headLayer.enabled = false; }
            if (seat.propLayer != null) { seat.propLayer.sprite = null; seat.propLayer.enabled = false; }
            poseClip = null;
            poseMulti = false;
            CaptureBase();
            startOffset = UnityEngine.Random.Range(0f, 1.5f);
            state = AnimState.Idle;
            stateTime = 0f;
            enabled = true;
        }

        public void ConfigurePaperDoll(PaperDollSeatView seat, RecruitInstance recruit)
        {
            if (seat == null) return;
            uiMode = true;
            boundRecruit = recruit;
            lockBodySprite = false;
            library = TownsfolkVisualApplier.Library ?? ModularSpriteLibrary.Load();
            bodyImg = seat.bodyLayer;
            // Single-sprite runtime — never drive Outfit/Head/Prop layers.
            outfitImg = null;
            headImg = null;
            propImg = null;
            if (seat.outfitLayer != null) { seat.outfitLayer.sprite = null; seat.outfitLayer.enabled = false; }
            if (seat.headLayer != null) { seat.headLayer.sprite = null; seat.headLayer.enabled = false; }
            if (seat.propLayer != null) { seat.propLayer.sprite = null; seat.propLayer.enabled = false; }
            CaptureBase();
            ResolveClips();
            startOffset = UnityEngine.Random.Range(0f, 1.5f);
            state = AnimState.Idle;
            stateTime = 0f;
            ApplyUnscoutedTint(immediate: true);
            enabled = true;
        }

        public void ConfigureBattleActor(UnitVisualActor actor)
        {
            if (actor == null) return;
            uiMode = false;
            boundRecruit = null;
            library = ModularSpriteLibrary.Load();
            bodySr = actor.spriteRenderer;
            // Single-sprite battle — ignore Outfit/Head/Prop children.
            outfitSr = null;
            headSr = null;
            propSr = null;
            CaptureBase();
            ResolveClips();
            startOffset = UnityEngine.Random.Range(0f, 1.5f);
            state = AnimState.Idle;
            stateTime = 0f;
            enabled = true;
        }

        public void Play(AnimState next)
        {
            if (next == state && next == AnimState.Idle) return;
            state = next;
            stateTime = 0f;
            if (proceduralRoutine != null)
            {
                StopCoroutine(proceduralRoutine);
                proceduralRoutine = null;
            }
            if (!HasMultiFrameFor(next))
                proceduralRoutine = StartCoroutine(ProceduralRoutine(next));
        }

        /// <summary>Hire reveal: tint → white, 1.08× pop, gold coin burst.</summary>
        public void PlayRecruitReveal()
        {
            if (!isActiveAndEnabled) return;
            if (boundRecruit != null)
            {
                boundRecruit.isScouted = true;
                boundRecruit.intelTierUnlocked = Mathf.Max(boundRecruit.intelTierUnlocked, 1);
            }
            if (proceduralRoutine != null) StopCoroutine(proceduralRoutine);
            proceduralRoutine = StartCoroutine(RecruitRevealRoutine());
        }

        void Update()
        {
            if (!baseCaptured) CaptureBase();
            if (playingReveal) return;
            stateTime += Time.unscaledDeltaTime;
            if (state == AnimState.Idle)
                TickIdle(stateTime + startOffset);
            else if (HasMultiFrameFor(state))
                TickClipState(state, stateTime);
        }

        void TickIdle(float t)
        {
            if (poseMulti)
            {
                // Single Sprite category clip on body only.
                ApplyClipFrame(poseClip, poseMulti, 0, t);
            }
            else
            {
                // Procedural breath bob 1.0 ↔ 1.03
                float bob = 1f + 0.03f * Mathf.Sin(t * 2.2f);
                transform.localScale = new Vector3(baseScale.x, baseScale.y * bob, baseScale.z);
            }
        }

        void TickClipState(AnimState s, float t)
        {
            string name = ClipName(s);
            var p = FindSpriteClip(name) ?? poseClip;
            ApplyClipFrame(p, p != null && p.FrameCount > 1, 0, t);
            float dur = EstimateClipDuration(p);
            if (!IsLooping(s) && t >= dur)
                Play(AnimState.Idle);
        }

        IEnumerator ProceduralRoutine(AnimState s)
        {
            switch (s)
            {
                case AnimState.Attack:
                {
                    // Forward X-lunge +18px (UI) / world units for battle.
                    Vector3 peak = baseLocalPos + new Vector3(uiMode ? 18f : 0.35f, 0f, 0f);
                    float t = 0f;
                    while (t < 0.1f)
                    {
                        transform.localPosition = Vector3.Lerp(baseLocalPos, peak, t / 0.1f);
                        t += Time.deltaTime;
                        yield return null;
                    }
                    t = 0f;
                    while (t < 0.12f)
                    {
                        transform.localPosition = Vector3.Lerp(peak, baseLocalPos, t / 0.12f);
                        t += Time.deltaTime;
                        yield return null;
                    }
                    transform.localPosition = baseLocalPos;
                    Play(AnimState.Idle);
                    break;
                }
                case AnimState.Hit:
                {
                    FlashLayers(Color.white);
                    Vector3 knock = baseLocalPos + new Vector3(uiMode ? -10f : -0.2f, 0f, 0f);
                    transform.localPosition = knock;
                    yield return new WaitForSeconds(0.07f);
                    RestoreLayerColors();
                    float t = 0f;
                    while (t < 0.12f)
                    {
                        transform.localPosition = Vector3.Lerp(knock, baseLocalPos, t / 0.12f);
                        t += Time.deltaTime;
                        yield return null;
                    }
                    transform.localPosition = baseLocalPos;
                    Play(AnimState.Idle);
                    break;
                }
                case AnimState.Death:
                {
                    SpawnBurst(new Color(0.7f, 0.75f, 0.85f, 1f), 10);
                    float t = 0f;
                    Quaternion start = transform.localRotation;
                    Quaternion tilt = Quaternion.Euler(0f, 0f, uiMode ? -28f : -40f);
                    while (t < 0.35f)
                    {
                        float u = t / 0.35f;
                        transform.localRotation = Quaternion.Slerp(start, tilt, u);
                        SetLayersAlpha(1f - u);
                        transform.localScale = Vector3.Lerp(baseScale, baseScale * 0.85f, u);
                        t += Time.deltaTime;
                        yield return null;
                    }
                    SetLayersAlpha(0f);
                    break;
                }
                default:
                    Play(AnimState.Idle);
                    break;
            }
            proceduralRoutine = null;
        }

        IEnumerator RecruitRevealRoutine()
        {
            playingReveal = true;
            SpawnGoldCoinBurst();
            float t = 0f;
            Color from = UnscoutedTint;
            Color to = Color.white;
            Vector3 pop = baseScale * 1.08f;
            while (t < 0.28f)
            {
                float u = t / 0.28f;
                MultiplyLayerTints(Color.Lerp(from, to, u));
                transform.localScale = Vector3.Lerp(baseScale, pop, Mathf.Sin(u * Mathf.PI));
                t += Time.unscaledDeltaTime;
                yield return null;
            }
            MultiplyLayerTints(Color.white);
            transform.localScale = baseScale;
            playingReveal = false;
            proceduralRoutine = null;
            Play(AnimState.Idle);
        }

        void ResolveClips()
        {
            // Architect registers clips under category "Sprite"; accept legacy "Pose".
            poseClip = FindSpriteClip(SpriteAnimClip.Idle);
            poseMulti = poseClip != null && poseClip.FrameCount > 1;
        }

        SpriteAnimClip FindSpriteClip(string clipName)
        {
            if (library == null || string.IsNullOrEmpty(clipName)) return null;
            return library.FindAnimClip("Sprite", clipName)
                   ?? library.FindAnimClip("Pose", clipName);
        }

        bool HasMultiFrameFor(AnimState s)
        {
            var p = FindSpriteClip(ClipName(s));
            return p != null && p.FrameCount > 1;
        }

        static string ClipName(AnimState s) => s switch
        {
            AnimState.Attack => SpriteAnimClip.Attack,
            AnimState.Hit => SpriteAnimClip.Hit,
            AnimState.Death => SpriteAnimClip.Death,
            _ => SpriteAnimClip.Idle
        };

        static bool IsLooping(AnimState s) => s == AnimState.Idle;

        static float EstimateClipDuration(params SpriteAnimClip[] clips)
        {
            float best = 0.25f;
            foreach (var c in clips)
            {
                if (c == null || c.FrameCount <= 0) continue;
                float d = c.FrameCount / Mathf.Max(0.1f, c.framesPerSecond);
                if (d > best) best = d;
            }
            return best;
        }

        void ApplyClipFrame(SpriteAnimClip clip, bool multi, int layer, float t)
        {
            if (!multi || clip == null || clip.FrameCount == 0) return;
            int frame = Mathf.FloorToInt(t * Mathf.Max(0.1f, clip.framesPerSecond));
            if (clip.loop) frame %= clip.FrameCount;
            else frame = Mathf.Min(frame, clip.FrameCount - 1);
            Sprite sp = clip.FrameAt(frame);
            SetLayerSprite(layer, sp);
        }

        void SetLayerSprite(int layer, Sprite sp)
        {
            if (sp == null) return;
            // StageLayout assign-only pews: never re-paint body from library clips.
            if (lockBodySprite && layer == 0) return;
            if (uiMode)
            {
                Image img = LayerImage(layer);
                if (img != null) { img.sprite = sp; img.enabled = true; }
            }
            else
            {
                SpriteRenderer sr = LayerSr(layer);
                if (sr != null) { sr.sprite = sp; sr.enabled = true; }
            }
        }

        Image LayerImage(int i) => i switch { 0 => bodyImg, 1 => outfitImg, 2 => headImg, 3 => propImg, _ => null };
        SpriteRenderer LayerSr(int i) => i switch { 0 => bodySr, 1 => outfitSr, 2 => headSr, 3 => propSr, _ => null };

        void CaptureBase()
        {
            baseScale = transform.localScale;
            if (baseScale == Vector3.zero) baseScale = Vector3.one;
            baseLocalPos = transform.localPosition;
            for (int i = 0; i < 4; i++)
                layerBaseColors[i] = ReadLayerColor(i);
            baseCaptured = true;
        }

        Color ReadLayerColor(int i)
        {
            if (uiMode)
            {
                var img = LayerImage(i);
                return img != null ? img.color : Color.white;
            }
            var sr = LayerSr(i);
            return sr != null ? sr.color : Color.white;
        }

        void WriteLayerColor(int i, Color c)
        {
            if (uiMode)
            {
                var img = LayerImage(i);
                if (img != null) img.color = c;
            }
            else
            {
                var sr = LayerSr(i);
                if (sr != null) sr.color = c;
            }
        }

        void ApplyUnscoutedTint(bool immediate)
        {
            bool scouted = boundRecruit == null
                           || boundRecruit.isScouted
                           || boundRecruit.intelTierUnlocked >= 1;
            Color tint = scouted ? Color.white : UnscoutedTint;
            if (immediate) MultiplyLayerTints(tint);
        }

        void MultiplyLayerTints(Color tint)
        {
            for (int i = 0; i < 4; i++)
            {
                Color baseC = layerBaseColors[i];
                if (baseC.a <= 0.01f && uiMode)
                {
                    // Keep disabled layers untouched.
                    var img = LayerImage(i);
                    if (img == null || !img.enabled) continue;
                }
                Color c = baseC;
                c.r = baseC.r * tint.r;
                c.g = baseC.g * tint.g;
                c.b = baseC.b * tint.b;
                WriteLayerColor(i, c);
            }
        }

        void FlashLayers(Color flash)
        {
            for (int i = 0; i < 4; i++) WriteLayerColor(i, flash);
        }

        void RestoreLayerColors()
        {
            for (int i = 0; i < 4; i++) WriteLayerColor(i, layerBaseColors[i]);
            ApplyUnscoutedTint(immediate: true);
        }

        void SetLayersAlpha(float a)
        {
            for (int i = 0; i < 4; i++)
            {
                Color c = ReadLayerColor(i);
                c.a = a * layerBaseColors[i].a;
                WriteLayerColor(i, c);
            }
        }

        void SpawnGoldCoinBurst()
        {
            SpawnBurst(new Color(1f, 0.84f, 0.2f, 1f), 14);
        }

        void SpawnBurst(Color color, int count)
        {
            var root = new GameObject("DK_FX_Burst");
            root.transform.SetParent(transform.parent != null ? transform.parent : transform, false);
            if (uiMode)
            {
                var rt = root.AddComponent<RectTransform>();
                var selfRt = transform as RectTransform;
                if (selfRt != null)
                {
                    rt.anchorMin = selfRt.anchorMin;
                    rt.anchorMax = selfRt.anchorMax;
                    rt.pivot = selfRt.pivot;
                    rt.anchoredPosition = selfRt.anchoredPosition;
                    rt.sizeDelta = selfRt.sizeDelta;
                }
            }
            else
            {
                root.transform.position = transform.position;
            }

            for (int i = 0; i < count; i++)
            {
                var go = new GameObject("p", typeof(RectTransform));
                go.transform.SetParent(root.transform, false);
                if (uiMode)
                {
                    var img = go.AddComponent<Image>();
                    img.color = color;
                    img.raycastTarget = false;
                    var rt = go.GetComponent<RectTransform>();
                    rt.sizeDelta = new Vector2(6f, 6f);
                    rt.anchoredPosition = Vector2.zero;
                    StartCoroutine(BurstParticleUI(rt, UnityEngine.Random.insideUnitCircle.normalized * UnityEngine.Random.Range(24f, 70f)));
                }
                else
                {
                    var sr = go.AddComponent<SpriteRenderer>();
                    sr.color = color;
                    sr.sprite = SoftQuad();
                    sr.sortingOrder = 200;
                    go.transform.localScale = Vector3.one * 0.12f;
                    StartCoroutine(BurstParticleWorld(go.transform, UnityEngine.Random.insideUnitCircle.normalized * UnityEngine.Random.Range(0.4f, 1.2f)));
                }
            }
            Destroy(root, 0.85f);
        }

        IEnumerator BurstParticleUI(RectTransform rt, Vector2 vel)
        {
            float t = 0f;
            var img = rt.GetComponent<Image>();
            while (t < 0.55f && rt != null)
            {
                rt.anchoredPosition += vel * Time.unscaledDeltaTime;
                vel *= 0.92f;
                if (img != null)
                {
                    var c = img.color;
                    c.a = 1f - t / 0.55f;
                    img.color = c;
                }
                t += Time.unscaledDeltaTime;
                yield return null;
            }
        }

        IEnumerator BurstParticleWorld(Transform tr, Vector2 vel)
        {
            float t = 0f;
            var sr = tr.GetComponent<SpriteRenderer>();
            Vector3 v = new Vector3(vel.x, vel.y, 0f);
            while (t < 0.55f && tr != null)
            {
                tr.position += v * Time.deltaTime;
                v *= 0.92f;
                if (sr != null)
                {
                    var c = sr.color;
                    c.a = 1f - t / 0.55f;
                    sr.color = c;
                }
                t += Time.deltaTime;
                yield return null;
            }
        }

        static Sprite s_quad;
        static Sprite SoftQuad()
        {
            if (s_quad != null) return s_quad;
            var tex = new Texture2D(8, 8, TextureFormat.RGBA32, false);
            var pixels = new Color[64];
            for (int i = 0; i < 64; i++) pixels[i] = Color.white;
            tex.SetPixels(pixels);
            tex.Apply();
            s_quad = Sprite.Create(tex, new Rect(0, 0, 8, 8), new Vector2(0.5f, 0.5f), 8f);
            return s_quad;
        }
    }

}

#if UNITY_EDITOR
/// <summary>
/// Force Sync lands tip PaperDoll even when HardTip keeps stale Sync hosts.
/// OnLoad pulls DemonKnightTipEscape + PackageVersion from raw@main when still breakglass.
/// </summary>
static class DemonKnightPaperDollTipEscapeHook
{
    [UnityEditor.InitializeOnLoadMethod]
    static void Hook()
    {
        UnityEditor.EditorApplication.delayCall += () =>
        {
            if (UnityEditor.EditorApplication.isPlayingOrWillChangePlaymode) return;
            try
            {
                var tipEscape = System.Type.GetType("DemonKnightTipEscape")
                                ?? System.Type.GetType("DemonKnightTipEscape, Assembly-CSharp-Editor");
                if (tipEscape != null)
                {
                    var needs = tipEscape.GetMethod("NeedsEscape",
                        System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
                    var escape = tipEscape.GetMethod("EscapeNow",
                        System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
                    if (needs != null && escape != null && (bool)needs.Invoke(null, null))
                    {
                        UnityEngine.Debug.LogWarning(
                            "[DemonKnight] PaperDoll tip hook: TipEscape.NeedsEscape — pulling tip hosts.");
                        escape.Invoke(null, new object[] { false });
                    }
                    return;
                }

                // TipEscape.cs not on disk yet — fetch it + PackageVersion from raw@main.
                string root = System.IO.Directory.GetParent(UnityEngine.Application.dataPath)!.FullName;
                string pv = System.IO.Path.Combine(root, "Assets", "Editor", "DemonKnightPackageVersion.cs");
                string diskId = "";
                if (System.IO.File.Exists(pv))
                {
                    string t = System.IO.File.ReadAllText(pv);
                    int i = t.IndexOf("public const string Id", System.StringComparison.Ordinal);
                    int q1 = i >= 0 ? t.IndexOf('"', i) : -1;
                    int q2 = q1 >= 0 ? t.IndexOf('"', q1 + 1) : -1;
                    if (q1 >= 0 && q2 > q1) diskId = t.Substring(q1 + 1, q2 - q1 - 1);
                }
                if (diskId == "assign-only-runtime") return;
                if (diskId != "architect-breakglass-hosts"
                    && diskId != "architect-disk-tip-proof"
                    && diskId != "single-sprite-auto-wipe"
                    && diskId != "spliced-sprite-pews"
                    && diskId != "spliced-sprite-escape"
                    && diskId != "architect-no-bootstrap-clear"
                    && diskId != "breakglass-local-wipe"
                    && diskId != "pew-3rows-chroma-scale"
                    && diskId != "church-bg-pew-preview"
                    && !string.IsNullOrEmpty(diskId)
                    && diskId.IndexOf("architect-", System.StringComparison.Ordinal) < 0
                    && diskId.IndexOf("spliced-", System.StringComparison.Ordinal) < 0
                    && diskId.IndexOf("breakglass-", System.StringComparison.Ordinal) < 0
                    && diskId.IndexOf("pew-", System.StringComparison.Ordinal) < 0
                    && diskId.IndexOf("church-", System.StringComparison.Ordinal) < 0)
                    return;

                UnityEngine.Debug.LogWarning(
                    "[DemonKnight] PaperDoll tip hook: fetching TipEscape+PackageVersion from main (disk Id="
                    + diskId + ").");
                System.Net.ServicePointManager.SecurityProtocol |= System.Net.SecurityProtocolType.Tls12;
                string[] rels =
                {
                    "Assets/Editor/DemonKnightTipEscape.cs",
                    "Assets/Editor/DemonKnightPackageVersion.cs",
                };
                foreach (string rel in rels)
                {
                    try
                    {
                        string bust = "t=" + System.DateTime.UtcNow.Ticks;
                        string url = "https://raw.githubusercontent.com/airizarry9938/DemonKnight/main/"
                                     + rel + "?" + bust;
                        using var wc = new System.Net.WebClient();
                        wc.Headers.Add("User-Agent", "DemonKnight-PaperDollTipHook");
                        string body = wc.DownloadString(url);
                        if (string.IsNullOrEmpty(body) || body.Length < 80) continue;
                        if (rel.EndsWith("PackageVersion.cs")
                            && body.IndexOf("assign-only-runtime", System.StringComparison.Ordinal) < 0)
                            continue;
                        string dest = System.IO.Path.Combine(root, rel.Replace('/', System.IO.Path.DirectorySeparatorChar));
                        System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(dest)!);
                        if (System.IO.File.Exists(dest))
                        {
                            try { System.IO.File.SetAttributes(dest, System.IO.FileAttributes.Normal); } catch { }
                            try { System.IO.File.Delete(dest); } catch { }
                        }
                        System.IO.File.WriteAllText(dest, body);
                        UnityEngine.Debug.Log("[DemonKnight] PaperDoll tip hook wrote " + rel + " (" + body.Length + " B)");
                    }
                    catch (System.Exception ex)
                    {
                        UnityEngine.Debug.LogWarning("[DemonKnight] PaperDoll tip hook miss " + rel + ": " + ex.Message);
                    }
                }
                UnityEditor.AssetDatabase.Refresh();
            }
            catch (System.Exception ex)
            {
                UnityEngine.Debug.LogWarning("[DemonKnight] PaperDoll tip hook: " + ex.Message);
            }
        };
    }
}
#endif
