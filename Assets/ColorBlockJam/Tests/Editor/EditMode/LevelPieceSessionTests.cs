using ColorBlockJam.Editor.Levels;
using NUnit.Framework;
using UnityEngine;

namespace ColorBlockJam.Tests.EditMode
{
    /// <summary>
    /// Verifies what the Level Editor's piece session forgets: a deleted level database's template footprints and
    /// level pieces (and nothing of another database), and the whole session a window layout file restored.
    /// </summary>
    public sealed class LevelPieceSessionTests
    {
        private const string k_deleted = "deleted-database";
        private const string k_kept = "kept-database";

        [Test]
        public void ForgettingADeletedDatabaseDropsOnlyItsFootprintsAndPieces()
        {
            LevelPieceSession session = new LevelPieceSession();
            session.SetPendingTemplate(k_deleted, TemplateEdit(new[] { Vector2Int.zero }));
            session.SetPendingTemplate(k_kept, TemplateEdit(new[] { Vector2Int.zero, Vector2Int.right }));
            LevelNamedPiece deletedPiece = session.AddNamedPiece(LevelPieceSession.GetLevelScope(k_deleted, "level-01"),
                "Level 1 Piece 1");
            session.SetDrawingPiece(deletedPiece);
            session.AddNamedPiece(LevelPieceSession.GetLevelScope(k_kept, "level-01"), "Level 1 Piece 1");

            session.ForgetDatabase(k_deleted);

            Assert.That(session.CountPendingTemplates(), Is.EqualTo(1));
            Assert.That(session.FindOtherPendingDatabase(k_deleted), Is.EqualTo(k_kept),
                "The other database still waits for its own Save Level.");
            Assert.That(session.NamedPieces.Count, Is.EqualTo(1));
            Assert.That(session.NamedPieces[0].Scope, Does.StartWith(k_kept + "/"));
            Assert.That(session.DrawingPiece, Is.Null, "A forgotten piece is no longer drawn.");
        }

        [Test]
        public void ClearStartsANewSession()
        {
            LevelPieceSession session = new LevelPieceSession();
            session.SetPendingTemplate(k_kept, TemplateEdit(new[] { Vector2Int.zero }));
            LevelNamedPiece before = session.AddNamedPiece(LevelPieceSession.GetLevelScope(k_kept, "level-05"),
                "Level 5 Piece 6");

            session.Clear();

            Assert.That(session.CountPendingTemplates(), Is.Zero);
            Assert.That(session.NamedPieces, Is.Empty);
            Assert.That(session.HasNamedDisplayName("Level 5 Piece 6"), Is.False, "The name is free again.");
            LevelNamedPiece after = session.AddNamedPiece(LevelPieceSession.GetLevelScope(k_kept, "level-05"),
                "Level 5 Piece 1");
            Assert.That(after.Token, Is.Not.EqualTo(before.Token), "A new session tag names its pieces.");
        }

        private static LevelTemplateEdit TemplateEdit(Vector2Int[] cells)
        {
            return new LevelTemplateEdit(LevelTestShapes.Key(cells), "Session fixture piece", cells,
                new[] { Vector2Int.zero, Vector2Int.up });
        }
    }
}
