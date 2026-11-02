using System;
using System.Collections.Generic;
using ColorBlockJam.Editor.Controls;
using ColorBlockJam.Runtime.Board;
using UnityEngine;
using UnityEngine.UIElements;

namespace ColorBlockJam.Editor.Levels
{
    /// <summary>
    /// The Blocks group's piece list: one tile per piece-library template with a centred thumbnail of its cells and its
    /// display name, plus a red Delete badge that asks to delete the template from the piece library. A click on a tile
    /// raises <see cref="ShapePicked"/>; the armed piece's tile is outlined white, the piece of the block the Pen edits
    /// green (without arming it) and a hovered tile light grey, in that priority, all found by ShapeKey (DEC-193). A
    /// catalog that failed to load shows its message and no tiles. A "Level pieces" section after the templates lists
    /// the level pieces under their level names, each with its own Delete badge that asks to delete the piece and its
    /// blocks from the open level (user request of 2026-09-26), and a last "+" New piece tile asks for freehand drawing
    /// and is outlined while it is armed (docs/Level-Editor-Design.md, "+" level pieces). Keys show only in tooltips.
    /// </summary>
    internal sealed class LevelShapePalette : VisualElement
    {
        internal const string k_name = "shape-palette";
        internal const string k_tileClass = "level-shape-tile";
        private const string k_deleteClass = "level-shape-tile__delete";
        internal const string k_messageName = "shape-palette-message";
        internal const string k_customHeaderName = "custom-pieces-header";
        internal const string k_customHeaderText = "Level pieces";
        internal const string k_customTileClass = "level-custom-tile";
        internal const string k_createTileName = "create-level-piece";
        internal const float k_gridSize = 40f;
        private const float k_tileWidth = 58f;
        private const float k_deleteSize = 13f;
        // The Delete badge hangs over the tile's top-right corner, clear of the centred 40 px grid (tile x 9 to 49).
        private const float k_deleteOverhang = 4f;
        internal static readonly Color s_armedBorder = Color.white;
        // Between idle and armed, so a hovered tile reads as clickable without looking armed.
        internal static readonly Color s_hoverBorder = new Color(0.7f, 0.7f, 0.7f);
        internal static readonly Color s_idleBorder = new Color(0f, 0f, 0f, 0.35f);
        // The piece of the block the Pen edits: green like the canvas edit outline, and wider than the other frames.
        internal static readonly Color s_editBorder = new Color(0.25f, 0.9f, 0.35f, 1f);
        internal const float k_editBorderWidth = 2f;
        private const float k_borderWidth = 1f;
        private static readonly Color s_filledCell = new Color(0.85f, 0.87f, 0.9f);

        private readonly List<VisualElement> m_tiles = new List<VisualElement>();
        private readonly List<Button> m_deleteButtons = new List<Button>();
        private readonly List<LevelShapeChoice> m_choices = new List<LevelShapeChoice>();
        // The Level pieces section and the "+" tile sit after the template slots and are kept out of m_tiles and
        // m_choices, so template tile indices stay the piece library's indices.
        private readonly List<VisualElement> m_customTiles = new List<VisualElement>();
        private readonly List<Button> m_customDeleteButtons = new List<Button>();
        private readonly List<LevelShapeChoice> m_customChoices = new List<LevelShapeChoice>();
        private readonly VisualElement m_tileRow;
        private readonly Label m_message;
        private ShapeKey m_armedKey;
        // The key of the block the Pen edits: a template's or a level piece's; the default key when none.
        private ShapeKey m_editingKey;
        private VisualElement m_hoveredTile;
        private VisualElement m_createTile;
        private bool m_freehandArmed;
        private int m_libraryChildCount;
        private int m_libraryExtent = 1;
        private bool m_catalogFailed = true;

        public LevelShapePalette()
        {
            name = k_name;
            m_message = new Label { name = k_messageName };
            m_message.style.whiteSpace = WhiteSpace.Normal;
            m_message.style.color = new Color(0.95f, 0.55f, 0.4f);
            m_message.style.display = DisplayStyle.None;
            Add(m_message);

            m_tileRow = new VisualElement { name = "shape-tiles" };
            m_tileRow.style.flexDirection = FlexDirection.Row;
            m_tileRow.style.flexWrap = Wrap.Wrap;
            Add(m_tileRow);
        }

