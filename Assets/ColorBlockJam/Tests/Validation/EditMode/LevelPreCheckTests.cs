using ColorBlockJam.Editor.Levels.Validation;
using NUnit.Framework;

namespace ColorBlockJam.Tests.Validation
{
    /// <summary>AC-3: the static pre-checks prove a level unsolvable with a named reason and never search.</summary>
    public sealed class LevelPreCheckTests
    {
        [Test]
        public void ABlockWithNoGateOfItsColourCannotBeSolvedWithoutASearch()
        {
            LevelCheckResult result = LevelSolvabilityCheck.Run(ValidationFixtures.BlockWithoutGateOfItsColour());

            Assert.That(result.Status, Is.EqualTo(LevelCheckStatus.Unsolvable));
            Assert.That(result.Text, Is.EqualTo(
                "Cannot be solved: the Red B1 block at (1,1) has no Red gate; it needs one at least 1 cell wide."));
            Assert.That(result.VisitedPositions, Is.Zero);
        }

        [Test]
        public void ABlockWithNoGateOfItsColourNamesTheNarrowestGateItCouldUse()
        {
            LevelCheckResult result = LevelSolvabilityCheck.Run(ValidationFixtures.SquareWithoutGateOfItsColour());

            Assert.That(result.Status, Is.EqualTo(LevelCheckStatus.Unsolvable));
            Assert.That(result.Text, Is.EqualTo(
                "Cannot be solved: the Red B1 block at (1,1) has no Red gate; it needs one at least 2 cells wide."));
            Assert.That(result.VisitedPositions, Is.Zero);
        }

        [Test]
        public void ABlockWiderThanEveryGateOfItsColourNamesTheGateWidthItNeeds()
        {
            LevelCheckResult result = LevelSolvabilityCheck.Run(ValidationFixtures.BlockWiderThanItsGate());

            Assert.That(result.Status, Is.EqualTo(LevelCheckStatus.Unsolvable));
            Assert.That(result.Text, Is.EqualTo(
                "Cannot be solved: the Red B1 block at (1,1) needs a Red gate at least 2 cells wide."));
            Assert.That(result.VisitedPositions, Is.Zero);
        }

        [Test]
        public void AHorizontalArrowBlockWhoseOnlyGateNeedsAVerticalExitIsCaughtWithoutASearch()
        {
            // The block can slide horizontally onto the gate's column, but a Horizontal axis never permits the
            // Vertical exit the Top gate needs, so PRE-1 catches it (Editor-Validation-F2) instead of falling
            // through to a search that names it unsolvable for the generic "can never leave" reason.
            LevelCheckResult result = LevelSolvabilityCheck.Run(ValidationFixtures.ArrowSingleNeedingBothAxes("Horizontal"));

            Assert.That(result.Status, Is.EqualTo(LevelCheckStatus.Unsolvable));
            Assert.That(result.Text, Is.EqualTo("Cannot be solved: the Red B1 block at (1,2) is a Horizontal "
                + "Arrow block; its axis never lines it up to leave through a Red gate."));
            Assert.That(result.VisitedPositions, Is.Zero);
        }

        [Test]
        public void AnIceCountAboveTheOtherBlocksNamesBothNumbersWithoutASearch()
        {
            LevelCheckResult result = LevelSolvabilityCheck.Run(ValidationFixtures.IceCountAboveOtherBlocks());

            Assert.That(result.Status, Is.EqualTo(LevelCheckStatus.Unsolvable));
            Assert.That(result.Text, Is.EqualTo("Cannot be solved: the Red B1 block at (0,1) needs 2 other blocks "
                + "to leave before it thaws, but the level has only 1 other block."));
            Assert.That(result.VisitedPositions, Is.Zero);
        }

        [Test]
        public void TheBlockDescriberOptionNamesTheBlockInPreCheckReasons()
        {
            LevelCheckOptions options = new LevelCheckOptions { BlockDescriber = (level, block) => "the red piece" };

            LevelCheckResult result = LevelSolvabilityCheck.Run(ValidationFixtures.BlockWithoutGateOfItsColour(), options, null);

            Assert.That(result.Text, Is.EqualTo(
                "Cannot be solved: the red piece has no Red gate; it needs one at least 1 cell wide."));
        }

        [Test]
        public void DesignerTextSplitsColourNamesAndFormatsCells()
        {
            // Shape names no longer come from a stored id (DEC-193); the TypeName and Custom_ cases were removed.
            Assert.That(LevelDesignerText.GetColourName("LightBlue"), Is.EqualTo("Light Blue"));
            Assert.That(LevelDesignerText.FormatCell(3, 2), Is.EqualTo("(3,2)"));
        }
    }
}
