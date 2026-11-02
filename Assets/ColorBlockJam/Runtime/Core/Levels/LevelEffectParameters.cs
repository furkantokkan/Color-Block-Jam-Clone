using ColorBlockJam.Runtime.Art;

namespace ColorBlockJam.Runtime.Levels
{
    // Typed, immutable parameters of each catalog effect (docs/Color-Block-Jam-PRD-Mechanics-Catalog.md).
    // LevelEffectRegistry converts the authored string pairs into these objects; gameplay reads only these.
    // Only the Arrow and Ice block effects exist; doors carry no effect type (DEC-177).

    public enum LevelEffectAxis
    {
        Horizontal = 0,
        Vertical = 1
    }

    /// <summary>Base of every typed effect parameter object; subclasses are immutable.</summary>
    public abstract class LevelEffectParameters
    {
    }

    /// <summary>BLK-01 Arrow: the block moves and exits only along its axis.</summary>
    public sealed class ArrowEffect : LevelEffectParameters
    {
        public ArrowEffect(LevelEffectAxis axis) { Axis = axis; }
        public LevelEffectAxis Axis { get; }
    }

    /// <summary>BLK-03 Ice: the block is frozen until this many other blocks exit.</summary>
    public sealed class IceEffect : LevelEffectParameters
    {
        public IceEffect(int clearsToThaw) { ClearsToThaw = clearsToThaw; }
        public int ClearsToThaw { get; }
    }

    /// <summary>OBJ-01 Colorful Path: cells only one colour may enter.</summary>
    public sealed class ColorfulPathObject : LevelEffectParameters
    {
        public ColorfulPathObject(PaletteColor color) { Color = color; }
        public PaletteColor Color { get; }
    }

    /// <summary>OBJ-02 Crate: wall cells that open after this many clears and reveal contents.</summary>
    public sealed class CrateObject : LevelEffectParameters
    {
        public CrateObject(int clearsToOpen) { ClearsToOpen = clearsToOpen; }
        public int ClearsToOpen { get; }
    }

    /// <summary>OBJ-03 Moveable Crate: a movable crate that opens after this many gate passes.</summary>
    public sealed class MoveableCrateObject : LevelEffectParameters
    {
        public MoveableCrateObject(int gatePassesToOpen) { GatePassesToOpen = gatePassesToOpen; }
        public int GatePassesToOpen { get; }
    }

    /// <summary>OBJ-04 Flower Crate: a movable crate that opens when every leaf block has cleared.</summary>
    public sealed class FlowerCrateObject : LevelEffectParameters
    {
        private readonly string[] m_leafIds;

        public FlowerCrateObject(string[] leafIds) { m_leafIds = (string[])leafIds.Clone(); }
        public int LeafCount => m_leafIds.Length;
        public string GetLeafIdAt(int index) { return m_leafIds[index]; }
    }

    /// <summary>OBJ-05 Barrier: wall cells when closed, floor when open; flips on each clear.</summary>
    public sealed class BarrierObject : LevelEffectParameters
    {
        public BarrierObject(bool startsOpen) { StartsOpen = startsOpen; }
        public bool StartsOpen { get; }
    }

    /// <summary>OBJ-06 Colorful Crate: wall cells that vanish when no block of the colour remains.</summary>
    public sealed class ColorfulCrateObject : LevelEffectParameters
    {
        public ColorfulCrateObject(PaletteColor color) { Color = color; }
        public PaletteColor Color { get; }
    }

    /// <summary>OBJ-07 Colorful Moving Path: a straight track with one colour platform cell.</summary>
    public sealed class ColorfulMovingPathObject : LevelEffectParameters
    {
        public ColorfulMovingPathObject(PaletteColor color, int platformIndex)
        {
            Color = color;
            PlatformIndex = platformIndex;
        }

        public PaletteColor Color { get; }
        public int PlatformIndex { get; }
    }

    /// <summary>OBJ-08 Ivy: wall cells that retract one cell per gate pass, last listed first.</summary>
    public sealed class IvyObject : LevelEffectParameters
    {
    }

    /// <summary>OBJ-09 Elevator: floor cells that raise stacked blocks one at a time.</summary>
    public sealed class ElevatorObject : LevelEffectParameters
    {
    }
}
