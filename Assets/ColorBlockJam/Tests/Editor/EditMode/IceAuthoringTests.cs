using ColorBlockJam.Runtime.Art;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Text.RegularExpressions;
using ColorBlockJam.Editor.BoardKit;
using ColorBlockJam.Presentation.Board;
using ColorBlockJam.Runtime.Levels;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace ColorBlockJam.Tests.EditMode
{
    /// <summary>Verifies Ice's authored material tint/opacity, the ShaderGraph scalar node and the runtime save gate.</summary>
    public sealed class IceAuthoringTests
    {
        [Test]
        public void IceLookUsesAuthoredMaterialTintAndOpacityOnTheBlocksOwnTiles()
        {
            var material = AssetDatabase.LoadAssetAtPath<Material>(BoardIceAssets.MaterialPath);
            var catalog = AssetDatabase.LoadAssetAtPath<BoardPieceCatalog>(BoardKitAssets.CatalogPath);
            var storage = new GameObject("Ice material defaults fixture");
            try
            {
                ModularBlockView view = storage.AddComponent<ModularBlockView>();
                view.Prepare(catalog, 1);
                view.SetCells(new[] { Vector2Int.zero }, AssetDatabase.LoadAssetAtPath<GamePalette>("Assets/ColorBlockJam/Data/Art/GamePalette.asset").GetAtlasColor(PaletteColor.Red));
                using var shells = new BlockIceOverlays(material, 1);
                shells.Attach(0, view, 1, 1);
                MeshRenderer renderer = view.GetShownPiece(0).Renderer;
                Assert.That(renderer.sharedMaterials, Has.Length.EqualTo(2), "Ice is a second material, not a second object (DEC-184).");
                Assert.That(renderer.sharedMaterials[1], Is.SameAs(material));
                var properties = new MaterialPropertyBlock();
                renderer.GetPropertyBlock(properties);
                Color actualTint = properties.GetColor("_IceTint");
                Color expectedTint = material.GetColor("_IceTint");
                for (int channel = 0; channel < 4; channel++)
                    Assert.That(actualTint[channel], Is.EqualTo(expectedTint[channel]).Within(.000001f),
                        "authored Ice tint channel " + channel);
                Assert.That(properties.GetFloat("_IceOpacity"), Is.EqualTo(material.GetFloat("_IceOpacity")));
                Assert.That(properties.GetFloat("_IceOpacity"), Is.InRange(.98f, .99f),
                    "steady-state Ice has dense coverage before the owned thaw fade");
                Assert.That(material.GetTexture("_IceTexture"), Is.SameAs(AssetDatabase.LoadAssetAtPath<Texture2D>(
                    "Assets/ColorBlockJam/Art/Textures/icetexture.jpg")));
                Assert.That(ShaderUtil.ShaderHasError(material.shader), Is.False);
                shells.Detach(0);
                Assert.That(renderer.sharedMaterials, Has.Length.EqualTo(1), "Thawing removes the Ice material again.");
                view.Release();
            }
            finally { UnityEngine.Object.DestroyImmediate(storage); }
        }

        [TestCase(0f)]
        [TestCase(.2f)]
        [TestCase(.65f)]
        [TestCase(.98f)]
        [TestCase(1f)]
        public void IceGraphScalarGeneratesRequestedShaderValue(float value)
        {
            const BindingFlags flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
            Type graphType = typeof(BoardIceAssets).GetNestedType("Graph", BindingFlags.NonPublic);
            object graph = Activator.CreateInstance(graphType, true);
            object node = graphType.GetMethod("Number", flags).Invoke(graph, new object[] { value });
            Assembly assembly = node.GetType().Assembly;
            Type builderType = assembly.GetType("UnityEditor.ShaderGraph.ShaderStringBuilder", true);
            object builder = Activator.CreateInstance(builderType, new object[] { 0, 256, false });
            try
            {
                Type modeType = assembly.GetType("UnityEditor.ShaderGraph.GenerationMode", true);
                node.GetType().GetMethod("GenerateNodeCode", flags).Invoke(node,
                    new[] { builder, Enum.Parse(modeType, "ForReals") });
                string code = builder.ToString();
                Match scalar = Regex.Match(code, @"=\s*\$precision\(([^)]+)\)");
                Assert.That(scalar.Success, Is.True, code);
                Assert.That(float.Parse(scalar.Groups[1].Value, CultureInfo.InvariantCulture),
                    Is.EqualTo(value).Within(.000001f), code);
            }
            finally { ((IDisposable)builder).Dispose(); }
        }

        [Test]
        public void DisposableSavedFixtureReopensWithIceCountAndRuntimeSupportWhileCrateRemainsGated()
        {
            string path = Path.Combine(Path.GetTempPath(), "ice-fixture-" + Guid.NewGuid().ToString("N") + ".json");
            try
            {
                var level = new LevelData("ice-roundtrip", "Ice", 3, 3, 60, Array.Empty<LevelDoorData>(), new[]
                {
                    new LevelBlockData("b", "Red", Vector2Int.zero, new[] { Vector2Int.zero },
                        new[] { new LevelEffectData("ice", new[] { new LevelEffectParamData("clearsToThaw", "20") }) })
                });
                Assert.That(LevelDatabaseJson.TryWrite(new[] { level }, out string json, out string error), Is.True, error);
                File.WriteAllText(path, json);
                Assert.That(LevelDatabaseJson.TryRead(File.ReadAllText(path), LevelReadMode.Runtime, out LevelData[] reopened, out error), Is.True, error);
                Assert.That(reopened[0].GetBlockAt(0).TryGetEffect(out IceEffect ice), Is.True);
                Assert.That(ice.ClearsToThaw, Is.EqualTo(20));
                Assert.That(LevelEffectRegistry.TryFind("crate", out LevelEffectDefinition crate), Is.True);
                Assert.That(crate.IsRuntimeImplemented, Is.False);
            }
            finally { if (File.Exists(path)) File.Delete(path); }
        }
    }
}
