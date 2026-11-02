using System.Collections.Generic;
using ColorBlockJam.Editor.Levels;
using ColorBlockJam.Runtime.Board;
using UnityEngine;

namespace ColorBlockJam.Tests.EditMode
{
    /// <summary>
    /// Shape helpers for the Level Editor tests since a piece is the ShapeKey of its cells (DEC-193): the key of a
    /// footprint and a template choice made the way LevelShapeCatalog.Read makes one from a piece-library record.
    /// </summary>
    internal static class LevelTestShapes
    {
        /// <summary>The ShapeKey of these cells; the default key when they break a piece limit.</summary>
        internal static ShapeKey Key(IReadOnlyList<Vector2Int> cells)
        {
            return ShapeKey.TryCreate(cells, out ShapeKey key) ? key : default;
        }

        /// <summary>A piece-library template with this name and footprint; its library key is the key of its cells.</summary>
        internal static LevelShapeChoice Template(string displayName, IReadOnlyList<Vector2Int> cells)
        {
            return new LevelShapeChoice(displayName, cells, Key(cells));
        }
    }
}
