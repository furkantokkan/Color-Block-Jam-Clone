using System;
using ColorBlockJam.Runtime.Board;
using UnityEngine;

namespace ColorBlockJam.Runtime.Levels
{
    /// <summary>
    /// The human name of one piece a level uses that the piece library lacks (DEC-193): the piece's ShapeKey and the
    /// name designers see for it, kept in the level so it survives reopening. The key is stored as its code, because
    /// Unity serializes this record inside the Level Editor's draft. The runtime validates it and never reads it.
    /// </summary>
    [Serializable]
    public sealed class LevelPieceName
    {
        /// <summary>The longest name a piece can have.</summary>
        public const int MaxNameLength = 128;

        [SerializeField] private int m_keyCode;
        [SerializeField] private string m_name;

        public LevelPieceName()
        {
        }

        public LevelPieceName(ShapeKey key, string name)
            : this(key.Code, name)
        {
        }

        private LevelPieceName(int keyCode, string name)
        {
            m_keyCode = keyCode;
            m_name = name;
        }

        /// <summary>The named piece, or the default, invalid key when the stored code is not a piece's canonical code.</summary>
        public ShapeKey Key => ShapeKey.TryFromCode(m_keyCode, out ShapeKey key) ? key : default;

        public string Name => m_name;

        /// <summary>A copy that keeps the stored code as it is, valid or not, so validation still sees it.</summary>
        public LevelPieceName Clone()
        {
            return new LevelPieceName(m_keyCode, m_name);
        }
    }
}
