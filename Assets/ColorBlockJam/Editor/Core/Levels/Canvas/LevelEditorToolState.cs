using System.Collections.Generic;
using ColorBlockJam.Runtime.Art;
using ColorBlockJam.Runtime.Board;
using UnityEngine;

namespace ColorBlockJam.Editor.Levels
{
    internal enum LevelObstacleBrush { None, Barrier, Void }

    /// <summary>
    /// The canvas tool a left click uses. Right click always removes. The rotate and mirror tools act on the clicked
    /// block (user request of 2026-09-25); values stay contiguous because the canvas indexes its icon cache by them.
    /// </summary>
    internal enum LevelEditorTool
    {
        Select = 0,
        Pen = 1,
        Eraser = 2,
        RotateCounterClockwise = 3,
        RotateClockwise = 4,
        MirrorHorizontal = 5,
        MirrorVertical = 6
    }

    /// <summary>
    /// Level Editor tool state in plain C#: the active tool, the colour for new blocks, the shape Select places with its
    /// ghost orientation, and what the Pen holds: freehand drawing or the Barrier brush (user order of 2026-09-26:
    /// pieces are placed with Select; the Pen paints gates, draws on the selected block and draws new pieces). The
    /// block, door or obstacle
    /// selection stays with the window, which resolves it after every draft change (docs/Level-Editor-Design.md
    /// section 2).
    /// </summary>
    internal sealed class LevelEditorToolState
    {
        /// <summary>The 11 gate and block colours: every PaletteColor below Wall.</summary>
        public const int k_colorCount = (int)PaletteColor.Wall;

        private Vector2Int[] m_armedCells = System.Array.Empty<Vector2Int>();
        private Vector2Int[] m_ghostCells = System.Array.Empty<Vector2Int>();

        public LevelEditorTool Tool { get; private set; } = LevelEditorTool.Select;
        public PaletteColor Color { get; private set; } = PaletteColor.Red;
        /// <summary>The colour id new blocks get, the PaletteColor name the level JSON uses.</summary>
        public string ColorId => Color.ToString();
        /// <summary>The armed piece's ShapeKey (DEC-193), or the default key when nothing is armed.</summary>
        public ShapeKey ArmedKey { get; private set; }
        /// <summary>The armed shape's cells in the ghost's current orientation; empty when nothing is armed.</summary>
        public IReadOnlyList<Vector2Int> ArmedCells => m_armedCells;
        /// <summary>
        /// The armed cell offset that sits on the hovered cell, so a stamp's origin is the hovered cell minus it.
        /// The cell nearest the piece's box centre when it is armed; a turn keeps that cell under the pointer.
        /// </summary>
        public Vector2Int ArmedAnchor { get; private set; }
        /// <summary>The ghost's cells relative to the hovered cell: ArmedCells minus ArmedAnchor.</summary>
        public IReadOnlyList<Vector2Int> GhostCells => m_ghostCells;
        /// <summary>True while Select holds a shape to place.</summary>
        public bool HasArmedShape => ArmedKey.IsValid;
        /// <summary>
        /// The obstacle armed in the Pen; LevelEditorTool stays contiguous for the canvas icon cache.
        /// </summary>
        public LevelObstacleBrush ObstacleBrush { get; private set; }
        /// <summary>True while the Pen holds the Barrier brush (the Obstacles Barrier tile).</summary>
        public bool IsBarrierArmed => ObstacleBrush == LevelObstacleBrush.Barrier;
        /// <summary>True while the Pen holds the Void brush (the Obstacles Void tile), which cuts board cells out.</summary>
        public bool IsVoidArmed => ObstacleBrush == LevelObstacleBrush.Void;
        /// <summary>
        /// True while the Pen draws a new piece freehand (the Blocks "+" New piece tile): a stroke over free cells
        /// becomes one block. Picking a piece, a brush or another tool clears it.
        /// </summary>
        public bool IsFreehandArmed { get; private set; }

        public void SetColor(PaletteColor color)
        {
            Color = color;
        }

        /// <summary>Steps the colour through the 11 gate colours, wrapping at both ends ([ and ]).</summary>
        public void CycleColor(int step)
        {
            int next = ((int)Color + step) % k_colorCount;
            Color = (PaletteColor)(next < 0 ? next + k_colorCount : next);
        }

        /// <summary>
        /// Selects the Select tool armed with this shape, so a click on a free cell places it; the ghost starts in the
        /// shape's own orientation. Any freehand drawing or brush is disarmed.
        /// </summary>
        public void Arm(LevelShapeChoice shape)
        {
            if (shape == null || shape.Cells.Count == 0 || !shape.Key.IsValid)
            {
                Select();
                return;
            }

            ArmedKey = shape.Key;
            m_armedCells = new Vector2Int[shape.Cells.Count];
            for (int index = 0; index < m_armedCells.Length; index++)
            {
                m_armedCells[index] = shape.Cells[index];
            }

            SetGhost(GetGripCell(m_armedCells));
            Tool = LevelEditorTool.Select;
            ObstacleBrush = LevelObstacleBrush.None;
            IsFreehandArmed = false;
        }

        /// <summary>Selects the Pen in freehand mode; any armed piece or brush is disarmed.</summary>
        public void ArmFreehand()
        {
            SetTool(LevelEditorTool.Pen);
            IsFreehandArmed = true;
        }

