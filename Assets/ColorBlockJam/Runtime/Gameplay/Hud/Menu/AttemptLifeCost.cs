using System;
using System.Threading;
using System.Threading.Tasks;
using ColorBlockJam.Player;
using ColorBlockJam.Player.Lives;
using Onity.Reactive;
using UnityEngine;

namespace ColorBlockJam.Gameplay.Hud.Menu
{
    /// <summary>
    /// Takes at most one life per level attempt (DEC-174): the menu flow charges it when the attempt fails, when the
    /// in-level Retry is confirmed and when HOME leaves a level in progress; a second charge in the same attempt (Retry
    /// on the fail popup) takes nothing, and <see cref="StartNewAttempt"/> after a Restart arms the next one.
    /// </summary>
    /// <remarks>
    /// Owner: <see cref="GameMenuFlow"/>, which creates it and disposes it with itself. The charge task is kept in
    /// <see cref="PendingCharge"/>, never fire-and-forget. It passes no scene token on purpose: the life loss must not be
    /// undone by the Game scene unloading, so only the save service's own lifetime can stop it (PROJECT.md Ownership).
    /// Nothing runs per frame.
    /// </remarks>
    public sealed class AttemptLifeCost : IDisposable
    {
        private readonly ILives m_lives;
        private readonly ReactiveProperty<bool> m_isCharging = new ReactiveProperty<bool>();
        private bool m_charged;
        private bool m_disposed;

        public AttemptLifeCost(ILives lives)
        {
            m_lives = lives ?? throw new ArgumentNullException(nameof(lives));
        }

        /// <summary>True while this attempt's life loss is being saved.</summary>
        public IReadOnlyReactiveProperty<bool> IsCharging => m_isCharging;

        /// <summary>True once this attempt's life was charged (or its charge started).</summary>
        public bool IsCharged => m_charged;

        /// <summary>True when a life is available now, so a new attempt may start.</summary>
        public bool HasLife => m_lives.GetAvailable() > 0;

        /// <summary>The charge in flight or finished, or a completed task when none ran. It never faults.</summary>
        public Task PendingCharge { get; private set; } = Task.CompletedTask;

        /// <summary>Charges this attempt's life once; later calls in the same attempt return the same task.</summary>
        public Task ChargeAsync()
        {
            if (m_disposed || m_charged)
            {
                return PendingCharge;
            }

            m_charged = true;
            PendingCharge = ChargeCoreAsync();
            return PendingCharge;
        }

        /// <summary>A Restart began a new attempt, so its end can cost a life again.</summary>
        public void StartNewAttempt()
        {
            if (!m_isCharging.Value)
            {
                m_charged = false;
            }
        }

        public void Dispose()
        {
            if (m_disposed)
            {
                return;
            }

            m_disposed = true;
            m_isCharging.Dispose();
        }

        private async Task ChargeCoreAsync()
        {
            m_isCharging.SetValue(true);
            PlayerSaveResult result = await m_lives.LoseOneAsync(CancellationToken.None);
            if (m_disposed)
            {
                return;
            }

            if (result != PlayerSaveResult.Saved)
            {
                Debug.LogWarning("Lives: this attempt's life loss could not be saved (" + result + "); no life was taken.");
            }

            m_isCharging.SetValue(false);
        }
    }
}
