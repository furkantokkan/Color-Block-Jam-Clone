using System.Collections;
using System.Collections.Generic;
using ColorBlockJam.Editor.Levels;
using ColorBlockJam.Runtime.Levels;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

namespace ColorBlockJam.Tests.EditMode
{
    /// <summary>
    /// Verifies the Obstacles Barrier (TASK-20260925-102907480-9e5cbf34): the paint, join, merge, erase and split
    /// commands, the Starts open command the Effect Editor can use, the closed-barrier rule for blocks, the canvas model,
    /// hit test and obstacle selection, one undo step per stroke in the window and the barrier save gate. Fixture levels
    /// live only in memory or the window draft; nothing is written to Data/Json/Levels.
    /// </summary>
    public sealed class LevelObstacleBarrierTests
    {
        private static readonly Vector2Int[] s_single = { Vector2Int.zero };
        private static readonly Vector2Int[] s_row = { new Vector2Int(0, 1), new Vector2Int(1, 1), new Vector2Int(2, 1) };

        private LevelEditorWindowFixture m_fixture;
        private LevelDatabaseWindow m_window;
        private readonly List<List<LevelData>> m_writes = new List<List<LevelData>>();

        [TearDown]
        public void CloseWindow()
        {
            m_fixture?.Dispose();
            m_fixture = null;
            m_window = null;
        }

        [Test]
        public void AStrokeOverFreeFloorPaintsOneClosedBarrierAndATouchingStrokeJoinsIt()
        {
            LevelData level = LevelObstacleWallCommandsTests.CreateLevel32();

            Assert.That(LevelBarrierCommands.TryApplyStroke(level, s_row, true, out LevelData painted, out string issue),
                Is.True, issue);
            Assert.That(issue, Is.Null);
            Assert.That(level.CellObjectCount, Is.Zero, "The source level stays unchanged.");
            Assert.That(painted.CellObjectCount, Is.EqualTo(1));
            LevelCellObjectData barrier = painted.GetCellObjectAt(0);
            Assert.That(barrier.Id, Is.EqualTo("barrier-1"));
            Assert.That(GetCells(barrier), Is.EqualTo(s_row), "One object in stroke order.");
            Assert.That(barrier.Effect.Type, Is.EqualTo("barrier"));
            Assert.That(barrier.Effect.ParamCount, Is.Zero, "A new barrier has no params.");
            Assert.That(LevelBarrierCommands.StartsOpen(barrier), Is.False, "So it starts closed.");
            Assert.That(painted.BlockCount, Is.EqualTo(3));
            Assert.That(painted.DoorCount, Is.EqualTo(2));
            Assert.That(painted.Validate(out string error), Is.True, error);

            // (2,2) touches (2,1): the stroke joins barrier-1.
            Assert.That(LevelBarrierCommands.TryApplyStroke(painted, new[] { new Vector2Int(2, 2), new Vector2Int(3, 2) },
                true, out LevelData joined, out issue), Is.True, issue);
            Assert.That(joined.CellObjectCount, Is.EqualTo(1));
            Assert.That(joined.GetCellObjectAt(0).Id, Is.EqualTo("barrier-1"));
            Assert.That(joined.GetCellObjectAt(0).CellCount, Is.EqualTo(5));

            // A stroke touching no barrier is a new one.
            Assert.That(LevelBarrierCommands.TryApplyStroke(joined, new[] { new Vector2Int(0, 3) }, true,
                out LevelData two, out issue), Is.True, issue);
            Assert.That(two.CellObjectCount, Is.EqualTo(2));
            Assert.That(two.GetCellObjectAt(1).Id, Is.EqualTo("barrier-2"));

            // A stroke over barrier cells only changes nothing.
            Assert.That(LevelBarrierCommands.TryApplyStroke(two, s_row, true, out LevelData same, out issue), Is.False);
            Assert.That(same, Is.Null);
            Assert.That(issue, Is.Null);
        }

