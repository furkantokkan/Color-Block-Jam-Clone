using System;
using System.Collections.Generic;
using ColorBlockJam.Runtime.Art;
using ColorBlockJam.Runtime.Levels;
using UnityEngine;
using UnityEngine.UIElements;

namespace ColorBlockJam.Editor.Levels
{
    /// <summary>
    /// Paints one Level Editor canvas frame with Painter2D, back to front, from the <see cref="DrawState"/> snapshot
    /// the canvas fills per repaint, using its cached palette colours; it never reads pointer events or changes the
    /// draft.
    /// </summary>
    internal sealed class LevelCanvasPainter
    {
        private const float k_outlineWidth = 2f;
        private const float k_selectionWidth = 3f;
        private const float k_ghostBorderWidth = 2f;
        private const float k_gridWidth = 1f;
        private const float k_doorGap = 1f;
        private static readonly Color s_gridColor = new Color(0.35f, 0.35f, 0.35f, 0.6f);
        private static readonly Color s_hoverColor = new Color(1f, 1f, 1f, 0.22f);
        private static readonly Color s_selectionColor = Color.white;
        private static readonly Color s_notchColor = new Color(1f, 1f, 1f, 0.85f);
        private static readonly Color s_unknownColor = new Color(0.5f, 0.5f, 0.5f);
        private static readonly Color s_validGhostColor = new Color(0.25f, 0.9f, 0.35f, 0.5f);
        private static readonly Color s_invalidGhostColor = new Color(0.95f, 0.2f, 0.2f, 0.5f);
        // The Pen's edited block: a green outline, and a lighter green over each free cell a click would add.
        private static readonly Color s_editOutlineColor = new Color(0.25f, 0.9f, 0.35f, 1f);
        private static readonly Color s_growCellColor = new Color(0.25f, 0.9f, 0.35f, 0.28f);
        private static readonly Color s_hatchColor = new Color(0.95f, 0.2f, 0.2f, 0.85f);
        private static readonly Color s_hatchFillColor = new Color(0.95f, 0.2f, 0.2f, 0.18f);
        private static readonly Color s_highlightColor = new Color(1f, 0.35f, 0.25f, 1f);
        private const float k_hatchSpacing = 8f;
        // Barrier overlay alphas over the Wall colour: a closed barrier reads as wall, an open one as tinted floor.
        private const float k_closedBarrierFillAlpha = 0.8f;
        private const float k_openBarrierFillAlpha = 0.22f;
        private static readonly Color s_closedBarrierLineColor = new Color(1f, 1f, 1f, 0.35f);

        // Fallback colours, indexed by PaletteColor, used when the GamePalette asset is missing an entry.
        private static readonly Color[] s_defaultColors =
        {
            new Color(0.90f, 0.22f, 0.22f), new Color(0.98f, 0.55f, 0.15f), new Color(0.98f, 0.85f, 0.20f),
            new Color(0.55f, 0.85f, 0.30f), new Color(0.15f, 0.55f, 0.25f), new Color(0.40f, 0.90f, 0.85f),
            new Color(0.05f, 0.55f, 0.55f), new Color(0.45f, 0.75f, 0.98f), new Color(0.15f, 0.35f, 0.90f),
            new Color(0.60f, 0.30f, 0.85f), new Color(0.98f, 0.50f, 0.75f), new Color(0.32f, 0.36f, 0.45f),
            new Color(0.82f, 0.84f, 0.80f)
        };

        private readonly Color[] m_colors = (Color[])s_defaultColors.Clone();
        // The snapshot of the draw in progress; reset after each draw so it holds no model between repaints.
        private DrawState m_state;
        private LevelCanvasModel m_model = LevelCanvasModel.Empty;
        private LevelCanvasGeometry m_geometry;

