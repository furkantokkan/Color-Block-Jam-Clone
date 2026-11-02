#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using ColorBlockJam.Domain.Board;
using ColorBlockJam.Domain.Commands;
using ColorBlockJam.Domain.Session;
using ColorBlockJam.Gameplay.Board;
using ColorBlockJam.Gameplay.Effects;
using ColorBlockJam.Gameplay.Input;
using ColorBlockJam.Gameplay.Levels;
using ColorBlockJam.Gameplay.Session;
using ColorBlockJam.Presentation.Board;
using ColorBlockJam.Presentation.Boosters;
using ColorBlockJam.Runtime.Art;
using ColorBlockJam.Runtime.Board;
using ColorBlockJam.Runtime.Layout;
using ColorBlockJam.Runtime.Levels;
using ColorBlockJam.Tests.Gameplay.PlayMode.Fixtures;
using NUnit.Framework;
using PrimeTween;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.TestTools.Utils;
using UnityEngine.UIElements;
using SessionState = ColorBlockJam.Domain.Session.SessionState;

namespace ColorBlockJam.Tests.Gameplay.PlayMode
{
    /// <summary>
    /// Proves the board presentation of the booster commands (B-10, B-19, B-20; contract AC-5, AC-6, AC-8, AC-9,
    /// AC-15, AC-16) with the real environment and cast prefabs: Rocket splits recompose the parent's root and
    /// spawn each part's root, in its block's colour, from the warmed root pool without growth, a part exits a
    /// one-wide door with its own depth, the Rocket flies from below the
    /// board to the removed cell's top, the Hammer lands on its block's top centre facing the camera and the Vacuum
    /// stands below the board facing it, Hammer and Vacuum targets keep their roots until their casts' contact and
    /// intake windows and within the 0.8 s Won Finish, a final Vacuum runs inside the session's bounded Finish,
    /// Suspend holds casts and removal tweens, Teardown stops and returns them, and Ice parts inherit their ice and
    /// counter on their own cells, while a Hammer takes a frozen block's ice with it (DEC-184).
    /// </summary>
    public sealed class BoardPresenterBoosterTests
    {
        private const float k_finishBoundSeconds = 0.8f;
        private const float k_frameSeconds = 1f / 60f;

        private static readonly int s_colorId = Shader.PropertyToID("_PaletteUV");

        // The booster bar's top centre as a viewport point, near the bottom of the screen (FixedCastAnchor).
        private static readonly Vector2 s_barAnchor = new Vector2(0.5f, 0.15f);

        private BoosterPresenterScene m_scene;
        private LevelSession m_session;
        private PrimeTweenFinishDeadline m_finishDeadline;

        [SetUp]
        public void SetUp()
        {
            m_scene = new BoosterPresenterScene();
        }

        [TearDown]
        public void TearDown()
        {
            m_session?.Dispose();
            m_session = null;
            m_finishDeadline?.Dispose();
            m_finishDeadline = null;
            m_scene.Dispose();
        }

        [Test]
        public void RepeatedRocketSplitsRecomposeAndSpawnRootsWithoutPoolGrowth()
        {
            BoardSimulation simulation = m_scene.Build(SplitLevel());
            BoardPresenter presenter = m_scene.Presenter;
            int prepared = presenter.PreparedPieceCount;
            int pooled = BoosterPresenterScene.CountPooledObjects();
            int objects = m_scene.CountBoardObjects();
            int casts = presenter.PreparedCastCount;
            Assert.That(casts, Is.EqualTo(3 * BoosterCasts.k_instancesPerKind), "Home warms two views per cast kind.");

            // The L's bridge: the parent keeps its first cell and part 2 takes the foot (B-4).
            Execute(simulation, GameCommand.RemoveCell(0, new Cell(0, 0), new Cell(0, 1)));
            AssertShowsRuntimeFootprints(simulation);
            Assert.That(presenter.GetPiece(0).Cells, Is.EquivalentTo(new[] { Vector2Int.zero }), "the parent is recomposed at once");
            Assert.That(presenter.GetPiece(2).Cells, Is.EquivalentTo(new[] { new Vector2Int(0, 2), new Vector2Int(1, 2) }));
            Assert.That(presenter.GetPiece(2).transform.localPosition, Is.EqualTo(presenter.GetPiece(0).transform.localPosition),
                "a part keeps its parent's origin, so no cell moves");
            Vector4 red = m_scene.Art.GetAtlasColor(PaletteColor.Red).UV;
            Vector4 blue = m_scene.Art.GetAtlasColor(PaletteColor.Blue).UV;
            AssertTint(presenter.GetPiece(0), red, "the recomposed parent's tiles keep its colour");
            AssertTint(presenter.GetPiece(2), red, "a part takes its parent's colour");

            // The cross's centre leaves four parts; the part of the L loses its end cell and stays whole.
            Execute(simulation, GameCommand.RemoveCell(1, new Cell(3, 0), new Cell(4, 1)));
            Execute(simulation, GameCommand.RemoveCell(2, new Cell(0, 0), new Cell(0, 2)));
            AssertShowsRuntimeFootprints(simulation);
            Assert.That(simulation.Board.BlockCount, Is.EqualTo(6));
            Assert.That(presenter.GetPiece(2).Cells, Is.EquivalentTo(new[] { new Vector2Int(1, 2) }));
            AssertTint(presenter.GetPiece(2), red, "a part recomposed again keeps its colour");
            AssertTint(presenter.GetPiece(1), blue, "the cross keeps its colour on its remaining cell");
            for (int block = 3; block < 6; block++)
            {
                AssertTint(presenter.GetPiece(block), blue, "part " + block);
            }

            Assert.That(presenter.ActivePieceCount, Is.EqualTo(6), "one root per live block");
            Assert.That(presenter.PreparedPieceCount, Is.EqualTo(prepared), "no root pool grew");
            // Home warms one block's worst split as the spare (DEC-184) and a prepared root holds no tiles (DEC-193);
            // the L and the cross split here before any tile comes back, so the shared tile pool may grow by the
            // missing tiles, which then stay in it.
            Assert.That(BoosterPresenterScene.CountPooledObjects(), Is.GreaterThanOrEqualTo(pooled));
            int grownPooled = BoosterPresenterScene.CountPooledObjects();
            int grownObjects = m_scene.CountBoardObjects();
            Assert.That(grownObjects, Is.GreaterThanOrEqualTo(objects));
            Assert.That(presenter.PreparedCastCount, Is.EqualTo(casts), "a third Rocket in flight is skipped, not grown");
            Assert.That(presenter.ActiveCastCount, Is.EqualTo(BoosterCasts.k_instancesPerKind));

            // Teardown and Restart: every root returns and the authored footprints come back.
            simulation = m_scene.Restart();
            Assert.That(presenter.ActiveCastCount, Is.Zero);
            Assert.That(presenter.ActivePieceCount, Is.EqualTo(2));
            Assert.That(presenter.GetPiece(0).Cells.Count, Is.EqualTo(4), "Restart shows the authored L again");
            AssertShowsRuntimeFootprints(simulation);
            Assert.That(presenter.PreparedPieceCount, Is.EqualTo(prepared));
            Assert.That(m_scene.CountBoardObjects(), Is.EqualTo(grownObjects), "Restart returns every tile to the pool");

            // The same splits again reuse the tiles the first attempt grew: nothing is created a second time.
            Execute(simulation, GameCommand.RemoveCell(0, new Cell(0, 0), new Cell(0, 1)));
            Execute(simulation, GameCommand.RemoveCell(1, new Cell(3, 0), new Cell(4, 1)));
            Execute(simulation, GameCommand.RemoveCell(2, new Cell(0, 0), new Cell(0, 2)));
            AssertShowsRuntimeFootprints(simulation);
            Assert.That(BoosterPresenterScene.CountPooledObjects(), Is.EqualTo(grownPooled), "grown tiles stay in the pool");
            Assert.That(m_scene.CountBoardObjects(), Is.EqualTo(grownObjects));
            Assert.That(presenter.PreparedPieceCount, Is.EqualTo(prepared));
        }