        [Test]
        public void AStrokeTouchingTwoBarriersMergesThemAndTheFirstListedEffectWins()
        {
            // barrier-open (listed first, starts open, with an extra param) and barrier-closed; (0,2) touches both.
            LevelEffectData open = LevelEffectTests.Effect("barrier", ("startsOpen", "true"));
            LevelData level = WithObjects(LevelObstacleWallCommandsTests.CreateLevel32(),
                LevelEffectTests.Object("barrier-open", open, new Vector2Int(0, 3)),
                LevelEffectTests.Object("barrier-closed", LevelBarrierCommands.CreateEffect(), new Vector2Int(0, 1)));

            Assert.That(LevelBarrierCommands.TryApplyStroke(level, new[] { new Vector2Int(0, 2) }, true,
                out LevelData merged, out string issue), Is.True, issue);
            Assert.That(merged.CellObjectCount, Is.EqualTo(1));
            LevelCellObjectData barrier = merged.GetCellObjectAt(0);
            Assert.That(barrier.Id, Is.EqualTo("barrier-open"));
            Assert.That(GetCells(barrier), Is.EqualTo(new[]
            {
                new Vector2Int(0, 3), new Vector2Int(0, 1), new Vector2Int(0, 2)
            }), "The first barrier's cells, then the absorbed one's, then the stroke's.");
            Assert.That(JsonUtility.ToJson(barrier.Effect), Is.EqualTo(JsonUtility.ToJson(open)),
                "The first-listed barrier's whole effect wins.");
            Assert.That(merged.Validate(out string error), Is.True, error);

            // Listed the other way round, the closed barrier wins.
            LevelData reversed = WithObjects(LevelObstacleWallCommandsTests.CreateLevel32(),
                LevelEffectTests.Object("barrier-closed", LevelBarrierCommands.CreateEffect(), new Vector2Int(0, 1)),
                LevelEffectTests.Object("barrier-open", open, new Vector2Int(0, 3)));
            Assert.That(LevelBarrierCommands.TryApplyStroke(reversed, new[] { new Vector2Int(0, 2) }, true,
                out merged, out issue), Is.True, issue);
            Assert.That(merged.GetCellObjectAt(0).Id, Is.EqualTo("barrier-closed"));
            Assert.That(LevelBarrierCommands.StartsOpen(merged.GetCellObjectAt(0)), Is.False);
        }

        [Test]
        public void ErasingAMiddleCellSplitsTheBarrierWithItsParamsAndErasingEveryCellRemovesIt()
        {
            LevelEffectData open = LevelEffectTests.Effect("barrier", ("startsOpen", "true"));
            LevelData level = WithObjects(LevelObstacleWallCommandsTests.CreateLevel32(),
                LevelEffectTests.Object("barrier-1", open, s_row),
                LevelEffectTests.Object("ivy-1", LevelEffectTests.Effect("ivy"), new Vector2Int(3, 2)));

            Assert.That(LevelBarrierCommands.TryApplyStroke(level, new[] { new Vector2Int(1, 1) }, false,
                out LevelData split, out string issue), Is.True, issue);
            Assert.That(split.CellObjectCount, Is.EqualTo(3));
            Assert.That(split.GetCellObjectAt(0).Id, Is.EqualTo("barrier-1"));
            Assert.That(GetCells(split.GetCellObjectAt(0)), Is.EqualTo(new[] { new Vector2Int(0, 1) }));
            Assert.That(split.GetCellObjectAt(1).Id, Is.EqualTo("ivy-1"), "Other board objects keep their place.");
            Assert.That(split.GetCellObjectAt(2).Id, Is.EqualTo("barrier-2"));
            Assert.That(GetCells(split.GetCellObjectAt(2)), Is.EqualTo(new[] { new Vector2Int(2, 1) }));
            Assert.That(JsonUtility.ToJson(split.GetCellObjectAt(2).Effect), Is.EqualTo(JsonUtility.ToJson(open)),
                "The split keeps the effect params.");
            Assert.That(split.Validate(out string error), Is.True, error);

            Assert.That(LevelBarrierCommands.TryApplyStroke(split, new[] { new Vector2Int(0, 1), new Vector2Int(2, 1) },
                false, out LevelData erased, out issue), Is.True, issue);
            Assert.That(erased.CellObjectCount, Is.EqualTo(1));
            Assert.That(erased.GetCellObjectAt(0).Id, Is.EqualTo("ivy-1"));
            Assert.That(LevelBarrierCommands.TryApplyStroke(erased, s_row, false, out _, out _), Is.False,
                "Nothing left to erase.");
            Assert.That(LevelBarrierCommands.TryApplyStroke(level, new[] { new Vector2Int(3, 2) }, false, out _, out _),
                Is.False, "Erasing barrier cells never removes another board object.");
        }

