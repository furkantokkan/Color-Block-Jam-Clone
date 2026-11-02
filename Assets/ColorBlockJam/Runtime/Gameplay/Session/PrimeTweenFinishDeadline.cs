using System;
using PrimeTween;

namespace ColorBlockJam.Gameplay.Session
{
    /// <summary>
    /// Scene-owned unscaled deadline. The session cancels at Teardown; scene disposal cancels it as well.
    /// </summary>
    public sealed class PrimeTweenFinishDeadline : IFinishDeadline, IDisposable
    {
        private Tween m_tween;
        private Action m_completed;
        private bool m_disposed;

        public void Start(float seconds, Action completed)
        {
            if (m_disposed)
            {
                throw new ObjectDisposedException(nameof(PrimeTweenFinishDeadline));
            }

            if (!(seconds > 0f) || float.IsInfinity(seconds))
            {
                throw new ArgumentOutOfRangeException(nameof(seconds));
            }

            if (completed == null)
            {
                throw new ArgumentNullException(nameof(completed));
            }

            Cancel();
            m_completed = completed;
            m_tween = Tween.Delay(this, seconds, static deadline => deadline.Complete(), useUnscaledTime: true);
        }

        public void Cancel()
        {
            m_completed = null;
            if (m_tween.isAlive)
            {
                m_tween.Stop();
            }
            m_tween = default;
        }

        public void Dispose()
        {
            m_disposed = true;
            Cancel();
        }

        private void Complete()
        {
            Action completed = m_completed;
            m_completed = null;
            m_tween = default;
            completed?.Invoke();
        }
    }
}