        /// <summary>
        /// What one canvas repaint shows: the model and its view, the window's overlays and the gesture in progress.
        /// The canvas fills it per repaint; the stroke lists are its own buffers, read only during the draw.
        /// </summary>
        internal struct DrawState
        {
            public LevelCanvasModel Model;
            public LevelCanvasGeometry Geometry;
            public IReadOnlyList<LevelWallGlyph> Walls;
            // Effect layers (design "Block Effects Architecture" section 6): underlays paint just above the blocks,
            // overlays above the grid.
            public ILevelCanvasEffectLayer[] EffectUnderlays;
            public ILevelCanvasEffectLayer[] EffectOverlays;
            public IReadOnlyList<Vector2Int> HatchCells;
            public IReadOnlyList<LevelEdgeSegment> HatchSegments;
            public LevelIssue Highlight;
            public LevelCanvasSelection Selection;
            public LevelCanvasHit Hover;
            public LevelEditorTool Tool;
            public PaletteColor ToolColor;
            public LevelObstacleBrush ObstacleBrush;
            // Select with an armed piece over a free board cell: the armed cells show there as a ghost.
            public bool PlacesArmedPiece;
            public IReadOnlyList<Vector2Int> ArmedCells;
            // The dragged block and its offset in cells; -1 when no block is dragged.
            public int DragBlock;
            public Vector2Int DragOffset;
            public bool GateStrokeActive;
            public BoardSide GateStrokeSide;
            public IReadOnlyList<int> GateStroke;
            public bool ObstacleStrokeActive;
            public bool ObstacleStrokePaints;
            public IReadOnlyList<Vector2Int> ObstacleStroke;
            public bool PieceStrokeActive;
            public IReadOnlyList<Vector2Int> PieceStroke;
            // The Pen draws freehand without the Barrier brush; the block it edits (-1 for none); whether a click on
            // the hovered cell would grow that piece; and every free cell a click would add.
            public bool FreehandPen;
            public int EditedBlock;
            public bool HoverCanGrow;
            public IReadOnlyList<Vector2Int> GrowCells;
        }

        /// <summary>The colour the canvas draws for a palette entry, so the swatches match the board.</summary>
        internal Color GetPaletteColor(PaletteColor color)
        {
            return m_colors[(int)color];
        }

        /// <summary>Caches the palette colours once; entries missing from the asset keep their fallback.</summary>
        public void SetPalette(GamePalette palette)
        {
            Array.Copy(s_defaultColors, m_colors, m_colors.Length);
            // The colours live in the palette's atlas (DEC-202); without a readable atlas the fallbacks stay.
            if (palette == null || palette.Atlas == null || !palette.Atlas.isReadable)
            {
                return;
            }

            for (int index = 0; index < palette.Colors.Count; index++)
            {
                GamePalette.Entry entry = palette.Colors[index];
                int type = (int)entry.Type;
                if (type >= 0 && type < m_colors.Length)
                {
                    m_colors[type] = palette.GetCellColor(entry.Cell);
                }
            }
        }

        // Layers, back to front (design section 3): floor (void cells stay unpainted), rule-set walls, barriers, doors,
        // blocks, effect underlays (Ice frost), the edited block's growable cells, ghost or hover, hatch, selection,
        // grid, effect overlays (Arrow badges), issue highlight. The overlays draw above the grid, so the grid line on an
        // even-width block's centre line cannot run through a badge's shaft. The block labels are the canvas's child
        // elements, so they draw above all of it. The caller skips the draw while the geometry is invalid; it draws
        // DrawBoard and then DrawOverlay, which together keep this order.

        /// <summary>
        /// The layers that change only with the model, walls or geometry: floor through the effect underlays. The
        /// canvas draws them on its own mesh, so a hover move does not tessellate them again.
        /// </summary>
        public void DrawBoard(Painter2D painter, in DrawState state)
        {
            Begin(in state);
            DrawFloor(painter);
            DrawWalls(painter);
            DrawBarriers(painter);
            for (int index = 0; index < m_model.DoorCount; index++)
            {
                DrawDoor(painter, m_model.GetDoor(index));
            }

            for (int index = 0; index < m_model.BlockCount; index++)
            {
                DrawBlock(painter, m_model.GetBlock(index));
            }

            for (int index = 0; index < state.EffectUnderlays.Length; index++) state.EffectUnderlays[index].Draw(painter, m_geometry);
            End();
        }

        /// <summary>
        /// The layers above the board that follow the pointer and the window's overlays: grow cells, ghost or hover,
        /// hatch, selection, grid, effect overlays and the issue highlight, in that order.
        /// </summary>
        public void DrawOverlay(Painter2D painter, in DrawState state)
        {
            Begin(in state);
            DrawGrowCells(painter);
            if (!DrawDoorGhost(painter) && !DrawObstacleGhost(painter) && !DrawPieceGhost(painter) && !DrawGhost(painter))
            {
                DrawHover(painter);
            }

            DrawHatch(painter);
            DrawSelection(painter);
            DrawGrid(painter);
            for (int index = 0; index < state.EffectOverlays.Length; index++) state.EffectOverlays[index].Draw(painter, m_geometry);
            DrawHighlight(painter);
            End();
        }

        private void Begin(in DrawState state)
        {
            m_state = state;
            m_model = state.Model ?? LevelCanvasModel.Empty;
            m_geometry = state.Geometry;
        }