        [UnityTest]
        public IEnumerator ARocketSplitShowsOnlyOnceTheNoseTouchesTheCell()
        {
            BoardSimulation simulation = m_scene.Build(SplitLevel());
            BoardPresenter presenter = m_scene.Presenter;

            // The L's bridge: the domain splits at once, the roots wait for the Rocket (B-20).
            ExecuteWaiting(simulation, GameCommand.RemoveCell(0, new Cell(0, 0), new Cell(0, 1)));
            Assert.That(simulation.Board.BlockCount, Is.EqualTo(3), "the domain never waits for the cast");
            RocketEffectView rocket = FindPlayingCast<RocketEffectView>();
            Assert.That(rocket, Is.Not.Null);
            float contact = rocket.Duration * RocketEffectView.k_noseFraction;
            Assert.That(presenter.GetPiece(0).Cells.Count, Is.EqualTo(4), "the whole L shows while the Rocket flies");
            Assert.That(presenter.GetPiece(2), Is.Null, "the new part has no root before the contact");

            yield return RunFor(simulation, contact * 0.5f);
            Assert.That(presenter.GetPiece(0).Cells.Count, Is.EqualTo(4), "still whole halfway");

            yield return RunUntil(simulation, () => presenter.GetPiece(2) != null, contact + 0.5f);
            Assert.That(presenter.GetPiece(0).Cells, Is.EquivalentTo(new[] { Vector2Int.zero }), "the L breaks at the contact");
            AssertShowsRuntimeFootprints(simulation);
        }

        [Test]
        public void HoldingOrRestartingDuringARocketFlightNeverShowsAStaleFootprint()
        {
            BoardSimulation simulation = m_scene.Build(SplitLevel());
            BoardPresenter presenter = m_scene.Presenter;

            ExecuteWaiting(simulation, GameCommand.RemoveCell(0, new Cell(0, 0), new Cell(0, 1)));
            Assert.That(presenter.GetPiece(2), Is.Null);
            presenter.SetHeld(2);
            Assert.That(presenter.GetPiece(2), Is.Not.Null, "holding a waiting part shows the split first");
            AssertShowsRuntimeFootprints(simulation);
            presenter.SetHeld(-1);

            ExecuteWaiting(simulation, GameCommand.RemoveCell(1, new Cell(3, 0), new Cell(4, 1)));
            Assert.That(presenter.GetPiece(1).Cells.Count, Is.EqualTo(5), "the cross waits for its Rocket");
            simulation = m_scene.Restart();
            Assert.That(presenter.ActivePieceCount, Is.EqualTo(2), "Teardown drops the waiting split");
            Assert.That(presenter.GetPiece(1).Cells.Count, Is.EqualTo(5));
            AssertShowsRuntimeFootprints(simulation);
        }

        [Test]
        public void EachRocketLaunchesBelowTheBoardAndItsNoseReachesTheRemovedCellsTop()
        {
            BoardSimulation simulation = m_scene.Build(SplitLevel());
            BoardPresenter presenter = m_scene.Presenter;

            // The L's foot is an end cell: the L is recomposed as an I with no new part, and one Rocket flies.
            Execute(simulation, GameCommand.RemoveCell(0, new Cell(0, 0), new Cell(1, 2)));
            Assert.That(simulation.Board.BlockCount, Is.EqualTo(2), "an end cell splits nothing");
            Assert.That(presenter.GetPiece(0).Cells, Is.EquivalentTo(new[] { new Vector2Int(0, 0), new Vector2Int(0, 1), new Vector2Int(0, 2) }));
            Assert.That(presenter.ActiveCastCount, Is.EqualTo(1), "a Rocket that splits nothing still plays");
            RocketEffectView first = FindPlayingCast<RocketEffectView>();
            Assert.That(first, Is.Not.Null);
            AssertRocketFromBelowTheBoardTo(first, new Cell(1, 2));

            // The cross stands at (3, 0): its centre is the board cell (4, 1), not its relative cell (1, 1).
            Execute(simulation, GameCommand.RemoveCell(1, new Cell(3, 0), new Cell(4, 1)));
            Assert.That(presenter.ActiveCastCount, Is.EqualTo(2));
            RocketEffectView second = null;
            for (int index = 0; index < presenter.PreparedCastCount; index++)
            {
                if (presenter.GetPreparedCast(index) is RocketEffectView rocket && rocket.IsPlaying && rocket != first)
                {
                    second = rocket;
                }
            }

            Assert.That(second, Is.Not.Null, "the second Rocket uses the other warmed view");
            AssertRocketFromBelowTheBoardTo(second, new Cell(4, 1));
        }

        [Test]
        public void WithACastAnchorTheRocketLaunchesFromAboveTheBoosterBar()
        {
            BoardSimulation simulation = m_scene.Build(SplitLevel());
            m_scene.Presenter.SetCastAnchor(new FixedCastAnchor());

            Execute(simulation, GameCommand.RemoveCell(0, new Cell(0, 0), new Cell(1, 2)));
            RocketEffectView rocket = FindPlayingCast<RocketEffectView>();

            Assert.That(rocket, Is.Not.Null);
            AssertOnFloorUnderTheBar(rocket.transform.position, "the Rocket launches above the booster bar");
            AssertRocketNoseReaches(rocket, new Cell(1, 2));
        }

        [Test]
        public void WithACastAnchorTheHammerFliesInFromAboveTheBoosterBarAndStillStrikesTheBlocksTop()
        {
            BoardSimulation simulation = m_scene.Build(HammerLevel(ice: false));
            m_scene.Presenter.SetCastAnchor(new FixedCastAnchor());

            Execute(simulation, GameCommand.RemoveBlock(0, new Cell(0, 0)));
            HammerEffectView hammer = FindPlayingCast<HammerEffectView>();

            Assert.That(hammer, Is.Not.Null);
            Vector3 top = m_scene.Environment.BoardRoot.TransformPoint(m_scene.Level.Layout.CellToLocal(Vector2Int.zero)
                + new Vector3(1f, ExitMotionSettings.k_blockTopHeight, 0f));
            AssertNear(hammer.transform.position, top, "the root still stands on the block's top centre");

            Transform visual = hammer.transform.Find("Visual");
            hammer.Suspend();
            hammer.SampleForPreview(0f);
            AssertOnFloorUnderTheBar(visual.position, "the mallet starts above the booster bar");

            // HammerEffect.prefab's m_strikePoint, the mallet face, lands on the root at the strike.
            hammer.SampleForPreview(HammerEffectView.k_strikeFraction);
            AssertNear(visual.TransformPoint(new Vector3(1f, 1.61f, -0.55f)), top, "its face lands on the block's top");
        }

