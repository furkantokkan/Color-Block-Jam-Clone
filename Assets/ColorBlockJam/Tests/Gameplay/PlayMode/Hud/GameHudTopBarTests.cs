using System.Collections;
using ColorBlockJam.Domain.Board;
using ColorBlockJam.Domain.Picking;
using ColorBlockJam.Domain.Session;
using ColorBlockJam.Gameplay.Boosters;
using ColorBlockJam.Gameplay.Hud;
using ColorBlockJam.Gameplay.Hud.Menu;
using ColorBlockJam.Gameplay.Hud.TopBar;
using ColorBlockJam.Gameplay.Input;
using ColorBlockJam.Gameplay.Session;
using ColorBlockJam.Gameplay.UI;
using ColorBlockJam.Player;
using ColorBlockJam.Runtime.Boosters;
using ColorBlockJam.Runtime.Currencies;
using ColorBlockJam.Runtime.Levels;
using NUnit.Framework;
using Onity.Unity.SceneFlow;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

namespace ColorBlockJam.Tests.Gameplay.PlayMode.Hud
{
    /// <summary>
    /// The Game HUD top bar, pause menu and Retry confirmation in the real Game scene composition (direct Play of the
    /// project's first level; contract AC-1 to AC-5, AC-7, AC-9, AC-11 to AC-13): every element exists once, the Level
    /// badge and coin holder show the project's values, Booster's timer still follows the countdown, Pause opens the
    /// shared Settings popup with HOME and Close resumes with the time held, Retry's confirmation restarts once, a
    /// background pause keeps the menu without an automatic resume, targeting hides the bar, the scene's pointer filter
    /// vetoes the buttons but not the bar's empty art, and unloading the scene leaves no overlay Back action enabled.
    /// It also covers a real Time Freeze, backgrounding through the scene's ApplicationPauseRelay (from Playing, from
    /// the user's pause menu and from the zero-stock purchase), the coin plus, the pause menu's toggle and placeholder,
    /// and a Hub launch request's level number. Stock a test needs is arranged on the shared save service and put back
    /// in teardown, so no test depends on another suite's spending.
    /// </summary>
    public sealed class GameHudTopBarTests
    {
        private static readonly string[] s_uniqueNames =
        {
            // Booster's timer and freeze counter, now inside the top bar (design 3).
            "timer-band", "timer-holder", "timer-icon", "timer-label",
            "freeze-counter", "freeze-icon", "freeze-bar", "freeze-fill", "freeze-label",
            // The top bar.
            TopBarView.BarName, "level-time", "level-caption", TopBarView.LevelNumberName, TopBarView.LevelNumberBackName,
            "time-caption", TopBarView.RetryButtonName, TopBarView.PauseButtonName, TopBarView.CoinHolderName,
            "coin-icon", TopBarView.CoinAmountName, TopBarView.CoinPlusName,
            // The heavy-text back layers and the frozen timer's ice layer (chief visual C1, C2).
            "level-caption-back", "time-caption-back", TopBarView.TimerLabelBackName, TopBarView.FreezeLabelBackName,
            TopBarView.CoinAmountBackName, "timer-frost",
            // The overlays.
            "settings-screen", "settings-close-button", "home-button",
            RestartConfirmView.ScreenName, RestartConfirmView.ScrimName, RestartConfirmView.TitleName,
            RestartConfirmView.HeartName, RestartConfirmView.PenaltyName, RestartConfirmView.MessageName,
            RestartConfirmView.RetryButtonName, RestartConfirmView.CloseButtonName
        };

        private GameHudScene m_scene;
        private bool m_sceneLoaded;
        private HudStockArrangement m_stock;

        // NUnit reuses the fixture instance, so the per-test scene handle starts empty.
        [SetUp]
        public void SetUp()
        {
            m_scene = null;
            m_sceneLoaded = false;
            m_stock = new HudStockArrangement();
        }

        [UnityTearDown]
        public IEnumerator UnloadGameScene()
        {
            // Stock a test arranged goes back to the project's value; a launch request a failed test left behind must
            // not reach a later Game scene.
            m_stock.Restore();
            OnitySceneTransitionStore.TryConsumeActiveEnterData(out LevelLaunchRequest _);
            if (m_sceneLoaded)
            {
                m_sceneLoaded = false;
                yield return GameHudScene.Unload();
            }

            m_scene = null;
        }

