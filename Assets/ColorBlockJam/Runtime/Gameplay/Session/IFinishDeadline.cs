using System;

namespace ColorBlockJam.Gameplay.Session
{
    /// <summary>A session's cancellable deadline for its bounded terminal presentation.</summary>
    public interface IFinishDeadline
    {
        /// <summary>Replaces any pending deadline. Positive seconds; completion never runs inline.</summary>
        void Start(float seconds, Action completed);

        /// <summary>Cancels without invoking completion. Repeated cancellation does nothing.</summary>
        void Cancel();
    }
}
