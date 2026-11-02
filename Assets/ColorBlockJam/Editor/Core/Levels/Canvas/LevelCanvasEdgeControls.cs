using System;
using ColorBlockJam.Runtime.Levels;
using UnityEngine;
using UnityEngine.UIElements;

namespace ColorBlockJam.Editor.Levels
{
    /// <summary>
    /// The Level Editor canvas's resize frame: it creates per side an add and a remove bar and per corner a cut and a
    /// fill button once on the canvas, places them around the board and enables them from the model, reporting each
    /// press through the callbacks the canvas passes in.
    /// </summary>
    internal sealed class LevelCanvasEdgeControls
    {
        // Frame sizes: LevelCanvasGeometry reserves its band from them.
        private const float k_edgeBarOffset = LevelCanvasGeometry.k_edgeBarOffset;
        private const float k_edgeBarThickness = LevelCanvasGeometry.k_edgeBarThickness;
        private const float k_edgeBarGap = LevelCanvasGeometry.k_edgeBarGap;
        private const float k_disabledIconAlpha = 0.3f;
        private static readonly BoardSide[] s_sides = { BoardSide.Top, BoardSide.Bottom, BoardSide.Left, BoardSide.Right };
        // Corners by screen direction: x -1 left / +1 right, y -1 top / +1 bottom.
        private static readonly Vector2Int[] s_corners =
        {
            new Vector2Int(-1, -1), new Vector2Int(1, -1), new Vector2Int(-1, 1), new Vector2Int(1, 1)
        };

        // Per-side add and remove bars in the edge band, indexed by side then [add, remove].
        private readonly Button[] m_edgeButtons = new Button[8];
        // Per-corner cut and fill buttons, indexed by corner then [cut, fill]; they need level JSON v2 void cells.
        private readonly Button[] m_cornerButtons = new Button[8];

        /// <summary>
        /// Creates the controls as children of the canvas: per side an outer add bar and an inner remove bar, per
        /// corner a cut and a fill button (reference layout). resize gets the side and +1 to add a line or -1 to
        /// remove one; corner gets the corner index (see <see cref="GetCornerCell"/>) and true to cut or false to fill.
        /// </summary>
        public LevelCanvasEdgeControls(VisualElement canvas, Action<BoardSide, int> resize, Action<int, bool> corner)
        {
            for (int sideIndex = 0; sideIndex < s_sides.Length; sideIndex++)
            {
                BoardSide side = s_sides[sideIndex];
                m_edgeButtons[2 * sideIndex] = CreateEdgeButton(canvas, side, 1, resize);
                m_edgeButtons[2 * sideIndex + 1] = CreateEdgeButton(canvas, side, -1, resize);
            }

            for (int cornerIndex = 0; cornerIndex < s_corners.Length; cornerIndex++)
            {
                m_cornerButtons[2 * cornerIndex] = CreateCornerButton(canvas, cornerIndex, true, corner);
                m_cornerButtons[2 * cornerIndex + 1] = CreateCornerButton(canvas, cornerIndex, false, corner);
            }
        }

        // The board corner cell a corner button acts on. s_corners counts x -1 as left and y -1 as top (screen); the
        // board counts y up from the bottom row.
        internal static Vector2Int GetCornerCell(LevelCanvasModel model, int cornerIndex)
        {
            return model.GetCornerCell(s_corners[cornerIndex].x > 0, s_corners[cornerIndex].y < 0);
        }

        internal Button GetEdgeButton(BoardSide side, bool add)
        {
            for (int sideIndex = 0; sideIndex < s_sides.Length; sideIndex++)
            {
                if (s_sides[sideIndex] == side)
                {
                    return m_edgeButtons[2 * sideIndex + (add ? 0 : 1)];
                }
            }

            return null;
        }

        internal static string GetEdgeButtonName(BoardSide side, bool add)
        {
            return "edge-" + side.ToString().ToLowerInvariant() + (add ? "-add" : "-remove");
        }

        internal static string GetCornerButtonName(int cornerIndex, bool cut)
        {
            Vector2Int corner = s_corners[cornerIndex];
            return "corner-" + (corner.y < 0 ? "top" : "bottom") + "-" + (corner.x < 0 ? "left" : "right") +
                   (cut ? "-cut" : "-fill");
        }

        // The add bar's arrow points out of the board and the remove bar's into it.
        private static string GetArrowIconName(BoardSide side, bool outward)
        {
            switch (side)
            {
                case BoardSide.Top:
                    return outward ? LevelEditorIcons.k_arrowUp : LevelEditorIcons.k_arrowDown;
                case BoardSide.Bottom:
                    return outward ? LevelEditorIcons.k_arrowDown : LevelEditorIcons.k_arrowUp;
                case BoardSide.Left:
                    return outward ? LevelEditorIcons.k_arrowLeft : LevelEditorIcons.k_arrowRight;
                default:
                    return outward ? LevelEditorIcons.k_arrowRight : LevelEditorIcons.k_arrowLeft;
            }
        }