        [UnityTest]
        public IEnumerator EveryTopBarAndOverlayElementExistsOnceAndThePopupsStartHidden()
        {
            yield return Load();
            VisualElement root = m_scene.Root;

            for (int i = 0; i < s_uniqueNames.Length; i++)
            {
                Assert.That(root.Query<VisualElement>(s_uniqueNames[i]).ToList().Count, Is.EqualTo(1), s_uniqueNames[i]);
            }

            Assert.That(root.Q("settings-screen").resolvedStyle.display, Is.EqualTo(DisplayStyle.None));
            Assert.That(root.Q(RestartConfirmView.ScreenName).resolvedStyle.display, Is.EqualTo(DisplayStyle.None));
            Assert.That(m_scene.Flow.Current, Is.EqualTo(GameOverlay.None));
            Assert.That(m_scene.Scope.Container.Resolve<IPauseOverlay>(), Is.TypeOf<PopupPauseOverlay>(),
                "with the HUD a background pause never resumes by itself");
        }

        [UnityTest]
        public IEnumerator TheBadgeShowsTheLevelsPositionAndTheHolderTheCompactCoinBalance()
        {
            yield return Load();
            TopBarView view = m_scene.TopBar.View;
            LevelCatalog levels = m_scene.Scope.Container.Resolve<LevelCatalog>();
            int expected = AttemptLevelNumber.Find(levels, m_scene.Session.Attempt.Level.LevelId);

            Assert.That(expected, Is.GreaterThan(0), "the attempt's level is in the active database");
            Assert.That(m_scene.Scope.Container.Resolve<AttemptLevelNumber>().Value, Is.EqualTo(expected));
            Assert.That(view.LevelNumber, Is.EqualTo(expected.ToString()));
            Assert.That(m_scene.Root.Q<Label>(TopBarView.LevelNumberBackName).text, Is.EqualTo(expected.ToString()),
                "both heavy-text layers");

            CurrencyCatalog catalog = m_scene.Scope.Container.Resolve<CurrencyCatalog>();
            Assert.That(catalog.TryGet(GameTopBar.CoinCurrencyId, out CurrencyDefinition coin), Is.True);
            int balance = m_scene.Scope.Container.Resolve<IWallet>().GetBalance(coin).Value;
            Assert.That(view.CoinAmount, Is.EqualTo(CurrencyText.FormatCompact(balance)));
            Assert.That(m_scene.Root.Q<Label>(TopBarView.CoinAmountBackName).text, Is.EqualTo(view.CoinAmount),
                "both heavy-text layers");
            Assert.That(m_scene.Root.Q<Label>(TopBarView.CoinAmountName).resolvedStyle.color.grayscale,
                Is.GreaterThan(0.9f), "the amount reads white over its dark outline (C1)");
            Assert.That(m_scene.Root.Q(TopBarView.CoinHolderName).resolvedStyle.display, Is.EqualTo(DisplayStyle.Flex));

            Assert.That(m_scene.Session.State.Value, Is.EqualTo(SessionState.Playing));
            Assert.That(view.RetryButton.enabledSelf, Is.True, "Retry works in Playing");
            Assert.That(view.PauseButton.enabledSelf, Is.True);
        }

        [UnityTest]
        public IEnumerator TheBarsControlsAreLaidOutInsideThePanel()
        {
            yield return Load();
            Rect panel = m_scene.Hud.View.Root.worldBound;
            TopBarView view = m_scene.TopBar.View;

            AssertInside(view.RetryButton.worldBound, panel, "Retry");
            AssertInside(view.PauseButton.worldBound, panel, "Pause");
            AssertInside(view.CoinPlus.worldBound, panel, "the coin plus");
            AssertInside(m_scene.Root.Q("level-time").worldBound, panel, "the Level and Time art");
            AssertInside(m_scene.Root.Q<Label>("timer-label").worldBound, panel, "the timer");
            Assert.That(view.RetryButton.worldBound.yMax, Is.LessThanOrEqualTo(view.PauseButton.worldBound.yMin + 0.5f),
                "Retry sits above Pause");
        }

