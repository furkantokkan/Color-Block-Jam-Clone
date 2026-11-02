namespace ColorBlockJam.Domain.Board
{
    /// <summary>Direction arithmetic for board sides: outward steps and the axis a side runs along.</summary>
    public static class SideMath
    {
        /// <summary>The one-cell step that leaves the board through the side.</summary>
        public static Cell GetOutward(Side side)
        {
            switch (side)
            {
                case Side.Top: return new Cell(0, 1);
                case Side.Bottom: return new Cell(0, -1);
                case Side.Left: return new Cell(-1, 0);
                default: return new Cell(1, 0);
            }
        }

        /// <summary>Finds the side a unit axis step points at; any other vector has no side.</summary>
        public static bool TryGetSide(Cell direction, out Side side)
        {
            if (direction.X == 0 && direction.Y == 1)
            {
                side = Side.Top;
                return true;
            }

            if (direction.X == 0 && direction.Y == -1)
            {
                side = Side.Bottom;
                return true;
            }

            if (direction.X == -1 && direction.Y == 0)
            {
                side = Side.Left;
                return true;
            }

            if (direction.X == 1 && direction.Y == 0)
            {
                side = Side.Right;
                return true;
            }

            side = Side.Top;
            return false;
        }

        /// <summary>Top and Bottom run along X, so their lanes are columns; Left and Right lanes are rows.</summary>
        public static bool RunsAlongX(Side side) => side == Side.Top || side == Side.Bottom;

        /// <summary>
        /// The axis a block travels on to leave through the side: Top/Bottom need Vertical, Left/Right need
        /// Horizontal. DragResolver, MoveBlockHandler and ArrowExitGate all derive this, so they share it here.
        /// </summary>
        public static AxisMask GetExitAxis(Side side) => RunsAlongX(side) ? AxisMask.Vertical : AxisMask.Horizontal;
    }
}
