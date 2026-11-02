using ColorBlockJam.Domain.Board;
using ColorBlockJam.Domain.Boosters;
using ColorBlockJam.Domain.Commands;
using ColorBlockJam.Domain.Hooks;
using ColorBlockJam.Domain.Session;
using ColorBlockJam.Tests.Gameplay.Fixtures;
using NUnit.Framework;

namespace ColorBlockJam.Tests.Gameplay.Domain
{
    /// <summary>
    /// The booster restriction gate (XP-6, DEC-171): a restricted effect keeps its booster off the block in every
    /// target check and every handler, a refusal changes nothing, presence follows the effect's live state and Vacuum
    /// leaves restricted blocks of its colour on the board.
    /// </summary>
    public sealed class BoosterRestrictionTests
    {
        // Blocks are numbered by letter: A (0) and B (1) are frozen, C (2) is a plain two-cell block and 'b' (3) is a
        // one-cell block of B's colour.
        private static readonly string[] s_board =
        {
            "AAb.",
            "....",
            "BBCC"
        };

        private static readonly Cell s_aOrigin = new Cell(0, 2);
        private static readonly Cell s_bOrigin = new Cell(2, 2);
        private static readonly Cell s_frozenBOrigin = new Cell(0, 0);

        [Test]
        public void RocketSkipsFrozenBlocksUntilTheyThaw()
        {
            BoardSimulation sim = Create(CommandKind.RemoveCell, out IceRules ice);

            Assert.That(BoosterTargets.CanRemoveCell(sim.Board, s_aOrigin, out int block, out CommandOutcome refusal), Is.False);
            Assert.That(block, Is.EqualTo(0), "a restricted block is still named, so targeting can reject it");
            Assert.That(refusal, Is.EqualTo(CommandOutcome.EffectRestricted));
            Assert.That(sim.Preview.AllowsBoosterTarget(CommandKind.RemoveCell, 0), Is.False, "the preview reads the same rule");

            Cell[] candidates = new Cell[sim.Board.Model.TotalCellCount];
            Assert.That(StartingRocketCandidates.Collect(sim.Board, candidates), Is.EqualTo(2),
                "only C's cells get a crosshair or the opening Rocket");
            Assert.That(candidates[0], Is.EqualTo(new Cell(2, 0)));
            Assert.That(candidates[1], Is.EqualTo(new Cell(3, 0)));

            Assert.That(sim.Commands.Execute(GameCommand.RemoveCell(0, s_aOrigin, s_aOrigin)),
                Is.EqualTo(CommandOutcome.EffectRestricted));
            Assert.That(sim.Events.Count, Is.EqualTo(0));
            Assert.That(sim.Board.GetFootprint(0).CellCount, Is.EqualTo(2), "a refusal changes nothing");

            ice.OnBlockExited(new BlockExitedEvent(2, 0, Side.Top, default, ExitOutcome.Leave));
            Assert.That(BoosterTargets.CanRemoveCell(sim.Board, s_aOrigin, out _, out refusal), Is.True,
                "a thawed block no longer carries Ice");
            Assert.That(sim.Commands.Execute(GameCommand.RemoveCell(0, s_aOrigin, s_aOrigin)), Is.EqualTo(CommandOutcome.Applied));
        }

        [Test]
        public void VacuumLeavesRestrictedBlocksOfTheColourOnTheBoard()
        {
            BoardSimulation sim = Create(CommandKind.RemoveColor, out _);

            Assert.That(BoosterTargets.CanRemoveColor(sim.Board, 1), Is.False, "a frozen block cannot be tapped");
            Assert.That(sim.Commands.Execute(GameCommand.RemoveColor(1, s_frozenBOrigin, BoardFixture.ColorOf('B'))),
                Is.EqualTo(CommandOutcome.EffectRestricted));
            Assert.That(sim.Events.Count, Is.EqualTo(0));

            Assert.That(BoosterTargets.CanRemoveColor(sim.Board, 3), Is.True);
            Assert.That(sim.Commands.Execute(GameCommand.RemoveColor(3, s_bOrigin, BoardFixture.ColorOf('B'))),
                Is.EqualTo(CommandOutcome.Applied));
            Assert.That(sim.Board.GetStatus(3), Is.EqualTo(BlockStatus.Removed));
            Assert.That(sim.Board.GetStatus(1), Is.EqualTo(BlockStatus.OnBoard), "the frozen block of that colour stays");
            Assert.That(sim.Events.Count, Is.EqualTo(1));
        }

        [Test]
        public void OnlyTheRestrictedBoosterIsRefused()
        {
            BoardSimulation sim = Create(CommandKind.RemoveCell, out _);

            Assert.That(BoosterTargets.CanRemoveBlock(sim.Board, 0), Is.True, "Hammer keeps breaking Ice (DEC-147)");
            Assert.That(BoosterTargets.CanRemoveColor(sim.Board, 1), Is.True);
            Assert.That(sim.Commands.Execute(GameCommand.RemoveBlock(0, s_aOrigin)), Is.EqualTo(CommandOutcome.Applied));
        }

        [Test]
        public void HammerRestrictionRefusesTheCommand()
        {
            BoardSimulation sim = Create(CommandKind.RemoveBlock, out _);

            Assert.That(BoosterTargets.CanRemoveBlock(sim.Board, 0), Is.False);
            Assert.That(sim.Commands.Execute(GameCommand.RemoveBlock(0, s_aOrigin)), Is.EqualTo(CommandOutcome.EffectRestricted));
            Assert.That(sim.Board.GetStatus(0), Is.EqualTo(BlockStatus.OnBoard));
            Assert.That(sim.Commands.Execute(GameCommand.RemoveBlock(2, new Cell(2, 0))), Is.EqualTo(CommandOutcome.Applied));
        }

        [Test]
        public void ArrowPresenceFollowsTheAuthoredSource()
        {
            BoardState board = new BoardState(BoardFixture.Create(s_board));
            ArrowEffectPresence arrow = new ArrowEffectPresence(new[]
            {
                AxisMask.Horizontal, AxisMask.Vertical, AxisMask.Both, AxisMask.Both
            });

            Assert.That(arrow.HasEffect(board, 0), Is.True);
            Assert.That(arrow.HasEffect(board, 1), Is.True);
            Assert.That(arrow.HasEffect(board, 2), Is.False);
            Assert.That(board.AllowsBoosterTarget(CommandKind.RemoveCell, 0), Is.True, "a board without hooks restricts nothing");
        }

        private static BoardSimulation Create(CommandKind restricted, out IceRules ice)
        {
            int[] counts = { 1, 1, 0, 0 };
            ice = new IceRules(counts, BoardFixture.Create(s_board).TotalCellCount);
            AttemptHooks hooks = new AttemptHooksBuilder()
                .Add((IDragGate)ice)
                .Add((IExitGate)ice)
                .Add((IBoardEventListener)ice)
                .Add(new BoosterRestrictionGate(new[] { new BoosterRestriction(restricted, ice) }))
                .Build();
            return BoardFixture.CreateSimulation(hooks, s_board);
        }
    }
}