        [UnityTest]
        public IEnumerator BoostersTimerTextStillFollowsTheCountdownAndTheFreezeCounterShowsOnlyWhileFrozen()
        {
            yield return Load();
            LevelSession session = m_scene.Session;
            Label timer = m_scene.Root.Q<Label>("timer-label");
            Assert.That(timer.text, Is.EqualTo(GameHudScene.FormatTime(session.DisplayedSeconds.Value)));

            int start = session.DisplayedSeconds.Value;
            yield return null;
            Assert.That(session.DisplayedSeconds.Value, Is.EqualTo(start), "the countdown waits for a block press (DEC-189)");
            session.Attempt.MarkBlockTouched();
            yield return GameHudScene.WaitUntil(() => session.DisplayedSeconds.Value != start);
            Assert.That(session.DisplayedSeconds.Value, Is.Not.EqualTo(start), "the countdown advanced");
            Assert.That(timer.text, Is.EqualTo(GameHudScene.FormatTime(session.DisplayedSeconds.Value)));
            Assert.That(m_scene.Root.Q<Label>(TopBarView.TimerLabelBackName).text, Is.SameAs(timer.text),
                "the heavy back layer carries the same string Booster's view wrote (C1)");

            VisualElement freeze = m_scene.Root.Q("freeze-counter");
            Assert.That(freeze.resolvedStyle.display, Is.EqualTo(DisplayStyle.None), "hidden while not frozen");
            VisualElement hudRoot = m_scene.Hud.View.Root;
            VisualElement frost = m_scene.Root.Q("timer-frost");
            Assert.That(frost.resolvedStyle.display, Is.EqualTo(DisplayStyle.None), "no ice layer while not frozen");
            hudRoot.AddToClassList(GameplayHudView.FrozenClass);
            yield return null;
            Assert.That(freeze.resolvedStyle.display, Is.EqualTo(DisplayStyle.Flex),
                "the frozen state class still reaches the counter in GameTopBar.uss");
            Assert.That(frost.resolvedStyle.display, Is.EqualTo(DisplayStyle.Flex), "the ice layer shows (C2)");
            Assert.That(frost.resolvedStyle.backgroundColor.grayscale, Is.GreaterThan(0.5f),
                "the ice layer is light, unlike the navy field under it");
            hudRoot.RemoveFromClassList(GameplayHudView.FrozenClass);
            yield return null;
            Assert.That(freeze.resolvedStyle.display, Is.EqualTo(DisplayStyle.None));
            Assert.That(frost.resolvedStyle.display, Is.EqualTo(DisplayStyle.None));
        }

        [UnityTest]
        public IEnumerator ARealTimeFreezeShowsItsCounterBelowTheTimerAndHoldsTheMainTimer()
        {
            yield return Load();
            m_stock.Arrange(m_scene.Resolve<PlayerSaveService>(), BoosterId.TimeFreeze, 1);
            yield return null;
            LevelSession session = m_scene.Session;
            ICountdown countdown = m_scene.Resolve<ICountdown>();
            BoosterUseCoordinator coordinator = m_scene.Resolve<BoosterUseCoordinator>();

            Assert.That(coordinator.Press(BoosterId.TimeFreeze), Is.EqualTo(BoosterPressResult.Submitted));
            yield return GameHudScene.WaitUntil(() => session.IsFrozen.Value && session.State.Value == SessionState.Playing);
            Assert.That(session.IsFrozen.Value, Is.True, "the committed Time Freeze applied in a command phase");
            yield return null;
            yield return null;

            VisualElement freeze = m_scene.Root.Q("freeze-counter");
            Rect timerField = m_scene.Root.Q("timer-holder").worldBound;
            Assert.That(m_scene.Hud.View.HasRootClass(GameplayHudView.FrozenClass), Is.True);
            Assert.That(freeze.resolvedStyle.display, Is.EqualTo(DisplayStyle.Flex), "the freeze counter is visible");
            Assert.That(freeze.worldBound.yMin, Is.GreaterThanOrEqualTo(timerField.yMax - 0.5f),
                "it sits below the Time field");
            Assert.That(freeze.worldBound.center.x, Is.InRange(timerField.xMin, timerField.xMax),
                "and under it, not beside it");
            Assert.That(m_scene.Root.Q("timer-frost").resolvedStyle.display, Is.EqualTo(DisplayStyle.Flex));
            Label freezeLabel = m_scene.Root.Q<Label>(TopBarView.FreezeLabelName);
            Assert.That(m_scene.Root.Q<Label>(TopBarView.FreezeLabelBackName).text, Is.EqualTo(freezeLabel.text),
                "both freeze text layers show the same count");

            float held = countdown.Remaining;
            int shown = session.DisplayedSeconds.Value;
            string timerText = m_scene.Root.Q<Label>("timer-label").text;
            yield return WaitSeconds(0.3f);

            Assert.That(session.IsFrozen.Value, Is.True);
            Assert.That(countdown.Remaining, Is.EqualTo(held), "the main countdown holds while frozen");
            Assert.That(session.DisplayedSeconds.Value, Is.EqualTo(shown));
            Assert.That(m_scene.Root.Q<Label>("timer-label").text, Is.EqualTo(timerText));
        }

