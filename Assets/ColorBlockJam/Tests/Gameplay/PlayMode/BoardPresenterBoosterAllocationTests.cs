#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using ColorBlockJam.Domain.Board;
using ColorBlockJam.Domain.Commands;
using ColorBlockJam.Domain.Session;
using ColorBlockJam.Gameplay.Board;
using ColorBlockJam.Presentation.Board;
using ColorBlockJam.Runtime.Art;
using ColorBlockJam.Runtime.Board;
using ColorBlockJam.Runtime.Levels;
using ColorBlockJam.Tests.Gameplay.PlayMode.Fixtures;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.TestTools.Constraints;
using UnityEngine.UIElements;
using Is = UnityEngine.TestTools.Constraints.Is;
using Object = UnityEngine.Object;

namespace ColorBlockJam.Tests.Gameplay.PlayMode
{
    /// <summary>
    /// Measures the Playing-time presenter paths of the booster commands (contract AC-16, B-19, B-20) with the
    /// Recorder-based Is.Not.AllocatingGCMemory: the command and view sync of a Rocket split of a frozen block,
    /// including a part slot whose shell and counter label are shown for the first time, a recompose into a new
    /// shape together with a cast view's first Play, a Hammer and a Vacuum removal with their casts, and the steady
    /// Sync with the Ice view attached. Owner-level cases prove that a warmed tile, Arrow marker or cast view used
    /// for the first time during Playing allocates nothing, because Home reads each one's Transform (the first
    /// managed access to a component creates its C# wrapper). Each path first runs once in a
    /// warm-up attempt (JIT, static lambda caches, tween and pool state); Restart then replays it on the second
    /// attempt, as the session does, with frames between so stopped tweens return to PrimeTween's pool. A split
    /// that shows a frozen part's counter is held to exactly UI Toolkit's own copy of each numeral a label is given
    /// (GcAllocations), one sample per label, which no prebuilt string avoids; everything else allocates nothing.
    /// That numeral copy is the documented UI Toolkit text exception of B-31 and PROJECT.md DEC-148.
    /// </summary>
    public sealed class BoardPresenterBoosterAllocationTests
    {
        private BoosterPresenterScene m_scene;
        private List<Label> m_labels;
        private string[] m_texts;

        [OneTimeSetUp]
        public void WarmUpTheAllocationRecorder()
        {
            // The first measurement in a domain also measures the recorder's own setup.
            Assert.That(() => { }, Is.Not.AllocatingGCMemory());
        }

        [SetUp]
        public void SetUp()
        {
            m_scene = new BoosterPresenterScene();
        }

        [TearDown]
        public void TearDown()
        {
            m_scene.Dispose();
        }