        [Test]
        public void WithACastAnchorTheVacuumCanisterStandsOnTheBoosterBar()
        {
            BoardSimulation simulation = m_scene.Build(VacuumLevel());
            m_scene.Presenter.SetCastAnchor(new FixedCastAnchor());

            Execute(simulation, GameCommand.RemoveColor(0, new Cell(0, 0), simulation.Board.GetFootprint(0).Color));
            VacuumEffectView vacuum = FindPlayingCast<VacuumEffectView>();

            Assert.That(vacuum, Is.Not.Null);
            Assert.That(vacuum.BaseDepth, Is.GreaterThan(0f));
            AssertVacuumStandsInFrontOn(vacuum, s_barAnchor, "the canister's bottom stands on the booster bar");
            Assert.That(Quaternion.Angle(vacuum.transform.rotation, m_scene.Camera.transform.rotation), Is.LessThan(0.01f),
                "the flat Vacuum faces the camera");
            Assert.That(m_scene.Camera.WorldToViewportPoint(vacuum.ReadyIntakePosition).y, Is.GreaterThan(s_barAnchor.y),
                "the intake rises above the bar towards the board");
        }

        [UnityTest]
        public IEnumerator ARocketPartExitsAOneWideDoorWithTravelFromItsOwnDepth()
        {
            BoardSimulation simulation = m_scene.Build(PartExitLevel());
            BoardPresenter presenter = m_scene.Presenter;
            Execute(simulation, GameCommand.RemoveCell(0, new Cell(0, 1), new Cell(1, 1)));
            Transform part = presenter.GetPiece(2).transform;

            // The part (2, 1) is one cell deep, flush with the one-wide red door on the right.
            Execute(simulation, GameCommand.MoveAndExit(2, new Cell(0, 1), new Cell(0, 1), Side.Right));
            Assert.That(Tween.GetTweensCount(part), Is.EqualTo(1));
            Assert.That(presenter.ActiveExitFragmentCount, Is.EqualTo(1), "the part bursts out of its door");

            // Wait without a sync, so the root is still checked out when its exit tween ends.
            float deadline = Time.realtimeSinceStartup + ExitMotionSettings.k_maxSeconds + 1f;
            while (Tween.GetTweensCount(part) > 0 && Time.realtimeSinceStartup < deadline)
            {
                yield return null;
            }

            // depth + 1 = 2 cells past its origin's column: the authored depth of 3 would end at x 4.
            Vector3 origin = m_scene.Level.Layout.CellToLocal(new Vector2Int(0, 1));
            Assert.That(Tween.GetTweensCount(part), Is.Zero);
            Assert.That(part.localPosition.x, Is.EqualTo(origin.x + 2f).Within(1e-4f));
            Sync(simulation);
            Assert.That(presenter.GetPiece(2), Is.Null, "the part's root returned once its exit ended");
            Assert.That(presenter.GetPiece(0), Is.Not.Null, "the parent stays");
        }

        [UnityTest]
        public IEnumerator AHammerTargetKeepsItsRootUntilTheStrikeThenShrinksWithinTheFinishBound()
        {
            BoardSimulation simulation = m_scene.Build(HammerLevel(ice: false));
            BoardPresenter presenter = m_scene.Presenter;
            Transform root = presenter.GetPiece(0).transform;

            // A camera turned about the vertical axis, so facing it differs from the board's own rotation.
            m_scene.Camera.transform.rotation = Quaternion.Euler(75f, 30f, 0f);
            Execute(simulation, GameCommand.RemoveBlock(0, new Cell(0, 0)));
            float start = Time.realtimeSinceStartup;
            HammerEffectView hammer = FindPlayingCast<HammerEffectView>();

            Assert.That(hammer, Is.Not.Null, "the Hammer cast plays");

            // B-20: the root stands on the top centre of the line's footprint box, (1, 0), turned to show its swing
            // plane to the camera.
            Vector3 top = m_scene.Environment.BoardRoot.TransformPoint(m_scene.Level.Layout.CellToLocal(Vector2Int.zero)
                + new Vector3(1f, ExitMotionSettings.k_blockTopHeight, 0f));
            Assert.That(Vector3.Distance(hammer.transform.position, top), Is.LessThan(1e-4f), "the Hammer lands on the block's top centre");
            Vector3 facing = Vector3.ProjectOnPlane(m_scene.Camera.transform.forward, Vector3.up).normalized;
            Assert.That(Vector3.Dot(hammer.transform.forward, facing), Is.EqualTo(1f).Within(1e-4f), "it faces the camera on the ground plane");
            Assert.That(Vector3.Dot(hammer.transform.up, Vector3.up), Is.EqualTo(1f).Within(1e-4f), "and stays upright");
            Assert.That(presenter.GetPiece(0), Is.Not.Null, "the removed block keeps its root for its removal");
            float contact = hammer.Duration * HammerEffectView.k_strikeFraction;
            Assert.That(presenter.FinishSeconds, Is.GreaterThan(contact).And.LessThanOrEqualTo(k_finishBoundSeconds));

            yield return RunFor(simulation, contact * 0.5f);
            Assert.That(root.localScale, Is.EqualTo(Vector3.one), "the block waits for the mallet's face");
            Assert.That(presenter.GetPiece(0), Is.Not.Null);

            float longestFrame = 0f;
            while (presenter.GetPiece(0) != null && Time.realtimeSinceStartup - start < 2f)
            {
                yield return null;
                longestFrame = Mathf.Max(longestFrame, Time.unscaledDeltaTime);
                Sync(simulation);
            }

            float returned = Time.realtimeSinceStartup - start;
            Assert.That(presenter.GetPiece(0), Is.Null, "the root returned after its shrink");
            Assert.That(returned, Is.GreaterThanOrEqualTo(contact - longestFrame), "not before the strike");
            Assert.That(returned, Is.LessThanOrEqualTo(k_finishBoundSeconds + longestFrame));
            // The cast returns once its hit burst (DEC-152), which outlives the motion, has ended too.
            yield return RunUntil(simulation, () => presenter.ActiveCastCount == 0,
                hammer.ParticleStartSeconds + hammer.ParticleSeconds + 0.3f);
            Assert.That(Tween.GetTweensCount(hammer), Is.Zero);
            Assert.That(presenter.FinishSeconds, Is.Zero);
            Assert.That(presenter.GetPiece(1), Is.Not.Null, "the other block is unchanged");
        }

