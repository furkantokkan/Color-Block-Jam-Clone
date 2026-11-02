using System.IO;
using ColorBlockJam.Editor.Levels;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;

namespace ColorBlockJam.Tests.EditMode
{
    /// <summary>
    /// Verifies the Level Editor's tool icons: seven 64 x 64 white-on-alpha PNGs with the recorded Editor GUI import
    /// settings, one badge icon per tool, and a missing icon that loads as null without a Console message.
    /// </summary>
    public sealed class LevelEditorIconsTests
    {
        private static readonly string[] s_iconNames =
        {
            LevelEditorIcons.k_cursor, LevelEditorIcons.k_pen, LevelEditorIcons.k_eraser,
            LevelEditorIcons.k_rotateLeft, LevelEditorIcons.k_rotateRight, LevelEditorIcons.k_reflectHorizontal,
            LevelEditorIcons.k_reflectVertical
        };

        [Test]
        public void EveryToolIconIsAWhiteSixtyFourPixelEditorGuiTexture()
        {
            foreach (string iconName in s_iconNames)
            {
                string path = LevelEditorIcons.k_folder + iconName + ".png";
                Texture2D icon = LevelEditorIcons.Load(iconName);
                Assert.That(icon, Is.Not.Null, path);
                Assert.That(icon.width, Is.EqualTo(64), path);
                Assert.That(icon.height, Is.EqualTo(64), path);

                TextureImporter importer = (TextureImporter)AssetImporter.GetAtPath(path);
                Assert.That(importer.textureType, Is.EqualTo(TextureImporterType.GUI), path);
                Assert.That(importer.alphaIsTransparency, Is.True, path);
                Assert.That(importer.mipmapEnabled, Is.False, path);
                Assert.That(importer.wrapMode, Is.EqualTo(TextureWrapMode.Clamp), path);
                Assert.That(importer.filterMode, Is.EqualTo(FilterMode.Bilinear), path);
                Assert.That(importer.maxTextureSize, Is.EqualTo(64), path);
                Assert.That(importer.textureCompression, Is.EqualTo(TextureImporterCompression.Uncompressed), path);

                // The imported texture is not readable, so the source file is decoded to check the stroke colour.
                Texture2D source = new Texture2D(2, 2);
                try
                {
                    Assert.That(source.LoadImage(File.ReadAllBytes(path)), Is.True, path);
                    Color32[] pixels = source.GetPixels32();
                    int visible = 0;
                    foreach (Color32 pixel in pixels)
                    {
                        if (pixel.a == 0)
                        {
                            continue;
                        }

                        visible++;
                        Assert.That(pixel.r == 255 && pixel.g == 255 && pixel.b == 255, Is.True,
                            $"{path}: every visible pixel is white so a tint can colour it.");
                    }

                    Assert.That(visible, Is.GreaterThan(0), $"{path} has a visible stroke.");
                }
                finally
                {
                    Object.DestroyImmediate(source);
                }
            }
        }

        [Test]
        public void EveryToolShowsItsOwnButtonIconInTheCanvasBadge()
        {
            var expected = new System.Collections.Generic.Dictionary<LevelEditorTool, string>
            {
                { LevelEditorTool.Select, LevelEditorIcons.k_cursor },
                { LevelEditorTool.Pen, LevelEditorIcons.k_pen },
                { LevelEditorTool.Eraser, LevelEditorIcons.k_eraser },
                { LevelEditorTool.RotateCounterClockwise, LevelEditorIcons.k_rotateLeft },
                { LevelEditorTool.RotateClockwise, LevelEditorIcons.k_rotateRight },
                { LevelEditorTool.MirrorHorizontal, LevelEditorIcons.k_reflectHorizontal },
                { LevelEditorTool.MirrorVertical, LevelEditorIcons.k_reflectVertical }
            };

            Assert.That(expected.Count, Is.EqualTo(System.Enum.GetValues(typeof(LevelEditorTool)).Length),
                "Every tool is listed.");
            foreach (var pair in expected)
            {
                Assert.That(LevelEditorIcons.GetToolIconName(pair.Key), Is.EqualTo(pair.Value), pair.Key.ToString());
                Assert.That((int)pair.Key, Is.LessThan(expected.Count), "Tool values stay contiguous for the icon cache.");
            }
        }

        [Test]
        public void AMissingIconLoadsAsNullWithoutAConsoleMessage()
        {
            Assert.That(LevelEditorIcons.Load("missing-level-editor-icon"), Is.Null);
            LogAssert.NoUnexpectedReceived();
        }
    }
}
