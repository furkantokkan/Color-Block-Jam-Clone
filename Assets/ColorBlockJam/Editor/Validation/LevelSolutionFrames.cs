using System;
using System.Collections.Generic;
using System.Globalization;
using ColorBlockJam.Runtime.Levels;
using UnityEngine;

namespace ColorBlockJam.Editor.Levels.Validation
{
    /// <summary>
    /// Turns a level and a solution's steps into playback frames without changing the level: frame i is the level
    /// after steps 0 to i. A slide moves its block to the step's To origin and an exit removes the block; a frozen
    /// block's clearsToThaw is its authored count minus the exits so far, and its Ice is gone at 0. Every other
    /// field, effect, door, void cell, board object and piece name is copied unchanged, so a full solution ends on a
    /// board without blocks.
    /// </summary>
    public static class LevelSolutionFrames
    {
        // The Ice count key of the level JSON (LevelEffectRegistry's "ice" definition).
        private const string k_iceCountKey = "clearsToThaw";

        /// <summary>One frame per step; the caller shows the unchanged level as the start.</summary>
        public static IReadOnlyList<LevelData> Create(LevelData level, IReadOnlyList<LevelSolutionStep> steps)
        {
            if (level == null)
            {
                throw new ArgumentNullException(nameof(level));
            }

            if (steps == null)
            {
                throw new ArgumentNullException(nameof(steps));
            }

            int blockCount = level.BlockCount;
            Vector2Int[] origins = new Vector2Int[blockCount];
            bool[] exited = new bool[blockCount];
            for (int block = 0; block < blockCount; block++)
            {
                origins[block] = level.GetBlockAt(block).Origin;
            }

            // The LevelData constructor copies every record, so the frames can share these arrays.
            Vector2Int[] voidCells = GetVoidCells(level);
            LevelDoorData[] doors = GetDoors(level);
            LevelCellObjectData[] cellObjects = GetCellObjects(level);
            LevelPieceName[] pieceNames = GetPieceNames(level);
            LevelData[] frames = new LevelData[steps.Count];
            int exitCount = 0;
            for (int i = 0; i < steps.Count; i++)
            {
                LevelSolutionStep step = steps[i];
                int block = step.BlockIndex;
                if (block < 0 || block >= blockCount || exited[block])
                {
                    throw new ArgumentException("Step " + i + " moves block " + block + ", which is not on the board.",
                        nameof(steps));
                }

                if (step.IsExit)
                {
                    exited[block] = true;
                    exitCount++;
                }
                else
                {
                    origins[block] = new Vector2Int(step.ToX, step.ToY);
                }

                frames[i] = new LevelData(level.Id, level.Name, level.Width, level.Height, level.TimerSeconds, voidCells,
                    doors, CreateBlocks(level, origins, exited, exitCount), cellObjects, pieceNames);
            }

            return frames;
        }

        // The blocks still on the board at their current origins, in authored order.
        private static LevelBlockData[] CreateBlocks(LevelData level, Vector2Int[] origins, bool[] exited, int exitCount)
        {
            LevelBlockData[] blocks = new LevelBlockData[level.BlockCount - exitCount];
            int next = 0;
            for (int block = 0; block < level.BlockCount; block++)
            {
                if (exited[block])
                {
                    continue;
                }

                LevelBlockData source = level.GetBlockAt(block);
                blocks[next++] = new LevelBlockData(source.Id, source.ColorId, origins[block], GetCells(source),
                    CreateEffects(source, exitCount));
            }

            return blocks;
        }

        // The block's effects with its Ice count lowered by the exits so far; the Ice effect is dropped at 0.
        private static LevelEffectData[] CreateEffects(LevelBlockData block, int exitCount)
        {
            int iceIndex = FindIceEffect(block);
            int remaining = iceIndex < 0 ? 0 : ((IceEffect)block.GetEffectAt(iceIndex).Parameters).ClearsToThaw - exitCount;
            bool dropIce = iceIndex >= 0 && remaining <= 0;
            LevelEffectData[] effects = new LevelEffectData[block.EffectCount - (dropIce ? 1 : 0)];
            int next = 0;
            for (int index = 0; index < block.EffectCount; index++)
            {
                LevelEffectData effect = block.GetEffectAt(index);
                if (index == iceIndex && dropIce)
                {
                    continue;
                }

                effects[next++] = index == iceIndex && exitCount > 0 ? WithIceCount(effect, remaining) : effect;
            }

            return effects;
        }

        // The first effect with Ice parameters, the one LevelBlockData.TryGetEffect and the check's Ice counts read; -1 without.
        private static int FindIceEffect(LevelBlockData block)
        {
            for (int index = 0; index < block.EffectCount; index++)
            {
                if (block.GetEffectAt(index)?.Parameters is IceEffect)
                {
                    return index;
                }
            }

            return -1;
        }

        private static LevelEffectData WithIceCount(LevelEffectData effect, int count)
        {
            LevelEffectParamData[] parameters = new LevelEffectParamData[effect.ParamCount];
            for (int index = 0; index < parameters.Length; index++)
            {
                LevelEffectParamData param = effect.GetParamAt(index);
                parameters[index] = param != null && string.Equals(param.Key, k_iceCountKey, StringComparison.Ordinal)
                    ? new LevelEffectParamData(param.Key, count.ToString(CultureInfo.InvariantCulture))
                    : param;
            }

            return new LevelEffectData(effect.Type, parameters);
        }

        private static Vector2Int[] GetCells(LevelBlockData block)
        {
            Vector2Int[] cells = new Vector2Int[block.CellCount];
            for (int index = 0; index < cells.Length; index++)
            {
                cells[index] = block.GetCellAt(index);
            }

            return cells;
        }

        private static Vector2Int[] GetVoidCells(LevelData level)
        {
            Vector2Int[] cells = new Vector2Int[level.VoidCellCount];
            for (int index = 0; index < cells.Length; index++)
            {
                cells[index] = level.GetVoidCellAt(index);
            }

            return cells;
        }

        private static LevelDoorData[] GetDoors(LevelData level)
        {
            LevelDoorData[] doors = new LevelDoorData[level.DoorCount];
            for (int index = 0; index < doors.Length; index++)
            {
                doors[index] = level.GetDoorAt(index);
            }

            return doors;
        }

        private static LevelCellObjectData[] GetCellObjects(LevelData level)
        {
            LevelCellObjectData[] cellObjects = new LevelCellObjectData[level.CellObjectCount];
            for (int index = 0; index < cellObjects.Length; index++)
            {
                cellObjects[index] = level.GetCellObjectAt(index);
            }

            return cellObjects;
        }

        private static LevelPieceName[] GetPieceNames(LevelData level)
        {
            LevelPieceName[] pieceNames = new LevelPieceName[level.PieceNameCount];
            for (int index = 0; index < pieceNames.Length; index++)
            {
                pieceNames[index] = level.GetPieceNameAt(index);
            }

            return pieceNames;
        }
    }
}
