using ColorBlockJam.Runtime.Art;
using ColorBlockJam.Runtime.Levels;
using UnityEngine;

namespace ColorBlockJam.Tests.EditMode
{
    /// <summary>
    /// Shared level and effect fixture builders for the Level JSON v2 tests, so LevelEffectTests and
    /// LevelEffectCommandsTests keep one definition each instead of parallel copies.
    /// </summary>
    internal static class LevelTestData
    {
        internal static LevelEffectData Effect(string type, params (string Key, string Value)[] parameters)
        {
            LevelEffectParamData[] pairs = new LevelEffectParamData[parameters.Length];
            for (int index = 0; index < parameters.Length; index++)
            {
                pairs[index] = new LevelEffectParamData(parameters[index].Key, parameters[index].Value);
            }

            return new LevelEffectData(type, pairs);
        }

        internal static LevelCellObjectData Object(string id, LevelEffectData effect, params Vector2Int[] cells)
        {
            return new LevelCellObjectData(id, cells, effect);
        }

        internal static LevelBlockData SingleBlock(string id, Vector2Int origin)
        {
            return new LevelBlockData(id, "Red", origin, new[] { Vector2Int.zero });
        }

        internal static bool IsRuntimeImplemented(string type)
        {
            return LevelEffectRegistry.TryFind(type, out LevelEffectDefinition definition)
                && definition.IsRuntimeImplemented;
        }

        /// <summary>
        /// A 5 x 5 fixture: b1 Red at (0,0)-(1,0), b2 Blue at (0,1)-(1,1) touching b1, b3 Yellow at (3,3);
        /// d1 Red on Top 0-1 (span 2), d2 Blue on Bottom 3 (span 1; left out when oneDoor is true).
        /// </summary>
        internal static LevelData EffectsFixture(
            LevelEffectData[] b1 = null,
            LevelEffectData[] b2 = null,
            LevelEffectData[] b3 = null,
            LevelEffectData[] d1 = null,
            LevelEffectData[] d2 = null,
            LevelCellObjectData[] objects = null,
            Vector2Int[] voidCells = null,
            bool oneDoor = false)
        {
            Vector2Int[] pair = { Vector2Int.zero, new Vector2Int(1, 0) };
            LevelDoorData first = new LevelDoorData("d1", BoardSide.Top, 0, 2, "Red", d1 ?? new LevelEffectData[0]);
            return new LevelData("fx", "Effects", 5, 5, 60,
                voidCells ?? new Vector2Int[0],
                oneDoor
                    ? new[] { first }
                    : new[] { first, new LevelDoorData("d2", BoardSide.Bottom, 3, 1, "Blue", d2 ?? new LevelEffectData[0]) },
                new[]
                {
                    new LevelBlockData("b1", "Red", new Vector2Int(0, 0), pair, b1 ?? new LevelEffectData[0]),
                    new LevelBlockData("b2", "Blue", new Vector2Int(0, 1), pair, b2 ?? new LevelEffectData[0]),
                    new LevelBlockData("b3", PaletteColor.Yellow.ToString(), new Vector2Int(3, 3),
                        new[] { Vector2Int.zero }, b3 ?? new LevelEffectData[0])
                },
                objects ?? new LevelCellObjectData[0]);
        }

        /// <summary>
        /// A 5 x 6 level with a void top-right corner, gates d1 (Bottom 1-2), d2 (Bottom 3) and d3 (Top 0), a Red domino
        /// b1 at (1,1), a Blue single b2 with Ice at (1,3), a closed barrier-1 without params at (3,2) and an open
        /// barrier-2 under b2 at (1,3) and (2,3).
        /// </summary>
        internal static LevelData EffectEditorFixture(params LevelEffectData[] d3Effects)
        {
            LevelDoorData[] doors =
            {
                new LevelDoorData("d1", BoardSide.Bottom, 1, 2, "Red"),
                new LevelDoorData("d2", BoardSide.Bottom, 3, 1, "Blue"),
                new LevelDoorData("d3", BoardSide.Top, 0, 1, "Yellow", d3Effects)
            };
            LevelBlockData[] blocks =
            {
                new LevelBlockData("b1", "Red", new Vector2Int(1, 1), new[] { Vector2Int.zero, Vector2Int.right }),
                new LevelBlockData("b2", "Blue", new Vector2Int(1, 3), new[] { Vector2Int.zero },
                    new[] { Effect("ice", ("clearsToThaw", "2")) })
            };
            LevelCellObjectData[] objects =
            {
                new LevelCellObjectData("barrier-1", new[] { new Vector2Int(3, 2) }, Effect("barrier")),
                new LevelCellObjectData("barrier-2", new[] { new Vector2Int(1, 3), new Vector2Int(2, 3) },
                    Effect("barrier", ("startsOpen", "true")))
            };
            return new LevelData("L1", "Effects", 5, 6, 60, new[] { new Vector2Int(4, 5) }, doors, blocks, objects);
        }
    }
}