        [UnityTest]
        public IEnumerator AVacuumOutlinesEveryMatchingRootAndDrawsItIntoTheIntake()
        {
            BoardSimulation simulation = m_scene.Build(VacuumLevel());
            BoardPresenter presenter = m_scene.Presenter;
            Transform first = presenter.GetPiece(0).transform;
            Transform second = presenter.GetPiece(1).transform;
            BlockColor red = simulation.Board.GetFootprint(0).Color;
            Vector4 tint = ReadColor(first);

            m_scene.Camera.transform.rotation = Quaternion.Euler(75f, 30f, 0f);
            Execute(simulation, GameCommand.RemoveColor(0, new Cell(0, 0), red));
            float start = Time.realtimeSinceStartup;
            VacuumEffectView vacuum = FindPlayingCast<VacuumEffectView>();

            Assert.That(vacuum, Is.Not.Null, "the Vacuum cast plays");
            Assert.That(presenter.ActiveCastCount, Is.EqualTo(1), "one cast per command, with no crosshair");

            // B-20, without a cast anchor: on screen the canister stands below the middle of the board's lower edge,
            // facing the camera, in front of the near wall tops.
            Transform boardRoot = m_scene.Environment.BoardRoot;
            Vector3 launch = boardRoot.TransformPoint(GetCastLaunchLocal());
            AssertVacuumStandsInFrontOn(vacuum, ToViewport(launch), "the canister stands below the board's lower edge");
            Assert.That(Quaternion.Angle(vacuum.transform.rotation, m_scene.Camera.transform.rotation), Is.LessThan(0.01f),
                "the flat Vacuum faces the camera");
            Assert.That(ReadColor(first), Is.EqualTo(tint), "the tapped block keeps its colour; the outline marks it");
            Assert.That(tint, Is.Not.EqualTo(Color.white));
            // Every block the Vacuum pulls shows the white outline, not only the tapped one (DEC-145).
            Assert.That(first.GetComponent<HighlightPlus.HighlightEffect>().highlighted, Is.True, "the tapped block is outlined");
            Assert.That(second.GetComponent<HighlightPlus.HighlightEffect>().highlighted, Is.True, "every pulled block is outlined");
            Assert.That(presenter.GetPiece(0), Is.Not.Null);
            Assert.That(presenter.GetPiece(1), Is.Not.Null);
            Assert.That(presenter.FinishSeconds, Is.GreaterThan(0f).And.LessThanOrEqualTo(k_finishBoundSeconds));
            Vector3 intake = vacuum.ReadyIntakePosition;
            float before = Vector3.Distance(second.position, intake);

            yield return RunFor(simulation, vacuum.Duration * 0.6f);
            Assert.That(second.localScale.x, Is.LessThan(1f), "pieces shrink while the intake faces the board");
            Assert.That(Vector3.Distance(second.position, intake), Is.LessThan(before), "pieces move into the nozzle");

            float longestFrame = 0f;
            float worstSizeGap = 0f;
            while ((presenter.GetPiece(0) != null || presenter.GetPiece(1) != null) && Time.realtimeSinceStartup - start < 2f)
            {
                yield return null;
                longestFrame = Mathf.Max(longestFrame, Time.unscaledDeltaTime);
                // A piece shrinks as it advances: its size is its remaining share of the way to the intake.
                if (presenter.GetPiece(1) != null)
                {
                    float remaining = Vector3.Distance(second.position, intake) / before;
                    worstSizeGap = Mathf.Max(worstSizeGap, Mathf.Abs(second.localScale.x - remaining));
                }

                Sync(simulation);
            }

            Assert.That(worstSizeGap, Is.LessThan(0.01f), "the size follows the remaining distance to the intake");

            Assert.That(presenter.GetPiece(0), Is.Null);
            Assert.That(presenter.GetPiece(1), Is.Null);
            Assert.That(Time.realtimeSinceStartup - start, Is.LessThanOrEqualTo(vacuum.Duration * VacuumEffectView.k_intakeEndFraction
                + 2f * longestFrame + 0.05f), "the removal ends inside the intake window");
            Assert.That(presenter.GetPiece(2), Is.Not.Null, "another colour remains");
            yield return RunUntil(simulation, () => presenter.ActiveCastCount == 0, 1f);
            Assert.That(presenter.FinishSeconds, Is.Zero);
        }

        [UnityTest]
        public IEnumerator SuspendHoldsCastsAndRemovalsAndTeardownStopsAndReturnsThem()
        {
            BoardSimulation simulation = m_scene.Build(VacuumLevel());
            BoardPresenter presenter = m_scene.Presenter;
            Transform second = presenter.GetPiece(1).transform;
            Execute(simulation, GameCommand.RemoveColor(0, new Cell(0, 0), simulation.Board.GetFootprint(0).Color));
            VacuumEffectView vacuum = FindPlayingCast<VacuumEffectView>();
            yield return RunFor(simulation, vacuum.Duration * 0.3f);

            presenter.SetTweensPaused(true);
            Assert.That(vacuum.IsPaused, Is.True, "Suspend holds the cast");
            float held = presenter.FinishSeconds;
            Vector3 position = second.position;
            Vector3 scale = second.localScale;
            yield return RunFor(simulation, 0.3f);

            Assert.That(presenter.FinishSeconds, Is.EqualTo(held).Within(1e-4f), "no removal or cast time passes while paused");
            Assert.That(second.position, Is.EqualTo(position));
            Assert.That(second.localScale, Is.EqualTo(scale));
            Assert.That(presenter.GetPiece(1), Is.Not.Null);

            presenter.SetTweensPaused(false);
            Assert.That(vacuum.IsPaused, Is.False);
            yield return RunFor(simulation, 0.1f);
            Assert.That(second.position, Is.Not.EqualTo(position), "Resume continues the removal");

            // Teardown steps 3 and 6.
            presenter.StopTweens();
            presenter.ReturnPieces();
            for (int index = 0; index < presenter.PreparedCastCount; index++)
            {
                BoosterEffectView view = presenter.GetPreparedCast(index);
                Assert.That(Tween.GetTweensCount(view), Is.Zero, view.name);
                Assert.That(view.IsPlaying, Is.False, view.name);
            }

            Assert.That(Tween.GetTweensCount(second), Is.Zero);
            Assert.That(presenter.ActiveCastCount, Is.Zero);
            Assert.That(presenter.ActivePieceCount, Is.Zero, "every root is back in its pool");
            Assert.That(presenter.FinishSeconds, Is.Zero);
            Assert.That(second.localScale, Is.EqualTo(Vector3.one), "the pool release restores the root's scale");
        }

