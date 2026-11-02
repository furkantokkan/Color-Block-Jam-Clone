using System;
using System.Threading;
using Onity.Reactive;

namespace ColorBlockJam.Gameplay.Session
{
    /// <summary>
    /// One attempt's cancellation source and disposables (NFR-L1). Teardown cancels the token, then
    /// disposes everything the attempt registered; a new attempt always gets a new lifetime because
    /// a disposed CompositeDisposable is final.
    /// </summary>
    public sealed class AttemptLifetime : IDisposable
    {
        private readonly CancellationTokenSource m_cancellation = new CancellationTokenSource();
        private bool m_disposed;

        public CancellationToken Token => m_cancellation.Token;

        /// <summary>Every subscription or resource the attempt starts; disposed by Teardown step 5.</summary>
        public CompositeDisposable Disposables { get; } = new CompositeDisposable();

        public void Cancel()
        {
            if (!m_disposed)
            {
                m_cancellation.Cancel();
            }
        }

        public void Dispose()
        {
            if (m_disposed)
            {
                return;
            }

            m_disposed = true;
            Disposables.Dispose();
            m_cancellation.Dispose();
        }
    }
}
