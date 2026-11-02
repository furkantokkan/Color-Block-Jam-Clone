using System;
using System.Collections;
using System.Threading;
using System.Threading.Tasks;
using ColorBlockJam.Domain.Board;
using ColorBlockJam.Domain.Commands;
using ColorBlockJam.Domain.Picking;
using ColorBlockJam.Domain.Session;
using ColorBlockJam.Environment;
using ColorBlockJam.Gameplay.Board;
using ColorBlockJam.Gameplay.Boosters;
using ColorBlockJam.Gameplay.Composition;
using ColorBlockJam.Gameplay.Effects;
using ColorBlockJam.Gameplay.Input;
using ColorBlockJam.Gameplay.Levels;
using ColorBlockJam.Gameplay.Session;
using ColorBlockJam.Gameplay.UI;
using ColorBlockJam.Player;
using ColorBlockJam.Presentation.Boosters;
using ColorBlockJam.Runtime.Board;
using ColorBlockJam.Runtime.Boosters;
using ColorBlockJam.Runtime.Levels;
using ColorBlockJam.Tests.Gameplay.PlayMode;
using ColorBlockJam.Tests.Gameplay.PlayMode.Fixtures;
using NUnit.Framework;
using Onity.DI;
using Onity.Unity.Contexts;
using Onity.Unity.SceneFlow;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace ColorBlockJam.Tests.Gameplay
{
    /// <summary>
    /// The Game scene's booster composition (B-8, B-12, B-16, B-17, B-21): direct Play still reaches Playing with no
    /// starting selection and no opening, the attempt clock is one AttemptCountdown behind both clock ports, targeting and
    /// the use coordinator are scene singletons that share one mode, and a bound HUD was shown on Home, whose booster bar
    /// the scene's own pointer filter vetoes while the board stays pressable (AC-1). The opening runs
    /// through the real loader and scene collaborators with a fixture level and an isolated gated stock: one combined
    /// debit on Home, a foreground wait after a background, and the attempt's Teardown as the stop point of every wait
    /// (AC-13, AC-14, AC-15). The scene is loaded additively and unloaded afterwards; nothing here spends the project's
    /// player stock, so later suites still see its seeded charges.
    /// </summary>
    public sealed class GameSceneBoosterCompositionTests
    {
        private const string k_gameScene = "Game";
        private const float k_buildTimeoutSeconds = 20f;
        private const float k_waitSeconds = 5f;
        private const int k_seed = 7;

        private bool m_sceneLoaded;
        private HudBoosterStore m_store;
        private LevelCatalog m_levels;
        private OnityContainer m_launchContainer;

        [UnityTearDown]
        public IEnumerator UnloadGameScene()
        {
            // A launch request that a failed test left behind must not reach a later Game scene.
            OnitySceneTransitionStore.TryConsumeActiveEnterData(out LevelLaunchRequest _);
            if (m_sceneLoaded)
            {
                m_sceneLoaded = false;
                yield return SceneManager.UnloadSceneAsync(k_gameScene);
            }

            // BindInstance owns nothing, so this only drops the test's bindings.
            m_launchContainer?.Dispose();
            m_launchContainer = null;
            m_store?.Dispose();
            m_store = null;
            m_levels = null;
        }

        [UnityTest]
        public IEnumerator DirectPlayReachesPlayingWithTheAttemptClockAndNoOpening()
        {
            yield return LoadGame();
            SceneContext scope = FindGameScope();
            LevelSession session = scope.Container.Resolve<LevelSession>();

            Assert.That(session.State.Value, Is.EqualTo(SessionState.Playing), "direct Play auto-plays (DEC-116)");
            Assert.That(session.IsOpening.Value, Is.False, "direct Play has no starting selection (B-16)");
            Assert.That(session.SupportsTimeBoosters, Is.True);
            ICountdown countdown = scope.Container.Resolve<ICountdown>();
            Assert.That(countdown, Is.TypeOf<AttemptCountdown>());
            Assert.That(scope.Container.Resolve<IFreezeCountdown>(), Is.SameAs(countdown));
            Assert.That(countdown.Remaining, Is.GreaterThan(0f));
            Assert.That(session.DisplayedSeconds.Value, Is.GreaterThan(0));

            Assert.That(scope.Container.Resolve<IStartingBoosterSource>().TryTake(out StartingBoosterSelection selection), Is.False);
            Assert.That(selection.IsEmpty, Is.True, "the launch selection was handed over once, and direct Play has none");
            Assert.That(scope.Container.Resolve<TimeFreezeFrostView>(), Is.Not.Null);
        }

        [UnityTest]
        public IEnumerator TargetingAndTheUseCoordinatorShareOneSceneMode()
        {
            yield return LoadGame();
            SceneContext scope = FindGameScope();
            BoosterTargeting targeting = scope.Container.Resolve<BoosterTargeting>();
            BoosterUseCoordinator coordinator = scope.Container.Resolve<BoosterUseCoordinator>();
            Assert.That(scope.Container.Resolve<BoosterUseCoordinator>(), Is.SameAs(coordinator));
            Assert.That(scope.Container.Resolve<BoosterTargeting>(), Is.SameAs(targeting));

            // A press only selects, so the project stock is read, never spent; Editor play seeds one charge (DEC-78).
            Assume.That(scope.Container.Resolve<IBoosterInventory>().GetStock(BoosterId.Rocket).Value, Is.GreaterThan(0),
                "an earlier suite spent the project's Rocket charge");

            Assert.That(coordinator.Press(BoosterId.Rocket), Is.EqualTo(BoosterPressResult.TargetingStarted));
            Assert.That(targeting.Mode.Value, Is.EqualTo(BoosterId.Rocket));
            Assert.That(coordinator.Press(BoosterId.Rocket), Is.EqualTo(BoosterPressResult.TargetingCancelled));
            Assert.That(targeting.Mode.Value, Is.EqualTo(BoosterId.None));
            Assert.That(scope.Container.Resolve<LevelSession>().State.Value, Is.EqualTo(SessionState.Playing));
        }

        [UnityTest]
        public IEnumerator AHubLaunchWithBothStartingBoostersCommitsOneDebitOnHomeAndAppliesThemOnce()
        {
            yield return LoadGame();
            LaunchRun run = StartLaunch(BoosterId.ExtraTime, BoosterId.StartingRocket);
            LevelSession session = run.Session;
            IBoardReader board = session.Attempt.Simulation.Board;
            int authored = session.Attempt.Level.Model.TimerSeconds;
            int occupied = CountOccupiedCells(board);

            Assert.That(session.State.Value, Is.EqualTo(SessionState.Home), "the combined debit is written on Home");
            Assert.That(m_store.ConsumeCount, Is.EqualTo(1), "one debit for both boosters");
            Assert.That(run.Hud.ShowCount, Is.EqualTo(1), "the HUD is shown on Home");
            Assert.That(run.Hud.OpeningCount, Is.Zero, "nothing opens before the commit");
            Assert.That(OnitySceneTransitionStore.TryConsumeActiveEnterData(out LevelLaunchRequest _), Is.False,
                "the loader consumed the launch request");
            yield return null;
            yield return null;
            Assert.That(session.State.Value, Is.EqualTo(SessionState.Home), "Play waits for the commit");

            m_store.Gate.SetResult(true);
            yield return WaitUntil(() => session.State.Value == SessionState.Playing);

            Assert.That(session.State.Value, Is.EqualTo(SessionState.Playing));
            Assert.That(run.Hud.OpeningCount, Is.EqualTo(1));
            Assert.That(run.Hud.StateAtOpening, Is.EqualTo(SessionState.Home), "the HUD learns the opening before Play");
            Assert.That(run.Hud.Opening.HasExtraTime && run.Hud.Opening.HasRocket, Is.True, "both icons open");
            Assert.That(run.Hud.Opening.BonusSeconds, Is.EqualTo(ExtraTimeBonus()));
            Assert.That(m_store.GetStock(BoosterId.ExtraTime).Value, Is.Zero);
            Assert.That(m_store.GetStock(BoosterId.StartingRocket).Value, Is.Zero);

            CommandLog log = session.Attempt.Simulation.Commands.Log;
            yield return WaitUntil(() => log.Count > 0);
            AssertOneRocketAt(log, run.Hud.Opening.Rocket.To);
            Assert.That(board.GetOccupant(run.Hud.Opening.Rocket.To), Is.EqualTo(-1), "the sampled cell is gone");
            Assert.That(CountOccupiedCells(board), Is.EqualTo(occupied - 1), "Rocket removed exactly one cell");
            Assert.That(run.Countdown.Remaining, Is.GreaterThan(authored), "Extra Time applied in the first command phase");
            Assert.That(run.Countdown.Remaining, Is.LessThanOrEqualTo(authored + ExtraTimeBonus()));

            yield return null;
            yield return null;
            AssertOneRocketAt(log, run.Hud.Opening.Rocket.To);
            Assert.That(m_store.ConsumeCount, Is.EqualTo(1), "nothing is spent or applied twice");
            Assert.That(run.Seeds.Calls, Is.EqualTo(1), "the Rocket target is sampled once");
            Assert.That(run.Load.Status, Is.EqualTo(TaskStatus.RanToCompletion));
        }

        [UnityTest]
        public IEnumerator AHubLaunchWithRocketAloneDebitsOnlyRocketAndKeepsTheAuthoredTime()
        {
            yield return LoadGame();
            LaunchRun run = StartLaunch(BoosterId.StartingRocket);
            LevelSession session = run.Session;
            int authored = session.Attempt.Level.Model.TimerSeconds;
            Assert.That(session.State.Value, Is.EqualTo(SessionState.Home));

            m_store.Gate.SetResult(true);
            yield return WaitUntil(() => session.State.Value == SessionState.Playing);
            CommandLog log = session.Attempt.Simulation.Commands.Log;
            yield return WaitUntil(() => log.Count > 0);

            Assert.That(run.Hud.Opening.HasRocket, Is.True);
            Assert.That(run.Hud.Opening.HasExtraTime, Is.False);
            Assert.That(m_store.ConsumeCount, Is.EqualTo(1));
            Assert.That(m_store.GetStock(BoosterId.StartingRocket).Value, Is.Zero);
            Assert.That(m_store.GetStock(BoosterId.ExtraTime).Value, Is.EqualTo(1), "an unselected booster is not spent");
            AssertOneRocketAt(log, run.Hud.Opening.Rocket.To);
            Assert.That(run.Countdown.Remaining, Is.LessThanOrEqualTo(authored), "no bonus without Extra Time");
        }

        [UnityTest]
        public IEnumerator ABackgroundDuringTheHomeCommitWaitsForTheForegroundThenPlaysTheSamePlanOnce()
        {
            yield return LoadGame();
            LaunchRun run = StartLaunch(BoosterId.ExtraTime, BoosterId.StartingRocket);
            LevelSession session = run.Session;

            session.NotifyBackground(true);
            m_store.Gate.SetResult(true);
            yield return WaitUntil(() => m_store.GetStock(BoosterId.StartingRocket).Value == 0);
            yield return null;
            yield return null;

            Assert.That(m_store.GetStock(BoosterId.ExtraTime).Value, Is.Zero, "the debit committed in the background");
            Assert.That(session.State.Value, Is.EqualTo(SessionState.Home), "a background keeps the attempt on Home");
            Assert.That(run.Hud.OpeningCount, Is.Zero);
            Assert.That(run.Load.IsCompleted, Is.False, "the loader waits for the foreground");

            session.NotifyBackground(false);
            yield return WaitUntil(() => session.State.Value == SessionState.Playing);
            CommandLog log = session.Attempt.Simulation.Commands.Log;
            yield return WaitUntil(() => log.Count > 0);

            Assert.That(run.Hud.OpeningCount, Is.EqualTo(1), "one Play after the foreground");
            Assert.That(run.Hud.StateAtOpening, Is.EqualTo(SessionState.Home));
            Assert.That(m_store.ConsumeCount, Is.EqualTo(1), "the committed plan is reused, not spent again");
            Assert.That(run.Seeds.Calls, Is.EqualTo(1), "and not sampled again");
            AssertOneRocketAt(log, run.Hud.Opening.Rocket.To);
        }

        [UnityTest]
        public IEnumerator UnloadingTheSceneDuringTheHomeCommitEndsTheLoaderAndSpendsNothing()
        {
            yield return LoadGame();
            LaunchRun run = StartLaunch(BoosterId.ExtraTime, BoosterId.StartingRocket);
            Assert.That(run.Load.IsCompleted, Is.False, "the debit waits for its write lock");

            m_sceneLoaded = false;
            yield return SceneManager.UnloadSceneAsync(k_gameScene);
            yield return WaitUntil(() => run.Load.IsCompleted);

            Assert.That(run.Load.Status, Is.EqualTo(TaskStatus.RanToCompletion),
                "the attempt's Teardown ended the wait although the lock never freed");
            Assert.That(m_store.GetStock(BoosterId.ExtraTime).Value, Is.EqualTo(1), "a withdrawn debit spends nothing");
            Assert.That(m_store.GetStock(BoosterId.StartingRocket).Value, Is.EqualTo(1));
            Assert.That(run.Hud.OpeningCount, Is.Zero, "nothing opens after the scene is gone");
        }

        [UnityTest]
        public IEnumerator UnloadingWhileBackgroundedAfterTheCommitEndsTheForegroundWait()
        {
            yield return LoadGame();
            LaunchRun run = StartLaunch(BoosterId.ExtraTime, BoosterId.StartingRocket);
            run.Session.NotifyBackground(true);
            m_store.Gate.SetResult(true);
            yield return WaitUntil(() => m_store.GetStock(BoosterId.StartingRocket).Value == 0);
            yield return null;
            Assert.That(run.Load.IsCompleted, Is.False, "the loader waits for the foreground");

            m_sceneLoaded = false;
            yield return SceneManager.UnloadSceneAsync(k_gameScene);
            yield return WaitUntil(() => run.Load.IsCompleted);

            Assert.That(run.Load.Status, Is.EqualTo(TaskStatus.RanToCompletion), "the foreground wait stopped with the attempt");
            Assert.That(m_store.GetStock(BoosterId.ExtraTime).Value, Is.Zero, "a committed debit stays spent (B-16)");
            Assert.That(m_store.GetStock(BoosterId.StartingRocket).Value, Is.Zero);
            Assert.That(m_store.ConsumeCount, Is.EqualTo(1));
            Assert.That(run.Hud.OpeningCount, Is.Zero, "and nothing applies");
        }

        [UnityTest]
        public IEnumerator ABoundHudWasShownOnHome()
        {
            yield return LoadGame();
            SceneContext scope = FindGameScope();

            // The scene binds the HUD only once its document is wired (B-21, the scene step); until then this check is
            // reported as ignored instead of passing without looking at anything.
            if (!scope.Container.TryResolve(out IGameplayHud hud))
            {
                Assert.Ignore("Game.unity does not wire the Gameplay HUD document yet (B-21 scene step).");
            }

            Assert.That(hud, Is.TypeOf<GameplayHud>());
            GameplayHud bound = (GameplayHud)hud;
            Assert.That(bound.IsBound, Is.True);
            Assert.That(bound.Bar.SlotCount, Is.EqualTo(4));
            BoosterId[] order = { BoosterId.TimeFreeze, BoosterId.Rocket, BoosterId.Hammer, BoosterId.Vacuum };
            for (int slot = 0; slot < order.Length; slot++)
            {
                Assert.That(bound.Bar.GetSlotBooster(slot), Is.EqualTo(order[slot]));
            }
        }

        [UnityTest]
        public IEnumerator TheScenesPointerFilterVetoesTheBoosterBarButNeverTheBoard()
        {
            yield return LoadGame();
            SceneContext scope = FindGameScope();

            // The installer adds the HUD document to the veto list at runtime (B-21); the serialized list stays empty,
            // so only the resolved filter proves that a press on the bar never reaches the board.
            Assert.That(scope.Container.TryResolve(out IGameplayHud hud), Is.True, "Game.unity wires the HUD document");
            GameplayHudView view = ((GameplayHud)hud).View;
            Assert.That(view, Is.Not.Null, "the HUD was bound on Home");
            IUiPointerFilter filter = scope.Container.Resolve<IUiPointerFilter>();
            IBoardProjector projector = scope.Container.Resolve<IBoardProjector>();
            IBoardReader board = scope.Container.Resolve<LevelSession>().Attempt.Simulation.Board;
            yield return null;
            yield return null;

            UnityEngine.UIElements.IPanel panel = view.Root.panel;
            Assert.That(panel, Is.Not.Null, "the HUD is on its panel");
            for (int slot = 0; slot < GameplayHudView.k_slotCount; slot++)
            {
                Vector2 button = ToScreen(panel, view.GetButton(slot).worldBound.center);
                Assert.That(filter.IsOverUi(button), Is.True, "booster button " + slot);
            }

            Assert.That(filter.IsOverUi(ToScreen(panel, view.BoosterBar.worldBound.center)), Is.True, "the bar itself");
            ScreenPoint centre = projector.Footprint.Project((board.Width - 1) * 0.5f, (board.Height - 1) * 0.5f);
            Assert.That(filter.IsOverUi(new Vector2(centre.X, centre.Y)), Is.False, "the board's middle cell stays pressable");
        }

        [UnityTest]
        public IEnumerator TheSceneHostsTheSharedPurchasePopupHiddenInTheHudAndShowsItUnderPausedPurchase()
        {
            yield return LoadGame();
            SceneContext scope = FindGameScope();
            Assert.That(scope.Container.TryResolve(out IGameplayHud hud), Is.True, "Game.unity wires the HUD document");
            IInGamePurchaseHost resolved = scope.Container.Resolve<IInGamePurchaseHost>();
            Assert.That(resolved, Is.TypeOf<InGamePurchaseHost>(), "with the HUD, the shared popup hosts the purchase (B-32)");
            InGamePurchaseHost host = (InGamePurchaseHost)resolved;
            Assert.That(scope.Container.Resolve<InGamePurchaseHost>(), Is.SameAs(host));
            VisualElement screen = ((GameplayHud)hud).View.Root.Q("booster-purchase-screen");
            Assert.That(screen, Is.Not.Null, "GameplayHud.uxml holds the shared popup instance");
            yield return null;
            Assert.That(screen.resolvedStyle.display, Is.EqualTo(DisplayStyle.None), "hidden until needed");
            Assert.That(host.IsShowing, Is.False);

            // The coordinator's zero-stock steps (show, then pause), so the project's player stock is neither spent nor
            // zeroed; the zero-stock press itself is covered on an isolated stock by InGamePurchasePopupTests.
            LevelSession session = scope.Container.Resolve<LevelSession>();
            Assert.That(scope.Container.Resolve<BoosterCatalog>().TryGet(BoosterId.Hammer, out BoosterDefinition hammer), Is.True);
            Assert.That(host.TryShow(hammer), Is.True);
            Assert.That(session.TryOpenPurchase(), Is.True);
            yield return null;

            Assert.That(session.State.Value, Is.EqualTo(SessionState.Paused));
            Assert.That(session.PauseReason, Is.EqualTo(PauseReason.Purchase));
            Assert.That(screen.resolvedStyle.display, Is.EqualTo(DisplayStyle.Flex));
            Assert.That(screen.Q<Label>("booster-purchase-title").text, Is.EqualTo(hammer.DisplayName));
            IUiPointerFilter filter = scope.Container.Resolve<IUiPointerFilter>();
            Assert.That(filter.IsOverUi(new Vector2(Screen.width * 0.5f, Screen.height * 0.5f)), Is.True,
                "the scene's pointer filter sees the scrim over the board");

            Assert.That(host.Close(), Is.True);
            yield return null;
            Assert.That(session.State.Value, Is.EqualTo(SessionState.Playing));
            Assert.That(screen.resolvedStyle.display, Is.EqualTo(DisplayStyle.None));
        }

        // Returns the directly played session to Home, then runs a second loader bound as the installer binds it, with a
        // Hub launch request for the selection, the split fixture level, a fixed seed, a gated isolated stock and a
        // recording HUD. The loader runs synchronously up to the gated debit.
        private LaunchRun StartLaunch(params BoosterId[] selection)
        {
            SceneContext scope = FindGameScope();
            OnityContainer scene = scope.Container;
            LevelSession session = scene.Resolve<LevelSession>();
            Assert.That(session.Pause(PauseReason.User), Is.True);
            Assert.That(session.GoHome(), Is.True);

            m_levels = new LevelCatalog();
            Assert.That(LevelDatabaseJson.TryWrite(new[] { BoardPresenterBoosterTests.SplitLevel() }, out string json,
                out string error), Is.True, error);
            Assert.That(m_levels.LoadJson(json, LevelReadMode.Runtime, out error), Is.True, error);

            m_store = new HudBoosterStore { Gate = new TaskCompletionSource<bool>() };
            RecordingHud hud = new RecordingHud(session);
            CountingSeedSource seeds = new CountingSeedSource(k_seed);
            GameAttemptLoader loader = new GameAttemptLoader(0, FindInGame<BoardCamera>());

            // The scene's own collaborators with test data; BindInstance owns none of them.
            m_launchContainer = new OnityContainer();
            m_launchContainer.BindInstance(m_levels);
            m_launchContainer.BindInstance<IStartingBoosterSource>(loader);
            m_launchContainer.BindInstance<ISeedSource>(seeds);
            m_launchContainer.BindInstance(new StartingBoosterPreparer(m_store, m_store, scene.Resolve<BoosterCatalog>()));
            m_launchContainer.BindInstance<IGameplayHud>(hud);
            m_launchContainer.BindInstance(scene.Resolve<EnvironmentView>());
            m_launchContainer.BindInstance(scene.Resolve<BlockEffectContextSource>());
            m_launchContainer.BindInstance(session);
            m_launchContainer.BindInstance(scene.Resolve<BoardPresenter>());

            OnitySceneTransitionStore.SetActiveEnterData(new LevelLaunchRequest(0, selection));
            Task load = loader.LoadAsync(m_launchContainer, CancellationToken.None);
            Assert.That(load.IsFaulted, Is.False, load.Exception?.ToString());
            Assert.That(session.Attempt, Is.Not.Null, "the launch prepared the fixture attempt on Home");
            return new LaunchRun(session, hud, seeds, load, scene.Resolve<ICountdown>());
        }

        private static int ExtraTimeBonus()
        {
            Assert.That(ProjectContext.Instance.Container.Resolve<BoosterCatalog>().TryGet(BoosterId.ExtraTime,
                out BoosterDefinition definition), Is.True);
            return ((ExtraTimeBoosterDefinition)definition).BonusSeconds;
        }

        private static void AssertOneRocketAt(CommandLog log, Cell cell)
        {
            int rockets = 0;
            for (int index = 0; index < log.Count; index++)
            {
                GameCommand command = log.GetAt(index);
                if (command.Kind == CommandKind.RemoveCell)
                {
                    rockets++;
                    Assert.That(command.To, Is.EqualTo(cell), "the Rocket removes the sampled cell");
                }
            }

            Assert.That(rockets, Is.EqualTo(1), "the opening Rocket applies once");
        }

        // Inverts RuntimePanelUtils.ScreenToPanel and flips back to screen space (origin bottom left), as the HUD layout
        // tests do.
        private static Vector2 ToScreen(UnityEngine.UIElements.IPanel panel, Vector2 panelPoint)
        {
            Vector2 origin = UnityEngine.UIElements.RuntimePanelUtils.ScreenToPanel(panel, Vector2.zero);
            Vector2 unit = UnityEngine.UIElements.RuntimePanelUtils.ScreenToPanel(panel, Vector2.one) - origin;
            Vector2 flipped = new Vector2((panelPoint.x - origin.x) / unit.x, (panelPoint.y - origin.y) / unit.y);
            return new Vector2(flipped.x, Screen.height - flipped.y);
        }

        private static int CountOccupiedCells(IBoardReader board)
        {
            int count = 0;
            for (int y = 0; y < board.Height; y++)
            {
                for (int x = 0; x < board.Width; x++)
                {
                    if (board.GetOccupant(new Cell(x, y)) >= 0)
                    {
                        count++;
                    }
                }
            }

            return count;
        }

        private static IEnumerator WaitUntil(Func<bool> condition)
        {
            float deadline = Time.realtimeSinceStartup + k_waitSeconds;
            while (!condition() && Time.realtimeSinceStartup < deadline)
            {
                yield return null;
            }
        }

        private IEnumerator LoadGame()
        {
            yield return SceneManager.LoadSceneAsync(k_gameScene, LoadSceneMode.Additive);
            m_sceneLoaded = true;
            SceneContext scope = FindGameScope();
            Assert.That(scope, Is.Not.Null, "the Game scene has a SceneContext");
            float deadline = Time.realtimeSinceStartup + k_buildTimeoutSeconds;
            while (!scope.IsReady && Time.realtimeSinceStartup < deadline)
            {
                yield return null;
            }

            Assert.That(scope.IsReady, Is.True, "the Game scene container finished its async build");
        }

        private static SceneContext FindGameScope()
        {
            return FindInGame<SceneContext>();
        }

        private static T FindInGame<T>() where T : Component
        {
            Scene game = SceneManager.GetSceneByName(k_gameScene);
            GameObject[] roots = game.GetRootGameObjects();
            for (int i = 0; i < roots.Length; i++)
            {
                T component = roots[i].GetComponentInChildren<T>(true);
                if (component != null)
                {
                    return component;
                }
            }

            return null;
        }

        private readonly struct LaunchRun
        {
            public LaunchRun(LevelSession session, RecordingHud hud, CountingSeedSource seeds, Task load, ICountdown countdown)
            {
                Session = session;
                Hud = hud;
                Seeds = seeds;
                Load = load;
                Countdown = countdown;
            }

            public LevelSession Session { get; }

            public RecordingHud Hud { get; }

            public CountingSeedSource Seeds { get; }

            public Task Load { get; }

            public ICountdown Countdown { get; }
        }

        /// <summary>Records what the loader shows and the session state when it learns the opening.</summary>
        private sealed class RecordingHud : IGameplayHud
        {
            private readonly LevelSession m_session;

            public RecordingHud(LevelSession session)
            {
                m_session = session;
            }

            public int ShowCount { get; private set; }

            public int OpeningCount { get; private set; }

            public OpeningPlan Opening { get; private set; }

            public SessionState StateAtOpening { get; private set; }

            public void Show(AttemptLevel level)
            {
                ShowCount++;
            }

            public void SetOpening(in OpeningPlan plan)
            {
                OpeningCount++;
                Opening = plan;
                StateAtOpening = m_session.State.Value;
            }
        }

        /// <summary>A fixed seed that counts how often the loader draws one.</summary>
        private sealed class CountingSeedSource : ISeedSource
        {
            private readonly int m_seed;

            public CountingSeedSource(int seed)
            {
                m_seed = seed;
            }

            public int Calls { get; private set; }

            public int NextSeed()
            {
                Calls++;
                return m_seed;
            }
        }
    }
}
