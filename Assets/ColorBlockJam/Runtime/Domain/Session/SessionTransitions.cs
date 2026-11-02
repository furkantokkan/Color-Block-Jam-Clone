namespace ColorBlockJam.Domain.Session
{
    /// <summary>
    /// The PRD 7.5 transition table as a pure function (AC-5.6, V-E5). Every pair not listed is
    /// rejected. No row leaves Won or Failed on a terminal request, so the table itself is the
    /// terminal latch (RULE-17).
    /// </summary>
    public static class SessionTransitions
    {
        public static bool TryGetTarget(SessionState from, SessionRequest request, out SessionState to)
        {
            to = from;
            switch (from)
            {
                case SessionState.Home:
                    return TryGetFromHome(request, ref to);
                case SessionState.Playing:
                    return TryGetFromPlaying(request, ref to);
                case SessionState.Paused:
                    return TryGetFromPaused(request, ref to);
                case SessionState.Won:
                    return TryGetFromWon(request, ref to);
                case SessionState.Failed:
                    return TryGetFromFailed(request, ref to);
                default:
                    return false;
            }
        }

        private static bool TryGetFromHome(SessionRequest request, ref SessionState to)
        {
            switch (request)
            {
                case SessionRequest.OpenPreLevel:
                case SessionRequest.ClosePreLevel:
                    to = SessionState.Home;
                    return true;
                case SessionRequest.Play:
                    to = SessionState.Playing;
                    return true;
                default:
                    return false;
            }
        }

        private static bool TryGetFromPlaying(SessionRequest request, ref SessionState to)
        {
            switch (request)
            {
                case SessionRequest.Pause:
                case SessionRequest.Background:
                case SessionRequest.BoosterCommit:
                case SessionRequest.ZeroStockTap:
                    to = SessionState.Paused;
                    return true;
                case SessionRequest.AllRequiredCleared:
                    to = SessionState.Won;
                    return true;
                case SessionRequest.TimerExpired:
                    to = SessionState.Failed;
                    return true;
                default:
                    return false;
            }
        }

        private static bool TryGetFromPaused(SessionRequest request, ref SessionState to)
        {
            switch (request)
            {
                case SessionRequest.Resume:
                case SessionRequest.CommitSucceeded:
                case SessionRequest.CommitFailed:
                case SessionRequest.PurchaseClosed:
                case SessionRequest.Restart:
                    to = SessionState.Playing;
                    return true;
                case SessionRequest.Background:
                    to = SessionState.Paused;
                    return true;
                case SessionRequest.Home:
                    to = SessionState.Home;
                    return true;
                default:
                    return false;
            }
        }

        private static bool TryGetFromWon(SessionRequest request, ref SessionState to)
        {
            switch (request)
            {
                case SessionRequest.NextLevel:
                    to = SessionState.Playing;
                    return true;
                case SessionRequest.Home:
                    to = SessionState.Home;
                    return true;
                default:
                    return false;
            }
        }

        private static bool TryGetFromFailed(SessionRequest request, ref SessionState to)
        {
            switch (request)
            {
                case SessionRequest.Restart:
                    to = SessionState.Playing;
                    return true;
                case SessionRequest.Home:
                    to = SessionState.Home;
                    return true;
                default:
                    return false;
            }
        }
    }
}
