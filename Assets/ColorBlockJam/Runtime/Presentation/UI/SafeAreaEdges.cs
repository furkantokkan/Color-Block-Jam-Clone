using System;

namespace ColorBlockJam.Presentation.UI
{
    /// <summary>
    /// The screen edges one <see cref="SafeAreaPadding"/> target pads, so a bottom bar can pad only its bottom
    /// while the content above it pads the top (Hub-And-Player-Data-Design 6.5).
    /// </summary>
    [Flags]
    public enum SafeAreaEdges
    {
        None = 0,
        Left = 1,
        Top = 2,
        Right = 4,
        Bottom = 8,
        All = Left | Top | Right | Bottom
    }
}
