using ColorBlockJam.Runtime.Levels;
using UnityEngine;

namespace ColorBlockJam.Editor.Levels
{
    /// <summary>
    /// Pixel layout of the Level Editor canvas: the cell size that fits the board, its ring and the edge controls
    /// into the element at the designer's zoom, the centred and panned board origin, and the conversions between
    /// board cells, ring segments and local pixels. Row Height - 1 is drawn at the top, as in the PRD's board
    /// coordinates.
    /// </summary>
    internal readonly struct LevelCanvasGeometry
    {
        internal const float k_margin = 12f;
        internal const float k_minCellSize = 18f;
        /// <summary>Wall ring thickness in cells.</summary>
        internal const float k_ringDepth = 0.5f;
        /// <summary>
        /// Door bars are exactly as deep as the wall ring they open (user feedback of 2026-09-25: the earlier 0.75 cell
        /// bars stuck out past the ring). The fit reserves this depth per side.
        /// </summary>
        internal const float k_doorDepth = k_ringDepth;
        // Edge controls from the gate bars outward: a gap, the inner (remove) bar, a gap, the outer (add) bar. The
        // corner buttons are squares of the bar thickness where the bar rows and columns cross (reference frame,
        // user feedback of 2026-09-25).
        /// <summary>Gap between the door bars and the inner (remove) edge bar.</summary>
        internal const float k_edgeBarOffset = 6f;
        internal const float k_edgeBarThickness = 22f;
        internal const float k_edgeBarGap = 6f;
        /// <summary>
        /// Room reserved outside the gate bars for the edge controls, with 2 px to spare: pixels outside the door
        /// bars on every side that hold the two edge bars and the corner buttons, so they never cover a door bar or
        /// a ring segment.
        /// </summary>
        internal const float k_edgeButtonBand = k_edgeBarOffset + 2f * k_edgeBarThickness + k_edgeBarGap + 2f;
        /// <summary>
        /// Zoom is relative to the fit, the size at which the board, its gate bars and edge controls fill the canvas.
        /// The first view shows a little margin (user request of 2026-09-25: the full fit was too close, a fixed
        /// 72 px cell too far).
        /// </summary>
        internal const float k_defaultZoom = 0.85f;
        /// <summary>The farthest view: the board at half the fit, so the limit follows the grid size.</summary>
        internal const float k_minZoom = 0.5f;
        /// <summary>The closest view shows about this many cells across the canvas width.</summary>
        internal const float k_closestCellsAcross = 3f;

        public readonly int Width;
        public readonly int Height;
        public readonly float CellSize;
        /// <summary>Local position of the board's top-left corner (the top-left of cell (0, Height - 1)).</summary>
        public readonly Vector2 Origin;

        private LevelCanvasGeometry(int width, int height, float cellSize, Vector2 origin)
        {
            Width = width;
            Height = height;
            CellSize = cellSize;
            Origin = origin;
        }

        public bool IsValid => Width > 0 && Height > 0 && CellSize > 0f;
        public Rect BoardRect => new Rect(Origin.x, Origin.y, Width * CellSize, Height * CellSize);

        /// <summary>
        /// Fits a width x height board, its door bars and the edge button band into an element of this size, scales
        /// that fit by zoom and moves it by pan pixels from the centre. Cells stay at least 18 px.
        /// </summary>
        public static LevelCanvasGeometry Create(Vector2 size, int width, int height, float zoom = 1f,
            Vector2 pan = default)
        {
            if (width <= 0 || height <= 0)
            {
                return default;
            }

            float fit = GetFit(size, width, height);
            float cellSize = Mathf.Max(k_minCellSize,
                Mathf.Floor(fit * Mathf.Clamp(zoom, k_minZoom, GetMaxZoom(size, width, height))));
            Vector2 origin = new Vector2(
                Mathf.Floor((size.x - width * cellSize) * 0.5f + pan.x),
                Mathf.Floor((size.y - height * cellSize) * 0.5f + pan.y));
            return new LevelCanvasGeometry(width, height, cellSize, origin);
        }

        /// <summary>
        /// The closest zoom for this canvas and board: about <see cref="k_closestCellsAcross"/> cells across the
        /// canvas width, and never below the fit.
        /// </summary>
        public static float GetMaxZoom(Vector2 size, int width, int height)
        {
            float fit = GetFit(size, width, height);
            return fit > 0f ? Mathf.Max(1f, size.x / k_closestCellsAcross / fit) : 1f;
        }

        // The cell size at which the board, its gate bars and the edge band fill the element.
        private static float GetFit(Vector2 size, int width, int height)
        {
            float reserve = 2f * k_doorDepth;
            float border = 2f * (k_margin + k_edgeButtonBand);
            float fit = Mathf.Min((size.x - border) / (width + reserve), (size.y - border) / (height + reserve));
            return float.IsNaN(fit) ? 0f : Mathf.Max(0f, fit);
        }

        public Rect GetCellRect(Vector2Int cell)
        {
            return new Rect(
                Origin.x + cell.x * CellSize,
                Origin.y + (Height - 1 - cell.y) * CellSize,
                CellSize,
                CellSize);
        }

        /// <summary>
        /// The rectangle outside the board edge at one ring segment, reaching depthInCells outward:
        /// k_ringDepth for the wall ring, k_doorDepth for a door bar.
        /// </summary>
        public Rect GetEdgeRect(BoardSide side, int segment, float depthInCells)
        {
            float depth = depthInCells * CellSize;
            switch (side)
            {
                case BoardSide.Top:
                    return new Rect(Origin.x + segment * CellSize, Origin.y - depth, CellSize, depth);
                case BoardSide.Bottom:
                    return new Rect(Origin.x + segment * CellSize, Origin.y + Height * CellSize, CellSize, depth);
                case BoardSide.Left:
                    return new Rect(Origin.x - depth, Origin.y + (Height - 1 - segment) * CellSize, depth, CellSize);
                default:
                    return new Rect(Origin.x + Width * CellSize, Origin.y + (Height - 1 - segment) * CellSize, depth, CellSize);
            }
        }

        /// <summary>The board cell under a local point; false when the point is off the board.</summary>
        public bool TryGetCell(Vector2 point, out Vector2Int cell)
        {
            cell = default;
            if (!IsValid || !BoardRect.Contains(point))
            {
                return false;
            }

            int x = Mathf.Min(Width - 1, Mathf.FloorToInt((point.x - Origin.x) / CellSize));
            int row = Mathf.Min(Height - 1, Mathf.FloorToInt((point.y - Origin.y) / CellSize));
            cell = new Vector2Int(x, Height - 1 - row);
            return true;
        }

        /// <summary>
        /// The board coordinates of the cell grid square under a local point, also beyond the board, so a dragged
        /// ghost can follow the pointer off the board and show red there.
        /// </summary>
        public Vector2Int GetCellUnclamped(Vector2 point)
        {
            int x = Mathf.FloorToInt((point.x - Origin.x) / CellSize);
            int row = Mathf.FloorToInt((point.y - Origin.y) / CellSize);
            return new Vector2Int(x, Height - 1 - row);
        }

        /// <summary>
        /// The ring segment beside the board under a local point within depthInCells of the edge. Corners belong
        /// to no side, so they return false.
        /// </summary>
        public bool TryGetEdgeSegment(Vector2 point, float depthInCells, out BoardSide side, out int segment)
        {
            side = default;
            segment = -1;
            if (!IsValid)
            {
                return false;
            }

            Rect board = BoardRect;
            float depth = depthInCells * CellSize;
            bool alongX = point.x >= board.xMin && point.x < board.xMax;
            bool alongY = point.y >= board.yMin && point.y < board.yMax;
            if (alongX && point.y >= board.yMin - depth && point.y < board.yMin)
            {
                side = BoardSide.Top;
            }
            else if (alongX && point.y >= board.yMax && point.y < board.yMax + depth)
            {
                side = BoardSide.Bottom;
            }
            else if (alongY && point.x >= board.xMin - depth && point.x < board.xMin)
            {
                side = BoardSide.Left;
            }
            else if (alongY && point.x >= board.xMax && point.x < board.xMax + depth)
            {
                side = BoardSide.Right;
            }
            else
            {
                return false;
            }

            segment = side == BoardSide.Top || side == BoardSide.Bottom
                ? Mathf.Min(Width - 1, Mathf.FloorToInt((point.x - board.xMin) / CellSize))
                : Height - 1 - Mathf.Min(Height - 1, Mathf.FloorToInt((point.y - board.yMin) / CellSize));
            return true;
        }
    }
}