        [UnityTest]
        public IEnumerator AnIceRocketSplitGivesEachFrozenPartAShellAndCounterWithItsInheritedCount()
        {
            BoardSimulation simulation = m_scene.Build(HammerLevel(ice: true));
            BoardPresenter presenter = m_scene.Presenter;
            int pooled = BoosterPresenterScene.CountPooledObjects();
            int objects = m_scene.CountBoardObjects();
            AssertIced(presenter.GetPiece(0), "the whole frozen line");
            int wholeTiles = presenter.GetPiece(0).View.ActivePieceCount;
            Transform parentRoot = presenter.GetPiece(0).transform;
            Vector3 parentCell = parentRoot.localPosition;

            // A refused press punches the frozen root (the Ice view's feedback), and the split below arrives mid-punch.
            presenter.ShowBlockedDrag(0);
            Assert.That(Tween.GetTweensCount(parentRoot), Is.EqualTo(1));

            // One frame lays the counter overlay out, so its label positions are final before the split.
            yield return null;

            // The frozen line loses its middle: the parent keeps its left cell and part 2 its right end.
            Execute(simulation, GameCommand.RemoveCell(0, new Cell(0, 0), new Cell(1, 0)));

            Assert.That(Tween.GetTweensCount(parentRoot), Is.Zero, "the recompose stops the unfinished punch");
            Assert.That((parentRoot.localPosition - parentCell).sqrMagnitude, Is.LessThan(1e-10f),
                "a frozen parent cannot settle later, so the split puts its root back on its cell");

            var rules = ((IceEffectState)m_scene.Effects.CurrentAttempt.GetAt(0)).Rules;
            Assert.That(rules.GetRemaining(2), Is.EqualTo(2), "the part inherits the counter");
            Assert.That(simulation.Drag.TryBegin(2, new BoardPoint(2f, 0f)), Is.False, "the part is still frozen");
            // The ice is the Ice material on each block's own tiles (DEC-184): the part's tiles wear it, and the
            // parent's recomposed tiles keep it over its one remaining cell.
            AssertIced(presenter.GetPiece(2), "the part");
            AssertIced(presenter.GetPiece(0), "the parent");
            int partTiles = presenter.GetPiece(2).View.ActivePieceCount;
            Assert.That(presenter.GetPiece(0).View.ActivePieceCount, Is.EqualTo(partTiles),
                "the parent is recomposed over its one remaining cell");
            Assert.That(partTiles, Is.LessThan(wholeTiles));

            VisualElement labels = m_scene.Document.rootVisualElement;
            Label parentCount = labels.Q<Label>("ice-count-0");
            Label partCount = labels.Q<Label>("ice-count-2");
            Assert.That(parentCount.text, Is.EqualTo("2"));
            Assert.That(partCount.text, Is.EqualTo("2"));
            Assert.That(partCount.style.display.value, Is.EqualTo(DisplayStyle.Flex));
            AssertLabelOn(parentCount, presenter.GetPiece(0).transform, new[] { new Vector2Int(0, 0) });
            AssertLabelOn(partCount, presenter.GetPiece(2).transform, new[] { new Vector2Int(2, 0) });
            Assert.That(BoosterPresenterScene.CountPooledObjects(), Is.EqualTo(pooled), "no Onity pool created an object");
            Assert.That(m_scene.CountBoardObjects(), Is.EqualTo(objects), "no tile or root was instantiated after Home");
        }

        [UnityTest]
        public IEnumerator AFrozenRectangleAtItsLargestPartCountUsesOnlyWarmedRootsShellsAndLabelsOnEveryAttempt()
        {
            // The worst case of a library shape with Ice (AC-16, B-10, B-19): the 3 x 2 Rectangle falls into the three
            // one-cell parts of its largest independent set, each frozen with its own ice and counter, and no further
            // Rocket charge, bought or seeded, can split a one-cell part. Restart replays it on the same pools.
            BoardSimulation simulation = m_scene.Build(FrozenRectangleLevel());
            BoardPresenter presenter = m_scene.Presenter;
            int prepared = presenter.PreparedPieceCount;
            int pooled = BoosterPresenterScene.CountPooledObjects();
            int objects = m_scene.CountBoardObjects();
            int labels = m_scene.Document.rootVisualElement.Query<Label>().ToList().Count;
            Cell[] removed = { new Cell(1, 0), new Cell(0, 1), new Cell(2, 1) };
            Cell[] parts = { new Cell(0, 0), new Cell(2, 0), new Cell(1, 1) };
            yield return null;

            for (int attempt = 0; attempt < 2; attempt++)
            {
                foreach (Cell cell in removed)
                {
                    int block = simulation.Board.GetOccupant(cell);
                    Execute(simulation, GameCommand.RemoveCell(block, simulation.Board.GetOrigin(block), cell));
                }

                Assert.That(simulation.Board.BlockCount, Is.EqualTo(4), "attempt " + attempt + ": three parts and the single");
                foreach (Cell cell in parts)
                {
                    int block = simulation.Board.GetOccupant(cell);
                    string name = "attempt " + attempt + ", part at " + cell.X + "," + cell.Y;
                    BlockPiece piece = presenter.GetPiece(block);
                    Assert.That(piece, Is.Not.Null, name);
                    Assert.That(piece.Cells.Count, Is.EqualTo(1), name);
                    AssertIced(piece, name);
                    Label count = m_scene.Document.rootVisualElement.Q<Label>("ice-count-" + block);
                    Assert.That(count, Is.Not.Null, name);
                    Assert.That(count.style.display.value, Is.EqualTo(DisplayStyle.Flex), name);
                    Assert.That(count.text, Is.EqualTo("2"), name + " inherits the count");
                    Assert.That(simulation.Commands.Execute(GameCommand.RemoveCell(block, simulation.Board.GetOrigin(block), cell)),
                        Is.EqualTo(CommandOutcome.FinalCell), name + ": a later charge cannot split it further");
                }

                Assert.That(presenter.PreparedPieceCount, Is.EqualTo(prepared), "attempt " + attempt + ": no root pool grew");
                Assert.That(BoosterPresenterScene.CountPooledObjects(), Is.EqualTo(pooled), "no Onity pool created an object");
                Assert.That(m_scene.CountBoardObjects(), Is.EqualTo(objects), "no root or tile was instantiated");
                Assert.That(m_scene.Document.rootVisualElement.Query<Label>().ToList().Count, Is.EqualTo(labels),
                    "every counter came from the Home-sized label slots");

                simulation = m_scene.Restart();
                yield return null;
            }
        }

        [UnityTest]
        public IEnumerator AHammerOnAFrozenBlockRemovesItsIceWithTheBlock()
        {
            BoardSimulation simulation = m_scene.Build(HammerLevel(ice: true));
            BoardPresenter presenter = m_scene.Presenter;
            BlockPiece frozen = presenter.GetPiece(0);
            var tiles = new List<BoardPieceView>();
            for (int index = 0; index < frozen.View.ActivePieceCount; index++) tiles.Add(frozen.View.GetShownPiece(index));
            Label count = m_scene.Document.rootVisualElement.Q<Label>("ice-count-0");
            Assert.That(count.style.display.value, Is.EqualTo(DisplayStyle.Flex));

            Execute(simulation, GameCommand.RemoveBlock(0, new Cell(0, 0)));

            Assert.That(count.style.display.value, Is.EqualTo(DisplayStyle.None), "the counter leaves with the block at once");
            AssertIced(frozen, "the ice rides the removed root");
            yield return RunUntil(simulation, () => presenter.GetPiece(0) == null, 2f);

            foreach (BoardPieceView tile in tiles)
            {
                Assert.That(tile.gameObject.activeSelf, Is.False, "the tile returned to the shared pool with the root");
                Assert.That(tile.IsIced, Is.False, "a returned tile is unfrozen");
            }
            Assert.That(presenter.GetPiece(1).View.IsIced, Is.False, "the plain block has none");
        }