        [UnityTest]
        public IEnumerator PauseOpensTheSettingsPopupWithHomeAndCloseResumesWithTheTimeHeld()
        {
            yield return Load();
            LevelSession session = m_scene.Session;
            ICountdown countdown = m_scene.Scope.Container.Resolve<ICountdown>();

            GameHudScene.Click(m_scene.TopBar.View.PauseButton);
            yield return GameHudScene.WaitUntil(() => session.State.Value == SessionState.Paused);

            Assert.That(session.State.Value, Is.EqualTo(SessionState.Paused));
            Assert.That(session.PauseReason, Is.EqualTo(PauseReason.User));
            Assert.That(m_scene.Flow.Current, Is.EqualTo(GameOverlay.PauseMenu));
            Assert.That(GameHudScene.IsOverlayBackEnabled(), Is.True, "overlay Back listens while the menu shows");
            float remaining = countdown.Remaining;
            int shown = session.DisplayedSeconds.Value;
            yield return null;
            yield return null;
            yield return null;

            VisualElement settings = m_scene.Root.Q("settings-screen");
            Assert.That(settings.resolvedStyle.display, Is.EqualTo(DisplayStyle.Flex));
            Assert.That(settings.Q("home-button").resolvedStyle.display, Is.EqualTo(DisplayStyle.Flex), "HOME shows in game");
            Assert.That(countdown.Remaining, Is.EqualTo(remaining), "the countdown holds while paused");
            IUiPointerFilter filter = m_scene.Scope.Container.Resolve<IUiPointerFilter>();
            Assert.That(filter.IsOverUi(new Vector2(Screen.width * 0.5f, Screen.height * 0.5f)), Is.True,
                "the open menu's scrim vetoes board presses");

            GameHudScene.Click(settings.Q<Button>("settings-close-button"));
            yield return GameHudScene.WaitUntil(() => session.State.Value == SessionState.Playing);

            Assert.That(session.State.Value, Is.EqualTo(SessionState.Playing));
            Assert.That(m_scene.Flow.Current, Is.EqualTo(GameOverlay.None));
            Assert.That(countdown.Remaining, Is.LessThanOrEqualTo(remaining));
            Assert.That(countdown.Remaining, Is.GreaterThan(remaining - 0.5f), "no time was lost behind the menu");
            Assert.That(session.DisplayedSeconds.Value, Is.InRange(shown - 1, shown));
            Assert.That(GameHudScene.IsOverlayBackEnabled(), Is.False);
            yield return null;
            Assert.That(settings.resolvedStyle.display, Is.EqualTo(DisplayStyle.None));
            Assert.That(m_scene.Root.Q<Label>("timer-label").text,
                Is.EqualTo(GameHudScene.FormatTime(session.DisplayedSeconds.Value)));
        }

