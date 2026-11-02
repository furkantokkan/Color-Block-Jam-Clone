using System;
using System.Collections.Generic;
using ColorBlockJam.Runtime.Art;
using ColorBlockJam.Runtime.Board;
using ColorBlockJam.Runtime.Levels;
using UnityEngine;
using UnityEngine.UIElements;

namespace ColorBlockJam.Editor.Levels
{
    /// <summary>
    /// Level Editor canvas: turns pointer gestures on one immutable <see cref="LevelCanvasModel"/> into requests for
    /// the window (select, place, move, rotate or mirror, remove, paint gates, paint or erase barrier or void cells,
    /// draw or edit a piece with the Pen, resize) while it keeps the hover, the view and the block labels;
    /// <see cref="LevelCanvasPainter"/> draws the board, <see cref="LevelCanvasEdgeControls"/> owns the resize frame and
    /// <see cref="LevelCanvasToolBadge"/> the pointer badge. It never changes the draft. The board (floor to effect
    /// underlays) repaints only when its model, walls, palette or geometry changes; hover, ghost, stroke, selection,
    /// tool, hatch and highlight repaint only the overlay child above it, and a badge move repaints nothing.
    /// </summary>
    internal sealed class LevelCanvasElement : VisualElement
    {
        internal const string k_name = "level-canvas";
        internal const string k_labelClass = "level-canvas__block-label";
        internal const string k_toolBadgeName = LevelCanvasToolBadge.ElementName;
        internal const float k_toolBadgeOffset = LevelCanvasToolBadge.PointerOffset;
        // The block label's dark outline, so the id reads over the Arrow badge and on every block colour.
        internal static readonly Color s_labelOutlineColor = new Color(0.08f, 0.08f, 0.1f, 1f);
        internal const float k_labelOutlineWidth = 1f;
        private const float k_wheelZoomStep = 1.1f;

        // The pointer gestures, one at a time: each captures one pointer until release, capture loss, Esc, a tool,
        // brush or mode change or a new model (CancelDrag).
        private enum CanvasGesture
        {
            None,
            Pan,
            BlockDrag,
            GateStroke,
            ObstacleStroke,
            PieceStroke
        }

        private readonly LevelCanvasPainter m_painter = new LevelCanvasPainter();
        // The pointer-driven layers (hover, ghosts, strokes, selection, grid, overlays) on their own mesh above the
        // board, so a hover change re-tessellates only these and never the floor, walls and blocks.
        private readonly VisualElement m_overlay = new VisualElement { name = "level-canvas-overlay", pickingMode = PickingMode.Ignore };
        private readonly LevelCanvasEdgeControls m_edgeControls;
        // A square frame with the tool's icon; the Select tool shows none, the system arrow is its pointer.
        private readonly LevelCanvasToolBadge m_toolBadge = new LevelCanvasToolBadge();
        private readonly List<Label> m_labels = new List<Label>();
        private readonly LevelCanvasHover m_hover = new LevelCanvasHover();
        private LevelCanvasModel m_model = LevelCanvasModel.Empty;
        private LevelCanvasGeometry m_geometry;
        private LevelCanvasSelection m_selection;
        private LevelEditorTool m_tool;
        private PaletteColor m_toolColor;
        private IReadOnlyList<Vector2Int> m_armedCells = Array.Empty<Vector2Int>();
        // The last pointer position over the canvas, so a tool switched by key shows its badge without a move.
        private bool m_pointerInside;
        private Vector2 m_pointerPosition;
        // Effect layers (design "Block Effects Architecture" section 6). Underlays paint just above the blocks (Ice
        // frost); overlays paint above the grid, so the grid line on an even-width block's centre line cannot run
        // through an Arrow badge's shaft. m_effectLayers holds all of them in registration order, Ice then Arrow,
        // which is also their badge order below a block's name.
        private readonly ILevelCanvasEffectLayer[] m_effectUnderlays = { new IceCanvasEffectLayer() };
        private readonly ILevelCanvasEffectLayer[] m_effectOverlays = { new ArrowCanvasEffectLayer() };
        private readonly ILevelCanvasEffectLayer[] m_effectLayers;
        private readonly LevelCanvasEffectBadgeLayout m_effectBadges = new LevelCanvasEffectBadgeLayout();
        // Wall and corner glyphs composed from the environment rule set; the window replaces them per draft change.
        private IReadOnlyList<LevelWallGlyph> m_walls = Array.Empty<LevelWallGlyph>();
        // Cells and gate segments a pending board shrink would remove, drawn hatched until confirmed or cancelled.
        private IReadOnlyList<Vector2Int> m_hatchCells = Array.Empty<Vector2Int>();
        private IReadOnlyList<LevelEdgeSegment> m_hatchSegments = Array.Empty<LevelEdgeSegment>();
        // The issue picked in the issues list, outlined on the board; null when none.
        private LevelIssue m_highlight;
        // The designer's view: wheel zoom about the view centre and a middle-button or Alt+drag pan, in pixels.
        private float m_zoom = LevelCanvasGeometry.k_defaultZoom;
        private Vector2 m_pan;
        private Vector2 m_panLast;
        // The gesture in progress and the pointer it captured; -1 while none is.
        private CanvasGesture m_gesture;
        private int m_gesturePointerId = -1;
        // Pen stroke on the wall ring: the side it started on and the segments it crossed, in order.
        private readonly List<int> m_doorStroke = new List<int>();
        private BoardSide m_doorStrokeSide;
        // The Pen's obstacle brush (Barrier): a left press on the board paints obstacles instead of placing a piece.
        private LevelObstacleBrush m_obstacleBrush;
        // Obstacle stroke: the board cells it crossed, each once and in order, whether it paints barrier or void cells
        // or erases them, and the last cell its walk reached, which may be off the board.
        private readonly List<Vector2Int> m_obstacleStroke = new List<Vector2Int>();
        private bool m_obstacleStrokePaints;
        private Vector2Int m_obstacleStrokeEnd;
        // The Pen's freehand mode ("New piece" tile) and its stroke: the free cells it crossed, each once and in order,
        // the last cell its walk reached and a reused walk buffer, shared with the obstacle stroke since only one
        // stroke is active at a time.
        private bool m_freehand;
        private readonly List<Vector2Int> m_pieceStroke = new List<Vector2Int>();
        private readonly List<Vector2Int> m_strokeWalk = new List<Vector2Int>();
        private Vector2Int m_pieceStrokeEnd;
        // The block the freehand piece grows, by id so it survives a new model; null before the piece's first cell.
        private string m_freehandBlockId;
        // True while freehand mode serves a plain Pen edit of a placed block rather than a "+" New piece drawing: a
        // click on empty space then ends the edit instead of starting a stroke.
        private bool m_penEditing;
        // Reused by every draw for the free cells the growing block can take, so a repaint allocates nothing.
        private readonly List<Vector2Int> m_growCells = new List<Vector2Int>();
        // Select-tool drag of one block: its index (-1 when none), the pressed cell and the current offset from it, in
        // cells.
        private int m_dragBlock = -1;
        private Vector2Int m_dragStart;
        private Vector2Int m_dragOffset;

