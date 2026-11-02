using ColorBlockJam.Domain.Session;
using ColorBlockJam.Gameplay.Hud;
using ColorBlockJam.Gameplay.Hud.Menu;
using ColorBlockJam.Gameplay.Hud.Navigation;
using ColorBlockJam.Gameplay.Hud.TopBar;
using ColorBlockJam.Runtime.Currencies;
using NUnit.Framework;
using UnityEngine;

namespace ColorBlockJam.Tests.Gameplay.Hud
{
    /// <summary>
    /// The top bar presenter (Game HUD design 4; contract AC-2, AC-4, AC-9, AC-10, AC-13) with a fake view: the Level
    /// number it is given, the compact coin amount written only when the committed balance changes, the Retry and Pause
    /// enabled matrix written from state and flag changes, button clicks forwarded to the menu flow, and nothing after
    /// Unbind or Dispose.
    /// </summary>
    public sealed class TopBarPresenterTests
    {
        private FakeSessionControl m_session;
        private FakeGameMenu m_menu;
        private FakeWallet m_wallet;
        private FakeTopBarView m_view;
        private CurrencyDefinition m_coin;
        private TopBarPresenter m_presenter;

        // NUnit reuses the fixture instance, so every collaborator is rebuilt here.
        [SetUp]
        public void SetUp()
        {
            m_session = new FakeSessionControl(SessionState.Home);
            m_menu = new FakeGameMenu();
            m_wallet = new FakeWallet(1250);
            m_view = new FakeTopBarView();
            m_coin = ScriptableObject.CreateInstance<CurrencyDefinition>();
            m_presenter = new TopBarPresenter(m_session, m_menu, m_wallet, m_coin);
        }

        [TearDown]
        public void TearDown()
        {
            m_presenter.Dispose();
            Object.DestroyImmediate(m_coin);
        }

        [Test]
        public void TheLevelNumberGivenBeforeBindShowsOnBindAndLaterNumbersFollow()
        {
            m_presenter.ShowLevel("29");

            m_presenter.Bind(m_view);
            Assert.That(m_view.LevelNumber, Is.EqualTo("29"));

            m_presenter.ShowLevel("123");
            Assert.That(m_view.LevelNumber, Is.EqualTo("123"), "a three-digit number is written as is");
        }

        [Test]
        public void TheCoinAmountIsTheCompactBalanceWrittenOnlyWhenItChanges()
        {
            m_presenter.Bind(m_view);

            Assert.That(m_view.CoinWrites, Is.EqualTo(1));
            Assert.That(m_view.CoinAmount, Is.EqualTo(CurrencyText.FormatCompact(1250)));
            Assert.That(m_view.HideCoinCount, Is.Zero);

            m_wallet.SetBalance(1250);
            m_session.SetState(SessionState.Playing);
            m_session.SetState(SessionState.Paused, PauseReason.User);
            Assert.That(m_view.CoinWrites, Is.EqualTo(1), "state changes and an unchanged balance write nothing");

            m_wallet.SetBalance(1260);
            Assert.That(m_view.CoinWrites, Is.EqualTo(2));
            Assert.That(m_view.CoinAmount, Is.EqualTo(CurrencyText.FormatCompact(1260)));

            m_wallet.SetBalance(2500000);
            Assert.That(m_view.CoinAmount, Is.EqualTo(CurrencyText.FormatCompact(2500000)));
        }

        [Test]
        public void WithoutACoinCurrencyTheHolderIsHidden()
        {
            m_presenter.Dispose();
            m_presenter = new TopBarPresenter(m_session, m_menu, m_wallet, null);

            m_presenter.Bind(m_view);

            Assert.That(m_view.HideCoinCount, Is.EqualTo(1));
            Assert.That(m_view.CoinWrites, Is.Zero);
            Assert.That(m_wallet.GetBalanceCalls, Is.Zero);
        }

        [TestCase(SessionState.Home, false, false, false)]
        [TestCase(SessionState.Playing, false, false, true)]
        [TestCase(SessionState.Playing, true, false, false)]
        [TestCase(SessionState.Playing, false, true, false)]
        [TestCase(SessionState.Paused, false, false, false)]
        [TestCase(SessionState.Paused, false, true, false)]
        [TestCase(SessionState.Won, false, false, false)]
        [TestCase(SessionState.Failed, false, false, true)]
        public void RetryIsEnabledOnlyInPlayingOutsideTheOpeningAndAPendingWriteOrInFailed(SessionState state,
            bool isOpening, bool isWritePending, bool expected)
        {
            Assert.That(TopBarPresenter.IsRetryEnabled(state, isOpening, isWritePending), Is.EqualTo(expected));

            m_session.SetState(state, PauseReason.User);
            m_session.SetOpening(isOpening);
            m_session.SetWritePending(isWritePending);
            m_presenter.Bind(m_view);

            Assert.That(m_view.RetryEnabled, Is.EqualTo(expected), "the view follows the rule");
        }

