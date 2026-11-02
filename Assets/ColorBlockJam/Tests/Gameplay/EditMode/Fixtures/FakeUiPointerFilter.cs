using ColorBlockJam.Gameplay.Input;
using UnityEngine;

namespace ColorBlockJam.Tests.Gameplay.Fixtures
{
    /// <summary>Reports every press as UI while OverUi is set.</summary>
    public sealed class FakeUiPointerFilter : IUiPointerFilter
    {
        public bool OverUi { get; set; }

        public bool IsOverUi(Vector2 screenPosition) => OverUi;
    }
}