        [Test]
        public void AClosedStrokeOverABlockIsRefusedWithTheValidationMessageAndJoiningAnOpenBarrierIsAccepted()
        {
            LevelData level = LevelObstacleWallCommandsTests.CreateLevel32();
            Vector2Int[] stroke = { new Vector2Int(0, 3), new Vector2Int(1, 3) };
            WithObjects(level, LevelEffectTests.Object("barrier-1", LevelBarrierCommands.CreateEffect(), stroke))
                .Validate(out string expected);
            Assert.That(expected, Is.EqualTo("Level level-32 cell object barrier-1 effect barrier starts closed on " +
                                             "block b1 at (1, 3)."));

            Assert.That(LevelBarrierCommands.TryApplyStroke(level, stroke, true, out LevelData refused, out string issue),
                Is.False);
            Assert.That(refused, Is.Null, "The whole stroke is refused.");
            Assert.That(issue, Is.EqualTo(expected));

            // (0,3) touches an open barrier at (0,2), so the stroke joins it and may cover b1.
            LevelData withOpen = WithObjects(level, LevelEffectTests.Object("barrier-1",
                LevelEffectTests.Effect("barrier", ("startsOpen", "true")), new Vector2Int(0, 2)));
            Assert.That(LevelBarrierCommands.TryApplyStroke(withOpen, stroke, true, out LevelData open, out issue),
                Is.True, issue);
            Assert.That(open.GetCellObjectAt(0).CellCount, Is.EqualTo(3));
            Assert.That(open.Validate(out string error), Is.True, error);

            // Void cells and other board objects are skipped and named; the rest of the stroke applies.
            LevelData ivy = WithObjects(level, LevelEffectTests.Object("ivy-1", LevelEffectTests.Effect("ivy"),
                new Vector2Int(1, 2)));
            Assert.That(LevelBarrierCommands.TryApplyStroke(ivy, new[] { new Vector2Int(0, 2), new Vector2Int(1, 2) },
                true, out LevelData partial, out issue), Is.True);
            Assert.That(issue, Is.EqualTo("Obstacle ivy-1 occupies that cell."));
            Assert.That(GetCells(partial.GetCellObjectAt(1)), Is.EqualTo(new[] { new Vector2Int(0, 2) }));
        }

        [Test]
        public void ABlockMayGoOnAnOpenBarrierButNotOnAClosedOne()
        {
            LevelData level = WithObjects(LevelObstacleWallCommandsTests.CreateLevel32(),
                LevelEffectTests.Object("barrier-1", LevelBarrierCommands.CreateEffect(),
                    new Vector2Int(0, 1), new Vector2Int(1, 1)),
                LevelEffectTests.Object("barrier-2", LevelEffectTests.Effect("barrier", ("startsOpen", "true")),
                    new Vector2Int(0, 2), new Vector2Int(1, 2)));
            Assert.That(level.Validate(out string error), Is.True, error);
            LevelCanvasModel model = LevelCanvasModel.Create(level);

            Assert.That(LevelEditCommands.TryPlaceBlock(level, s_single, "Red", new Vector2Int(1, 1),
                out _, out string issue), Is.False);
            Assert.That(issue, Is.EqualTo(GetValidationError(level, new Vector2Int(1, 1))));
            Assert.That(issue, Does.EndWith("barrier-1 effect barrier starts closed on block b4 at (1, 1)."));
            Assert.That(model.IsFree(new Vector2Int(1, 1), -1), Is.False, "The ghost matches the place command.");

            Assert.That(LevelEditCommands.TryPlaceBlock(level, s_single, "Red", new Vector2Int(1, 2),
                out LevelData placed, out issue), Is.True, issue);
            Assert.That(placed.Validate(out error), Is.True, error);
            Assert.That(model.IsFree(new Vector2Int(1, 2), -1), Is.True, "An open barrier cell is floor.");

            // b3 sits at (3,0): a move onto the closed barrier is refused, onto the open one accepted.
            Assert.That(LevelEditCommands.TryMoveBlock(level, 2, new Vector2Int(0, 1), out _, out issue), Is.False);
            Assert.That(issue, Does.EndWith("barrier-1 effect barrier starts closed on block b3 at (0, 1)."));
            Assert.That(LevelEditCommands.TryMoveBlock(level, 2, new Vector2Int(0, 2), out LevelData moved, out issue),
                Is.True, issue);
            Assert.That(moved.Validate(out error), Is.True, error);
            Assert.That(model.IsFree(new Vector2Int(0, 2), 2), Is.True);

            // A new cell of b1 on the closed barrier is refused too.
            Assert.That(LevelEditCommands.TryReplaceBlockCells(level, 0, new[] { Vector2Int.zero,
                new Vector2Int(0, -2) }, out _, out issue), Is.False);
            Assert.That(issue, Does.EndWith("starts closed on block b1 at (1, 1)."));
        }