        /// <summary>Raised when a template tile is clicked. Subscriber: the Level Editor window.</summary>
        public event Action<LevelShapeChoice> ShapePicked;
        /// <summary>Raised when a template tile's × is clicked; the window confirms and deletes. Subscriber: the window.</summary>
        public event Action<LevelShapeChoice> ShapeDeleteRequested;
        /// <summary>Raised when a Level pieces tile is clicked. Subscriber: the Level Editor window.</summary>
        public event Action<LevelShapeChoice> CustomPiecePicked;
        /// <summary>
        /// Raised when a Level pieces tile's × is clicked; the window confirms and deletes the piece and its blocks from
        /// the open level. Subscriber: the Level Editor window.
        /// </summary>
        public event Action<LevelShapeChoice> CustomPieceDeleteRequested;
        /// <summary>
        /// Raised when the "+" New piece tile is clicked to draw a new piece freehand. Subscriber: the Level Editor
        /// window.
        /// </summary>
        public event Action PieceCreateRequested;

        internal int TileCount => m_tiles.Count;
        internal int CustomTileCount => m_customTiles.Count;
        /// <summary>The "+" New piece tile, or null while the catalog failed to load.</summary>
        internal VisualElement PlusTile => m_createTile;

        /// <summary>Outlines the "+" New piece tile while the Pen draws freehand.</summary>
        public void SetFreehandArmed(bool armed)
        {
            m_freehandArmed = armed;
            RefreshBorders();
        }

        internal VisualElement GetTile(int index)
        {
            return m_tiles[index];
        }

        /// <summary>The piece a template tile shows, with the footprint its thumbnail draws.</summary>
        internal LevelShapeChoice GetChoice(int index)
        {
            return m_choices[index];
        }

        internal Button GetDeleteButton(int index)
        {
            return m_deleteButtons[index];
        }

        internal VisualElement GetCustomTile(int index)
        {
            return m_customTiles[index];
        }

        internal LevelShapeChoice GetCustomChoice(int index)
        {
            return m_customChoices[index];
        }

        internal Button GetCustomDeleteButton(int index)
        {
            return m_customDeleteButtons[index];
        }

        /// <summary>
        /// Lists these level pieces in the Level pieces section, in order; an empty list hides the section. The "+"
        /// tile stays last either way.
        /// </summary>
        public void SetCustomPieces(IReadOnlyList<LevelShapeChoice> pieces)
        {
            m_customChoices.Clear();
            for (int index = 0; pieces != null && index < pieces.Count; index++)
            {
                if (pieces[index] != null && pieces[index].Cells.Count > 0)
                {
                    m_customChoices.Add(pieces[index]);
                }
            }

            RebuildCustomSection();
            RefreshBorders();
        }

        /// <summary>Rebuilds the tiles from a catalog; an unreadable catalog shows its message instead.</summary>
        public void SetCatalog(LevelShapeCatalog catalog)
        {
            m_tileRow.Clear();
            m_tiles.Clear();
            m_deleteButtons.Clear();
            m_choices.Clear();
            m_customTiles.Clear();
            m_createTile = null;
            m_hoveredTile = null;
            m_libraryChildCount = 0;
            bool failed = catalog == null || catalog.Error != null;
            m_catalogFailed = failed;
            m_message.text = failed ? catalog?.Error ?? LevelShapeCatalog.Missing.Error : string.Empty;
            m_message.style.display = failed ? DisplayStyle.Flex : DisplayStyle.None;
            if (failed)
            {
                return;
            }

            m_libraryExtent = LevelShapeThumbnail.GetExtent(catalog);
            float cellSize = LevelShapeThumbnail.GetCellSize(k_gridSize, m_libraryExtent);
            for (int index = 0; index < catalog.Count; index++)
            {
                LevelShapeChoice choice = catalog.Get(index);
                string key = choice.TemplateKey.ToString();
                VisualElement tile = CreateTile(choice, cellSize);
                Button delete = CreateDeleteButton("delete-shape-" + key,
                    $"Delete {choice.DisplayName} from the piece library", () => ShapeDeleteRequested?.Invoke(choice));
                // The × is the tile's sibling, so a × click never arms.
                VisualElement slot = CreateSlot("slot-" + key, tile);
                slot.Add(delete);
                m_choices.Add(choice);
                m_tiles.Add(tile);
                m_deleteButtons.Add(delete);
                m_tileRow.Add(slot);
            }

            m_libraryChildCount = m_tileRow.childCount;
            RebuildCustomSection();
            RefreshBorders();
        }

