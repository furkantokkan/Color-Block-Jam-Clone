using ColorBlockJam.Domain.Session;
using Onity.Reactive;

namespace ColorBlockJam.Gameplay.Hud
{
    /// <summary>
    /// The narrow part of the level session the top bar and the in-game menus use (Game HUD design 4): the state, why
    /// it is paused, the opening hold, a pending booster write, and the four requests the menus make. EditMode tests
    /// drive the menu flow and the top bar through a fake of it.
    /// </summary>
    public interface IGameSessionControl
    {
        /// <summary>The session state; it notifies only on a change.</summary>
        IReadOnlyReactiveProperty<SessionState> State { get; }

        /// <summary>Why the session is Paused; None outside Paused. A change of reason alone raises no notification.</summary>
        PauseReason PauseReason { get; }

        /// <summary>True while the starting-booster opening holds the attempt (B-16).</summary>
        IReadOnlyReactiveProperty<bool> IsOpening { get; }

        /// <summary>True while an accepted booster's debit is being written; Resume, Restart and Home are refused.</summary>
        IReadOnlyReactiveProperty<bool> IsWritePending { get; }

        /// <summary>True from Won until the bounded Finish ends and the attempt is torn down (DEC-93).</summary>
        IReadOnlyReactiveProperty<bool> IsFinishing { get; }

        /// <summary>True once the player pressed a block in this attempt and the countdown runs (DEC-189).</summary>
        IReadOnlyReactiveProperty<bool> IsTimerStarted { get; }

        /// <summary>Pauses a Playing session for the player (Paused(User)); false when the session refuses.</summary>
        bool Pause();

        /// <summary>Resumes a Paused session; false while a write is pending or outside Paused.</summary>
        bool Resume();

        /// <summary>
        /// Resumes a Background pause after the foreground return, or once a pending booster write resolves; false when
        /// nothing resumed now or the session is not paused by backgrounding.
        /// </summary>
        bool ResumeFromBackground();

        /// <summary>Rebuilds the level from its authored data; allowed from Paused and Failed only.</summary>
        bool Restart();

        /// <summary>Tears the attempt down and returns the session to Home; it changes no scene.</summary>
        bool GoHome();
    }
}