        private void End()
        {
            m_state = default;
            m_model = LevelCanvasModel.Empty;
        }

        // Each rule-set wall quadrant: straight walls fill it, corners are a quarter disc rounded toward their
        // RoundedCorner, like the Corner_1 mesh seen from above.
        private void DrawWalls(Painter2D painter)
        {
            Color wall = m_colors[(int)PaletteColor.Wall];
            float cell = m_geometry.CellSize;
            IReadOnlyList<LevelWallGlyph> walls = m_state.Walls;
            for (int index = 0; index < walls.Count; index++)
            {
                LevelWallGlyph glyph = walls[index];
                float side = glyph.Size * cell;
                Vector2 centre = new Vector2(
                    m_geometry.Origin.x + (glyph.Center.x + 0.5f) * cell,
                    m_geometry.Origin.y + (m_geometry.Height - 0.5f - glyph.Center.y) * cell);
                if (!glyph.IsCorner)
                {
                    FillRect(painter, new Rect(centre.x - 0.5f * side, centre.y - 0.5f * side, side, side), wall);
                    continue;
                }

                // Screen y points down, so the board's +y corner is screen -y.
                Vector2 rounded = new Vector2(glyph.RoundedCorner.x, -glyph.RoundedCorner.y);
                Vector2 pivot = centre - rounded * (0.5f * side);
                float start = rounded.x > 0f ? (rounded.y > 0f ? 0f : 270f) : (rounded.y > 0f ? 90f : 180f);
                FillCorner(painter, pivot, side, start, wall);
            }
        }

        // Barrier cells above the floor and below doors and blocks: a hatched Wall tint, dark while the barrier starts
        // closed and light while it starts open, with the barrier's outline so neighbouring barriers stay apart.
        private void DrawBarriers(Painter2D painter)
        {
            Color wall = m_colors[(int)PaletteColor.Wall];
            for (int index = 0; index < m_model.ObstacleCount; index++)
            {
                LevelCanvasObstacle obstacle = m_model.GetObstacle(index);
                if (!obstacle.IsBarrier)
                {
                    continue;
                }

                Color fill = new Color(wall.r, wall.g, wall.b,
                    obstacle.StartsOpen ? k_openBarrierFillAlpha : k_closedBarrierFillAlpha);
                Color line = obstacle.StartsOpen ? wall : s_closedBarrierLineColor;
                for (int part = 0; part < obstacle.CellCount; part++)
                {
                    Vector2Int cell = obstacle.GetCell(part);
                    if (m_model.IsOnBoard(cell))
                    {
                        HatchRect(painter, m_geometry.GetCellRect(cell), fill, line);
                    }
                }

                StrokeOutline(painter, obstacle, wall, k_outlineWidth);
            }
        }

        // Red diagonal lines over what a pending shrink removes.
        private void DrawHatch(Painter2D painter)
        {
            IReadOnlyList<Vector2Int> cells = m_state.HatchCells;
            IReadOnlyList<LevelEdgeSegment> segments = m_state.HatchSegments;
            if (cells.Count == 0 && segments.Count == 0)
            {
                return;
            }

            for (int index = 0; index < cells.Count; index++)
            {
                HatchRect(painter, m_geometry.GetCellRect(cells[index]));
            }

            for (int index = 0; index < segments.Count; index++)
            {
                LevelEdgeSegment segment = segments[index];
                HatchRect(painter, m_geometry.GetEdgeRect(segment.Side, segment.Segment, LevelCanvasGeometry.k_doorDepth));
            }
        }

        private static void HatchRect(Painter2D painter, Rect rect)
        {
            HatchRect(painter, rect, s_hatchFillColor, s_hatchColor);
        }

        private static void HatchRect(Painter2D painter, Rect rect, Color fillColor, Color lineColor)
        {
            FillRect(painter, rect, fillColor);
            painter.strokeColor = lineColor;
            painter.lineWidth = 1.5f;
            painter.lineCap = LineCap.Butt;
            painter.BeginPath();
            // Lines at 45 degrees, clipped to the rectangle by walking the diagonal offset.
            for (float offset = k_hatchSpacing; offset < rect.width + rect.height; offset += k_hatchSpacing)
            {
                Vector2 from = offset <= rect.height
                    ? new Vector2(rect.xMin, rect.yMin + offset)
                    : new Vector2(rect.xMin + offset - rect.height, rect.yMax);
                Vector2 to = offset <= rect.width
                    ? new Vector2(rect.xMin + offset, rect.yMin)
                    : new Vector2(rect.xMax, rect.yMin + offset - rect.width);
                painter.MoveTo(from);
                painter.LineTo(to);
            }

            painter.Stroke();
        }