        [Test]
        public void StartsOpenCommandChangesOnlyThatParamAndClosingOverABlockIsRefused()
        {
            LevelData level = WithObjects(LevelObstacleWallCommandsTests.CreateLevel32(),
                LevelEffectTests.Object("barrier-1", LevelBarrierCommands.CreateEffect(), s_row),
                LevelEffectTests.Object("barrier-2", LevelEffectTests.Effect("barrier", ("startsOpen", "true")),
                    new Vector2Int(1, 3)),
                LevelEffectTests.Object("ivy-1", LevelEffectTests.Effect("ivy"), new Vector2Int(3, 2)));

            Assert.That(LevelBarrierCommands.TrySetStartsOpen(level, 0, true, out LevelData opened, out string issue),
                Is.True, issue);
            LevelCellObjectData barrier = opened.GetCellObjectAt(0);
            Assert.That(barrier.Id, Is.EqualTo("barrier-1"));
            Assert.That(GetCells(barrier), Is.EqualTo(s_row));
            Assert.That(barrier.Effect.FindValue("startsOpen"), Is.EqualTo("true"));
            Assert.That(opened.CellObjectCount, Is.EqualTo(3));
            Assert.That(LevelCanvasModel.Create(opened).GetObstacle(0).StartsOpen, Is.True, "The overlay lightens.");
            Assert.That(LevelBarrierCommands.TrySetStartsOpen(opened, 0, true, out LevelData same, out issue), Is.False);
            Assert.That(same, Is.Null);
            Assert.That(issue, Is.Null, "The same state is no edit and no refusal.");
            Assert.That(LevelBarrierCommands.TrySetStartsOpen(opened, 0, false, out LevelData closed, out issue),
                Is.True, issue);
            Assert.That(closed.GetCellObjectAt(0).Effect.FindValue("startsOpen"), Is.EqualTo("false"));

            // barrier-2 lies under block b1, so it may not start closed.
            Assert.That(LevelBarrierCommands.TrySetStartsOpen(level, 1, false, out _, out issue), Is.False);
            Assert.That(issue, Is.EqualTo("Level level-32 cell object barrier-2 effect barrier starts closed on " +
                                          "block b1 at (1, 3)."));
            Assert.That(LevelBarrierCommands.TrySetStartsOpen(level, 2, true, out _, out issue), Is.False);
            Assert.That(issue, Is.EqualTo("Select a barrier first."));
        }