        // Replaces everything after the template slots: the Level pieces header and tiles, then the "+" tile. The
        // header takes a whole row, so the level piece tiles start on a new line; without level pieces "+" follows the
        // templates.
        private void RebuildCustomSection()
        {
            while (m_tileRow.childCount > m_libraryChildCount)
            {
                m_tileRow.RemoveAt(m_tileRow.childCount - 1);
            }

            m_customTiles.Clear();
            m_customDeleteButtons.Clear();
            if (m_hoveredTile != null && !m_tiles.Contains(m_hoveredTile))
            {
                m_hoveredTile = null;
            }

            m_createTile = null;
            if (m_catalogFailed)
            {
                return;
            }

            if (m_customChoices.Count > 0)
            {
                Label header = new Label(k_customHeaderText) { name = k_customHeaderName, pickingMode = PickingMode.Ignore };
                header.tooltip = "Pieces that belong to levels, not to the piece library.";
                header.style.flexBasis = Length.Percent(100);
                header.style.fontSize = 10;
                header.style.marginTop = 2;
                m_tileRow.Add(header);

                int extent = m_libraryExtent;
                for (int index = 0; index < m_customChoices.Count; index++)
                {
                    RectInt bounds = LevelShapeThumbnail.GetBounds(m_customChoices[index].Cells);
                    extent = Mathf.Max(extent, Mathf.Max(bounds.width, bounds.height));
                }

                // Never larger than the templates' cells, so a small level piece does not look bigger than a template.
                float cellSize = LevelShapeThumbnail.GetCellSize(k_gridSize, extent);
                for (int index = 0; index < m_customChoices.Count; index++)
                {
                    LevelShapeChoice choice = m_customChoices[index];
                    string key = choice.Key.ToString();
                    VisualElement tile = CreateCustomTile(choice, cellSize);
                    Button delete = CreateDeleteButton("delete-custom-" + key,
                        $"Delete {choice.DisplayName} and its blocks from this level",
                        () => CustomPieceDeleteRequested?.Invoke(choice));
                    VisualElement slot = CreateSlot("custom-slot-" + key, tile);
                    slot.Add(delete);
                    m_customTiles.Add(tile);
                    m_customDeleteButtons.Add(delete);
                    m_tileRow.Add(slot);
                }
            }

            m_createTile = CreatePieceTile();
            m_tileRow.Add(CreateSlot("create-slot", m_createTile));
        }

        private VisualElement CreateCustomTile(LevelShapeChoice choice, float cellSize)
        {
            VisualElement tile = new VisualElement
            {
                name = "custom-" + choice.Key,
                tooltip = $"{choice.DisplayName} (key {choice.Key}): a level piece. Click, then click free cells with " +
                          "Select to place it; while the Piece Editor is open it also shows there. R / Shift+R rotates, " +
                          "F / Shift+F mirrors the ghost."
            };
            tile.AddToClassList(k_customTileClass);
            StyleTile(tile);
            tile.Add(CreateThumbnail(choice.Cells, cellSize));
            tile.Add(CreateLabel(choice.DisplayName));

            // Owner: this tile; stop point: it is dropped by the next SetCustomPieces, SetCatalog or with the window GUI.
            tile.AddManipulator(new Clickable(() => CustomPiecePicked?.Invoke(choice)));
            AddHover(tile);
            return tile;
        }