        // The picked issue: an outline around each of its cells and its gate span.
        private void DrawHighlight(Painter2D painter)
        {
            LevelIssue highlight = m_state.Highlight;
            if (highlight == null)
            {
                return;
            }

            for (int index = 0; index < highlight.Cells.Count; index++)
            {
                Vector2Int cell = highlight.Cells[index];
                if (cell.x >= 0 && cell.x < m_geometry.Width && cell.y >= 0 && cell.y < m_geometry.Height)
                {
                    StrokeRect(painter, m_geometry.GetCellRect(cell), s_highlightColor, k_selectionWidth);
                }
            }

            if (highlight.HasSegment && highlight.SegmentLength > 0)
            {
                // Clipped to the edge like the gate bars; a span wholly off the edge has nothing to outline.
                if (ClampSpan(highlight.Side, highlight.SegmentStart, highlight.SegmentLength, out int first, out int last))
                {
                    StrokeRect(painter, GetDoorRect(highlight.Side, first, last), s_highlightColor, k_selectionWidth);
                }
            }
        }

        // The Pen's gate ghost: the stroke's segments, or the hovered ring segment; red where another colour's gate is
        // or the cell behind the segment is a void.
        private bool DrawDoorGhost(Painter2D painter)
        {
            if (m_state.Tool != LevelEditorTool.Pen)
            {
                return false;
            }

            if (m_state.GateStrokeActive)
            {
                // The window stops a stroke at its first refused segment, so the rest shows red too.
                IReadOnlyList<int> stroke = m_state.GateStroke;
                bool blocked = false;
                for (int index = 0; index < stroke.Count; index++)
                {
                    blocked = !DrawDoorGhostSegment(painter, m_state.GateStrokeSide, stroke[index], blocked) || blocked;
                }

                return true;
            }

            LevelCanvasHit hit = m_state.Hover;
            if (hit.Kind != LevelCanvasHitKind.RingSegment && hit.Kind != LevelCanvasHitKind.Door)
            {
                return false;
            }

            DrawDoorGhostSegment(painter, hit.Side, hit.Index, false);
            return true;
        }

        // Draws one ghost bar and returns whether it is paintable; blocked forces red.
        private bool DrawDoorGhostSegment(Painter2D painter, BoardSide side, int segment, bool blocked)
        {
            PaletteColor toolColor = m_state.ToolColor;
            int doorIndex = m_model.GetDoorIndexAt(side, segment);
            bool valid = !blocked && !m_model.IsVoid(m_model.GetEdgeCell(side, segment))
                         && (doorIndex < 0
                             || (m_model.GetDoor(doorIndex).HasColor && m_model.GetDoor(doorIndex).Color == toolColor));
            Color color = valid ? m_colors[(int)toolColor] : s_invalidGhostColor;
            Rect bar = m_geometry.GetEdgeRect(side, segment, LevelCanvasGeometry.k_doorDepth);
            DrawGhostRect(painter, bar, new Color(color.r, color.g, color.b, 0.6f),
                valid ? Color.white : new Color(color.r, color.g, color.b, 1f));
            return valid;
        }

        // The dragged block at its offset, or the armed shape at the hovered cell: green when every cell is on the
        // board and free, red otherwise. Returns false when there is no ghost to draw.
        private bool DrawGhost(Painter2D painter)
        {
            int dragBlock = m_state.DragBlock;
            Vector2Int dragOffset = m_state.DragOffset;
            if (dragBlock >= 0 && dragBlock < m_model.BlockCount)
            {
                if (dragOffset == Vector2Int.zero)
                {
                    return false;
                }

                LevelCanvasBlock block = m_model.GetBlock(dragBlock);
                bool valid = true;
                for (int index = 0; index < block.CellCount && valid; index++)
                {
                    valid = m_model.IsFree(block.GetCell(index) + dragOffset, dragBlock);
                }

                for (int index = 0; index < block.CellCount; index++)
                {
                    DrawGhostCell(painter, m_geometry.GetCellRect(block.GetCell(index) + dragOffset), valid);
                }

                return true;
            }

            // The armed piece shows only where a Select click would place it; over a block the click selects instead.
            if (!m_state.PlacesArmedPiece)
            {
                return false;
            }

            Vector2Int origin = m_state.Hover.Cell;
            IReadOnlyList<Vector2Int> armedCells = m_state.ArmedCells;
            bool fits = true;
            for (int index = 0; index < armedCells.Count && fits; index++)
            {
                fits = m_model.IsFree(origin + armedCells[index], -1);
            }

            for (int index = 0; index < armedCells.Count; index++)
            {
                DrawGhostCell(painter, m_geometry.GetCellRect(origin + armedCells[index]), fits);
            }

            return true;
        }

