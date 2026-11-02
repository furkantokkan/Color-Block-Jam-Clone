using ColorBlockJam.Runtime.Art;
using Onity.Pooling;
using UnityEngine;

namespace ColorBlockJam.Presentation.Board
{
    /// <summary>
    /// One pooled board tile. Its owner resolves the colour and sets it with SetColor, the only colour
    /// write; SetClipPlane (exit and effect clipping) and SetIceValues are the only other MaterialPropertyBlock
    /// writes, and a pool release makes none. SetIce adds the shared Ice material as a second material on the
    /// tile's own renderers, so a frozen block needs no second object (DEC-184). The view never reads a palette
    /// (PROJECT.md Performance Rule 1).
    /// </summary>
    public sealed class BoardPieceView : MonoBehaviour, IPoolHooks
    {
        [SerializeField] private MeshFilter m_filter;
        [SerializeField] private MeshRenderer m_renderer;
        private MaterialPropertyBlock m_properties;
        private MeshRenderer[] m_renderers;
        // Per renderer: its own materials, and the same plus one Ice slot, cached so SetIce allocates nothing.
        private Material[][] m_baseMaterials;
        private Material[][] m_iceMaterials;
        // Every owner sets the colour after checkout, so no default cell is ever shown.
        private AtlasColor m_color;
        private Vector4 m_clipPlane;
        private bool m_clipEnabled;
        private bool m_iced;

        public MeshRenderer Renderer => m_renderer;
        public MeshFilter Filter => m_filter;
        public bool IsIced => m_iced;

        /// <summary>Moves the tile's UV onto color's atlas cell, so the atlas alone gives the colour (DEC-202).</summary>
        public void SetColor(AtlasColor color)
        {
            m_color = color;
            ApplyState();
        }

        public void SetClipPlane(Vector4 plane, bool enabled)
        {
            m_clipPlane = plane;
            m_clipEnabled = enabled;
            ApplyState();
        }

        /// <summary>
        /// Adds material as a second material on every renderer of the tile, or removes it for null. The Ice pass
        /// reads the tile's own clip plane, so exit and effect clipping cover the ice too.
        /// </summary>
        public void SetIce(Material material)
        {
            if (m_renderer == null || m_iced == (material != null)) return;
            CreateCache();
            m_iced = material != null;
            for (int i = 0; i < m_renderers.Length; i++)
            {
                Material[] iced = m_iceMaterials[i];
                iced[iced.Length - 1] = material;
                m_renderers[i].sharedMaterials = m_iced ? iced : m_baseMaterials[i];
            }
        }

        /// <summary>Writes the Ice pass's tint, opacity and crack amount into the tile's property block.</summary>
        public void SetIceValues(Color tint, float opacity, float cracks)
        {
            if (m_renderer == null) return;
            CreateCache();
            m_properties.SetColor(IceShader.TintId, tint);
            m_properties.SetFloat(IceShader.OpacityId, opacity);
            m_properties.SetFloat(IceShader.CracksId, cracks);
            ApplyState();
        }

        /// <summary>
        /// Creates the property block, renderer cache and material arrays without a GPU write. Pool owners call it
        /// while preparing, outside Playing, so a tile first shown by a Rocket recompose allocates nothing.
        /// </summary>
        public void CreateCache()
        {
            if (m_properties == null) m_properties = new MaterialPropertyBlock();
            if (m_renderers != null) return;
            m_renderers = GetComponentsInChildren<MeshRenderer>(true);
            m_baseMaterials = new Material[m_renderers.Length][];
            m_iceMaterials = new Material[m_renderers.Length][];
            for (int i = 0; i < m_renderers.Length; i++)
            {
                Material[] own = m_renderers[i].sharedMaterials;
                m_baseMaterials[i] = own;
                m_iceMaterials[i] = new Material[own.Length + 1];
                own.CopyTo(m_iceMaterials[i], 0);
            }
        }

        private void ApplyState()
        {
            if (m_renderer == null) return;
            // Owners that never prepared the tile (environment and editor previews) cache on first write.
            CreateCache();
            m_properties.SetVector(BoardLitShader.PaletteUVId, m_color.UV);
            m_properties.SetVector(BoardLitShader.ClipPlaneId, m_clipPlane);
            m_properties.SetFloat(BoardLitShader.ClipEnabledId, m_clipEnabled ? 1f : 0f);
            for (int i = 0; i < m_renderers.Length; i++) m_renderers[i].SetPropertyBlock(m_properties);
        }

        public void OnPoolGet() { }

        // A released tile is hidden, and every owner sets its colour after the next Get, which writes
        // the cleared clip state too; release therefore makes no property-block write. A frozen block's tile
        // goes back to its own materials, so the next block that takes it is not iced.
        public void OnPoolRelease()
        {
            m_clipPlane = Vector4.zero;
            m_clipEnabled = false;
            SetIce(null);
        }
    }
}
