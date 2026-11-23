using System;
using UnityEngine;
using UnityEngine.UI;

namespace ArthurianAutobattler
{
    /// <summary>
    /// Maps 48 citizens onto the Somerset church art: 12 pews × 4 seats (3×4 grid,
    /// columns Left/Center/Right × rows front→altar).
    /// Coordinates are normalized 0–1 over the drawn church sprite
    /// (0,0 = bottom-left, 1,1 = top-right), tuned to SomersetChurchInterior.jpg.
    /// tip: fix-architect-48-seat-preview
    /// </summary>
    [Serializable]
    public class PewAnchor
    {
        [Tooltip("Normalized center of the pew seating strip on the background.")]
        public Vector2 normalizedCenter = new Vector2(0.5f, 0.5f);

        [Tooltip("How wide the seating strip is as a fraction of background width.")]
        [Range(0.08f, 0.28f)]
        public float seatSpread = 0.16f;

        [Tooltip("Sprite scale for perspective (front pews larger).")]
        [Range(0.55f, 1.65f)]
        public float scale = 1f;

        public int seatCapacity = 4;
    }

    public class SomersetPewLayout : MonoBehaviour
    {
        public const int PewColumns = 3;
        public const int PewRows = 4;
        public const int PewCount = 12;
        public const int TotalSeats = 48;
        public const int SeatsPerPew = 4;

        /// <summary>
        /// All 4 pew-rows show hitboxes/sprites (12×4 = 48). Prior tips hid row 3
        /// (VisualSpriteRowCount=3 → 36, with oversized 110×140 boxes reading as ~33).
        /// </summary>
        public const int VisualSpriteRowCount = 4;
        /// <summary>Legacy alias — seats per pew (Gossip / adjacency group size).</summary>
        public const int PewSeatRowSize = SeatsPerPew;

        /// <summary>Default church hitbox (fits 4 per pew strip). Override via StageLayoutProfile.</summary>
        public static readonly Vector2 DefaultHitboxSize = new Vector2(64f, 84f);
        /// <summary>Default unit sprite scale relative to hitbox (Architect Layout tab).</summary>
        public const float DefaultSpriteScale = 1.15f;

        /// <summary>
        /// True when this seat should draw a pew sprite / hitbox (all 4 rows = 48 seats).
        /// Visual identity comes from ClassSpritePoolApplier (by unit class) at church spawn;
        /// StageLayoutProfile pew slots remain position/scale placement guides only.
        /// </summary>
        public static bool SeatShowsSprite(int pewSeatIndex)
            => GetStaticPewRow(pewSeatIndex) < VisualSpriteRowCount;

        /// <summary>Bump when default pew anchors change so Play Mode refreshes stale scene data.</summary>
        public const int CurrentLayoutVersion = 6;

        [Header("Background (full-screen church art)")]
        public RectTransform backgroundRect;

        [Header("12 Pew Anchors (row-major: front-left → altar-right)")]
        public PewAnchor[] pews = CreateDefaultPews();

        [HideInInspector]
        public int layoutVersion = 0;

        /// <summary>
        /// Defaults matched to SomersetChurchInterior.jpg (1408×768).
        /// Occupied rows sit mid→altar; camera-front pews stay empty.
        /// </summary>
        public static PewAnchor[] CreateDefaultPews()
        {
            // Column X centers converge slightly toward the altar (perspective).
            float[][] colXByRow =
            {
                new[] { 0.220f, 0.500f, 0.780f }, // nearest occupied (mid-nave)
                new[] { 0.226f, 0.500f, 0.775f },
                new[] { 0.232f, 0.500f, 0.770f },
                new[] { 0.238f, 0.500f, 0.765f }  // back / altar / windows
            };

            // All 4 pew-rows visible (48 hitboxes). tip: fix-architect-48-seat-preview
            float[] rowY = { 0.355f, 0.490f, 0.625f, 0.760f };
            float[] rowScale = { 1.18f, 1.08f, 0.98f, 0.90f };
            // Wide enough strips for 4 compact hitboxes (64×84) without overflow clipping.
            float[] rowSpread = { 0.240f, 0.220f, 0.200f, 0.180f };

            // Uniform 12×4 = 48.
            int[,] caps =
            {
                { 4, 4, 4 },
                { 4, 4, 4 },
                { 4, 4, 4 },
                { 4, 4, 4 }
            };

            var result = new PewAnchor[PewCount];
            int i = 0;
            for (int row = 0; row < PewRows; row++)
            {
                for (int col = 0; col < PewColumns; col++)
                {
                    result[i++] = new PewAnchor
                    {
                        normalizedCenter = new Vector2(colXByRow[row][col], rowY[row]),
                        seatSpread = rowSpread[row],
                        scale = rowScale[row],
                        seatCapacity = caps[row, col]
                    };
                }
            }

            return result;
        }