        // True when the cell already holds what the current brush paints (a barrier or a void), or, with no brush
        // armed, what any obstacle stroke or the Eraser would erase (a void or barrier cell).
        private bool IsPaintedObstacle(Vector2Int cell)
        {
            if (m_state.ObstacleBrush == LevelObstacleBrush.Barrier) return m_model.IsBarrier(cell);
            if (m_state.ObstacleBrush == LevelObstacleBrush.Void) return m_model.IsVoid(cell);
            return m_model.IsVoid(cell) || m_model.IsBarrier(cell);
        }

        private bool CanPaintObstacle(Vector2Int cell) => m_state.ObstacleBrush == LevelObstacleBrush.Void
            ? m_model.CanPaintWall(cell)
            : m_model.CanPaintBarrier(cell);

        // The obstacle ghost: a paint stroke's cells that are not barrier cells yet, green where the model shows a new
        // barrier may go and red elsewhere; an erase stroke's obstacle cells in green; with no stroke, the Barrier
        // brush's hovered cell (green or red) or the Eraser's hovered void or barrier cell. The commands still decide
        // on release. False when there is none to draw.
        private bool DrawObstacleGhost(Painter2D painter)
        {
            if (m_state.ObstacleStrokeActive)
            {
                IReadOnlyList<Vector2Int> stroke = m_state.ObstacleStroke;
                bool paints = m_state.ObstacleStrokePaints;
                for (int index = 0; index < stroke.Count; index++)
                {
                    Vector2Int cell = stroke[index];
                    if (paints != IsPaintedObstacle(cell))
                    {
                        bool valid = !paints || CanPaintObstacle(cell);
                        DrawGhostCell(painter, m_geometry.GetCellRect(cell), valid);
                    }
                }

                return true;
            }

            LevelCanvasHit hit = m_state.Hover;
            if (m_state.Tool == LevelEditorTool.Pen && m_state.ObstacleBrush != LevelObstacleBrush.None
                && (hit.Kind == LevelCanvasHitKind.Cell || hit.Kind == LevelCanvasHitKind.Block))
            {
                DrawGhostCell(painter, m_geometry.GetCellRect(hit.Cell), CanPaintObstacle(hit.Cell));
                return true;
            }

            if (m_state.Tool == LevelEditorTool.Eraser && hit.Kind == LevelCanvasHitKind.Cell && IsPaintedObstacle(hit.Cell))
            {
                DrawGhostCell(painter, m_geometry.GetCellRect(hit.Cell), true);
                return true;
            }

            return false;
        }

        // The freehand ghost: the stroke's cells in the selected colour, or with no stroke the hovered cell a click
        // would add, green when it can grow the piece and red when it cannot. A block gets none: its own cells (a right
        // click removes one) and another block, which then shows the plain hover because a click edits it. False when
        // there is none to draw.
        private bool DrawPieceGhost(Painter2D painter)
        {
            if (m_state.PieceStrokeActive)
            {
                IReadOnlyList<Vector2Int> stroke = m_state.PieceStroke;
                for (int index = 0; index < stroke.Count; index++)
                {
                    DrawColorGhostCell(painter, m_geometry.GetCellRect(stroke[index]));
                }

                return true;
            }

            LevelCanvasHit hit = m_state.Hover;
            if (!m_state.FreehandPen || hit.Kind != LevelCanvasHitKind.Cell)
            {
                return false;
            }

            DrawGhostCell(painter, m_geometry.GetCellRect(hit.Cell), m_state.HoverCanGrow);
            return true;
        }

        // Every free cell a click would add to the edited block, filled light green under the ghost; the canvas leaves
        // the list empty during a stroke.
        private void DrawGrowCells(Painter2D painter)
        {
            IReadOnlyList<Vector2Int> cells = m_state.GrowCells;
            for (int index = 0; index < cells.Count; index++)
            {
                FillRect(painter, m_geometry.GetCellRect(cells[index]), s_growCellColor);
            }
        }

        // The shared ghost look: a translucent fill with a solid inset border, its own colour.
        private static void DrawGhostRect(Painter2D painter, Rect rect, Color fill, Color border)
        {
            FillRect(painter, rect, fill);
            float inset = 0.5f * k_ghostBorderWidth;
            StrokeRect(painter, new Rect(rect.x + inset, rect.y + inset, rect.width - 2f * inset, rect.height - 2f * inset),
                border, k_ghostBorderWidth);
        }

