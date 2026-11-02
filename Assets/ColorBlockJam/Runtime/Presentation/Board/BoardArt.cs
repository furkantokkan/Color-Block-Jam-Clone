using System;
using ColorBlockJam.Runtime.Art;
using ColorBlockJam.Runtime.Board;
using UnityEngine;

namespace ColorBlockJam.Presentation.Board
{
    /// <summary>
    /// The shared board art (palette and piece catalog, whose bare block root every block is built on, DEC-193) that
    /// one owner hands to the block factory, so prefabs and views hold no ScriptableObject references and receive
    /// resolved colours. The Game scene installer binds one instance; editor tools build theirs from asset loads.
    /// </summary>
    public sealed class BoardArt
    {
        public BoardArt(GamePalette palette, BoardPieceCatalog catalog)
        {
            Palette = palette != null ? palette : throw new ArgumentNullException(nameof(palette));
            Catalog = catalog != null ? catalog : throw new ArgumentNullException(nameof(catalog));
        }

        public GamePalette Palette { get; }
        public BoardPieceCatalog Catalog { get; }
        /// <summary>The arrow markers' atlas cell (DEC-202).</summary>
        public AtlasColor ArrowColor => Palette.ArrowAtlasColor;

        /// <summary>The atlas cell board tiles sample for color (DEC-202).</summary>
        public AtlasColor GetAtlasColor(PaletteColor color) => Palette.GetAtlasColor(color);

        /// <summary>The atlas pixel of color, for what cannot sample the atlas (booster particles).</summary>
        public Color GetColor(PaletteColor color) => Palette.GetColor(color);

        /// <summary>
        /// Checks every palette colour, every piece kind and the catalog's block root, so a broken asset fails at
        /// scope build instead of during an attempt.
        /// </summary>
        public bool Validate(out string error)
        {
            if (!Palette.Validate(out string paletteError))
            {
                error = "Board art is incomplete: " + paletteError;
                return false;
            }

            try
            {
                for (int kind = 0; kind < BlockPieceCapacity.KindCount; kind++)
                {
                    Catalog.Get((BlockPieceKind)kind);
                }
            }
            catch (Exception exception) when (exception is ArgumentException || exception is InvalidOperationException)
            {
                error = "Board art is incomplete: " + exception.Message;
                return false;
            }

            if (!BlockPieceFactory.TryGetRoot(Catalog, out _, out string rootError))
            {
                error = "Board art is incomplete: " + rootError;
                return false;
            }

            error = null;
            return true;
        }
    }
}
