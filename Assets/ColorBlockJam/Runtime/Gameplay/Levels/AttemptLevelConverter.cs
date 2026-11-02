using System;
using ColorBlockJam.Domain.Board;
using ColorBlockJam.Runtime.Art;
using ColorBlockJam.Runtime.Layout;
using ColorBlockJam.Runtime.Levels;
using UnityEngine;

namespace ColorBlockJam.Gameplay.Levels
{
    /// <summary>
    /// Converts authored level data into the domain model and the per-block visual specs, once on
    /// Home where allocation is allowed (AC-8.5). It only reads the LevelData, never writes it, and
    /// rejects the whole level with one error naming the first door or block that cannot convert.
    /// Every block builds from its own cells (DEC-193), so no shape is resolved here; whether the block
    /// root prefab is usable is checked where it is used, by the board presenter's TryBuild, which the
    /// session runs before it commits an attempt.
    /// </summary>
    public static class AttemptLevelConverter
    {
        public static bool TryConvert(LevelData level, IBoardLayout layout, out AttemptLevel result, out string error)
        {
            if (level == null)
            {
                throw new ArgumentNullException(nameof(level));
            }

            if (layout == null)
            {
                throw new ArgumentNullException(nameof(layout));
            }

            result = null;
            if (!level.Validate(out error))
            {
                return false;
            }

            if (layout.Width != level.Width || layout.Height != level.Height)
            {
                error = $"Level {level.Id} does not match its board layout size.";
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
                    error = $"Door {door.Id} uses unknown colour '{door.ColorId}'.";
                    return false;
                }

                // Side ordinals are equal by design, so the cast is the whole mapping (DEC-110).
                doors[i] = new DoorDefinition(i, (Side)(int)door.Side, door.Start, door.Length, new BlockColor((int)doorColor));
            }

            BlockDefinition[] blocks = new BlockDefinition[level.BlockCount];
            BlockVisualSpec[] visuals = new BlockVisualSpec[level.BlockCount];
            string[] ids = new string[level.BlockCount];
            for (int i = 0; i < blocks.Length; i++)
            {
                LevelBlockData block = level.GetBlockAt(i);
                Cell[] cells = new Cell[block.CellCount];
                Vector2Int[] visualCells = new Vector2Int[block.CellCount];
                for (int c = 0; c < cells.Length; c++)
                {
                    Vector2Int cell = block.GetCellAt(c);
                    cells[c] = new Cell(cell.x, cell.y);
                    visualCells[c] = cell;
                }

                // Blocks share the eleven gate colours with doors, as the level preview does.
                if (!BoardLayout.TryParseDoorColor(block.ColorId, out PaletteColor blockColor))
                {
                    error = $"Block {block.Id} uses unknown colour '{block.ColorId}'.";
                    return false;
                }

                // The block index is the authored order: the stable pick tie id (DEC-115).
                blocks[i] = new BlockDefinition(i, new BlockColor((int)blockColor), new Cell(block.Origin.x, block.Origin.y), cells);
                // The root shows the level's own cells (DEC-193).
                visuals[i] = new BlockVisualSpec(blockColor, visualCells, block.Origin);
                ids[i] = block.Id;
            }

            if (!LevelModel.TryCreate(width, layout.Height, floor, doors, blocks, level.TimerSeconds,
                    out LevelModel model, out error))
            {
                error = $"Level {level.Id}: {error}";
                return false;
            }

            result = new AttemptLevel(model, layout, level.Id, visuals, ids);
            return true;
        }
    }
}