        // The Ice material is the second material of every tile the block shows (DEC-184).
        private static void AssertIced(BlockPiece piece, string message)
        {
            Assert.That(piece, Is.Not.Null, message);
            Assert.That(piece.View.IsIced, Is.True, message);
            Assert.That(piece.View.ActivePieceCount, Is.GreaterThan(0), message);
            for (int index = 0; index < piece.View.ActivePieceCount; index++)
                Assert.That(piece.View.GetShownPiece(index).Renderer.sharedMaterials, Has.Length.EqualTo(2), message);
        }

        [UnityTest]
        public IEnumerator AFinalVacuumRunsInsideTheSessionsBoundedWonFinishAndTeardownReturnsItsRootsAndCast()
        {
            // The session with this presenter, as the Game scene composes it; a command source stands in for the
            // committed booster intent and reaches the same command executor (DEC-122).
            LevelData data = AllRedLevel();
            AttemptLevel level = m_scene.Convert(data);
            m_scene.PrepareEffects(data, level);
            ScriptedPointerSource pointer = new ScriptedPointerSource();
            m_finishDeadline = new PrimeTweenFinishDeadline();
            m_session = new LevelSession(new DragInputRouter(pointer, new NoUiPointerFilter(), new IdentityBoardProjector()),
                pointer, m_scene.Presenter, new InactiveCountdown(), new NoPauseOverlay(true), m_scene.Hooks, m_finishDeadline);
            Assert.That(m_session.TryPrepare(level, out string error), Is.True, error);
            Assert.That(m_session.Play(), Is.True);
            BoardPresenter presenter = m_scene.Presenter;
            Transform first = presenter.GetPiece(0).transform;
            Transform second = presenter.GetPiece(1).transform;
            IBoardReader board = m_session.Attempt.Simulation.Board;
            GameCommand vacuum = GameCommand.RemoveColor(0, board.GetOrigin(0), board.GetFootprint(0).Color);
            Assert.That(m_session.TrySetCommandSource(new OneCommandSource(vacuum)), Is.True);

            // The command phase clears the board, so Won latches and its Sync starts the removals and the cast.
            m_session.RunTick(k_frameSeconds);
            float start = Time.realtimeSinceStartup;
            Assert.That(m_session.State.Value, Is.EqualTo(SessionState.Won));
            Assert.That(m_session.Attempt, Is.Not.Null, "the final Vacuum presentation runs before Teardown");
            Assert.That(presenter.GetPiece(0), Is.Not.Null, "every removed root is kept for its removal");
            Assert.That(presenter.GetPiece(1), Is.Not.Null);
            Assert.That(presenter.ActiveCastCount, Is.EqualTo(1));
            VacuumEffectView cast = FindPlayingCast<VacuumEffectView>();
            Assert.That(cast, Is.Not.Null, "the Vacuum cast plays in the Finish");
            Assert.That(presenter.FinishSeconds, Is.GreaterThan(0f).And.LessThanOrEqualTo(k_finishBoundSeconds));

            // Inside the intake window the roots are still checked out and shrinking into the nozzle. The shrink runs on
            // game time after the intake delay, so the test waits for it rather than for a wall-clock point: a throttled
            // or unfocused Editor can reach any fixed real time before the tween's first frame.
            float finishSeconds = Mathf.Min(k_finishBoundSeconds, presenter.FinishSeconds);
            float intakeStart = cast.Duration * VacuumEffectView.k_intakeStartFraction;
            float longestFrame = 0f;
            while (second.localScale.x >= 1f && m_session.Attempt != null
                   && Time.realtimeSinceStartup - start < 2f * k_finishBoundSeconds)
            {
                yield return null;
                longestFrame = Mathf.Max(longestFrame, Time.unscaledDeltaTime);
            }

            if (m_session.Attempt == null)
            {
                // The Finish deadline runs on real time and the shrink on game time after the intake delay, so one
                // frame at least as long as the rest of the window (a paging Editor) ends the bounded Finish before
                // any frame inside it. That is the DEC-93 timeout, not an early Teardown; the cleanup checks follow.
                Assert.That(longestFrame, Is.GreaterThanOrEqualTo(finishSeconds - intakeStart),
                    "Teardown waits for the final presentation unless a frame outlasts the Finish window");
            }
            else
            {
                Assert.That(presenter.GetPiece(1), Is.Not.Null);
                Assert.That(second.localScale.x, Is.LessThan(1f), "the matching roots shrink toward the intake");
                Assert.That(cast.IsPlaying, Is.True);
            }

            while (m_session.Attempt != null && Time.realtimeSinceStartup - start < 2f * k_finishBoundSeconds)
            {
                yield return null;
                longestFrame = Mathf.Max(longestFrame, Time.unscaledDeltaTime);
            }

            float elapsed = Time.realtimeSinceStartup - start;
            Assert.That(m_session.Attempt, Is.Null, "Teardown ran");
            Assert.That(elapsed, Is.LessThanOrEqualTo(k_finishBoundSeconds + longestFrame), "Finish ends within 0.8 s plus one frame");
            Assert.That(m_session.State.Value, Is.EqualTo(SessionState.Won));
            Assert.That(presenter.ActivePieceCount, Is.Zero, "every root is back in its pool");
            Assert.That(presenter.ActiveCastCount, Is.Zero, "the cast is back in its pool");
            for (int index = 0; index < presenter.PreparedCastCount; index++)
            {
                BoosterEffectView view = presenter.GetPreparedCast(index);
                Assert.That(Tween.GetTweensCount(view), Is.Zero, view.name);
                Assert.That(view.IsPlaying, Is.False, view.name);
            }

            Assert.That(Tween.GetTweensCount(first), Is.Zero, "no removal tween survives Teardown");
            Assert.That(Tween.GetTweensCount(second), Is.Zero);
            Assert.That(Tween.GetTweensCount(m_finishDeadline), Is.Zero, "the Finish deadline is stopped");
        }

        // One tick's command phase and view sync phase, as the session runs them.
        // Runs one command and shows a Rocket split at once, as its contact would: these tests check what the split
        // shows, and ExecuteWaiting checks when.
        private void Execute(BoardSimulation simulation, GameCommand command)
        {
            ExecuteWaiting(simulation, command);
            m_scene.Presenter.CompleteRocketHits();
        }

        private void ExecuteWaiting(BoardSimulation simulation, GameCommand command)
        {
            simulation.Events.Clear();
            Assert.That(simulation.Commands.Execute(command), Is.EqualTo(CommandOutcome.Applied), command.ToString());
            m_scene.Presenter.Sync(simulation, default);
        }

        private void Sync(BoardSimulation simulation)
        {
            simulation.Events.Clear();
            m_scene.Presenter.Sync(simulation, default);
        }

