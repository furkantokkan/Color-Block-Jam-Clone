using System;
using ColorBlockJam.Runtime.Board;
using UnityEngine;

namespace ColorBlockJam.Presentation.Board
{
    /// <summary>
    /// Maps each BlockPieceKind and Arrow_N marker to its calibrated board-kit prefab, and names the one bare block
    /// root every block is checked out from (DEC-193).
    /// </summary>
    [CreateAssetMenu(menuName = "Color Block Jam/Board/Piece Catalog")]
    public sealed class BoardPieceCatalog : ScriptableObject
    {
        [Serializable]
        public struct Entry
        {
            [SerializeField] private BlockPieceKind m_kind;
            [SerializeField] private BoardPieceView m_prefab;
            public BlockPieceKind Kind => m_kind;
            public BoardPieceView Prefab => m_prefab;
        }

        // The calibrated Arrow_1 to Arrow_3 marker prefabs (catalog BLK-01) that m_environment already lists.
        private static readonly string[] s_arrowNames = { "Arrow_1", "Arrow_2", "Arrow_3" };

        [SerializeField] private Entry[] m_pieces = Array.Empty<Entry>();
        [SerializeField] private BoardPieceView[] m_environment = Array.Empty<BoardPieceView>();
        [Tooltip("The bare block root (Prefabs/Board/ModularBlock.prefab: BlockPiece with its ModularBlockView, no child). " +
                 "Every block is a copy of it showing the level's own cells (DEC-193).")]
        [SerializeField] private BlockPiece m_blockRoot;

        /// <summary>The one prefab every block root is pooled from; BlockPieceFactory.TryGetRoot checks it.</summary>
        public BlockPiece BlockRoot => m_blockRoot;

        public BoardPieceView Get(BlockPieceKind kind)
        {
            for (int i = 0; i < m_pieces.Length; i++)
                if (m_pieces[i].Kind == kind && m_pieces[i].Prefab != null) return m_pieces[i].Prefab;
            throw new InvalidOperationException("Missing calibrated board piece.");
        }

        /// <summary>
        /// The Arrow_N marker prefab (variant 1 to 3), found by its prefab name in the environment list; false when
        /// the list lacks it. Read-only; call it on Home, because reading a prefab name allocates.
        /// </summary>
        public bool TryGetArrow(int variant, out BoardPieceView prefab)
        {
            prefab = null;
            if (variant < 1 || variant > s_arrowNames.Length) return false;
            string name = s_arrowNames[variant - 1];
            for (int i = 0; i < m_environment.Length; i++)
            {
                if (m_environment[i] != null && m_environment[i].name == name)
                {
                    prefab = m_environment[i];
                    return true;
                }
            }
            return false;
        }
    }
}
