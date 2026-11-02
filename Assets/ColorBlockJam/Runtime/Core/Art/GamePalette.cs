using System;
using System.Collections.Generic;
using UnityEngine;

namespace ColorBlockJam.Runtime.Art
{
    /// <summary>
    /// Names the cells of the shared colour atlas (PaletteAtlas, DEC-202): each PaletteColor's cell and its light
    /// door-arrow cell and the arrow cell. The atlas texture holds every colour; this asset only
    /// says where each one is. Board tiles take an AtlasColor; what cannot sample the atlas (booster particles, editor
    /// canvases, the Blender export) reads the cell's pixel from the readable atlas.
    /// </summary>
    [CreateAssetMenu(menuName = "Color Block Jam/Art/Game Palette")]
    public sealed class GamePalette : ScriptableObject
    {
        [Serializable]
        public struct Entry
        {
            [SerializeField] private PaletteColor m_type;
            [SerializeField] private Vector2Int m_cell;
            // The same colour blended toward white, as door arrows show it (references 02, 17).
            [SerializeField] private Vector2Int m_lightCell;
            public PaletteColor Type => m_type;
            public Vector2Int Cell => m_cell;
            public Vector2Int LightCell => m_lightCell;
        }

        [SerializeField] private Texture2D m_atlas;
        [SerializeField] private Entry[] m_colors = Array.Empty<Entry>();
        [SerializeField] private Vector2Int m_arrowCell = new Vector2Int(13, 2);

        public Texture2D Atlas => m_atlas;
        public IReadOnlyList<Entry> Colors => m_colors;
        public Vector2Int ArrowCell => m_arrowCell;

        public AtlasColor GetAtlasColor(PaletteColor type) => AtlasColor.FromCell(GetEntry(type).Cell);
        public AtlasColor GetLightAtlasColor(PaletteColor type) => AtlasColor.FromCell(GetEntry(type).LightCell);
        public AtlasColor ArrowAtlasColor => AtlasColor.FromCell(m_arrowCell);

        /// <summary>The colour of type's cell, read from the atlas.</summary>
        public Color GetColor(PaletteColor type) => GetCellColor(GetEntry(type).Cell);

        public Color ArrowColor => GetCellColor(m_arrowCell);

        /// <summary>The pixel at the centre of cell; the atlas must import readable. Allocates nothing.</summary>
        public Color GetCellColor(Vector2Int cell)
        {
            if (m_atlas == null) throw new InvalidOperationException("The palette has no atlas.");
            Vector2Int pixel = PaletteAtlas.GetCellCenterPixel(cell);
            return m_atlas.GetPixel(pixel.x, pixel.y);
        }

        /// <summary>Checks the atlas, one entry per PaletteColor and every cell, in designer words.</summary>
        public bool Validate(out string error)
        {
            if (m_atlas == null) { error = "The palette needs its colour atlas."; return false; }
            if (!m_atlas.isReadable) { error = "The colour atlas must import with Read/Write enabled."; return false; }
            if (m_atlas.width != PaletteAtlas.Width || m_atlas.height != PaletteAtlas.Height)
            {
                error = "The colour atlas must be " + PaletteAtlas.Width + " x " + PaletteAtlas.Height + " pixels.";
                return false;
            }

            foreach (PaletteColor type in (PaletteColor[])Enum.GetValues(typeof(PaletteColor)))
            {
                if (!TryGetEntry(type, out Entry entry)) { error = "The palette has no cell for " + type + "."; return false; }
                if (!PaletteAtlas.Contains(entry.Cell) || !PaletteAtlas.Contains(entry.LightCell))
                {
                    error = type + " points outside the colour atlas.";
                    return false;
                }
            }

            if (!PaletteAtlas.Contains(m_arrowCell)) { error = "The arrow cell is outside the colour atlas."; return false; }
            error = null;
            return true;
        }

        private Entry GetEntry(PaletteColor type)
        {
            if (TryGetEntry(type, out Entry entry)) return entry;
            throw new ArgumentException("Unknown palette color.", nameof(type));
        }

        private bool TryGetEntry(PaletteColor type, out Entry entry)
        {
            for (int i = 0; i < m_colors.Length; i++)
            {
                if (m_colors[i].Type == type)
                {
                    entry = m_colors[i];
                    return true;
                }
            }

            entry = default;
            return false;
        }
    }
}