        [UnityTest]
        public IEnumerator AFrozenRocketSplitAllocatesOnlyUiToolkitsCopyOfThePartsNumeralEvenForAFreshPartSlot()
        {
            BoardSimulation simulation = m_scene.Build(TwoFrozenLinesLevel());
            BoardPresenter presenter = m_scene.Presenter;
            GameCommand splitLower = GameCommand.RemoveCell(0, new Cell(0, 0), new Cell(1, 0));
            GameCommand trimUpper = GameCommand.RemoveCell(1, new Cell(0, 2), new Cell(2, 2));
            GameCommand splitUpper = GameCommand.RemoveCell(1, new Cell(0, 2), new Cell(1, 2));

            // Warm-up attempt: the lower line's split shows part slot 2 once, and the upper line loses an end cell with
            // no split, so both Rocket views have played while part slot 3 has never been shown.
            Execute(simulation, splitLower);
            Execute(simulation, trimUpper);
            simulation = m_scene.Restart();
            // Home warms one block's worst split as the spare (DEC-184) and a prepared root holds no tiles (DEC-193), so
            // the second split below would grow the shared tile pool: the stated exception to no Instantiate during
            // Playing, not what this test measures. The pool stands as an earlier attempt with both splits left it.
            WarmTilesForBothSplits(presenter);
            yield return null;
            yield return null;

            // Part slot 2 again, then the upper line's split issues part 3: its shell, label and root come out of their
            // pools for the first time, with every code path already warm. The only samples allowed are UI Toolkit's
            // copies of the numerals the part's counter label and its fill label are given (GcAllocations).
            CommandOutcome lower = CommandOutcome.Applied;
            SnapshotLabels();
            int lowerSamples = GcAllocations.Count(() =>
            {
                simulation.Events.Clear();
                lower = simulation.Commands.Execute(splitLower);
                presenter.Sync(simulation, default);
                presenter.CompleteRocketHits();
            });
            Assert.That(lower, Is.EqualTo(CommandOutcome.Applied));
            Assert.That(CountNumeralWrites(), Is.EqualTo(2), "the part's label and its fill each show its inherited 2");
            Assert.That(lowerSamples, Is.EqualTo(2), "a split of a part slot shown in an earlier attempt");

            CommandOutcome upper = CommandOutcome.Applied;
            SnapshotLabels();
            int upperSamples = GcAllocations.Count(() =>
            {
                simulation.Events.Clear();
                upper = simulation.Commands.Execute(splitUpper);
                presenter.Sync(simulation, default);
                presenter.CompleteRocketHits();
            });
            Assert.That(upper, Is.EqualTo(CommandOutcome.Applied));
            Assert.That(CountNumeralWrites(), Is.EqualTo(2));
            Assert.That(upperSamples, Is.EqualTo(2), "a split of a part slot shown for the first time grows no inline style");

            Assert.That(simulation.Board.BlockCount, Is.EqualTo(4));
            Assert.That(presenter.GetPiece(3), Is.Not.Null, "the new part has its root");
            Assert.That(presenter.GetPiece(3).View.IsIced, Is.True, "and its inherited ice on its own tiles (DEC-184)");
            Label label = m_scene.Document.rootVisualElement.Q<Label>("ice-count-3");
            Assert.That(label.style.display.value, Is.EqualTo(DisplayStyle.Flex), "and its counter");
            Assert.That(label.text, Is.EqualTo("2"));
        }

        [UnityTest]
        public IEnumerator TilesAndACastViewFirstShownDuringPlayingAllocateNothing()
        {
            BoardSimulation simulation = m_scene.Build(BoardPresenterBoosterTests.SplitLevel());
            BoardPresenter presenter = m_scene.Presenter;
            GameCommand bridge = GameCommand.RemoveCell(0, new Cell(0, 0), new Cell(0, 1));
            GameCommand arm = GameCommand.RemoveCell(1, new Cell(3, 0), new Cell(4, 0));

            // Warm-up attempt: one Rocket view plays and the cross is never recomposed.
            Execute(simulation, bridge);
            simulation = m_scene.Restart();
            yield return null;
            yield return null;

            // The cross loses an arm: its root shows a T, with tiles that only Home's warm-up touched, and the second
            // Rocket view plays for the first time. The first managed access to a component creates its C# wrapper, so
            // Home reads every warmed tile's and cast view's Transform before Playing.
            Execute(simulation, bridge);
            Assert.That(presenter.ActiveCastCount, Is.EqualTo(1));
            CommandOutcome outcome = CommandOutcome.Applied;
            Assert.That(() =>
            {
                simulation.Events.Clear();
                outcome = simulation.Commands.Execute(arm);
                presenter.Sync(simulation, default);
                presenter.CompleteRocketHits();
            }, Is.Not.AllocatingGCMemory(), "a first recompose into a new shape and a cast view's first Play");
            Assert.That(outcome, Is.EqualTo(CommandOutcome.Applied));
            Assert.That(presenter.ActiveCastCount, Is.EqualTo(2), "both warmed Rocket views are in flight");
            Assert.That(presenter.GetPiece(1).Cells.Count, Is.EqualTo(4), "the cross shows its T");
        }

