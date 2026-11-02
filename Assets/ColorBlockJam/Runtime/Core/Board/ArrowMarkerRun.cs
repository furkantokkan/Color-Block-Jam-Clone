using System.Collections.Generic;
using ColorBlockJam.Runtime.Levels;
using UnityEngine;

namespace ColorBlockJam.Runtime.Board
{
    /// <summary>
    /// The Arrow block marker (catalog BLK-01) one block shows, in the cell space of the cells it was computed from:
    /// its axis, the centre of the footprint's bounding box, the box's extent along the axis and the Arrow_N variant
    /// that extent picks. The default value is no marker.
    /// </summary>
    public readonly struct ArrowMarker
    {
        public ArrowMarker(LevelEffectAxis axis, Vector2 center, int extent)
        {
            Axis = axis;
            Center = center;
            Extent = extent;
        }

        public LevelEffectAxis Axis { get; }

        /// <summary>The footprint's bounding-box centre, in cells: cell (x, y) is centred on (x, y).</summary>
        public Vector2 Center { get; }

        /// <summary>The bounding box's extent along the axis, in cells; 0 for no marker.</summary>
        public int Extent { get; }

        public bool HasMarker => Extent > 0;

        /// <summary>
        /// The Arrow_N prefab and the marker's length in cells: the extent clamped to 1-3, so a longer block reuses
        /// Arrow_3 without stretching; 0 for no marker.
        /// </summary>
        public int Variant => HasMarker ? Mathf.Min(Extent, ArrowMarkerRun.MaxVariant) : 0;
    }

    /// <summary>
    /// The one BLK-01 view rule the Level Editor canvas, the LevelEditor scene preview and the Game scene share: the
    /// marker is centred on the footprint's bounding box and runs along the block's axis over the box's extent,
    /// clamped to 1-3 cells. An irregular footprint (L, T) keeps the box centre, even over an empty part of the box.
    /// Plain C#: no scene object, no Onity and no allocation per call. Revision 1 of
    /// TASK-20260926-212320271-98b25efa-level-editor-arrow-markers supersedes the catalog's run through the bounds
    /// centre.
    /// </summary>
    public static class ArrowMarkerRun
    {
        /// <summary>Arrow_1 to Arrow_3; a longer extent reuses Arrow_3 without stretching.</summary>
        public const int MaxVariant = 3;

        /// <summary>
        /// The axis of the block's Arrow effect. False for no Arrow effect, or one whose axis is missing or invalid:
        /// the registry converts only effects whose keys follow their rules (LevelEffectRegistry.TryConvert).
        /// </summary>
        public static bool TryGetAxis(LevelBlockData block, out LevelEffectAxis axis)
        {
            if (block != null && block.TryGetEffect(out ArrowEffect arrow))
            {
                axis = arrow.Axis;
                return true;
            }

            axis = default;
            return false;
        }

        /// <summary>The marker of the block's Arrow effect over these cells (in any cell space), or none.</summary>
        public static ArrowMarker Create(LevelBlockData block, IReadOnlyList<Vector2Int> cells)
        {
            return TryGetAxis(block, out LevelEffectAxis axis) ? Create(cells, axis) : default;
        }

        /// <summary>The marker along the axis over these cells, in their cell space; none for no cells or no axis.</summary>
        public static ArrowMarker Create(IReadOnlyList<Vector2Int> cells, LevelEffectAxis axis)
        {
            if (cells == null || cells.Count == 0
                || (axis != LevelEffectAxis.Horizontal && axis != LevelEffectAxis.Vertical))
            {
                return default;
            }

            Vector2Int min = cells[0];
            Vector2Int max = cells[0];
            for (int index = 1; index < cells.Count; index++)
            {
                min = Vector2Int.Min(min, cells[index]);
                max = Vector2Int.Max(max, cells[index]);
            }

            int extent = (axis == LevelEffectAxis.Horizontal ? max.x - min.x : max.y - min.y) + 1;
            Vector2 center = new Vector2((min.x + max.x) * 0.5f, (min.y + max.y) * 0.5f);
            return new ArrowMarker(axis, center, extent);
        }
    }
}
