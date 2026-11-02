using System;
using System.Diagnostics;
using ColorBlockJam.Domain.Board;
using ColorBlockJam.Runtime.Board;
using ColorBlockJam.Runtime.Levels;

namespace ColorBlockJam.Editor.Levels.Validation
{
    /// <summary>
    /// The one solvability check the Level Editor's Validate Level button and Save Level share (PRD 7.9,
    /// REQ-O2). It runs the support gate, the block limit, the model builder, the stage 2 pre-checks and the
    /// bounded stage 3 search, in that order, on the calling thread (contract D1). Validate Level also lists a Solvable
    /// level's distinct solutions, with the check's own tables (RunAndList) or for a verdict it already holds
    /// (ListSolutions). Stage 1 structural validation and the Play Mode refusal belong to the caller. It only reads the
    /// level.
    /// </summary>
    public static class LevelSolvabilityCheck
    {
        public static LevelCheckResult Run(LevelData level)
        {
            return Run(level, null, null);
        }

        /// <summary>
        /// The verdict alone, as Save Level, Test Level and save-on-close ask for it; it never lists solutions. Null
        /// options use the defaults; null progress is silent and never cancels.
        /// </summary>
        public static LevelCheckResult Run(LevelData level, LevelCheckOptions options, ILevelCheckProgress progress)
        {
            return Check(level, options ?? new LevelCheckOptions(), progress ?? SilentLevelCheckProgress.Instance,
                out _);
        }

        /// <summary>
        /// Validate Level: the check and, for a Solvable verdict, the solution listing, which reuses the check's tables,
        /// so they are built once. Each phase reports through its own progress, and the listing's positions and time
        /// start from 0 when it starts. A Cancel during the check gives its Not checked answer and no list; a Cancel
        /// during the listing keeps the verdict and the entries found so far. The result equals Run followed by
        /// ListSolutions. Null options use the defaults; a null progress is silent and never cancels.
        /// </summary>
        public static LevelCheckResult RunAndList(LevelData level, LevelCheckOptions options,
            ILevelCheckProgress checkProgress, ILevelCheckProgress listProgress)
        {
            options = options ?? new LevelCheckOptions();
            LevelCheckResult verdict = Check(level, options, checkProgress ?? SilentLevelCheckProgress.Instance,
                out LevelSolverTables tables);
            if (verdict.Status != LevelCheckStatus.Solvable)
            {
                return verdict;
            }

            return LevelSolutionListing.Run(tables, verdict, options, listProgress ?? SilentLevelCheckProgress.Instance,
                Stopwatch.StartNew());
        }

        /// <summary>
        /// Lists distinct solutions for a verdict the caller already holds for this unchanged level, for example a kept
        /// result without a list: the check does not run again, but its tables are built again. The listing's time
        /// budget counts from just before that rebuild, and its first progress report, which can cancel, comes right
        /// after it. A verdict that is not Solvable comes back unchanged. Null options use the defaults; null progress
        /// is silent and never cancels. A Solvable verdict that cannot be this level's (the check would refuse the
        /// level, or its solution does not clear exactly the level's blocks) throws ArgumentException.
        /// </summary>
        public static LevelCheckResult ListSolutions(LevelData level, LevelCheckResult verdict, LevelCheckOptions options,
            ILevelCheckProgress progress)
        {
            if (verdict == null)
            {
                throw new ArgumentNullException(nameof(verdict));
            }

            if (verdict.Status != LevelCheckStatus.Solvable)
            {
                return verdict;
            }

            if (level == null)
            {
                throw new ArgumentNullException(nameof(level));
            }

            Stopwatch stopwatch = Stopwatch.StartNew();
            // A Solvable verdict passed these gates for its own level, so a refusal here means another level.
            if (!TryCreateTables(level, out LevelSolverTables tables, out _))
            {
                throw new ArgumentException("The verdict is not a Solvable verdict of this level.", nameof(verdict));
            }

            return LevelSolutionListing.Run(tables, verdict, options ?? new LevelCheckOptions(),
                progress ?? SilentLevelCheckProgress.Instance, stopwatch);
        }