        // Like the arrows: the outer fill button shows its own corner's bracket, pointing out of the board, and the
        // inner cut button the opposite bracket, pointing into it.
        private static string GetCornerIconName(Vector2Int corner, bool outward)
        {
            bool top = (corner.y < 0) == outward;
            bool left = (corner.x < 0) == outward;
            return top
                ? left ? LevelEditorIcons.k_cornerTopLeft : LevelEditorIcons.k_cornerTopRight
                : left ? LevelEditorIcons.k_cornerBottomLeft : LevelEditorIcons.k_cornerBottomRight;
        }

        // One frame around the board, as in the reference: two bars per side outside the gate bars, each running past
        // the board over the wall ring up to a gap before the corner block, and per corner a 2 x 2 block whose outer
        // cell holds the fill button and whose inner cell holds the cut button. An invalid geometry hides them all.
        public void Position(LevelCanvasGeometry geometry)
        {
            bool visible = geometry.IsValid;
            Rect board = geometry.BoardRect;
            float inner = LevelCanvasGeometry.k_doorDepth * geometry.CellSize + k_edgeBarOffset;
            float outer = inner + k_edgeBarThickness + k_edgeBarGap;
            // The bars reach the inner bar lines of the neighbouring sides, less one gap.
            float reach = inner - k_edgeBarGap;
            for (int sideIndex = 0; sideIndex < s_sides.Length; sideIndex++)
            {
                BoardSide side = s_sides[sideIndex];
                PlaceBar(m_edgeButtons[2 * sideIndex], side, board, outer, reach, visible);
                PlaceBar(m_edgeButtons[2 * sideIndex + 1], side, board, inner, reach, visible);
            }

            for (int cornerIndex = 0; cornerIndex < s_corners.Length; cornerIndex++)
            {
                Vector2Int corner = s_corners[cornerIndex];
                Vector2 boardCorner = new Vector2(corner.x < 0 ? board.xMin : board.xMax, corner.y < 0 ? board.yMin : board.yMax);
                PlaceCorner(m_cornerButtons[2 * cornerIndex], boardCorner, corner, inner, visible);
                PlaceCorner(m_cornerButtons[2 * cornerIndex + 1], boardCorner, corner, outer, visible);
            }
        }

        // Add stops at the largest board and remove at the smallest, per axis.
        public void Refresh(LevelCanvasModel model)
        {
            for (int sideIndex = 0; sideIndex < s_sides.Length; sideIndex++)
            {
                bool horizontal = s_sides[sideIndex] == BoardSide.Top || s_sides[sideIndex] == BoardSide.Bottom;
                // Top and Bottom change the height; Left and Right change the width.
                int size = horizontal ? model.Height : model.Width;
                int max = horizontal ? LevelBoardResize.k_maxHeight : LevelBoardResize.k_maxWidth;
                SetEdgeEnabled(m_edgeButtons[2 * sideIndex], size > 0 && size < max);
                SetEdgeEnabled(m_edgeButtons[2 * sideIndex + 1], size > LevelBoardResize.k_minSize);
            }

            for (int cornerIndex = 0; cornerIndex < s_corners.Length; cornerIndex++)
            {
                bool right = s_corners[cornerIndex].x > 0;
                bool top = s_corners[cornerIndex].y < 0;
                // Each press cuts or fills one square step of the corner's notch (task 224a7aef, DEC-190).
                string cutIssue = model.GetCornerCutIssue(right, top);
                Button cut = m_cornerButtons[2 * cornerIndex];
                SetEdgeEnabled(cut, cutIssue == null);
                cut.tooltip = cutIssue == null
                    ? $"Cut the next corner step ({LevelBoardResize.FormatCount(model.GetCornerCutCells(right, top), "cell")})."
                    : "Cannot cut: " + cutIssue;
                bool isVoid = model.IsVoid(model.GetCornerCell(right, top));
                Button fill = m_cornerButtons[2 * cornerIndex + 1];
                SetEdgeEnabled(fill, isVoid);
                fill.tooltip = isVoid
                    ? $"Fill the last corner step ({LevelBoardResize.FormatCount(model.GetCornerFillCells(right, top), "cell")}) back with floor."
                    : "This corner cell is floor.";
            }
        }

        private static Button CreateEdgeButton(VisualElement canvas, BoardSide side, int delta, Action<BoardSide, int> resize)
        {
            // Owner: the canvas; stop point: the bar is dropped with the canvas.
            Button button = new Button(() => resize(side, delta))
            {
                name = GetEdgeButtonName(side, delta > 0),
                tooltip = GetEdgeTooltip(side, delta > 0)
            };
            StyleEdgeControl(button);
            SetEdgeIcon(button, GetArrowIconName(side, delta > 0));
            canvas.Add(button);
            return button;
        }

