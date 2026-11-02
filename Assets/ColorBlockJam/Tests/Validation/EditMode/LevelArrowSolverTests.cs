using System.Collections.Generic;
using ColorBlockJam.Domain.Board;
using ColorBlockJam.Domain.Commands;
using ColorBlockJam.Domain.Session;
using ColorBlockJam.Editor.Levels.Validation;
using ColorBlockJam.Runtime.Levels;
using NUnit.Framework;

namespace ColorBlockJam.Tests.Validation
{
    /// <summary>
    /// Arrow (BLK-01) in the solvability check: an Arrow block slides only along its axis and leaves only through a
    /// side that axis points to, as ArrowAxisConstraint and ArrowExitGate rule the game. Blocks never split.
    /// </summary>
    public sealed class LevelArrowSolverTests
    {
        [TestCase(false, BoardSide.Left, "Horizontal")]
        [TestCase(false, BoardSide.Right, "Horizontal")]
        [TestCase(true, BoardSide.Top, "Vertical")]
        [TestCase(true, BoardSide.Bottom, "Vertical")]
        public void ATripleLeavesThroughAGateOnItsAxis(bool vertical, BoardSide side, string axis)
        {
            LevelData level = ValidationFixtures.TripleWithOneGate(vertical, side, axis);

            LevelCheckResult result = LevelSolvabilityCheck.Run(level);

            Assert.That(result.Status, Is.EqualTo(LevelCheckStatus.Solvable), result.Text);
            LevelSolutionStep last = result.Steps[result.Steps.Count - 1];
            Assert.That(last.IsExit, Is.True);
            Assert.That(last.ExitSide, Is.EqualTo(side));
            AssertReplaysThroughTheGameHooks(level, result);
        }

        [TestCase(false, BoardSide.Top, "Horizontal")]
        [TestCase(false, BoardSide.Bottom, "Horizontal")]
        [TestCase(true, BoardSide.Left, "Vertical")]
        [TestCase(true, BoardSide.Right, "Vertical")]
        public void ATripleCannotLeaveThroughAGateAcrossItsAxis(bool vertical, BoardSide side, string axis)
        {
            LevelCheckResult plain = LevelSolvabilityCheck.Run(ValidationFixtures.TripleWithOneGate(vertical, side, null));
            Assert.That(plain.Status, Is.EqualTo(LevelCheckStatus.Solvable), "without Arrow the gate is usable");

            LevelData level = ValidationFixtures.TripleWithOneGate(vertical, side, axis);
            LevelCheckResult result = LevelSolvabilityCheck.Run(level);

            Assert.That(result.Status, Is.EqualTo(LevelCheckStatus.Unsolvable));
            Assert.That(result.Text, Is.EqualTo(AxisMismatch(level, 0, axis, "Red")));
            Assert.That(result.VisitedPositions, Is.EqualTo(0), "PRE-1 proves this without a search");
        }

        // Horizontal only slides toward the door's column, never up to it, so PRE-1 names the axis mismatch;
        // Vertical can reach the door's row but never its column, which is the same PRE-1 check's generic reason.
        [TestCase("Horizontal")]
        [TestCase("Vertical")]
        public void ASingleArrowBlockWhoseGateNeedsBothAxesCannotBeSolved(string axis)
        {
            LevelData level = ValidationFixtures.ArrowSingleNeedingBothAxes(axis);
            LevelCheckResult result = LevelSolvabilityCheck.Run(level);

            Assert.That(result.Status, Is.EqualTo(LevelCheckStatus.Unsolvable));
            string expected = axis == "Horizontal"
                ? AxisMismatch(level, 0, "Horizontal", "Red")
                : CannotReachFittingGate(level, 0, "Red");
            Assert.That(result.Text, Is.EqualTo(expected));
        }

        [Test]
        public void ABlockingArrowBlockClearsAColumnOnlyAlongItsAxis()
        {
            LevelData horizontal = ValidationFixtures.ArrowBlockingColumn("Horizontal");
            LevelCheckResult cleared = LevelSolvabilityCheck.Run(horizontal);

            Assert.That(cleared.Status, Is.EqualTo(LevelCheckStatus.Solvable), cleared.Text);
            Assert.That(cleared.Steps[0].BlockIndex, Is.EqualTo(1), "the Orange Arrow block leaves the column first");
            AssertReplaysThroughTheGameHooks(horizontal, cleared);

            LevelData vertical = ValidationFixtures.ArrowBlockingColumn("Vertical");
            LevelCheckResult stuck = LevelSolvabilityCheck.Run(vertical);

            Assert.That(stuck.Status, Is.EqualTo(LevelCheckStatus.Unsolvable), "leaving the column needs a slide across Vertical");
            // PRE-1 catches this without a search: alone on the board, the Orange Arrow block's own Vertical axis
            // never lines it up with its Orange gate on the Right (DEC-158/BLK-01).
            Assert.That(stuck.Text, Is.EqualTo(AxisMismatch(vertical, 1, "Vertical", "Orange")));
        }