        [UnityTest]
        public IEnumerator RetryShowsTheConfirmationAndItsRetryRestartsTheLevelOnce()
        {
            yield return Load();
            LevelSession session = m_scene.Session;
            object before = session.Attempt;
            int generation = session.Attempt.Generation;
            int authored = session.Attempt.Level.Model.TimerSeconds;
            int[] stockBefore = ReadStock();
            int coinsBefore = ReadCoins();

            GameHudScene.Click(m_scene.TopBar.View.RetryButton);
            yield return GameHudScene.WaitUntil(() => session.State.Value == SessionState.Paused);

            Assert.That(session.PauseReason, Is.EqualTo(PauseReason.User), "Retry pauses before asking");
            Assert.That(m_scene.Flow.Current, Is.EqualTo(GameOverlay.RestartConfirm));
            Assert.That(session.Attempt, Is.SameAs(before), "nothing restarts before the confirmation");
            yield return null;
            VisualElement screen = m_scene.Root.Q(RestartConfirmView.ScreenName);
            Assert.That(screen.resolvedStyle.display, Is.EqualTo(DisplayStyle.Flex));
            Assert.That(m_scene.Root.Q("settings-screen").resolvedStyle.display, Is.EqualTo(DisplayStyle.None),
                "one overlay at a time");
            string title = m_scene.Scope.Container.Resolve<AttemptLevelNumber>().Title;
            Assert.That(screen.Q<Label>(RestartConfirmView.TitleName).text, Is.EqualTo(title));
            Assert.That(title, Does.StartWith("Level "));
            Assert.That(screen.Q<Label>(RestartConfirmView.PenaltyName).text, Is.EqualTo("-1"));
            Assert.That(screen.Q<Label>(RestartConfirmView.MessageName).text, Is.EqualTo("You will lose 1 life!"));

            Button confirm = screen.Q<Button>(RestartConfirmView.RetryButtonName);
            GameHudScene.Click(confirm);
            Assert.That(confirm.enabledSelf, Is.False, "the press locks Retry: one press, one Restart (design 4)");
            yield return GameHudScene.WaitUntil(() => session.State.Value == SessionState.Playing);

            Assert.That(session.State.Value, Is.EqualTo(SessionState.Playing), "play resumes on the rebuilt level");
            Assert.That(session.Attempt, Is.Not.Null);
            Assert.That(session.Attempt, Is.Not.SameAs(before), "a new attempt from the authored level");
            Assert.That(session.Attempt.Generation, Is.EqualTo(generation + 1), "exactly one new attempt");
            Assert.That(ReadStock(), Is.EqualTo(stockBefore), "no booster stock is refunded, refilled or spent");
            Assert.That(ReadCoins(), Is.EqualTo(coinsBefore), "no coins change");
            Assert.That(session.IsOpening.Value, Is.False, "a restart applies no starting selection");
            Assert.That(m_scene.Hud.View.HasRootClass(GameplayHudView.OpeningClass), Is.False);
            Assert.That(m_scene.Flow.Current, Is.EqualTo(GameOverlay.None));
            Assert.That(m_scene.Scope.Container.Resolve<ICountdown>().Remaining,
                Is.InRange(authored - 1f, (float)authored), "the clock restarted from the authored time");
            object rebuilt = session.Attempt;
            yield return null;
            yield return null;
            Assert.That(session.Attempt, Is.SameAs(rebuilt), "exactly one restart");
            Assert.That(screen.resolvedStyle.display, Is.EqualTo(DisplayStyle.None));

            GameHudScene.Click(m_scene.TopBar.View.RetryButton);
            yield return GameHudScene.WaitUntil(() => m_scene.Flow.Current == GameOverlay.RestartConfirm);
            Assert.That(confirm.enabledSelf, Is.True, "the next confirmation takes a press again");
        }

        [UnityTest]
        public IEnumerator ClosingTheConfirmationResumesTheSameAttempt()
        {
            yield return Load();
            LevelSession session = m_scene.Session;
            object before = session.Attempt;
            GameHudScene.Click(m_scene.TopBar.View.RetryButton);
            yield return GameHudScene.WaitUntil(() => session.State.Value == SessionState.Paused);

            GameHudScene.Click(m_scene.Root.Q<Button>(RestartConfirmView.CloseButtonName));
            yield return GameHudScene.WaitUntil(() => session.State.Value == SessionState.Playing);

            Assert.That(session.State.Value, Is.EqualTo(SessionState.Playing));
            Assert.That(session.Attempt, Is.SameAs(before), "Close never restarts");
            Assert.That(m_scene.Flow.Current, Is.EqualTo(GameOverlay.None));
        }

        [UnityTest]
        public IEnumerator ABackgroundPauseThroughTheRealRelayOpensNothingAndResumesOnReturn()
        {
            yield return Load();
            LevelSession session = m_scene.Session;
            ICountdown countdown = m_scene.Resolve<ICountdown>();

            m_scene.SendApplicationPause(true);
            Assert.That(session.State.Value, Is.EqualTo(SessionState.Paused));
            Assert.That(session.PauseReason, Is.EqualTo(PauseReason.Background));
            Assert.That(m_scene.Flow.Current, Is.EqualTo(GameOverlay.None), "backgrounding opens no menu (DEC-182)");
            float remaining = countdown.Remaining;
            yield return null;
            yield return null;
            Assert.That(countdown.Remaining, Is.EqualTo(remaining), "no time is lost in the background");

            m_scene.SendApplicationPause(false);

            Assert.That(session.State.Value, Is.EqualTo(SessionState.Playing), "the return resumes play");
            Assert.That(m_scene.Flow.Current, Is.EqualTo(GameOverlay.None));
            Assert.That(m_scene.Root.Q("settings-screen").resolvedStyle.display, Is.EqualTo(DisplayStyle.None));
        }

