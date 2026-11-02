using System;
using System.Collections.Generic;
using ColorBlockJam.Runtime.Board;
using ColorBlockJam.Runtime.Levels;
using UnityEngine;

namespace ColorBlockJam.Editor.Levels
{
    /// <summary>
    /// One level piece this Level Editor session tracks: a "+" New piece, the level piece a Pen edit changes, or a
    /// level-piece tile shown in the Piece Editor. Its session-unique token ("piece:&lt;scope&gt;:&lt;n&gt;") is its
    /// identity, because its key changes with every stroke while it is drawn (DEC-193); it also keeps its level scope,
    /// the name it was given here, its footprint, the block its strokes draw and the blocks that follow its changes. It
    /// stays level-only: only the Piece Editor's Save as template adds a piece to the piece library (DEC-180).
    /// </summary>
    [Serializable]
    internal sealed class LevelNamedPiece
    {
        private const string k_defaultPrefix = "Level";

        [SerializeField] private string m_scope;
        [SerializeField] private string m_token;
        [SerializeField] private string m_displayName;
        [SerializeField] private Vector2Int[] m_cells = Array.Empty<Vector2Int>();
        [SerializeField] private string m_blockId;
        [SerializeField] private bool m_wasPlaced;
        [SerializeField] private List<string> m_memberIds = new List<string>();

        public LevelNamedPiece(string scope, string token, string displayName)
        {
            m_scope = scope;
            m_token = token;
            m_displayName = displayName;
        }

        /// <summary>The database-and-level scope (LevelPieceSession.GetLevelScope) the piece belongs to.</summary>
        public string Scope => m_scope;

        /// <summary>The Piece Editor piece id: "piece:&lt;scope&gt;:&lt;n&gt;", unique in this session.</summary>
        public string Token => m_token;

        /// <summary>
        /// The name this session gave the piece: the level's name for its key when it started, else a new
        /// "&lt;Level Name&gt; Piece n". The level's own name for its current key comes first (LevelPieceNames).
        /// </summary>
        public string DisplayName
        {
            get => m_displayName;
            set => m_displayName = value;
        }

        /// <summary>The footprint from (0, 0); empty until the first stroke.</summary>
        public IReadOnlyList<Vector2Int> Cells => m_cells ?? Array.Empty<Vector2Int>();

        /// <summary>The ShapeKey of the footprint; the default key before the first stroke.</summary>
        public ShapeKey Key => ShapeKey.TryCreate(Cells, out ShapeKey key) ? key : default;

        /// <summary>The block the strokes draw and redraw, or null before the first stroke.</summary>
        public string BlockId
        {
            get => string.IsNullOrEmpty(m_blockId) ? null : m_blockId;
            set => m_blockId = value;
        }

        /// <summary>
        /// True once a block of the open level has used the piece. From then on its tile shows only while a block uses
        /// it, so erasing its last block deletes it from the list and an undo that brings a block back restores it
        /// (user request of 2026-09-26). A piece drawn only in the Piece Editor keeps its tile.
        /// </summary>
        public bool WasPlaced
        {
            get => m_wasPlaced;
            set => m_wasPlaced = value;
        }

        /// <summary>
        /// The blocks of the open level that belong to the piece while it is edited (DEC-193): the blocks with its
        /// key when an edit of it started, the block it drew and the blocks placed from its tile. Only these follow its
        /// changes, so a block that merely shares a key the piece passes through on the way never moves.
        /// </summary>
        public IReadOnlyList<string> MemberIds => (IReadOnlyList<string>)m_memberIds ?? Array.Empty<string>();

        public void SetCells(IReadOnlyList<Vector2Int> cells)
        {
            m_cells = LevelCells.Copy(cells);
        }

        /// <summary>Records these blocks, and only these, as the piece's blocks, as an edit of it starts.</summary>
        public void SetMembers(IReadOnlyList<string> blockIds)
        {
            m_memberIds = new List<string>();
            for (int index = 0; blockIds != null && index < blockIds.Count; index++)
            {
                AddMember(blockIds[index]);
            }
        }

        /// <summary>Records one more block of the piece: the block it drew or a block placed from its tile.</summary>
        public void AddMember(string blockId)
        {
            m_memberIds ??= new List<string>();
            if (!string.IsNullOrEmpty(blockId) && !LevelBlockQueries.ContainsId(m_memberIds, blockId))
            {
                m_memberIds.Add(blockId);
            }
        }

        /// <summary>
        /// The default name "&lt;Level Name&gt; Piece &lt;n&gt;" with the lowest n from 1 that
        /// <paramref name="isTaken"/> refuses; the caller compares names ignoring case. A level name too long for a
        /// piece name (LevelPieceName.MaxNameLength) gives "Level Piece &lt;n&gt;".
        /// </summary>
        public static string GetDefaultName(string levelName, Func<string, bool> isTaken)
        {
            string prefix = string.IsNullOrWhiteSpace(levelName) ? k_defaultPrefix : levelName.Trim();
            for (int number = 1; ; number++)
            {
                string name = $"{prefix} Piece {number}";
                if (name.Length > LevelPieceName.MaxNameLength)
                {
                    prefix = k_defaultPrefix;
                    number = 0;
                    continue;
                }

                if (!isTaken(name))
                {
                    return name;
                }
            }
        }
    }
}
