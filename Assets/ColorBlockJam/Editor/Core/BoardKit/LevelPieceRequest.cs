using System;
using System.Collections.Generic;
using UnityEngine;

namespace ColorBlockJam.Editor.BoardKit
{
    /// <summary>
    /// A piece that belongs to one level, passed between the Level Editor and the Piece Editor with the name the
    /// designer sees. The piece id is the Level Editor's own token for the piece, opaque here, and never its ShapeKey,
    /// because a piece being drawn changes its key with every stroke (DEC-193). The cells are copied, so neither window
    /// can change the other's copy.
    /// </summary>
    [Serializable]
    internal sealed class LevelPieceRequest
    {
        // Serialized so the Piece Editor keeps the request across a domain reload.
        [SerializeField] private string m_levelId;
        [SerializeField] private string m_pieceId;
        [SerializeField] private Vector2Int[] m_cells;
        [SerializeField] private string m_name;

        internal LevelPieceRequest(string levelId, string pieceId, IReadOnlyList<Vector2Int> cells, string name = null)
        {
            m_levelId = levelId ?? string.Empty;
            m_pieceId = pieceId ?? string.Empty;
            m_cells = new Vector2Int[cells == null ? 0 : cells.Count];
            for (int i = 0; i < m_cells.Length; i++)
                m_cells[i] = cells[i];
            m_name = name ?? string.Empty;
        }

        public string LevelId => m_levelId;
        public string PieceId => m_pieceId;
        public IReadOnlyList<Vector2Int> Cells => Array.AsReadOnly(m_cells ?? Array.Empty<Vector2Int>());

        /// <summary>The piece's display name, or empty when the level gave none.</summary>
        public string Name => m_name ?? string.Empty;
    }
}
