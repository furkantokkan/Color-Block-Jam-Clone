using ColorBlockJam.Runtime.Art;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using ColorBlockJam.Domain.Board;
using ColorBlockJam.Domain.Commands;
using ColorBlockJam.Domain.Hooks;
using ColorBlockJam.Domain.Session;
using ColorBlockJam.Environment;
using ColorBlockJam.Gameplay.Board;
using ColorBlockJam.Gameplay.Composition;
using ColorBlockJam.Gameplay.Effects;
using ColorBlockJam.Gameplay.Input;
using ColorBlockJam.Gameplay.Session;
using ColorBlockJam.Presentation.Board;
using ColorBlockJam.Runtime.Levels;
using ColorBlockJam.Runtime.Board;
using NUnit.Framework;
using Onity.Unity.Contexts;
using Onity.DI;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.TestTools.Constraints;
using UnityEngine.UIElements;
using Is = UnityEngine.TestTools.Constraints.Is;

namespace ColorBlockJam.Tests.Gameplay
{
    public sealed class GameSceneIceTests : InputTestFixture
    {
        private LevelCatalog m_levels;
        private bool m_loaded;
        private Mouse m_mouse;

        public override void Setup()
        {
            base.Setup();
            m_mouse = InputSystem.AddDevice<Mouse>();
        }

        [UnityTearDown]
        public IEnumerator UnloadGame()
        {
            if (m_loaded) yield return SceneManager.UnloadSceneAsync("Game");
            m_levels = null;
        }

