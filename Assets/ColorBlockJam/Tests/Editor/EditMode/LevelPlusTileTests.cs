using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using ColorBlockJam.Editor.BoardKit;
using ColorBlockJam.Editor.Levels;
using ColorBlockJam.Runtime.Board;
using ColorBlockJam.Runtime.Levels;
using NUnit.Framework;
using NUnit.Framework.Constraints;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace ColorBlockJam.Tests.EditMode
{
    /// <summary>
    /// Verifies the Level Editor side of the "+" level-piece tile and the Piece Editor link: the "+" request, Piece
    /// Editor changes (level filter, limits, naming, re-stamp of the piece's own blocks), template and level piece tiles
    /// driving an open Piece Editor, and Re-stamp after a template's footprint changed in the library (DEC-193).
    /// Handlers are driven directly, so the user's own windows never receive a test event, and nothing is saved.
    /// </summary>
    public sealed class LevelPlusTileTests
    {
        private const string k_levelId = "plus-fixture";
        private static readonly Vector2Int[] s_single = { Vector2Int.zero };
        private static readonly Vector2Int[] s_domino = { Vector2Int.zero, Vector2Int.right };
        private static readonly Vector2Int[] s_vertical = { Vector2Int.zero, Vector2Int.up };
        // An F pentomino: no piece of the test libraries or the base library has it.
        private static readonly Vector2Int[] s_fPentomino =
        {
            new Vector2Int(1, 0), new Vector2Int(1, 1), new Vector2Int(0, 1), new Vector2Int(1, 2), new Vector2Int(2, 2)
        };

        private LevelEditorWindowFixture m_fixture;
        private LevelDatabaseWindow m_window;
        private BlockShapeLibrary m_library;
        private readonly List<LevelPieceRequest> m_levelRequests = new List<LevelPieceRequest>();
        private readonly List<ShapeKey> m_libraryRequests = new List<ShapeKey>();
        private bool m_pieceEditorOpen;

        [SetUp]
        public void ClearRequests()
        {
            m_levelRequests.Clear();
            m_libraryRequests.Clear();
            m_pieceEditorOpen = false;
        }

        [TearDown]
        public void CloseWindow()
        {
            // A reload test replaces the fixture's window with its own; that one is discarded and closed here, before
            // the fixture restores the published-level snapshot.
            if (m_window != null && !ReferenceEquals(m_window, m_fixture?.Window))
            {
                m_window.DiscardChanges();
                m_window.Close();
            }

            m_fixture?.Dispose();
            m_fixture = null;
            m_window = null;

            if (m_library != null)
            {
                Object.DestroyImmediate(m_library);
                m_library = null;
            }
        }

        [UnityTest]
        public IEnumerator PlusTileCreatesANamedLevelPieceAndShowsItInThePieceEditor()
        {
            VisualElement root = OpenWindow();
            RequireLevel(root);
            LoadFixture(root, CreateFixture(6, 6, new LevelBlockData("b1", "Red", Vector2Int.zero, s_single)));
            string before = JsonUtility.ToJson(GetDraft());
            VisualElement plus = root.Q<LevelShapePalette>().PlusTile;
            Assume.That(plus, Is.Not.Null, "The project piece library is readable.");

            yield return ScrollTo(root, plus);
            Click(root, plus.worldBound.center);

            // User decision of 2026-09-25: "+" makes a level piece named after the level and draws it freehand.
            LevelNamedPiece piece = GetPieceSession().DrawingPiece;
            Assert.That(piece, Is.Not.Null);
            Assert.That(piece.DisplayName, Is.EqualTo("Plus fixture Piece 1"));
            Assert.That(piece.Scope, Is.EqualTo(Scope()));
            Assert.That(GetToolState().IsFreehandArmed, Is.True);
            Assert.That(root.Q<LevelCanvasElement>().IsFreehand, Is.True);
            Assert.That(m_levelRequests, Has.Count.EqualTo(1), "The Piece Editor shows the new piece.");
            Assert.That(m_levelRequests[0].LevelId, Is.EqualTo(Scope()));
            Assert.That(m_levelRequests[0].PieceId, Is.EqualTo(piece.Token), "Its session token (DEC-193).");
            Assert.That(JsonUtility.ToJson(GetDraft()), Is.EqualTo(before), "Creating a piece changes no level.");

            Click(root, plus.worldBound.center);
            Assert.That(GetPieceSession().DrawingPiece.DisplayName, Is.EqualTo("Plus fixture Piece 2"),
                "Each click, a new piece.");
        }

        [Test]
        public void TheDefaultNameSkipsLibraryTableAndSessionNames()
        {
            VisualElement root = OpenWindow();
            RequireLevel(root);
            UseLibrary(Shape("Single", s_single), Shape("Plus fixture Piece 1", s_domino));
            LoadFixture(root, LevelRecordCopy.WithPieceNames(
                CreateFixture(6, 6, new LevelBlockData("b1", "Red", Vector2Int.zero, s_single)),
                new[] { new LevelPieceName(LevelTestShapes.Key(s_fPentomino), "Plus fixture Piece 2") }));

            Invoke("CreateNamedPiece");
            Assert.That(GetPieceSession().DrawingPiece.DisplayName, Is.EqualTo("Plus fixture Piece 3"),
                "1 is a library piece and 2 a name in this level's pieces table.");
            Invoke("CreateNamedPiece");
            Assert.That(GetPieceSession().DrawingPiece.DisplayName, Is.EqualTo("Plus fixture Piece 4"),
                "3 is a named piece of this session.");
        }

        [Test]
        public void DrawnNamedPiecesShowTheirNamesAndRaiseNoShapeWarning()
        {
            VisualElement root = OpenWindow();
            RequireLevel(root);
            LoadFixture(root, CreateFixture(6, 6, new LevelBlockData("b1", "Red", new Vector2Int(5, 5), s_single)));
            Invoke("CreateNamedPiece");
            Draw(s_fPentomino);

            LevelShapePalette palette = root.Q<LevelShapePalette>();
            VisualElement tile = FindCustomTile(palette, LevelTestShapes.Key(s_fPentomino));
            Assert.That(tile, Is.Not.Null, "The drawn piece has a Level pieces tile.");
            Assert.That(tile.Q<Label>().text, Is.EqualTo("Plus fixture Piece 1"), "It shows the piece's name.");
            Assert.That(FindIssue(root, GetDraft().GetBlockAt(1).Id), Is.Null, "No issue compares it with the library.");
        }

        [Test]
        public void ANewPieceNeverTakesANameOfAnotherLevelOfTheDatabase()
        {
            VisualElement root = OpenWindow();
            RequireLevel(root);
            LevelData fixture = CreateFixture(6, 6, new LevelBlockData("b1", "Red", new Vector2Int(5, 5), s_single));
            // A saved level names its own level piece "Plus fixture Piece 1".
            LevelData other = LevelRecordCopy.WithPieceNames(new LevelData("plus-other", "Plus other", 6, 6, 60,
                    new[] { new LevelDoorData("d1", BoardSide.Top, 0, 1, "Red") },
                    new[] { new LevelBlockData("b1", "Red", Vector2Int.zero, s_fPentomino) }),
                new[] { new LevelPieceName(LevelTestShapes.Key(s_fPentomino), "Plus fixture Piece 1") });
            Assert.That(LevelDatabaseJson.TryWrite(new[] { fixture, other }, out string json, out string error), Is.True, error);

            LevelDatabaseSnapshot previousSource = LevelEditorTestAccess.GetLoadedSource(m_window);
            object previousIndex = LevelEditorTestAccess.Get(m_window, "m_selectedIndex");
            try
            {
                LevelEditorTestAccess.LoadLevels(m_window, json);
                LevelEditorTestAccess.Set(m_window, "m_selectedIndex", 0);
                LoadFixture(root, fixture);

                Invoke("CreateNamedPiece");

                Assert.That(GetPieceSession().DrawingPiece.DisplayName, Is.EqualTo("Plus fixture Piece 2"),
                    "Plus fixture Piece 1 is a piece name of another level.");
            }
            finally
            {
                // The window's catalog goes back to the loaded source's levels (DEC-196).
                if (previousSource != null)
                {
                    previousSource.LoadInto(LevelEditorTestAccess.GetLevels(m_window), out _);
                }

                LevelEditorTestAccess.Set(m_window, "m_selectedIndex", previousIndex);
            }
        }

        [UnityTest]
        public IEnumerator APieceEditorChangeJoinsTheOpenUndoGroupAndAnUnchangedLevelRecordsNothing()
        {
            yield return ShowWindowBuiltByUnity();
            VisualElement root = m_window.rootVisualElement;
            RequireLevel(root);
            LoadFixture(root, CreateFixture(8, 8, new LevelBlockData("b1", "Red", new Vector2Int(7, 7), s_single)));
            Invoke("CreateNamedPiece");
            Draw(s_fPentomino);
            LevelPieceRequest mirrored = m_levelRequests[m_levelRequests.Count - 1];
            Assert.That(mirrored.Name, Is.EqualTo("Plus fixture Piece 1"), "Mirrors carry the display name.");
            LevelData drawn = GetDraft();
            Vector2Int[] grown = AddNeighbour(s_fPentomino);
            // The Piece Editor's stroke group is open while LevelPieceChanged runs.
            Undo.IncrementCurrentGroup();
            int group = Undo.GetCurrentGroup();

            Change(new LevelPieceRequest(Scope(), mirrored.PieceId, grown, mirrored.Name));

            Assert.That(Undo.GetCurrentGroup(), Is.EqualTo(group), "The level edit neither opens nor closes a group.");
            Assert.That(LevelCustomPiece.FindTransform(grown, Cells(GetDraft().GetBlockAt(1))), Is.EqualTo(0));
            string changed = JsonUtility.ToJson(GetDraft());

            // The same cells again, as after the Piece Editor's own undo already reverted the level: nothing to do.
            Change(new LevelPieceRequest(Scope(), mirrored.PieceId, grown, mirrored.Name));
            Assert.That(JsonUtility.ToJson(GetDraft()), Is.EqualTo(changed));

            Undo.PerformUndo();
            yield return null;

            Assert.That(JsonUtility.ToJson(GetDraft()), Is.EqualTo(JsonUtility.ToJson(drawn)), "One undo reverts it.");
        }

        [Test]
        public void ANameEditRenamesThePieceAndKeepsItsBlocks()
        {
            VisualElement root = OpenWindow();
            RequireLevel(root);
            LoadFixture(root, CreateFixture(6, 6, new LevelBlockData("b1", "Red", new Vector2Int(5, 5), s_single)));
            Invoke("CreateNamedPiece");
            Draw(s_fPentomino);
            LevelPieceRequest mirrored = m_levelRequests[m_levelRequests.Count - 1];
            string blockBefore = JsonUtility.ToJson(GetDraft().GetBlockAt(1));

            Change(new LevelPieceRequest(Scope(), mirrored.PieceId, mirrored.Cells, "Zig"));

            LevelNamedPiece piece = GetPieceSession().FindNamedPiece(mirrored.PieceId);
            Assert.That(piece, Is.Not.Null, "The piece stays tracked.");
            Assert.That(piece.DisplayName, Is.EqualTo("Zig"));
            Assert.That(JsonUtility.ToJson(GetDraft().GetBlockAt(1)), Is.EqualTo(blockBefore),
                "A name edit changes no block.");
            Assert.That(FindCustomTile(root.Q<LevelShapePalette>(), LevelTestShapes.Key(s_fPentomino)).Q<Label>().text,
                Is.EqualTo("Zig"));
            DropdownField pieceType = (DropdownField)LevelEditorTestAccess.Get(m_window, "m_pieceTypeField");
            Assert.That(pieceType.value, Is.EqualTo("Zig (level piece)"), "Piece Type shows the name.");
            Assert.That(root.Q<Label>("status").text, Does.Contain("Renamed the piece Zig."));
        }

        [Test]
        public void AFootprintBeyondThePieceLimitsIsRefusedWithAMessageAndChangesNothing()
        {
            VisualElement root = OpenWindow();
            RequireLevel(root);
            LoadFixture(root, CreateFixture(6, 6, new LevelBlockData("b1", "Red", Vector2Int.zero, s_single)));
            LevelEditorToolState state = GetToolState();
            Invoke("CreateNamedPiece");
            string token = GetPieceSession().DrawingPiece.Token;
            string before = JsonUtility.ToJson(GetDraft());
            Vector2Int[][] refused =
            {
                new[] { Vector2Int.zero, new Vector2Int(2, 0) },
                new[] { Vector2Int.zero, Vector2Int.right, new Vector2Int(2, 0), new Vector2Int(3, 0),
                    new Vector2Int(4, 0), new Vector2Int(5, 0) }
            };

            for (int index = 0; index < refused.Length; index++)
            {
                LevelCustomPiece.TryValidate(refused[index], out string issue);
                Change(new LevelPieceRequest(Scope(), token, refused[index]));

                Assert.That(root.Q<Label>("status").text, Does.Contain(issue));
                Assert.That(root.Q("shape-banner").style.display.value, Is.EqualTo(DisplayStyle.Flex));
                Assert.That(root.Q<Label>("shape-banner-text").text, Does.Contain(issue));
                Assert.That(state.HasArmedShape, Is.False);
                Assert.That(JsonUtility.ToJson(GetDraft()), Is.EqualTo(before));
            }
        }

        [Test]
        public void ApplyingANamedPiecesNewCellsReStampsOnlyThatPiecesBlocks()
        {
            VisualElement root = OpenWindow();
            RequireLevel(root);
            LoadFixture(root, CreateFixture(8, 8, new LevelBlockData("b1", "Red", new Vector2Int(7, 7), s_single)));
            Vector2Int[] b = AddNeighbour(s_fPentomino);
            Vector2Int[] c = AddNeighbour(b);

            // Two "+" pieces, each drawn as an F: one + click, one new level piece. They share a key, but each
            // piece's changes move only its own recorded block (DEC-193).
            Invoke("CreateNamedPiece");
            Draw(Offset(s_fPentomino, Vector2Int.zero));
            LevelPieceRequest first = m_levelRequests[m_levelRequests.Count - 1];
            Invoke("CreateNamedPiece");
            Draw(Offset(s_fPentomino, new Vector2Int(4, 4)));
            LevelPieceRequest second = m_levelRequests[m_levelRequests.Count - 1];
            Vector2Int[] firstCells = Cells(GetDraft().GetBlockAt(1));

            Change(new LevelPieceRequest(Scope(), second.PieceId, c));

            LevelData after = GetDraft();
            Assert.That(Cells(after.GetBlockAt(1)), Is.EqualTo(firstCells), "The first piece's block is untouched.");
            Assert.That(LevelCustomPiece.FindTransform(c, Cells(after.GetBlockAt(2))), Is.EqualTo(0), "It takes C.");
            Assert.That(root.Q<Label>("status").text,
                Does.Contain("Updated Plus fixture Piece 2. Re-stamped 1 block(s): " + after.GetBlockAt(2).Id));
            Assert.That(GetToolState().IsFreehandArmed, Is.True, "Editing a piece keeps the Pen drawing.");

            Change(new LevelPieceRequest(Scope(), first.PieceId, b));
            Assert.That(LevelCustomPiece.FindTransform(b, Cells(GetDraft().GetBlockAt(1))), Is.EqualTo(0));
        }

        [Test]
        public void ANamedPiecesChangeLeavesTheBlocksOfAMatchingLibraryPieceAlone()
        {
            VisualElement root = OpenWindow();
            RequireLevel(root);
            Vector2Int[] triple = { Vector2Int.zero, Vector2Int.right, new Vector2Int(2, 0) };
            UseLibrary(Shape("Single", s_single), Shape("TripleHorizontal", triple));
            LoadFixture(root, CreateFixture(8, 8,
                new LevelBlockData("b1", "Red", new Vector2Int(0, 6), triple)));
            Invoke("CreateNamedPiece");

            Draw(Offset(triple, Vector2Int.zero));
            LevelPieceRequest mirrored = m_levelRequests[m_levelRequests.Count - 1];
            Vector2Int[] lShape = { Vector2Int.zero, Vector2Int.right, new Vector2Int(2, 0), new Vector2Int(2, 1) };

            Change(new LevelPieceRequest(Scope(), mirrored.PieceId, lShape));

            LevelData after = GetDraft();
            Assert.That(Cells(after.GetBlockAt(0)), Is.EquivalentTo(triple), "The library triple is untouched.");
            Assert.That(Cells(after.GetBlockAt(1)), Is.EquivalentTo(lShape));
        }

        [Test]
        public void AnApplyAfterSwitchingToAnotherDatabaseWithTheSameLevelIdChangesNothingUntilSwitchingBack()
        {
            VisualElement root = OpenWindow();
            RequireLevel(root);
            LevelData fixture = CreateFixture(8, 8, new LevelBlockData("b1", "Red", new Vector2Int(7, 7), s_single));
            LoadFixture(root, fixture);
            Invoke("CreateNamedPiece");
            Draw(s_fPentomino);
            LevelPieceRequest mirrored = m_levelRequests[m_levelRequests.Count - 1];
            string drawnId = GetDraft().GetBlockAt(1).Id;
            Vector2Int[] grown = AddNeighbour(s_fPentomino);

            string guid = (string)LevelEditorTestAccess.Get(m_window, "m_loadedGuid");

            // Another database whose open level has the same id: its own draft, without the drawn block's edit.
            LevelEditorTestAccess.Set(m_window, "m_loadedGuid", "another-database-guid");
            LoadFixture(root, GetDraft());
            string other = JsonUtility.ToJson(GetDraft());
            Change(new LevelPieceRequest(mirrored.LevelId, mirrored.PieceId, grown));

            Assert.That(JsonUtility.ToJson(GetDraft()), Is.EqualTo(other), "Nothing changes in the other database.");
            Assert.That(root.Q<Label>("status").text,
                Does.Contain("This piece belongs to").And.Contain(k_levelId).And.Contain("open that level to edit it"));
            Assert.That(FindCustomTile(root.Q<LevelShapePalette>(), "Plus fixture Piece 1"), Is.Null,
                "The other database's level lists none of this level's pieces.");

            // Back in the first database, the same Apply re-stamps the piece's block.
            LevelEditorTestAccess.Set(m_window, "m_loadedGuid", guid);
            LoadFixture(root, GetDraft());
            Change(new LevelPieceRequest(mirrored.LevelId, mirrored.PieceId, grown));

            Assert.That(GetDraft().GetBlockAt(1).Id, Is.EqualTo(drawnId));
            Assert.That(LevelCustomPiece.FindTransform(grown, Cells(GetDraft().GetBlockAt(1))), Is.EqualTo(0));
        }

        [UnityTest]
        public IEnumerator LibraryFootprintChangeOffersReStampThatKeepsOrientationAsOneUndoStep()
        {
            yield return ShowWindowBuiltByUnity();
            VisualElement root = m_window.rootVisualElement;
            RequireLevel(root);
            UseLibrary(Shape("Single", s_single), Shape("DoubleHorizontal", s_domino));
            LoadFixture(root, CreateFixture(6, 6,
                new LevelBlockData("b1", "Red", new Vector2Int(0, 0), s_vertical),
                new LevelBlockData("b2", "Blue", new Vector2Int(2, 2), s_domino),
                new LevelBlockData("b3", "Pink", new Vector2Int(5, 5), s_single)));
            LevelData before = GetDraft();
            Assert.That(FindIssue(root, "b1"), Is.Null, "A turned library piece matches.");

            Vector2Int[] triple = { Vector2Int.zero, Vector2Int.right, new Vector2Int(2, 0) };
            ChangeLibrary(Shape("Single", s_single), Shape("DoubleHorizontal", triple));

            Assert.That(JsonUtility.ToJson(GetDraft()), Is.EqualTo(JsonUtility.ToJson(before)), "Nothing re-stamps itself.");
            Assert.That(root.Q<Label>("shape-banner-text").text, Does.Contain("2 block(s) in this level"));
            Button restamp = root.Q<Button>("shape-banner-restamp");
            Assert.That(restamp.style.display.value, Is.EqualTo(DisplayStyle.Flex));

            Press(restamp);

            LevelData after = GetDraft();
            Assert.That(Cells(after.GetBlockAt(0)), Is.EquivalentTo(new[] { Vector2Int.zero, Vector2Int.up, new Vector2Int(0, 2) }),
                "b1 stays vertical.");
            Assert.That(Cells(after.GetBlockAt(1)), Is.EquivalentTo(triple), "b2 stays horizontal.");
            Assert.That(after.GetBlockAt(0).Key, Is.EqualTo(LevelTestShapes.Key(triple)));
            Assert.That(FindIssue(root, "b1"), Is.Null);
            // The old domino stays remembered exactly while another level of the database still has blocks from it.
            List<LevelData> levels = (List<LevelData>)LevelEditorTestAccess.Invoke(m_window, "GetDatabaseLevels", null);
            bool othersUseIt = false;
            for (int index = 1; index < levels.Count; index++)
            {
                othersUseIt |= LevelBlockQueries.HasBlock(levels[index], LevelTestShapes.Key(s_domino));
            }

            Assert.That(GetPieceSession().FindFootprintChange(LevelTestShapes.Key(s_domino)),
                othersUseIt ? (IResolveConstraint)Is.Not.Null : Is.Null);

            Undo.PerformUndo();
            yield return null;

            Assert.That(JsonUtility.ToJson(GetDraft()), Is.EqualTo(JsonUtility.ToJson(before)), "One undo restores both.");
        }

        [Test]
        public void ReStampThatWouldLeaveTheBoardIsRefusedWhole()
        {
            VisualElement root = OpenWindow();
            RequireLevel(root);
            UseLibrary(Shape("DoubleHorizontal", s_domino));
            LoadFixture(root, CreateFixture(3, 4,
                new LevelBlockData("b1", "Red", new Vector2Int(1, 0), s_domino)));
            ChangeLibrary(Shape("DoubleHorizontal", new[] { Vector2Int.zero, Vector2Int.right, new Vector2Int(2, 0) }));
            string before = JsonUtility.ToJson(GetDraft());

            Press(root.Q<Button>("shape-banner-restamp"));

            Assert.That(JsonUtility.ToJson(GetDraft()), Is.EqualTo(before));
            Assert.That(root.Q<Label>("shape-banner-text").text, Does.Contain("Re-stamp changed nothing").And.Contain("b1"));
        }

        [UnityTest]
        public IEnumerator LibraryAndLevelPieceTilesShowThePieceInAnOpenPieceEditorWithoutLibraryWrites()
        {
            VisualElement root = OpenWindow();
            RequireLevel(root);
            ShapeKey fKey = LevelTestShapes.Key(s_fPentomino);
            // A line of four: no base library piece has it.
            Vector2Int[] line = { Vector2Int.zero, Vector2Int.right, new Vector2Int(2, 0), new Vector2Int(3, 0) };
            LoadFixture(root, CreateFixture(6, 6,
                new LevelBlockData("b1", "Red", Vector2Int.zero, s_fPentomino),
                new LevelBlockData("b2", "Blue", new Vector2Int(2, 5), line)));
            string libraryBefore = File.ReadAllText(BoardKitAssets.ShapesJsonPath);
            LevelShapePalette palette = root.Q<LevelShapePalette>();
            VisualElement library = palette.TileCount > 0 ? palette.GetTile(0) : null;

            Assume.That(library, Is.Not.Null, "The project piece library has a template.");
            VisualElement custom = FindCustomTile(palette, fKey);
            Assert.That(custom, Is.Not.Null, "The database's level pieces are listed.");
            Assert.That(FindCustomTile(palette, LevelTestShapes.Key(line)), Is.Not.Null);
            Assert.That(custom.parent.Q<Button>(), Is.Not.Null, "Level piece tiles have a × (user request of 2026-09-26).");

            yield return ScrollTo(root, library);
            Click(root, library.worldBound.center);
            Assert.That(m_libraryRequests, Is.Empty, "A closed Piece Editor is not opened.");

            m_pieceEditorOpen = true;
            Click(root, library.worldBound.center);
            yield return ScrollTo(root, custom);
            Click(root, custom.worldBound.center);

            Assert.That(m_libraryRequests, Has.Count.EqualTo(1));
            Assert.That(m_libraryRequests[0].ToString(), Is.EqualTo(library.name.Substring("shape-".Length)));
            Assert.That(m_levelRequests, Has.Count.EqualTo(1));
            Assert.That(m_levelRequests[0].LevelId, Is.EqualTo(Scope()), "The database and level scope.");
            Assert.That(m_levelRequests[0].PieceId, Does.StartWith("piece:" + Scope() + ":"),
                "A level piece tile sends its tracked piece's session token.");
            Assert.That(LevelCustomPiece.FindTransform(s_fPentomino, m_levelRequests[0].Cells), Is.GreaterThanOrEqualTo(0));
            Assert.That(GetToolState().ArmedKey, Is.EqualTo(fKey), "The level piece tile arms the piece.");
            Assert.That(File.ReadAllText(BoardKitAssets.ShapesJsonPath), Is.EqualTo(libraryBefore), "No library write.");
        }

        [UnityTest]
        public IEnumerator LevelPieceTileDeleteRemovesThePieceAndItsBlocksAsOneUndoStep()
        {
            // R-B (user request of 2026-09-26): a level piece tile's × deletes the piece and its blocks from the open
            // level.
            yield return ShowWindowBuiltByUnity();
            VisualElement root = m_window.rootVisualElement;
            RequireLevel(root);
            ShapeKey fKey = LevelTestShapes.Key(s_fPentomino);
            // A vertical line of four: no base library piece has it.
            Vector2Int[] line = { Vector2Int.zero, Vector2Int.up, new Vector2Int(0, 2), new Vector2Int(0, 3) };
            ShapeKey otherKey = LevelTestShapes.Key(line);
            LoadFixture(root, CreateFixture(6, 6,
                new LevelBlockData("b1", "Red", Vector2Int.zero, s_fPentomino),
                new LevelBlockData("b2", "Blue", new Vector2Int(5, 0), line),
                new LevelBlockData("b3", "Red", new Vector2Int(3, 3), s_fPentomino)));
            string fixture = JsonUtility.ToJson(GetDraft());
            LevelShapePalette palette = root.Q<LevelShapePalette>();
            List<string> asked = new List<string>();
            bool answer = false;
            m_window.ConfirmPieceDelete = message =>
            {
                asked.Add(message);
                return answer;
            };

            Press(FindCustomTile(palette, fKey).parent.Q<Button>());
            Assert.That(asked, Has.Count.EqualTo(1));
            Assert.That(asked[0], Does.Contain("and its 2 block(s)"), "The question counts the level's blocks.");
            Assert.That(JsonUtility.ToJson(GetDraft()), Is.EqualTo(fixture), "Cancel changes nothing.");

            answer = true;
            Press(FindCustomTile(palette, fKey).parent.Q<Button>());
            LevelData deleted = GetDraft();
            Assert.That(deleted.BlockCount, Is.EqualTo(1), "Both blocks of the piece are removed.");
            Assert.That(deleted.GetBlockAt(0).Id, Is.EqualTo("b2"), "Other pieces keep their blocks.");
            Assert.That(FindCustomTile(palette, fKey), Is.Null, "The piece leaves the Level pieces list.");
            Assert.That(FindCustomTile(palette, otherKey), Is.Not.Null);

            Undo.PerformUndo();
            yield return null;
            Assert.That(JsonUtility.ToJson(GetDraft()), Is.EqualTo(fixture), "One undo step restores both blocks.");
            Assert.That(FindCustomTile(palette, fKey), Is.Not.Null, "And the tile.");
        }

        [Test]
        public void LevelPieceTileDeleteForgetsAnUnplacedPiece()
        {
            VisualElement root = OpenWindow();
            RequireLevel(root);
            LoadFixture(root, CreateFixture(6, 6));
            ShapeKey fKey = LevelTestShapes.Key(s_fPentomino);
            LevelShapePalette palette = root.Q<LevelShapePalette>();
            // A "+" piece drawn only in the Piece Editor.
            Invoke("CreateNamedPiece");
            Change(new LevelPieceRequest(Scope(), GetPieceSession().DrawingPiece.Token, s_fPentomino));
            Assume.That(FindCustomTile(palette, fKey), Is.Not.Null, "The unplaced piece is listed.");
            string before = JsonUtility.ToJson(GetDraft());
            m_window.ConfirmPieceDelete = _ => true;

            Press(FindCustomTile(palette, fKey).parent.Q<Button>());

            Assert.That(FindCustomTile(palette, fKey), Is.Null, "A piece no block uses is forgotten.");
            Assert.That(GetPieceSession().FindNamedPieceForKey(Scope(), fKey), Is.Null);
            Assert.That(GetToolState().HasArmedShape, Is.False, "The deleted piece is no longer armed.");
            Assert.That(JsonUtility.ToJson(GetDraft()), Is.EqualTo(before), "The level has nothing to remove.");
        }

        [Test]
        public void ALibraryReadBeforeTheDatabaseLoadsReadsNoOtherLevels()
        {
            // The window can hold m_sourceValid while its levels are not loaded yet, and CreateGUI reads the library
            // before it loads the database. An unloaded catalog throws on any read (a failed read leaves it unloaded).
            OpenWindow();
            try
            {
                Invoke("DestroyDraft");
                Assume.That(LevelEditorTestAccess.GetLevels(m_window).LoadJson("{ not loaded", out _), Is.False);
                LevelEditorTestAccess.Set(m_window, "m_sourceValid", true);

                Assert.DoesNotThrow(() => Invoke("RefreshShapeCatalog", true));
                Assert.That(m_window.rootVisualElement.Q("shape-banner").style.display.value, Is.EqualTo(DisplayStyle.None));
            }
            finally
            {
                LevelEditorTestAccess.Set(m_window, "m_sourceValid", false);
            }
        }

        [Test]
        public void TheWindowSubscribesToBothLinkEventsUntilItCloses()
        {
            OpenWindow();

            Assert.That(IsSubscribed(typeof(PieceEditorWindow), "LevelPieceChanged"), Is.True);
            Assert.That(IsSubscribed(typeof(ShapeLibraryAuthoring), "LibraryCommitted"), Is.True);

            LevelDatabaseWindow window = m_window;
            window.DiscardChanges();
            window.Close();
            m_window = null;

            Assert.That(IsSubscribed(typeof(PieceEditorWindow), "LevelPieceChanged", window), Is.False);
            Assert.That(IsSubscribed(typeof(ShapeLibraryAuthoring), "LibraryCommitted", window), Is.False);
        }

        // The open level's database-and-level scope, the level id of every request this window sends.
        private string Scope()
        {
            return (string)LevelEditorTestAccess.Invoke(m_window, "GetOpenLevelScope", null);
        }

        // Draws a freehand stroke through the window's release handler, as the canvas raises it.
        private void Draw(Vector2Int[] cells)
        {
            Invoke("PlacePieceStroke", (IReadOnlyList<Vector2Int>)cells);
        }

        private static Vector2Int[] Offset(Vector2Int[] cells, Vector2Int offset)
        {
            Vector2Int[] result = new Vector2Int[cells.Length];
            for (int index = 0; index < cells.Length; index++)
            {
                result[index] = cells[index] + offset;
            }

            return result;
        }

        private bool IsSubscribed(Type owner, string eventName, object target = null)
        {
            Delegate handlers = (Delegate)owner.GetField(eventName, BindingFlags.Static | BindingFlags.NonPublic).GetValue(null);
            foreach (Delegate handler in handlers?.GetInvocationList() ?? Array.Empty<Delegate>())
            {
                if (ReferenceEquals(handler.Target, target ?? m_window))
                {
                    return true;
                }
            }

            return false;
        }

        private static LevelData CreateFixture(int width, int height, params LevelBlockData[] blocks)
        {
            return new LevelData(k_levelId, "Plus fixture", width, height, 60,
                new[] { new LevelDoorData("d1", BoardSide.Top, 0, 1, "Red") }, blocks);
        }

        private static BlockShapeLibrary.Shape Shape(string displayName, Vector2Int[] cells)
        {
            return new BlockShapeLibrary.Shape(displayName, cells);
        }

        // A transient library stands in for the project asset, so no real piece is written.
        private void UseLibrary(params BlockShapeLibrary.Shape[] shapes)
        {
            m_library = ScriptableObject.CreateInstance<BlockShapeLibrary>();
            m_library.Load(BlockShapeLibraryJson.Write(shapes));
            LevelEditorTestAccess.Set(m_window, "m_shapeLibrary", m_library);
            Invoke("OnFocus");
        }

        // A library commit as the Piece Editor makes it, delivered to this window only.
        private void ChangeLibrary(params BlockShapeLibrary.Shape[] shapes)
        {
            m_library.Load(BlockShapeLibraryJson.Write(shapes));
            Invoke("OnLibraryCommitted");
        }

        private LevelPieceSession GetPieceSession()
        {
            return (LevelPieceSession)LevelEditorTestAccess.Get(m_window, "m_pieceSession");
        }

        // A Piece Editor change as PieceEditorWindow.SendLevelPiece raises it: the listener runs, then the pending undo
        // records are flushed into the open group.
        private void Change(LevelPieceRequest request)
        {
            Invoke("OnLevelPieceChanged", request);
            Undo.FlushUndoRecordObjects();
        }

        private void Invoke(string method, params object[] arguments)
        {
            LevelEditorTestAccess.Invoke(m_window, method, arguments);
        }

        // The Level pieces tile with this name, or null.
        private static VisualElement FindCustomTile(LevelShapePalette palette, string displayName)
        {
            for (int index = 0; index < palette.CustomTileCount; index++)
            {
                if (palette.GetCustomChoice(index).DisplayName == displayName)
                {
                    return palette.GetCustomTile(index);
                }
            }

            return null;
        }

        // The Level pieces tile of this key, or null.
        private static VisualElement FindCustomTile(LevelShapePalette palette, ShapeKey key)
        {
            for (int index = 0; index < palette.CustomTileCount; index++)
            {
                if (palette.GetCustomChoice(index).Key == key)
                {
                    return palette.GetCustomTile(index);
                }
            }

            return null;
        }

        private static LevelIssue FindIssue(VisualElement root, string blockId)
        {
            LevelEditorIssuesList list = root.Q<LevelEditorIssuesList>();
            for (int index = 0; index < list.IssueCount; index++)
            {
                LevelIssue issue = list.GetIssue(index);
                if (issue.EntityId == blockId && issue.Message.Contains("library piece"))
                {
                    return issue;
                }
            }

            return null;
        }

        // The piece plus one free edge neighbour that keeps it inside 5 x 5.
        private static Vector2Int[] AddNeighbour(Vector2Int[] cells)
        {
            Vector2Int[] steps = { Vector2Int.right, Vector2Int.up, Vector2Int.left, Vector2Int.down };
            foreach (Vector2Int cell in cells)
            {
                foreach (Vector2Int step in steps)
                {
                    Vector2Int next = cell + step;
                    if (Array.IndexOf(cells, next) >= 0)
                    {
                        continue;
                    }

                    Vector2Int[] grown = new Vector2Int[cells.Length + 1];
                    Array.Copy(cells, grown, cells.Length);
                    grown[cells.Length] = next;
                    if (LevelCustomPiece.TryValidate(grown, out _) && HasEightOrientations(grown))
                    {
                        return grown;
                    }
                }
            }

            throw new InvalidOperationException("No neighbour keeps the piece asymmetric.");
        }

        // True when no rotation or mirror maps the piece onto itself, so each block orientation has one transform.
        private static bool HasEightOrientations(Vector2Int[] cells)
        {
            for (int transform = 1; transform < LevelCustomPiece.k_transformCount; transform++)
            {
                if (LevelCustomPiece.FindTransform(cells, LevelCustomPiece.Transform(cells, transform)) != transform)
                {
                    return false;
                }
            }

            return true;
        }

        private static Vector2Int[] Cells(LevelBlockData block)
        {
            return LevelRecordCopy.GetCells(block);
        }

        private void RequireLevel(VisualElement root)
        {
            // Every caller immediately replaces this with its own fixture through LoadFixture or UseLibrary, so a
            // deterministic fixture level here only needs to guarantee a level is loaded (Tests-Editor-1-F3),
            // whatever the real project's active database holds.
            m_fixture.LoadDatabase();
            // A fresh undo group after the fixture load, so the test's own first edit starts a clean group.
            Undo.IncrementCurrentGroup();
            Assert.That(IsLevelLoaded(root), Is.True, "The fixture database has a loaded level.");
        }

        private void LoadFixture(VisualElement root, LevelData level)
        {
            LevelDraft draft = (LevelDraft)LevelEditorTestAccess.Get(m_window, "m_draft");
            draft.Replace(level);
            Invoke("OnDraftChanged");
            ValidateLayout(root);
        }

        private LevelData GetDraft()
        {
            return ((LevelDraft)LevelEditorTestAccess.Get(m_window, "m_draft")).Snapshot();
        }

        private LevelEditorToolState GetToolState()
        {
            return (LevelEditorToolState)LevelEditorTestAccess.Get(m_window, "m_toolState");
        }

        private LevelDatabaseWindow CreateWindow()
        {
            LevelDatabaseWindow window = ScriptableObject.CreateInstance<LevelDatabaseWindow>();
            // The Piece Editor link is recorded here; no Piece Editor window opens or asks to discard its draft.
            window.IsPieceEditorOpen = () => m_pieceEditorOpen;
            window.ShowLevelPieceInEditor = m_levelRequests.Add;
            window.ShowLibraryPieceInEditor = m_libraryRequests.Add;
            return window;
        }

        private VisualElement OpenWindow()
        {
            m_fixture = new LevelEditorWindowFixture();
            VisualElement root = m_fixture.Open(CreateWindow());
            m_window = m_fixture.Window;
            return root;
        }

        // Unity builds this window's GUI itself here: a manual CreateGUI could be followed by Unity's own
        // call, which rebuilds the draft and clears its undo records between an edit and its undo.
        private IEnumerator ShowWindowBuiltByUnity()
        {
            m_fixture = new LevelEditorWindowFixture();
            yield return m_fixture.ShowBuiltByUnity(CreateWindow());
            m_window = m_fixture.Window;
        }

        private static IEnumerator ScrollTo(VisualElement root, VisualElement element)
        {
            root.Q<ScrollView>("tools-pane").ScrollTo(element);
            yield return null;
            ValidateLayout(root);
        }

        // Submit presses a button the way a click does, wherever the view is scrolled.
        private static void Press(Button button)
        {
            UiTestInput.Press(button);
        }

        private static void Click(VisualElement root, Vector2 position)
        {
            UiTestInput.Click(root, position);
        }

        private static void ValidateLayout(VisualElement root)
        {
            UiTestInput.ValidateLayout(root);
        }

        private static bool IsLevelLoaded(VisualElement root)
        {
            return LevelEditorWindowFixture.IsLevelLoaded(root);
        }
    }
}
