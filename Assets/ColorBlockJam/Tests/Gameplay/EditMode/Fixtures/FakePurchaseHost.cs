using ColorBlockJam.Gameplay.Boosters;
using ColorBlockJam.Runtime.Boosters;

namespace ColorBlockJam.Tests.Gameplay.Fixtures
{
    /// <summary>A purchase presentation that shows while Available is set and counts what the coordinator asked of it.</summary>
    public sealed class FakePurchaseHost : IInGamePurchaseHost
    {
        public bool Available { get; set; } = true;

        public bool IsShowing { get; private set; }

        public int ShowCount { get; private set; }

        public int HideCount { get; private set; }

        public BoosterDefinition LastDefinition { get; private set; }

        public bool TryShow(BoosterDefinition definition)
        {
            if (!Available)
            {
                return false;
            }

            ShowCount++;
            LastDefinition = definition;
            IsShowing = true;
            return true;
        }

        public void Hide()
        {
            HideCount++;
            IsShowing = false;
        }
    }
}
