using ColorBlockJam.Domain.Board;
using ColorBlockJam.Domain.Hooks;

namespace ColorBlockJam.Tests.Gameplay.Fixtures
{
    /// <summary>An axis constraint whose answer a test may change mid-drag, like an Arrow block's axis.</summary>
    public sealed class AxisGate : IAxisConstraint
    {
        public AxisGate(AxisMask axes)
        {
            Axes = axes;
        }

        public AxisMask Axes { get; set; }

        public AxisMask GetAxes(IBoardReader board, int block) => Axes;
    }
}