        [Test]
        public void TheTablesKeepTheDomainAnswersAndTheMovesApplyTheAxis()
        {
            LevelData level = ValidationFixtures.TripleWithOneGate(false, BoardSide.Top, "Horizontal");
            Assert.That(LevelSolverModelBuilder.TryBuild(level, out LevelModel model, out string error), Is.True, error);
            AxisMask[] axes = LevelSolvabilityCheck.ReadArrowAxes(level);
            LevelSolverTables tables = LevelSolverTables.Create(model, LevelSolvabilityCheck.ReadIceCounts(level), axes);
            int slot = tables.GetAuthoredSlot(0);

            Assert.That(axes, Is.EqualTo(new[] { AxisMask.Horizontal }));
            Assert.That(tables.GetAxes(0), Is.EqualTo(AxisMask.Horizontal));

            // Alone on the board the Domain lets the I3 stand one cell up, flush with the top gate, and fit that gate
            // there; the Arrow refuses both the slide up and the exit.
            int up = tables.GetSlideTarget(0, slot, 2);
            Assert.That(up, Is.GreaterThanOrEqualTo(0));
            Assert.That(tables.IsStanding(0, up), Is.True);
            Assert.That(LevelSolverMoves.GetSlide(tables, 0, slot, 2, CellMask.Empty), Is.EqualTo(-1));
            Assert.That(tables.ExitFits(0, up, Side.Top), Is.True);
            Assert.That(LevelSolverMoves.CanExit(tables, 0, up, Side.Top, CellMask.Empty), Is.False);
            Assert.That(LevelSolverMoves.GetSlide(tables, 0, slot, 0, CellMask.Empty), Is.GreaterThanOrEqualTo(0));
            Assert.That(LevelSolverMoves.GetSlide(tables, 0, slot, 1, CellMask.Empty), Is.GreaterThanOrEqualTo(0));
        }

        [Test]
        public void ABlockWithoutArrowReadsBothAxes()
        {
            AxisMask[] axes = LevelSolvabilityCheck.ReadArrowAxes(ValidationFixtures.ArrowBlockingColumn("Vertical"));

            Assert.That(axes, Is.EqualTo(new[] { AxisMask.Both, AxisMask.Vertical }));
        }

        [Test]
        public void TablesRefuseAMissingAxisMask()
        {
            LevelData level = ValidationFixtures.TripleWithOneGate(false, BoardSide.Left, "Horizontal");
            Assert.That(LevelSolverModelBuilder.TryBuild(level, out LevelModel model, out string error), Is.True, error);
            int[] ice = LevelSolvabilityCheck.ReadIceCounts(level);

            Assert.That(() => LevelSolverTables.Create(model, ice, new AxisMask[0]), Throws.ArgumentException);
            Assert.That(() => LevelSolverTables.Create(model, ice, new[] { AxisMask.None }), Throws.ArgumentException);
        }

        // PRE-1's axis-mismatch sentence (LevelPreChecks.DescribeNoExit's axis case): a wide-enough gate of the
        // block's colour exists, but the block's own Arrow axis never lines it up with one (DEC-158/BLK-01).
        private static string AxisMismatch(LevelData level, int block, string axisName, string gateColour)
        {
            return "Cannot be solved: " + LevelDesignerText.DescribeBlock(level, block) + " is a " + axisName
                + " Arrow block; its axis never lines it up to leave through a " + gateColour + " gate.";
        }

        // PRE-1's generic sentence (LevelPreChecks.DescribeNoExit's final case): the block's axis allows a
        // wide-enough gate of its colour, but no reachable origin lines it up with one.
        private static string CannotReachFittingGate(LevelData level, int block, string gateColour)
        {
            return "Cannot be solved: " + LevelDesignerText.DescribeBlock(level, block) + " cannot reach a "
                + gateColour + " gate it fits through.";
        }

        // The solution, as DEC-122 moves, is applied by the Domain with the game's Arrow hooks and clears the board.
        private static void AssertReplaysThroughTheGameHooks(LevelData level, LevelCheckResult result)
        {
            BoardSimulation simulation = ValidationLevelFixture.CreateSimulation(level);
            List<GameCommand> commands = ValidationLevelFixture.ToMoveCommands(result.Steps);
            for (int c = 0; c < commands.Count; c++)
            {
                Assert.That(ValidationLevelFixture.Execute(simulation, commands[c]), Is.EqualTo(CommandOutcome.Applied),
                    commands[c].ToString());
            }

            Assert.That(simulation.IsCleared, Is.True);
        }
    }
}
