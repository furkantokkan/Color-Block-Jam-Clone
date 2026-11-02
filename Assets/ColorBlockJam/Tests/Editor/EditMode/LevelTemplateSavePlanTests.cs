using System;
using System.Collections.Generic;
using ColorBlockJam.Editor.Levels;
using ColorBlockJam.Runtime.Board;
using ColorBlockJam.Runtime.Levels;
using NUnit.Framework;
using UnityEngine;

namespace ColorBlockJam.Tests.EditMode
{
    /// <summary>
    /// Verifies the pure Save Level plan for pending template footprints: other levels re-stamped in their own
    /// orientation from the footprint they carry now, unfit levels named without refusing the save, the whole-save
    /// refusals (missing record, library changed elsewhere, family clash, open-level guard) and the banner counts.
    /// </summary>
    public sealed class LevelTemplateSavePlanTests
    {
        private const string k_lName = "L";
        private static readonly Vector2Int[] s_single = { Vector2Int.zero };
        // The library L: a column of three with a foot to the right at the top; all 8 orientations differ.
        private static readonly Vector2Int[] s_l =
        {
            new Vector2Int(0, 0), new Vector2Int(0, 1), new Vector2Int(0, 2), new Vector2Int(1, 2)
        };
        // The L grown by (1,1): a P pentomino, also without symmetry.
        private static readonly Vector2Int[] s_p =
        {
            new Vector2Int(0, 0), new Vector2Int(0, 1), new Vector2Int(1, 1), new Vector2Int(0, 2), new Vector2Int(1, 2)
        };
        // The P grown by (2,2).
        private static readonly Vector2Int[] s_p2 =
        {
            new Vector2Int(0, 0), new Vector2Int(0, 1), new Vector2Int(1, 1), new Vector2Int(0, 2),
            new Vector2Int(1, 2), new Vector2Int(2, 2)
        };
        private static readonly Vector2Int[] s_t =
        {
            new Vector2Int(0, 0), new Vector2Int(1, 0), new Vector2Int(2, 0), new Vector2Int(1, 1)
        };
        // The key the library held for the L template when every edit here began; declared after the cells, so the
        // initializer reads them.
        private static readonly ShapeKey s_lKey = LevelTestShapes.Key(s_l);

        // LevelCustomPiece.Transform indices: 0-3 turn a footprint clockwise that many times; 4-7 mirror it first,
        // then turn that many times (matches the designer's R and F keys).
        private const int k_turnOnce = 1;
        private const int k_turnTwice = 2;
        private const int k_turnThrice = 3;
        private const int k_mirror = 4;
        private const int k_mirrorTurnOnce = 5;
        private const int k_mirrorTurnTwice = 6;