        [Test]
        public void SaveIssueNamesTheBarrierWhileItsGameplayIsNotImplemented()
        {
            LevelData plain = LevelObstacleWallCommandsTests.CreateLevel32();
            LevelData barrier = WithObjects(plain,
                LevelEffectTests.Object("barrier-1", LevelBarrierCommands.CreateEffect(), s_row));
            Assert.That(barrier.Validate(out string error), Is.True, error);

            Assert.That(LevelBarrierCommands.GetSaveIssue(plain), Is.Null, "A level without a barrier saves as before.");
            // Another unimplemented board object still saves as before; only barriers are gated here.
            LevelData ivy = WithObjects(plain, LevelEffectTests.Object("ivy-1", LevelEffectTests.Effect("ivy"),
                new Vector2Int(2, 2)));
            Assert.That(ivy.ValidateRuntimeSupport(out _), Is.False);
            Assert.That(LevelBarrierCommands.GetSaveIssue(ivy), Is.Null);

            string issue = LevelBarrierCommands.GetSaveIssue(barrier);
            if (LevelEffectTests.IsImplemented("barrier"))
            {
                Assert.That(issue, Is.Null, "Once TASK-20260925-103527585-3d1b0a34 lands, barriers save.");
                return;
            }

            Assert.That(barrier.ValidateRuntimeSupport(out string expected), Is.False);
            Assert.That(issue, Is.EqualTo(expected));
            Assert.That(issue, Is.EqualTo("Level level-32 cell object barrier-1 effect barrier is not implemented in " +
                                          "gameplay yet."));
        }

        [Test]
        public void ModelKeepsBarrierCellsAndStateAndTheHitTestReportsTheBoardObject()
        {
            LevelData level = WithObjects(LevelObstacleWallCommandsTests.CreateLevel32(),
                LevelEffectTests.Object("barrier-1", LevelBarrierCommands.CreateEffect(),
                    new Vector2Int(0, 1), new Vector2Int(1, 1)),
                LevelEffectTests.Object("barrier-2", LevelEffectTests.Effect("barrier", ("startsOpen", "true")),
                    new Vector2Int(1, 3)),
                LevelEffectTests.Object("ivy-1", LevelEffectTests.Effect("ivy"), new Vector2Int(2, 2)));
            LevelCanvasModel model = LevelCanvasModel.Create(level);

            Assert.That(model.ObstacleCount, Is.EqualTo(3));
            Assert.That(model.GetObstacle(0).IsBarrier && !model.GetObstacle(0).StartsOpen, Is.True);
            Assert.That(model.GetObstacle(1).IsBarrier && model.GetObstacle(1).StartsOpen, Is.True);
            Assert.That(model.GetObstacle(2).IsBarrier, Is.False);
            Assert.That(model.GetObstacle(0).CellCount, Is.EqualTo(2));
            Assert.That(model.IsBarrier(new Vector2Int(1, 1)), Is.True);
            Assert.That(model.IsBarrier(new Vector2Int(2, 2)), Is.False);
            Assert.That(model.GetObstacleIndexAt(new Vector2Int(2, 2)), Is.EqualTo(2));
            Assert.That(model.GetObstacleIndexAt(new Vector2Int(3, 3)), Is.EqualTo(-1));

            Assert.That(model.IsFree(new Vector2Int(0, 1), -1), Is.False, "Closed barrier.");
            Assert.That(model.IsFree(new Vector2Int(1, 3), 0), Is.True, "b1's own cell on the open barrier.");
            Assert.That(model.CanPaintBarrier(new Vector2Int(3, 3)), Is.True);
            Assert.That(model.CanPaintBarrier(new Vector2Int(3, 0)), Is.False, "A new, closed barrier may not cover b3.");
            Assert.That(model.CanPaintBarrier(new Vector2Int(2, 2)), Is.False, "Another board object.");
            Assert.That(model.CanPaintBarrier(new Vector2Int(0, 1)), Is.True, "Its own cell joins it.");

            LevelCanvasGeometry geometry = LevelCanvasGeometry.Create(new Vector2(680f, 518f), 4, 6);
            LevelCanvasHit barrierHit = LevelCanvasHitTest.Test(model, geometry,
                geometry.GetCellRect(new Vector2Int(1, 1)).center);
            Assert.That(barrierHit, Is.EqualTo(LevelCanvasHit.ForCell(new Vector2Int(1, 1), 0, "barrier-1")));
            Assert.That(barrierHit.Kind, Is.EqualTo(LevelCanvasHitKind.Cell));
            LevelCanvasHit blockHit = LevelCanvasHitTest.Test(model, geometry,
                geometry.GetCellRect(new Vector2Int(1, 3)).center);
            Assert.That(blockHit.Kind, Is.EqualTo(LevelCanvasHitKind.Block), "A block over an open barrier wins.");
            Assert.That(LevelCanvasHitTest.Test(model, geometry, geometry.GetCellRect(new Vector2Int(3, 3)).center),
                Is.EqualTo(LevelCanvasHit.ForCell(new Vector2Int(3, 3))));

            // The selection is found by id after its index moves, and by index when the id is gone.
            Assert.That(model.Resolve(LevelCanvasSelection.ForObstacle(7, "barrier-2")),
                Is.EqualTo(LevelCanvasSelection.ForObstacle(1, "barrier-2")));
            Assert.That(model.Resolve(LevelCanvasSelection.ForObstacle(0, "gone")),
                Is.EqualTo(LevelCanvasSelection.ForObstacle(0, "barrier-1")));
            Assert.That(model.Resolve(LevelCanvasSelection.ForObstacle(9, "gone")), Is.EqualTo(LevelCanvasSelection.None));
        }

