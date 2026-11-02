using ColorBlockJam.Domain.Picking;

namespace ColorBlockJam.Tests.Gameplay.Fixtures
{
    /// <summary>
    /// Projects board points to screen pixels: an orthographic top view by default, or a perspective
    /// view that shrinks rows further up the board, where screen distance differs from board distance.
    /// </summary>
    public sealed class FakeFootprintProjector : IFootprintProjector
    {
        private readonly float m_pixelsPerCell;
        private readonly float m_depthPerRow;

        private FakeFootprintProjector(float pixelsPerCell, float depthPerRow)
        {
            m_pixelsPerCell = pixelsPerCell;
            m_depthPerRow = depthPerRow;
        }

        public static FakeFootprintProjector CreateOrthographic() => new FakeFootprintProjector(100f, 0f);

        public static FakeFootprintProjector CreatePerspective(float depthPerRow) => new FakeFootprintProjector(100f, depthPerRow);

        public ScreenPoint Project(float boardX, float boardY)
        {
            float w = 1f + m_depthPerRow * boardY;
            return new ScreenPoint(boardX * m_pixelsPerCell / w, boardY * m_pixelsPerCell / w);
        }
    }
}