        public LevelCanvasElement()
        {
            name = k_name;
            m_effectLayers = new ILevelCanvasEffectLayer[m_effectUnderlays.Length + m_effectOverlays.Length];
            m_effectUnderlays.CopyTo(m_effectLayers, 0);
            m_effectOverlays.CopyTo(m_effectLayers, m_effectUnderlays.Length);
            focusable = true;
            style.flexGrow = 1;
            // The pointer badge near an edge stays inside the canvas instead of covering the bottom bar.
            style.overflow = Overflow.Hidden;
            // Every callback lives as long as this element; it is dropped with the window GUI.
            generateVisualContent += OnGenerateVisualContent;
            // First child, so the edge controls, labels and badge still draw above it.
            m_overlay.style.position = Position.Absolute;
            m_overlay.style.left = 0;
            m_overlay.style.top = 0;
            m_overlay.style.right = 0;
            m_overlay.style.bottom = 0;
            m_overlay.generateVisualContent += OnGenerateOverlayContent;
            Add(m_overlay);
            RegisterCallback<GeometryChangedEvent>(_ => UpdateGeometry());
            RegisterCallback<PointerMoveEvent>(OnPointerMove);
            RegisterCallback<WheelEvent>(OnWheel);
            RegisterCallback<PointerLeaveEvent>(_ =>
            {
                m_pointerInside = false;
                m_toolBadge.Hide();
                SetHover(LevelCanvasHit.Outside);
            });
            RegisterCallback<PointerDownEvent>(OnPointerDown);
            RegisterCallback<PointerUpEvent>(OnPointerUp);
            RegisterCallback<PointerCaptureOutEvent>(_ =>
            {
                m_toolBadge.Hide();
                CancelDrag();
            });
            // Right click removes; no context menu opens over the canvas.
            RegisterCallback<ContextClickEvent>(evt => evt.StopPropagation());

            // The edge controls are added before the badge, so the badge draws over them.
            m_edgeControls = new LevelCanvasEdgeControls(this,
                (side, delta) => ResizeRequested?.Invoke(side, delta),
                (corner, cut) => CornerRequested?.Invoke(LevelCanvasEdgeControls.GetCornerCell(m_model, corner), cut));
            Add(m_toolBadge.Element);
            RegisterCallback<DetachFromPanelEvent>(_ =>
            {
                for (int index = 0; index < m_effectLayers.Length; index++) m_effectLayers[index].Dispose();
                m_effectBadges.Dispose();
            });
            RegisterCallback<AttachToPanelEvent>(_ => RefreshLabels());
            UpdateToolBadgeLook();
        }

        /// <summary>Raised when a Select-tool left click lands on a block or a door. Subscriber: the window.</summary>
        public event Action<LevelCanvasSelection> SelectionRequested;
        /// <summary>Raised when a Pen left click lands on a board cell: the origin for the armed shape.</summary>
        public event Action<Vector2Int> PlaceRequested;
        /// <summary>Raised when a dragged block is released elsewhere: block index and the offset in cells.</summary>
        public event Action<int, Vector2Int> MoveRequested;
        /// <summary>Raised by a rotate- or mirror-tool left click on a block: its index and the active tool.</summary>
        public event Action<int, LevelEditorTool> TransformRequested;
        /// <summary>Raised by a right click, or an Eraser left click, on a block: its index.</summary>
        public event Action<int> RemoveRequested;
        /// <summary>Raised when a Pen stroke on the wall ring ends: its side and the segments it crossed, in order.</summary>
        public event Action<BoardSide, IReadOnlyList<int>> DoorStrokeRequested;
        /// <summary>
        /// Raised by the Eraser (left or right click), or a right click with any tool but the Pen, on a gate: its door
        /// index. The window removes the whole gate.
        /// </summary>
        public event Action<int> DoorRemoveRequested;
        /// <summary>
        /// Raised by a Pen right click on a gate: its door index, side and the clicked segment. The window takes that
        /// one segment off (TASK-20260926-212320311-949d4415-level-editor-pen-gate-segment-remove). Subscriber: the
        /// window.
        /// </summary>
        public event Action<int, BoardSide, int> DoorSegmentRemoveRequested;
        /// <summary>Raised by an edge button: the side and +1 to add a line there or -1 to remove its edge line.</summary>
        public event Action<BoardSide, int> ResizeRequested;
        /// <summary>Raised when the Pen presses a ring corner, where no gate can sit.</summary>
        public event Action GateCornerRefused;
        /// <summary>Raised by a corner button: the board corner cell, and true to cut it into a void or false to fill it.</summary>
        public event Action<Vector2Int, bool> CornerRequested;
        /// <summary>
        /// Raised when an obstacle stroke ends: the board cells it crossed, each once and in order, and true to paint
        /// barrier cells (the Barrier brush) or false to erase obstacles (the Eraser, or a right drag with the brush).
        /// </summary>
        public event Action<IReadOnlyList<Vector2Int>, bool> ObstacleStrokeRequested;
        /// <summary>
        /// Raised when a freehand Pen click or stroke ends: the free board cells it crossed, each once and in order,
        /// including cells of the piece's own block. The window adds them to the piece's block or refuses them.
        /// Subscriber: the window.
        /// </summary>
        public event Action<IReadOnlyList<Vector2Int>> PieceStrokeRequested;
        /// <summary>
        /// Raised by a freehand Pen right click on a cell of the piece's own block: the window removes that cell or
        /// refuses. Subscriber: the window.
        /// </summary>
        public event Action<Vector2Int> PieceCellRemoveRequested;
        /// <summary>
        /// Raised by a Pen left press on a block that is not the growing block, with or without freehand ("+" armed
        /// too), unless the Barrier brush is armed: that block's index, the block the Pen edits next. A press on the
        /// growing block's own cells or a free cell still starts a stroke. Subscriber: the window.
        /// </summary>
        public event Action<int> PenBlockPicked;
        /// <summary>
        /// Raised by a left or right click on empty space with Select or the plain Pen (user order of 2026-09-26): a
        /// board floor cell with no block or board object, or the area outside the board and its gate ring. While the
        /// Pen edits a block, a left click on one of its green cells still adds that cell, and Select with an armed
        /// piece still places it on a free cell. A "+" New piece drawing and the Barrier brush keep their own clicks.
        /// Subscriber: the window, which ends a Pen edit and clears the selection.
        /// </summary>
        public event Action EmptySpaceClicked;

