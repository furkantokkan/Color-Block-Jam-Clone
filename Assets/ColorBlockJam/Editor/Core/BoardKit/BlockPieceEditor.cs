using System;
using System.Collections.Generic;
using ColorBlockJam.Presentation.Board;
using ColorBlockJam.Runtime.Board;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace ColorBlockJam.Editor.BoardKit
{
    /// <summary>
    /// Shows a BlockPiece in the Inspector: its view and, for a block root in a scene, the cells it shows with a
    /// summary line (size, the piece library template or "level piece", and the key). No prefab carries a shape
    /// (DEC-193), so the root prefab asset shows one line that says so instead.
    /// </summary>
    [CustomEditor(typeof(BlockPiece))]
    internal sealed class BlockPieceEditor : UnityEditor.Editor
    {
        internal const string ShapeGridName = "block-piece-shape-grid";
        internal const string ShapeSummaryName = "block-piece-shape-summary";
        internal const string RootAssetText = "Block root: shows the cells of the block it is checked out for.";

        private const long k_refreshMilliseconds = 250;
        private const string k_waitingText = "No cells: this root is waiting in its pool.";
        private const string k_notPreparedText = "No cells: this root is not prepared.";
        private const string k_shapeTooltip =
            "The cells this piece shows; the outlined cell is the block origin. The cells are the piece: its key is the "
            + "same for every turn and mirror, in every level (DEC-193).";

        private BlockPieceShapeGrid m_shapeGrid;
        private Label m_shapeSummary;

        /// <summary>
        /// Script and view, then for the root prefab asset one line, and for a scene root the shape grid with its
        /// summary line.
        /// </summary>
        public override VisualElement CreateInspectorGUI()
        {
            var root = new VisualElement();
            var script = new PropertyField(serializedObject.FindProperty("m_Script"));
            script.SetEnabled(false);
            root.Add(script);
            root.Add(new PropertyField(serializedObject.FindProperty("m_view")));

            m_shapeSummary = new Label { name = ShapeSummaryName };
            m_shapeSummary.style.whiteSpace = WhiteSpace.Normal;
            m_shapeSummary.style.marginLeft = 3;
            m_shapeSummary.style.marginTop = 2;
            if (EditorUtility.IsPersistent(target))
            {
                m_shapeSummary.text = RootAssetText;
                root.Add(m_shapeSummary);
                return root;
            }

            var header = new Label("Shape") { tooltip = k_shapeTooltip };
            header.style.unityFontStyleAndWeight = FontStyle.Bold;
            header.style.marginTop = 6;
            header.style.marginLeft = 3;
            root.Add(header);
            m_shapeGrid = new BlockPieceShapeGrid { name = ShapeGridName, tooltip = k_shapeTooltip };
            m_shapeGrid.style.marginLeft = 3;
            m_shapeGrid.style.marginTop = 2;
            root.Add(m_shapeGrid);
            root.Add(m_shapeSummary);

            Refresh();
            // A scene root's cells change without a serialized change (pool checkout and return, a Rocket split), so
            // the Inspector reads them again. Owner: the root's scheduler. Stop point: the item stops when the
            // Inspector's panel releases the root.
            root.schedule.Execute(Refresh).Every(k_refreshMilliseconds);
            return root;
        }

        /// <summary>
        /// Shows the cells a scene root shows now; the grid and the summary change only with them. A root prefab asset
        /// has no grid, so this does nothing for it.
        /// </summary>
        internal void Refresh()
        {
            var piece = target as BlockPiece;
            if (piece == null || m_shapeGrid == null) return;

            IReadOnlyList<Vector2Int> shown = piece.IsPrepared ? piece.Cells : Array.Empty<Vector2Int>();
            bool cellsChanged = m_shapeGrid.SetCells(shown);
            if (shown.Count == 0)
                m_shapeSummary.text = piece.IsPrepared ? k_waitingText : k_notPreparedText;
            else if (cellsChanged)
                m_shapeSummary.text = BlockPieceShapeSummary.Describe(m_shapeGrid.Cells, FindTemplateName);
        }

        // The display name of the piece library template with this key; null for a level piece or an unreadable library.
        private static string FindTemplateName(ShapeKey key)
        {
            BlockShapeLibrary library = BlockShapeLibrary.LoadProject();
            if (library == null) return null;
            try
            {
                return library.TryGet(key, out BlockShapeLibrary.Shape shape) ? shape.DisplayName : null;
            }
            catch (Exception error) when (error is ArgumentException || error is InvalidOperationException)
            {
                // A library whose JSON cannot be read names no template.
                return null;
            }
        }
    }
}
