using ColorBlockJam.Domain.Board;
using ColorBlockJam.Runtime.Art;
using ColorBlockJam.Runtime.Layout;
using ColorBlockJam.Runtime.Levels;
using UnityEngine;

namespace ColorBlockJam.Editor.Levels.Validation
{
    /// <summary>
    /// Builds the Domain LevelModel the check asks its questions of, with the same semantics as the
    /// model half of Gameplay's AttemptLevelConverter (contract D3, AC-10): floor from the level's board
    /// layout, doors and block colours through BoardLayout.TryParseDoorColor, block cells from the level's
    /// authoritative cells, then LevelModel.TryCreate. It resolves no shape, so it needs no piece library
    /// or Gameplay reference. It only reads the LevelData.
    /// </summary>
    internal static class LevelSolverModelBuilder
    {
        /// <summary>On failure error is a designer-language reason without ids, ready for "Not checked: ...".</summary>
        public static bool TryBuild(LevelData level, out LevelModel model, out string error)
        {
            model = null;
            if (level == null)
            {
                error = "no level is open";
                return false;
            }

            if (!level.Validate(out _))
            {
                error = "the level has errors; fix them in Issues first";
                return false;
            }

            if (!BoardLayout.TryCreate(level, out BoardLayout layout, out _)
                || layout.Width != level.Width || layout.Height != level.Height)
            {
                error = "the board could not be built";
                return false;
            }

            int width = layout.Width;
            bool[] floor = new bool[width * layout.Height];
            for (int y = 0; y < layout.Height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    floor[y * width + x] = layout.IsFloor(x, y);
                }
            }

            DoorDefinition[] doors = new DoorDefinition[level.DoorCount];
            for (int i = 0; i < doors.Length; i++)
            {
                LevelDoorData door = level.GetDoorAt(i);
                if (!BoardLayout.TryParseDoorColor(door.ColorId, out PaletteColor doorColor))
                {
                    error = "a gate uses a colour that is not a gate colour";
                    return false;
                }

                // Side ordinals are equal by design, so the cast is the whole mapping (DEC-110).
                doors[i] = new DoorDefinition(i, (Side)(int)door.Side, door.Start, door.Length, new BlockColor((int)doorColor));
            }

            BlockDefinition[] blocks = new BlockDefinition[level.BlockCount];
            for (int i = 0; i < blocks.Length; i++)
            {
                LevelBlockData block = level.GetBlockAt(i);
                Cell[] cells = new Cell[block.CellCount];
                for (int c = 0; c < cells.Length; c++)
                {
                    Vector2Int cell = block.GetCellAt(c);
                    cells[c] = new Cell(cell.x, cell.y);
                }

                // Blocks share the eleven gate colours with doors, as the converter and the level preview do.
                if (!BoardLayout.TryParseDoorColor(block.ColorId, out PaletteColor blockColor))
                {
                    error = "a block uses a colour that is not a gate colour";
                    return false;
                }

                blocks[i] = new BlockDefinition(i, new BlockColor((int)blockColor), new Cell(block.Origin.x, block.Origin.y), cells);
            }

            if (!LevelModel.TryCreate(width, layout.Height, floor, doors, blocks, level.TimerSeconds, out model, out _))
            {
                model = null;
                error = "the board could not be built";
                return false;
            }

            error = null;
            return true;
        }
    }
}
