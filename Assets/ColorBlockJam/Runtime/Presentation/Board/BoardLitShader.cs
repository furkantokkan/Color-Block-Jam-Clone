using UnityEngine;

namespace ColorBlockJam.Presentation.Board
{
    /// <summary>
    /// The Board Lit shader's property-block contract on the shared board material (PROJECT.md Performance Rule 1,
    /// DEC-123): its property IDs. A tile's colour is the atlas cell its _PaletteUV selects (AtlasColor, DEC-202).
    /// </summary>
    internal static class BoardLitShader
    {
        public static readonly int PaletteUVId = Shader.PropertyToID("_PaletteUV");
        public static readonly int ClipPlaneId = Shader.PropertyToID("_ClipPlane");
        public static readonly int ClipEnabledId = Shader.PropertyToID("_ClipEnabled");
    }
}
