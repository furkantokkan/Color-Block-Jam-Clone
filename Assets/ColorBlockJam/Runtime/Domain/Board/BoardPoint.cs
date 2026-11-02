namespace ColorBlockJam.Domain.Board
{
    /// <summary>
    /// A fractional position on the board plane in cell units: pointer projections and visual offsets.
    /// Cell (x, y) is centred on point (x, y) and spans x - 0.5 to x + 0.5.
    /// </summary>
    public readonly struct BoardPoint
    {
        public readonly float X;
        public readonly float Y;

        public BoardPoint(float x, float y)
        {
            X = x;
            Y = y;
        }

        public static BoardPoint FromCell(Cell cell) => new BoardPoint(cell.X, cell.Y);

        public static BoardPoint operator +(BoardPoint a, BoardPoint b) => new BoardPoint(a.X + b.X, a.Y + b.Y);

        public static BoardPoint operator -(BoardPoint a, BoardPoint b) => new BoardPoint(a.X - b.X, a.Y - b.Y);

        public static BoardPoint operator +(BoardPoint a, Cell b) => new BoardPoint(a.X + b.X, a.Y + b.Y);

        public static BoardPoint operator -(BoardPoint a, Cell b) => new BoardPoint(a.X - b.X, a.Y - b.Y);

        public override string ToString() => "(" + X + ", " + Y + ")";
    }
}