        [UnityTest]
        public IEnumerator BarrierStrokesJoinSplitAndAreOneUndoStepEach()
        {
            yield return ShowWindowBuiltByUnity();
            VisualElement root = m_window.rootVisualElement;
            LevelCanvasElement canvas = root.Q<LevelCanvasElement>();
            Assert.That(IsLevelLoaded(root) && canvas.Geometry.IsValid, Is.True, "The fixture database has a loaded level.");

            LoadFixture(root, LevelObstacleWallCommandsTests.CreateLevel32());
            string plain = JsonUtility.ToJson(GetDraft());
            Press(root.Q<Button>("obstacle-barrier"));

            Drag(root, canvas, new Vector2Int(0, 1), new Vector2Int(2, 1), 0);
            Assert.That(GetDraft().CellObjectCount, Is.EqualTo(1));
            Assert.That(GetCells(GetDraft().GetCellObjectAt(0)), Is.EqualTo(s_row));
            Assert.That(canvas.Model.IsBarrier(new Vector2Int(1, 1)), Is.True, "The canvas model follows the draft.");
            string painted = JsonUtility.ToJson(GetDraft());
            Undo.PerformUndo();
            Assert.That(JsonUtility.ToJson(GetDraft()), Is.EqualTo(plain), "One undo reverts the stroke.");
            Undo.PerformRedo();
            Assert.That(JsonUtility.ToJson(GetDraft()), Is.EqualTo(painted));

            Drag(root, canvas, new Vector2Int(2, 2), new Vector2Int(3, 2), 0);
            Assert.That(GetDraft().CellObjectCount, Is.EqualTo(1), "The touching stroke joins barrier-1.");
            Assert.That(GetDraft().GetCellObjectAt(0).CellCount, Is.EqualTo(5));
            string joined = JsonUtility.ToJson(GetDraft());

            // The Eraser splits it; a right drag with the brush erases the rest of a piece. The brush keeps the Pen, where
            // 3 picks a colour (TASK-20260926-180414067-b923c8a9), so the Eraser button picks it up.
            Press(root.Q<Button>("tool-eraser"));
            Drag(root, canvas, new Vector2Int(1, 1), new Vector2Int(1, 1), 0);
            Assert.That(GetDraft().CellObjectCount, Is.EqualTo(2), "The barrier split into two objects.");
            Undo.PerformUndo();
            Assert.That(JsonUtility.ToJson(GetDraft()), Is.EqualTo(joined), "One undo restores the whole barrier.");
            Undo.PerformRedo();
            Press(root.Q<Button>("obstacle-barrier"));
            Drag(root, canvas, new Vector2Int(0, 1), new Vector2Int(0, 1), 1);
            Assert.That(GetDraft().CellObjectCount, Is.EqualTo(1), "An empty barrier is removed.");
            Assert.That(canvas.ObstacleBrush, Is.EqualTo(LevelObstacleBrush.Barrier), "The brush stays armed.");
            Assert.That(GetDraft().BlockCount, Is.EqualTo(3), "Barrier strokes keep every block.");

            // A new barrier starts closed, so a stroke over b1 is refused whole with the validation message.
            string before = JsonUtility.ToJson(GetDraft());
            Drag(root, canvas, new Vector2Int(0, 3), new Vector2Int(1, 3), 0);
            Assert.That(JsonUtility.ToJson(GetDraft()), Is.EqualTo(before));
            Assert.That(root.Q<Label>("status").text, Does.EndWith("effect barrier starts closed on block b1 at (1, 3)."));
        }