        [Test]
        public void AWarmedTileFirstShownByARocketRemainderAllocatesNothing()
        {
            // Home warms the shared tiles with both Squares' own tiles plus one L remainder's spare (DEC-184); an L shows
            // one OuterCorner more than the Square, so the fresh root's L takes a tile only Home's warm-up has touched.
            Vector2Int[] square = { new Vector2Int(0, 0), new Vector2Int(1, 0), new Vector2Int(0, 1), new Vector2Int(1, 1) };
            Vector2Int[] remainder = { new Vector2Int(0, 0), new Vector2Int(0, 1), new Vector2Int(1, 1) };
            BlockPieceFactory factory = new BlockPieceFactory(m_scene.Art, m_scene.Environment.BoardRoot);
            try
            {
                var tiles = new int[BlockPieceCapacity.KindCount];
                var spare = new int[BlockPieceCapacity.KindCount];
                BlockPieceCapacity.AddTileBudget(square, tiles, spare);
                BlockPieceCapacity.AddTileBudget(square, tiles, spare);
                // Both roots take their remainder in this test, so the spare is counted once per root.
                for (int kind = 0; kind < tiles.Length; kind++) tiles[kind] += 2 * spare[kind];
                factory.Prepare(2, square.Length);
                factory.WarmTiles(tiles);
                BlockPiece warm = factory.Create(new BlockPieceSpawnData(PaletteColor.Red, square));
                BlockPiece fresh = factory.Create(new BlockPieceSpawnData(PaletteColor.Red, square));
                Assert.That(warm.TrySetCells(remainder), Is.True, "the same call once on another root warms the code path");

                Assert.That(() => { fresh.TrySetCells(remainder); }, Is.Not.AllocatingGCMemory());
                Assert.That(fresh.Cells, Is.EquivalentTo(remainder));
                factory.Return(warm);
                factory.Return(fresh);
            }
            finally
            {
                factory.Dispose();
            }
        }

        [Test]
        public void AWarmedArrowMarkerFirstAttachedToARocketPartAllocatesNothing()
        {
            Vector2Int[] single = { Vector2Int.zero };
            Transform first = new GameObject("First marker root").transform;
            Transform second = new GameObject("Second marker root").transform;
            BlockArrowMarkers warm = new BlockArrowMarkers(m_scene.Art.Catalog, m_scene.Environment.BoardRoot);
            BlockArrowMarkers fresh = new BlockArrowMarkers(m_scene.Art.Catalog, m_scene.Environment.BoardRoot);
            try
            {
                warm.Prepare(new[] { 1, 0, 0 });
                fresh.Prepare(new[] { 2, 0, 0 });
                Assert.That(warm.TryAttach(first, single, LevelEffectAxis.Horizontal, AtlasColor.FromCell(Vector2Int.zero), out BoardPieceView marker), Is.True,
                    "the same call once on another owner warms the code path");
                warm.Return(marker);

                // Neither of the fresh owner's markers has been attached before.
                Assert.That(() =>
                {
                    fresh.TryAttach(first, single, LevelEffectAxis.Horizontal, AtlasColor.FromCell(Vector2Int.zero), out _);
                    fresh.TryAttach(second, single, LevelEffectAxis.Vertical, AtlasColor.FromCell(Vector2Int.zero), out _);
                }, Is.Not.AllocatingGCMemory());
                Assert.That(fresh.AttachedCount, Is.EqualTo(2));
                fresh.ReturnAll();
            }
            finally
            {
                warm.Dispose();
                fresh.Dispose();
                Object.DestroyImmediate(first.gameObject);
                Object.DestroyImmediate(second.gameObject);
            }
        }

