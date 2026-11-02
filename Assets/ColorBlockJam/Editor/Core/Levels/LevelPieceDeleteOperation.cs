using System;
using System.Collections.Generic;
using ColorBlockJam.Runtime.Levels;

namespace ColorBlockJam.Editor.Levels
{
    /// <summary>
    /// Applies a template-delete plan in the same order with or without a Level Editor window:
    /// confirm, write the affected levels, update an open draft, then delete the library record.
    /// Presentation and Unity asset operations belong to the caller.
    /// </summary>
    internal static class LevelPieceDeleteOperation
    {
        internal static LevelPieceDeleteResult Delete(LevelPieceDeletePlan plan,
            Func<LevelPieceDeleteQuestion, LevelPieceDeleteChoice> choose,
            Func<IReadOnlyList<LevelData>, LevelFileWriteResult> write,
            Action<LevelPieceDeleteChoice> applyDraft, Func<string> deleteLibrary)
        {
            if (!plan.CanDeleteTemplate && !plan.CanDeleteBlocks)
            {
                return new LevelPieceDeleteResult(LevelPieceDeleteStatus.Refused, LevelPieceDeleteChoice.Cancel);
            }

            LevelPieceDeleteChoice choice = plan.Question.Accept(choose(plan.Question));
            if (choice == LevelPieceDeleteChoice.Cancel)
            {
                return new LevelPieceDeleteResult(LevelPieceDeleteStatus.Cancelled, choice);
            }

            if (plan.ChangesSavedLevels)
            {
                LevelFileWriteResult file = write(plan.GetLevels(choice));
                if (!file.Reloaded)
                {
                    return new LevelPieceDeleteResult(file.JsonWritten
                        ? LevelPieceDeleteStatus.NotReloaded : LevelPieceDeleteStatus.WriteFailed, choice, file.Error);
                }
            }

            applyDraft?.Invoke(choice);
            string issue = deleteLibrary();
            return new LevelPieceDeleteResult(issue == null
                ? LevelPieceDeleteStatus.Deleted : LevelPieceDeleteStatus.LibraryFailed, choice, issue);
        }
    }

    internal enum LevelPieceDeleteStatus
    {
        Refused,
        Cancelled,
        WriteFailed,
        NotReloaded,
        LibraryFailed,
        Deleted
    }

    internal readonly struct LevelPieceDeleteResult
    {
        internal LevelPieceDeleteResult(LevelPieceDeleteStatus status, LevelPieceDeleteChoice choice, string error = null)
        {
            Status = status;
            Choice = choice;
            Error = error;
        }

        internal LevelPieceDeleteStatus Status { get; }
        internal LevelPieceDeleteChoice Choice { get; }
        internal string Error { get; }
    }
}