        private IEnumerator RunFor(BoardSimulation simulation, float seconds)
        {
            float end = Time.realtimeSinceStartup + seconds;
            while (Time.realtimeSinceStartup < end)
            {
                yield return null;
                Sync(simulation);
            }
        }

        private IEnumerator RunUntil(BoardSimulation simulation, System.Func<bool> done, float timeout)
        {
            float end = Time.realtimeSinceStartup + timeout;
            while (!done() && Time.realtimeSinceStartup < end)
            {
                yield return null;
                Sync(simulation);
            }

            Assert.That(done(), Is.True, "the presentation finished within " + timeout + " s");
        }

        // Every on-board block shows its runtime footprint on a root at its origin; no other slot holds one.
        private void AssertShowsRuntimeFootprints(BoardSimulation simulation)
        {
            IBoardReader board = simulation.Preview;
            int live = 0;
            for (int block = 0; block < board.BlockCount; block++)
            {
                BlockPiece piece = m_scene.Presenter.GetPiece(block);
                if (board.GetStatus(block) != BlockStatus.OnBoard)
                {
                    continue;
                }

                live++;
                Assert.That(piece, Is.Not.Null, "block " + block);
                BlockFootprint footprint = board.GetFootprint(block);
                var expected = new Vector2Int[footprint.CellCount];
                for (int i = 0; i < expected.Length; i++)
                {
                    expected[i] = new Vector2Int(footprint.GetCell(i).X, footprint.GetCell(i).Y);
                }

                Assert.That(piece.Cells, Is.EquivalentTo(expected), "block " + block);
                Cell origin = board.GetOrigin(block);
                Assert.That(piece.transform.localPosition,
                    Is.EqualTo(m_scene.Level.Layout.CellToLocal(new Vector2Int(origin.X, origin.Y))), "block " + block);
            }

            Assert.That(m_scene.Presenter.ActivePieceCount, Is.EqualTo(live));
        }

        private T FindPlayingCast<T>() where T : BoosterEffectView
        {
            for (int index = 0; index < m_scene.Presenter.PreparedCastCount; index++)
            {
                if (m_scene.Presenter.GetPreparedCast(index) is T view && view.IsPlaying)
                {
                    return view;
                }
            }

            return null;
        }

        // The atlas cell the tile samples (DEC-202).
        private static Vector4 ReadColor(Transform root)
        {
            MaterialPropertyBlock properties = new MaterialPropertyBlock();
            root.GetComponentInChildren<MeshRenderer>(false).GetPropertyBlock(properties);
            return properties.GetVector(s_colorId);
        }

        // Every tile the root shows carries the expected tint in its property block (DEC-123).
        private static void AssertTint(BlockPiece piece, Vector4 expected, string message)
        {
            MaterialPropertyBlock properties = new MaterialPropertyBlock();
            int tiles = 0;
            foreach (BoardPieceView tile in piece.GetComponentsInChildren<BoardPieceView>(false))
            {
                tile.Renderer.GetPropertyBlock(properties);
                Assert.That(properties.GetVector(s_colorId), Is.EqualTo(expected), message);
                tiles++;
            }

            Assert.That(tiles, Is.GreaterThan(0), message + ": the root shows its tiles");
        }

        private static void AssertNear(Vector3 actual, Vector3 expected, string message)
        {
            Assert.That(Vector3.Distance(actual, expected), Is.LessThan(1e-4f), message + ": " + actual + " vs " + expected);
        }

        // The label is centred where the camera projects its anchor on the root's cells (IceCounterLabels.GetLabelOffset).
        private void AssertLabelOn(Label label, Transform root, IReadOnlyList<Vector2Int> cells)
        {
            VisualElement layer = label.parent;
            Assert.That(layer.panel, Is.Not.Null, "the counter layer is on the overlay panel");
            Vector3 screen = m_scene.Camera.WorldToScreenPoint(root.TransformPoint(IceCounterLabels.GetLabelOffset(cells)));
            Vector2 expected = layer.WorldToLocal(RuntimePanelUtils.ScreenToPanel(layer.panel,
                new Vector2(screen.x, Screen.height - screen.y)));
            Vector2 centre = new Vector2(label.style.left.value.value + label.style.width.value.value * 0.5f,
                label.style.top.value.value + label.style.height.value.value * 0.5f);
            Assert.That(Vector2.Distance(centre, expected), Is.LessThan(0.01f), label.name + " at " + centre + ", expected " + expected);
        }

        // The Rocket stands below the middle of the board's lower edge, turned as the board, and its nose reaches the
        // top of the removed board cell at RocketEffectView.k_noseFraction (B-20).
        private void AssertRocketFromBelowTheBoardTo(RocketEffectView rocket, Cell cell)
        {
            Transform boardRoot = m_scene.Environment.BoardRoot;
            AssertNear(rocket.transform.position, boardRoot.TransformPoint(GetCastLaunchLocal()), "the Rocket launches below the board");
            AssertRocketNoseReaches(rocket, cell);
        }

        // The Rocket is turned as the board, and its nose reaches the top of the removed board cell at
        // RocketEffectView.k_noseFraction wherever it launched from (B-20).
        private void AssertRocketNoseReaches(RocketEffectView rocket, Cell cell)
        {
            Transform boardRoot = m_scene.Environment.BoardRoot;
            Assert.That(Quaternion.Angle(rocket.transform.rotation, boardRoot.rotation), Is.LessThan(0.01f));

            // Hold the motion, then sample its nose pose; the removal of the cell does not wait for it.
            rocket.Suspend();
            rocket.SampleForPreview(RocketEffectView.k_noseFraction);
            Vector3 tip = rocket.transform.Find("Visual").TransformPoint(new Vector3(0f, 2.29f, 0f));
            Vector3 destination = boardRoot.TransformPoint(m_scene.Level.Layout.CellToLocal(new Vector2Int(cell.X, cell.Y))
                + Vector3.up * ExitMotionSettings.k_blockTopHeight);
            AssertNear(tip, destination, "the nose reaches the removed cell's top");
        }

        // Below the middle of the board's lower edge, one cell past the wall ring's outer side, at floor level.
        private Vector3 GetCastLaunchLocal()
        {
            Bounds bounds = m_scene.Level.Layout.LocalBounds;
            return new Vector3(bounds.center.x, 0f, bounds.min.z - BoardLayout.CellSize);
        }

        // The bottom of the drawn canister: the root lowered by the view's BaseDepth along its camera-facing up axis.
        private static Vector3 GetVacuumBase(VacuumEffectView vacuum)
        {
            return vacuum.transform.position - vacuum.transform.up * vacuum.BaseDepth;
        }

        private Vector2 ToViewport(Vector3 world)
        {
            Vector3 viewport = m_scene.Camera.WorldToViewportPoint(world);
            return new Vector2(viewport.x, viewport.y);
        }

