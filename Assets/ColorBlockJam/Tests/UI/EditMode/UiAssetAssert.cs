using System.IO;
using System.Reflection;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace ColorBlockJam.Tests.SharedUI
{
    /// <summary>
    /// Holds the popup sprites' measured import borders in one table and the USS-slice and assembly-boundary
    /// assertions the shared popup asset tests reuse, so a re-measured border or a third popup changes one place.
    /// </summary>
    internal static class UiAssetAssert
    {
        // Measured borders from docs/UI-Asset-Import.md, Unity order L, B, R, T (Sprite.border / spriteBorder).
        public static readonly Vector4 BgPopupBorder = new Vector4(106, 104, 107, 74);
        public static readonly Vector4 BgPopupHeaderBorder = new Vector4(79, 90, 79, 90);
        public static readonly Vector4 BgPopupInfoHolderBorder = new Vector4(43, 35, 42, 26);
        public static readonly Vector4 BtnPopupBlueBorder = new Vector4(46, 65, 47, 69);
        public static readonly Vector4 BtnPopupGreenBorder = new Vector4(69, 128, 70, 129);

        // The union of assembly-name prefixes ColorBlockJam.UI must never reference: no Hub or Gameplay scene
        // composition, no Presentation or Environment board art, and no Onity.Unity scene-flow or context binding.
        private static readonly string[] s_forbiddenReferencePrefixes =
        {
            "ColorBlockJam.Hub", "ColorBlockJam.Gameplay", "ColorBlockJam.Presentation", "ColorBlockJam.Environment",
            "Onity.Unity"
        };

        /// <summary>Asserts the sprite's measured import border and that every USS rule drawing it slices by it.</summary>
        public static void AssertUssSlicesMatchBorder(string spritePath, string ussPath, Vector4 expected)
        {
            var importer = (TextureImporter)AssetImporter.GetAtPath(spritePath);
            Assert.That(importer, Is.Not.Null, spritePath);
            Assert.That(importer.spriteBorder, Is.EqualTo(expected), spritePath + " spriteBorder (L, B, R, T)");

            string uss = File.ReadAllText(ussPath);
            string fileName = Path.GetFileName(spritePath);
            int rules = 0;
            foreach (Match rule in Regex.Matches(uss, @"\{([^{}]*)\}"))
            {
                string body = rule.Groups[1].Value;
                if (!body.Contains("/" + fileName + "\""))
                {
                    continue;
                }

                rules++;
                Assert.That(Slice(body, "left"), Is.EqualTo((int)expected.x), fileName + " slice left");
                Assert.That(Slice(body, "top"), Is.EqualTo((int)expected.w), fileName + " slice top");
                Assert.That(Slice(body, "right"), Is.EqualTo((int)expected.z), fileName + " slice right");
                Assert.That(Slice(body, "bottom"), Is.EqualTo((int)expected.y), fileName + " slice bottom");
            }

            Assert.That(rules, Is.GreaterThan(0), fileName + " is used by " + Path.GetFileName(ussPath));
        }

        /// <summary>Asserts none of the assembly's references starts with a scene, composition or board-art prefix.</summary>
        public static void AssertNoSceneOrCompositionReference(Assembly assembly)
        {
            foreach (AssemblyName referenced in assembly.GetReferencedAssemblies())
            {
                for (int index = 0; index < s_forbiddenReferencePrefixes.Length; index++)
                {
                    Assert.That(referenced.Name, Does.Not.StartWith(s_forbiddenReferencePrefixes[index]));
                }
            }
        }

        private static int Slice(string body, string side)
        {
            Match match = Regex.Match(body, @"-unity-slice-" + side + @"\s*:\s*(\d+)");
            Assert.That(match.Success, Is.True, "-unity-slice-" + side);
            return int.Parse(match.Groups[1].Value);
        }
    }
}
