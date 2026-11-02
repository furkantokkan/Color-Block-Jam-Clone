using System;
using System.Text.RegularExpressions;
using ColorBlockJam.Editor.LevelPreview;
using ColorBlockJam.Editor.Levels;
using ColorBlockJam.Environment;
using ColorBlockJam.Presentation.Board;
using ColorBlockJam.Runtime.Art;
using ColorBlockJam.Runtime.Board;
using ColorBlockJam.Runtime.Layout;
using ColorBlockJam.Runtime.Levels;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace ColorBlockJam.Tests.LevelPreview
{
    /// <summary>
    /// Verifies the scene preview's solution playback frames (TASK-20260928-091128541-cb48c84a AC-11, DEC-170): the
    /// host's frame entry skips only LevelData.Validate, so a frame shows its blocks where the steps moved them and the
    /// cleared board shows the play area without a block, while TryShow still refuses a level without blocks, a
    /// frame the host cannot map, dress or build keeps the previous frame, and a draft refused after a playback shows
    /// the last valid draft again, not the frame; and the owner, in the LevelEditor scene opened additively and closed
    /// without saving, shows the window's frames, never keeps one as the published draft and shows the draft again on
    /// the next draft publish.
    /// </summary>
    public sealed class LevelPreviewPlaybackTests
    {
        private const string k_levelId = "preview-playback";

        private static readonly Vector2Int[] s_single = { Vector2Int.zero };
        private static readonly Vector2Int[] s_square =
        {
            new Vector2Int(0, 0), new Vector2Int(0, 1), new Vector2Int(1, 0), new Vector2Int(1, 1)
        };

        private BoardArt m_art;

        [SetUp]
        public void LoadArt()
        {
            m_art = LevelPreviewTestSupport.CreateBoardArt();
        }

        [Test]
        public void AFrameShowsItsBlocksWhereTheStepsMovedThemAndReusesTheWarmPools()
        {
            LevelPreviewHost host = null;
            try
            {
                host = CreateHost(SceneManager.GetActiveScene());
                Assert.That(host.TryShow(CreateDraft(), out string error), Is.True, error);
                int objects = LevelPreviewTestSupport.CountObjects(host.Root);

                Assert.That(host.TryShowFrame(CreateStepFrame(), out error), Is.True, error);

                Assert.That(host.LevelId, Is.EqualTo(k_levelId), "A frame keeps its level's id.");
                Assert.That(host.ActiveBlocks.Count, Is.EqualTo(2));
                AssertBlockOnCell(host, host.ActiveBlocks[0], new Vector2Int(0, 1));
                AssertBlockOnCell(host, host.ActiveBlocks[1], new Vector2Int(2, 2));
                Assert.That(LevelPreviewTestSupport.CountObjects(host.Root), Is.EqualTo(objects), "A frame reuses the warm pools.");
                LevelPreviewTestSupport.AssertEverythingIsDontSave(host.Root);
            }
            finally
            {
                host?.Dispose();
            }
        }

        [Test]
        public void TheClearedBoardShowsThePlayAreaWithoutABlock()
        {
            Scene scene = SceneManager.GetActiveScene();
            int rootsBefore = scene.rootCount;
            LevelPreviewHost host = null;
            try
            {
                host = CreateHost(scene);

                // A fresh host may start on the cleared board: its first build prepares no block pool.
                Assert.That(host.TryShowFrame(CreateBoard(), out string error), Is.True, error);
                AssertClearedBoard(host);
                Assert.That(scene.rootCount, Is.EqualTo(rootsBefore + 1), "One host root lives in the target scene.");

                // The draft, its cleared board at the end of a playback, then the draft again.
                Assert.That(host.TryShow(CreateDraft(), out error), Is.True, error);
                Assert.That(host.ActiveBlocks.Count, Is.EqualTo(2));
                Assert.That(host.TryShowFrame(CreateBoard(), out error), Is.True, error);
                AssertClearedBoard(host);
                Assert.That(host.TryShow(CreateDraft(), out error), Is.True, error);
                AssertBlockOnCell(host, host.ActiveBlocks[0], new Vector2Int(1, 1));
                AssertBlockOnCell(host, host.ActiveBlocks[1], new Vector2Int(2, 2));
            }
            finally
            {
                host?.Dispose();
            }

            Assert.That(scene.rootCount, Is.EqualTo(rootsBefore), "Dispose leaves nothing behind.");
        }

        [Test]
        public void TryShowStillRefusesALevelWithoutABlockBeforeTheSceneChanges()
        {
            Scene scene = SceneManager.GetActiveScene();
            int rootsBefore = scene.rootCount;
            LevelData cleared = CreateBoard();
            Assume.That(cleared.Validate(out string reason), Is.False);
            LevelPreviewHost host = null;
            try
            {
                host = CreateHost(scene);

                Assert.That(host.TryShow(cleared, out string error), Is.False);
                Assert.That(error, Is.EqualTo(reason).And.Contain("needs at least one block"));
                Assert.That(host.Root, Is.Null, "The scene is not touched.");
                Assert.That(scene.rootCount, Is.EqualTo(rootsBefore));

                Assert.That(host.TryShow(CreateDraft(), out error), Is.True, error);
                BlockPiece first = host.ActiveBlocks[0];
                Assert.That(host.TryShow(cleared, out error), Is.False);
                Assert.That(error, Does.Contain("needs at least one block"));
                Assert.That(host.ActiveBlocks.Count, Is.EqualTo(2), "The shown draft stays.");
                Assert.That(host.ActiveBlocks[0], Is.SameAs(first));
            }
            finally
            {
                host?.Dispose();
            }
        }

        [Test]
        public void AFrameTheHostCannotMapKeepsThePreviousFrameAndReturnsTheReason()
        {
            LevelPreviewHost host = null;
            try
            {
                host = CreateHost(SceneManager.GetActiveScene());
                Assert.That(host.TryShow(CreateDraft(), out string error), Is.True, error);
                Assert.That(host.TryShowFrame(CreateStepFrame(), out error), Is.True, error);
                BlockPiece stepped = host.ActiveBlocks[0];
                int objects = LevelPreviewTestSupport.CountObjects(host.Root);

                LevelData unknownColour = CreateBoard(Block("b1", "Magenta", new Vector2Int(0, 1), s_single),
                    Block("b2", "Pink", new Vector2Int(2, 2), s_square));
                Assert.That(host.TryShowFrame(unknownColour, out error), Is.False);

                Assert.That(error, Does.Contain("Block b1").And.Contain("unknown colour 'Magenta'"));
                Assert.That(host.ActiveBlocks.Count, Is.EqualTo(2), "The previous frame stays.");
                Assert.That(host.ActiveBlocks[0], Is.SameAs(stepped));
                AssertBlockOnCell(host, stepped, new Vector2Int(0, 1));
                Assert.That(LevelPreviewTestSupport.CountObjects(host.Root), Is.EqualTo(objects), "The scene did not change.");
            }
            finally
            {
                host?.Dispose();
            }
        }

        [Test]
        public void AFrameWhoseEffectTheHostCannotShowKeepsThePreviousFrame()
        {
            BoardPieceCatalog catalog = LevelPreviewTestSupport.CreateCatalogWithoutArrows(m_art.Catalog);
            LevelPreviewHost host = null;
            try
            {
                host = LevelPreviewTestSupport.CreateHost(SceneManager.GetActiveScene(),
                    new BoardArt(m_art.Palette, catalog),
                    () => new ILevelPreviewEffect[] { new ArrowPreviewEffect() });
                Assert.That(host.TryShow(CreateDraft(), out string error), Is.True, error);
                BlockPiece first = host.ActiveBlocks[0];

                // The effect checks run for frames too: a Horizontal Arrow single needs Arrow_1, which this catalog lacks.
                LevelData arrowFrame = CreateBoard(
                    Block("b1", "Red", new Vector2Int(0, 1), s_single,
                        new LevelEffectData("arrow", new[] { new LevelEffectParamData("axis", "Horizontal") })),
                    Block("b2", "Pink", new Vector2Int(2, 2), s_square));
                Assert.That(host.TryShowFrame(arrowFrame, out error), Is.False);

                Assert.That(error, Does.Contain("b1").And.Contain("Arrow_1"));
                Assert.That(host.ActiveBlocks.Count, Is.EqualTo(2), "The previous board stays.");
                Assert.That(host.ActiveBlocks[0], Is.SameAs(first));
                AssertBlockOnCell(host, first, new Vector2Int(1, 1));
                Assert.That(host.GetEffect<ArrowPreviewEffect>().ActiveMarkers, Is.Empty);
            }
            finally
            {
                host?.Dispose();
                Object.DestroyImmediate(catalog);
            }
        }

        [Test]
        public void AFrameWhoseBuildFailsShowsThePreviousFrameAgain()
        {
            Scene scene = SceneManager.GetActiveScene();
            int rootsBefore = scene.rootCount;
            Transform destroyed = new GameObject("Destroyed Blocks Root").transform;
            Object.DestroyImmediate(destroyed.gameObject);
            LevelPreviewHost host = null;
            try
            {
                host = CreateHost(scene);
                Assert.That(host.TryShow(CreateDraft(), out string error), Is.True, error);
                Assert.That(host.TryShowFrame(CreateBoard(), out error), Is.True, error);

                // Fault injection: a board with more blocks than the root pool holds (DEC-193: one root pool) builds
                // a new block factory under a destroyed parent, which throws in the middle of the build.
                LevelPreviewTestSupport.SetBlocksRoot(host, destroyed);
                LevelData threeBlockFrame = CreateBoard(Block("b3", "Pink", Vector2Int.zero, s_single),
                    Block("b4", "Pink", new Vector2Int(1, 0), s_single), Block("b5", "Pink", new Vector2Int(3, 0), s_single));
                Assert.That(host.TryShowFrame(threeBlockFrame, out error), Is.False);

                Assert.That(error, Is.Not.Empty);
                AssertClearedBoard(host);
                Assert.That(scene.rootCount, Is.EqualTo(rootsBefore + 1), "The failed build's root was replaced, not kept.");

                // The next show builds under the rebuilt root.
                Assert.That(host.TryShow(CreateDraft(), out error), Is.True, error);
                Assert.That(host.ActiveBlocks.Count, Is.EqualTo(2));
            }
            finally
            {
                host?.Dispose();
            }

            Assert.That(scene.rootCount, Is.EqualTo(rootsBefore), "Dispose leaves nothing behind.");
        }

        [Test]
        public void ADraftRefusedAfterAPlaybackShowsTheLastValidDraftAgain()
        {
            LevelPreviewHost host = null;
            try
            {
                host = CreateHost(SceneManager.GetActiveScene());
                Assert.That(host.TryShow(CreateDraft(), out string error), Is.True, error);
                Assert.That(host.TryShowFrame(CreateStepFrame(), out error), Is.True, error);
                Assert.That(host.TryShowFrame(CreateBoard(), out error), Is.True, error);
                Assume.That(host.ActiveBlocks, Is.Empty, "The playback ended on its cleared board.");

                // An undo back to a draft with errors after the playback: the draft is refused, and the preview shows
                // the last valid draft again (DEC-102), not the playback's last frame.
                LevelData overlapping = CreateBoard(Block("b1", "Red", new Vector2Int(2, 2), s_single),
                    Block("b2", "Pink", new Vector2Int(2, 2), s_square));
                Assume.That(overlapping.Validate(out string reason), Is.False);
                Assert.That(host.TryShow(overlapping, out error), Is.False);

                Assert.That(error, Is.EqualTo(reason), "The refused draft's own reason is returned.");
                Assert.That(host.LevelId, Is.EqualTo(k_levelId));
                Assert.That(host.ActiveBlocks.Count, Is.EqualTo(2), "The last valid draft shows again.");
                AssertBlockOnCell(host, host.ActiveBlocks[0], new Vector2Int(1, 1));
                AssertBlockOnCell(host, host.ActiveBlocks[1], new Vector2Int(2, 2));
                LevelPreviewTestSupport.AssertEverythingIsDontSave(host.Root);

                // A draft refused while the valid draft shows keeps it, as before.
                BlockPiece first = host.ActiveBlocks[0];
                int objects = LevelPreviewTestSupport.CountObjects(host.Root);
                Assert.That(host.TryShow(overlapping, out error), Is.False);
                Assert.That(host.ActiveBlocks.Count, Is.EqualTo(2));
                Assert.That(host.ActiveBlocks[0], Is.SameAs(first));
                Assert.That(LevelPreviewTestSupport.CountObjects(host.Root), Is.EqualTo(objects));

                // Frames still show while they come, and a later valid draft replaces them as before.
                Assert.That(host.TryShowFrame(CreateBoard(), out error), Is.True, error);
                Assert.That(host.ActiveBlocks, Is.Empty);
                Assert.That(host.TryShow(CreateStepFrame(), out error), Is.True, error);
                AssertBlockOnCell(host, host.ActiveBlocks[0], new Vector2Int(0, 1));
            }
            finally
            {
                host?.Dispose();
            }
        }

        [Test]
        public void TheOwnerShowsPlaybackFramesAndThenTheDraftWithoutKeepingAFrameAsTheDraft()
        {
            Scene scene = SceneManager.GetSceneByPath(LevelPreviewOwner.ScenePath);
            bool openedHere = !scene.isLoaded;
            LevelData publishedBefore = LevelDatabaseWindow.LastPublishedLevel;
            LevelData draft = CreateDraft();
            try
            {
                // The owner starts without a preview: no published draft when the scene opens, or a null publish when
                // the scene was already open.
                LevelPreviewTestSupport.SetLastPublishedLevel(null);
                if (openedHere)
                {
                    scene = EditorSceneManager.OpenScene(LevelPreviewOwner.ScenePath, OpenSceneMode.Additive);
                }
                else
                {
                    RaiseDraftPublished(null);
                }

                Assert.That(LevelPreviewTestSupport.GetOwnerHost(), Is.Null);
                Assert.That(LevelPreviewTestSupport.HasOwnerHandler(nameof(LevelDatabaseWindow.SolutionFramePublished)), Is.True,
                    "The owner listens to playback frames while the LevelEditor scene is loaded.");

                // The window's draft is published; a playback raises its start board, a step and the cleared board.
                LevelPreviewTestSupport.SetLastPublishedLevel(draft);
                RaiseFrame(draft);
                LevelPreviewHost host = LevelPreviewTestSupport.GetOwnerHost();
                Assert.That(host, Is.Not.Null, "A frame creates the preview when none is shown.");
                Assert.That(host.Root.scene, Is.EqualTo(scene));
                AssertBlockOnCell(host, host.ActiveBlocks[0], new Vector2Int(1, 1));

                RaiseFrame(CreateStepFrame());
                Assert.That(LevelPreviewTestSupport.GetOwnerHost(), Is.SameAs(host));
                AssertBlockOnCell(host, host.ActiveBlocks[0], new Vector2Int(0, 1));

                RaiseFrame(null);
                Assert.That(host.ActiveBlocks.Count, Is.EqualTo(2), "A null frame is ignored.");

                RaiseFrame(CreateBoard());
                Assert.That(host.ActiveBlocks, Is.Empty, "The cleared board shows no block.");
                Assert.That(host.Root, Is.Not.Null, "The play area stays.");

                // A frame the preview cannot show keeps the previous one and warns once, however often it comes.
                LevelData unknownColour = CreateBoard(Block("b1", "Magenta", new Vector2Int(0, 1), s_single));
                LogAssert.Expect(LogType.Warning, new Regex("keeps the previous playback frame"));
                RaiseFrame(unknownColour);
                RaiseFrame(unknownColour);
                LogAssert.NoUnexpectedReceived();
                Assert.That(host.ActiveBlocks, Is.Empty, "The cleared board stays.");
                Assert.That(LevelDatabaseWindow.LastPublishedLevel, Is.SameAs(draft), "No frame becomes the published draft.");

                // The window publishes its draft again when the playback ends or stops.
                RaiseDraftPublished(draft);
                Assert.That(LevelPreviewTestSupport.GetOwnerHost(), Is.SameAs(host));
                Assert.That(host.ActiveBlocks.Count, Is.EqualTo(2));
                AssertBlockOnCell(host, host.ActiveBlocks[0], new Vector2Int(1, 1));
                AssertBlockOnCell(host, host.ActiveBlocks[1], new Vector2Int(2, 2));
                Assert.That(LevelDatabaseWindow.LastPublishedLevel, Is.SameAs(draft));

                if (openedHere)
                {
                    // Closing the scene is the owner's stop point: it stops listening and removes the preview.
                    Assert.That(EditorSceneManager.CloseScene(scene, true), Is.True);
                    Assert.That(LevelPreviewTestSupport.GetOwnerHost(), Is.Null);
                    Assert.That(LevelPreviewTestSupport.HasOwnerHandler(nameof(LevelDatabaseWindow.SolutionFramePublished)), Is.False);
                    Assert.That(LevelPreviewTestSupport.HasOwnerHandler(nameof(LevelDatabaseWindow.DraftLevelPublished)), Is.False);
                }
            }
            finally
            {
                if (openedHere && scene.IsValid() && scene.isLoaded)
                {
                    EditorSceneManager.CloseScene(scene, true);
                }

                LevelPreviewTestSupport.SetLastPublishedLevel(publishedBefore);
                if (!openedHere)
                {
                    // The scene was open before the test: its preview shows the published level again.
                    RaiseDraftPublished(publishedBefore);
                }
            }
        }

        // The play area stands, a floor tile on every cell, and no block root is active.
        private static void AssertClearedBoard(LevelPreviewHost host)
        {
            Assert.That(host.LevelId, Is.EqualTo(k_levelId));
            Assert.That(host.ActiveBlocks, Is.Empty);
            Assert.That(host.BlocksRoot.GetComponentsInChildren<BlockPiece>(false), Is.Empty, "No block root is active.");
            for (int y = 0; y < 4; y++)
            {
                for (int x = 0; x < 4; x++)
                {
                    Assert.That(FindFloorTile(host, new Vector2Int(x, y)), Is.Not.Null, $"Floor tile at ({x},{y}).");
                }
            }

            LevelPreviewTestSupport.AssertEverythingIsDontSave(host.Root);
        }

        // The block root and the floor tile of the cell share a world position, so the block sits on that cell.
        private static void AssertBlockOnCell(LevelPreviewHost host, BlockPiece block, Vector2Int cell)
        {
            BoardPieceView tile = FindFloorTile(host, cell);
            Assert.That(tile, Is.Not.Null, $"No floor tile at {cell}.");
            Assert.That(Vector3.Distance(block.transform.position, tile.transform.position), Is.LessThan(1e-4f),
                $"The block sits on {cell}.");
        }

        private static BoardPieceView FindFloorTile(LevelPreviewHost host, Vector2Int cell)
        {
            Assert.That(BoardLayout.TryCreate(CreateDraft(), out BoardLayout layout, out string error), Is.True, error);
            Vector3 local = layout.CellToLocal(cell);
            foreach (BoardPieceView piece in host.BlocksRoot.parent.GetComponentsInChildren<BoardPieceView>(false))
            {
                if (piece.name.StartsWith("Grid", StringComparison.Ordinal) && piece.transform.localPosition == local)
                {
                    return piece;
                }
            }

            return null;
        }

        private LevelPreviewHost CreateHost(Scene scene)
        {
            return LevelPreviewTestSupport.CreateHost(scene, m_art);
        }

        // The window's draft: a Red single at (1,1) beside its left gate and a Pink square at (2,2) under its top gate.
        private static LevelData CreateDraft()
        {
            return CreateBoard(Block("b1", "Red", new Vector2Int(1, 1), s_single),
                Block("b2", "Pink", new Vector2Int(2, 2), s_square));
        }

        // The draft after its first solver step: the Red single slid one cell left, next to its gate.
        private static LevelData CreateStepFrame()
        {
            return CreateBoard(Block("b1", "Red", new Vector2Int(0, 1), s_single),
                Block("b2", "Pink", new Vector2Int(2, 2), s_square));
        }

        // The draft's 4 x 4 board and id with these blocks, as LevelSolutionFrames builds a frame; no block gives the
        // cleared board.
        private static LevelData CreateBoard(params LevelBlockData[] blocks)
        {
            return new LevelData(k_levelId, "Playback", 4, 4, 60,
                new[]
                {
                    new LevelDoorData("d1", BoardSide.Left, 1, 1, "Red"),
                    new LevelDoorData("d2", BoardSide.Top, 2, 2, "Pink")
                },
                blocks);
        }

        private static LevelBlockData Block(string id, string colorId, Vector2Int origin,
            Vector2Int[] cells, params LevelEffectData[] effects)
        {
            return new LevelBlockData(id, colorId, origin, cells, effects);
        }

        private static void RaiseFrame(LevelData frame)
        {
            LevelPreviewTestSupport.Raise(nameof(LevelDatabaseWindow.SolutionFramePublished), frame);
        }

        private static void RaiseDraftPublished(LevelData level)
        {
            LevelPreviewTestSupport.Raise(nameof(LevelDatabaseWindow.DraftLevelPublished), level);
        }
    }
}
