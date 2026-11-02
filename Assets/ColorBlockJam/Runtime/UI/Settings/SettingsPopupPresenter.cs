using System;
using System.Threading;
using System.Threading.Tasks;
using ColorBlockJam.Player;
using ColorBlockJam.UI;
using Onity.Reactive;
using UnityEngine;

namespace ColorBlockJam.UI.Settings
{
    /// <summary>
    /// Drives the shared Settings popup from the committed player settings: a tap flips the shown toggle at once and
    /// commits it through <see cref="IPlayerSettings.SetAsync"/>; a failed save restores the committed value with a
    /// short message; close and HOME presses go to the host as requests (design 8, AC-2.2, AC-2.5, DEC-15, DEC-136).
    /// </summary>
    /// <remarks>
    /// Owner: the host scene's container (AsSingle); the host binds it to a view with its own lifetime token and
    /// unbinds it when the view goes away. It knows no navigator or scene: the Hub's SettingsOverlay closes its
    /// overlay, and the Game's PauseMenuHost maps <see cref="CloseRequested"/> to Resume and
    /// <see cref="HomeRequested"/> to Home (DEC-154). Every subscription goes into one CompositeDisposable cleared by
    /// <see cref="Unbind"/>. Each save is one owned task
    /// per toggle, stored until it completes; its token is linked to the host token passed to <see cref="Bind"/> and
    /// is cancelled by <see cref="Unbind"/>, and a result from an earlier bind is ignored. A toggle ignores taps while
    /// its own save is pending.
    /// </remarks>
    public sealed class SettingsPopupPresenter : IDisposable
    {
        public const string SaveFailedMessage = PopupMessages.SaveFailed;

        // One slot per PlayerSettingId value (Music, Sound, Vibration); the count lives on SettingsPopupView, the
        // other keeper of one slot per id, so only one literal is updated together with the enum.
        private const int k_settingCount = SettingsPopupView.SettingCount;

        private readonly IPlayerSettings m_settings;
        private readonly CompositeDisposable m_subscriptions = new CompositeDisposable();
        private readonly Observer<bool>[] m_onCommittedChanged = new Observer<bool>[k_settingCount];
        private readonly bool[] m_shown = new bool[k_settingCount];
        private readonly bool[] m_pending = new bool[k_settingCount];
        private readonly Task[] m_saves = new Task[k_settingCount];
        private readonly Action<PlayerSettingId> m_onTogglePressed;
        private readonly Action m_onCloseRequested;
        private readonly Action m_onHomePressed;

        private ISettingsPopupView m_view;
        private CancellationTokenSource m_bindCancellation;
        private int m_bindGeneration;
        private bool m_isVisible;
        private bool m_isHomeVisible;
        private bool m_disposed;

        public SettingsPopupPresenter(IPlayerSettings settings)
        {
            m_settings = settings ?? throw new ArgumentNullException(nameof(settings));
            m_onTogglePressed = OnTogglePressed;
            m_onCloseRequested = OnCloseRequested;
            m_onHomePressed = OnHomePressed;
            for (int i = 0; i < k_settingCount; i++)
            {
                int index = i;
                m_onCommittedChanged[i] = value => OnCommittedChanged(index, value);
            }
        }

        /// <summary>Raised by the close button and by a tap outside the popup; the host decides what closing means.</summary>
        public event Action CloseRequested;

        /// <summary>Raised by the HOME button, which only a host that shows it can receive (reference 20).</summary>
        public event Action HomeRequested;

        public bool IsBound => m_view != null;

        /// <summary>True between <see cref="Show"/> and <see cref="Hide"/>.</summary>
        public bool IsVisible => m_isVisible;

        /// <summary>True while the host shows the HOME button; false by default.</summary>
        public bool IsHomeVisible => m_isHomeVisible;

        /// <summary>True from a tap until that toggle's save result arrives.</summary>
        public bool IsSavePending(PlayerSettingId id)
        {
            return m_pending[ToIndex(id)];
        }

        /// <summary>The owned save task of this toggle's last tap; a completed task when none is running.</summary>
        public Task GetSave(PlayerSettingId id)
        {
            return m_saves[ToIndex(id)] ?? Task.CompletedTask;
        }

        /// <summary>Binds the view; the popup starts hidden until the host shows it.</summary>
        /// <param name="hostToken">The host's lifetime token; every save started by this bind is linked to it.</param>
        public void Bind(ISettingsPopupView view, CancellationToken hostToken)
        {
            if (m_disposed)
            {
                throw new ObjectDisposedException(nameof(SettingsPopupPresenter));
            }

            if (view == null)
            {
                throw new ArgumentNullException(nameof(view));
            }

            if (m_view != null)
            {
                throw new InvalidOperationException("Settings popup presenter: already bound; unbind first.");
            }

            m_view = view;
            m_bindGeneration++;
            m_bindCancellation = CancellationTokenSource.CreateLinkedTokenSource(hostToken);
            Array.Clear(m_pending, 0, k_settingCount);

            m_view.TogglePressed += m_onTogglePressed;
            m_view.CloseRequested += m_onCloseRequested;
            m_view.HomePressed += m_onHomePressed;
            m_view.SetMessage(string.Empty);
            m_view.SetHomeVisible(m_isHomeVisible);
            m_view.SetVisible(m_isVisible);

            // Each subscription emits the committed value at once, then only on a committed change.
            for (int i = 0; i < k_settingCount; i++)
            {
                m_subscriptions.Add(m_settings.Get((PlayerSettingId)i).Subscribe(m_onCommittedChanged[i]));
            }
        }

