using System.Collections.Generic;
using ColorBlockJam.Editor.Levels;
using ColorBlockJam.Runtime.Levels;
using NUnit.Framework;
using UnityEngine;

namespace ColorBlockJam.Tests.EditMode
{
    /// <summary>Checks commit order and failure boundaries without a window, asset or database file.</summary>
    public sealed class LevelPieceDeleteOperationTests
    {
        private static readonly Vector2Int[] s_bar = { Vector2Int.zero, Vector2Int.right };
        private static readonly Vector2Int[] s_single = { Vector2Int.zero };

        [TestCase(false)]
        [TestCase(true)]
        public void TheLibraryChangesOnlyAfterTheLevelsAndDraft(bool libraryFails)
        {
            List<string> calls = new List<string>();
            LevelPieceDeletePlan plan = CreatePlan();

            LevelPieceDeleteResult result = LevelPieceDeleteOperation.Delete(plan,
                _ => { calls.Add("choose"); return LevelPieceDeleteChoice.DeleteBlocks; },
                levels =>
                {
                    calls.Add("write");
                    Assert.That(levels[0].BlockCount, Is.EqualTo(1));
                    Assert.That(levels[0].GetBlockAt(0).Id, Is.EqualTo("keep"));
                    return new LevelFileWriteResult(true, true, null);
                },
                choice => { calls.Add("draft"); Assert.That(choice, Is.EqualTo(LevelPieceDeleteChoice.DeleteBlocks)); },
                () => { calls.Add("library"); return libraryFails ? "Library is read-only." : null; });

            Assert.That(calls, Is.EqualTo(new[] { "choose", "write", "draft", "library" }));
            Assert.That(result.Status, Is.EqualTo(libraryFails
                ? LevelPieceDeleteStatus.LibraryFailed : LevelPieceDeleteStatus.Deleted));
            Assert.That(result.Choice, Is.EqualTo(LevelPieceDeleteChoice.DeleteBlocks));
            Assert.That(result.Error, Is.EqualTo(libraryFails ? "Library is read-only." : null));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void AWriteOrReloadFailureStopsBeforeChangingTheDraftAndLibrary(bool written)
        {
            List<string> calls = new List<string>();
            LevelPieceDeleteResult result = LevelPieceDeleteOperation.Delete(CreatePlan(),
                _ => LevelPieceDeleteChoice.DeleteTemplate,
                _ => { calls.Add("write"); return new LevelFileWriteResult(written, false, "Injected failure."); },
                _ => calls.Add("draft"),
                () => { calls.Add("library"); return null; });

            Assert.That(calls, Is.EqualTo(new[] { "write" }));
            Assert.That(result.Status, Is.EqualTo(written
                ? LevelPieceDeleteStatus.NotReloaded : LevelPieceDeleteStatus.WriteFailed));
            Assert.That(result.Error, Is.EqualTo("Injected failure."));
        }

        [TestCase(0)]
        [TestCase(99)]
        public void CancellationOrAnUnknownAnswerChangesNothing(int answer)
        {
            List<string> calls = new List<string>();
            LevelPieceDeleteResult result = LevelPieceDeleteOperation.Delete(CreatePlan(),
                _ => { calls.Add("choose"); return (LevelPieceDeleteChoice)answer; },
                _ => { calls.Add("write"); return new LevelFileWriteResult(true, true, null); },
                _ => calls.Add("draft"),
                () => { calls.Add("library"); return null; });

            Assert.That(calls, Is.EqualTo(new[] { "choose" }));
            Assert.That(result.Status, Is.EqualTo(LevelPieceDeleteStatus.Cancelled));
        }

        [Test]
        public void ADraftOnlyChangeDoesNotWriteTheDatabase()
        {
            LevelData saved = Level(new LevelBlockData("keep", "Red", new Vector2Int(4, 4), s_single));
            LevelData draft = Level(new LevelBlockData("draft", "Red", Vector2Int.zero, s_bar));
            LevelPieceDeletePlan plan = CreatePlan(saved, draft);
            Assert.That(plan.ChangesSavedLevels, Is.False);
            List<string> calls = new List<string>();

            LevelPieceDeleteResult result = LevelPieceDeleteOperation.Delete(plan,
                _ => LevelPieceDeleteChoice.DeleteTemplate,
                _ => { calls.Add("write"); return new LevelFileWriteResult(true, true, null); },
                _ => calls.Add("draft"),
                () => { calls.Add("library"); return null; });

            Assert.That(calls, Is.EqualTo(new[] { "draft", "library" }));
            Assert.That(result.Status, Is.EqualTo(LevelPieceDeleteStatus.Deleted));
        }

        [Test]
        public void ARefusedPlanDoesNotAskOrMutate()
        {
            LevelData invalid = new LevelData("invalid", "Invalid", 0, 6, 60, new LevelDoorData[0],
                new[] { new LevelBlockData("bar", "Red", Vector2Int.zero, s_bar) });
            LevelPieceDeletePlan plan = CreatePlan(invalid);
            Assert.That(plan.CanDeleteTemplate || plan.CanDeleteBlocks, Is.False);
            List<string> calls = new List<string>();

            LevelPieceDeleteResult result = LevelPieceDeleteOperation.Delete(plan,
                _ => { calls.Add("choose"); return LevelPieceDeleteChoice.DeleteTemplate; },
                _ => { calls.Add("write"); return new LevelFileWriteResult(true, true, null); },
                _ => calls.Add("draft"),
                () => { calls.Add("library"); return null; });

            Assert.That(calls, Is.Empty);
            Assert.That(result.Status, Is.EqualTo(LevelPieceDeleteStatus.Refused));
        }

        private static LevelPieceDeletePlan CreatePlan(LevelData saved = null, LevelData draft = null)
        {
            saved ??= Level(new LevelBlockData("bar", "Red", Vector2Int.zero, s_bar),
                new LevelBlockData("keep", "Red", new Vector2Int(4, 4), s_single));
            return LevelPieceDeletePlan.Create(new LevelPieceDeletePlan.Input
            {
                TemplateKey = LevelTestShapes.Key(s_bar),
                DisplayName = "Bar",
                DatabaseName = "Test database",
                SavedLevels = new[] { saved },
                OpenIndex = draft != null ? 0 : -1,
                Draft = draft,
                DraftHasChanges = draft != null
            });
        }

        private static LevelData Level(params LevelBlockData[] blocks)
        {
            return new LevelData("test", "Test", 6, 6, 60,
                new[] { new LevelDoorData("door", BoardSide.Top, 0, 1, "Red") }, blocks);
        }
    }
}