        [UnityTest]
        public IEnumerator ABackgroundFromTheUsersPauseMenuKeepsTheSameMenuAndTimeThroughTheRealRelay()
        {
            yield return Load();
            LevelSession session = m_scene.Session;
            GameHudScene.Click(m_scene.TopBar.View.PauseButton);
            yield return GameHudScene.WaitUntil(() => m_scene.Flow.Current == GameOverlay.PauseMenu);
            Assert.That(session.PauseReason, Is.EqualTo(PauseReason.User));
            int shown = session.DisplayedSeconds.Value;

            m_scene.SendApplicationPause(true);
            Assert.That(session.PauseReason, Is.EqualTo(PauseReason.Background), "the user pause latched to Background");
            m_scene.SendApplicationPause(false);
            yield return null;
            yield return null;

            Assert.That(session.State.Value, Is.EqualTo(SessionState.Paused), "no automatic resume");
            Assert.That(m_scene.Flow.Current, Is.EqualTo(GameOverlay.PauseMenu), "the same menu, shown once");
            Assert.That(session.DisplayedSeconds.Value, Is.EqualTo(shown));
        }

        [UnityTest]
        public IEnumerator ABackgroundDuringTheZeroStockPurchaseResumesPlayOnReturnAndHidesThePopup()
        {
            yield return Load();
            m_stock.Arrange(m_scene.Resolve<PlayerSaveService>(), BoosterId.Hammer, 0);
            yield return null;
            LevelSession session = m_scene.Session;
            InGamePurchaseHost purchase = m_scene.Resolve<InGamePurchaseHost>();
            BoosterUseCoordinator coordinator = m_scene.Resolve<BoosterUseCoordinator>();

            Assert.That(coordinator.Press(BoosterId.Hammer), Is.EqualTo(BoosterPressResult.PurchaseOpened));
            Assert.That(session.PauseReason, Is.EqualTo(PauseReason.Purchase));
            Assert.That(purchase.IsShowing && purchase.IsBackEnabled, Is.True, "the purchase popup and its Back show");
            int shown = session.DisplayedSeconds.Value;

            m_scene.SendApplicationPause(true);
            Assert.That(session.PauseReason, Is.EqualTo(PauseReason.Background));
            Assert.That(m_scene.Flow.Current, Is.EqualTo(GameOverlay.None), "nothing opens while in the background");
            Assert.That(session.DisplayedSeconds.Value, Is.EqualTo(shown), "no time is lost in the background");
            m_scene.SendApplicationPause(false);

            Assert.That(session.State.Value, Is.EqualTo(SessionState.Playing), "the return resumes play");
            Assert.That(m_scene.Flow.Current, Is.EqualTo(GameOverlay.None), "no pause menu (DEC-182)");
            Assert.That(purchase.IsShowing, Is.False, "the purchase popup hides with the resume");
            Assert.That(purchase.IsBackEnabled, Is.False);
            Assert.That(GameHudScene.IsOverlayBackEnabled(), Is.False, "no overlay Back is left listening");
        }

        [UnityTest]
        public IEnumerator PressingTheCoinPlusKeepsPlayingAndOpensNothing()
        {
            yield return Load();
            LevelSession session = m_scene.Session;

            GameHudScene.Click(m_scene.TopBar.View.CoinPlus);
            yield return null;
            yield return null;

            Assert.That(session.State.Value, Is.EqualTo(SessionState.Playing), "the plus pauses nothing (Shop locked)");
            Assert.That(m_scene.Flow.Current, Is.EqualTo(GameOverlay.None));
            Assert.That(m_scene.Resolve<InGamePurchaseHost>().IsShowing, Is.False);
            Assert.That(m_scene.Root.Q("settings-screen").resolvedStyle.display, Is.EqualTo(DisplayStyle.None));
            Assert.That(m_scene.Root.Q(RestartConfirmView.ScreenName).resolvedStyle.display, Is.EqualTo(DisplayStyle.None));
        }