        /// <summary>Cancels pending saves, disposes every subscription and detaches the view.</summary>
        public void Unbind()
        {
            if (m_view == null)
            {
                return;
            }

            m_view.TogglePressed -= m_onTogglePressed;
            m_view.CloseRequested -= m_onCloseRequested;
            m_view.HomePressed -= m_onHomePressed;
            m_view = null;
            m_subscriptions.Clear();
            Array.Clear(m_pending, 0, k_settingCount);
            Array.Clear(m_saves, 0, k_settingCount);

            // Detached first: a save that completes inside Cancel() finds no view and changes nothing.
            CancellationTokenSource cancellation = m_bindCancellation;
            m_bindCancellation = null;
            cancellation.Cancel();
            cancellation.Dispose();
        }

        /// <summary>Shows the popup and clears an earlier message.</summary>
        public void Show()
        {
            m_isVisible = true;
            if (m_view == null)
            {
                return;
            }

            m_view.SetMessage(string.Empty);
            m_view.SetVisible(true);
        }

        /// <summary>Hides the popup; a pending save still completes.</summary>
        public void Hide()
        {
            m_isVisible = false;
            if (m_view == null)
            {
                return;
            }

            m_view.SetVisible(false);
            m_view.SetMessage(string.Empty);
        }

        /// <summary>Shows or hides the HOME button; kept across binds. The Hub keeps it hidden (DEC-135).</summary>
        public void SetHomeVisible(bool visible)
        {
            m_isHomeVisible = visible;
            m_view?.SetHomeVisible(visible);
        }

        public void Dispose()
        {
            if (m_disposed)
            {
                return;
            }

            Unbind();
            m_subscriptions.Dispose();
            CloseRequested = null;
            HomeRequested = null;
            m_disposed = true;
        }

        private void OnCommittedChanged(int index, bool value)
        {
            // While a save is pending the toggle keeps the tapped value; the result reconciles it.
            if (m_pending[index])
            {
                return;
            }

            SetShown(index, value);
        }

        private void OnTogglePressed(PlayerSettingId id)
        {
            int index = (int)id;
            if (m_view == null || index < 0 || index >= k_settingCount || m_pending[index])
            {
                return;
            }

            bool next = !m_shown[index];
            SetShown(index, next);
            m_view.SetMessage(string.Empty);
            m_pending[index] = true;
            Task save = SaveAsync(id, next, m_bindGeneration, m_bindCancellation.Token);

            // A save that completed synchronously already cleared its slot.
            m_saves[index] = save.IsCompleted ? null : save;
        }

        private void OnCloseRequested()
        {
            CloseRequested?.Invoke();
        }

        private void OnHomePressed()
        {
            // A hidden button cannot be pressed; this also ignores a stale click that lands after the host hid it.
            if (m_isHomeVisible)
            {
                HomeRequested?.Invoke();
            }
        }

        /// <summary>
        /// Owner: this presenter, which stores the task per toggle. Stops: when the save service returns, or when
        /// <paramref name="cancellationToken"/> (this bind, linked to the host token) is cancelled.
        /// </summary>
        private async Task SaveAsync(PlayerSettingId id, bool value, int generation, CancellationToken cancellationToken)
        {
            PlayerSaveResult result;
            try
            {
                result = await m_settings.SetAsync(id, value, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                result = PlayerSaveResult.Cancelled;
            }
            catch (Exception exception)
            {
                // The task is stored, never awaited, so an unexpected fault is logged here instead of being lost.
                Debug.LogException(exception);
                result = PlayerSaveResult.SaveFailed;
            }

            OnSaveCompleted(id, result, generation);
        }

        private void OnSaveCompleted(PlayerSettingId id, PlayerSaveResult result, int generation)
        {
            int index = (int)id;
            if (generation != m_bindGeneration || m_view == null)
            {
                return;
            }

            m_saves[index] = null;
            m_pending[index] = false;

            // Saved: the committed value already equals the tap. Otherwise the committed value never changed.
            SetShown(index, m_settings.Get(id).Value);
            if (result != PlayerSaveResult.Saved)
            {
                m_view.SetMessage(SaveFailedMessage);
            }
        }

        private void SetShown(int index, bool value)
        {
            m_shown[index] = value;
            m_view.SetToggle((PlayerSettingId)index, value);
        }

        private static int ToIndex(PlayerSettingId id)
        {
            int index = (int)id;
            if (index < 0 || index >= k_settingCount)
            {
                throw new ArgumentOutOfRangeException(nameof(id));
            }

            return index;
        }
    }
}