        [UnityTest]
        public IEnumerator SelectPicksABarrierWithAnOutlineAndKeepsItAcrossAnUnrelatedEdit()
        {
            yield return ShowWindowBuiltByUnity();
            VisualElement root = m_window.rootVisualElement;
            LevelCanvasElement canvas = root.Q<LevelCanvasElement>();
            Assert.That(IsLevelLoaded(root) && canvas.Geometry.IsValid, Is.True, "The fixture database has a loaded level.");

            LoadFixture(root, WithObjects(LevelObstacleWallCommandsTests.CreateLevel32(),
                LevelEffectTests.Object("barrier-1", LevelEffectTests.Effect("barrier", ("startsOpen", "true")), s_row)));
            Assert.That(canvas.Model.GetObstacle(0).StartsOpen, Is.True, "The model keeps the state the overlay shows.");
            SendKey(canvas, KeyCode.Alpha1);
            Drag(root, canvas, new Vector2Int(1, 1), new Vector2Int(1, 1), 0);
            Assert.That(canvas.Selection, Is.EqualTo(LevelCanvasSelection.ForObstacle(0, "barrier-1")),
                "The canvas outlines the selected barrier.");
            Assert.That(root.Q<Foldout>("blocks-group").Q<DropdownField>().value, Is.EqualTo("Obstacle barrier-1"),
                "Selected Block names the obstacle, as it names a selected door.");
            Assert.That(canvas.IsDragging, Is.False, "A barrier never drags.");

            // An unrelated edit (a right click removing b3) keeps the barrier selected.
            Drag(root, canvas, new Vector2Int(3, 0), new Vector2Int(3, 0), 1);
            Assert.That(GetDraft().BlockCount, Is.EqualTo(2));
            Assert.That(canvas.Selection, Is.EqualTo(LevelCanvasSelection.ForObstacle(0, "barrier-1")));
            Undo.PerformUndo();
            Assert.That(canvas.Selection, Is.EqualTo(LevelCanvasSelection.ForObstacle(0, "barrier-1")),
                "Undo keeps it too.");
        }

        // Drives the window's own SaveLevel, not a reimplemented shim: a plain level reaches the fake write seam, and
        // a barrier level is refused by the gate before that seam ever runs.
        [UnityTest]
        public IEnumerator SaveGateRefusesABarrierLevelWithTheRuntimeSupportMessage()
        {
            yield return ShowWindowBuiltByUnity(CreateWindowWithFakeWrite());
            VisualElement root = m_window.rootVisualElement;
            Assert.That(IsLevelLoaded(root), Is.True, "The fixture database has a loaded level.");

            LevelData plain = LevelObstacleWallCommandsTests.CreateLevel32();
            LoadFixture(root, plain);
            Assert.That(SaveLevel(), Is.True, "A level without a barrier saves as before.");
            Assert.That(m_writes, Has.Count.EqualTo(1), "The write seam saw the plain level.");

            LevelData barrier = WithObjects(plain,
                LevelEffectTests.Object("barrier-1", LevelBarrierCommands.CreateEffect(), s_row));
            LoadFixture(root, barrier);
            bool saved = SaveLevel();
            Assert.That(saved, Is.EqualTo(LevelEffectTests.IsImplemented("barrier")));
            if (!saved)
            {
                Assert.That(m_writes, Has.Count.EqualTo(1), "The barrier gate refuses Save Level before any write.");
                Assert.That(root.Q<Label>("status").text, Is.EqualTo("Level level-32 cell object barrier-1 effect " +
                                                                      "barrier is not implemented in gameplay yet."));
            }
        }

