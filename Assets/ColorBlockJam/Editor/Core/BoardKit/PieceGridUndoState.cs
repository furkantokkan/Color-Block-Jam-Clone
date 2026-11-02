using UnityEngine;

namespace ColorBlockJam.Editor.BoardKit
{
    // A transient Undo target keeps grid history separate from the window's name,
    // selection and preview color. It is never saved as a project asset.
    internal sealed class PieceGridUndoState : ScriptableObject
    {
        [SerializeField] private PieceGridDraft m_grid = new PieceGridDraft();
        public PieceGridDraft Grid => m_grid;
    }
}
