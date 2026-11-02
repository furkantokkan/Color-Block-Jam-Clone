namespace ColorBlockJam.Editor.Levels
{
    /// <summary>
    /// Remembers the canvas hit under the pointer and decides whether a pointer move changed it, so the canvas
    /// repaints only when the hovered cell, segment, block or door changes, never for a move inside one cell.
    /// </summary>
    internal sealed class LevelCanvasHover
    {
        public LevelCanvasHit Current { get; private set; } = LevelCanvasHit.Outside;

        /// <summary>Stores the new hit and returns true only when it differs from the previous one.</summary>
        public bool Update(LevelCanvasHit hit)
        {
            if (hit.Equals(Current))
            {
                return false;
            }

            Current = hit;
            return true;
        }
    }
}