        [UnityTest]
        public IEnumerator ThePauseMenusToggleSavesAPlaceholderChangesNothingAndCloseKeepsTheTime()
        {
            yield return Load();
            LevelSession session = m_scene.Session;
            IPlayerSettings settings = m_scene.Resolve<IPlayerSettings>();
            PlayerSaveService save = m_scene.Resolve<PlayerSaveService>();
            GameHudScene.Click(m_scene.TopBar.View.PauseButton);
            yield return GameHudScene.WaitUntil(() => m_scene.Flow.Current == GameOverlay.PauseMenu);
            yield return null;
            int shown = session.DisplayedSeconds.Value;
            VisualElement screen = m_scene.Root.Q("settings-screen");
            bool music = settings.Get(PlayerSettingId.Music).Value;

            // The save service publishes a setting only after its commit (DEC-92), so a changed value is a saved one.
            GameHudScene.Click(screen.Q<Button>("music-toggle"));
            yield return GameHudScene.WaitUntil(() => settings.Get(PlayerSettingId.Music).Value != music
                                                      && !save.IsSaving.Value);
            Assert.That(settings.Get(PlayerSettingId.Music).Value, Is.EqualTo(!music), "the toggle saved the setting");

            GameHudScene.Click(screen.Q<Button>("music-toggle"));
            yield return GameHudScene.WaitUntil(() => settings.Get(PlayerSettingId.Music).Value == music
                                                      && !save.IsSaving.Value);
            Assert.That(settings.Get(PlayerSettingId.Music).Value, Is.EqualTo(music), "restored");

            bool sound = settings.Get(PlayerSettingId.Sound).Value;
            bool vibration = settings.Get(PlayerSettingId.Vibration).Value;
            GameHudScene.Click(screen.Q<Button>("legal-terms-button"));
            yield return null;
            Assert.That(session.State.Value, Is.EqualTo(SessionState.Paused), "a placeholder changes no state");
            Assert.That(m_scene.Flow.Current, Is.EqualTo(GameOverlay.PauseMenu));
            Assert.That(settings.Get(PlayerSettingId.Music).Value, Is.EqualTo(music));
            Assert.That(settings.Get(PlayerSettingId.Sound).Value, Is.EqualTo(sound));
            Assert.That(settings.Get(PlayerSettingId.Vibration).Value, Is.EqualTo(vibration));
            Assert.That(session.DisplayedSeconds.Value, Is.EqualTo(shown));

            GameHudScene.Click(screen.Q<Button>("settings-close-button"));
            yield return GameHudScene.WaitUntil(() => session.State.Value == SessionState.Playing);
            Assert.That(session.State.Value, Is.EqualTo(SessionState.Playing), "Close resumes");
            Assert.That(session.DisplayedSeconds.Value, Is.EqualTo(shown), "with the same displayed seconds");
        }

        [UnityTest]
        public IEnumerator AHubLaunchRequestShowsTheLaunchedLevelsNumber()
        {
            // Hub's Play hands the loader this request through the Onity transition store (DEC-138).
            const int k_index = 1;
            OnitySceneTransitionStore.SetActiveEnterData(new LevelLaunchRequest(k_index, System.Array.Empty<BoosterId>()));

            yield return Load();

            LevelCatalog levels = m_scene.Resolve<LevelCatalog>();
            Assert.That(levels.Count, Is.GreaterThan(k_index), "the project has a second level");
            Assert.That(m_scene.Session.Attempt.Level.LevelId, Is.EqualTo(levels.Manifest.GetLevelIdAt(k_index)),
                "the loader played the requested level");
            Assert.That(m_scene.TopBar.View.LevelNumber, Is.EqualTo((k_index + 1).ToString()), "Level k+1");
            Assert.That(m_scene.Root.Q<Label>(TopBarView.LevelNumberBackName).text, Is.EqualTo((k_index + 1).ToString()));
            Assert.That(m_scene.Resolve<AttemptLevelNumber>().Title, Is.EqualTo("Level " + (k_index + 1)));
        }

        [UnityTest]
        public IEnumerator TargetingHidesTheTopBarAndCancellingBringsItBack()
        {
            yield return Load();
            m_stock.Arrange(m_scene.Resolve<PlayerSaveService>(), BoosterId.Rocket, 1);
            yield return null;
            BoosterUseCoordinator coordinator = m_scene.Scope.Container.Resolve<BoosterUseCoordinator>();
            VisualElement bar = m_scene.TopBar.View.Bar;
            Assert.That(bar.resolvedStyle.display, Is.EqualTo(DisplayStyle.Flex));

            // A press only selects, so the project's stock is read, never spent (DEC-78).
            Assert.That(coordinator.Press(BoosterId.Rocket), Is.EqualTo(BoosterPressResult.TargetingStarted));
            yield return null;

            Assert.That(m_scene.Hud.View.HasRootClass(GameplayHudView.TargetingClass), Is.True);
            Assert.That(bar.resolvedStyle.display, Is.EqualTo(DisplayStyle.None), "the banner takes the top (reference 02)");
            Assert.That(m_scene.Hud.View.Banner.resolvedStyle.display, Is.EqualTo(DisplayStyle.Flex));

            Assert.That(coordinator.Press(BoosterId.Rocket), Is.EqualTo(BoosterPressResult.TargetingCancelled));
            yield return null;

            Assert.That(bar.resolvedStyle.display, Is.EqualTo(DisplayStyle.Flex), "the top bar returns");
            Assert.That(m_scene.Session.State.Value, Is.EqualTo(SessionState.Playing));
        }

