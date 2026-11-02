using ColorBlockJam.Runtime.Levels;

namespace ColorBlockJam.Editor.Levels.Validation
{
    /// <summary>Names one block of a level in designer words, for example "the Pink Square block at (1,1)".</summary>
    public delegate string LevelBlockDescriber(LevelData level, int blockIndex);
}