        [Test]
        public void EachWarmedCastViewsFirstPlayAllocatesNothing()
        {
            Transform boardRoot = m_scene.Environment.BoardRoot;
            Vector3 launch = boardRoot.TransformPoint(new Vector3(1f, 0f, -2f));
            Vector3 cell = boardRoot.TransformPoint(new Vector3(1f, ExitMotionSettings.k_blockTopHeight, 1f));
            BoosterCasts warm = new BoosterCasts(m_scene.Catalog, boardRoot);
            BoosterCasts fresh = new BoosterCasts(m_scene.Catalog, boardRoot);
            try
            {
                // The same calls once on another owner warm the code paths; the fresh owner's six views never played.
                warm.Prepare();
                fresh.Prepare();
                PlayEveryCast(warm, launch, cell);
                warm.Stop();
                warm.ReturnAll();

                Assert.That(() => { PlayEveryCast(fresh, launch, cell); }, Is.Not.AllocatingGCMemory());
                Assert.That(fresh.ActiveCount, Is.EqualTo(fresh.PreparedCount), "every warmed view is playing");
                fresh.Stop();
                fresh.ReturnAll();
            }
            finally
            {
                warm.Dispose();
                fresh.Dispose();
            }
        }

        [UnityTest]
        public IEnumerator AHammerRemovalOfAFrozenBlockAndItsCastAllocateNothing()
        {
            BoardSimulation simulation = m_scene.Build(BoardPresenterBoosterTests.HammerLevel(ice: true));
            BoardPresenter presenter = m_scene.Presenter;
            GameCommand hammer = GameCommand.RemoveBlock(0, new Cell(0, 0));

            Execute(simulation, hammer);
            simulation = m_scene.Restart();
            yield return null;
            yield return null;

            CommandOutcome outcome = CommandOutcome.Applied;
            Assert.That(() =>
            {
                simulation.Events.Clear();
                outcome = simulation.Commands.Execute(hammer);
                presenter.Sync(simulation, default);
            }, Is.Not.AllocatingGCMemory());
            Assert.That(outcome, Is.EqualTo(CommandOutcome.Applied));
            Assert.That(presenter.ActiveCastCount, Is.EqualTo(1), "the Hammer cast plays");
            Assert.That(presenter.GetPiece(0), Is.Not.Null, "the removed root is kept for its removal");
        }

        [UnityTest]
        public IEnumerator AVacuumRemovalOfEveryMatchingBlockAndItsCastAllocateNothing()
        {
            BoardSimulation simulation = m_scene.Build(BoardPresenterBoosterTests.VacuumLevel());
            BoardPresenter presenter = m_scene.Presenter;
            GameCommand vacuum = GameCommand.RemoveColor(0, new Cell(0, 0), simulation.Board.GetFootprint(0).Color);

            Execute(simulation, vacuum);
            simulation = m_scene.Restart();
            yield return null;
            yield return null;

            CommandOutcome outcome = CommandOutcome.Applied;
            Assert.That(() =>
            {
                simulation.Events.Clear();
                outcome = simulation.Commands.Execute(vacuum);
                presenter.Sync(simulation, default);
            }, Is.Not.AllocatingGCMemory());
            Assert.That(outcome, Is.EqualTo(CommandOutcome.Applied));
            Assert.That(presenter.ActiveCastCount, Is.EqualTo(1), "the Vacuum cast plays");
            Assert.That(presenter.GetPiece(0), Is.Not.Null);
            Assert.That(presenter.GetPiece(1), Is.Not.Null);
        }

        [UnityTest]
        public IEnumerator TheSteadySyncWithTheIceViewAttachedAllocatesNothingBeforeAndAfterASplit()
        {
            BoardSimulation simulation = m_scene.Build(BoardPresenterBoosterTests.HammerLevel(ice: true));
            BoardPresenter presenter = m_scene.Presenter;
            simulation.Events.Clear();
            presenter.Sync(simulation, default);
            yield return null;

            Assert.That(() =>
            {
                for (int frame = 0; frame < 100; frame++) presenter.Sync(simulation, default);
            }, Is.Not.AllocatingGCMemory(), "the frozen board before any booster");

            // A part attached to the Ice view and a Rocket cast in flight are synced every frame too.
            Execute(simulation, GameCommand.RemoveCell(0, new Cell(0, 0), new Cell(1, 0)));
            simulation.Events.Clear();
            presenter.Sync(simulation, default);
            yield return null;

            Assert.That(() =>
            {
                for (int frame = 0; frame < 100; frame++) presenter.Sync(simulation, default);
            }, Is.Not.AllocatingGCMemory(), "the frozen board after a split");
            Assert.That(presenter.GetPiece(2), Is.Not.Null);
        }

