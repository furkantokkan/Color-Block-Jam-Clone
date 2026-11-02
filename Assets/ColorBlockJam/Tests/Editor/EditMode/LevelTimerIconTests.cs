using System.Reflection;
using ColorBlockJam.Editor.Levels;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace ColorBlockJam.Tests.EditMode
{
    /// <summary>
    /// Verifies the stopwatch beside the Level Editor's Timer field: a 64 x 64 white-on-alpha icon imported like the
    /// other tool icons, drawn inside the Timer field before its label, without changing the top bar's order.
    /// </summary>
    public sealed class LevelTimerIconTests
    {
        [Test]
        public void TimerIconIsImportedLikeTheToolIcons()
        {
            string path = LevelEditorIcons.k_folder + LevelEditorIcons.k_timer + ".png";
            Texture2D icon = LevelEditorIcons.Load(LevelEditorIcons.k_timer);
            TextureImporter importer = (TextureImporter)AssetImporter.GetAtPath(path);
            TextureImporter pen = (TextureImporter)AssetImporter.GetAtPath(LevelEditorIcons.k_folder + LevelEditorIcons.k_pen + ".png");

            Assert.That(icon, Is.Not.Null, path);
            Assert.That(icon.width, Is.EqualTo(64));
            Assert.That(icon.height, Is.EqualTo(64));
            Assert.That(importer.textureType, Is.EqualTo(pen.textureType));
            Assert.That(importer.mipmapEnabled, Is.EqualTo(pen.mipmapEnabled));
            Assert.That(importer.alphaIsTransparency, Is.True);
            Assert.That(importer.wrapMode, Is.EqualTo(pen.wrapMode));
            Assert.That(importer.GetDefaultPlatformTextureSettings().maxTextureSize,
                Is.EqualTo(pen.GetDefaultPlatformTextureSettings().maxTextureSize));
        }

        [Test]
        public void StopwatchSitsInsideTheTimerFieldBeforeItsLabel()
        {
            LevelDatabaseWindow window = ScriptableObject.CreateInstance<LevelDatabaseWindow>();
            try
            {
                window.ShowUtility();
                window.position = new Rect(40f, 40f, 960f, 640f);
                // Tests call CreateGUI directly, as the other editor window tests do.
                LevelEditorTestAccess.Invoke(window, "CreateGUI", null);
                VisualElement root = window.rootVisualElement;
                IntegerField timer = root.Q<IntegerField>("timer-seconds");
                Image stopwatch = timer.Q<Image>("timer-icon");

                Assert.That(stopwatch, Is.Not.Null, "The stopwatch is inside the Timer field.");
                Assert.That(timer.IndexOf(stopwatch), Is.Zero, "It comes before the Timer label.");
                Assert.That(stopwatch.image, Is.SameAs(LevelEditorIcons.Load(LevelEditorIcons.k_timer)));
                Assert.That(stopwatch.pickingMode, Is.EqualTo(PickingMode.Ignore), "Clicks still reach the field.");
                Assert.That(timer.label, Is.EqualTo("Timer"));

                // The top bar order stays Level Name, Timer, m:ss.
                VisualElement row = timer.parent;
                Assert.That(row.IndexOf(timer), Is.EqualTo(row.IndexOf(root.Q("level-name")) + 1));
                Assert.That(row.IndexOf(root.Q("timer-text")), Is.EqualTo(row.IndexOf(timer) + 1));
            }
            finally
            {
                window.Close();
            }
        }
    }
}
