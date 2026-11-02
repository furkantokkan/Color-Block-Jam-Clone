namespace ColorBlockJam.Gameplay.Session
{
    /// <summary>Presentation-only feedback for a selected block whose drag was refused.</summary>
    public interface IBlockedDragFeedback
    {
        void ShowBlockedDrag(int block);
    }
}