        [Test]
        public void OtherLevelsAreRestampedInTheirOwnOrientationAndUntouchedOrFinishedLevelsAreSkipped()
        {
            LevelTemplateEdit edit = Edit(s_l, s_p);
            LevelData candidate = CreateLevel("One", Block("b1", Vector2Int.zero, s_p));
            LevelData turned = CreateLevel("Two",
                Block("b1", new Vector2Int(3, 3), LevelCustomPiece.Transform(s_l, k_turnOnce)),
                Block("b2", Vector2Int.zero, LevelCustomPiece.Transform(s_l, k_mirror)));
            LevelData untouched = CreateLevel("Three", Block("b1", Vector2Int.zero, s_single));
            LevelData finished = CreateLevel("Four",
                Block("b1", Vector2Int.zero, LevelCustomPiece.Transform(s_p, k_turnTwice)));

            LevelTemplateSavePlan plan = Plan(candidate,
                new[] { Other(1, turned), Other(2, untouched), Other(3, finished) },
                Library(Piece(k_lName, s_l), Piece("Single", s_single)), edit);

            Assert.That(plan.Refusals, Is.Empty);
            Assert.That(plan.Unfit, Is.Empty);
            Assert.That(plan.ToCommit, Is.EqualTo(new[] { edit }));
            Assert.That(plan.HasWork, Is.True);
            Assert.That(plan.Restamped, Has.Count.EqualTo(1), "Only the level that still carries L is written.");
            LevelTemplateSavePlan.RestampedLevel restamped = plan.Restamped[0];
            Assert.That(restamped.Index, Is.EqualTo(1));
            Assert.That(restamped.Name, Is.EqualTo("Two"));
            Assert.That(restamped.BlockCount, Is.EqualTo(2));
            Assert.That(restamped.Level, Is.Not.SameAs(turned));
            Assert.That(Cells(restamped.Level.GetBlockAt(0)), Is.EquivalentTo(LevelCustomPiece.Transform(s_p, k_turnOnce)));
            Assert.That(Cells(restamped.Level.GetBlockAt(1)), Is.EquivalentTo(LevelCustomPiece.Transform(s_p, k_mirror)));
            Assert.That(restamped.Level.GetBlockAt(0).Origin, Is.EqualTo(new Vector2Int(3, 3)), "The origin stays.");
            Assert.That(Cells(turned.GetBlockAt(0)), Is.EquivalentTo(LevelCustomPiece.Transform(s_l, k_turnOnce)),
                "The given level never changes.");
        }

        [Test]
        public void EachLevelIsRestampedFromTheLibraryTheBaseOrTheRememberedFootprintItsBlocksCarry()
        {
            // The window committed P earlier; the edit now grows P2 from L after an undo past that Save (F2).
            LevelTemplateEdit edit = Edit(s_l, s_p2);
            LevelData candidate = CreateLevel("One", Block("b1", Vector2Int.zero, s_p2));
            Vector2Int corner = Vector2Int.one;
            LevelData fromLibrary = CreateLevel("Lib",
                Block("b1", corner, LevelCustomPiece.Transform(s_p, k_turnTwice)));
            LevelData fromBase = CreateLevel("Base", Block("b1", corner, LevelCustomPiece.Transform(s_l, k_turnThrice)));
            LevelData fromPrevious = CreateLevel("Prev",
                Block("b1", corner, LevelCustomPiece.Transform(s_t, k_mirrorTurnOnce)));
            LevelData mixed = CreateLevel("Mixed", Block("b1", Vector2Int.zero, s_l),
                Block("b2", new Vector2Int(4, 4), s_p2));
            LevelData lost = CreateLevel("Lost", Block("b7", corner, Line(3)));

            LevelTemplateSavePlan plan = LevelTemplateSavePlan.Create(candidate,
                new[]
                {
                    Other(1, fromLibrary), Other(2, fromBase), Other(3, fromPrevious), Other(4, mixed), Other(5, lost)
                },
                new[] { edit }, Library(Piece(k_lName, s_p)), Committed(s_lKey, s_p),
                Previous(LevelTestShapes.Key(s_p), s_t));

            Assert.That(plan.Refusals, Is.Empty, "A library equal to the committed footprint is accepted.");
            Assert.That(plan.ToCommit, Is.EqualTo(new[] { edit }));
            Assert.That(plan.Restamped, Has.Count.EqualTo(4));
            AssertRestamped(plan.Restamped[0], 1, LevelCustomPiece.Transform(s_p2, k_turnTwice));
            AssertRestamped(plan.Restamped[1], 2, LevelCustomPiece.Transform(s_p2, k_turnThrice));
            int previousTransform = LevelCustomPiece.FindTransform(s_t, LevelCustomPiece.Transform(s_t, k_mirrorTurnOnce));
            AssertRestamped(plan.Restamped[2], 3, LevelCustomPiece.Transform(s_p2, previousTransform));
            Assert.That(plan.Restamped[3].Index, Is.EqualTo(4));
            Assert.That(plan.Restamped[3].BlockCount, Is.EqualTo(1), "The block already on P2 is not counted.");
            Assert.That(Cells(plan.Restamped[3].Level.GetBlockAt(1)), Is.EquivalentTo(s_p2), "It keeps its cells.");
            Assert.That(plan.Restamped[3].Level.GetBlockAt(1).Origin, Is.EqualTo(new Vector2Int(4, 4)));

            // A block is its footprint key (DEC-193): the line in Lost is a piece of its own, not a block of L that
            // lost its footprint, so the save leaves that level alone instead of naming it unfit.
            Assert.That(plan.UnfitLevels, Is.Empty);
            Assert.That(plan.Unfit, Is.Empty);
        }