        // The ghost look in the Pen's colour: a 50 % fill with a solid inset border.
        private void DrawColorGhostCell(Painter2D painter, Rect rect)
        {
            Color color = m_colors[(int)m_state.ToolColor];
            DrawGhostRect(painter, rect, new Color(color.r, color.g, color.b, 0.5f), new Color(color.r, color.g, color.b, 1f));
        }

        // A 50 % fill plus a solid inset border, so a red ghost still reads over a block of a similar colour.
        private static void DrawGhostCell(Painter2D painter, Rect rect, bool valid)
        {
            Color color = valid ? s_validGhostColor : s_invalidGhostColor;
            DrawGhostRect(painter, rect, color, new Color(color.r, color.g, color.b, 1f));
        }

        private void DrawDoor(Painter2D painter, LevelCanvasDoor door)
        {
            if (!TryGetDoorSpan(door, out int first, out int last))
            {
                return;
            }

            Color color = GetColor(door.HasColor, door.Color);
            bool horizontal = door.Side == BoardSide.Top || door.Side == BoardSide.Bottom;
            for (int segment = first; segment <= last; segment++)
            {
                Rect bar = m_geometry.GetEdgeRect(door.Side, segment, LevelCanvasGeometry.k_doorDepth);
                FillRect(painter, horizontal
                    ? new Rect(bar.x + k_doorGap, bar.y, bar.width - 2f * k_doorGap, bar.height)
                    : new Rect(bar.x, bar.y + k_doorGap, bar.width, bar.height - 2f * k_doorGap), color);
            }

            // A small notch in the middle of the door points out of the board, the exit direction. Screen y grows
            // downward, so Vector2.down (0, -1) points up, out through the top edge.
            Rect span = GetDoorRect(door.Side, first, last);
            Vector2 outward;
            Vector2 edge;
            switch (door.Side)
            {
                case BoardSide.Top:
                    outward = Vector2.down;
                    edge = new Vector2(span.center.x, span.yMax);
                    break;
                case BoardSide.Bottom:
                    outward = Vector2.up;
                    edge = new Vector2(span.center.x, span.yMin);
                    break;
                case BoardSide.Left:
                    outward = Vector2.left;
                    edge = new Vector2(span.xMax, span.center.y);
                    break;
                default:
                    outward = Vector2.right;
                    edge = new Vector2(span.xMin, span.center.y);
                    break;
            }

            // The notch keeps its proportions inside the door bar: from a fifth to four fifths of its depth.
            float depth = LevelCanvasGeometry.k_doorDepth * m_geometry.CellSize;
            Vector2 along = new Vector2(outward.y, outward.x);
            Vector2 baseCentre = edge + outward * (0.2f * depth);
            Vector2 apex = edge + outward * (0.8f * depth);
            float halfWidth = 0.27f * depth;
            painter.fillColor = s_notchColor;
            painter.BeginPath();
            painter.MoveTo(baseCentre + along * halfWidth);
            painter.LineTo(apex);
            painter.LineTo(baseCentre - along * halfWidth);
            painter.ClosePath();
            painter.Fill();
        }

        // The door's segments clamped to its side, first to last; false when none of them lies on the side.
        private bool TryGetDoorSpan(LevelCanvasDoor door, out int first, out int last)
        {
            return ClampSpan(door.Side, door.Start, door.Length, out first, out last);
        }

        // A span of length segments starting at start, clamped to the side's length, first to last; false when none
        // of it lies on the side. Shared by a door's span, a picked issue's gate span and a selected door's outline.
        private bool ClampSpan(BoardSide side, int start, int length, out int first, out int last)
        {
            first = Mathf.Max(0, start);
            last = Mathf.Min(m_model.GetSideLength(side), start + length) - 1;
            return last >= first;
        }

        private Rect GetDoorRect(BoardSide side, int first, int last)
        {
            Rect a = m_geometry.GetEdgeRect(side, first, LevelCanvasGeometry.k_doorDepth);
            Rect b = m_geometry.GetEdgeRect(side, last, LevelCanvasGeometry.k_doorDepth);
            return Rect.MinMaxRect(
                Mathf.Min(a.xMin, b.xMin), Mathf.Min(a.yMin, b.yMin),
                Mathf.Max(a.xMax, b.xMax), Mathf.Max(a.yMax, b.yMax));
        }