        // The flat Vacuum's canister bottom projects onto the expected screen point, and its camera-facing plane is in
        // front of both near wall tops, so the board's lower wall and gates never hide its hose or nozzle.
        private void AssertVacuumStandsInFrontOn(VacuumEffectView vacuum, Vector2 viewport, string message)
        {
            Vector3 canisterBase = GetVacuumBase(vacuum);
            Assert.That(Vector2.Distance(ToViewport(canisterBase), viewport), Is.LessThan(1e-3f),
                message + ": viewport " + ToViewport(canisterBase) + " vs " + viewport);

            Transform boardRoot = m_scene.Environment.BoardRoot;
            Transform eye = m_scene.Camera.transform;
            Bounds bounds = m_scene.Level.Layout.LocalBounds;
            float plane = Vector3.Dot(canisterBase - eye.position, eye.forward);
            foreach (float x in new[] { bounds.min.x, bounds.max.x })
            {
                Vector3 wallTop = boardRoot.TransformPoint(new Vector3(x, bounds.max.y, bounds.min.z));
                Assert.That(plane, Is.LessThan(Vector3.Dot(wallTop - eye.position, eye.forward)),
                    message + ": in front of the near wall top at x " + x);
            }
        }

        // A launch point on the board's floor plane that the camera projects onto the bar anchor.
        private void AssertOnFloorUnderTheBar(Vector3 world, string message)
        {
            Assert.That(m_scene.Environment.BoardRoot.InverseTransformPoint(world).y, Is.EqualTo(0f).Within(1e-3f),
                message + " at floor level");
            Vector3 viewport = m_scene.Camera.WorldToViewportPoint(world);
            Assert.That(Vector2.Distance(new Vector2(viewport.x, viewport.y), s_barAnchor), Is.LessThan(1e-3f),
                message + ": viewport " + viewport + " vs " + s_barAnchor);
        }

        // The HUD's booster bar top as the Game scene's HUD reports it (IBoosterCastAnchor), fixed for the fixture camera.
        private sealed class FixedCastAnchor : IBoosterCastAnchor
        {
            public bool TryGetLaunchViewportPoint(out Vector2 viewportPoint)
            {
                viewportPoint = s_barAnchor;
                return true;
            }
        }

        // Offers one command once, as a committed booster intent would reach the command phase.
        private sealed class OneCommandSource : ICommandSource
        {
            private readonly GameCommand m_command;
            private bool m_offered;

            public OneCommandSource(GameCommand command)
            {
                m_command = command;
            }

            public bool TryGetNext(IBoardReader board, out GameCommand command)
            {
                command = m_command;
                if (m_offered)
                {
                    return false;
                }

                m_offered = true;
                return true;
            }

            public void OnExecuted(in GameCommand command, CommandOutcome outcome)
            {
                Assert.That(outcome, Is.EqualTo(CommandOutcome.Applied), command.ToString());
            }
        }

        private static LevelDoorData[] RedTopDoor() => new[] { new LevelDoorData("red", BoardSide.Top, 0, 1, "Red") };

        // 6 x 4: a red L at (0, 0) and a blue cross at (3, 0).
        internal static LevelData SplitLevel()
        {
            return new LevelData("booster-split", "Booster split", 6, 4, 60, RedTopDoor(), new[]
            {
                new LevelBlockData("l", "Red", Vector2Int.zero,
                    new[] { new Vector2Int(0, 0), new Vector2Int(0, 1), new Vector2Int(0, 2), new Vector2Int(1, 2) }),
                new LevelBlockData("cross", "Blue", new Vector2Int(3, 0),
                    new[] { new Vector2Int(1, 0), new Vector2Int(0, 1), new Vector2Int(1, 1), new Vector2Int(2, 1), new Vector2Int(1, 2) })
            });
        }

        // 3 x 3: a red line of three across row 1, flush with a one-wide red door on the right at row 1.
        private static LevelData PartExitLevel()
        {
            return new LevelData("booster-part-exit", "Booster part exit", 3, 3, 60,
                new[] { new LevelDoorData("right", BoardSide.Right, 1, 1, "Red") }, new[]
                {
                    new LevelBlockData("line", "Red", new Vector2Int(0, 1),
                        new[] { new Vector2Int(0, 0), new Vector2Int(1, 0), new Vector2Int(2, 0) }),
                    new LevelBlockData("stay", "Blue", Vector2Int.zero, new[] { Vector2Int.zero })
                });
        }

        // 4 x 4: a red line of three on the bottom row, frozen for two exits when ice is set, and a blue single.
        internal static LevelData HammerLevel(bool ice)
        {
            LevelEffectData[] effects = ice
                ? new[] { new LevelEffectData("ice", new[] { new LevelEffectParamData("clearsToThaw", "2") }) }
                : new LevelEffectData[0];
            return new LevelData(ice ? "booster-ice" : "booster-hammer", "Booster hammer", 4, 4, 60, RedTopDoor(), new[]
            {
                new LevelBlockData("line", "Red", Vector2Int.zero,
                    new[] { new Vector2Int(0, 0), new Vector2Int(1, 0), new Vector2Int(2, 0) }, effects),
                new LevelBlockData("plain", "Blue", new Vector2Int(3, 3), new[] { Vector2Int.zero })
            });
        }

        // 5 x 4: a red Rectangle (3 x 2) at (0, 0), frozen for two exits, and a blue single.
        private static LevelData FrozenRectangleLevel()
        {
            LevelEffectData[] ice = { new LevelEffectData("ice", new[] { new LevelEffectParamData("clearsToThaw", "2") }) };
            return new LevelData("booster-frozen-rectangle", "Booster frozen rectangle", 5, 4, 60, RedTopDoor(), new[]
            {
                new LevelBlockData("rectangle", "Red", Vector2Int.zero, new[]
                {
                    new Vector2Int(0, 0), new Vector2Int(1, 0), new Vector2Int(2, 0),
                    new Vector2Int(0, 1), new Vector2Int(1, 1), new Vector2Int(2, 1)
                }, ice),
                new LevelBlockData("plain", "Blue", new Vector2Int(4, 3), new[] { Vector2Int.zero })
            });
        }

        // 5 x 5: two red blocks and a blue single.
        internal static LevelData VacuumLevel()
        {
            return new LevelData("booster-vacuum", "Booster vacuum", 5, 5, 60, RedTopDoor(), new[]
            {
                new LevelBlockData("red-single", "Red", Vector2Int.zero, new[] { Vector2Int.zero }),
                new LevelBlockData("red-double", "Red", new Vector2Int(2, 2),
                    new[] { Vector2Int.zero, Vector2Int.right }),
                new LevelBlockData("blue", "Blue", new Vector2Int(4, 4), new[] { Vector2Int.zero })
            });
        }

        // 5 x 5: the red blocks of VacuumLevel only, so one Vacuum on red clears the board.
        private static LevelData AllRedLevel()
        {
            return new LevelData("booster-final-vacuum", "Booster final vacuum", 5, 5, 60, RedTopDoor(), new[]
            {
                new LevelBlockData("red-single", "Red", Vector2Int.zero, new[] { Vector2Int.zero }),
                new LevelBlockData("red-double", "Red", new Vector2Int(2, 2),
                    new[] { Vector2Int.zero, Vector2Int.right })
            });
        }
    }
}
#endif