        internal LevelCanvasModel Model => m_model;
        /// <summary>True while the Pen draws new pieces freehand.</summary>
        internal bool IsFreehand => m_freehand;
        /// <summary>True while freehand mode serves a plain Pen edit of a placed block.</summary>
        internal bool IsPenEditing => m_penEditing;
        /// <summary>True while a freehand stroke is in progress.</summary>
        internal bool IsPieceStrokeActive => m_gesture == CanvasGesture.PieceStroke;
        internal IReadOnlyList<Vector2Int> PieceStroke => m_pieceStroke;
        /// <summary>The id of the block the freehand piece grows, or null.</summary>
        internal string FreehandBlockId => m_freehandBlockId;
        internal LevelCanvasGeometry Geometry => m_geometry;
        internal LevelCanvasSelection Selection => m_selection;
        internal LevelCanvasHit Hover => m_hover.Current;
        internal LevelEditorTool Tool => m_tool;
        internal bool IsDragging => m_gesture == CanvasGesture.BlockDrag;
        internal bool IsPaintingGate => m_gesture == CanvasGesture.GateStroke;
        /// <summary>True while a barrier or void paint or erase stroke is in progress.</summary>
        internal bool IsObstacleStrokeActive => m_gesture == CanvasGesture.ObstacleStroke;
        internal LevelObstacleBrush ObstacleBrush => m_obstacleBrush;
        internal bool IsPanning => m_gesture == CanvasGesture.Pan;
        internal float Zoom => m_zoom;
        internal Vector2 Pan => m_pan;
        internal IReadOnlyList<LevelWallGlyph> Walls => m_walls;
        internal IReadOnlyList<Vector2Int> HatchCells => m_hatchCells;
        internal LevelIssue Highlight => m_highlight;
        internal Vector2Int DragOffset => m_dragOffset;
        internal VisualElement ToolBadge => m_toolBadge.Element;
        internal Image ToolBadgeIcon => m_toolBadge.Icon;

        internal static string GetEdgeButtonName(BoardSide side, bool add)
        {
            return LevelCanvasEdgeControls.GetEdgeButtonName(side, add);
        }

        internal static string GetCornerButtonName(int cornerIndex, bool cut)
        {
            return LevelCanvasEdgeControls.GetCornerButtonName(cornerIndex, cut);
        }

        internal Button GetEdgeButton(BoardSide side, bool add)
        {
            return m_edgeControls.GetEdgeButton(side, add);
        }

        /// <summary>The registered effect layer of this type, or null; for tests.</summary>
        internal T GetEffectLayer<T>() where T : class, ILevelCanvasEffectLayer
        {
            for (int index = 0; index < m_effectLayers.Length; index++)
            {
                if (m_effectLayers[index] is T layer)
                {
                    return layer;
                }
            }

            return null;
        }

        /// <summary>The colour the canvas draws for a palette entry, so the swatches match the board.</summary>
        internal Color GetPaletteColor(PaletteColor color)
        {
            return m_painter.GetPaletteColor(color);
        }

        /// <summary>Caches the palette colours once; entries missing from the asset keep their fallback.</summary>
        public void SetPalette(GamePalette palette)
        {
            m_painter.SetPalette(palette);
            RefreshLabels();
            UpdateToolBadgeLook();
            RepaintAll();
        }

        public void SetModel(LevelCanvasModel model)
        {
            m_model = model ?? LevelCanvasModel.Empty;
            // A drag that outlives its model (undo, reload) would move a block that may no longer exist.
            CancelDrag();
            m_hover.Update(LevelCanvasHit.Outside);
            // Labels first: UpdateGeometry repositions every label, so the list must already match the new blocks.
            RefreshLabels();
            UpdateGeometry();
            m_edgeControls.Refresh(m_model);
            RepaintAll();
        }

        /// <summary>The wall and corner glyphs of the current model; an empty list draws no walls.</summary>
        public void SetWalls(IReadOnlyList<LevelWallGlyph> walls)
        {
            m_walls = walls ?? Array.Empty<LevelWallGlyph>();
            RepaintAll();
        }

        /// <summary>Hatches the cells and gate segments a pending shrink would remove; empty lists clear it.</summary>
        public void SetHatch(IReadOnlyList<Vector2Int> cells, IReadOnlyList<LevelEdgeSegment> segments)
        {
            m_hatchCells = cells ?? Array.Empty<Vector2Int>();
            m_hatchSegments = segments ?? Array.Empty<LevelEdgeSegment>();
            m_overlay.MarkDirtyRepaint();
        }

        /// <summary>Outlines one issue's cells and gate span; null clears the outline.</summary>
        public void SetIssueHighlight(LevelIssue issue)
        {
            if (ReferenceEquals(issue, m_highlight))
            {
                return;
            }

            m_highlight = issue;
            m_overlay.MarkDirtyRepaint();
        }

        public void SetSelection(LevelCanvasSelection selection)
        {
            if (selection.Equals(m_selection))
            {
                return;
            }

            m_selection = selection;
            m_overlay.MarkDirtyRepaint();
        }

        /// <summary>Sets the left-click tool and, for Select, the armed shape's cells that the ghost shows.</summary>
        public void SetTool(LevelEditorTool tool, IReadOnlyList<Vector2Int> armedCells)
        {
            // A gate or piece stroke belongs to the Pen and an obstacle stroke to the tool that began it; another tool
            // drops them unpainted.
            if ((tool != LevelEditorTool.Pen && (IsPaintingGate || IsPieceStrokeActive))
                || (tool != m_tool && IsObstacleStrokeActive))
            {
                CancelDrag();
            }

            m_tool = tool;
            m_armedCells = armedCells ?? Array.Empty<Vector2Int>();
            UpdateToolBadgeLook();
            m_overlay.MarkDirtyRepaint();
        }

        /// <summary>Arms or disarms the Pen's obstacle brush; a change drops an obstacle stroke in progress unpainted.</summary>
        public void SetObstacleBrush(LevelObstacleBrush brush)
        {
            if (brush == m_obstacleBrush)
            {
                return;
            }

            if (IsObstacleStrokeActive)
            {
                CancelDrag();
            }

            m_obstacleBrush = brush;
            UpdateToolBadgeLook();
            m_overlay.MarkDirtyRepaint();
        }

        /// <summary>Turns the Pen's freehand mode on or off; a change drops a freehand stroke in progress unplaced.</summary>
        public void SetFreehand(bool freehand)
        {
            if (freehand == m_freehand)
            {
                return;
            }

            if (IsPieceStrokeActive)
            {
                CancelDrag();
            }

            m_freehand = freehand;
            m_overlay.MarkDirtyRepaint();
        }

        /// <summary>
        /// True when freehand mode serves a plain Pen edit of a placed block, false for a "+" New piece drawing: only
        /// the edit ends on a click on empty space. Changes no drawing, so it needs no repaint.
        /// </summary>
        public void SetPenEditing(bool editing)
        {
            m_penEditing = editing;
        }

