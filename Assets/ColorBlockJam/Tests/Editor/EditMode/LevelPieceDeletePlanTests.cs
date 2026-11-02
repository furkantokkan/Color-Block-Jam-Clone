using System;
using System.Collections.Generic;
using ColorBlockJam.Editor.Levels;
using ColorBlockJam.Runtime.Levels;
using NUnit.Framework;
using UnityEngine;

namespace ColorBlockJam.Tests.EditMode
{
    /// <summary>
    /// Verifies the plain C# plan of a template delete that level blocks use (DEC-126,
    /// TASK-20260926-191802848-515eb50b, choices revised by DEC-193): Delete template keeping the blocks and their
    /// effects, Delete template and its blocks removing blocks and deleting emptied levels, the refusals for an empty
    /// database and a broken effect reference, and the question text. The template's blocks are the blocks whose cells
    /// form its key. No window, asset or file is involved.
    /// </summary>
    public sealed class LevelPieceDeletePlanTests
    {
        private const string k_name = "Bar piece";
        private static readonly Vector2Int[] s_single = { Vector2Int.zero };
        private static readonly Vector2Int[] s_bar = { Vector2Int.zero, Vector2Int.right, new Vector2Int(2, 0) };

        [Test]
        public void DeleteTemplateKeepsEffects()
        {
            LevelData used = Level("l1", "One",
                Block("b1", 0, 0, s_bar),
                new LevelBlockData("b2", "Blue", new Vector2Int(5, 5), s_single,
                    new[] { LevelTestData.Effect("ice", ("clearsToThaw", "1")) }),
                new LevelBlockData("b3", "Red", new Vector2Int(0, 3), s_bar,
                    new[] { LevelTestData.Effect("arrow", ("axis", "Horizontal")) }));
            Assert.That(used.Validate(out string error), Is.True, error);

            LevelData result = Create(new[] { used }).GetLevels(LevelPieceDeleteChoice.DeleteTemplate)[0];

            Assert.That(result.GetBlockAt(2).EffectCount, Is.EqualTo(1));
            Assert.That(result.GetBlockAt(2).GetEffectAt(0).Type, Is.EqualTo("arrow"));
            Assert.That(result.GetBlockAt(1).EffectCount, Is.EqualTo(1), "The untouched block keeps its Ice.");
        }

        [Test]
        public void TheQuestionCountsTheOpenDraftAndItsSavedCopy()
        {
            LevelData saved = Level("l1", "One", Block("b1", 0, 0, s_bar));
            LevelData draft = Level("l1", "One", Block("b1", 0, 0, s_bar), Block("b7", 0, 2, s_bar));

            LevelPieceDeletePlan plan = Create(new[] { saved }, openIndex: 0, draft: draft);

            Assert.That(plan.Question.Message, Does.Contain("- One (open level): 2 block(s), 1 in its saved copy"));
        }

        [Test]
        public void DeleteBlocksRemovesTheBlocksAndDeletesEveryLevelLeftEmpty()
        {
            LevelData emptied = Level("l1", "Only bars", Block("b1", 0, 0, s_bar));
            LevelData mixed = Level("l2", "Mixed", Block("b1", 0, 0, s_bar), Block("b2", 5, 5, s_single));
            LevelData untouched = Level("l3", "Other", Block("b1", 0, 0, s_single));
            LevelPieceDeletePlan plan = Create(new[] { emptied, mixed, untouched });

            Assert.That(plan.CanDeleteBlocks, Is.True);
            List<LevelData> levels = plan.GetLevels(LevelPieceDeleteChoice.DeleteBlocks);
            Assert.That(levels, Has.Count.EqualTo(2), "The emptied level is deleted.");
            Assert.That(levels[0].Id, Is.EqualTo("l2"));
            Assert.That(levels[0].BlockCount, Is.EqualTo(1));
            Assert.That(levels[0].GetBlockAt(0).Id, Is.EqualTo("b2"));
            Assert.That(levels[1], Is.SameAs(untouched));
            Assert.That(plan.DeletedLevelNames, Is.EqualTo(new[] { "Only bars" }));
            Assert.That(plan.GetWrittenIndex(0, LevelPieceDeleteChoice.DeleteBlocks), Is.EqualTo(-1));
            Assert.That(plan.GetWrittenIndex(2, LevelPieceDeleteChoice.DeleteBlocks), Is.EqualTo(1));
            Assert.That(plan.GetNearestWrittenIndex(0, LevelPieceDeleteChoice.DeleteBlocks), Is.EqualTo(0));
            Assert.That(plan.Question.Message, Does.Contain(LevelPieceDeletePlan.k_emptiedHeading + "\n- Only bars"));
            Assert.That(plan.GetDoneStatus(LevelPieceDeleteChoice.DeleteBlocks),
                Does.Contain("deleted Only bars because no block remained"));
        }

        [Test]
        public void DeletingTheOpenLevelWithUnsavedChangesSaysTheyWillBeLost()
        {
            LevelData open = Level("l1", "Open", Block("b1", 0, 0, s_bar));
            LevelData other = Level("l2", "Other", Block("b1", 0, 0, s_single));
            LevelData draft = Level("l1", "Open", Block("b1", 0, 0, s_bar), Block("b2", 6, 6, s_single));

            LevelPieceDeletePlan plan = Create(new[] { open, other }, openIndex: 0, draft: draft, draftChanged: true);

            Assert.That(plan.DeletesOpenLevel, Is.True, "The written record keeps no block.");
            Assert.That(plan.Question.Message, Does.Contain("- Open (open level)\nOpen has unsaved changes, which will be lost."));
            Assert.That(plan.Apply(draft, LevelPieceDeleteChoice.DeleteBlocks).BlockCount, Is.EqualTo(1));
        }

