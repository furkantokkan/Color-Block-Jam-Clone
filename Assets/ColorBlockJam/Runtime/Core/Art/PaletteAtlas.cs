using System;
using UnityEngine;

namespace ColorBlockJam.Runtime.Art
{
    /// <summary>
    /// The grid of the shared colour atlas that MAT_Gameplay samples (PROJECT.md Performance Rule 1, DEC-202):
    /// 128 x 32 pixels in 8-pixel cells, 16 columns by 4 rows, with cell (0, 0) at the bottom left. The bottom two
    /// rows hold the booster swatches the Blender UVs read; GamePalette names every other colour by its cell.
    /// </summary>
    public static class PaletteAtlas
    {
        public const int Width = 128;
        public const int Height = 32;
        public const int CellSize = 8;
        public const int Columns = Width / CellSize;
        public const int Rows = Height / CellSize;

        public static bool Contains(Vector2Int cell) =>
            cell.x >= 0 && cell.x < Columns && cell.y >= 0 && cell.y < Rows;

        public static Vector2Int GetCellCenterPixel(Vector2Int cell)
        {
            if (!Contains(cell)) throw new ArgumentOutOfRangeException(nameof(cell), "The colour atlas has no such cell.");
            return new Vector2Int(cell.x * CellSize + CellSize / 2, cell.y * CellSize + CellSize / 2);
        }

        /// <summary>
        /// The _PaletteUV scale/offset that samples cell's centre: a zero scale, so every vertex of the mesh reads
        /// the same texel whatever its own UVs.
        /// </summary>
        public static Vector4 GetCellUV(Vector2Int cell)
        {
            Vector2Int pixel = GetCellCenterPixel(cell);
            return new Vector4(0f, 0f, (float)pixel.x / Width, (float)pixel.y / Height);
        }
    }

    /// <summary>
    /// One colour of the shared atlas, as the UV scale/offset a board tile writes into _PaletteUV (DEC-202). Blocks,
    /// gates, walls, floor, arrows and exit fragments pass one of these instead of a Color; GamePalette resolves it
    /// from a PaletteColor. It allocates nothing.
    /// </summary>
    public readonly struct AtlasColor : IEquatable<AtlasColor>
    {
        private AtlasColor(Vector2Int cell)
        {
            Cell = cell;
            UV = PaletteAtlas.GetCellUV(cell);
        }

        public Vector2Int Cell { get; }
        public Vector4 UV { get; }

        public static AtlasColor FromCell(Vector2Int cell) => new AtlasColor(cell);

        public bool Equals(AtlasColor other) => Cell == other.Cell;
        public override bool Equals(object obj) => obj is AtlasColor other && Equals(other);
        public override int GetHashCode() => Cell.GetHashCode();
        public static bool operator ==(AtlasColor left, AtlasColor right) => left.Equals(right);
        public static bool operator !=(AtlasColor left, AtlasColor right) => !left.Equals(right);
        public override string ToString() => "atlas cell " + Cell;
    }
}
