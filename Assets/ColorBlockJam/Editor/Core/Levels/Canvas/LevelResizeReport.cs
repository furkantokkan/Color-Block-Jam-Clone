using System;
using System.Collections.Generic;
using ColorBlockJam.Runtime.Levels;
using UnityEngine;

namespace ColorBlockJam.Editor.Levels
{
    /// <summary>One ring segment: a board side and the segment index along it (x on Top/Bottom, y on Left/Right).</summary>
    internal readonly struct LevelEdgeSegment : IEquatable<LevelEdgeSegment>
    {
        public readonly BoardSide Side;
        public readonly int Segment;

        public LevelEdgeSegment(BoardSide side, int segment)
        {
            Side = side;
            Segment = segment;
        }

        public bool Equals(LevelEdgeSegment other)
        {
            return Side == other.Side && Segment == other.Segment;
        }

        public override bool Equals(object obj)
        {
            return obj is LevelEdgeSegment other && Equals(other);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine((int)Side, Segment);
        }

        public override string ToString()
        {
            return $"{Side} {Segment}";
        }
    }

    /// <summary>
    /// What one board resize takes away, in the source level's coordinates: the counts for the confirm question and
    /// the cells and door segments the canvas hatches before the designer confirms (LevelEditCommands.TryResizeBoard).
    /// </summary>
    internal readonly struct LevelResizeReport
    {
        public readonly int RemovedBlockCount;
        public readonly int RemovedDoorCount;
        public readonly int ShortenedDoorCount;
        /// <summary>Cell objects (level JSON v2 board objects such as Barrier) with a cell in a removed line.</summary>
        public readonly int RemovedCellObjectCount;

        private readonly Vector2Int[] m_removedCells;
        private readonly LevelEdgeSegment[] m_removedDoorSegments;

        internal LevelResizeReport(int removedBlockCount, int removedDoorCount, int shortenedDoorCount,
            Vector2Int[] removedCells, LevelEdgeSegment[] removedDoorSegments, int removedCellObjectCount = 0)
        {
            RemovedBlockCount = removedBlockCount;
            RemovedDoorCount = removedDoorCount;
            ShortenedDoorCount = shortenedDoorCount;
            RemovedCellObjectCount = removedCellObjectCount;
            m_removedCells = removedCells;
            m_removedDoorSegments = removedDoorSegments;
        }

        /// <summary>True when confirming would delete or shorten content, so the designer must be asked.</summary>
        public bool HasRemovals => RemovedBlockCount > 0 || RemovedDoorCount > 0 || ShortenedDoorCount > 0
                                   || RemovedCellObjectCount > 0;

        /// <summary>Every board cell in the removed lines, whether or not a block covers it.</summary>
        public IReadOnlyList<Vector2Int> RemovedCells => m_removedCells ?? Array.Empty<Vector2Int>();

        /// <summary>The segments that removed or shortened doors lose.</summary>
        public IReadOnlyList<LevelEdgeSegment> RemovedDoorSegments =>
            m_removedDoorSegments ?? Array.Empty<LevelEdgeSegment>();

        /// <summary>
        /// The confirm question with exact counts and no ids, such as "Remove 2 blocks and 1 door?" (PRD 11.6);
        /// empty when nothing is removed.
        /// </summary>
        public string Describe()
        {
            List<string> parts = new List<string>(3);
            if (RemovedBlockCount > 0)
            {
                parts.Add(LevelBoardResize.FormatCount(RemovedBlockCount, "block"));
            }

            if (RemovedDoorCount > 0)
            {
                parts.Add(LevelBoardResize.FormatCount(RemovedDoorCount, "door"));
            }

            if (RemovedCellObjectCount > 0)
            {
                parts.Add(LevelBoardResize.FormatCount(RemovedCellObjectCount, "board object"));
            }

            // "A", "A and B" or "A, B and C".
            string removed = parts.Count == 0 ? null
                : parts.Count == 1 ? parts[0]
                : string.Join(", ", parts.GetRange(0, parts.Count - 1)) + " and " + parts[parts.Count - 1];

            if (ShortenedDoorCount <= 0)
            {
                return removed == null ? string.Empty : $"Remove {removed}?";
            }

            string shortened = LevelBoardResize.FormatCount(ShortenedDoorCount, "door");
            if (removed == null)
            {
                return $"Shorten {shortened}?";
            }

            // The comma keeps "and shorten" apart when the removal part already joins its parts with "and".
            string joint = parts.Count > 1 ? ", and shorten " : " and shorten ";
            return $"Remove {removed}{joint}{shortened}?";
        }
    }
}
