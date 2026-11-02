using ColorBlockJam.Runtime.Levels;
using UnityEngine;

namespace ColorBlockJam.Editor.Levels
{
    /// <summary>
    /// Field-complete copies of level records with one changed field, so an editor command never drops a field it
    /// does not edit: block and door effects, void cells, cell objects and the level's piece names (level JSON v3)
    /// travel with every copy (docs/Level-Editor-Design.md section 4).
    /// </summary>
    internal static class LevelRecordCopy
    {
        public static LevelBlockData WithOrigin(LevelBlockData block, Vector2Int origin)
        {
            return new LevelBlockData(block.Id, block.ColorId, origin, GetCells(block), GetEffects(block));
        }

        public static LevelBlockData WithColor(LevelBlockData block, string colorId)
        {
            return new LevelBlockData(block.Id, colorId, block.Origin, GetCells(block), GetEffects(block));
        }

        /// <summary>New cells, so a new piece when they form another key (DEC-193); origin, id, colour and effects stay.</summary>
        public static LevelBlockData WithCells(LevelBlockData block, Vector2Int[] cells)
        {
            return new LevelBlockData(block.Id, block.ColorId, block.Origin, cells, GetEffects(block));
        }

        /// <summary>A new first segment and length along the same side; id, side, colour and effects stay.</summary>
        public static LevelDoorData WithSpan(LevelDoorData door, int start, int length)
        {
            return new LevelDoorData(door.Id, door.Side, start, length, door.ColorId, GetEffects(door));
        }

        public static LevelDoorData WithColor(LevelDoorData door, string colorId)
        {
            return new LevelDoorData(door.Id, door.Side, door.Start, door.Length, colorId, GetEffects(door));
        }

        /// <summary>Another effect list (the Effect Editor); id, colour, origin and cells stay.</summary>
        public static LevelBlockData WithEffects(LevelBlockData block, LevelEffectData[] effects)
        {
            return new LevelBlockData(block.Id, block.ColorId, block.Origin, GetCells(block), effects);
        }

        /// <summary>Another effect list (the Effect Editor); id, side, span and colour stay.</summary>
        public static LevelDoorData WithEffects(LevelDoorData door, LevelEffectData[] effects)
        {
            return new LevelDoorData(door.Id, door.Side, door.Start, door.Length, door.ColorId, effects);
        }

        /// <summary>Another effect for a board object (the Effect Editor); its id and cells stay.</summary>
        public static LevelCellObjectData WithEffect(LevelCellObjectData cellObject, LevelEffectData effect)
        {
            return new LevelCellObjectData(cellObject.Id, GetCells(cellObject), effect);
        }

        /// <summary>The level with another block list; every other field, doors and piece names included, is copied.</summary>
        public static LevelData WithBlocks(LevelData level, LevelBlockData[] blocks)
        {
            return new LevelData(level.Id, level.Name, level.Width, level.Height, level.TimerSeconds,
                GetVoidCells(level), GetDoors(level), blocks, GetCellObjects(level), GetPieceNames(level));
        }

        /// <summary>The level with another door list; every other field, blocks included, is copied.</summary>
        public static LevelData WithDoors(LevelData level, LevelDoorData[] doors)
        {
            return new LevelData(level.Id, level.Name, level.Width, level.Height, level.TimerSeconds,
                GetVoidCells(level), doors, GetBlocks(level), GetCellObjects(level), GetPieceNames(level));
        }

        /// <summary>The level with another void cell list; every other field is copied.</summary>
        public static LevelData WithVoidCells(LevelData level, Vector2Int[] voidCells)
        {
            return new LevelData(level.Id, level.Name, level.Width, level.Height, level.TimerSeconds,
                voidCells, GetDoors(level), GetBlocks(level), GetCellObjects(level), GetPieceNames(level));
        }

        /// <summary>The level with another board object list; every other field is copied.</summary>
        public static LevelData WithCellObjects(LevelData level, LevelCellObjectData[] cellObjects)
        {
            return new LevelData(level.Id, level.Name, level.Width, level.Height, level.TimerSeconds,
                GetVoidCells(level), GetDoors(level), GetBlocks(level), cellObjects, GetPieceNames(level));
        }

