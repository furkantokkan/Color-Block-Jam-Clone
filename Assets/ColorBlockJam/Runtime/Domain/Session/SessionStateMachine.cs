namespace ColorBlockJam.Domain.Session
{
    /// <summary>
    /// Holds the session state, its pause reason and the attempt number, and changes them only through
    /// the transition table. The pause reason follows from the request, so callers cannot disagree
    /// with the table about why the session paused.
    /// </summary>
    public sealed class SessionStateMachine
    {
        // The one mapping between the pausing requests and their reasons; TryApply reads it one way
        // and TryPause the other, so a new pause reason is added in this row list only.
        private static readonly (SessionRequest Request, PauseReason Reason)[] s_pauses =
        {
            (SessionRequest.Pause, PauseReason.User),
            (SessionRequest.Background, PauseReason.Background),
            (SessionRequest.BoosterCommit, PauseReason.Saving),
            (SessionRequest.ZeroStockTap, PauseReason.Purchase)
        };

        public SessionState State { get; private set; } = SessionState.Home;

        /// <summary>Why the session is Paused; None in every other state.</summary>
        public PauseReason PauseReason { get; private set; }

        /// <summary>Counts entries into Playing that start an attempt: Play, Restart and Next Level, never Resume.</summary>
        public int AttemptNumber { get; private set; }

        /// <summary>Applies a request when the table allows it; a rejected request changes nothing (AC-5.6).</summary>
        public bool TryApply(SessionRequest request)
        {
            if (State == SessionState.Paused && !MatchesPauseReason(request))
            {
                return false;
            }

            if (!SessionTransitions.TryGetTarget(State, request, out SessionState to))
            {
                return false;
            }

            SessionState from = State;
            State = to;
            PauseReason = to == SessionState.Paused ? GetPauseReason(request) : PauseReason.None;
            if (to == SessionState.Playing && (from != SessionState.Paused || request == SessionRequest.Restart))
            {
                AttemptNumber++;
            }

            return true;
        }

        /// <summary>Applies the pausing request that belongs to the reason; None and unknown reasons are refused.</summary>
        public bool TryPause(PauseReason reason)
        {
            for (int i = 0; i < s_pauses.Length; i++)
            {
                if (s_pauses[i].Reason == reason)
                {
                    return TryApply(s_pauses[i].Request);
                }
            }

            return false;
        }

        // A save or purchase completion resumes only the pause it belongs to. Backgrounding replaces the
        // reason with Background, so a late completion never resumes gameplay by itself (DEC-92).
        private bool MatchesPauseReason(SessionRequest request)
        {
            switch (request)
            {
                case SessionRequest.CommitSucceeded:
                case SessionRequest.CommitFailed:
                    return PauseReason == PauseReason.Saving;
                case SessionRequest.PurchaseClosed:
                    return PauseReason == PauseReason.Purchase;
                default:
                    return true;
            }
        }

        private static PauseReason GetPauseReason(SessionRequest request)
        {
            for (int i = 0; i < s_pauses.Length; i++)
            {
                if (s_pauses[i].Request == request)
                {
                    return s_pauses[i].Reason;
                }
            }

            return PauseReason.None;
        }
    }
}