        [Test]
        public void LevelsThatCannotTakeTheFootprintAreUnfitNamedAndLeftUnchangedWithoutRefusingTheSave()
        {
            LevelTemplateEdit edit = Edit(s_l, s_p);
            LevelData candidate = CreateLevel("One", Block("b1", Vector2Int.zero, s_p));
            LevelCellObjectData barrier = LevelEffectTests.Object("o1",
                LevelEffectTests.Effect("barrier"), new Vector2Int(1, 1));
            LevelData blocked = new LevelData("blocked", "Blocked", 8, 8, 60, Array.Empty<Vector2Int>(),
                new LevelDoorData[0], new[] { Block("b2", Vector2Int.zero, s_l) }, new[] { barrier });
            LevelData overlap = CreateLevel("Overlap", Block("b1", Vector2Int.zero, s_l),
                Block("b2", Vector2Int.one, s_single));
            LevelData fine = CreateLevel("Fine", Block("b1", new Vector2Int(5, 5), s_l));

            LevelTemplateSavePlan plan = Plan(candidate, new[] { Other(1, blocked), Other(2, overlap), Other(3, fine) },
                Library(Piece(k_lName, s_l), Piece("Single", s_single)), edit);

            Assert.That(plan.Refusals, Is.Empty, "Unfit levels never refuse the save by themselves.");
            Assert.That(plan.ToCommit, Is.EqualTo(new[] { edit }));
            Assert.That(plan.Unfit, Has.Count.EqualTo(2));
            Assert.That(plan.Unfit[0], Does.StartWith("Level Blocked:").And.Contain("b2")
                .And.Contain("under board object o1"));
            Assert.That(plan.Unfit[1], Does.StartWith("Level Overlap:").And.Contain("b1 and b2 overlap"),
                "An overlap in another level is unfit, because the written database must validate.");
            Assert.That(plan.UnfitLevels[0].Index, Is.EqualTo(1));
            Assert.That(plan.UnfitLevels[1].Index, Is.EqualTo(2));
            Assert.That(plan.Restamped, Has.Count.EqualTo(1));
            Assert.That(plan.Restamped[0].Index, Is.EqualTo(3));

            // DEC-193: each unfit level keeps its blocks on the old footprint, whose key is no template once the save
            // commits; its pieces table names that key after the template, so the blocks keep the template's name.
            Assert.That(plan.Named, Has.Count.EqualTo(2));
            Assert.That(plan.Named[0].Index, Is.EqualTo(1));
            Assert.That(plan.Named[1].Index, Is.EqualTo(2));
            Assert.That(plan.Named[0].BlockCount, Is.EqualTo(1), "Blocked names its one L block.");
            Assert.That(plan.Named[1].BlockCount, Is.EqualTo(1), "Overlap's single is not an L block.");
            Assert.That(LevelPieceNames.Find(plan.Named[0].Level, s_lKey), Is.EqualTo(k_lName));
            Assert.That(LevelPieceNames.Find(plan.Named[1].Level, s_lKey), Is.EqualTo(k_lName));
            Assert.That(LevelPieceNames.Find(plan.Named[1].Level, LevelTestShapes.Key(s_single)), Is.Null,
                "Only the edited template's old footprint is named.");
            Assert.That(Cells(plan.Named[0].Level.GetBlockAt(0)), Is.EquivalentTo(s_l), "The blocks keep their cells.");
            Assert.That(LevelPieceNames.Find(blocked, s_lKey), Is.Null, "The given level never changes.");
            Assert.That(plan.HasWork, Is.True);
        }