        public void ResetToChurchArtDefaults()
        {
            pews = CreateDefaultPews();
            layoutVersion = CurrentLayoutVersion;
        }

        /// <summary>
        /// Scene-serialized pews keep old Y values after script updates. Call before seating.
        /// </summary>
        public void ApplyLatestDefaultsIfNeeded(bool force = false)
        {
            bool staleVersion = layoutVersion < CurrentLayoutVersion;
            bool looksLikeOldFrontRow = pews != null && pews.Length > 0 && pews[0] != null
                && pews[0].normalizedCenter.y < 0.32f; // old layout sat at ~0.255

            if (force || staleVersion || looksLikeOldFrontRow || pews == null || pews.Length != PewCount)
            {
                ResetToChurchArtDefaults();
                Debug.Log($"[DemonKnight] SomersetPewLayout refreshed to v{CurrentLayoutVersion} (front pews empty, seats shifted up).");
            }
        }

        private void Awake() => ApplyLatestDefaultsIfNeeded();

        public int GetPewIndexForSeat(int globalSeatIndex)
        {
            EnsureValid();
            int remaining = Mathf.Clamp(globalSeatIndex, 0, TotalSeats - 1);
            for (int p = 0; p < PewCount; p++)
            {
                if (remaining < pews[p].seatCapacity) return p;
                remaining -= pews[p].seatCapacity;
            }
            return PewCount - 1;
        }

        public int GetSlotInPew(int globalSeatIndex)
        {
            EnsureValid();
            int remaining = Mathf.Clamp(globalSeatIndex, 0, TotalSeats - 1);
            for (int p = 0; p < PewCount; p++)
            {
                if (remaining < pews[p].seatCapacity) return remaining;
                remaining -= pews[p].seatCapacity;
            }
            return 0;
        }

        public int GetPewRow(int pewIndex) => Mathf.Clamp(pewIndex, 0, PewCount - 1) / PewColumns;
        public int GetPewColumn(int pewIndex) => Mathf.Clamp(pewIndex, 0, PewCount - 1) % PewColumns;

        // Static helpers — uniform SeatsPerPew (for RecruitInstance / Gossip).
        private static readonly int[] DefaultCapacities =
        {
            SeatsPerPew, SeatsPerPew, SeatsPerPew, SeatsPerPew,
            SeatsPerPew, SeatsPerPew, SeatsPerPew, SeatsPerPew,
            SeatsPerPew, SeatsPerPew, SeatsPerPew, SeatsPerPew
        };

        public static int GetStaticPewIndex(int globalSeatIndex)
        {
            int seat = Mathf.Clamp(globalSeatIndex, 0, TotalSeats - 1);
            return Mathf.Clamp(seat / SeatsPerPew, 0, PewCount - 1);
        }

        public static int GetStaticPewRow(int globalSeatIndex) => GetStaticPewIndex(globalSeatIndex) / PewColumns;
        public static int GetStaticPewColumn(int globalSeatIndex) => GetStaticPewIndex(globalSeatIndex) % PewColumns;

        /// <summary>Normalized position of a seat within the background.</summary>
        public Vector2 GetNormalizedSeatPosition(int globalSeatIndex)
            => GetNormalizedSeatPosition(globalSeatIndex, spreadMul: 1f, gap01: 0f);