        private void DrawBlock(Painter2D painter, LevelCanvasBlock block)
        {
            Color color = GetColor(block.HasColor, block.Color);
            for (int index = 0; index < block.CellCount; index++)
            {
                FillRect(painter, m_geometry.GetCellRect(block.GetCell(index)), color);
            }

            StrokeOutline(painter, block, new Color(color.r * 0.6f, color.g * 0.6f, color.b * 0.6f), k_outlineWidth);
        }

        private void DrawHover(Painter2D painter)
        {
            LevelCanvasHit hit = m_state.Hover;
            switch (hit.Kind)
            {
                case LevelCanvasHitKind.Cell:
                    // A void cell is not board, so it takes no hover highlight.
                    if (!m_model.IsVoid(hit.Cell))
                    {
                        FillRect(painter, m_geometry.GetCellRect(hit.Cell), s_hoverColor);
                    }

                    break;
                case LevelCanvasHitKind.Block:
                    LevelCanvasBlock block = m_model.GetBlock(hit.EntityIndex);
                    for (int index = 0; index < block.CellCount; index++)
                    {
                        FillRect(painter, m_geometry.GetCellRect(block.GetCell(index)), s_hoverColor);
                    }

                    break;
                case LevelCanvasHitKind.RingSegment:
                    FillRect(painter, m_geometry.GetEdgeRect(hit.Side, hit.Index, LevelCanvasGeometry.k_ringDepth), s_hoverColor);
                    break;
                case LevelCanvasHitKind.Door:
                    // Every tool that reaches here (not the Pen, which draws its segment ghost) selects or erases the
                    // whole gate, so the whole gate lights up, as a hovered block does.
                    if (TryGetDoorSpan(m_model.GetDoor(hit.EntityIndex), out int first, out int last))
                    {
                        FillRect(painter, GetDoorRect(hit.Side, first, last), s_hoverColor);
                    }

                    break;
            }
        }

        // The selected block, door or board object in white; the block the Pen edits (the growing block) in green,
        // which replaces the white outline when it is also the selection.
        private void DrawSelection(Painter2D painter)
        {
            int edited = m_state.EditedBlock;
            LevelCanvasSelection selection = m_state.Selection;
            if (edited >= 0)
            {
                StrokeOutline(painter, m_model.GetBlock(edited), s_editOutlineColor, k_selectionWidth);
            }

            if (selection.Kind == LevelCanvasSelectionKind.Block)
            {
                if (selection.Index >= 0 && selection.Index < m_model.BlockCount && selection.Index != edited)
                {
                    StrokeOutline(painter, m_model.GetBlock(selection.Index), s_selectionColor, k_selectionWidth);
                }
            }
            else if (selection.Kind == LevelCanvasSelectionKind.Door
                     && selection.Index >= 0 && selection.Index < m_model.DoorCount)
            {
                LevelCanvasDoor door = m_model.GetDoor(selection.Index);
                if (ClampSpan(door.Side, door.Start, door.Length, out int first, out int last))
                {
                    StrokeRect(painter, GetDoorRect(door.Side, first, last), s_selectionColor, k_selectionWidth);
                }
            }
            else if (selection.Kind == LevelCanvasSelectionKind.Obstacle
                     && selection.Index >= 0 && selection.Index < m_model.ObstacleCount)
            {
                StrokeOutline(painter, m_model.GetObstacle(selection.Index), s_selectionColor, k_selectionWidth);
            }
        }

        // A void cell is not part of the board, so it keeps the canvas background like the space outside the walls
        // that wrap it; a board without voids is one rectangle.
        private void DrawFloor(Painter2D painter)
        {
            Color ground = m_colors[(int)PaletteColor.Ground];
            if (m_model.VoidCount == 0)
            {
                FillRect(painter, m_geometry.BoardRect, ground);
                return;
            }

            for (int y = 0; y < m_model.Height; y++)
            {
                for (int x = 0; x < m_model.Width; x++)
                {
                    Vector2Int cell = new Vector2Int(x, y);
                    if (!m_model.IsVoid(cell))
                    {
                        FillRect(painter, m_geometry.GetCellRect(cell), ground);
                    }
                }
            }
        }

        private bool IsFloorPair(Vector2Int first, Vector2Int second)
        {
            return !m_model.IsVoid(first) && !m_model.IsVoid(second);
        }