        [Test]
        public void ALibraryChangedElsewhereIsRefusedAndAFootprintItAlreadyHoldsCommitsNothing()
        {
            LevelTemplateEdit edit = Edit(s_l, s_p);
            LevelData candidate = CreateLevel("One", Block("b1", Vector2Int.zero, s_p));

            LevelTemplateSavePlan changed = Plan(candidate, Array.Empty<LevelTemplateSavePlan.OtherLevel>(),
                Library(Piece(k_lName, s_t)), edit);
            Assert.That(changed.Refusals, Has.Count.EqualTo(1));
            Assert.That(changed.Refusals[0], Does.Contain("Template L changed in the piece library"));
            Assert.That(changed.ToCommit, Is.Empty);

            LevelTemplateSavePlan saved = Plan(candidate, Array.Empty<LevelTemplateSavePlan.OtherLevel>(),
                Library(Piece(k_lName, LevelCustomPiece.Transform(s_p, k_turnTwice))), edit);
            Assert.That(saved.Refusals, Is.Empty);
            Assert.That(saved.ToCommit, Is.Empty, "The library already holds P, turned.");
            Assert.That(saved.HasWork, Is.False);

            // An undo past a Save brings back an edit whose base the library no longer holds (F2).
            LevelTemplateEdit grown = Edit(s_l, s_p2);
            LevelData grownCandidate = CreateLevel("One", Block("b1", Vector2Int.zero, s_p2));
            LevelTemplateSavePlan unknown = Plan(grownCandidate, Array.Empty<LevelTemplateSavePlan.OtherLevel>(),
                Library(Piece(k_lName, s_p)), grown);
            Assert.That(unknown.Refusals, Has.Count.EqualTo(1), "Without the committed footprint P is foreign.");
            LevelTemplateSavePlan committed = LevelTemplateSavePlan.Create(grownCandidate,
                Array.Empty<LevelTemplateSavePlan.OtherLevel>(), new[] { grown }, Library(Piece(k_lName, s_p)),
                Committed(s_lKey, s_p), null);
            Assert.That(committed.Refusals, Is.Empty);
            Assert.That(committed.ToCommit, Is.EqualTo(new[] { grown }));
        }

        [Test]
        public void AFootprintOfAnotherLibraryPieceAndTwoEditsIntoOneFamilyAreRefused()
        {
            LevelData candidate = CreateLevel("One", Block("b1", Vector2Int.zero, s_single));
            LevelTemplateEdit edit = Edit(s_l, LevelCustomPiece.Transform(s_p, k_turnThrice));
            LevelTemplateSavePlan clash = Plan(candidate, Array.Empty<LevelTemplateSavePlan.OtherLevel>(),
                Library(Piece(k_lName, s_l), Piece("P piece", s_p)), edit);

            Assert.That(clash.Refusals, Has.Count.EqualTo(1));
            Assert.That(clash.Refusals[0], Does.Contain("template L").And.Contain("P piece"));
            Assert.That(clash.ToCommit, Is.Empty);

            // Both grow into the P pentomino: the L by (1,1), the T by (2,1).
            Vector2Int[] grownT = { new Vector2Int(0, 0), new Vector2Int(1, 0), new Vector2Int(2, 0),
                new Vector2Int(1, 1), new Vector2Int(2, 1) };
            LevelTemplateSavePlan pair = Plan(candidate, Array.Empty<LevelTemplateSavePlan.OtherLevel>(),
                Library(Piece(k_lName, s_l), Piece("T", s_t)),
                Edit(s_l, s_p), new LevelTemplateEdit(LevelTestShapes.Key(s_t), "T", s_t, grownT));

            Assert.That(pair.Refusals, Has.Count.EqualTo(1), "The pair is named once.");
            Assert.That(pair.Refusals[0], Does.Contain("Templates L and T"));
            Assert.That(pair.ToCommit, Is.Empty);
        }