        private VisualElement CreatePieceTile()
        {
            VisualElement tile = new VisualElement
            {
                name = k_createTileName,
                tooltip = "New piece: draw a block freehand with the Pen. Click or drag over free cells in the selected " +
                          "colour; the piece holds at most 25 connected cells inside 5 x 5. It belongs to this level, and " +
                          "the Piece Editor, where it shows too, can Save as template to add it to the piece library. Esc " +
                          "cancels the stroke."
            };
            StyleTile(tile);

            Label plus = new Label("+") { pickingMode = PickingMode.Ignore };
            plus.style.width = k_gridSize;
            plus.style.height = k_gridSize;
            plus.style.fontSize = 24;
            plus.style.unityFontStyleAndWeight = FontStyle.Bold;
            plus.style.unityTextAlign = TextAnchor.MiddleCenter;
            tile.Add(plus);
            tile.Add(CreateLabel("New piece"));

            // Owner: this tile; stop point: it is dropped by the next SetCustomPieces, SetCatalog or with the window GUI.
            tile.AddManipulator(new Clickable(() => PieceCreateRequested?.Invoke()));
            AddHover(tile);
            return tile;
        }

        // Every tile sits in a slot with room for the Delete badge, so the grid stays even; the "+" tile has no badge.
        private static VisualElement CreateSlot(string slotName, VisualElement tile)
        {
            VisualElement slot = new VisualElement { name = slotName };
            slot.style.marginRight = 3f + k_deleteOverhang;
            slot.style.marginTop = k_deleteOverhang;
            slot.style.marginBottom = 3;
            slot.Add(tile);
            return slot;
        }

        /// <summary>Outlines the tile of the armed piece; the default key clears the outline.</summary>
        public void SetArmed(ShapeKey key)
        {
            m_armedKey = key;
            RefreshBorders();
        }

        /// <summary>
        /// Outlines, in green, the template or level piece tile of the block the Pen edits, without arming it; the
        /// armed outline wins on the same tile. The default key clears it.
        /// </summary>
        public void SetEditing(ShapeKey key)
        {
            if (key == m_editingKey)
            {
                return;
            }

            m_editingKey = key;
            RefreshBorders();
        }

        // Pointer enter and leave land here; leave clears only its own tile, so an enter on the next tile that
        // arrives first is kept.
        private void SetHovered(VisualElement tile, bool hovered)
        {
            if (hovered)
            {
                m_hoveredTile = tile;
            }
            else if (m_hoveredTile == tile)
            {
                m_hoveredTile = null;
            }

            RefreshBorders();
        }

        // Every border is re-derived from the armed piece, the edited piece and the hovered tile, so no state can leave
        // a stale colour behind when another changes.
        private void RefreshBorders()
        {
            for (int index = 0; index < m_tiles.Count; index++)
            {
                SetTileBorder(m_tiles[index], m_choices[index].Key);
            }

            for (int index = 0; index < m_customTiles.Count; index++)
            {
                SetTileBorder(m_customTiles[index], m_customChoices[index].Key);
            }

            if (m_createTile != null)
            {
                SetBorder(m_createTile, m_freehandArmed ? s_armedBorder
                    : m_createTile == m_hoveredTile ? s_hoverBorder : s_idleBorder);
            }
        }

        // Armed wins over editing, editing over hovered, hovered over idle; only the editing frame is wider.
        private void SetTileBorder(VisualElement tile, ShapeKey key)
        {
            if (m_armedKey.IsValid && key == m_armedKey)
            {
                SetBorder(tile, s_armedBorder);
            }
            else if (m_editingKey.IsValid && key == m_editingKey)
            {
                SetBorder(tile, s_editBorder, k_editBorderWidth);
            }
            else
            {
                SetBorder(tile, tile == m_hoveredTile ? s_hoverBorder : s_idleBorder);
            }
        }

        private VisualElement CreateTile(LevelShapeChoice choice, float cellSize)
        {
            VisualElement tile = new VisualElement
            {
                name = "shape-" + choice.TemplateKey,
                tooltip = $"{choice.DisplayName} (key {choice.Key}): click, then click free cells with Select to place " +
                          "it. R / Shift+R rotates, F / Shift+F mirrors the ghost."
            };
            tile.AddToClassList(k_tileClass);
            StyleTile(tile);
            tile.Add(CreateThumbnail(choice.Cells, cellSize));
            tile.Add(CreateLabel(choice.DisplayName));

            // Owner: this tile; stop point: it is dropped by the next SetCatalog or with the window GUI.
            tile.AddManipulator(new Clickable(() => ShapePicked?.Invoke(choice)));
            // The Delete badge is a sibling, so it does not highlight the tile.
            AddHover(tile);
            return tile;
        }