        /// <summary>
        /// The block the freehand piece grows: a click or stroke may cross its cells, the ghost is green only next to
        /// it, and a right click on it removes one cell. Null before the piece's first cell.
        /// </summary>
        public void SetFreehandBlock(string blockId)
        {
            if (string.Equals(blockId, m_freehandBlockId, StringComparison.Ordinal))
            {
                return;
            }

            m_freehandBlockId = blockId;
            m_overlay.MarkDirtyRepaint();
        }

        /// <summary>The selected colour; the Pen's badge takes it. Changes only the badge, never the board.</summary>
        public void SetToolColor(PaletteColor color)
        {
            m_toolColor = color;
            UpdateToolBadgeLook();
        }

        /// <summary>
        /// Drops the gesture in progress without changing the draft (Esc, lost capture, new model): a block drag, a
        /// gate, obstacle or freehand piece stroke, or a pan. False when none was in progress.
        /// </summary>
        public bool CancelDrag()
        {
            if (m_gesture == CanvasGesture.None)
            {
                return false;
            }

            int pointerId = m_gesturePointerId;
            m_gesture = CanvasGesture.None;
            m_gesturePointerId = -1;
            m_pieceStroke.Clear();
            m_dragBlock = -1;
            m_dragOffset = Vector2Int.zero;
            m_doorStroke.Clear();
            m_obstacleStroke.Clear();
            if (this.HasPointerCapture(pointerId))
            {
                this.ReleasePointer(pointerId);
            }

            m_overlay.MarkDirtyRepaint();
            return true;
        }

        /// <summary>The hit under a local point, for the pointer handlers and tests.</summary>
        internal LevelCanvasHit HitTest(Vector2 localPoint)
        {
            return LevelCanvasHitTest.Test(m_model, m_geometry, localPoint);
        }

        // Owner: this element; stop point: pointer up, capture loss, Esc, a tool, brush or mode change or a new model
        // (CancelDrag).
        private void StartGesture(CanvasGesture gesture, int pointerId)
        {
            m_gesture = gesture;
            m_gesturePointerId = pointerId;
            this.CapturePointer(pointerId);
        }

        private void OnPointerMove(PointerMoveEvent evt)
        {
            m_pointerInside = true;
            m_pointerPosition = evt.localPosition;
            MoveToolBadge(m_pointerPosition);
            if (m_gesture == CanvasGesture.None || evt.pointerId != m_gesturePointerId)
            {
                SetHover(HitTest(evt.localPosition));
                return;
            }

            switch (m_gesture)
            {
                case CanvasGesture.Pan:
                    PanTo(evt.localPosition);
                    break;
                case CanvasGesture.GateStroke:
                    ExtendDoorStroke(evt.localPosition);
                    break;
                case CanvasGesture.ObstacleStroke:
                    ExtendObstacleStroke(evt.localPosition);
                    break;
                case CanvasGesture.PieceStroke:
                    ExtendPieceStroke(evt.localPosition);
                    break;
                case CanvasGesture.BlockDrag:
                    DragBlockTo(evt.localPosition);
                    break;
            }
        }

        private void OnPointerDown(PointerDownEvent evt)
        {
            Focus();
            // Middle button, or Alt with the left button, pans the view with every tool.
            if (evt.button == 2 || (evt.button == 0 && evt.altKey))
            {
                m_panLast = evt.localPosition;
                StartGesture(CanvasGesture.Pan, evt.pointerId);
                evt.StopPropagation();
                return;
            }

            LevelCanvasHit hit = HitTest(evt.localPosition);
            SetHover(hit);
            if (evt.button == 1)
            {
                // One right-click rule everywhere: right click removes (design section 2). With the Barrier brush, a
                // right press on a board cell starts a drag that erases barrier cells; a block or gate is still removed
                // (a gate by one segment, since the brush belongs to the Pen).
                if (m_tool == LevelEditorTool.Pen && m_obstacleBrush != LevelObstacleBrush.None && hit.Kind == LevelCanvasHitKind.Cell)
                {
                    StartObstacleStroke(hit.Cell, false, evt.pointerId);
                }
                else if (IsFreehandPen() && hit.Kind == LevelCanvasHitKind.Block
                         && hit.EntityIndex == GetFreehandBlockIndex())
                {
                    // While a piece grows, a right click takes one cell off it instead of removing the whole block.
                    PieceCellRemoveRequested?.Invoke(hit.Cell);
                }
                else if (ClearsSelectionAt(evt, hit))
                {
                    // Nothing to remove on empty space: the click ends a Pen edit and clears the selection instead.
                    EmptySpaceClicked?.Invoke();
                }
                else
                {
                    // Only the Pen's right click takes one gate segment off; every other tool removes the whole gate.
                    RequestRemove(hit, m_tool == LevelEditorTool.Pen);
                }

                evt.StopPropagation();
                return;
            }

            if (evt.button != 0)
            {
                return;
            }

            switch (m_tool)
            {
                case LevelEditorTool.Pen:
                    if (ClearsSelectionAt(evt, hit))
                    {
                        // Empty space ends a Pen edit and clears the selection (user order of 2026-09-26).
                        EmptySpaceClicked?.Invoke();
                    }
                    else if (hit.Kind == LevelCanvasHitKind.Cell || hit.Kind == LevelCanvasHitKind.Block)
                    {
                        // The Barrier brush paints obstacles from here; a press on another block picks it for editing
                        // (user order of 2026-09-26); freehand draws a piece or grows the edited block from a free cell
                        // or its own cell; otherwise the window says what the Pen needs.
                        if (m_obstacleBrush != LevelObstacleBrush.None)
                        {
                            StartObstacleStroke(hit.Cell, true, evt.pointerId);
                        }
                        else if (hit.Kind == LevelCanvasHitKind.Block && hit.EntityIndex != GetFreehandBlockIndex())
                        {
                            PenBlockPicked?.Invoke(hit.EntityIndex);
                        }
                        else if (m_freehand)
                        {
                            if (IsFreehandCell(hit.Cell))
                            {
                                StartPieceStroke(hit.Cell, evt.pointerId);
                            }
                        }
                        else
                        {
                            PlaceRequested?.Invoke(hit.Cell);
                        }
                    }
                    else if (hit.Kind == LevelCanvasHitKind.RingSegment || hit.Kind == LevelCanvasHitKind.Door)
                    {
                        StartDoorStroke(hit, evt.pointerId);
                    }
                    else if (IsRingCorner(evt.localPosition))
                    {
                        GateCornerRefused?.Invoke();
                    }

                    break;
                case LevelEditorTool.RotateCounterClockwise:
                case LevelEditorTool.RotateClockwise:
                case LevelEditorTool.MirrorHorizontal:
                case LevelEditorTool.MirrorVertical:
                    if (hit.Kind == LevelCanvasHitKind.Block)
                    {
                        TransformRequested?.Invoke(hit.EntityIndex, m_tool);
                    }

                    break;
                case LevelEditorTool.Eraser:
                    // A block or gate goes at once; a press on a board cell starts a drag that clears void or barrier cells.
                    if (hit.Kind == LevelCanvasHitKind.Cell)
                    {
                        StartObstacleStroke(hit.Cell, false, evt.pointerId);
                    }
                    else
                    {
                        RequestRemove(hit, false);
                    }

                    break;
                default:
                    // Select with an armed piece places it on a free board cell (user order of 2026-09-26); a block,
                    // a board object or a gate is still selected as usual, and other empty space clears the selection.
                    if (IsPlacingOn(hit))
                    {
                        PlaceRequested?.Invoke(hit.Cell);
                    }
                    else if (ClearsSelectionAt(evt, hit))
                    {
                        EmptySpaceClicked?.Invoke();
                    }
                    else
                    {
                        SelectOrStartDrag(hit, evt.pointerId);
                    }

                    break;
            }
        }

