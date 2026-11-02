using System;
using ColorBlockJam.Editor.Levels;
using UnityEngine;
using UnityEngine.UIElements;

namespace ColorBlockJam.Editor.BoardKit
{
    /// <summary>
    /// Draws the Piece Editor's paint grid for one draft and turns pointer strokes on its cells into paint or erase
    /// requests. The owning editor changes the draft and records undo through the cell-changed and stroke-ended
    /// callbacks; the view only reads the draft.
    /// </summary>
    internal sealed class PieceGridView
    {
        /// <summary>The background of an empty grid cell.</summary>
        internal static readonly Color EmptyCellColor = new Color(.2f, .23f, .29f);
        private static readonly Color s_hoverColor = new Color(1f, 1f, 1f, .9f);
        private static readonly Color s_eraseHoverColor = new Color(1f, .35f, .35f, .95f);
        private readonly VisualElement m_root;
        private readonly float m_size;
        private readonly Func<LevelEditorTool> m_getTool;
        private readonly Action<LevelEditorTool> m_transformPiece;
        private readonly Action<int, int, bool, bool> m_cellChanged;
        private readonly Action<bool> m_strokeEnded;
        private PieceGridDraft m_draft;
        private Color m_filledColor;
        private bool m_painting;
        private bool m_paintValue;
        private bool m_strokeRecorded;

        /// <param name="root">The element the cells are drawn in; it takes the focus when a stroke starts.</param>
        /// <param name="size">The longest grid side in pixels.</param>
        /// <param name="getTool">The Level Editor's current Tools choice.</param>
        /// <param name="transformPiece">A rotate or mirror tool's click on a cell.</param>
        /// <param name="cellChanged">
        /// A stroke changes cell (x, y) to filled or empty; the last argument is true for the stroke's first change,
        /// before the draft has changed, so the owner can record one undo step per stroke.
        /// </param>
        /// <param name="strokeEnded">A stroke ended; the argument is true when it changed any cell.</param>
        internal PieceGridView(VisualElement root, float size, Func<LevelEditorTool> getTool,
            Action<LevelEditorTool> transformPiece, Action<int, int, bool, bool> cellChanged, Action<bool> strokeEnded)
        {
            m_root = root;
            m_size = size;
            m_getTool = getTool;
            m_transformPiece = transformPiece;
            m_cellChanged = cellChanged;
            m_strokeEnded = strokeEnded;
        }

        /// <summary>True for the tools that turn the whole piece on a click instead of painting.</summary>
        internal static bool IsTransformTool(LevelEditorTool tool) =>
            tool == LevelEditorTool.RotateCounterClockwise || tool == LevelEditorTool.RotateClockwise ||
            tool == LevelEditorTool.MirrorHorizontal || tool == LevelEditorTool.MirrorVertical;