        [TestCase(SessionState.Home, false, false)]
        [TestCase(SessionState.Playing, false, true)]
        [TestCase(SessionState.Playing, true, false)]
        [TestCase(SessionState.Paused, false, false)]
        [TestCase(SessionState.Paused, true, false)]
        [TestCase(SessionState.Won, false, true)]
        [TestCase(SessionState.Failed, false, false)]
        [TestCase(SessionState.Failed, true, false)]
        public void PauseIsEnabledInPlayingAndWonWithoutAPendingWrite(SessionState state, bool isWritePending,
            bool expected)
        {
            Assert.That(TopBarPresenter.IsPauseEnabled(state, isWritePending), Is.EqualTo(expected));

            m_session.SetState(state, PauseReason.User);
            m_session.SetWritePending(isWritePending);
            m_presenter.Bind(m_view);

            Assert.That(m_view.PauseEnabled, Is.EqualTo(expected));
        }

        /// <summary>
        /// For every state and flag the enabled look equals what the menu flow does with the press, so an enabled
        /// button always acts and a disabled one would never have acted (review minor D).
        /// </summary>
        [Test]
        public void TheEnabledMatrixMatchesWhatTheMenuFlowDoes(
            [Values(SessionState.Home, SessionState.Playing, SessionState.Paused, SessionState.Won, SessionState.Failed)]
            SessionState state,
            [Values(false, true)] bool isOpening,
            [Values(false, true)] bool isWritePending)
        {
            Assert.That(ActsOnPress(state, isOpening, isWritePending, retry: true),
                Is.EqualTo(TopBarPresenter.IsRetryEnabled(state, isOpening, isWritePending)), "Retry");
            Assert.That(ActsOnPress(state, isOpening, isWritePending, retry: false),
                Is.EqualTo(TopBarPresenter.IsPauseEnabled(state, isWritePending)), "Pause");
        }

        [Test]
        public void TheButtonsFollowStateOpeningAndWriteChanges()
        {
            m_presenter.Bind(m_view);
            Assert.That(m_view.RetryEnabled, Is.False, "Home");
            Assert.That(m_view.PauseEnabled, Is.False, "Home");

            m_session.SetOpening(true);
            m_session.SetState(SessionState.Playing);
            Assert.That(m_view.RetryEnabled, Is.False, "the opening hold keeps Retry off");
            Assert.That(m_view.PauseEnabled, Is.True, "Pause works in the opening hold");

            m_session.SetOpening(false);
            Assert.That(m_view.RetryEnabled, Is.True);

            m_session.SetWritePending(true);
            Assert.That(m_view.RetryEnabled, Is.False, "a pending booster write keeps Retry off");
            Assert.That(m_view.PauseEnabled, Is.False, "and Pause, which the flow ignores then");

            m_session.SetWritePending(false);
            Assert.That(m_view.PauseEnabled, Is.True);
            m_session.SetState(SessionState.Won);
            Assert.That(m_view.RetryEnabled, Is.False);
            Assert.That(m_view.PauseEnabled, Is.True);
        }

        [Test]
        public void ButtonClicksGoToTheMenuFlow()
        {
            m_presenter.Bind(m_view);

            m_view.ClickPause();
            m_view.ClickRetry();
            m_view.ClickRetry();

            Assert.That(m_menu.PauseRequests, Is.EqualTo(1));
            Assert.That(m_menu.RestartRequests, Is.EqualTo(2));
        }

        [Test]
        public void UnbindAndDisposeStopEveryWrite()
        {
            m_presenter.Bind(m_view);
            int enabledWrites = m_view.EnabledWrites;

            m_presenter.Unbind();
            m_wallet.SetBalance(7);
            m_session.SetState(SessionState.Playing);
            m_view.ClickPause();
            m_view.ClickRetry();

            Assert.That(m_view.CoinWrites, Is.EqualTo(1));
            Assert.That(m_view.EnabledWrites, Is.EqualTo(enabledWrites));
            Assert.That(m_menu.PauseRequests + m_menu.RestartRequests, Is.Zero);

            FakeTopBarView second = new FakeTopBarView();
            m_presenter.Bind(second);
            Assert.That(second.CoinAmount, Is.EqualTo(CurrencyText.FormatCompact(7)), "a rebind shows the current balance");

            m_presenter.Dispose();
            m_wallet.SetBalance(8);
            m_session.SetState(SessionState.Paused, PauseReason.User);
            second.ClickPause();

            Assert.That(second.CoinWrites, Is.EqualTo(1));
            Assert.That(m_menu.PauseRequests, Is.Zero);
            Assert.That(m_presenter.IsBound, Is.False);
        }

        // One fresh flow over fakes in the given state: true when the press showed an overlay.
        private static bool ActsOnPress(SessionState state, bool isOpening, bool isWritePending, bool retry)
        {
            FakeSessionControl session = new FakeSessionControl(state);
            session.SetOpening(isOpening);
            session.SetWritePending(isWritePending);
            FakePauseMenu menu = new FakePauseMenu(session);
            FakeRestartConfirm confirm = new FakeRestartConfirm(session);
            AttemptLevelNumber levelNumber = new AttemptLevelNumber();
            levelNumber.Set(1);
            using (GameExitNavigator exit = new GameExitNavigator(session, new FakeHubTransition()))
            using (GameMenuFlow flow = new GameMenuFlow(session, menu, confirm, new FakeOverlayBackInput(), exit,
                       levelNumber))
            {
                if (retry)
                {
                    flow.RequestRestart();
                    return confirm.ShowCount > 0;
                }

                flow.RequestPause();
                return menu.ShowCount > 0;
            }
        }
    }
}