        [Test]
        public void AMissingLibraryRecordOrAnUnreadableLibraryIsRefused()
        {
            LevelTemplateEdit edit = Edit(s_l, s_p);
            LevelData candidate = CreateLevel("One", Block("b1", Vector2Int.zero, s_p));
            LevelData other = CreateLevel("Two", Block("b1", Vector2Int.zero, s_l));

            LevelTemplateSavePlan missing = Plan(candidate, new[] { Other(1, other) },
                Library(Piece("Single", s_single)), edit);
            Assert.That(missing.Refusals, Has.Count.EqualTo(1));
            Assert.That(missing.Refusals[0], Does.Contain("Template L is no longer in the piece library"));
            Assert.That(missing.ToCommit, Is.Empty);
            Assert.That(missing.Restamped, Is.Empty, "Without the record nothing is re-stamped.");

            LevelTemplateSavePlan unreadable = Plan(candidate, new[] { Other(1, other) }, LevelShapeCatalog.Missing,
                edit);
            Assert.That(unreadable.Refusals, Has.Count.EqualTo(1));
            Assert.That(unreadable.Refusals[0], Does.Contain(LevelShapeCatalog.Missing.Error));
        }

        [Test]
        public void ABlockOfTheOpenLevelOffTheNewFootprintIsRefusedAndNamed()
        {
            // b3 is one of the template's blocks in this level, yet still on the old footprint.
            LevelTemplateEdit edit = Edit(s_l, s_p, "b1", "b3");
            LevelData candidate = CreateLevel("One", Block("b1", Vector2Int.zero, s_p),
                Block("b3", new Vector2Int(4, 4), s_l));

            LevelTemplateSavePlan plan = Plan(candidate, Array.Empty<LevelTemplateSavePlan.OtherLevel>(),
                Library(Piece(k_lName, s_l)), edit);

            Assert.That(plan.Refusals, Has.Count.EqualTo(1));
            Assert.That(plan.Refusals[0], Does.Contain("Block b3"));
            Assert.That(plan.ToCommit, Is.Empty);
        }

        [Test]
        public void CountsBlocksHereAndInTheOtherLevelsThatHoldThem()
        {
            LevelTemplateEdit edit = Edit(s_l, s_p);
            LevelData candidate = CreateLevel("One", Block("b1", Vector2Int.zero, s_p),
                Block("b2", new Vector2Int(4, 4), s_p),
                Block("b3", new Vector2Int(7, 7), s_single));
            LevelData one = CreateLevel("Two", Block("b1", Vector2Int.zero, s_p));
            LevelData two = CreateLevel("Three", Block("b1", Vector2Int.zero, s_p),
                Block("b2", new Vector2Int(4, 4), s_p));
            LevelData none = CreateLevel("Four", Block("b1", Vector2Int.zero, s_single));

            LevelTemplateSavePlan plan = Plan(candidate, new[] { Other(1, one), Other(2, two), Other(3, none) },
                Library(Piece(k_lName, s_l), Piece("Single", s_single)), edit);

            Assert.That(plan.GetCounts(s_lKey, out int here, out int elsewhere, out int levels), Is.True);
            Assert.That(here, Is.EqualTo(2));
            Assert.That(elsewhere, Is.EqualTo(3));
            Assert.That(levels, Is.EqualTo(2));
            Assert.That(plan.GetCounts(LevelTestShapes.Key(s_single), out here, out elsewhere, out levels), Is.False);
            Assert.That(here + elsewhere + levels, Is.Zero);
        }

