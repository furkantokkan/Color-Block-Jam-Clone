using System;
using System.Collections;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using ColorBlockJam.Player;
using ColorBlockJam.UI.Settings;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace ColorBlockJam.Tests.SharedUI
{
    /// <summary>
    /// Proves the shared Settings popup presenter of design 8 with a fake view and a fake settings port built on
    /// ReactiveProperty (contract scenarios 2, 3, 6 and 8): committed values, flip-then-commit, failure restore,
    /// pending taps, host requests and the HOME button a host can show.
    /// </summary>
    /// <remarks>
    /// Save results are awaited through <see cref="SettingsPopupPresenter.GetSave"/> with a frame pump, so the tests
    /// hold whether the continuation runs inline or through the Editor's synchronization context.
    /// </remarks>
    public sealed class SettingsPopupPresenterTests
    {
        private const float k_timeoutSeconds = 2f;

        private FakePlayerSettings m_settings;
        private FakeSettingsPopupView m_view;
        private SettingsPopupPresenter m_presenter;
        private int m_closeRequests;
        private int m_homeRequests;

        [SetUp]
        public void SetUp()
        {
            m_settings = new FakePlayerSettings(music: true, sound: false, vibration: true);
            m_view = new FakeSettingsPopupView();
            m_presenter = new SettingsPopupPresenter(m_settings);
            m_closeRequests = 0;
            m_homeRequests = 0;
            m_presenter.CloseRequested += () => m_closeRequests++;
            m_presenter.HomeRequested += () => m_homeRequests++;
        }

        [TearDown]
        public void TearDown()
        {
            m_presenter.Dispose();
            m_settings.Dispose();
        }

        [Test]
        public void Bind_ShowsTheCommittedValues_StartsHidden_AndHidesHome()
        {
            m_presenter.Bind(m_view, CancellationToken.None);

            Assert.That(m_view.Toggle(PlayerSettingId.Music), Is.True);
            Assert.That(m_view.Toggle(PlayerSettingId.Sound), Is.False);
            Assert.That(m_view.Toggle(PlayerSettingId.Vibration), Is.True);
            Assert.That(m_view.Visible, Is.False);
            Assert.That(m_view.HomeVisible, Is.False, "HOME stays hidden unless a host shows it");
            Assert.That(m_view.Message, Is.Empty);
        }

        [Test]
        public void CommittedChange_UpdatesTheToggle()
        {
            m_presenter.Bind(m_view, CancellationToken.None);

            m_settings.Property(PlayerSettingId.Sound).Value = true;

            Assert.That(m_view.Toggle(PlayerSettingId.Sound), Is.True);
        }

        [Test]
        public void Tap_FlipsTheShownStateAtOnce_AndSavesTheNewValue()
        {
            m_presenter.Bind(m_view, CancellationToken.None);

            m_view.PressToggle(PlayerSettingId.Music);

            Assert.That(m_view.Toggle(PlayerSettingId.Music), Is.False, "flipped before the save returns");
            Assert.That(m_settings.Calls.Count, Is.EqualTo(1));
            Assert.That(m_settings.Calls[0].Id, Is.EqualTo(PlayerSettingId.Music));
            Assert.That(m_settings.Calls[0].Value, Is.False);
            Assert.That(m_presenter.IsSavePending(PlayerSettingId.Music), Is.True);
            Assert.That(m_settings.Property(PlayerSettingId.Music).Value, Is.True, "nothing committed yet");
        }

        [UnityTest]
        public IEnumerator Tap_Saved_KeepsTheNewValueWithoutAMessage()
        {
            m_presenter.Bind(m_view, CancellationToken.None);
            m_view.PressToggle(PlayerSettingId.Sound);

            m_settings.CompleteNext(PlayerSaveResult.Saved);
            yield return WaitForSave(PlayerSettingId.Sound);

            Assert.That(m_presenter.IsSavePending(PlayerSettingId.Sound), Is.False);
            Assert.That(m_view.Toggle(PlayerSettingId.Sound), Is.True);
            Assert.That(m_settings.Property(PlayerSettingId.Sound).Value, Is.True);
            Assert.That(m_view.Message, Is.Empty);
        }

        [UnityTest]
        public IEnumerator Tap_SaveFailed_RestoresTheCommittedValueAndShowsAMessage()
        {
            m_presenter.Bind(m_view, CancellationToken.None);
            m_view.PressToggle(PlayerSettingId.Vibration);
            Assert.That(m_view.Toggle(PlayerSettingId.Vibration), Is.False);

            m_settings.CompleteNext(PlayerSaveResult.SaveFailed);
            yield return WaitForSave(PlayerSettingId.Vibration);

            Assert.That(m_view.Toggle(PlayerSettingId.Vibration), Is.True, "restored to the committed value");
            Assert.That(m_view.Message, Is.EqualTo(SettingsPopupPresenter.SaveFailedMessage));
            Assert.That(m_presenter.IsSavePending(PlayerSettingId.Vibration), Is.False);
        }

        [UnityTest]
        public IEnumerator Tap_Cancelled_RestoresTheCommittedValueAndShowsAMessage()
        {
            m_presenter.Bind(m_view, CancellationToken.None);
            m_view.PressToggle(PlayerSettingId.Music);

            m_settings.CompleteNext(PlayerSaveResult.Cancelled);
            yield return WaitForSave(PlayerSettingId.Music);

            Assert.That(m_view.Toggle(PlayerSettingId.Music), Is.True);
            Assert.That(m_view.Message, Is.EqualTo(SettingsPopupPresenter.SaveFailedMessage));
        }

        [UnityTest]
        public IEnumerator Tap_SaveThrows_LogsRestoresAndShowsAMessage()
        {
            m_presenter.Bind(m_view, CancellationToken.None);
            m_view.PressToggle(PlayerSettingId.Music);

            LogAssert.Expect(LogType.Exception, new Regex("disk gone"));
            m_settings.FailNext(new InvalidOperationException("disk gone"));
            yield return WaitForSave(PlayerSettingId.Music);

            Assert.That(m_view.Toggle(PlayerSettingId.Music), Is.True);
            Assert.That(m_view.Message, Is.EqualTo(SettingsPopupPresenter.SaveFailedMessage));
        }

        [UnityTest]
        public IEnumerator Tap_WhileItsSaveIsPending_IsIgnored_ThenAcceptedAgain()
        {
            m_presenter.Bind(m_view, CancellationToken.None);
            m_view.PressToggle(PlayerSettingId.Music);

            m_view.PressToggle(PlayerSettingId.Music);
            m_view.PressToggle(PlayerSettingId.Music);

            Assert.That(m_settings.Calls.Count, Is.EqualTo(1), "one save per pending toggle");
            Assert.That(m_view.Toggle(PlayerSettingId.Music), Is.False);

            m_settings.CompleteNext(PlayerSaveResult.Saved);
            yield return WaitForSave(PlayerSettingId.Music);
            m_view.PressToggle(PlayerSettingId.Music);

            Assert.That(m_settings.Calls.Count, Is.EqualTo(2));
            Assert.That(m_settings.Calls[1].Value, Is.True);
            Assert.That(m_view.Toggle(PlayerSettingId.Music), Is.True);
        }

        [Test]
        public void PendingSaveOnOneToggle_DoesNotBlockAnother()
        {
            m_presenter.Bind(m_view, CancellationToken.None);

            m_view.PressToggle(PlayerSettingId.Music);
            m_view.PressToggle(PlayerSettingId.Sound);

            Assert.That(m_settings.Calls.Count, Is.EqualTo(2));
            Assert.That(m_view.Toggle(PlayerSettingId.Sound), Is.True);
        }

        [Test]
        public void CommittedChangeWhileItsSaveIsPending_LeavesTheToggleToTheResult()
        {
            m_presenter.Bind(m_view, CancellationToken.None);
            m_view.PressToggle(PlayerSettingId.Music);
            int toggleWrites = m_view.ToggleWrites;

            // A publish while the tap is pending is not shown; the save result reconciles the toggle.
            m_settings.Property(PlayerSettingId.Music).Value = false;

            Assert.That(m_view.ToggleWrites, Is.EqualTo(toggleWrites));
            Assert.That(m_view.Toggle(PlayerSettingId.Music), Is.False);
        }

        [Test]
        public void Tap_ClearsAnEarlierMessage()
        {
            m_presenter.Bind(m_view, CancellationToken.None);
            m_view.SetMessage(SettingsPopupPresenter.SaveFailedMessage);

            m_view.PressToggle(PlayerSettingId.Sound);

            Assert.That(m_view.Message, Is.Empty);
        }

        [UnityTest]
        public IEnumerator Unbind_CancelsThePendingSave_AndIgnoresItsResult()
        {
            m_presenter.Bind(m_view, CancellationToken.None);
            m_view.PressToggle(PlayerSettingId.Music);
            CancellationToken saveToken = m_settings.Calls[0].Token;
            int toggleWrites = m_view.ToggleWrites;

            m_presenter.Unbind();
            yield return null;

            Assert.That(saveToken.IsCancellationRequested, Is.True);
            Assert.That(m_view.ToggleWrites, Is.EqualTo(toggleWrites), "the late result touches no view");
            Assert.That(m_view.Message, Is.Empty);
            Assert.That(m_view.HasListeners, Is.False);
            Assert.That(m_presenter.IsBound, Is.False);
        }

        [Test]
        public void Bind_LinksSavesToTheHostToken()
        {
            using var host = new CancellationTokenSource();
            m_presenter.Bind(m_view, host.Token);
            m_view.PressToggle(PlayerSettingId.Sound);

            host.Cancel();

            Assert.That(m_settings.Calls[0].Token.IsCancellationRequested, Is.True);
        }

        [Test]
        public void Unbind_DisposesSubscriptions()
        {
            m_presenter.Bind(m_view, CancellationToken.None);
            int toggleWrites = m_view.ToggleWrites;

            m_presenter.Unbind();
            m_settings.Property(PlayerSettingId.Sound).Value = true;
            m_view.PressToggle(PlayerSettingId.Sound);

            Assert.That(m_view.ToggleWrites, Is.EqualTo(toggleWrites));
            Assert.That(m_settings.Calls, Is.Empty);
        }

        [Test]
        public void Rebind_AfterAChange_ShowsTheSavedValues()
        {
            m_presenter.Bind(m_view, CancellationToken.None);
            m_presenter.Unbind();
            m_settings.Property(PlayerSettingId.Vibration).Value = false;
            var reopened = new FakeSettingsPopupView();

            m_presenter.Bind(reopened, CancellationToken.None);

            Assert.That(reopened.Toggle(PlayerSettingId.Music), Is.True);
            Assert.That(reopened.Toggle(PlayerSettingId.Sound), Is.False);
            Assert.That(reopened.Toggle(PlayerSettingId.Vibration), Is.False);
        }

        [Test]
        public void Bind_Twice_Throws()
        {
            m_presenter.Bind(m_view, CancellationToken.None);

            Assert.Throws<InvalidOperationException>(() => m_presenter.Bind(new FakeSettingsPopupView(), CancellationToken.None));
        }

        [Test]
        public void ShowAndHide_SetTheViewAndClearTheMessage()
        {
            m_presenter.Bind(m_view, CancellationToken.None);

            m_presenter.Show();
            Assert.That(m_view.Visible, Is.True);
            Assert.That(m_presenter.IsVisible, Is.True);

            m_view.SetMessage(SettingsPopupPresenter.SaveFailedMessage);
            m_presenter.Hide();
            Assert.That(m_view.Visible, Is.False);
            Assert.That(m_view.Message, Is.Empty);

            m_view.SetMessage(SettingsPopupPresenter.SaveFailedMessage);
            m_presenter.Show();
            Assert.That(m_view.Message, Is.Empty, "reopening clears an old message");
        }

        [Test]
        public void CloseFromTheView_RaisesCloseRequested_AndLeavesVisibilityToTheHost()
        {
            m_presenter.Bind(m_view, CancellationToken.None);
            m_presenter.Show();

            // The close button and the scrim both raise the view's CloseRequested.
            m_view.RequestClose();

            Assert.That(m_closeRequests, Is.EqualTo(1));
            Assert.That(m_view.Visible, Is.True, "the host hides it (Hub: through the navigator)");
        }

        [Test]
        public void HostShowsHome_ThenHomePress_RaisesHomeRequested()
        {
            m_presenter.Bind(m_view, CancellationToken.None);

            m_presenter.SetHomeVisible(true);
            Assert.That(m_view.HomeVisible, Is.True);
            Assert.That(m_presenter.IsHomeVisible, Is.True);

            m_view.PressHome();

            Assert.That(m_homeRequests, Is.EqualTo(1));
            Assert.That(m_closeRequests, Is.Zero);
            Assert.That(m_settings.Calls, Is.Empty, "HOME saves nothing");
        }

        [Test]
        public void HomeVisibility_SetBeforeBind_AppliesOnBind_AndSurvivesRebind()
        {
            m_presenter.SetHomeVisible(true);
            m_presenter.Bind(m_view, CancellationToken.None);
            Assert.That(m_view.HomeVisible, Is.True);

            m_presenter.Unbind();
            var reopened = new FakeSettingsPopupView();
            m_presenter.Bind(reopened, CancellationToken.None);

            Assert.That(reopened.HomeVisible, Is.True);
        }

        [Test]
        public void HomePress_WhileHidden_IsIgnored()
        {
            m_presenter.Bind(m_view, CancellationToken.None);

            m_view.PressHome();

            Assert.That(m_homeRequests, Is.Zero);
        }

        [Test]
        public void Dispose_UnbindsAndDropsHostListeners()
        {
            m_presenter.Bind(m_view, CancellationToken.None);

            m_presenter.Dispose();
            m_view.RequestClose();

            Assert.That(m_view.HasListeners, Is.False);
            Assert.That(m_closeRequests, Is.Zero);
            Assert.Throws<ObjectDisposedException>(() => m_presenter.Bind(new FakeSettingsPopupView(), CancellationToken.None));
        }

        private IEnumerator WaitForSave(PlayerSettingId id)
        {
            Task save = m_presenter.GetSave(id);
            float deadline = Time.realtimeSinceStartup + k_timeoutSeconds;
            while ((!save.IsCompleted || m_presenter.IsSavePending(id)) && Time.realtimeSinceStartup < deadline)
            {
                yield return null;
            }

            Assert.That(m_presenter.IsSavePending(id), Is.False, "the save result never arrived");
        }
    }
}
