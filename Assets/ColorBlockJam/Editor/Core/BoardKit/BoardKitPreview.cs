using System.IO;
using ColorBlockJam.Runtime.Art;
using ColorBlockJam.Presentation.Board;
using UnityEditor;
using UnityEngine;

namespace ColorBlockJam.Editor.BoardKit
{
    /// <summary>
    /// Renders the board kit contact-sheet PNGs under docs/art/board-kit: one per piece library template, named by
    /// its key (DEC-193), and one per environment piece and palette colour.
    /// </summary>
    public static class BoardKitPreview
    {
        // The number of block colours PaletteColor keys before Wall/Ground (Runtime/Core/Art/PaletteColor.cs).
        private const int k_blockColorCount = (int)PaletteColor.Wall;

        [MenuItem("Color Block Jam/Art/Render Board Kit Contact Sheet Frames")]
        public static void RenderAll()
        {
            Directory.CreateDirectory("docs/art/board-kit/shapes");
            var library = BlockShapeLibrary.LoadProject();
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(BoardKitAssets.PrefabPath);
            var art = new BoardArt(AssetDatabase.LoadAssetAtPath<GamePalette>(BoardKitAssets.PalettePath),
                AssetDatabase.LoadAssetAtPath<BoardPieceCatalog>(BoardKitAssets.CatalogPath));
            for (int i = 0; i < library.Shapes.Count; i++)
            {
                var preview = new PreviewRenderUtility();
                var root = UnityEngine.Object.Instantiate(prefab);
                preview.AddSingleGO(root);
                try
                {
                    var view = root.GetComponent<ModularBlockView>();
                    view.Prepare(art.Catalog, library.Shapes[i].Cells.Count);
                    view.SetCells(library.Shapes[i].Cells, art.GetAtlasColor((PaletteColor)(i % k_blockColorCount)));
                    Bounds bounds = BoardKitPreviewStage.GetRendererBounds(root);
                    Camera camera = preview.camera;
                    camera.orthographic = true;
                    camera.orthographicSize = 2.35f;
                    camera.nearClipPlane = .01f;
                    camera.farClipPlane = 100f;
                    camera.transform.position = bounds.center + new Vector3(1.8f, 6f, -8f);
                    camera.transform.LookAt(bounds.center);
                    camera.backgroundColor = new Color(.09f, .115f, .17f);
                    camera.clearFlags = CameraClearFlags.SolidColor;
                    BoardKitPreviewStage.ApplyLighting(preview, new Color(.6f, .6f, .6f));
                    preview.BeginStaticPreview(new Rect(0, 0, 400, 400));
                    preview.Render(true, true);
                    Texture2D texture = preview.EndStaticPreview();
                    File.WriteAllBytes("docs/art/board-kit/shapes/" + library.Shapes[i].Key + ".png", texture.EncodeToPNG());
                    UnityEngine.Object.DestroyImmediate(texture);
                    view.Release();
                }
                finally { preview.Cleanup(); }
            }
            Debug.Log($"Rendered {library.Shapes.Count} isolated board previews; active scene was not edited.");
        }

        [MenuItem("Color Block Jam/Art/Render Environment and Palette Previews")]
        public static void RenderEnvironment()
        {
            Directory.CreateDirectory("docs/art/board-kit/environment");
            string[] names = BoardKitAssets.EnvironmentPieceNames;
            var palette = AssetDatabase.LoadAssetAtPath<GamePalette>(BoardKitAssets.PalettePath);
            for (int i = 0; i < names.Length + k_blockColorCount; i++)
            {
                string name = i < names.Length ? names[i] : "Palette_" + (PaletteColor)(i - names.Length);
                string source = i < names.Length ? names[i] : "Door_1x1";
                var preview = new PreviewRenderUtility();
                var root = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(BoardKitAssets.EnvironmentPrefabPath + "/" + source + ".prefab"));
                preview.AddSingleGO(root);
                try
                {
                    // Pieces carry no colour of their own (DEC-123), so each preview is tinted here.
                    AtlasColor color = i >= names.Length ? palette.GetAtlasColor((PaletteColor)(i - names.Length)) : GetEnvironmentColor(palette, name);
                    root.GetComponent<BoardPieceView>().SetColor(color);
                    Bounds bounds = BoardKitPreviewStage.GetRendererBounds(root);
                    var camera = preview.camera;
                    camera.orthographic = true; camera.orthographicSize = 1.75f;
                    camera.nearClipPlane = .01f; camera.farClipPlane = 100;
                    camera.transform.position = bounds.center + new Vector3(2, 6, -8);
                    camera.transform.LookAt(bounds.center);
                    camera.backgroundColor = new Color(.09f, .115f, .17f);
                    camera.clearFlags = CameraClearFlags.SolidColor;
                    BoardKitPreviewStage.ApplyLighting(preview, Color.gray);
                    preview.BeginStaticPreview(new Rect(0, 0, 320, 320)); preview.Render(true, true);
                    var texture = preview.EndStaticPreview();
                    File.WriteAllBytes("docs/art/board-kit/environment/" + name + ".png", texture.EncodeToPNG());
                    Object.DestroyImmediate(texture);
                }
                finally { preview.Cleanup(); }
            }
        }

        // The colours the pieces showed before they lost their own palette field.
        private static AtlasColor GetEnvironmentColor(GamePalette palette, string name)
        {
            if (name.StartsWith("Arrow_", System.StringComparison.Ordinal)) return palette.ArrowAtlasColor;
            if (name.StartsWith("Door", System.StringComparison.Ordinal)) return palette.GetAtlasColor(PaletteColor.Orange);
            return palette.GetAtlasColor(name == "Grid" ? PaletteColor.Ground : PaletteColor.Wall);
        }
    }
}
