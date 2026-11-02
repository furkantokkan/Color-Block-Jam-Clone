// Copy-only diagnostic for TASK-20260927-123500933-c1e67965 (never copied back).
#if UNITY_EDITOR
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using ColorBlockJam.Environment;
using ColorBlockJam.Runtime.Layout;
using ColorBlockJam.Runtime.Levels;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace EnvGateCapture
{
    public sealed class EnvGateDiagnosticTests
    {
        private static readonly string s_out =
            @"C:\Users\e-fur\AppData\Local\Temp\cbj-envgate-7336b1-results\captures";

        [Test, Order(0)]
        public void C_DumpComposition()
        {
            Directory.CreateDirectory(s_out);
            var database = AssetDatabase.LoadAssetAtPath<EnvironmentDatabase>("Assets/ColorBlockJam/Data/Environment/EnvironmentDatabase.asset");
            var composer = new EnvironmentComposer(database);
            var grid = new GameObject("grid").AddComponent<Grid>();
            grid.cellSize = new Vector3(0.5f, 0.5f, 0.5f);
            grid.cellSwizzle = GridLayout.CellSwizzle.XZY;
            var placements = new List<EnvironmentPiecePlacement>();
            var gates = new List<EnvironmentGateSpan>();
            var text = new StringBuilder();

            Dump("hole", Layout(6, 6, new[] { new Vector2Int(2, 2), new Vector2Int(3, 2), new Vector2Int(2, 3),
                new Vector2Int(3, 3), new Vector2Int(0, 5), new Vector2Int(1, 5) }), new Vector3(0.25f, 0f, 4.75f), 1.6f);
            Dump("rect4x5", Layout(4, 5, new Vector2Int[0], new LevelDoorData("t", BoardSide.Top, 1, 3, "LightBlue"),
                new LevelDoorData("b", BoardSide.Bottom, 0, 3, "Pink")), Vector3.zero, 100f);
            File.WriteAllText(Path.Combine(s_out, "composition-dump.txt"), text.ToString());
            Object.DestroyImmediate(grid.gameObject);

            void Dump(string name, BoardLayout layout, Vector3 near, float radius)
            {
                composer.ComposePlayArea(layout, placements, gates);
                var counts = new SortedDictionary<string, int>();
                text.AppendLine($"== {name}: {placements.Count} placements, {gates.Count} gates");
                foreach (EnvironmentPiecePlacement p in placements)
                {
                    string key = p.Piece.name;
                    counts[key] = counts.TryGetValue(key, out int c) ? c + 1 : 1;
                }

                foreach (var pair in counts) text.AppendLine($"  count {pair.Key} = {pair.Value}");
                foreach (EnvironmentPiecePlacement p in placements)
                {
                    Vector3 local = grid.CellToLocalInterpolated(p.GridPosition);
                    if (Mathf.Abs(local.x - near.x) > radius || Mathf.Abs(local.z - near.z) > radius) continue;
                    if (p.Piece == database.FloorPiece || p.Piece == database.FloorBasePiece) continue;
                    text.AppendLine(string.Format(CultureInfo.InvariantCulture,
                        "  {0,-14} local=({1:F3},{2:F3}) grid=({3:F3},{4:F3}) yaw={5,3} scale=({6:F3},{7:F3},{8:F3})",
                        p.Piece.name, local.x, local.z, p.GridPosition.x, p.GridPosition.y, p.Yaw, p.Scale.x, p.Scale.y, p.Scale.z));
                }
            }
        }

        private static BoardLayout Layout(int width, int height, Vector2Int[] voids, params LevelDoorData[] doors)
        {
            Assert.That(BoardLayout.TryCreate(width, height, voids, doors, out BoardLayout layout, out string error), Is.True, error);
            return layout;
        }
    }
}
#endif
