using System;
using System.Collections.Generic;
using ColorBlockJam.Runtime.Board;
using ColorBlockJam.Runtime.Levels;
using UnityEngine;

namespace ColorBlockJam.Editor.Levels
{
    internal interface ILevelBlockWarning
    {
        string GetWarning(LevelData level, LevelBlockData block, string subject);
    }

    /// <summary>Composition of effect warnings; general draft validation does not depend on effect types.</summary>
    internal static class LevelEffectWarnings
    {
        private static readonly ILevelBlockWarning[] s_blockWarnings = { new ArrowBlockWarning() };

        internal static IReadOnlyList<ILevelBlockWarning> BlockWarnings => s_blockWarnings;
    }

    /// <summary>PRE-1: an Arrow block needs a matching door on an axis side within its lane span.</summary>
    internal sealed class ArrowBlockWarning : ILevelBlockWarning
    {
        public string GetWarning(LevelData level, LevelBlockData block, string subject)
        {
            if (block.CellCount == 0 || !block.TryGetEffect(out ArrowEffect arrow))
            {
                return null;
            }

            bool horizontal = arrow.Axis == LevelEffectAxis.Horizontal;
            long lo = long.MaxValue;
            long hi = long.MinValue;
            for (int index = 0; index < block.CellCount; index++)
            {
                Vector2Int cell = block.GetCellAt(index);
                // Longs, as in Validate: an origin near int.MaxValue must not wrap.
                long lane = horizontal ? (long)block.Origin.y + cell.y : (long)block.Origin.x + cell.x;
                lo = Math.Min(lo, lane);
                hi = Math.Max(hi, lane);
            }

            BoardSide first = horizontal ? BoardSide.Left : BoardSide.Top;
            BoardSide second = horizontal ? BoardSide.Right : BoardSide.Bottom;
            for (int index = 0; index < level.DoorCount; index++)
            {
                LevelDoorData door = level.GetDoorAt(index);
                if (door != null && (door.Side == first || door.Side == second) && door.Length >= 1
                    && string.Equals(door.ColorId, block.ColorId, StringComparison.Ordinal)
                    && door.Start <= hi && (long)door.Start + door.Length - 1 >= lo)
                {
                    return null;
                }
            }

            string span = lo == hi
                ? $"{(horizontal ? "row" : "column")} {lo}"
                : $"{(horizontal ? "rows" : "columns")} {lo}-{hi}";
            return $"{subject} can never exit: its {arrow.Axis} arrow needs a {block.ColorId} door on the {first} or "
                + $"{second} edge within {span}.";
        }
    }
}