        /// <summary>Selects the Pen with the Barrier brush; any armed piece, freehand mode or other brush is disarmed.</summary>
        public void ArmBarrier()
        {
            SetTool(LevelEditorTool.Pen);
            ObstacleBrush = LevelObstacleBrush.Barrier;
        }

        /// <summary>
        /// Selects the Pen with the Void brush, which turns floor cells into void so the play area takes any outline
        /// (an interior hole or a notch in its edge); any armed piece, freehand mode or other brush is disarmed.
        /// </summary>
        public void ArmVoid()
        {
            SetTool(LevelEditorTool.Pen);
            ObstacleBrush = LevelObstacleBrush.Void;
        }

        /// <summary>The Pen button: the plain Pen, which never holds a piece to place; any armed piece is disarmed.</summary>
        public void SelectPen()
        {
            SetTool(LevelEditorTool.Pen);
        }

        /// <summary>
        /// Turns the armed ghost 90 degrees around the hovered cell (R clockwise, Shift+R counter-clockwise, or a
        /// rotate button), so the cell under the pointer stays under it; false when nothing is armed.
        /// </summary>
        public bool RotateArmed(bool clockwise)
        {
            if (!HasArmedShape)
            {
                return false;
            }

            int minX = int.MaxValue;
            int minY = int.MaxValue;
            for (int index = 0; index < m_ghostCells.Length; index++)
            {
                Vector2Int cell = m_ghostCells[index];
                m_ghostCells[index] = clockwise ? new Vector2Int(cell.y, -cell.x) : new Vector2Int(-cell.y, cell.x);
                minX = Mathf.Min(minX, m_ghostCells[index].x);
                minY = Mathf.Min(minY, m_ghostCells[index].y);
            }

            // The armed cells stay normalized to start at (0, 0); the anchor is the hovered cell among them.
            ArmedAnchor = new Vector2Int(-minX, -minY);
            m_armedCells = new Vector2Int[m_ghostCells.Length];
            for (int index = 0; index < m_armedCells.Length; index++)
            {
                m_armedCells[index] = m_ghostCells[index] + ArmedAnchor;
            }

            return true;
        }

        /// <summary>Mirrors the armed ghost (F horizontally, Shift+F vertically); false when nothing is armed.</summary>
        public bool FlipArmed(bool vertical)
        {
            if (!HasArmedShape)
            {
                return false;
            }

            m_armedCells = vertical ? LevelPieceTools.FlipVertical(m_armedCells) : LevelPieceTools.FlipHorizontal(m_armedCells);
            SetGhost(ArmedAnchor);
            return true;
        }

        /// <summary>The Select tool; disarms any shape.</summary>
        public void Select()
        {
            SetTool(LevelEditorTool.Select);
        }

        public void SelectEraser()
        {
            SetTool(LevelEditorTool.Eraser);
        }

        /// <summary>Picks up one of the four rotate and mirror tools; any other tool selects Select instead.</summary>
        public void SelectTransform(LevelEditorTool tool)
        {
            bool transform = tool == LevelEditorTool.RotateCounterClockwise || tool == LevelEditorTool.RotateClockwise
                || tool == LevelEditorTool.MirrorHorizontal || tool == LevelEditorTool.MirrorVertical;
            SetTool(transform ? tool : LevelEditorTool.Select);
        }

        private void SetTool(LevelEditorTool tool)
        {
            Tool = tool;
            ArmedKey = default;
            m_armedCells = System.Array.Empty<Vector2Int>();
            SetGhost(Vector2Int.zero);
            ObstacleBrush = LevelObstacleBrush.None;
            IsFreehandArmed = false;
        }

        // The cell the pointer holds a newly armed piece by: the one nearest its bounding-box centre (ties go to the
        // first listed cell), so the ghost sits centred under the pointer and turns around a cell of its own.
        private static Vector2Int GetGripCell(Vector2Int[] cells)
        {
            int minX = int.MaxValue;
            int minY = int.MaxValue;
            int maxX = int.MinValue;
            int maxY = int.MinValue;
            for (int index = 0; index < cells.Length; index++)
            {
                minX = Mathf.Min(minX, cells[index].x);
                minY = Mathf.Min(minY, cells[index].y);
                maxX = Mathf.Max(maxX, cells[index].x);
                maxY = Mathf.Max(maxY, cells[index].y);
            }

            // Doubled coordinates keep a half-cell centre in integers.
            Vector2Int centre = new Vector2Int(minX + maxX, minY + maxY);
            Vector2Int grip = cells[0];
            int nearest = int.MaxValue;
            for (int index = 0; index < cells.Length; index++)
            {
                Vector2Int offset = cells[index] * 2 - centre;
                int distance = offset.x * offset.x + offset.y * offset.y;
                if (distance < nearest)
                {
                    nearest = distance;
                    grip = cells[index];
                }
            }

            return grip;
        }

        // Moves the anchor and rebuilds the ghost offsets from the armed cells.
        private void SetGhost(Vector2Int anchor)
        {
            ArmedAnchor = anchor;
            m_ghostCells = new Vector2Int[m_armedCells.Length];
            for (int index = 0; index < m_ghostCells.Length; index++)
            {
                m_ghostCells[index] = m_armedCells[index] - anchor;
            }
        }
    }
}