        [Test]
        public void ARetryAfterAFailedCommitRestampsNothingTwiceAndASuccessLeavesNoWork()
        {
            LevelTemplateEdit edit = Edit(s_l, s_p);
            LevelData candidate = CreateLevel("One", Block("b1", Vector2Int.zero, s_p));
            LevelData other = CreateLevel("Two", Block("b1", new Vector2Int(2, 2),
                LevelCustomPiece.Transform(s_p, k_mirrorTurnTwice)));

            LevelTemplateSavePlan retry = Plan(candidate, new[] { Other(1, other) }, Library(Piece(k_lName, s_l)),
                edit);
            Assert.That(retry.Refusals, Is.Empty);
            Assert.That(retry.Restamped, Is.Empty, "The level took P in the first Save.");
            Assert.That(retry.ToCommit, Is.EqualTo(new[] { edit }), "The library still lacks P.");
            Assert.That(retry.HasWork, Is.True);

            LevelTemplateSavePlan done = Plan(candidate, new[] { Other(1, other) }, Library(Piece(k_lName, s_p)),
                edit);
            Assert.That(done.Refusals, Is.Empty);
            Assert.That(done.Restamped, Is.Empty);
            Assert.That(done.ToCommit, Is.Empty);
            Assert.That(done.HasWork, Is.False);
        }

        [Test]
        public void APendingEntryAndANewerDraftEditOfOneTemplateActAsOneWithTheNewestFootprint()
        {
            // Save Level wrote P to the levels but the library commit failed; the designer then grew P2.
            LevelTemplateEdit pending = Edit(s_l, s_p);
            LevelTemplateEdit draft = Edit(s_l, s_p2);
            LevelData candidate = CreateLevel("One", Block("b1", Vector2Int.zero, s_p2));
            LevelData other = CreateLevel("Two",
                Block("b1", Vector2Int.one, LevelCustomPiece.Transform(s_p, k_turnOnce)));

            LevelTemplateSavePlan plan = Plan(candidate, new[] { Other(1, other) }, Library(Piece(k_lName, s_l)),
                pending, draft);

            Assert.That(plan.Refusals, Is.Empty);
            Assert.That(plan.ToCommit, Is.EqualTo(new[] { draft }));
            Assert.That(plan.Restamped, Has.Count.EqualTo(1));
            AssertRestamped(plan.Restamped[0], 1, LevelCustomPiece.Transform(s_p2, k_turnOnce));
            Assert.That(plan.GetCounts(s_lKey, out int here, out int elsewhere, out int levels), Is.True);
            Assert.That(new[] { here, elsewhere, levels }, Is.EqualTo(new[] { 1, 1, 1 }));
        }

        [Test]
        public void ASavedFootprintAloneOnlyRetriesTheLibraryCommit()
        {
            // An earlier Save Level wrote P, left Three and Four on L as the designer answered, and its commit failed.
            LevelTemplateEdit saved = Edit(s_l, s_p);
            LevelData candidate = CreateLevel("Three", Block("b1", Vector2Int.zero, s_l));
            LevelData other = CreateLevel("Four", Block("b1", new Vector2Int(6, 5), s_l));
            LevelTemplateSavePlan.OtherLevel[] others = { Other(1, other) };

            LevelTemplateSavePlan retry = LevelTemplateSavePlan.Create(candidate, others,
                Array.Empty<LevelTemplateEdit>(), Library(Piece(k_lName, s_l)), null, null, new[] { saved });

            Assert.That(retry.Refusals, Is.Empty, "The open level may be one the designer left on the old footprint.");
            Assert.That(retry.Unfit, Is.Empty, "Nobody is asked again.");
            Assert.That(retry.Restamped, Is.Empty, "The earlier save already wrote the other levels.");
            Assert.That(retry.ToCommit, Is.EqualTo(new[] { saved }));
            Assert.That(retry.GetCounts(s_lKey, out int here, out int elsewhere, out int levels), Is.True);
            Assert.That(new[] { here, elsewhere, levels }, Is.EqualTo(new[] { 1, 1, 1 }));

            LevelTemplateSavePlan changed = LevelTemplateSavePlan.Create(candidate, others,
                Array.Empty<LevelTemplateEdit>(), Library(Piece(k_lName, s_t)), null, null, new[] { saved });
            Assert.That(changed.Refusals, Has.Count.EqualTo(1));
            Assert.That(changed.Refusals[0], Does.Contain("changed in the piece library")
                .And.Contain("Press Keep library footprint in the template banner"));
            Assert.That(changed.ToCommit, Is.Empty);

            // A newer draft edit of the same template plans the whole save again, open level included; b1 is one of
            // the template's blocks here.
            LevelTemplateEdit draft = Edit(s_p, s_p2, "b1");
            LevelTemplateSavePlan edited = LevelTemplateSavePlan.Create(candidate, others, new[] { draft },
                Library(Piece(k_lName, s_l)), null, null, new[] { saved });
            Assert.That(edited.Refusals, Has.Count.EqualTo(1));
            Assert.That(edited.Refusals[0], Does.StartWith("Block b1 of template L in this level does not match"));
        }