        [UnityTest]
        public IEnumerator TheScenesPointerFilterVetoesTheTopBarButtonsButNotItsEmptyArt()
        {
            yield return Load();
            IUiPointerFilter filter = m_scene.Scope.Container.Resolve<IUiPointerFilter>();
            TopBarView view = m_scene.TopBar.View;
            IPanel panel = view.Bar.panel;
            Assert.That(panel, Is.Not.Null, "the top bar is on its panel");

            Assert.That(filter.IsOverUi(GameHudScene.ToScreen(panel, view.RetryButton.worldBound.center)), Is.True, "Retry");
            Assert.That(filter.IsOverUi(GameHudScene.ToScreen(panel, view.PauseButton.worldBound.center)), Is.True, "Pause");
            Assert.That(filter.IsOverUi(GameHudScene.ToScreen(panel, view.CoinPlus.worldBound.center)), Is.True,
                "the coin plus");

            Assert.That(filter.IsOverUi(GameHudScene.ToScreen(panel, m_scene.Root.Q("level-time").worldBound.center)),
                Is.False, "the Level and Time art lets presses through");
            Assert.That(filter.IsOverUi(GameHudScene.ToScreen(panel, m_scene.Root.Q<Label>("timer-label").worldBound.center)),
                Is.False, "the timer text lets presses through");

            IBoardProjector projector = m_scene.Scope.Container.Resolve<IBoardProjector>();
            IBoardReader board = m_scene.Session.Attempt.Simulation.Board;
            ScreenPoint centre = projector.Footprint.Project((board.Width - 1) * 0.5f, (board.Height - 1) * 0.5f);
            Assert.That(filter.IsOverUi(new Vector2(centre.X, centre.Y)), Is.False, "the board's middle stays pressable");
        }

        [UnityTest]
        public IEnumerator UnloadingTheSceneWithTheMenuOpenLeavesNoOverlayBackActionEnabled()
        {
            yield return Load();
            LevelSession session = m_scene.Session;
            GameHudScene.Click(m_scene.TopBar.View.PauseButton);
            yield return GameHudScene.WaitUntil(() => session.State.Value == SessionState.Paused);
            Assert.That(GameHudScene.IsOverlayBackEnabled(), Is.True);

            m_sceneLoaded = false;
            yield return GameHudScene.Unload();

            Assert.That(GameHudScene.IsOverlayBackEnabled(), Is.False, "the scene container disposed the Back action");
            Assert.That(m_scene.Flow.OnBack(), Is.False, "the disposed flow ignores Back");
        }

        private IEnumerator Load()
        {
            m_sceneLoaded = true;
            yield return GameHudScene.Load(scene => m_scene = scene);
            Assert.That(m_scene.Session.State.Value, Is.EqualTo(SessionState.Playing), "direct Play auto-plays (DEC-116)");
        }

        private static readonly BoosterId[] s_boosters =
        {
            BoosterId.TimeFreeze, BoosterId.Rocket, BoosterId.Hammer, BoosterId.Vacuum, BoosterId.ExtraTime
        };

        private int[] ReadStock()
        {
            IBoosterInventory inventory = m_scene.Resolve<IBoosterInventory>();
            int[] stock = new int[s_boosters.Length];
            for (int i = 0; i < s_boosters.Length; i++)
            {
                stock[i] = inventory.GetStock(s_boosters[i]).Value;
            }

            return stock;
        }

        private int ReadCoins()
        {
            Assert.That(m_scene.Resolve<CurrencyCatalog>().TryGet(GameTopBar.CoinCurrencyId, out CurrencyDefinition coin),
                Is.True);
            return m_scene.Resolve<IWallet>().GetBalance(coin).Value;
        }

        private static IEnumerator WaitSeconds(float seconds)
        {
            float end = Time.realtimeSinceStartup + seconds;
            while (Time.realtimeSinceStartup < end)
            {
                yield return null;
            }
        }

        private static void AssertInside(Rect bounds, Rect panel, string message)
        {
            Assert.That(bounds.width, Is.GreaterThan(0f), message + " is laid out");
            Assert.That(bounds.xMin, Is.GreaterThanOrEqualTo(panel.xMin - 0.5f), message + " stays inside the panel (left)");
            Assert.That(bounds.xMax, Is.LessThanOrEqualTo(panel.xMax + 0.5f), message + " stays inside the panel (right)");
            Assert.That(bounds.yMin, Is.GreaterThanOrEqualTo(panel.yMin - 0.5f), message + " stays inside the panel (top)");
            Assert.That(bounds.yMax, Is.LessThanOrEqualTo(panel.yMax + 0.5f), message + " stays inside the panel (bottom)");
        }
    }
}