        /// <summary>The level with another table of piece names (DEC-193); every other field is copied.</summary>
        public static LevelData WithPieceNames(LevelData level, LevelPieceName[] pieceNames)
        {
            return new LevelData(level.Id, level.Name, level.Width, level.Height, level.TimerSeconds,
                GetVoidCells(level), GetDoors(level), GetBlocks(level), GetCellObjects(level), pieceNames);
        }

        /// <summary>
        /// The level with another board size and the content that fits it, in the new board's coordinates; id, name,
        /// timer and piece names stay.
        /// </summary>
        public static LevelData WithBoard(LevelData level, int width, int height, Vector2Int[] voidCells,
            LevelDoorData[] doors, LevelBlockData[] blocks, LevelCellObjectData[] cellObjects)
        {
            return new LevelData(level.Id, level.Name, width, height, level.TimerSeconds, voidCells, doors, blocks,
                cellObjects, GetPieceNames(level));
        }

        public static LevelEffectData[] GetEffects(LevelBlockData block)
        {
            LevelEffectData[] effects = new LevelEffectData[block.EffectCount];
            for (int index = 0; index < effects.Length; index++)
            {
                effects[index] = block.GetEffectAt(index);
            }

            return effects;
        }

        public static LevelEffectData[] GetEffects(LevelDoorData door)
        {
            LevelEffectData[] effects = new LevelEffectData[door.EffectCount];
            for (int index = 0; index < effects.Length; index++)
            {
                effects[index] = door.GetEffectAt(index);
            }

            return effects;
        }

        public static Vector2Int[] GetVoidCells(LevelData level)
        {
            Vector2Int[] cells = new Vector2Int[level.VoidCellCount];
            for (int index = 0; index < cells.Length; index++)
            {
                cells[index] = level.GetVoidCellAt(index);
            }

            return cells;
        }

        public static LevelCellObjectData[] GetCellObjects(LevelData level)
        {
            LevelCellObjectData[] cellObjects = new LevelCellObjectData[level.CellObjectCount];
            for (int index = 0; index < cellObjects.Length; index++)
            {
                cellObjects[index] = level.GetCellObjectAt(index);
            }

            return cellObjects;
        }

        /// <summary>The level's piece names as a new array of the same records; callers replace entries, not fields.</summary>
        public static LevelPieceName[] GetPieceNames(LevelData level)
        {
            LevelPieceName[] names = new LevelPieceName[level.PieceNameCount];
            for (int index = 0; index < names.Length; index++)
            {
                names[index] = level.GetPieceNameAt(index);
            }

            return names;
        }

        /// <summary>The object's absolute board cells as a new array.</summary>
        public static Vector2Int[] GetCells(LevelCellObjectData cellObject)
        {
            Vector2Int[] cells = new Vector2Int[cellObject.CellCount];
            for (int index = 0; index < cells.Length; index++)
            {
                cells[index] = cellObject.GetCellAt(index);
            }

            return cells;
        }

        /// <summary>The block's relative cells as a new array.</summary>
        public static Vector2Int[] GetCells(LevelBlockData block)
        {
            Vector2Int[] cells = new Vector2Int[block.CellCount];
            for (int index = 0; index < cells.Length; index++)
            {
                cells[index] = block.GetCellAt(index);
            }

            return cells;
        }

        /// <summary>The level's blocks as a new array of the same records; callers replace entries, not fields.</summary>
        public static LevelBlockData[] GetBlocks(LevelData level)
        {
            LevelBlockData[] blocks = new LevelBlockData[level.BlockCount];
            for (int index = 0; index < blocks.Length; index++)
            {
                blocks[index] = level.GetBlockAt(index);
            }

            return blocks;
        }

        /// <summary>The level's doors as a new array of the same records; callers replace entries, not fields.</summary>
        public static LevelDoorData[] GetDoors(LevelData level)
        {
            LevelDoorData[] doors = new LevelDoorData[level.DoorCount];
            for (int index = 0; index < doors.Length; index++)
            {
                doors[index] = level.GetDoorAt(index);
            }

            return doors;
        }
    }
}