        /// <summary>Rebuilds the cells for <paramref name="draft"/>, painting filled cells in <paramref name="filledColor"/>.</summary>
        internal void Draw(PieceGridDraft draft, Color filledColor)
        {
            m_draft = draft;
            m_filledColor = filledColor;
            m_root.Clear();
            float cellSize = m_size / Mathf.Max(draft.Width, draft.Height);
            m_root.style.width = draft.Width * cellSize;
            m_root.style.height = draft.Height * cellSize;
            for (int y = draft.MinY + draft.Height - 1; y >= draft.MinY; y--)
            {
                var row = new VisualElement(); row.style.flexDirection = FlexDirection.Row;
                row.style.height = cellSize; row.style.flexShrink = 0;
                m_root.Add(row);
                for (int x = draft.MinX; x < draft.MinX + draft.Width; x++)
                {
                    int cx = x, cy = y;
                    var cell = new VisualElement();
                    cell.name = "cell-" + x + "-" + y;
                    cell.style.width = cellSize - 2; cell.style.height = cellSize - 2;
                    cell.style.flexShrink = 0;
                    cell.style.marginLeft = 1; cell.style.marginRight = 1;
                    cell.style.marginTop = 1; cell.style.marginBottom = 1;
                    cell.style.backgroundColor = GetCellColor(draft.Get(x, y));
                    cell.tooltip = x == 0 && y == 0 ? "Center (0, 0)" : $"({x}, {y})";
                    cell.RegisterCallback<PointerDownEvent>(evt =>
                    {
                        if (evt.button != 0 && evt.button != 1) return;
                        evt.StopPropagation();
                        // Right click always erases, as in the Level Editor; the left button follows its Tools choice.
                        if (evt.button == 1) { BeginPaint(cx, cy, false, cell); return; }
                        LevelEditorTool tool = m_getTool();
                        if (IsTransformTool(tool)) { m_transformPiece(tool); return; }
                        BeginPaint(cx, cy, tool != LevelEditorTool.Eraser, cell);
                    });
                    cell.RegisterCallback<PointerEnterEvent>(evt =>
                    {
                        SetHover(cell, true);
                        if (evt.pressedButtons == 0) EndPaint();
                        if (m_painting) Paint(cx, cy, cell);
                    });
                    cell.RegisterCallback<PointerLeaveEvent>(_ => SetHover(cell, false));
                    cell.RegisterCallback<ContextClickEvent>(evt => evt.StopPropagation());
                    row.Add(cell);
                }
            }
            // One overlay at the visual center, independent of cell borders and
            // odd/even grid dimensions. It never intercepts painting input.
            var cross = new VisualElement { name = "grid-center", pickingMode = PickingMode.Ignore };
            cross.style.position = Position.Absolute;
            cross.style.left = Length.Percent(50); cross.style.top = Length.Percent(50);
            cross.style.width = 0; cross.style.height = 0;
            AddCrossLine(cross, -8, -1, 16, 2);
            AddCrossLine(cross, -1, -8, 2, 16);
            m_root.Add(cross);
        }

        /// <summary>
        /// Starts a stroke on cell (x, y) that fills or empties every cell it crosses; tests drive strokes through
        /// this without OS pointer input.
        /// </summary>
        internal void BeginPaint(int x, int y, bool filled, VisualElement cell)
        {
            EndPaint();
            m_root.Focus();
            m_painting = true; m_paintValue = filled; m_strokeRecorded = false;
            Paint(x, y, cell);
        }

        /// <summary>Paints cell (x, y) with the current stroke's value; a cell that already has it is left alone.</summary>
        internal void Paint(int x, int y, VisualElement cell)
        {
            if (m_draft.Get(x, y) == m_paintValue) return;
            bool firstChange = !m_strokeRecorded;
            m_strokeRecorded = true;
            m_cellChanged(x, y, m_paintValue, firstChange);
            cell.style.backgroundColor = GetCellColor(m_paintValue);
        }

        /// <summary>Ends the current stroke, if any, and reports whether it changed a cell.</summary>
        internal void EndPaint()
        {
            if (!m_painting) return;
            bool changed = m_strokeRecorded;
            m_painting = false; m_strokeRecorded = false;
            m_strokeEnded(changed);
        }

        /// <summary>Drops the current stroke without reporting it, for an undo or a newly loaded draft.</summary>
        internal void CancelStroke()
        {
            m_painting = false; m_strokeRecorded = false;
        }

        private static void AddCrossLine(VisualElement parent, float x, float y, float width, float height)
        {
            var line = new VisualElement { pickingMode = PickingMode.Ignore };
            line.style.position = Position.Absolute; line.style.left = x; line.style.top = y;
            line.style.width = width; line.style.height = height;
            line.style.backgroundColor = new Color(1, 1, 1, .85f);
            parent.Add(line);
        }

        private Color GetCellColor(bool filled) => filled ? m_filledColor : EmptyCellColor;

        // Outlines the cell under the pointer; red when the left button would erase it.
        private void SetHover(VisualElement cell, bool hovered)
        {
            Color color = !hovered ? Color.clear : m_getTool() == LevelEditorTool.Eraser ? s_eraseHoverColor : s_hoverColor;
            float width = hovered ? 2f : 0f;
            cell.style.borderLeftColor = color; cell.style.borderRightColor = color;
            cell.style.borderTopColor = color; cell.style.borderBottomColor = color;
            cell.style.borderLeftWidth = width; cell.style.borderRightWidth = width;
            cell.style.borderTopWidth = width; cell.style.borderBottomWidth = width;
        }
    }
}