        private void DrawGrid(Painter2D painter)
        {
            Rect board = m_geometry.BoardRect;
            float cell = m_geometry.CellSize;
            painter.strokeColor = s_gridColor;
            painter.lineWidth = k_gridWidth;
            painter.lineCap = LineCap.Butt;
            painter.BeginPath();
            if (m_model.VoidCount == 0)
            {
                for (int x = 1; x < m_geometry.Width; x++)
                {
                    painter.MoveTo(new Vector2(board.xMin + x * cell, board.yMin));
                    painter.LineTo(new Vector2(board.xMin + x * cell, board.yMax));
                }

                for (int y = 1; y < m_geometry.Height; y++)
                {
                    painter.MoveTo(new Vector2(board.xMin, board.yMin + y * cell));
                    painter.LineTo(new Vector2(board.xMax, board.yMin + y * cell));
                }
            }
            else
            {
                // Only edges between two floor cells: no line runs into a void cell or over the walls wrapping it.
                for (int y = 0; y < m_model.Height; y++)
                {
                    for (int x = 0; x < m_model.Width; x++)
                    {
                        Rect rect = m_geometry.GetCellRect(new Vector2Int(x, y));
                        if (x > 0 && IsFloorPair(new Vector2Int(x - 1, y), new Vector2Int(x, y)))
                        {
                            painter.MoveTo(new Vector2(rect.xMin, rect.yMin));
                            painter.LineTo(new Vector2(rect.xMin, rect.yMax));
                        }

                        if (y > 0 && IsFloorPair(new Vector2Int(x, y - 1), new Vector2Int(x, y)))
                        {
                            // Row y - 1 is below row y on screen, so their shared edge is this cell's bottom.
                            painter.MoveTo(new Vector2(rect.xMin, rect.yMax));
                            painter.LineTo(new Vector2(rect.xMax, rect.yMax));
                        }
                    }
                }
            }

            painter.Stroke();
        }

        // Strokes the block's or board object's cell edges that no other cell of it shares: the union outline.
        private void StrokeOutline(Painter2D painter, ILevelCanvasFootprint block, Color color, float width)
        {
            painter.strokeColor = color;
            painter.lineWidth = width;
            // Round caps close the corners where two separate edge strokes meet.
            painter.lineCap = LineCap.Round;
            painter.BeginPath();
            for (int index = 0; index < block.CellCount; index++)
            {
                Vector2Int cell = block.GetCell(index);
                Rect rect = m_geometry.GetCellRect(cell);
                if (!block.Contains(cell + Vector2Int.up))
                {
                    AddEdge(painter, new Vector2(rect.xMin, rect.yMin), new Vector2(rect.xMax, rect.yMin));
                }

                if (!block.Contains(cell + Vector2Int.down))
                {
                    AddEdge(painter, new Vector2(rect.xMin, rect.yMax), new Vector2(rect.xMax, rect.yMax));
                }

                if (!block.Contains(cell + Vector2Int.left))
                {
                    AddEdge(painter, new Vector2(rect.xMin, rect.yMin), new Vector2(rect.xMin, rect.yMax));
                }

                if (!block.Contains(cell + Vector2Int.right))
                {
                    AddEdge(painter, new Vector2(rect.xMax, rect.yMin), new Vector2(rect.xMax, rect.yMax));
                }
            }

            painter.Stroke();
        }

        private static void AddEdge(Painter2D painter, Vector2 from, Vector2 to)
        {
            painter.MoveTo(from);
            painter.LineTo(to);
        }

        private static void StrokeRect(Painter2D painter, Rect rect, Color color, float width)
        {
            painter.strokeColor = color;
            painter.lineWidth = width;
            painter.lineCap = LineCap.Butt;
            painter.BeginPath();
            AddRectPath(painter, rect);
            painter.Stroke();
        }

        private static void FillRect(Painter2D painter, Rect rect, Color color)
        {
            painter.fillColor = color;
            painter.BeginPath();
            AddRectPath(painter, rect);
            painter.Fill();
        }

        private static void AddRectPath(Painter2D painter, Rect rect)
        {
            painter.MoveTo(new Vector2(rect.xMin, rect.yMin));
            painter.LineTo(new Vector2(rect.xMax, rect.yMin));
            painter.LineTo(new Vector2(rect.xMax, rect.yMax));
            painter.LineTo(new Vector2(rect.xMin, rect.yMax));
            painter.ClosePath();
        }

        // A quarter disc outside one board corner, from startDegrees clockwise through 90 degrees.
        private static void FillCorner(Painter2D painter, Vector2 corner, float radius, float startDegrees, Color color)
        {
            painter.fillColor = color;
            painter.BeginPath();
            painter.MoveTo(corner);
            painter.Arc(corner, radius, Angle.Degrees(startDegrees), Angle.Degrees(startDegrees + 90f));
            painter.ClosePath();
            painter.Fill();
        }

        private Color GetColor(bool hasColor, PaletteColor color)
        {
            return hasColor ? m_colors[(int)color] : s_unknownColor;
        }
    }
}
