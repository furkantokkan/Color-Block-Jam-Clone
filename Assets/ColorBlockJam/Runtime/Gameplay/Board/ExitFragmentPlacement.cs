using System;
using ColorBlockJam.Domain.Board;
using ColorBlockJam.Presentation.Board;
using UnityEngine;

namespace ColorBlockJam.Gameplay.Board
{
    /// <summary>
    /// Where an exiting block's fragments start (DEC-131): on the door line, where the exit clip plane
    /// hides the block, centred across the lanes the block covers along that side, at the block body's
    /// middle height and turned so the emitter's +Z points out of the board. BoardRoot-local board
    /// units, one cell per unit, as the clip plane and CellToLocal use them.
    /// </summary>
    public readonly struct ExitFragmentPlacement
    {
        /// <summary>Height of the block body's middle above BoardRoot: BoardHeights.BlockCenter, which the Ice shell shares.</summary>
        public const float k_centerHeight = BoardHeights.BlockCenter;

        public ExitFragmentPlacement(Vector3 localPosition, Quaternion localRotation, int lanes)
        {
            LocalPosition = localPosition;
            LocalRotation = localRotation;
            Lanes = lanes;
        }

        public Vector3 LocalPosition { get; }

        public Quaternion LocalRotation { get; }

        /// <summary>The block's extent along the door side, in cells: the emitting box's width.</summary>
        public int Lanes { get; }

        /// <summary>
        /// The door line on the side's outward axis (local z for Top and Bottom, x for Left and Right): the
        /// floor edge plus ExitMotionSettings.k_clipOffsetCells outward, the door piece's centre line.
        /// </summary>
        public static float GetDoorLine(LevelModel model, Side side)
        {
            if (model == null)
            {
                throw new ArgumentNullException(nameof(model));
            }

            float edge;
            switch (side)
            {
                case Side.Top:
                    edge = model.Height - 0.5f;
                    break;
                case Side.Right:
                    edge = model.Width - 0.5f;
                    break;
                default:
                    edge = -0.5f;
                    break;
            }

            Cell outward = SideMath.GetOutward(side);
            return edge + ExitMotionSettings.k_clipOffsetCells * (outward.X + outward.Y);
        }

        /// <summary>
        /// The placement for an authored block leaving through side from origin, the exit event's cell. It reads
        /// the authored footprint, so a Rocket part or a narrowed block uses the IBoardReader overload instead.
        /// </summary>
        public static ExitFragmentPlacement Create(LevelModel model, int block, Side side, Cell origin)
        {
            if (model == null)
            {
                throw new ArgumentNullException(nameof(model));
            }

            BlockDefinition definition = model.GetBlock(block);
            return Create(model, definition.MinX, definition.MaxX, definition.MinY, definition.MaxY, side, origin);
        }

        /// <summary>
        /// The placement for block leaving through side from origin, over the block's runtime footprint (B-19):
        /// a Rocket part or a block that lost a cell emits across the lanes it covers now, not the authored ones.
        /// An exited block's footprint stays readable, so the view sync phase can call this after the exit.
        /// </summary>
        public static ExitFragmentPlacement Create(IBoardReader board, int block, Side side, Cell origin)
        {
            if (board == null)
            {
                throw new ArgumentNullException(nameof(board));
            }

            BlockFootprint footprint = board.GetFootprint(block);
            return Create(board.Model, footprint.MinX, footprint.MaxX, footprint.MinY, footprint.MaxY, side, origin);
        }

        private static ExitFragmentPlacement Create(LevelModel model, int minX, int maxX, int minY, int maxY, Side side,
            Cell origin)
        {
            bool alongX = SideMath.RunsAlongX(side);
            int first = alongX ? origin.X + minX : origin.Y + minY;
            int last = alongX ? origin.X + maxX : origin.Y + maxY;
            float centre = (first + last) * 0.5f;
            float line = GetDoorLine(model, side);
            Vector3 position = alongX
                ? new Vector3(centre, k_centerHeight, line)
                : new Vector3(line, k_centerHeight, centre);

            Cell outward = SideMath.GetOutward(side);
            Quaternion rotation = Quaternion.LookRotation(new Vector3(outward.X, 0f, outward.Y), Vector3.up);
            return new ExitFragmentPlacement(position, rotation, last - first + 1);
        }
    }
}
