using System;
using System.Collections.Generic;
using ColorBlockJam.Domain.Session;
using NUnit.Framework;

namespace ColorBlockJam.Tests.Gameplay.Domain
{
    public sealed class SessionTransitionsTests
    {
        // The PRD 7.5 table, row by row (design D.1).
        private static readonly object[] s_rows =
        {
            new object[] { SessionState.Home, SessionRequest.OpenPreLevel, SessionState.Home },
            new object[] { SessionState.Home, SessionRequest.ClosePreLevel, SessionState.Home },
            new object[] { SessionState.Home, SessionRequest.Play, SessionState.Playing },
            new object[] { SessionState.Playing, SessionRequest.Pause, SessionState.Paused },
            new object[] { SessionState.Playing, SessionRequest.Background, SessionState.Paused },
            new object[] { SessionState.Playing, SessionRequest.BoosterCommit, SessionState.Paused },
            new object[] { SessionState.Playing, SessionRequest.ZeroStockTap, SessionState.Paused },
            new object[] { SessionState.Paused, SessionRequest.Resume, SessionState.Playing },
            new object[] { SessionState.Paused, SessionRequest.CommitSucceeded, SessionState.Playing },
            new object[] { SessionState.Paused, SessionRequest.CommitFailed, SessionState.Playing },
            new object[] { SessionState.Paused, SessionRequest.PurchaseClosed, SessionState.Playing },
            new object[] { SessionState.Paused, SessionRequest.Background, SessionState.Paused },
            new object[] { SessionState.Paused, SessionRequest.Restart, SessionState.Playing },
            new object[] { SessionState.Paused, SessionRequest.Home, SessionState.Home },
            new object[] { SessionState.Playing, SessionRequest.AllRequiredCleared, SessionState.Won },
            new object[] { SessionState.Playing, SessionRequest.TimerExpired, SessionState.Failed },
            new object[] { SessionState.Won, SessionRequest.NextLevel, SessionState.Playing },
            new object[] { SessionState.Won, SessionRequest.Home, SessionState.Home },
            new object[] { SessionState.Failed, SessionRequest.Home, SessionState.Home },
            new object[] { SessionState.Failed, SessionRequest.Restart, SessionState.Playing }
        };

        [TestCaseSource(nameof(s_rows))]
        public void EveryTableRowReachesItsTarget(SessionState from, SessionRequest request, SessionState expected)
        {
            bool allowed = SessionTransitions.TryGetTarget(from, request, out SessionState to);

            Assert.That(allowed, Is.True);
            Assert.That(to, Is.EqualTo(expected));
        }

        [Test]
        public void EveryOtherPairIsRejectedAndKeepsTheState()
        {
            HashSet<(SessionState, SessionRequest)> listed = new HashSet<(SessionState, SessionRequest)>();
            for (int i = 0; i < s_rows.Length; i++)
            {
                object[] row = (object[])s_rows[i];
                listed.Add(((SessionState)row[0], (SessionRequest)row[1]));
            }

            int rejected = 0;
            foreach (SessionState from in (SessionState[])Enum.GetValues(typeof(SessionState)))
            {
                foreach (SessionRequest request in (SessionRequest[])Enum.GetValues(typeof(SessionRequest)))
                {
                    if (listed.Contains((from, request)))
                    {
                        continue;
                    }

                    bool allowed = SessionTransitions.TryGetTarget(from, request, out SessionState to);

                    Assert.That(allowed, Is.False, $"{from} + {request}");
                    Assert.That(to, Is.EqualTo(from), $"{from} + {request}");
                    rejected++;
                }
            }

            Assert.That(rejected, Is.EqualTo(5 * 16 - s_rows.Length));
        }

        [Test]
        public void NoTerminalRequestLeavesATerminalState()
        {
            SessionRequest[] terminal = { SessionRequest.AllRequiredCleared, SessionRequest.TimerExpired };
            SessionState[] ended = { SessionState.Won, SessionState.Failed };
            for (int i = 0; i < ended.Length; i++)
            {
                for (int j = 0; j < terminal.Length; j++)
                {
                    Assert.That(SessionTransitions.TryGetTarget(ended[i], terminal[j], out _), Is.False);
                }
            }
        }
    }
}
