using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using ColorBlockJam.Player;
using ColorBlockJam.UI.Settings;
using Onity.Reactive;

namespace ColorBlockJam.Tests.SharedUI
{
    /// <summary>One recorded <see cref="IPlayerSettings.SetAsync"/> call and the completion the test controls.</summary>
    internal readonly struct SettingsSaveCall
    {
        public readonly PlayerSettingId Id;
        public readonly bool Value;
        public readonly CancellationToken Token;
        public readonly TaskCompletionSource<PlayerSaveResult> Completion;

        public SettingsSaveCall(PlayerSettingId id, bool value, CancellationToken token,
            TaskCompletionSource<PlayerSaveResult> completion)
        {
            Id = id;
            Value = value;
            Token = token;
            Completion = completion;
        }
    }

    /// <summary>
    /// Settings port on ReactiveProperty: SetAsync waits until the test completes it; Saved publishes the value before
    /// the result returns, as the real service does (design 4.4 rule 2).
    /// </summary>
    internal sealed class FakePlayerSettings : IPlayerSettings, IDisposable
    {
        private readonly ReactiveProperty<bool>[] m_values;
        private int m_nextToComplete;

        public FakePlayerSettings(bool music, bool sound, bool vibration)
        {
            m_values = new[]
            {
                new ReactiveProperty<bool>(music),
                new ReactiveProperty<bool>(sound),
                new ReactiveProperty<bool>(vibration)
            };
        }

        public List<SettingsSaveCall> Calls { get; } = new List<SettingsSaveCall>();

        public ReactiveProperty<bool> Property(PlayerSettingId id)
        {
            return m_values[(int)id];
        }

        public IReadOnlyReactiveProperty<bool> Get(PlayerSettingId id)
        {
            return m_values[(int)id];
        }

        public Task<PlayerSaveResult> SetAsync(PlayerSettingId id, bool value, CancellationToken cancellationToken)
        {
            var completion = new TaskCompletionSource<PlayerSaveResult>();
            Calls.Add(new SettingsSaveCall(id, value, cancellationToken, completion));
            cancellationToken.Register(() => completion.TrySetResult(PlayerSaveResult.Cancelled));
            return completion.Task;
        }

        public void CompleteNext(PlayerSaveResult result)
        {
            SettingsSaveCall call = Calls[m_nextToComplete++];
            if (result == PlayerSaveResult.Saved)
            {
                m_values[(int)call.Id].Value = call.Value;
            }

            call.Completion.TrySetResult(result);
        }

        public void FailNext(Exception exception)
        {
            Calls[m_nextToComplete++].Completion.TrySetException(exception);
        }

        public void Dispose()
        {
            for (int i = 0; i < m_values.Length; i++)
            {
                m_values[i].Dispose();
            }
        }
    }

    /// <summary>Records what the presenter writes and lets the test raise the view's events.</summary>
    internal sealed class FakeSettingsPopupView : ISettingsPopupView
    {
        private readonly bool[] m_toggles = new bool[3];

        public event Action<PlayerSettingId> TogglePressed;

        public event Action CloseRequested;

        public event Action HomePressed;

        public bool Visible { get; private set; }

        public bool HomeVisible { get; private set; } = true;

        public string Message { get; private set; } = string.Empty;

        public int ToggleWrites { get; private set; }

        public bool HasListeners => TogglePressed != null || CloseRequested != null || HomePressed != null;

        public bool Toggle(PlayerSettingId id)
        {
            return m_toggles[(int)id];
        }

        public void SetVisible(bool visible)
        {
            Visible = visible;
        }

        public void SetToggle(PlayerSettingId id, bool isOn)
        {
            ToggleWrites++;
            m_toggles[(int)id] = isOn;
        }

        public void SetMessage(string text)
        {
            Message = text ?? string.Empty;
        }

        public void SetHomeVisible(bool visible)
        {
            HomeVisible = visible;
        }

        public void PressToggle(PlayerSettingId id)
        {
            TogglePressed?.Invoke(id);
        }

        public void RequestClose()
        {
            CloseRequested?.Invoke();
        }

        public void PressHome()
        {
            HomePressed?.Invoke();
        }
    }
}