        // The check itself: gates, tables, pre-checks and the search. tables holds the check's cached Domain answers
        // for the listing, or null when a gate answered before they were built.
        private static LevelCheckResult Check(LevelData level, LevelCheckOptions options, ILevelCheckProgress progress,
            out LevelSolverTables tables)
        {
            Stopwatch stopwatch = Stopwatch.StartNew();
            tables = null;

            if (level == null)
            {
                return LevelCheckResult.CreateNotChecked("no level is open", 0, Seconds(stopwatch));
            }

            if (!TryCreateTables(level, out tables, out string notCheckedReason))
            {
                return LevelCheckResult.CreateNotChecked(notCheckedReason, 0, Seconds(stopwatch));
            }

            Func<ShapeKey, string> shapeNames = options.ShapeNames;
            LevelBlockDescriber describe = options.BlockDescriber
                ?? ((describedLevel, block) => LevelDesignerText.DescribeBlock(describedLevel, block, shapeNames));
            string preCheckReason = LevelPreChecks.FindReason(level, tables, describe);
            if (preCheckReason != null)
            {
                return LevelCheckResult.CreateUnsolvable(preCheckReason, 0, Seconds(stopwatch));
            }

            LevelSolverSearch search = new LevelSolverSearch(tables, options, progress, stopwatch);
            LevelCheckResult result = search.Run();
            if (result.Status != LevelCheckStatus.Unsolvable)
            {
                return result;
            }

            // An exhausted search names the blocks that never left in any reachable position (DEC-176). A budget hit
            // names none: a block that has not left yet may simply leave late in a solution.
            int stuck = ((1 << tables.BlockCount) - 1) & ~search.EverExitedMask;
            return stuck == 0
                ? result
                : LevelCheckResult.CreateUnsolvable(LevelDesignerText.DescribeBlocks(level, stuck, describe)
                    + " can never leave", result.VisitedPositions, result.ElapsedSeconds);
        }

        // The one gate chain the check and the listing share (DEC-178): the support table, the block limit and the
        // model build, then the cached Domain answers. On false, tables is null and the reason is the Not checked text.
        private static bool TryCreateTables(LevelData level, out LevelSolverTables tables, out string notCheckedReason)
        {
            tables = null;

            // Never "Solvable" for a rule the search ignores (DEC-MECH-12).
            if (LevelSolverSupport.TryFindUnsupported(level, out string mechanic))
            {
                notCheckedReason = "this level uses " + mechanic + ", which the check does not cover yet";
                return false;
            }

            if (level.BlockCount > LevelCheckOptions.MaxBlocks)
            {
                notCheckedReason = "this level has " + level.BlockCount + " blocks and the check covers at most "
                    + LevelCheckOptions.MaxBlocks;
                return false;
            }

            if (!LevelSolverModelBuilder.TryBuild(level, out LevelModel model, out notCheckedReason))
            {
                return false;
            }

            tables = LevelSolverTables.Create(model, ReadIceCounts(level), ReadArrowAxes(level));
            notCheckedReason = null;
            return true;
        }

        /// <summary>Each block's authored clearsToThaw, 0 without Ice; the same read Gameplay's Ice module makes.</summary>
        internal static int[] ReadIceCounts(LevelData level)
        {
            int[] counts = new int[level.BlockCount];
            for (int block = 0; block < counts.Length; block++)
            {
                if (level.GetBlockAt(block).TryGetEffect(out IceEffect ice))
                {
                    counts[block] = ice.ClearsToThaw;
                }
            }

            return counts;
        }

        /// <summary>
        /// Each block's movement axes: Horizontal or Vertical for an Arrow block, Both without Arrow; the same
        /// read (ArrowMarkerRun.TryGetAxis) and mapping Gameplay's Arrow module makes.
        /// </summary>
        internal static AxisMask[] ReadArrowAxes(LevelData level)
        {
            AxisMask[] axes = new AxisMask[level.BlockCount];
            for (int block = 0; block < axes.Length; block++)
            {
                axes[block] = !ArrowMarkerRun.TryGetAxis(level.GetBlockAt(block), out LevelEffectAxis axis) ? AxisMask.Both
                    : axis == LevelEffectAxis.Horizontal ? AxisMask.Horizontal : AxisMask.Vertical;
            }

            return axes;
        }

        private static double Seconds(Stopwatch stopwatch) => stopwatch.Elapsed.TotalSeconds;
    }
}
