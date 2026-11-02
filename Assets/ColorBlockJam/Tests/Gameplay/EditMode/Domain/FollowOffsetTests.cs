using ColorBlockJam.Domain.Board;
using ColorBlockJam.Domain.Hooks;
using ColorBlockJam.Domain.Movement;
using ColorBlockJam.Domain.Session;
using ColorBlockJam.Tests.Gameplay.Fixtures;
using NUnit.Framework;

namespace ColorBlockJam.Tests.Gameplay.Domain
{
    public sealed class FollowOffsetTests
    {
        private const float k_lead = 0.5f;
        private const float k_tolerance = 1e-5f;

        private static readonly AttemptHooks s_noHooks = AttemptHooks.CreateEmpty();

        [Test]
        public void ThereIsNoLeadTowardABlockedCell()
        {
            BoardState board = new BoardState(BoardFixture.Create(new[] { "....", "AB..", "...." }));

            BoardPoint towardBlock = Compute(board, new Cell(0, 1), 0.4f, 1f, AxisMask.Both);
            BoardPoint towardWall = Compute(board, new Cell(0, 1), -0.4f, 1f, AxisMask.Both);

            Assert.That(towardBlock.X, Is.EqualTo(0f));
            Assert.That(towardWall.X, Is.EqualTo(0f));
        }

        [Test]
        public void TheLeadTowardAFreeCellIsCappedAtHalfACell()
        {
            BoardState board = new BoardState(BoardFixture.Create(new[] { "....", "A...", "...." }));

            Assert.That(Compute(board, new Cell(0, 1), 0.3f, 1f, AxisMask.Both).X, Is.EqualTo(0.3f).Within(k_tolerance));
            Assert.That(Compute(board, new Cell(0, 1), 0.9f, 1f, AxisMask.Both).X, Is.EqualTo(k_lead));
            Assert.That(Compute(board, new Cell(0, 1), 0f, 1.45f, AxisMask.Both).Y, Is.EqualTo(0.45f).Within(k_tolerance));
        }

        [Test]
        public void ThereIsNoLeadIntoAUsableDoor()
        {
            // A held block touching a door it fits has already exited (DEC-164), so the view never leans into one.
            BoardState board = new BoardState(BoardFixture.Create(new[] { ".A.", "...", "..." },
                BoardFixture.Door(Side.Top, 1, 1, 'A')));

            Assert.That(Compute(board, new Cell(1, 2), 1f, 2.8f, AxisMask.Both).Y, Is.EqualTo(0f));
            Assert.That(Compute(board, new Cell(1, 2), 1f, 2.2f, AxisMask.Both).Y, Is.EqualTo(0f));
        }

        [Test]
        public void ThereIsNoLeadTowardAWrongColourDoor()
        {
            BoardState board = new BoardState(BoardFixture.Create(new[] { ".A.", "...", "..." },
                BoardFixture.Door(Side.Top, 1, 1, 'B')));

            Assert.That(Compute(board, new Cell(1, 2), 1f, 2.8f, AxisMask.Both).Y, Is.EqualTo(0f));
        }

        [Test]
        public void ADiagonalLeadIntoABlockedCornerKeepsOnlyTheLargerAxis()
        {
            BoardState board = new BoardState(BoardFixture.Create(new[] { "..B", ".A.", "..." }));

            BoardPoint mostlyX = Compute(board, new Cell(1, 1), 1.4f, 1.2f, AxisMask.Both);
            BoardPoint mostlyY = Compute(board, new Cell(1, 1), 1.2f, 1.4f, AxisMask.Both);
            BoardPoint freeCorner = Compute(board, new Cell(1, 1), 0.6f, 1.2f, AxisMask.Both);

            Assert.That(mostlyX.X, Is.EqualTo(0.4f).Within(k_tolerance));
            Assert.That(mostlyX.Y, Is.EqualTo(0f));
            Assert.That(mostlyY.X, Is.EqualTo(0f));
            Assert.That(mostlyY.Y, Is.EqualTo(0.4f).Within(k_tolerance));
            Assert.That(freeCorner.X, Is.EqualTo(-0.4f).Within(k_tolerance));
            Assert.That(freeCorner.Y, Is.EqualTo(0.2f).Within(k_tolerance));
        }

        [Test]
        public void ALockedAxisHasNoLead()
        {
            BoardState board = new BoardState(BoardFixture.Create(new[] { "...", ".A.", "..." }));

            BoardPoint lead = Compute(board, new Cell(1, 1), 1.3f, 1.4f, AxisMask.Horizontal);

            Assert.That(lead.X, Is.EqualTo(0.3f).Within(k_tolerance));
            Assert.That(lead.Y, Is.EqualTo(0f));
        }

        [TestCase(0.49f)]
        [TestCase(0.51f)]
        [TestCase(1.2f)]
        public void TheViewFollowsThePointerContinuouslyAcrossAStep(float pointerX)
        {
            BoardSimulation sim = BoardFixture.CreateSimulation(new[] { "....", "A...", "...." });
            Assert.That(sim.Drag.TryBegin(0, new BoardPoint(0f, 1f)), Is.True);
            BoardPoint pointer = new BoardPoint(pointerX, 1f);

            sim.Drag.Update(pointer, out _);
            BoardPoint offset = sim.Drag.GetViewOffset(pointer);

            float viewX = sim.Preview.GetOrigin(0).X + offset.X;
            Assert.That(viewX, Is.EqualTo(pointerX).Within(k_tolerance));
        }

        [Test]
        public void TheViewOffsetIsZeroWhenNothingIsHeld()
        {
            BoardSimulation sim = BoardFixture.CreateSimulation(new[] { "....", "A...", "...." });

            BoardPoint offset = sim.Drag.GetViewOffset(new BoardPoint(0.4f, 1f));

            Assert.That(offset.X, Is.EqualTo(0f));
            Assert.That(offset.Y, Is.EqualTo(0f));
        }

        private static BoardPoint Compute(IBoardReader board, Cell origin, float x, float y, AxisMask axes)
        {
            return FollowOffset.Compute(board, s_noHooks, 0, origin, new BoardPoint(x, y), axes, k_lead);
        }
    }
}