        // True when Select holds an armed piece and the hit is a board cell with no block or board object on it.
        private bool IsPlacingOn(LevelCanvasHit hit)
        {
            return m_tool == LevelEditorTool.Select && m_armedCells.Count > 0 && hit.Kind == LevelCanvasHitKind.Cell
                   && hit.EntityIndex < 0;
        }

        // True when this press raises EmptySpaceClicked: Select, or the Pen without the Barrier brush and not drawing a
        // "+" New piece, on empty space. Empty space is a board floor cell with no block or board object, or the area
        // outside the board and its gate ring; a void cell, a gate, a ring segment, a ring corner and the edge and
        // corner buttons (the only children that take the pointer) keep their own clicks. A left click on a green cell
        // of the block the Pen edits still adds that cell.
        private bool ClearsSelectionAt(PointerDownEvent evt, LevelCanvasHit hit)
        {
            bool clears = evt.target == this
                          && (m_tool == LevelEditorTool.Select
                              || (m_tool == LevelEditorTool.Pen && m_obstacleBrush == LevelObstacleBrush.None
                                  && (!m_freehand || m_penEditing)));
            if (!clears)
            {
                return false;
            }

            if (hit.Kind == LevelCanvasHitKind.Outside)
            {
                return !IsRingCorner(evt.localPosition);
            }

            return hit.Kind == LevelCanvasHitKind.Cell && hit.EntityIndex < 0 && !m_model.IsVoid(hit.Cell)
                   && !(evt.button == 0 && IsFreehandPen() && CanGrowPieceAt(hit.Cell));
        }

        private void SelectOrStartDrag(LevelCanvasHit hit, int pointerId)
        {
            if (hit.Kind == LevelCanvasHitKind.Door)
            {
                SelectionRequested?.Invoke(LevelCanvasSelection.ForDoor(hit.EntityIndex, hit.EntityId));
                return;
            }

            // A board cell a board object covers selects that object, a barrier for example; it never drags.
            if (hit.Kind == LevelCanvasHitKind.Cell && hit.EntityIndex >= 0)
            {
                SelectionRequested?.Invoke(LevelCanvasSelection.ForObstacle(hit.EntityIndex, hit.EntityId));
                return;
            }

            if (hit.Kind != LevelCanvasHitKind.Block)
            {
                return;
            }

            SelectionRequested?.Invoke(LevelCanvasSelection.ForBlock(hit.EntityIndex, hit.EntityId));
            m_dragBlock = hit.EntityIndex;
            m_dragStart = hit.Cell;
            m_dragOffset = Vector2Int.zero;
            StartGesture(CanvasGesture.BlockDrag, pointerId);
        }

        private void DragBlockTo(Vector2 localPoint)
        {
            Vector2Int offset = m_geometry.GetCellUnclamped(localPoint) - m_dragStart;
            if (offset != m_dragOffset)
            {
                m_dragOffset = offset;
                m_overlay.MarkDirtyRepaint();
            }
        }

        private void PanTo(Vector2 localPoint)
        {
            m_pan += localPoint - m_panLast;
            m_panLast = localPoint;
            UpdateGeometry();
        }

        private void OnPointerUp(PointerUpEvent evt)
        {
            if (m_gesture == CanvasGesture.None || evt.pointerId != m_gesturePointerId)
            {
                return;
            }

            switch (m_gesture)
            {
                case CanvasGesture.Pan:
                    CancelDrag();
                    break;
                case CanvasGesture.GateStroke:
                    FinishDoorStroke(evt.localPosition);
                    break;
                case CanvasGesture.ObstacleStroke:
                    FinishObstacleStroke(evt.localPosition);
                    break;
                case CanvasGesture.PieceStroke:
                    FinishPieceStroke(evt.localPosition);
                    break;
                case CanvasGesture.BlockDrag:
                    FinishBlockDrag(evt.localPosition);
                    break;
            }
        }

        private void FinishBlockDrag(Vector2 localPoint)
        {
            int block = m_dragBlock;
            Vector2Int offset = m_dragOffset;
            CancelDrag();
            SetHover(HitTest(localPoint));
            if (offset != Vector2Int.zero)
            {
                // The window applies it through the move command; a refused move leaves the block where it was.
                MoveRequested?.Invoke(block, offset);
            }
        }

        // The wheel zooms about the view centre (user request: "orta noktaya"), so the board part in the middle stays.
        private void OnWheel(WheelEvent evt)
        {
            if (!m_geometry.IsValid || evt.delta.y == 0f)
            {
                return;
            }

            ZoomAt(contentRect.center, evt.delta.y < 0f ? m_zoom * k_wheelZoomStep : m_zoom / k_wheelZoomStep);
            evt.StopPropagation();
        }

        /// <summary>
        /// Sets the zoom, clamped between half the fit and about three cells across, and keeps the board point
        /// under localPoint in place.
        /// </summary>
        internal void ZoomAt(Vector2 localPoint, float zoom)
        {
            LevelCanvasGeometry before = m_geometry;
            Vector2 boardPoint = (localPoint - before.Origin) / before.CellSize;
            m_zoom = Mathf.Clamp(zoom, LevelCanvasGeometry.k_minZoom,
                LevelCanvasGeometry.GetMaxZoom(contentRect.size, m_model.Width, m_model.Height));
            LevelCanvasGeometry centred = LevelCanvasGeometry.Create(contentRect.size, m_model.Width, m_model.Height, m_zoom);
            m_pan = localPoint - boardPoint * centred.CellSize - centred.Origin;
            UpdateGeometry();
        }

        /// <summary>Back to the first view: the default zoom with the board centred (Home).</summary>
        public void ResetView()
        {
            m_zoom = LevelCanvasGeometry.k_defaultZoom;
            m_pan = Vector2.zero;
            UpdateGeometry();
        }