        private static void AssertRestamped(LevelTemplateSavePlan.RestampedLevel restamped, int index,
            Vector2Int[] cells)
        {
            Assert.That(restamped.Index, Is.EqualTo(index));
            Assert.That(restamped.BlockCount, Is.EqualTo(1));
            Assert.That(Cells(restamped.Level.GetBlockAt(0)), Is.EquivalentTo(cells), $"Level {restamped.Name}.");
        }

        private static LevelTemplateSavePlan Plan(LevelData candidate, LevelTemplateSavePlan.OtherLevel[] others,
            LevelShapeCatalog library, params LevelTemplateEdit[] edits)
        {
            return LevelTemplateSavePlan.Create(candidate, others, edits, library, null, null);
        }

        // A pending change of the L template, which the library held as L when the edit began, from baseCells to
        // cells; members are the template's blocks in the open level (DEC-193).
        private static LevelTemplateEdit Edit(Vector2Int[] baseCells, Vector2Int[] cells, params string[] members)
        {
            return new LevelTemplateEdit(s_lKey, k_lName, baseCells, cells, addedIds: members);
        }

        // The footprint this window committed for a template key.
        private static Func<ShapeKey, IReadOnlyList<Vector2Int>> Committed(ShapeKey templateKey, Vector2Int[] cells)
        {
            return key => key == templateKey ? cells : null;
        }

        // The footprint a template had before its last library change, by the template's current library key.
        private static Func<ShapeKey, IReadOnlyList<IReadOnlyList<Vector2Int>>> Previous(ShapeKey libraryKey,
            Vector2Int[] cells)
        {
            return key => key == libraryKey ? new IReadOnlyList<Vector2Int>[] { cells } : null;
        }

        private static LevelTemplateSavePlan.OtherLevel Other(int index, LevelData level)
        {
            return new LevelTemplateSavePlan.OtherLevel(index, level.Name, level);
        }

        private static LevelShapeCatalog Library(params LevelShapeChoice[] choices)
        {
            return LevelShapeCatalog.Create(choices);
        }

        private static LevelShapeChoice Piece(string displayName, Vector2Int[] cells)
        {
            return LevelTestShapes.Template(displayName, cells);
        }

        private static LevelData CreateLevel(string name, params LevelBlockData[] blocks)
        {
            return new LevelData(name.ToLowerInvariant(), name, 8, 8, 60, new LevelDoorData[0], blocks);
        }

        private static LevelBlockData Block(string id, Vector2Int origin, Vector2Int[] cells)
        {
            return new LevelBlockData(id, "Red", origin, cells);
        }

        private static Vector2Int[] Cells(LevelBlockData block)
        {
            return LevelRecordCopy.GetCells(block);
        }

        private static Vector2Int[] Line(int length)
        {
            Vector2Int[] cells = new Vector2Int[length];
            for (int index = 0; index < length; index++)
            {
                cells[index] = new Vector2Int(index, 0);
            }

            return cells;
        }
    }
}