        [Test]
        public void DeleteBlocksIsRefusedWhenNoLevelWouldRemain()
        {
            LevelData level = Level("l1", "Only", Block("b1", 0, 0, s_bar));

            LevelPieceDeletePlan plan = Create(new[] { level });

            Assert.That(plan.CanDeleteBlocks, Is.False);
            Assert.That(plan.CanDeleteTemplate, Is.True);
            Assert.That(plan.DeleteRefusals[0], Does.Contain("Level database has no levels."));
            Assert.That(plan.Question.Message, Does.Contain("Delete template and its blocks is not possible:"));
            Assert.That(plan.Question.Message, Does.Not.Contain(LevelPieceDeletePlan.k_emptiedHeading));
        }

        [Test]
        public void DeleteBlocksIsRefusedWhenAnEffectNamesARemovedBlock()
        {
            // A flower crate's leaves name blocks; deleting leaf b1 would leave the crate naming a missing block.
            LevelEffectData flowerCrate = LevelTestData.Effect("flower-crate", ("leaves", "b1"));
            LevelData level = new LevelData("l1", "Flower", 8, 8, 60, Array.Empty<Vector2Int>(),
                new[] { new LevelDoorData("d1", BoardSide.Top, 0, 1, "Red") },
                new[]
                {
                    Block("b1", 0, 0, s_bar),
                    new LevelBlockData("b2", "Blue", new Vector2Int(5, 5), s_single)
                },
                new[] { new LevelCellObjectData("o1", new[] { new Vector2Int(7, 7) }, flowerCrate) });
            Assert.That(level.Validate(out string error), Is.True, error);

            LevelPieceDeletePlan plan = Create(new[] { level, Level("l2", "Two", Block("b1", 0, 0, s_single)) });

            Assert.That(plan.CanDeleteBlocks, Is.False);
            Assert.That(plan.DeleteRefusals[0], Does.StartWith("Flower: ").And.Contain("names missing block b1"));
        }

        [Test]
        public void TheQuestionNamesPieceCountsAndOtherDatabases()
        {
            LevelData open = Level("l1", "First", Block("b1", 0, 0, s_bar), Block("b2", 6, 6, s_single));
            LevelData second = Level("l2", "Second", Block("b1", 0, 0, s_bar), Block("b2", 0, 2, s_bar),
                Block("b3", 6, 6, s_single));
            LevelPieceDeletePlan plan = Create(new[] { open, second }, openIndex: 0, draft: open,
                others: new[]
                {
                    new LevelPieceDeletePlan.OtherDatabase("Spare.json", 4),
                    new LevelPieceDeletePlan.OtherDatabase("Empty.json", 0),
                    new LevelPieceDeletePlan.OtherDatabase("Broken.json", -1)
                });

            string message = plan.Question.Message;
            Assert.That(message, Does.StartWith("Delete Bar piece from the piece library? 3 block(s) in 2 level(s) keep " +
                                                "their cells as level pieces and take the name Bar piece.\n\n" +
                                                "3 block(s) in 2 level(s) of Levels.json use it:\n" +
                                                "- First (open level): 1 block(s)\n" +
                                                "- Second: 2 block(s)\n"));
            Assert.That(message, Does.Contain("Spare.json: 4 block(s) also use it and stay unchanged."));
            Assert.That(message, Does.Not.Contain("Empty.json"));
            Assert.That(message, Does.Contain("Broken.json could not be read"));
            Assert.That(message, Does.Contain("Either choice writes Levels.json now"));
            Assert.That(message, Does.Not.Contain(LevelPieceDeletePlan.k_emptiedHeading), "No level is emptied.");
            Assert.That(plan.Question.CanDeleteTemplate && plan.Question.CanDeleteBlocks, Is.True);
        }

        private static LevelPieceDeletePlan Create(IReadOnlyList<LevelData> levels, int openIndex = -1,
            LevelData draft = null, bool draftChanged = false,
            IReadOnlyList<LevelPieceDeletePlan.OtherDatabase> others = null)
        {
            return LevelPieceDeletePlan.Create(new LevelPieceDeletePlan.Input
            {
                TemplateKey = LevelTestShapes.Key(s_bar),
                DisplayName = k_name,
                DatabaseName = "Levels.json",
                SavedLevels = levels,
                OpenIndex = openIndex,
                Draft = draft,
                DraftHasChanges = draftChanged,
                OtherDatabases = others
            });
        }

        private static LevelBlockData Block(string id, int x, int y, Vector2Int[] cells, string color = "Red")
        {
            return new LevelBlockData(id, color, new Vector2Int(x, y), cells);
        }

        private static LevelData Level(string id, string name, params LevelBlockData[] blocks)
        {
            return new LevelData(id, name, 8, 8, 60,
                new[] { new LevelDoorData("d1", BoardSide.Top, 0, 1, "Red") }, blocks);
        }
    }
}