        // Right click, or the Eraser, on a block or a gate. penRightClick takes only the clicked gate segment off.
        private void RequestRemove(LevelCanvasHit hit, bool penRightClick)
        {
            if (hit.Kind == LevelCanvasHitKind.Block)
            {
                RemoveRequested?.Invoke(hit.EntityIndex);
            }
            else if (hit.Kind == LevelCanvasHitKind.Door && penRightClick)
            {
                DoorSegmentRemoveRequested?.Invoke(hit.EntityIndex, hit.Side, hit.Index);
            }
            else if (hit.Kind == LevelCanvasHitKind.Door)
            {
                DoorRemoveRequested?.Invoke(hit.EntityIndex);
            }
            else if (hit.Kind == LevelCanvasHitKind.Cell && (m_model.IsVoid(hit.Cell) || m_model.IsBarrier(hit.Cell)))
            {
                ObstacleStrokeRequested?.Invoke(new[] { hit.Cell }, false);
            }
        }

        // Pen on the wall ring: a stroke along one side, shown as a gate ghost until the pointer is released.
        private void StartDoorStroke(LevelCanvasHit hit, int pointerId)
        {
            m_doorStroke.Clear();
            m_doorStroke.Add(hit.Index);
            m_doorStrokeSide = hit.Side;
            StartGesture(CanvasGesture.GateStroke, pointerId);
            m_overlay.MarkDirtyRepaint();
        }

        // Adds every segment between the last one and the pointer's projection on the stroke's side, so a fast
        // drag leaves no gap.
        private void ExtendDoorStroke(Vector2 localPoint)
        {
            int target = GetSegmentAlong(m_doorStrokeSide, localPoint);
            int last = m_doorStroke[m_doorStroke.Count - 1];
            if (target == last)
            {
                return;
            }

            int step = target > last ? 1 : -1;
            for (int segment = last + step; segment != target + step; segment += step)
            {
                if (!m_doorStroke.Contains(segment))
                {
                    m_doorStroke.Add(segment);
                }
            }

            m_overlay.MarkDirtyRepaint();
        }

        private void FinishDoorStroke(Vector2 localPoint)
        {
            ExtendDoorStroke(localPoint);
            BoardSide side = m_doorStrokeSide;
            int[] segments = m_doorStroke.ToArray();
            // Releasing the capture raises capture-out, whose CancelDrag must find the stroke already finished.
            CancelDrag();
            SetHover(HitTest(localPoint));
            DoorStrokeRequested?.Invoke(side, segments);
        }

        // An obstacle stroke from the pressed board cell: the Barrier brush paints obstacles, the Eraser and a right drag erase
        // them. The window applies it on release.
        private void StartObstacleStroke(Vector2Int cell, bool paint, int pointerId)
        {
            m_obstacleStroke.Clear();
            m_obstacleStroke.Add(cell);
            m_obstacleStrokePaints = paint;
            m_obstacleStrokeEnd = cell;
            StartGesture(CanvasGesture.ObstacleStroke, pointerId);
            m_overlay.MarkDirtyRepaint();
        }

        // Walks from the last reached cell to the one under the pointer one side-sharing step at a time
        // (LevelCanvasWalk), so a fast drag leaves no gap and the stroke stays four-connected; each board cell joins
        // it once, and cells off the board are crossed but not added.
        private void ExtendObstacleStroke(Vector2 localPoint)
        {
            Vector2Int target = m_geometry.GetCellUnclamped(localPoint);
            m_strokeWalk.Clear();
            LevelCanvasWalk.AddCrossedCells(m_obstacleStrokeEnd, target, m_strokeWalk);
            bool added = false;
            for (int index = 0; index < m_strokeWalk.Count; index++)
            {
                Vector2Int cell = m_strokeWalk[index];
                if (m_model.IsOnBoard(cell) && !m_obstacleStroke.Contains(cell))
                {
                    m_obstacleStroke.Add(cell);
                    added = true;
                }
            }

            m_obstacleStrokeEnd = target;
            if (added)
            {
                m_overlay.MarkDirtyRepaint();
            }
        }

        private void FinishObstacleStroke(Vector2 localPoint)
        {
            ExtendObstacleStroke(localPoint);
            bool paint = m_obstacleStrokePaints;
            Vector2Int[] cells = m_obstacleStroke.ToArray();
            // Releasing the capture raises capture-out, whose CancelDrag must find the stroke already finished.
            CancelDrag();
            SetHover(HitTest(localPoint));
            ObstacleStrokeRequested?.Invoke(cells, paint);
        }

        // A cell a freehand stroke may take: free for a block, or already the growing piece's, and not under any board
        // object. Stricter than the place rule (IsFree), which the Barrier task owns.
        private bool IsFreehandCell(Vector2Int cell)
        {
            return m_model.IsFree(cell, GetFreehandBlockIndex()) && !m_model.IsBoardObject(cell);
        }

        private bool IsFreehandPen()
        {
            return m_tool == LevelEditorTool.Pen && m_freehand && m_obstacleBrush == LevelObstacleBrush.None;
        }

        // The growing piece's block in the current model, or -1.
        private int GetFreehandBlockIndex()
        {
            for (int index = 0; m_freehandBlockId != null && index < m_model.BlockCount; index++)
            {
                if (string.Equals(m_model.GetBlock(index).Id, m_freehandBlockId, StringComparison.Ordinal))
                {
                    return index;
                }
            }

            return -1;
        }

        /// <summary>
        /// True when a freehand click on this cell would add it to the piece, the green ghost: a new free cell that
        /// keeps the piece inside the learning limits and, once the piece has a block, touches that block edge to
        /// edge. Otherwise the ghost is red. The window still decides on release.
        /// </summary>
        internal bool CanGrowPieceAt(Vector2Int cell)
        {
            if (!m_model.IsFree(cell, -1) || m_model.IsBoardObject(cell))
            {
                return false;
            }

            int blockIndex = GetFreehandBlockIndex();
            if (blockIndex < 0)
            {
                return true;
            }

            LevelCanvasBlock block = m_model.GetBlock(blockIndex);
            if (block.CellCount >= LevelCustomPiece.k_maxCells)
            {
                return false;
            }

            bool touches = false;
            Vector2Int min = cell;
            Vector2Int max = cell;
            for (int index = 0; index < block.CellCount; index++)
            {
                Vector2Int part = block.GetCell(index);
                touches |= Mathf.Abs(part.x - cell.x) + Mathf.Abs(part.y - cell.y) == 1;
                min = Vector2Int.Min(min, part);
                max = Vector2Int.Max(max, part);
            }

            return touches && max.x - min.x < LevelCustomPiece.k_maxExtent && max.y - min.y < LevelCustomPiece.k_maxExtent;
        }