        // Two of each cast kind at once, as overlapping boosters would play them.
        private static void PlayEveryCast(BoosterCasts casts, Vector3 launch, Vector3 cell)
        {
            casts.TryPlayRocket(launch, Quaternion.identity, cell, Color.red);
            casts.TryPlayRocket(launch, Quaternion.identity, cell, Color.red);
            casts.TryPlayHammer(cell, Quaternion.identity, Color.blue, launch, out _);
            casts.TryPlayHammer(cell, Quaternion.identity, Color.blue, launch, out _);
            casts.TryPlayVacuum(launch, Quaternion.identity, out _, out _, out _);
            casts.TryPlayVacuum(launch, Quaternion.identity, out _, out _, out _);
        }

        // Records every label's text before a measured frame; Query allocates, so it runs outside the measurement.
        private void SnapshotLabels()
        {
            m_labels = m_scene.Document.rootVisualElement.Query<Label>().ToList();
            m_texts = new string[m_labels.Count];
            for (int index = 0; index < m_texts.Length; index++) m_texts[index] = m_labels[index].text;
        }

        // The labels given a new non-empty numeral since SnapshotLabels; an emptied or unchanged label copies nothing.
        private int CountNumeralWrites()
        {
            int writes = 0;
            for (int index = 0; index < m_texts.Length; index++)
            {
                string text = m_labels[index].text;
                if (!string.IsNullOrEmpty(text) && text != m_texts[index]) writes++;
            }

            return writes;
        }

        // One tick's command phase and view sync phase, as the session runs them.
        private void Execute(BoardSimulation simulation, GameCommand command)
        {
            simulation.Events.Clear();
            Assert.That(simulation.Commands.Execute(command), Is.EqualTo(CommandOutcome.Applied), command.ToString());
            m_scene.Presenter.Sync(simulation, default);
            // A Rocket split shows at its contact (B-20); these tests measure what it shows, not when.
            m_scene.Presenter.CompleteRocketHits();
        }

        // 4 x 4: a red line of three on the bottom row and a blue one on row 2, both frozen for two exits.
        // Grows the presenter's shared tile pool to both lines' own tiles plus each line's worst split, so two splits
        // in one attempt take only warmed tiles.
        private static void WarmTilesForBothSplits(BoardPresenter presenter)
        {
            Vector2Int[] line = { new Vector2Int(0, 0), new Vector2Int(1, 0), new Vector2Int(2, 0) };
            var tiles = new int[BlockPieceCapacity.KindCount];
            var spare = new int[BlockPieceCapacity.KindCount];
            BlockPieceCapacity.AddTileBudget(line, tiles, spare);
            BlockPieceCapacity.AddTileBudget(line, tiles, spare);
            for (int kind = 0; kind < tiles.Length; kind++) tiles[kind] += 2 * spare[kind];
            FieldInfo field = typeof(BoardPresenter).GetField("m_factory", BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.That(field, Is.Not.Null, "BoardPresenter.m_factory");
            ((BlockPieceFactory)field.GetValue(presenter)).WarmTiles(tiles);
        }

        private static LevelData TwoFrozenLinesLevel()
        {
            LevelEffectData[] ice = { new LevelEffectData("ice", new[] { new LevelEffectParamData("clearsToThaw", "2") }) };
            Vector2Int[] line = { new Vector2Int(0, 0), new Vector2Int(1, 0), new Vector2Int(2, 0) };
            return new LevelData("booster-two-ice", "Booster two ice", 4, 4, 60,
                new[] { new LevelDoorData("red", BoardSide.Top, 0, 1, "Red") }, new[]
                {
                    new LevelBlockData("lower", "Red", Vector2Int.zero, line, ice),
                    new LevelBlockData("upper", "Blue", new Vector2Int(0, 2), line, ice)
                });
        }
    }
}
#endif
