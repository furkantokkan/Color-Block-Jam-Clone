using System;

namespace ColorBlockJam.Editor.Levels
{
    internal enum LevelCanvasSelectionKind
    {
        None = 0,
        Block = 1,
        Door = 2,
        Obstacle = 3
    }

    /// <summary>
    /// The block, door or board object (obstacle) selected in the Level Editor, stored as (id, index) so it survives
    /// draft edits; the canvas model resolves it again after every change (docs/Level-Editor-Design.md section 2).
    /// </summary>
    internal readonly struct LevelCanvasSelection : IEquatable<LevelCanvasSelection>
    {
        public static readonly LevelCanvasSelection None = default;

        public readonly LevelCanvasSelectionKind Kind;
        public readonly int Index;
        public readonly string Id;

        private LevelCanvasSelection(LevelCanvasSelectionKind kind, int index, string id)
        {
            Kind = kind;
            Index = index;
            Id = id;
        }

        public static LevelCanvasSelection ForBlock(int index, string id)
        {
            return new LevelCanvasSelection(LevelCanvasSelectionKind.Block, index, id);
        }

        public static LevelCanvasSelection ForDoor(int index, string id)
        {
            return new LevelCanvasSelection(LevelCanvasSelectionKind.Door, index, id);
        }

        /// <summary>A board object (level cell object) by its index in the level's list and its id.</summary>
        public static LevelCanvasSelection ForObstacle(int index, string id)
        {
            return new LevelCanvasSelection(LevelCanvasSelectionKind.Obstacle, index, id);
        }

        public bool Equals(LevelCanvasSelection other)
        {
            return Kind == other.Kind && Index == other.Index && string.Equals(Id, other.Id, StringComparison.Ordinal);
        }

        public override bool Equals(object obj)
        {
            return obj is LevelCanvasSelection other && Equals(other);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine((int)Kind, Index, Id);
        }
    }
}