        /// <summary>
        /// Clears cells and fills it with every cell a freehand click would add to the growing block
        /// (<see cref="CanGrowPieceAt"/>), scanning the block's bounding box grown by one cell, row by row from the
        /// bottom. Empty unless the Pen draws freehand without the brush, no piece or gate stroke is in progress and the
        /// growing block exists. Allocates nothing beyond the list's own growth.
        /// </summary>
        internal void CollectGrowCells(List<Vector2Int> cells)
        {
            cells.Clear();
            int blockIndex = GetFreehandBlockIndex();
            if (!IsFreehandPen() || IsPieceStrokeActive || IsPaintingGate || blockIndex < 0)
            {
                return;
            }

            LevelCanvasBlock block = m_model.GetBlock(blockIndex);
            if (block.CellCount == 0)
            {
                return;
            }

            Vector2Int min = block.GetCell(0);
            Vector2Int max = min;
            for (int index = 1; index < block.CellCount; index++)
            {
                min = Vector2Int.Min(min, block.GetCell(index));
                max = Vector2Int.Max(max, block.GetCell(index));
            }

            for (int y = min.y - 1; y <= max.y + 1; y++)
            {
                for (int x = min.x - 1; x <= max.x + 1; x++)
                {
                    Vector2Int cell = new Vector2Int(x, y);
                    if (CanGrowPieceAt(cell))
                    {
                        cells.Add(cell);
                    }
                }
            }
        }

        // A freehand stroke from the pressed cell; the window adds its cells to the piece on release.
        private void StartPieceStroke(Vector2Int cell, int pointerId)
        {
            m_pieceStroke.Clear();
            m_pieceStroke.Add(cell);
            m_pieceStrokeEnd = cell;
            StartGesture(CanvasGesture.PieceStroke, pointerId);
            m_overlay.MarkDirtyRepaint();
        }

        // Walks cell by cell to the cell under the pointer (LevelCanvasWalk), so a fast drag leaves no gap. Each free
        // cell joins the stroke once; a block, board object, void or off-board cell is crossed but skipped, so a stroke
        // across one splits and is refused on release.
        private void ExtendPieceStroke(Vector2 localPoint)
        {
            Vector2Int target = m_geometry.GetCellUnclamped(localPoint);
            if (target == m_pieceStrokeEnd)
            {
                return;
            }

            m_strokeWalk.Clear();
            LevelCanvasWalk.AddCrossedCells(m_pieceStrokeEnd, target, m_strokeWalk);
            bool added = false;
            for (int index = 0; index < m_strokeWalk.Count; index++)
            {
                Vector2Int cell = m_strokeWalk[index];
                if (IsFreehandCell(cell) && !m_pieceStroke.Contains(cell))
                {
                    m_pieceStroke.Add(cell);
                    added = true;
                }
            }

            m_pieceStrokeEnd = target;
            if (added)
            {
                m_overlay.MarkDirtyRepaint();
            }
        }

        private void FinishPieceStroke(Vector2 localPoint)
        {
            ExtendPieceStroke(localPoint);
            Vector2Int[] cells = m_pieceStroke.ToArray();
            // Releasing the capture raises capture-out, whose CancelDrag must find the stroke already finished.
            CancelDrag();
            SetHover(HitTest(localPoint));
            PieceStrokeRequested?.Invoke(cells);
        }

        // A point in the gate band beside two edges at once: a ring corner, which belongs to no side.
        private bool IsRingCorner(Vector2 point)
        {
            if (!m_geometry.IsValid)
            {
                return false;
            }

            Rect board = m_geometry.BoardRect;
            float depth = LevelCanvasGeometry.k_doorDepth * m_geometry.CellSize;
            bool besideX = (point.x < board.xMin && point.x >= board.xMin - depth)
                           || (point.x >= board.xMax && point.x < board.xMax + depth);
            bool besideY = (point.y < board.yMin && point.y >= board.yMin - depth)
                           || (point.y >= board.yMax && point.y < board.yMax + depth);
            return besideX && besideY;
        }

        // The segment of a side under a point projected onto that side, clamped to the side's length.
        private int GetSegmentAlong(BoardSide side, Vector2 localPoint)
        {
            Rect board = m_geometry.BoardRect;
            float cell = m_geometry.CellSize;
            int length = m_model.GetSideLength(side);
            int segment = side == BoardSide.Top || side == BoardSide.Bottom
                ? Mathf.FloorToInt((localPoint.x - board.xMin) / cell)
                : m_geometry.Height - 1 - Mathf.FloorToInt((localPoint.y - board.yMin) / cell);
            return Mathf.Clamp(segment, 0, Mathf.Max(0, length - 1));
        }

        // A model, wall, palette or geometry change: the board and the overlay both redraw.
        private void RepaintAll()
        {
            MarkDirtyRepaint();
            m_overlay.MarkDirtyRepaint();
        }

        private void SetHover(LevelCanvasHit hit)
        {
            if (m_hover.Update(hit))
            {
                m_overlay.MarkDirtyRepaint();
            }
        }

        // Shows the badge at the pointer plus its offset; the canvas itself does not repaint.
        private void MoveToolBadge(Vector2 localPoint)
        {
            if (!m_geometry.IsValid || m_tool == LevelEditorTool.Select)
            {
                m_toolBadge.Hide();
                return;
            }

            m_toolBadge.Move(localPoint);
        }

        // The active tool's icon; the Pen takes the selected colour, or the Wall colour while it holds the Barrier
        // brush, and the other tools the light tint. The Select tool hides the badge until the pointer moves with
        // another tool.
        private void UpdateToolBadgeLook()
        {
            if (m_tool == LevelEditorTool.Select)
            {
                m_toolBadge.Hide();
            }
            else if (m_pointerInside)
            {
                MoveToolBadge(m_pointerPosition);
            }

            PaletteColor penColor = m_obstacleBrush != LevelObstacleBrush.None ? PaletteColor.Wall : m_toolColor;
            m_toolBadge.SetLook(m_tool, m_painter.GetPaletteColor(penColor));
        }

        private void UpdateGeometry()
        {
            LevelCanvasGeometry geometry = LevelCanvasGeometry.Create(contentRect.size, m_model.Width, m_model.Height,
                m_zoom, m_pan);
            if (geometry.Width == m_geometry.Width && geometry.Height == m_geometry.Height
                && geometry.CellSize == m_geometry.CellSize && geometry.Origin == m_geometry.Origin)
            {
                return;
            }

            m_geometry = geometry;
            if (!m_geometry.IsValid)
            {
                m_toolBadge.Hide();
            }

            m_edgeControls.Position(m_geometry);
            PositionLabels();
            RepaintAll();
        }