        private bool SaveLevel()
        {
            return (bool)LevelEditorTestAccess.Invoke(m_window, "SaveLevel", null);
        }

        // Every seam is a fake: nothing reaches the level JSON or the piece library. The barrier gate is expected to
        // refuse before this write ever runs, so it only needs to record that it ran, not persist anything.
        private LevelDatabaseWindow CreateWindowWithFakeWrite()
        {
            LevelDatabaseWindow window = ScriptableObject.CreateInstance<LevelDatabaseWindow>();
            window.IsPieceEditorOpen = () => false;
            window.ShowLevelPieceInEditor = _ => { };
            window.ShowLibraryPieceInEditor = _ => { };
            window.CommitTemplatesToLibrary = _ => "No template is committed in this test.";
            window.WriteLevels = levels =>
            {
                m_writes.Add(new List<LevelData>(levels));
                return true;
            };
            return window;
        }

        // The validation message of the level with one more single-cell block b4 at cell.
        private static string GetValidationError(LevelData level, Vector2Int cell)
        {
            LevelBlockData[] blocks = new LevelBlockData[level.BlockCount + 1];
            for (int index = 0; index < level.BlockCount; index++)
            {
                blocks[index] = level.GetBlockAt(index);
            }

            blocks[level.BlockCount] = new LevelBlockData("b4", "Red", cell, s_single);
            LevelData probe = LevelRecordCopy.WithBlocks(level, blocks);
            Assert.That(probe.Validate(out string error), Is.False);
            return error;
        }

        private static LevelData WithObjects(LevelData level, params LevelCellObjectData[] objects)
        {
            List<LevelCellObjectData> all = new List<LevelCellObjectData>(LevelRecordCopy.GetCellObjects(level));
            all.AddRange(objects);
            return LevelRecordCopy.WithCellObjects(level, all.ToArray());
        }

        private static Vector2Int[] GetCells(LevelCellObjectData item)
        {
            return LevelRecordCopy.GetCells(item);
        }

        private static void Drag(VisualElement root, LevelCanvasElement canvas, Vector2Int from, Vector2Int to, int button)
        {
            Vector2 start = canvas.LocalToWorld(canvas.Geometry.GetCellRect(from).center);
            Vector2 end = canvas.LocalToWorld(canvas.Geometry.GetCellRect(to).center);
            UiTestInput.SendPointer(root, EventType.MouseDown, start, button);
            UiTestInput.SendPointer(root, EventType.MouseDrag, end, button);
            UiTestInput.SendPointer(root, EventType.MouseUp, end, button);
        }

        private static void SendKey(VisualElement target, KeyCode key)
        {
            UiTestInput.SendKey(target, key);
        }

        private static void Press(Button button)
        {
            UiTestInput.Press(button);
        }

        // Replaces the window's draft with a known level, as a Reload would, and refreshes the canvas.
        private void LoadFixture(VisualElement root, LevelData level)
        {
            m_fixture.LoadFixture(root, level);
        }

        private LevelData GetDraft()
        {
            return m_fixture.GetDraft();
        }

        private static bool IsLevelLoaded(VisualElement root)
        {
            return LevelEditorWindowFixture.IsLevelLoaded(root);
        }

        // Unity builds the GUI itself, so a later automatic CreateGUI cannot clear the undo records under a test.
        // A caller with its own fake seams (the save gate test's fake write) passes its own configured window.
        private IEnumerator ShowWindowBuiltByUnity(LevelDatabaseWindow window = null)
        {
            m_fixture = new LevelEditorWindowFixture();
            yield return window != null ? m_fixture.ShowBuiltByUnity(window) : m_fixture.ShowBuiltByUnity();
            m_window = m_fixture.Window;
            // A fixture level, deterministic whatever the real project's active database holds (Tests-Editor-1-F3).
            m_fixture.LoadDatabase();
        }

        private static void ValidateLayout(VisualElement root)
        {
            UiTestInput.ValidateLayout(root);
        }
    }
}