        [UnityTest]
        public IEnumerator LoaderPreparesSharedContextAndIceThawsAndRestartsInRealGameScene()
        {
            yield return SceneManager.LoadSceneAsync("Game", LoadSceneMode.Additive);
            m_loaded = true;
            SceneContext scope = Find<SceneContext>();
            float deadline = Time.realtimeSinceStartup + 20f;
            while (!scope.IsReady && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(scope.IsReady, Is.True);
            LevelSession session = scope.Container.Resolve<LevelSession>();
            Assert.That(session.Pause(PauseReason.User), Is.True);
            Assert.That(session.GoHome(), Is.True);
            m_levels = new LevelCatalog();
            Assert.That(LevelDatabaseJson.TryWrite(new[] { Level() }, out string json, out string error), Is.True, error);
            Assert.That(m_levels.LoadJson(json, LevelReadMode.Runtime, out error), Is.True, error);
            var loader = new GameAttemptLoader(0, Find<BoardCamera>());
            // Exercise the actual loader boundary with a test catalog; authored project JSON stays intact. Load takes the
            // selected level and its number once its pack is resident (DEC-196): index 0 is Level 1.
            typeof(GameAttemptLoader).GetMethod("Load", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(loader, new object[] { scope.Container, m_levels.GetAt(0), 1 });
            yield return null;
            BoardPresenter presenter = scope.Container.Resolve<BoardPresenter>();
            BlockEffectContextSource source = scope.Container.Resolve<BlockEffectContextSource>();
            var first = (IceEffectState)source.CurrentAttempt.GetAt(0);
            var simulation = session.Attempt.Simulation;
            Assert.That(first.Rules.GetRemaining(0), Is.EqualTo(1));
            Assert.That(simulation.Drag.TryBegin(0, new BoardPoint(0, 0)), Is.False);
            // The ice is the Ice material on the block's own tiles (DEC-184), so it moves with the block by construction.
            Assert.That(presenter.GetPiece(0).View.IsIced, Is.True);
            Vector3 blockPosition = presenter.GetPiece(0).transform.position;
            Camera camera = Find<Camera>();
            Vector3 screen = camera.WorldToScreenPoint(blockPosition + Vector3.up * ExitMotionSettings.k_blockTopHeight);
            Move(m_mouse.position, new Vector2(screen.x, screen.y));
            yield return null;
            Press(m_mouse.leftButton);
            bool shook = false;
            for (int frame = 0; frame < 5; frame++)
            {
                yield return null;
                shook |= (presenter.GetPiece(0).transform.position - blockPosition).sqrMagnitude > 0.000001f;
            }
            Release(m_mouse.leftButton);
            Assert.That(shook, Is.True, "a blocked pointer press punches the frozen block");
            for (float end = Time.realtimeSinceStartup + 0.5f; Time.realtimeSinceStartup < end;) yield return null;
            Assert.That(presenter.GetPiece(0).transform.position, Is.EqualTo(blockPosition), "the punch ends on the cell");
            UIDocument document = Find<UIDocument>();
            Label count = document.rootVisualElement.Q<Label>("ice-count-0");
            Assert.That(count.text, Is.EqualTo("1"));
            Assert.That(count.pickingMode, Is.EqualTo(PickingMode.Ignore));
            Assert.That(float.IsNaN(count.resolvedStyle.left), Is.False, "counter projected after initial camera framing");
            int prepared = presenter.PreparedPieceCount;
            Assert.That(simulation.Commands.Execute(GameCommand.MoveAndExit(1, new Cell(2, 0), new Cell(2, 0), Side.Bottom)), Is.EqualTo(CommandOutcome.Applied));
            presenter.Sync(simulation, default);
            Assert.That(first.Rules.GetRemaining(0), Is.Zero);
            Assert.That(count.style.display.value, Is.EqualTo(DisplayStyle.None));
            Assert.That(session.Pause(PauseReason.User), Is.True);
            Assert.That(session.Restart(), Is.True);
            yield return null;
            var restarted = (IceEffectState)source.CurrentAttempt.GetAt(0);
            Assert.That(restarted, Is.Not.SameAs(first));
            Assert.That(restarted.Rules.GetRemaining(0), Is.EqualTo(1));
            Assert.That(presenter.PreparedPieceCount, Is.EqualTo(prepared));
            Assert.That(presenter.GetPiece(0).View.IsIced, Is.True, "restart shows the ice again");
            Assert.That(count.text, Is.EqualTo("1"));
            var level = session.Attempt.Level;
            Assert.That(session.Pause(PauseReason.User), Is.True);
            Assert.That(session.GoHome(), Is.True);
            // The scene intentionally uses InactiveCountdown until the timer task. Exercise its real
            // presentation cleanup with a test-owned clock and the same warmed scene collaborators.
            var countdown = new TestCountdown();
            using (var failureSession = new LevelSession(scope.Container.Resolve<DragInputRouter>(),
                scope.Container.Resolve<IPointerSource>(), presenter, countdown,
                scope.Container.Resolve<IPauseOverlay>(), scope.Container.Resolve<IAttemptHookSource>(), new PrimeTweenFinishDeadline()))
            {
                Assert.That(failureSession.TryPrepare(level, out error), Is.True, error);
                Assert.That(failureSession.Play(), Is.True);
                BlockPiece frozen = presenter.GetPiece(0);
                Assert.That(frozen.View.IsIced, Is.True);
                Assert.That(count.text, Is.EqualTo("1"));
                countdown.Expire = true;
                // The countdown waits for a block press (DEC-189); the test stands in for it.
                failureSession.Attempt.MarkBlockTouched();
                failureSession.RunTick(1f);
                Assert.That(failureSession.State.Value, Is.EqualTo(SessionState.Failed));
                Assert.That(failureSession.Attempt, Is.Null);
                Assert.That(countdown.Stopped, Is.True);
                Assert.That(presenter.ActivePieceCount, Is.Zero);
                Assert.That(count.style.display.value, Is.EqualTo(DisplayStyle.None));
                Assert.That(frozen.View.IsIced, Is.False, "teardown takes the ice off the returned root");
                Assert.That(failureSession.GoHome(), Is.True);
            }
            VerifyGenericViews(scope, level);
            VerifyWarmedIceSync(scope, level);
        }

        private static void VerifyWarmedIceSync(SceneContext scope, ColorBlockJam.Gameplay.Levels.AttemptLevel level)
        {
#if UNITY_EDITOR
            var source = new BlockEffectContextSource(new IBlockEffectModule[] { new IceEffectModule(null) });
            source.Prepare(level, Level(20));
            var hooks = new EffectAttemptHookSource(source);
            var root = new GameObject("Ice allocation fixture");
            Material material = UnityEditor.AssetDatabase.LoadAssetAtPath<Material>("Assets/ColorBlockJam/Art/Materials/Effects/MAT_Ice.mat");
            BlockEffectViewContext viewContext = scope.Container.Resolve<BlockEffectViewContext>();
            // The ice is shown on the block's own tiles (DEC-184), so the fixture root is a block view.
            var blockView = root.AddComponent<ColorBlockJam.Presentation.Board.ModularBlockView>();
            blockView.Prepare(viewContext.Art.Catalog, 1);
            blockView.SetCells(new[] { Vector2Int.zero }, viewContext.Art.GetAtlasColor(PaletteColor.Red));
            using (var view = new IceEffectView(viewContext, material))
            {
                try
                {
                    Assert.That(view.TryPrepare(source.Context, source.Context.GetAt(0), out string error), Is.True, error);
                    hooks.Create(level);
                    view.BindAttempt(source.CurrentAttempt, source.CurrentAttempt.GetAt(0));
                    view.Attach(0, root.transform);
                    var board = new BoardSimulation(level.Model, new AttemptHooksBuilder().Build(),
                        ColorBlockJam.Domain.Movement.DragSettings.Default).Preview;
                    var leave = new BlockExitedEvent(1, 0, Side.Bottom, default, ExitOutcome.Leave);
                    ((IceEffectState)source.CurrentAttempt.GetAt(0)).Rules.OnBlockExited(leave);
                    view.Sync(board); // Warm the same count-change path before measuring a fresh attempt.
                    view.ReturnAll();
                    hooks.Create(level);
                    view.BindAttempt(source.CurrentAttempt, source.CurrentAttempt.GetAt(0));
                    view.Attach(0, root.transform);
                    for (int i = 0; i < 20; i++) view.Sync(board);
                    // The Recorder-based constraint; GC.GetAllocatedBytesForCurrentThread reports 0 in this Mono runtime, so
                    // the Ice task's byte-count assertions were vacuous. Superseded by the booster contract (B-31, DEC-148):
                    // steady Sync stays 0 B and a count change allows only UI Toolkit's numeral copies.
                    // The first measurement in a domain also measures the recorder's own setup, so an empty one comes first.
                    Assert.That(() => { }, Is.Not.AllocatingGCMemory());
                    Assert.That(() =>
                    {
                        for (int i = 0; i < 100; i++) view.Sync(board);
                    }, Is.Not.AllocatingGCMemory(), "warmed Ice Sync allocates no managed memory");
                    ((IceEffectState)source.CurrentAttempt.GetAt(0)).Rules.OnBlockExited(leave);
                    int writes = view.Labels.TextWriteCount;
                    // UI Toolkit 2022.3 copies each non-empty string a Label is given, even a prebuilt numeral, so the
                    // count change's only samples are the counter label's and its fill label's copies of "19".
                    int samples = PlayMode.Fixtures.GcAllocations.Count(() => view.Sync(board));
                    Assert.That(view.Labels.GetLabel(0).text, Is.EqualTo("19"));
                    Assert.That(view.Labels.TextWriteCount, Is.EqualTo(writes + 1));
                    Assert.That(samples, Is.EqualTo(2), "a warmed nonzero count change allocates only UI Toolkit's two numeral copies");
                    view.Sync(board);
                    Assert.That(view.Labels.TextWriteCount, Is.EqualTo(writes + 1));
                    Assert.That(view.Overlays.ActiveCount, Is.EqualTo(1));
                }
                finally { UnityEngine.Object.Destroy(root); }
            }
#endif
        }

        private static void VerifyGenericViews(SceneContext scope, ColorBlockJam.Gameplay.Levels.AttemptLevel level)
        {
            var journal = new List<string>();
            var first = new ProbeModule("first", journal);
            var second = new ProbeModule("second", journal);
            var source = new BlockEffectContextSource(new IBlockEffectModule[] { first, second });
            source.Prepare(level, Level());
            var hooks = new EffectAttemptHookSource(source);
            var simulation = new BoardSimulation(level.Model, hooks.Create(level), ColorBlockJam.Domain.Movement.DragSettings.Default);
            using (var presenter = new BoardPresenter(scope.Container.Resolve<EnvironmentView>(),
                scope.Container.Resolve<BoardArt>(), source, scope.Container.Resolve<BlockEffectViewContext>()))
            {
                Assert.That(presenter.TryBuild(level, out string error), Is.True, error);
                Assert.That(journal, Is.EqualTo(new[] { "first.prepare", "second.prepare" }));
                journal.Clear();
                presenter.Place(simulation.Preview);
                Assert.That(journal, Is.EqualTo(new[] { "first.bind", "second.bind", "first.attach0", "second.attach0", "first.attach1", "second.attach1" }));
                journal.Clear();
                presenter.Sync(simulation, default);
                presenter.ReprojectEffects();
                presenter.SetTweensPaused(true);
                presenter.StopTweens();
                Assert.That(journal, Is.EqualTo(new[] { "first.sync", "second.sync", "first.project", "second.project",
                    "first.pause", "second.pause", "first.stop", "second.stop" }));
                Assert.That(first.View.Board, Is.SameAs(simulation.Preview));
                Assert.That(second.View.Board, Is.SameAs(simulation.Preview));
                presenter.ReturnPieces();
                Assert.That(journal, Does.Contain("first.return"));
                Assert.That(journal, Does.Contain("second.return"));
                Assert.That(presenter.ActivePieceCount, Is.Zero);
            }
            Assert.That(first.View.Disposed, Is.True);
            Assert.That(second.View.Disposed, Is.True);

            // A later preparation failure must release already prepared views and leave no pieces.
            second.FailPrepare = true;
            journal.Clear();
            using (var presenter = new BoardPresenter(scope.Container.Resolve<EnvironmentView>(),
                scope.Container.Resolve<BoardArt>(), source, scope.Container.Resolve<BlockEffectViewContext>()))
            {
                Assert.That(presenter.TryBuild(level, out string error), Is.False);
                Assert.That(error, Does.Contain("Injected"));
                Assert.That(presenter.ActivePieceCount, Is.Zero);
                Assert.That(first.View.Disposed, Is.True);
                Assert.That(second.View.Disposed, Is.True);
                Assert.That(journal, Is.EqualTo(new[] { "first.prepare", "second.prepare", "first.dispose", "second.dispose" }));
            }
        }

        private sealed class ProbeModule : IBlockEffectModule
        {
            private readonly List<string> m_journal;
            public ProbeModule(string typeId, List<string> journal) { TypeId = typeId; m_journal = journal; }
            public string TypeId { get; }
            public bool FailPrepare;
            public ProbeView View;
            public IBlockEffectLevel Read(LevelData authored) => new ProbeMetadata(TypeId, authored.BlockCount);
            public IBlockEffectState AddHooks(IBlockEffectLevel level, AttemptHooksBuilder hooks) => new ProbeState(TypeId);
            public IBlockEffectView CreateView(BlockEffectViewContext context) => View = new ProbeView(this, m_journal);
        }

        private sealed class ProbeMetadata : IBlockEffectLevel
        {
            public ProbeMetadata(string typeId, int count) { TypeId = typeId; BlockCount = count; }
            public string TypeId { get; }
            public int BlockCount { get; }
        }

        private sealed class ProbeState : IBlockEffectState
        {
            public ProbeState(string typeId) { TypeId = typeId; }
            public string TypeId { get; }
        }

        private sealed class ProbeView : IBlockEffectView
        {
            private readonly ProbeModule m_module;
            private readonly List<string> m_journal;
            public ProbeView(ProbeModule module, List<string> journal) { m_module = module; m_journal = journal; }
            public string TypeId => m_module.TypeId;
            public IBoardReader Board;
            public bool Disposed;
            private void Add(string action) => m_journal.Add(TypeId + "." + action);
            public bool TryPrepare(BlockEffectContext context, IBlockEffectLevel metadata, out string error)
            {
                Assert.That(metadata.TypeId, Is.EqualTo(TypeId));
                Add("prepare");
                error = m_module.FailPrepare ? "Injected preparation failure" : null;
                return !m_module.FailPrepare;
            }
            public void BindAttempt(BlockEffectAttempt attempt, IBlockEffectState state)
            {
                Assert.That(state.TypeId, Is.EqualTo(TypeId));
                Add("bind");
            }
            public void Attach(int block, Transform root) { Assert.That(root, Is.Not.Null); Add("attach" + block); }
            public void Sync(IBoardReader board) { Board = board; Add("sync"); }
            public void Reproject() => Add("project");
            public void SetClipPlane(int block, Vector4 plane, bool enabled) => Add("clip" + block);
            public void SetPaused(bool paused) => Add("pause");
            public void StopTweens() => Add("stop");
            public void Detach(int block) => Add("detach" + block);
            public void ReturnAll() => Add("return");
            public void Dispose() { Disposed = true; Add("dispose"); }
        }

        private sealed class TestCountdown : ICountdown
        {
            public bool Expire;
            public bool Stopped;
            public float Remaining { get; private set; }
            public void Start(int seconds) { Remaining = seconds; Stopped = false; }
            public void SetPaused(bool paused) { }
            public void Stop() { Stopped = true; }
            public bool Tick(float deltaTime) => Expire;
        }

        private static LevelData Level(int count = 1) => new LevelData("ice-live", "Ice", 3, 3, 60,
            new[] { new LevelDoorData("red", BoardSide.Bottom, 0, 1, "Red"), new LevelDoorData("blue", BoardSide.Bottom, 2, 1, "Blue") },
            new[]
            {
                new LevelBlockData("ice", "Red", Vector2Int.zero, new[] { Vector2Int.zero },
                    new[] { new LevelEffectData("ice", new[] { new LevelEffectParamData("clearsToThaw", count.ToString()) }) }),
                new LevelBlockData("plain", "Blue", new Vector2Int(2, 0), new[] { Vector2Int.zero })
            });

        private static T Find<T>() where T : Component
        {
            foreach (GameObject root in SceneManager.GetSceneByName("Game").GetRootGameObjects())
            {
                T component = root.GetComponentInChildren<T>(true);
                if (component != null) return component;
            }
            return null;
        }
    }
}