        // One Label per block, reused across models, with the block id in capitals as its text. The level data keeps
        // the id as authored; only the canvas shows it upper case. A block that shares a cell with another one, which
        // only a re-stamp leaves (contract D3-B), shows its id struck through with the rich-text <s> tag; every new
        // model sets the text again, so the mark goes as soon as the blocks are apart.
        private void RefreshLabels()
        {
            for (int index = 0; index < m_effectLayers.Length; index++) m_effectLayers[index].SetModel(m_model, this);
            m_effectBadges.SetModel(m_model, this);
            while (m_labels.Count < m_model.BlockCount)
            {
                Label label = new Label { pickingMode = PickingMode.Ignore, enableRichText = true };
                label.AddToClassList(k_labelClass);
                label.style.position = Position.Absolute;
                label.style.unityTextAlign = TextAnchor.MiddleCenter;
                label.style.unityFontStyleAndWeight = FontStyle.Bold;
                label.style.unityTextOutlineWidth = k_labelOutlineWidth;
                label.style.unityTextOutlineColor = s_labelOutlineColor;
                label.style.marginLeft = 0;
                label.style.marginTop = 0;
                label.style.paddingLeft = 0;
                label.style.paddingRight = 0;
                label.style.paddingTop = 0;
                label.style.paddingBottom = 0;
                m_labels.Add(label);
                Add(label);
            }

            while (m_labels.Count > m_model.BlockCount)
            {
                m_labels[m_labels.Count - 1].RemoveFromHierarchy();
                m_labels.RemoveAt(m_labels.Count - 1);
            }

            for (int index = 0; index < m_labels.Count; index++)
            {
                LevelCanvasBlock block = m_model.GetBlock(index);
                string id = block.Id != null ? block.Id.ToUpperInvariant() : string.Empty;
                m_labels[index].text = m_model.IsOverlapping(index) ? "<s>" + id + "</s>" : id;
                m_labels[index].style.color = Color.black;
            }

            // Labels added after the badge would draw over it.
            m_toolBadge.Element.BringToFront();
            PositionLabels();
        }

        private void PositionLabels()
        {
            for (int index = 0; index < m_effectLayers.Length; index++) m_effectLayers[index].Position(m_geometry);
            float cellSize = m_geometry.CellSize;
            float fontSize = Mathf.Clamp(cellSize * 0.4f, 11f, 20f);
            for (int index = 0; index < m_labels.Count; index++)
            {
                Label label = m_labels[index];
                LevelCanvasBlock block = m_model.GetBlock(index);
                bool visible = m_geometry.IsValid && block.CellCount > 0;
                label.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
                if (!visible)
                {
                    m_effectBadges.Hide(index);
                    continue;
                }

                // Two cells wide around the centre, so an id centred on a cell edge stays on one line.
                Vector2 centre = GetLabelCentre(block);
                label.style.left = centre.x - cellSize;
                label.style.top = centre.y - cellSize * 0.5f;
                label.style.width = cellSize * 2f;
                label.style.height = cellSize;
                label.style.fontSize = fontSize;
                m_effectBadges.Position(index, block, m_geometry, centre, fontSize, m_effectLayers);
            }
        }

        // The footprint's centre (the mean of its cell centres) when the text around it lies on the block, as for
        // rectangles and T pieces; a 2 x 2 block's centre is the corner its four cells share. Otherwise, in a notch (a C
        // or an arch) or at the edge of a cell beside another block (a 7 shape, D-001), the label keeps the block cell
        // nearest the centre.
        private Vector2 GetLabelCentre(LevelCanvasBlock block)
        {
            Vector2 sum = Vector2.zero;
            for (int index = 0; index < block.CellCount; index++)
            {
                sum += m_geometry.GetCellRect(block.GetCell(index)).center;
            }

            Vector2 centre = sum / block.CellCount;
            // The id's own glyphs are about half a cell wide and tall around the centre, so each corner of that area
            // must lie on a block cell; a centre on a shared edge or corner then still counts as on the block.
            float reach = m_geometry.CellSize * 0.25f;
            bool onBlock = IsOnBlock(block, centre + new Vector2(-reach, -reach))
                           && IsOnBlock(block, centre + new Vector2(reach, -reach))
                           && IsOnBlock(block, centre + new Vector2(-reach, reach))
                           && IsOnBlock(block, centre + new Vector2(reach, reach));
            return onBlock ? centre : m_geometry.GetCellRect(block.LabelCell).center;
        }

        private bool IsOnBlock(LevelCanvasBlock block, Vector2 point)
        {
            for (int index = 0; index < block.CellCount; index++)
            {
                if (m_geometry.GetCellRect(block.GetCell(index)).Contains(point))
                {
                    return true;
                }
            }

            return false;
        }

        // The board layers read only the model, walls and geometry; the struct lives on the stack.
        private void OnGenerateVisualContent(MeshGenerationContext context)
        {
            if (!m_geometry.IsValid)
            {
                return;
            }

            LevelCanvasPainter.DrawState state = new LevelCanvasPainter.DrawState
            {
                Model = m_model,
                Geometry = m_geometry,
                Walls = m_walls,
                EffectUnderlays = m_effectUnderlays
            };
            m_painter.DrawBoard(context.painter2D, in state);
        }

        // Hands the painter this repaint's snapshot: the model, the window's overlays and the gesture in progress. The
        // grow cells fill the reused m_growCells, and the struct lives on the stack, so a repaint allocates nothing.
        private void OnGenerateOverlayContent(MeshGenerationContext context)
        {
            if (!m_geometry.IsValid)
            {
                return;
            }

            CollectGrowCells(m_growCells);
            bool freehandPen = IsFreehandPen();
            LevelCanvasHit hover = m_hover.Current;
            LevelCanvasPainter.DrawState state = new LevelCanvasPainter.DrawState
            {
                Model = m_model,
                Geometry = m_geometry,
                Walls = m_walls,
                EffectUnderlays = m_effectUnderlays,
                EffectOverlays = m_effectOverlays,
                HatchCells = m_hatchCells,
                HatchSegments = m_hatchSegments,
                Highlight = m_highlight,
                Selection = m_selection,
                Hover = hover,
                Tool = m_tool,
                ToolColor = m_toolColor,
                ObstacleBrush = m_obstacleBrush,
                PlacesArmedPiece = IsPlacingOn(hover),
                ArmedCells = m_armedCells,
                DragBlock = m_dragBlock,
                DragOffset = m_dragOffset,
                GateStrokeActive = IsPaintingGate,
                GateStrokeSide = m_doorStrokeSide,
                GateStroke = m_doorStroke,
                ObstacleStrokeActive = IsObstacleStrokeActive,
                ObstacleStrokePaints = m_obstacleStrokePaints,
                ObstacleStroke = m_obstacleStroke,
                PieceStrokeActive = IsPieceStrokeActive,
                PieceStroke = m_pieceStroke,
                FreehandPen = freehandPen,
                EditedBlock = freehandPen ? GetFreehandBlockIndex() : -1,
                // A click on the hovered cell would grow the freehand piece: the freehand ghost's colour.
                HoverCanGrow = freehandPen && hover.Kind == LevelCanvasHitKind.Cell && CanGrowPieceAt(hover.Cell),
                GrowCells = m_growCells
            };
            m_painter.DrawOverlay(context.painter2D, in state);
        }
    }
}
