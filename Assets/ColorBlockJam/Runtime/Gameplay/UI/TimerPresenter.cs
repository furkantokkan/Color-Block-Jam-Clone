using System;
using ColorBlockJam.Domain.Session;
using ColorBlockJam.Gameplay.Session;
using ColorBlockJam.Runtime.Boosters;
using Onity.Reactive;

namespace ColorBlockJam.Gameplay.UI
{
    /// <summary>
    /// Shows the main countdown as "m:ss" and the separate Time Freeze counter with its bar (B-9, AC-3): both follow the
    /// session's whole-second values, which change only in the view sync phase, and pick prebuilt strings, so the text
    /// changes only when the shown second changes and a Playing tick allocates nothing. The freeze bar does not step: on
    /// each shown second the view drains it smoothly toward the next second's fill.
    /// </summary>
    /// <remarks>
    /// Owner: the gameplay HUD. Its four subscriptions go into one CompositeDisposable that <see cref="Unbind"/>
    /// clears; <see cref="Dispose"/> is the last stop point. The session state holds the view's drain outside Playing,
    /// so a pause holds the bar with the freeze clock.
    /// </remarks>
    public sealed class TimerPresenter : IDisposable
    {
        private const int k_nothingShown = -1;

        private readonly LevelSession m_session;
        private readonly CompositeDisposable m_subscriptions = new CompositeDisposable();
        private readonly Observer<int> m_onSecondsChanged;
        private readonly Observer<int> m_onFreezeSecondsChanged;
        private readonly Observer<bool> m_onFrozenChanged;
        private readonly Observer<SessionState> m_onStateChanged;

        // The Time Freeze duration in whole seconds, the bar's full length.
        private readonly int m_freezeSeconds;

        private IGameplayHudView m_view;
        private HudText m_text;
        private int m_shownSeconds = k_nothingShown;
        private int m_shownFreezeSeconds = k_nothingShown;
        private bool m_disposed;

        public TimerPresenter(LevelSession session, BoosterCatalog catalog)
        {
            m_session = session ?? throw new ArgumentNullException(nameof(session));
            if (catalog == null)
            {
                throw new ArgumentNullException(nameof(catalog));
            }

            m_freezeSeconds = catalog.TryGet(BoosterId.TimeFreeze, out BoosterDefinition definition)
                && definition is TimeFreezeBoosterDefinition freeze
                    ? Math.Max(1, (int)Math.Ceiling(freeze.DurationSeconds))
                    : 1;
            m_onSecondsChanged = OnSecondsChanged;
            m_onFreezeSecondsChanged = OnFreezeSecondsChanged;
            m_onFrozenChanged = OnFrozenChanged;
            m_onStateChanged = OnStateChanged;
        }

        public bool IsBound => m_view != null;

        /// <summary>The Time Freeze counter's largest value: the freeze duration rounded up.</summary>
        public int FreezeSeconds => m_freezeSeconds;

        public void Bind(IGameplayHudView view, HudText text)
        {
            if (m_disposed)
            {
                throw new ObjectDisposedException(nameof(TimerPresenter));
            }

            if (m_view != null)
            {
                throw new InvalidOperationException("Timer presenter: already bound; unbind first.");
            }

            m_view = view ?? throw new ArgumentNullException(nameof(view));
            m_text = text ?? throw new ArgumentNullException(nameof(text));
            m_shownSeconds = k_nothingShown;
            m_shownFreezeSeconds = k_nothingShown;
            m_subscriptions.Add(m_session.DisplayedSeconds.Subscribe(m_onSecondsChanged));
            m_subscriptions.Add(m_session.FreezeDisplayedSeconds.Subscribe(m_onFreezeSecondsChanged));
            m_subscriptions.Add(m_session.IsFrozen.Subscribe(m_onFrozenChanged));
            m_subscriptions.Add(m_session.State.Subscribe(m_onStateChanged));
        }

        /// <summary>
        /// Shows a level's authored time on Home, before Play starts the countdown and publishes the same value; the
        /// caller reserved the clock table for it.
        /// </summary>
        public void ShowSeconds(int seconds)
        {
            OnSecondsChanged(seconds);
        }

        public void Unbind()
        {
            if (m_view == null)
            {
                return;
            }

            m_subscriptions.Clear();
            m_view = null;
        }

        public void Dispose()
        {
            if (m_disposed)
            {
                return;
            }

            Unbind();
            m_subscriptions.Dispose();
            m_disposed = true;
        }

        private void OnSecondsChanged(int seconds)
        {
            if (m_view == null || seconds == m_shownSeconds)
            {
                return;
            }

            m_shownSeconds = seconds;
            m_view.SetTimer(m_text.GetClock(seconds));
        }

        private void OnFreezeSecondsChanged(int seconds)
        {
            if (m_view == null || seconds == m_shownFreezeSeconds)
            {
                return;
            }

            m_shownFreezeSeconds = seconds;
            m_view.SetFreezeCounter(m_text.GetNumber(seconds), GetFreezeFill(seconds));

            // The counter rounds up, so it reads s from the moment s seconds remain until s - 1 remain: draining to the
            // next second's fill over that one Playing second follows the real remaining time.
            if (seconds > 0)
            {
                m_view.DrainFreezeBar(GetFreezeFill(seconds - 1), 1f);
            }
        }

        private float GetFreezeFill(int seconds)
        {
            return seconds >= m_freezeSeconds ? 1f : seconds <= 0 ? 0f : (float)seconds / m_freezeSeconds;
        }

        private void OnStateChanged(SessionState state)
        {
            m_view?.SetFreezeBarPaused(state != SessionState.Playing);
        }

        private void OnFrozenChanged(bool frozen)
        {
            m_view?.SetFrozen(frozen);
        }
    }
}
