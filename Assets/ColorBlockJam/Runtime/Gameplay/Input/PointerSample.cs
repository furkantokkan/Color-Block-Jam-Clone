using UnityEngine;

namespace ColorBlockJam.Gameplay.Input
{
    /// <summary>One polled reading of the primary pointer: its phase and screen position in pixels.</summary>
    public readonly struct PointerSample
    {
        public PointerSample(PointerPhase phase, Vector2 screenPosition)
        {
            Phase = phase;
            ScreenPosition = screenPosition;
        }

        public PointerPhase Phase { get; }

        public Vector2 ScreenPosition { get; }
    }
}
