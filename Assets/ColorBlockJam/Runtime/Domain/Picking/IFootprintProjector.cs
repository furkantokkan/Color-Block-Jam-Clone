namespace ColorBlockJam.Domain.Picking
{
    /// <summary>
    /// Projects a board-plane point (cell units, at block-top height) to screen space: the camera in
    /// the scene, a fake in tests. Picking measures distances in screen space through it (DEC-115).
    /// </summary>
    public interface IFootprintProjector
    {
        ScreenPoint Project(float boardX, float boardY);
    }
}
