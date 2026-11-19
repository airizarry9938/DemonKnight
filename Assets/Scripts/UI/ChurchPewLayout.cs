using System;
using UnityEngine;

namespace ArthurianAutobattler
{
    /// <summary>
    /// Maps 52 citizens onto the Somerset church art: 12 pews in a 3×4 grid
    /// (columns Left/Center/Right × rows front→altar), ~4–5 seats per pew.
    /// Coordinates are normalized 0–1 over the church background RectTransform
    /// (0,0 = bottom-left, 1,1 = top-right), tuned to SomersetChurchInterior.jpg.
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
        [Range(0.55f, 1.2f)]
        public float scale = 1f;

        public int seatCapacity = 4;
    }

    public class ChurchPewLayout : MonoBehaviour
    {
        public const int PewColumns = 3;
        public const int PewRows = 4;
        public const int PewCount = 12;
        public const int TotalSeats = 52;

        [Header("Background (full-screen church art)")]
        public RectTransform backgroundRect;

        [Header("12 Pew Anchors (row-major: front-left → altar-right)")]
        public PewAnchor[] pews = CreateDefaultPews();

        /// <summary>
        /// Default anchors matched to SomersetChurchInterior.jpg (3 cols × 4 rows).
        /// Row 0 = nearest/front (bottom of image), Row 3 = toward altar/windows.
        /// </summary>
        public static PewAnchor[] CreateDefaultPews()
        {
            // Column X centers (aisles between left/center/right).
            float[] colX = { 0.22f, 0.50f, 0.78f };
            // Row Y centers from front (bottom) to back (top), with perspective.
            float[] rowY = { 0.30f, 0.42f, 0.53f, 0.63f };
            float[] rowScale = { 1.00f, 0.90f, 0.80f, 0.72f };
            float[] rowSpread = { 0.18f, 0.17f, 0.15f, 0.14f };

            // Capacities: front row bigger (5 each), then mix to total 52.
            // 5+5+5 + 4+5+4 + 4+4+4 + 4+4+4 = 15+13+12+12 = 52
            int[,] caps =
            {
                { 5, 5, 5 },
                { 4, 5, 4 },
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
                        normalizedCenter = new Vector2(colX[col], rowY[row]),
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
        }

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

        // Static helpers matching CreateDefaultPews capacities (for RecruitInstance / Gossip).
        private static readonly int[] DefaultCapacities = { 5, 5, 5, 4, 5, 4, 4, 4, 4, 4, 4, 4 };

        public static int GetStaticPewIndex(int globalSeatIndex)
        {
            int remaining = Mathf.Clamp(globalSeatIndex, 0, TotalSeats - 1);
            for (int p = 0; p < PewCount; p++)
            {
                if (remaining < DefaultCapacities[p]) return p;
                remaining -= DefaultCapacities[p];
            }
            return PewCount - 1;
        }

        public static int GetStaticPewRow(int globalSeatIndex) => GetStaticPewIndex(globalSeatIndex) / PewColumns;
        public static int GetStaticPewColumn(int globalSeatIndex) => GetStaticPewIndex(globalSeatIndex) % PewColumns;

        /// <summary>Normalized position of a seat within the background.</summary>
        public Vector2 GetNormalizedSeatPosition(int globalSeatIndex)
        {
            EnsureValid();
            int pewIndex = GetPewIndexForSeat(globalSeatIndex);
            int slot = GetSlotInPew(globalSeatIndex);
            PewAnchor pew = pews[pewIndex];
            int cap = Mathf.Max(1, pew.seatCapacity);

            // Spread seats left→right along the pew bench.
            float t = cap == 1 ? 0.5f : slot / (float)(cap - 1);
            float x = pew.normalizedCenter.x + Mathf.Lerp(-pew.seatSpread * 0.5f, pew.seatSpread * 0.5f, t);
            float y = pew.normalizedCenter.y;
            return new Vector2(x, y);
        }

        public float GetSeatScale(int globalSeatIndex)
        {
            EnsureValid();
            return pews[GetPewIndexForSeat(globalSeatIndex)].scale;
        }

        /// <summary>Converts normalized 0–1 coords into local anchored position inside backgroundRect.</summary>
        public Vector2 NormalizedToLocal(Vector2 normalized)
        {
            if (backgroundRect == null) return Vector2.zero;
            Vector2 size = backgroundRect.rect.size;
            return new Vector2(
                (normalized.x - 0.5f) * size.x,
                (normalized.y - 0.5f) * size.y);
        }

        public void EnsureValid()
        {
            if (pews == null || pews.Length != PewCount)
                pews = CreateDefaultPews();

            int sum = 0;
            for (int i = 0; i < pews.Length; i++)
            {
                if (pews[i] == null) pews[i] = new PewAnchor();
                pews[i].seatCapacity = Mathf.Max(1, pews[i].seatCapacity);
                sum += pews[i].seatCapacity;
            }

            // Auto-fix last pew if capacities drifted off 52.
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