        // The tile's centred thumbnail grid. Owner: this tile's grid; stop point: it is dropped with the tile.
        private static VisualElement CreateThumbnail(IReadOnlyList<Vector2Int> cells, float cellSize)
        {
            VisualElement grid = new VisualElement { pickingMode = PickingMode.Ignore };
            grid.style.width = k_gridSize;
            grid.style.height = k_gridSize;
            grid.generateVisualContent += context => DrawThumbnail(context, cells, cellSize);
            return grid;
        }

        // Owner: the tile; stop point: it is dropped by the next SetCustomPieces, SetCatalog or with the window GUI.
        private void AddHover(VisualElement tile)
        {
            tile.RegisterCallback<PointerEnterEvent>(_ => SetHovered(tile, true));
            tile.RegisterCallback<PointerLeaveEvent>(_ => SetHovered(tile, false));
        }

        private static void StyleTile(VisualElement tile)
        {
            tile.style.width = k_tileWidth;
            tile.style.alignItems = Align.Center;
            tile.style.paddingTop = 3;
            tile.style.paddingBottom = 2;
            SetBorder(tile, s_idleBorder);
        }

        private static Label CreateLabel(string text)
        {
            Label label = new Label(text) { pickingMode = PickingMode.Ignore };
            label.style.fontSize = 9;
            label.style.unityTextAlign = TextAnchor.MiddleCenter;
            label.style.whiteSpace = WhiteSpace.Normal;
            label.style.overflow = Overflow.Hidden;
            label.style.maxWidth = k_tileWidth - 4f;
            return label;
        }

        private static Button CreateDeleteButton(string buttonName, string tooltip, Action clicked)
        {
            // Owner: this button; stop point: it is dropped by the next SetCatalog, SetCustomPieces or with the window GUI.
            Button delete = new Button(clicked) { name = buttonName, tooltip = tooltip };
            delete.AddToClassList(k_deleteClass);
            delete.style.position = Position.Absolute;
            delete.style.top = -k_deleteOverhang;
            delete.style.right = -k_deleteOverhang;
            delete.style.width = k_deleteSize;
            delete.style.height = k_deleteSize;
            delete.style.marginLeft = 0;
            delete.style.marginRight = 0;
            delete.style.marginTop = 0;
            delete.style.marginBottom = 0;
            delete.style.paddingLeft = 0;
            delete.style.paddingRight = 0;
            delete.style.paddingTop = 0;
            delete.style.paddingBottom = 0;
            delete.style.fontSize = 9;
            delete.style.unityTextAlign = TextAnchor.MiddleCenter;
            // The round red icon becomes the whole badge, with no button box around its edge.
            ActionIcons.ShowDelete(delete, k_deleteSize);
            return delete;
        }

        // Only the piece's cells, centred on its bounding box at the catalog's shared cell size, row 0 at the bottom.
        private static void DrawThumbnail(MeshGenerationContext context, IReadOnlyList<Vector2Int> cells, float cellSize)
        {
            RectInt bounds = LevelShapeThumbnail.GetBounds(cells);
            Painter2D painter = context.painter2D;
            painter.fillColor = s_filledCell;
            for (int index = 0; index < cells.Count; index++)
            {
                Rect rect = LevelShapeThumbnail.GetCellRect(bounds, cells[index], k_gridSize, cellSize);
                // A one-pixel gap between cells keeps the piece readable at small sizes.
                float left = rect.x + 0.5f;
                float top = rect.y + 0.5f;
                float size = rect.width - 1f;
                painter.BeginPath();
                painter.MoveTo(new Vector2(left, top));
                painter.LineTo(new Vector2(left + size, top));
                painter.LineTo(new Vector2(left + size, top + size));
                painter.LineTo(new Vector2(left, top + size));
                painter.ClosePath();
                painter.Fill();
            }
        }

        private static void SetBorder(VisualElement tile, Color color, float width = k_borderWidth)
        {
            tile.style.borderLeftWidth = width;
            tile.style.borderRightWidth = width;
            tile.style.borderTopWidth = width;
            tile.style.borderBottomWidth = width;
            tile.style.borderLeftColor = color;
            tile.style.borderRightColor = color;
            tile.style.borderTopColor = color;
            tile.style.borderBottomColor = color;
        }
    }
}
