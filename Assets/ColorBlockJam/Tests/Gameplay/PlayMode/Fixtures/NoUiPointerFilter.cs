using ColorBlockJam.Gameplay.Input;
using UnityEngine;

namespace ColorBlockJam.Tests.Gameplay.PlayMode.Fixtures
{
    /// <summary>A UI filter for a scene without runtime UI: no press lands on UI.</summary>
    public sealed class NoUiPointerFilter : IUiPointerFilter
    {
        public bool IsOverUi(Vector2 screenPosition) => false;
    }
}