        /// <param name="spreadMul">Multiplies pew seatSpread (Architect Layout strip width).</param>
        /// <param name="gap01">Normalized gap between seats (shrinks usable strip).</param>
        public Vector2 GetNormalizedSeatPosition(int globalSeatIndex, float spreadMul, float gap01)
        {
            EnsureValid();
            int pewIndex = GetPewIndexForSeat(globalSeatIndex);
            int slot = GetSlotInPew(globalSeatIndex);
            PewAnchor pew = pews[pewIndex];
            int cap = Mathf.Max(1, pew.seatCapacity);

            float spread = pew.seatSpread * Mathf.Max(0.25f, spreadMul);
            float gap = Mathf.Clamp(gap01, 0f, 0.08f);
            if (cap > 1 && gap > 0f)
                spread = Mathf.Max(0.04f, spread - gap * (cap - 1));

            // Spread seats left→right along the pew bench.
            float t = cap == 1 ? 0.5f : slot / (float)(cap - 1);
            float x = pew.normalizedCenter.x + Mathf.Lerp(-spread * 0.5f, spread * 0.5f, t);
            float y = pew.normalizedCenter.y;
            return new Vector2(x, y);
        }

        /// <summary>Static play-layout seat pos (Architect preview / StageLayout defaults).</summary>
        public static Vector2 GetStaticNormalizedSeatPosition(
            PewAnchor[] pews, int globalSeatIndex, float spreadMul = 1f, float gap01 = 0f)
        {
            if (pews == null || pews.Length == 0) return new Vector2(0.5f, 0.5f);
            int remaining = Mathf.Clamp(globalSeatIndex, 0, TotalSeats - 1);
            int pewIndex = 0;
            int slot = 0;
            for (int p = 0; p < pews.Length; p++)
            {
                int cap = Mathf.Max(1, pews[p] != null ? pews[p].seatCapacity : SeatsPerPew);
                if (remaining < cap) { pewIndex = p; slot = remaining; break; }
                remaining -= cap;
                pewIndex = p;
            }
            PewAnchor pew = pews[Mathf.Clamp(pewIndex, 0, pews.Length - 1)] ?? new PewAnchor();
            int capN = Mathf.Max(1, pew.seatCapacity);
            float spread = pew.seatSpread * Mathf.Max(0.25f, spreadMul);
            float gap = Mathf.Clamp(gap01, 0f, 0.08f);
            if (capN > 1 && gap > 0f)
                spread = Mathf.Max(0.04f, spread - gap * (capN - 1));
            float t = capN == 1 ? 0.5f : slot / (float)(capN - 1);
            float x = pew.normalizedCenter.x + Mathf.Lerp(-spread * 0.5f, spread * 0.5f, t);
            return new Vector2(x, pew.normalizedCenter.y);
        }

        public float GetSeatScale(int globalSeatIndex)
        {
            EnsureValid();
            return pews[GetPewIndexForSeat(globalSeatIndex)].scale;
        }

        /// <summary>
        /// Local rect of the church sprite as drawn (accounts for Image.preserveAspect letterboxing).
        /// </summary>
        public Rect GetDrawnContentRect()
        {
            if (backgroundRect == null) return new Rect(0f, 0f, 0f, 0f);

            Rect r = backgroundRect.rect;
            Image img = backgroundRect.GetComponent<Image>();
            if (img == null || !img.preserveAspect || img.sprite == null || r.height <= 0.01f)
                return r;

            float spriteAspect = img.sprite.rect.width / Mathf.Max(1f, img.sprite.rect.height);
            float rectAspect = r.width / r.height;
            if (spriteAspect > rectAspect)
            {
                float h = r.width / spriteAspect;
                float y = r.y + (r.height - h) * 0.5f;
                return new Rect(r.x, y, r.width, h);
            }

            float w = r.height * spriteAspect;
            float x = r.x + (r.width - w) * 0.5f;
            return new Rect(x, r.y, w, r.height);
        }

