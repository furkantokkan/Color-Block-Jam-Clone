using UnityEngine;

namespace ColorBlockJam.Presentation.Board
{
    /// <summary>
    /// The Ice Shader Graph's property-block contract (DEC-184): the IDs a frozen block's tiles write next to their
    /// Board Lit values, since the Ice pass renders the same tile renderer as a second material.
    /// </summary>
    internal static class IceShader
    {
        public static readonly int TintId = Shader.PropertyToID("_IceTint");
        public static readonly int OpacityId = Shader.PropertyToID("_IceOpacity");
        public static readonly int CracksId = Shader.PropertyToID("_IceCracks");
    }
}