        // The inner button cuts the board's corner cell into a void (level JSON v2), the outer one fills it back;
        // Refresh enables each from the model.
        private static Button CreateCornerButton(VisualElement canvas, int cornerIndex, bool cut, Action<int, bool> corner)
        {
            Button button = new Button(() => corner(cornerIndex, cut))
            {
                name = GetCornerButtonName(cornerIndex, cut)
            };
            StyleEdgeControl(button);
            button.style.width = k_edgeBarThickness;
            button.style.height = k_edgeBarThickness;
            SetEdgeIcon(button, GetCornerIconName(s_corners[cornerIndex], !cut));
            canvas.Add(button);
            return button;
        }

        private static void StyleEdgeControl(Button button)
        {
            // Qualified: Position is also this class's placing method.
            button.style.position = UnityEngine.UIElements.Position.Absolute;
            button.style.marginLeft = 0;
            button.style.marginRight = 0;
            button.style.marginTop = 0;
            button.style.marginBottom = 0;
            button.style.paddingLeft = 0;
            button.style.paddingRight = 0;
            button.style.paddingTop = 0;
            button.style.paddingBottom = 0;
            button.style.unityTextAlign = TextAnchor.MiddleCenter;
            button.style.alignItems = Align.Center;
            button.style.justifyContent = Justify.Center;
            button.style.display = DisplayStyle.None;
        }

        // The white icon in the skin's neutral tint, centred at the control's thickness; a missing file shows a letter.
        private static void SetEdgeIcon(Button button, string iconName)
        {
            Texture2D icon = LevelEditorIcons.Load(iconName);
            if (icon == null)
            {
                button.text = iconName.Substring(iconName.IndexOf('-') + 1, 1).ToUpperInvariant();
                return;
            }

            Image image = new Image
            {
                name = "edge-icon",
                image = icon,
                tintColor = LevelEditorIcons.NeutralTint,
                scaleMode = ScaleMode.ScaleToFit,
                pickingMode = PickingMode.Ignore
            };
            image.style.width = k_edgeBarThickness;
            image.style.height = k_edgeBarThickness;
            image.style.flexShrink = 0;
            button.Add(image);
        }

        // The disabled style alone barely dims a white icon, so a disabled control also fades its icon.
        private static void SetEdgeEnabled(Button button, bool enabled)
        {
            button.SetEnabled(enabled);
            Image icon = button.Q<Image>("edge-icon");
            if (icon != null)
            {
                Color tint = LevelEditorIcons.NeutralTint;
                icon.tintColor = enabled ? tint : new Color(tint.r, tint.g, tint.b, k_disabledIconAlpha);
            }
        }

        private static string GetEdgeTooltip(BoardSide side, bool add)
        {
            switch (side)
            {
                case BoardSide.Top:
                    return add ? "Add a row at the top." : "Remove the top row.";
                case BoardSide.Bottom:
                    return add ? "Add a row at the bottom; the level moves up with it." : "Remove the bottom row.";
                case BoardSide.Left:
                    return add ? "Add a column on the left; the level moves right with it." : "Remove the left column.";
                default:
                    return add ? "Add a column on the right." : "Remove the right column.";
            }
        }

        // One bar along a side: its near edge distance pixels outside the board, running reach pixels past both ends.
        private static void PlaceBar(Button bar, BoardSide side, Rect board, float distance, float reach, bool visible)
        {
            bar.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
            if (!visible)
            {
                return;
            }

            bool horizontal = side == BoardSide.Top || side == BoardSide.Bottom;
            bar.style.width = horizontal ? board.width + 2f * reach : k_edgeBarThickness;
            bar.style.height = horizontal ? k_edgeBarThickness : board.height + 2f * reach;
            bar.style.left = side == BoardSide.Left ? board.xMin - distance - k_edgeBarThickness
                : side == BoardSide.Right ? board.xMax + distance
                : board.xMin - reach;
            bar.style.top = side == BoardSide.Top ? board.yMin - distance - k_edgeBarThickness
                : side == BoardSide.Bottom ? board.yMax + distance
                : board.yMin - reach;
        }

        // A corner button in the cell where the row and the column of the bars at this distance cross.
        private static void PlaceCorner(Button button, Vector2 boardCorner, Vector2Int corner, float distance, bool visible)
        {
            button.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
            float offset = distance + 0.5f * k_edgeBarThickness;
            button.style.left = boardCorner.x + corner.x * offset - 0.5f * k_edgeBarThickness;
            button.style.top = boardCorner.y + corner.y * offset - 0.5f * k_edgeBarThickness;
        }
    }
}