        /// <summary>Converts normalized 0–1 coords into local anchored position inside backgroundRect.</summary>
        public Vector2 NormalizedToLocal(Vector2 normalized)
        {
            if (backgroundRect == null) return Vector2.zero;
            Rect content = GetDrawnContentRect();
            return new Vector2(
                content.xMin + normalized.x * content.width,
                content.yMin + normalized.y * content.height);
        }

        /// <summary>
        /// Place a seat with anchors (not baked pixel offsets). Survives Play Mode when
        /// RectTransform.rect is still zero during Start — pixel placement would stack every seat.
        /// </summary>
        public void ApplySeatTransform(RectTransform seatRt, int globalSeatIndex, Vector2 baseSize)
            => ApplySeatTransform(seatRt, globalSeatIndex, baseSize, jitterXPixels: 0f);

        /// <param name="jitterXPixels">Deterministic horizontal offset (±2px) for natural crowding.</param>
        public void ApplySeatTransform(RectTransform seatRt, int globalSeatIndex, Vector2 baseSize, float jitterXPixels)
        {
            if (seatRt == null) return;
            Vector2 norm = GetNormalizedSeatPosition(globalSeatIndex);
            float scale = GetSeatScale(globalSeatIndex);
            ApplyNormalizedSeatTransform(seatRt, norm, baseSize, scale, jitterXPixels);
        }

        /// <summary>
        /// Place a seat from StageLayoutProfile church authoring (normalized 0–1 + scale mul).
        /// </summary>
        public void ApplyNormalizedSeatTransform(
            RectTransform seatRt, Vector2 normalized, Vector2 baseSize, float scaleMul, float jitterXPixels)
        {
            if (seatRt == null) return;
            GetContentNormalizedRange(out Vector2 cMin, out Vector2 cMax);
            Vector2 anchor = new Vector2(
                Mathf.Lerp(cMin.x, cMax.x, Mathf.Clamp01(normalized.x)),
                Mathf.Lerp(cMin.y, cMax.y, Mathf.Clamp01(normalized.y)));

            seatRt.anchorMin = seatRt.anchorMax = anchor;
            seatRt.pivot = new Vector2(0.5f, 0.12f);
            seatRt.anchoredPosition = new Vector2(Mathf.Clamp(jitterXPixels, -2f, 2f), 0f);
            seatRt.sizeDelta = baseSize * Mathf.Max(0.15f, scaleMul);
            seatRt.localScale = Vector3.one;
            seatRt.localRotation = Quaternion.identity;
        }

        /// <summary>Drawn church art as normalized min/max inside backgroundRect (0–1).</summary>
        public void GetContentNormalizedRange(out Vector2 min, out Vector2 max)
        {
            min = Vector2.zero;
            max = Vector2.one;
            if (backgroundRect == null) return;

            Rect full = backgroundRect.rect;
            if (full.width <= 0.01f || full.height <= 0.01f) return;

            Rect content = GetDrawnContentRect();
            min = new Vector2(
                (content.xMin - full.xMin) / full.width,
                (content.yMin - full.yMin) / full.height);
            max = new Vector2(
                (content.xMax - full.xMin) / full.width,
                (content.yMax - full.yMin) / full.height);
        }

        public void EnsureValid()
        {
            ApplyLatestDefaultsIfNeeded();

            if (pews == null || pews.Length != PewCount)
            {
                pews = CreateDefaultPews();
                layoutVersion = CurrentLayoutVersion;
            }

            int sum = 0;
            for (int i = 0; i < pews.Length; i++)
            {
                if (pews[i] == null) pews[i] = new PewAnchor();
                pews[i].seatCapacity = Mathf.Max(1, pews[i].seatCapacity);
                sum += pews[i].seatCapacity;
            }

            // Auto-fix last pew if capacities drifted off 48.
            if (sum != TotalSeats && pews.Length > 0)
            {
                int delta = TotalSeats - (sum - pews[PewCount - 1].seatCapacity);
                pews[PewCount - 1].seatCapacity = Mathf.Max(1, delta);
            }
        }

#if UNITY_EDITOR
        private void OnValidate() => EnsureValid();
#endif
    }
}
