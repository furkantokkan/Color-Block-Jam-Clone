using System;
using ColorBlockJam.Editor.BoardKit;
using ColorBlockJam.Editor.LevelPreview;
using ColorBlockJam.Environment;
using ColorBlockJam.Presentation.Board;
using ColorBlockJam.Runtime.Art;
using ColorBlockJam.Runtime.Board;
using ColorBlockJam.Runtime.Levels;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

namespace ColorBlockJam.Tests.LevelPreview
{
    /// <summary>
    /// Verifies the LevelEditor scene preview's Ice overlay (IcePreviewEffect): an Ice block's shell shows and
    /// returns to its pool alongside an Arrow marker in the same host, and a rebuild that drops Ice removes its
    /// UI Toolkit fill document while dispose leaves nothing behind.
    /// </summary>
    public sealed class PreviewIceTests
    {
        [Test]
        public void IceAndArrowCoexistAndRebuildReturnsAllTemporaryViews()
        {
            BoardArt art = LevelPreviewTestSupport.CreateBoardArt();
            LevelPreviewHost host = LevelPreviewTestSupport.CreateHost(SceneManager.GetActiveScene(), art,
                () => new ILevelPreviewEffect[] { new IcePreviewEffect(
                    AssetDatabase.LoadAssetAtPath<Material>(BoardIceAssets.MaterialPath),
                    AssetDatabase.LoadAssetAtPath<PanelSettings>(BoardIceAssets.PanelPath)), new ArrowPreviewEffect() });
            try
            {
                Assert.That(host.TryShow(Level(true), out string error), Is.True, error);
                Assert.That(host.GetEffect<ArrowPreviewEffect>().ActiveMarkers.Count, Is.EqualTo(1));
                // The ice is the Ice material on the block's own tiles (DEC-184); no extra object joins the scene.
                Transform root = host.ActiveBlocks[0].transform;
                int children = root.childCount;
                Assert.That(host.ActiveBlocks[0].View.IsIced, Is.True);
                Assert.That(root.childCount, Is.EqualTo(children));
                Assert.That(host.TryShow(Level(false), out error), Is.True, error);
                Assert.That(host.ActiveBlocks[0].View.IsIced, Is.False);
                Assert.That(host.Root.GetComponentsInChildren<UIDocument>(true).Length, Is.Zero);
                Assert.That(host.TryShow(Level(true), out error), Is.True, error);
                Assert.That(host.Root.GetComponentsInChildren<UIDocument>(true).Length, Is.EqualTo(1));
            }
            finally { host.Dispose(); }
            Assert.That(host.Root, Is.Null);
        }

        private static LevelData Level(bool ice)
        {
            LevelEffectData[] effects = ice ? new[]
            {
                new LevelEffectData("ice", new[] { new LevelEffectParamData("clearsToThaw", "2") }),
                new LevelEffectData("arrow", new[] { new LevelEffectParamData("axis", "Horizontal") })
            } : Array.Empty<LevelEffectData>();
            return new LevelData("preview-ice", "Ice", 4, 4, 60, Array.Empty<LevelDoorData>(), new[]
            {
                new LevelBlockData("b1", "Red", Vector2Int.zero,
                    new[] { Vector2Int.zero, Vector2Int.up, Vector2Int.right }, effects)
            });
        }
    }
}
