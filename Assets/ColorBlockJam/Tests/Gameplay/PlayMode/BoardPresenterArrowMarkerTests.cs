#if UNITY_EDITOR
using System;
using System.Collections;
using ColorBlockJam.Domain.Board;
using ColorBlockJam.Domain.Commands;
using ColorBlockJam.Domain.Movement;
using ColorBlockJam.Domain.Session;
using ColorBlockJam.Gameplay.Board;
using ColorBlockJam.Gameplay.Effects;
using ColorBlockJam.Gameplay.Levels;
using ColorBlockJam.Gameplay.Session;
using ColorBlockJam.Presentation.Board;
using ColorBlockJam.Runtime.Board;
using ColorBlockJam.Runtime.Layout;
using ColorBlockJam.Runtime.Levels;
using ColorBlockJam.Tests.Gameplay.PlayMode.Fixtures;
using NUnit.Framework;
using PrimeTween;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace ColorBlockJam.Tests.Gameplay.PlayMode
{
    /// <summary>
    /// Proves the Game scene's Arrow markers (TASK-20260926-212320271-98b25efa-level-editor-arrow-markers AC-4, moved
    /// behind ArrowEffectView by TASK-20260926-230921278-a3ffdc40 revision 2) with the real environment prefab and
    /// board piece catalog. The presenter gets the Arrow module through a BlockEffectContextSource and
    /// EffectAttemptHookSource, as the Game scene does, and builds through the session's TryBuild(level): the view
    /// warms one marker per Arrow block and part on Home, puts each on its block root at the bounding-box centre,
    /// clips it with its block's exit, returns it when the exit ends and on teardown, keeps its pools on Restart
    /// without instantiating, re-attaches a Rocket-split block's marker and gives each part one with its parent's axis
    /// (B-19), and a level change disposes them. A catalog without the needed Arrow prefab rejects the level.
    /// </summary>
    public sealed class BoardPresenterArrowMarkerTests
    {
        private const int k_exitBlock = 0;
        private const int k_horizontalBlock = 1;
        private const int k_plainBlock = 2;

        private static readonly int s_colorId = Shader.PropertyToID("_PaletteUV");
        private static readonly int s_clipPlaneId = Shader.PropertyToID("_ClipPlane");
        private static readonly int s_clipEnabledId = Shader.PropertyToID("_ClipEnabled");
        private static readonly Cell s_exitOrigin = new Cell(1, 2);

        // The Vertical 2 x 2 shows one Arrow_2 or two Arrow_1 diagonal parts at once, the Horizontal double one Arrow_2
        // or one Arrow_1 part (FragmentCapacity.AddArrowMarkerCapacity): Arrow_1 x 3 plus Arrow_2 x 2 (B-19).
        private const int k_preparedMarkers = 5;

        private static readonly Vector2Int[] s_square =
        {
            new Vector2Int(0, 0), new Vector2Int(1, 0), new Vector2Int(0, 1), new Vector2Int(1, 1)
        };
        private static readonly Vector2Int[] s_double = { new Vector2Int(0, 0), new Vector2Int(1, 0) };
        private static readonly Vector2Int[] s_single = { new Vector2Int(0, 0) };

        private PresenterScene m_scene;
        private BoardArt m_art;
        private LevelData m_data;
        private AttemptLevel m_level;
        private BlockEffectContextSource m_source;
        private EffectAttemptHookSource m_hooks;
        private BoardPresenter m_presenter;
        private BoardSimulation m_simulation;

        [SetUp]
        public void SetUp()
        {
            m_scene = new PresenterScene();
            m_art = PresenterScene.CreateArt();
            m_data = CreateArrowLevel();
            m_level = Convert(m_data);
            m_source = CreateSource();
            m_hooks = new EffectAttemptHookSource(m_source);
            m_presenter = CreatePresenter(m_art, m_source);
            Assert.That(Build(m_presenter, m_source, m_level, m_data, out string error), Is.True, error);
            m_simulation = StartAttempt(m_level);
            m_presenter.Place(m_simulation.Preview);
        }

        [TearDown]
        public void TearDown()
        {
            m_presenter.Dispose();
            m_scene.Dispose();
        }

        private ArrowEffectView Arrows => (ArrowEffectView)m_presenter.GetEffectView("arrow");

        [Test]
        public void EachArrowBlockShowsItsVariantOnItsRootCentredTurnedAndTinted()
        {
            BoardPresenter presenter = m_presenter;

            // Superseded by the booster contract (B-19, AC-16): Home also warms the markers the Arrow blocks' Rocket
            // parts can show at once, so the one-per-block count of 2 is now k_preparedMarkers.
            Assert.That(Arrows.PreparedArrowMarkerCount, Is.EqualTo(k_preparedMarkers),
                "Home warms the markers each Arrow block and its Rocket parts can show at once.");

            // The user's b2 case: a Vertical 2 x 2 is centred between its columns and rows.
            BoardPieceView vertical = Arrows.GetArrowMarker(k_exitBlock);
            AssertMarker(vertical, presenter.GetPiece(k_exitBlock).transform, 2, new Vector3(0.5f, 0f, 0.5f), Vector3.forward);
            AssertCentredOnBlock(vertical, presenter.GetPiece(k_exitBlock));

            BoardPieceView horizontal = Arrows.GetArrowMarker(k_horizontalBlock);
            AssertMarker(horizontal, presenter.GetPiece(k_horizontalBlock).transform, 2, new Vector3(0.5f, 0f, 0f), Vector3.right);

            Assert.That(Arrows.GetArrowMarker(k_plainBlock), Is.Null, "A block without Arrow has no marker.");
            Assert.That(CountMarkersUnder(presenter.GetPiece(k_plainBlock).transform), Is.Zero);
        }

        [Test]
        public void TheMarkerMovesWithItsBlockRoot()
        {
            Transform root = m_presenter.GetPiece(k_horizontalBlock).transform;
            Vector3 offset = Arrows.GetArrowMarker(k_horizontalBlock).transform.position - root.position;

            // The drag follow and the exit tween move only the root.
            root.localPosition += new Vector3(0.4f, 0f, 0.3f);

            Vector3 moved = Arrows.GetArrowMarker(k_horizontalBlock).transform.position - root.position;
            Assert.That(Vector3.Distance(moved, offset), Is.LessThan(1e-5f));
        }

        [UnityTest]
        public IEnumerator AnExitClipsTheMarkerWithItsBlockAndReturnsItWhenTheExitEnds()
        {
            BoardPresenter presenter = m_presenter;
            BlockPiece piece = presenter.GetPiece(k_exitBlock);
            BoardPieceView marker = Arrows.GetArrowMarker(k_exitBlock);
            Transform boardRoot = m_scene.Environment.BoardRoot;
            int objects = CountObjects(boardRoot);

            Assert.That(m_simulation.Commands.Execute(GameCommand.MoveAndExit(k_exitBlock, s_exitOrigin, s_exitOrigin, Side.Top)),
                Is.EqualTo(CommandOutcome.Applied), "The Vertical block leaves through the door on its axis side.");
            presenter.Sync(m_simulation, default);

            MaterialPropertyBlock markerProperties = ReadProperties(marker.Renderer);
            MaterialPropertyBlock tileProperties = ReadProperties(FindTileRenderer(piece, marker));
            Assert.That(markerProperties.GetFloat(s_clipEnabledId), Is.EqualTo(1f), "The marker clips at the door line.");
            Assert.That(markerProperties.GetVector(s_clipPlaneId), Is.EqualTo(tileProperties.GetVector(s_clipPlaneId)),
                "With the same plane as its block.");

            yield return WaitForTweens(piece.transform);
            m_simulation.Events.Clear();
            presenter.Sync(m_simulation, default);

            Assert.That(presenter.GetPiece(k_exitBlock), Is.Null);
            Assert.That(Arrows.GetArrowMarker(k_exitBlock), Is.Null, "The exit returned the marker with its root.");
            Assert.That(marker.gameObject.activeSelf, Is.False);
            Assert.That(marker.transform.parent, Is.SameAs(boardRoot), "The marker waits under BoardRoot, not on the pooled root.");
            Assert.That(CountMarkersUnder(piece.transform), Is.Zero);
            Assert.That(CountObjects(boardRoot), Is.EqualTo(objects), "Nothing was instantiated during play.");
            Assert.That(Arrows.GetArrowMarker(k_horizontalBlock), Is.Not.Null, "The other Arrow block keeps its marker.");
        }

        [Test]
        public void TeardownReturnsEveryMarkerAndRestartReattachesThemWithoutInstantiating()
        {
            BoardPresenter presenter = m_presenter;
            Transform boardRoot = m_scene.Environment.BoardRoot;
            BoardPieceView vertical = Arrows.GetArrowMarker(k_exitBlock);
            BoardPieceView horizontal = Arrows.GetArrowMarker(k_horizontalBlock);
            int objects = CountObjects(boardRoot);

            // Teardown steps 3 and 6.
            presenter.StopTweens();
            presenter.ReturnPieces();

            Assert.That(Arrows.GetArrowMarker(k_exitBlock), Is.Null);
            Assert.That(Arrows.GetArrowMarker(k_horizontalBlock), Is.Null);
            Assert.That(vertical.gameObject.activeSelf, Is.False);
            Assert.That(horizontal.gameObject.activeSelf, Is.False);
            Assert.That(vertical.transform.parent, Is.SameAs(boardRoot));
            Assert.That(horizontal.transform.parent, Is.SameAs(boardRoot));

            // Restart: a new attempt of the same immutable level, built through the session's TryBuild(level).
            BoardSimulation restarted = StartAttempt(m_level);
            Assert.That(presenter.TryBuild(m_level, out string error), Is.True, error);
            presenter.Place(restarted.Preview);

            Assert.That(Arrows.PreparedArrowMarkerCount, Is.EqualTo(k_preparedMarkers));
            Assert.That(CountObjects(boardRoot), Is.EqualTo(objects), "Restart reuses the warmed markers.");
            AssertMarker(Arrows.GetArrowMarker(k_exitBlock), presenter.GetPiece(k_exitBlock).transform, 2,
                new Vector3(0.5f, 0f, 0.5f), Vector3.forward);
            AssertMarker(Arrows.GetArrowMarker(k_horizontalBlock), presenter.GetPiece(k_horizontalBlock).transform, 2,
                new Vector3(0.5f, 0f, 0f), Vector3.right);
        }

        [Test]
        public void ARocketSplitReattachesEachPartsMarkerWithItsParentsAxisAndRestartLeavesNoneOnThePartSlots()
        {
            BoardPresenter presenter = m_presenter;
            Transform boardRoot = m_scene.Environment.BoardRoot;
            int markers = CountMarkersUnder(boardRoot);
            int objects = CountObjects(boardRoot);

            // The Vertical 2 x 2 at (1, 2) loses its lower right cell: an L whose box still spans two rows.
            Execute(GameCommand.RemoveCell(k_exitBlock, s_exitOrigin, new Cell(2, 2)));
            Assert.That(m_simulation.Board.BlockCount, Is.EqualTo(3), "an L is still one block");
            AssertMarker(Arrows.GetArrowMarker(k_exitBlock), presenter.GetPiece(k_exitBlock).transform, 2,
                new Vector3(0.5f, 0f, 0.5f), Vector3.forward);

            // Then its upper left cell: the diagonal leaves two single parts, each with an Arrow_1 along the parent's
            // axis on its own root, which stands at the parent's origin.
            Execute(GameCommand.RemoveCell(k_exitBlock, s_exitOrigin, new Cell(1, 3)));
            const int part = 3;
            Assert.That(m_simulation.Board.BlockCount, Is.EqualTo(4));
            Assert.That(presenter.GetPiece(part).Cells, Is.EquivalentTo(new[] { new Vector2Int(1, 1) }));
            AssertMarker(Arrows.GetArrowMarker(k_exitBlock), presenter.GetPiece(k_exitBlock).transform, 1,
                Vector3.zero, Vector3.forward);
            AssertMarker(Arrows.GetArrowMarker(part), presenter.GetPiece(part).transform, 1,
                new Vector3(1f, 0f, 1f), Vector3.forward);
            Assert.That(m_simulation.Hooks.GetAxes(m_simulation.Board, part), Is.EqualTo(AxisMask.Vertical),
                "the part's rules read the same axis as its marker");

            // The Horizontal double loses its right cell: an Arrow_1 on its remaining cell, still turned across.
            Execute(GameCommand.RemoveCell(k_horizontalBlock, new Cell(0, 0), new Cell(1, 0)));
            AssertMarker(Arrows.GetArrowMarker(k_horizontalBlock), presenter.GetPiece(k_horizontalBlock).transform, 1,
                Vector3.zero, Vector3.right);
            Assert.That(Arrows.GetArrowMarker(k_plainBlock), Is.Null, "a block without Arrow still has none");
            Assert.That(CountMarkersUnder(boardRoot), Is.EqualTo(markers), "every marker came from the warmed pools");
            Assert.That(CountObjects(boardRoot), Is.EqualTo(objects), "nothing was instantiated during play");

            // Teardown and Restart: the part slot has neither root nor marker, and the authored markers come back.
            presenter.StopTweens();
            presenter.ReturnPieces();
            BoardSimulation restarted = StartAttempt(m_level);
            Assert.That(presenter.TryBuild(m_level, out string error), Is.True, error);
            presenter.Place(restarted.Preview);

            Assert.That(presenter.GetPiece(part), Is.Null);
            Assert.That(Arrows.GetArrowMarker(part), Is.Null, "a part's marker leaves with its attempt");
            AssertMarker(Arrows.GetArrowMarker(k_exitBlock), presenter.GetPiece(k_exitBlock).transform, 2,
                new Vector3(0.5f, 0f, 0.5f), Vector3.forward);
            AssertMarker(Arrows.GetArrowMarker(k_horizontalBlock), presenter.GetPiece(k_horizontalBlock).transform, 2,
                new Vector3(0.5f, 0f, 0f), Vector3.right);
            Assert.That(CountMarkersUnder(boardRoot), Is.EqualTo(markers));
            Assert.That(CountObjects(boardRoot), Is.EqualTo(objects));
        }

        [UnityTest]
        public IEnumerator ALevelChangeOrDisposeLeavesNoMarker()
        {
            BoardPresenter presenter = m_presenter;
            Transform boardRoot = m_scene.Environment.BoardRoot;

            // The next level has no Arrow block.
            LevelData plainData = CreatePlainLevel();
            Assert.That(Build(presenter, m_source, Convert(plainData), plainData, out string error), Is.True, error);
            yield return null;

            Assert.That(Arrows.PreparedArrowMarkerCount, Is.Zero);
            Assert.That(CountMarkersUnder(boardRoot), Is.Zero, "The level change disposed the marker pools.");

            LevelData arrowData = CreateArrowLevel();
            Assert.That(Build(presenter, m_source, Convert(arrowData), arrowData, out error), Is.True, error);
            Assert.That(CountMarkersUnder(boardRoot), Is.EqualTo(k_preparedMarkers));
            presenter.Dispose();
            yield return null;

            Assert.That(CountMarkersUnder(boardRoot), Is.Zero, "Dispose leaves no marker.");
        }

        [Test]
        public void WithoutTheArrowModuleNoMarkerIsShownAndAnotherLevelsDataIsRefused()
        {
            // A presenter without effect views: the attempt level alone carries no effect.
            BoardPresenter presenter = new BoardPresenter(m_scene.Environment, m_art);
            try
            {
                Assert.That(presenter.TryBuild(m_level, out string error), Is.True, error);
                presenter.Place(new BoardSimulation(m_level.Model,
                    ColorBlockJam.Domain.Hooks.AttemptHooks.CreateEmpty(), DragSettings.Default).Preview);

                Assert.That(presenter.GetEffectView("arrow"), Is.Null);
                Assert.That(CountMarkersUnder(presenter.GetPiece(k_exitBlock).transform), Is.Zero);
            }
            finally
            {
                presenter.Dispose();
            }

            BlockEffectContextSource other = CreateSource();
            Assert.Throws<ArgumentException>(() => other.Prepare(Convert(CreatePlainLevel()), m_data),
                "Authored data from another level is refused.");
        }

        [Test]
        public void ALevelWhoseCatalogLacksItsArrowPrefabIsRejectedNamingTheBlock()
        {
            // An in-memory catalog copy with an empty environment list, so it has no Arrow prefab; the asset is unchanged.
            BoardPieceCatalog catalog = Object.Instantiate(m_art.Catalog);
            using (SerializedObject serialized = new SerializedObject(catalog))
            {
                serialized.FindProperty("m_environment").ClearArray();
                serialized.ApplyModifiedPropertiesWithoutUndo();
            }

            BlockEffectContextSource source = CreateSource();
            BoardPresenter presenter = CreatePresenter(new BoardArt(m_art.Palette, catalog), source);
            try
            {
                Assert.That(Build(presenter, source, m_level, m_data, out string error), Is.False);

                Assert.That(error, Does.Contain("exit").And.Contain("Arrow_2"), "The error names the block and its prefab.");
                Assert.That(presenter.GetEffectView("arrow"), Is.Null, "The rejected views were disposed.");
                Assert.That(presenter.ActivePieceCount, Is.Zero);

                // A level without Arrow still builds and places afterwards.
                LevelData plainData = CreatePlainLevel();
                AttemptLevel plain = Convert(plainData);
                Assert.That(Build(presenter, source, plain, plainData, out error), Is.True, error);
                presenter.Place(new BoardSimulation(plain.Model, new EffectAttemptHookSource(source).Create(plain),
                    DragSettings.Default).Preview);
                Assert.That(presenter.GetPiece(0) != null, Is.True, "The next level builds and places.");
            }
            finally
            {
                presenter.Dispose();
                Object.DestroyImmediate(catalog);
            }
        }

        // A 4 x 4 board: block 0 a red 2 x 2 at (1, 2) with a Vertical Arrow, flush below a red two-cell top door
        // over columns 1 and 2; block 1 a blue horizontal double at (0, 0) with a Horizontal Arrow; block 2 an orange
        // single at (3, 0) without effects.
        private static LevelData CreateArrowLevel()
        {
            return new LevelData("presenter-arrows", "Presenter arrows", 4, 4, 60,
                new[] { new LevelDoorData("top", BoardSide.Top, 1, 2, "Red") },
                new[]
                {
                    new LevelBlockData("exit", "Red", new Vector2Int(s_exitOrigin.X, s_exitOrigin.Y), s_square,
                        new[] { Arrow("Vertical") }),
                    new LevelBlockData("across", "Blue", new Vector2Int(0, 0), s_double,
                        new[] { Arrow("Horizontal") }),
                    new LevelBlockData("plain", "Orange", new Vector2Int(3, 0), s_single)
                });
        }

        // A 3 x 3 board with one red single and no effect.
        private static LevelData CreatePlainLevel()
        {
            return new LevelData("presenter-plain", "Presenter plain", 3, 3, 60,
                new[] { new LevelDoorData("top", BoardSide.Top, 1, 1, "Red") },
                new[] { new LevelBlockData("exit", "Red", new Vector2Int(1, 2), s_single) });
        }

        private static BlockEffectContextSource CreateSource()
        {
            return new BlockEffectContextSource(new IBlockEffectModule[] { new ArrowEffectModule() });
        }

        private BoardPresenter CreatePresenter(BoardArt art, BlockEffectContextSource source)
        {
            return new BoardPresenter(m_scene.Environment, art, source,
                new BlockEffectViewContext(art, m_scene.Environment.BoardRoot, null, null));
        }

        // GameAttemptLoader.Load on Home: the effect metadata for this exact level, then the session's TryBuild(level).
        private static bool Build(BoardPresenter presenter, BlockEffectContextSource source, AttemptLevel level,
            LevelData data, out string error)
        {
            source.Prepare(level, data);
            return presenter.TryBuild(level, out error);
        }

        // One attempt as LevelSession starts it: fresh hooks published for the level, then its simulation.
        private BoardSimulation StartAttempt(AttemptLevel level)
        {
            return new BoardSimulation(level.Model, m_hooks.Create(level), DragSettings.Default);
        }

        // One tick's command phase and view sync phase, as the session runs them.
        private void Execute(GameCommand command)
        {
            m_simulation.Events.Clear();
            Assert.That(m_simulation.Commands.Execute(command), Is.EqualTo(CommandOutcome.Applied), command.ToString());
            m_presenter.Sync(m_simulation, default);
        }

        private static AttemptLevel Convert(LevelData data)
        {
            Assert.That(BoardLayout.TryCreate(data, out BoardLayout layout, out string error), Is.True, error);
            Assert.That(AttemptLevelConverter.TryConvert(data, layout, out AttemptLevel level, out error), Is.True, error);
            return level;
        }

        private static LevelEffectData Arrow(string axis)
        {
            return new LevelEffectData("arrow", new[] { new LevelEffectParamData("axis", axis) });
        }

        // The catalog's Arrow_N on the shared material, on the root at the local position, long along the root
        // direction, unscaled and tinted with the palette's Arrow colour.
        private static void AssertMarker(BoardPieceView marker, Transform root, int variant, Vector3 localPosition, Vector3 along)
        {
            BoardArt art = PresenterScene.CreateArt();
            Assert.That(marker, Is.Not.Null);
            Assert.That(art.Catalog.TryGetArrow(variant, out BoardPieceView prefab), Is.True);
            Assert.That(marker.Filter.sharedMesh, Is.SameAs(prefab.Filter.sharedMesh), "Arrow_" + variant);
            Assert.That(marker.Renderer.sharedMaterial, Is.SameAs(prefab.Renderer.sharedMaterial), "One shared material.");
            Assert.That(marker.gameObject.activeSelf, Is.True);
            Assert.That(marker.transform.parent, Is.SameAs(root), "The marker rides on its block root.");
            Assert.That(Vector3.Distance(marker.transform.localPosition, localPosition), Is.LessThan(1e-4f), "Box centre.");
            Assert.That(marker.transform.localScale, Is.EqualTo(Vector3.one));
            Vector3 forward = root.InverseTransformDirection(marker.transform.forward);
            Assert.That(Vector3.Dot(forward, along), Is.EqualTo(1f).Within(1e-4f), "Long along its axis.");
            Assert.That(ReadProperties(marker.Renderer).GetVector(s_colorId), Is.EqualTo(art.ArrowColor.UV));
        }

        // The marker's mesh and the block's shown tiles share one centre on the board plane.
        private static void AssertCentredOnBlock(BoardPieceView marker, BlockPiece block)
        {
            Bounds tiles = default;
            bool first = true;
            foreach (MeshRenderer tile in block.GetComponentsInChildren<MeshRenderer>(false))
            {
                if (tile.GetComponentInParent<BoardPieceView>() == marker)
                {
                    continue;
                }

                if (first)
                {
                    tiles = tile.bounds;
                    first = false;
                }
                else
                {
                    tiles.Encapsulate(tile.bounds);
                }
            }

            Assert.That(first, Is.False, "The block shows its tiles.");
            Bounds arrow = marker.Renderer.bounds;
            Assert.That(arrow.center.x, Is.EqualTo(tiles.center.x).Within(1e-3f), "Centred across the block.");
            Assert.That(arrow.center.z, Is.EqualTo(tiles.center.z).Within(1e-3f), "Centred along the block.");
        }

        private static MeshRenderer FindTileRenderer(BlockPiece block, BoardPieceView marker)
        {
            foreach (MeshRenderer tile in block.GetComponentsInChildren<MeshRenderer>(false))
            {
                if (tile.GetComponentInParent<BoardPieceView>() != marker)
                {
                    return tile;
                }
            }

            Assert.Fail("The block shows no tile.");
            return null;
        }

        private static MaterialPropertyBlock ReadProperties(Renderer renderer)
        {
            MaterialPropertyBlock properties = new MaterialPropertyBlock();
            renderer.GetPropertyBlock(properties);
            return properties;
        }

        // The Arrow_1 to Arrow_3 meshes of Arrows.fbx; the gate meshes' names differ.
        private static bool IsMarker(BoardPieceView view)
        {
            Mesh mesh = view.Filter != null ? view.Filter.sharedMesh : null;
            return mesh != null && (mesh.name == "Arrow_1" || mesh.name == "Arrow_2" || mesh.name == "Arrow_3");
        }

        private static int CountMarkersUnder(Transform root)
        {
            int count = 0;
            foreach (BoardPieceView view in root.GetComponentsInChildren<BoardPieceView>(true))
            {
                count += IsMarker(view) ? 1 : 0;
            }

            return count;
        }

        private static int CountObjects(Transform root) => root.GetComponentsInChildren<Transform>(true).Length;

        private static IEnumerator WaitForTweens(Transform target)
        {
            float deadline = Time.realtimeSinceStartup + ExitMotionSettings.k_maxSeconds + 1f;
            while (Tween.GetTweensCount(target) > 0 && Time.realtimeSinceStartup < deadline)
            {
                yield return null;
            }

            Assert.That(Tween.GetTweensCount(target), Is.Zero, "The exit tween ends within its bound.");
        }
    }
}
#endif
