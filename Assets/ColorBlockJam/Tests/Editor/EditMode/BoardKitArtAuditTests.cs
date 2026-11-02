using ColorBlockJam.Editor.BoardKit;
using ColorBlockJam.Presentation.Board;
using ColorBlockJam.Runtime.Art;
using ColorBlockJam.Runtime.Board;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace ColorBlockJam.Tests.EditMode
{
    /// <summary>
    /// Audits the board kit's shared art assets: the palette has no duplicate colours, the piece catalog's meshes
    /// come from the original modular FBX, the discarded corner mesh stays out, and the shared material and shader
    /// are error-free and instanced (Performance Rule 1, DEC-96).
    /// </summary>
    public sealed class BoardKitArtAuditTests
    {
        [Test]
        public void PaletteCatalogAndShaderAreComplete()
        {
            var palette = AssetDatabase.LoadAssetAtPath<GamePalette>(BoardKitAssets.PalettePath);
            var ids = new System.Collections.Generic.HashSet<PaletteColor>();
            foreach (var entry in palette.Colors) Assert.That(ids.Add(entry.Type), Is.True);
            Assert.That(palette.Colors.Count, Is.EqualTo(13));
            var catalog = AssetDatabase.LoadAssetAtPath<BoardPieceCatalog>(BoardKitAssets.CatalogPath);
            for (int i = 0; i < 4; i++)
            {
                var piece = catalog.Get((BlockPieceKind)i);
                foreach (var filter in piece.GetComponentsInChildren<MeshFilter>(true))
                    Assert.That(AssetDatabase.GetAssetPath(filter.sharedMesh), Is.EqualTo(BoardKitAssets.Root + "/Art/Models/Supplied/BlockParts.fbx"));
            }
            Assert.That(AssetDatabase.LoadAssetAtPath<GameObject>(BoardKitAssets.Root + "/Prefabs/Board/Pieces/corner_3.002.prefab"), Is.Null);
            var shader = Shader.Find("ColorBlockJam/Board Lit");
            Assert.That(ShaderUtil.ShaderHasError(shader), Is.False);
            var material = AssetDatabase.LoadAssetAtPath<Material>(BoardKitAssets.Root + "/Art/Materials/Shared/MAT_Gameplay.mat");
            Assert.That(material.enableInstancing, Is.True);
            foreach (string pass in new[] { "ForwardLit", "ShadowCaster", "DepthOnly", "DepthNormals" }) Assert.That(material.FindPass(pass), Is.GreaterThanOrEqualTo(0));
        }
    }
}
