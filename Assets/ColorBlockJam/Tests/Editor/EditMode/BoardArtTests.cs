using System;
using ColorBlockJam.Editor.BoardKit;
using ColorBlockJam.Presentation.Board;
using ColorBlockJam.Runtime.Art;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace ColorBlockJam.Tests.EditMode
{
    /// <summary>BoardArt resolves colours from its palette and refuses incomplete art at scope build (DEC-123).</summary>
    public sealed class BoardArtTests
    {
        private GamePalette m_palette;
        private BoardPieceCatalog m_catalog;

        [SetUp]
        public void LoadArt()
        {
            m_palette = AssetDatabase.LoadAssetAtPath<GamePalette>(BoardKitAssets.PalettePath);
            m_catalog = AssetDatabase.LoadAssetAtPath<BoardPieceCatalog>(BoardKitAssets.CatalogPath);
        }

        [Test]
        public void ProjectArtValidatesAndResolvesPaletteColours()
        {
            var art = new BoardArt(m_palette, m_catalog);

            Assert.That(art.Validate(out string error), Is.True, error);
            Assert.That(art.GetColor(PaletteColor.LightBlue), Is.EqualTo(m_palette.GetColor(PaletteColor.LightBlue)));
            Assert.That(art.ArrowColor, Is.EqualTo(m_palette.ArrowColor));
        }

        [Test]
        public void MissingAssetsAreRefusedByTheConstructor()
        {
            Assert.Throws<ArgumentNullException>(() => new BoardArt(null, m_catalog));
            Assert.Throws<ArgumentNullException>(() => new BoardArt(m_palette, null));
        }

        [Test]
        public void IncompleteCatalogFailsValidationWithAReason()
        {
            var emptyCatalog = ScriptableObject.CreateInstance<BoardPieceCatalog>();
            try
            {
                Assert.That(new BoardArt(m_palette, emptyCatalog).Validate(out string catalogError), Is.False);
                Assert.That(catalogError, Does.Contain("Board art is incomplete"));
            }
            finally
            {
                Object.DestroyImmediate(emptyCatalog);
            }
        }
    }
}
