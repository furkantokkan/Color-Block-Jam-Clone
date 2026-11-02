using System.Reflection;
using ColorBlockJam.Editor.Levels;
using ColorBlockJam.Runtime.Board;
using ColorBlockJam.Runtime.Levels;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

namespace ColorBlockJam.Tests.EditMode
{
    /// <summary>
    /// Verifies the Level Editor canvas Arrow badge (TASK-20260926-212320271-98b25efa-level-editor-arrow-markers AC-2,
    /// revision 1): the canvas Arrow layer derives each block's marker in board cells from the model, blocks without a
    /// valid Arrow get none, the badge is centred on the block's bounding box along its axis, the block labels have a
    /// dark outline, and a canvas repaint draws one badge per Arrow block without errors. No Level Editor window is
    /// opened. Since TASK-20260926-230921278-a3ffdc40 the badge lives in ArrowCanvasEffectLayer (seam-only port).
    /// </summary>
    public sealed class CanvasArrowBadgeTests
    {
        private static readonly Vector2Int[] s_verticalI3 = { new Vector2Int(0, 0), new Vector2Int(0, 1), new Vector2Int(0, 2) };
        private static readonly Vector2Int[] s_single = { new Vector2Int(0, 0) };
        private static readonly Vector2Int[] s_square =
        {
            new Vector2Int(0, 0), new Vector2Int(1, 0), new Vector2Int(0, 1), new Vector2Int(1, 1)
        };
        private static readonly Vector2Int[] s_t =
        {
            new Vector2Int(1, 0), new Vector2Int(0, 1), new Vector2Int(1, 1), new Vector2Int(2, 1)
        };

        [Test]
        public void ModelCarriesEachArrowBlocksMarkerInBoardCellsAndNoneForOtherBlocks()
        {
            LevelCanvasModel model = LevelCanvasModel.Create(CreateLevel("Vertical"));

            ArrowMarker vertical = ArrowCanvasEffectLayer.CreateMarker(model.GetBlock(0));
            Assert.That(vertical.HasMarker, Is.True);
            Assert.That(vertical.Axis, Is.EqualTo(LevelEffectAxis.Vertical));
            Assert.That(vertical.Center, Is.EqualTo(new Vector2(1f, 2f)), "The centre is in absolute board cells.");
            Assert.That(vertical.Variant, Is.EqualTo(3));

            // The T at (3, 1) covers x 3-5 and y 1-2: its bar's length, centred between the bar and the stem.
            ArrowMarker t = ArrowCanvasEffectLayer.CreateMarker(model.GetBlock(1));
            Assert.That(t.Axis, Is.EqualTo(LevelEffectAxis.Horizontal));
            Assert.That(t.Center, Is.EqualTo(new Vector2(4f, 1.5f)));
            Assert.That(t.Variant, Is.EqualTo(3));

            // The 2 x 2 at (2, 4) is centred on the line between its columns.
            ArrowMarker square = ArrowCanvasEffectLayer.CreateMarker(model.GetBlock(4));
            Assert.That(square.Center, Is.EqualTo(new Vector2(2.5f, 4.5f)));
            Assert.That(square.Variant, Is.EqualTo(2));

            Assert.That(ArrowCanvasEffectLayer.CreateMarker(model.GetBlock(2)).HasMarker, Is.False,
                "A block without effects has no badge.");
            Assert.That(ArrowCanvasEffectLayer.CreateMarker(model.GetBlock(3)).HasMarker, Is.False,
                "An Arrow without its axis has no badge.");
        }

        [Test]
        public void AModelBuiltAfterAnAxisChangeCarriesTheNewMarker()
        {
            // The window builds a new model for every draft change, an Effect Editor edit or its undo included; this
            // checks only that a new model follows the level's axis.
            Assert.That(ArrowCanvasEffectLayer.CreateMarker(LevelCanvasModel.Create(CreateLevel("Vertical")).GetBlock(0)).Variant, Is.EqualTo(3));

            ArrowMarker across = ArrowCanvasEffectLayer.CreateMarker(LevelCanvasModel.Create(CreateLevel("Horizontal")).GetBlock(0));

            Assert.That(across.Axis, Is.EqualTo(LevelEffectAxis.Horizontal));
            Assert.That(across.Center, Is.EqualTo(new Vector2(1f, 2f)));
            Assert.That(across.Variant, Is.EqualTo(1));
            Assert.That(ArrowCanvasEffectLayer.CreateMarker(LevelCanvasModel.Create(CreateLevel("Vertical")).GetBlock(0)).Axis,
                Is.EqualTo(LevelEffectAxis.Vertical));
        }

        [Test]
        public void VerticalBadgeOnATwoByTwoRunsDownTheLineBetweenItsColumns()
        {
            LevelCanvasGeometry geometry = LevelCanvasGeometry.Create(new Vector2(640f, 640f), 6, 6);
            Vector2Int[] cells = Offset(s_square, new Vector2Int(1, 1));
            ArrowMarker marker = ArrowMarkerRun.Create(cells, LevelEffectAxis.Vertical);
            Vector2 blockCentre = GetMeanCentre(geometry, cells);
            Rect bottomLeft = geometry.GetCellRect(new Vector2Int(1, 1));
            Rect topLeft = geometry.GetCellRect(new Vector2Int(1, 2));

            ArrowCanvasEffectLayer.ArrowBadge badge = ArrowCanvasEffectLayer.GetArrowBadge(geometry, marker);

            Assert.That(badge.TipA.x, Is.EqualTo(bottomLeft.xMax).Within(1e-3f), "It runs on the line between the columns.");
            Assert.That(badge.TipB.x, Is.EqualTo(bottomLeft.xMax).Within(1e-3f));
            Assert.That(Vector2.Distance((badge.TipA + badge.TipB) * 0.5f, blockCentre), Is.LessThan(1e-3f),
                "Centred on the block.");
            Assert.That(badge.TipA.y, Is.GreaterThan(badge.TipB.y), "Tip A is at the lower row.");
            Assert.That(badge.TipA.y, Is.LessThan(bottomLeft.yMax), "Tip A stays inside the block.");
            Assert.That(badge.TipB.y, Is.GreaterThan(topLeft.yMin), "Tip B stays inside the block.");
            Assert.That(badge.Length, Is.GreaterThan(1.5f * geometry.CellSize).And.LessThan(2f * geometry.CellSize),
                "It spans most of the two rows.");
            Assert.That(badge.HeadLength, Is.GreaterThan(0f).And.LessThan(0.5f * badge.Length));
        }

        [Test]
        public void HorizontalBadgeOnATLiesBetweenItsBarAndStemAndClampsAtThreeCells()
        {
            LevelCanvasGeometry geometry = LevelCanvasGeometry.Create(new Vector2(640f, 640f), 6, 6);
            Vector2Int[] tCells = Offset(s_t, new Vector2Int(1, 1));
            ArrowMarker t = ArrowMarkerRun.Create(tCells, LevelEffectAxis.Horizontal);
            Rect stem = geometry.GetCellRect(new Vector2Int(2, 1));
            Rect bar = geometry.GetCellRect(new Vector2Int(2, 2));

            ArrowCanvasEffectLayer.ArrowBadge badge = ArrowCanvasEffectLayer.GetArrowBadge(geometry, t);

            float between = (stem.center.y + bar.center.y) * 0.5f;
            Assert.That(badge.TipA.y, Is.EqualTo(between).Within(1e-3f), "It runs between the bar and the stem rows.");
            Assert.That(badge.TipB.y, Is.EqualTo(between).Within(1e-3f));
            Assert.That(badge.TipA.x, Is.LessThan(badge.TipB.x));
            Assert.That((badge.TipA.x + badge.TipB.x) * 0.5f, Is.EqualTo(stem.center.x).Within(1e-3f));

            // A horizontal line of five is centred on its middle cell and clamps to Arrow_3's three cells.
            Vector2Int[] line =
            {
                new Vector2Int(0, 5), new Vector2Int(1, 5), new Vector2Int(2, 5), new Vector2Int(3, 5), new Vector2Int(4, 5)
            };
            ArrowCanvasEffectLayer.ArrowBadge clamped = ArrowCanvasEffectLayer.GetArrowBadge(geometry,
                ArrowMarkerRun.Create(line, LevelEffectAxis.Horizontal));
            Rect middle = geometry.GetCellRect(new Vector2Int(2, 5));
            Assert.That(Vector2.Distance((clamped.TipA + clamped.TipB) * 0.5f, middle.center), Is.LessThan(1e-3f));
            Assert.That(clamped.Length, Is.GreaterThan(2.5f * geometry.CellSize).And.LessThan(3f * geometry.CellSize));
        }

        [Test]
        public void BadgeDrawsAFilledOutlinedArrowOverTheBlock()
        {
            LevelCanvasGeometry geometry = LevelCanvasGeometry.Create(new Vector2(640f, 640f), 6, 6);
            ArrowMarker marker = ArrowMarkerRun.Create(Offset(s_verticalI3, new Vector2Int(1, 1)), LevelEffectAxis.Vertical);
            ArrowCanvasEffectLayer.ArrowBadge badge = ArrowCanvasEffectLayer.GetArrowBadge(geometry, marker);
            Painter2D painter = new Painter2D();
            VectorImage image = ScriptableObject.CreateInstance<VectorImage>();
            try
            {
                ArrowCanvasEffectLayer.DrawArrowBadge(painter, badge);

                Assert.That(painter.SaveToVectorImage(image), Is.True, "The badge produced geometry.");
                // The 1.5 px outline and its mitred corners add a few pixels around the arrow's own extent.
                Assert.That(image.height, Is.EqualTo(badge.Length).Within(5f), "It spans tip to tip along the axis.");
                Assert.That(image.width, Is.EqualTo(2f * badge.HeadHalfWidth).Within(5f), "Its heads set its width.");
            }
            finally
            {
                painter.Dispose();
                Object.DestroyImmediate(image);
            }
        }

        [Test]
        public void CanvasTakesAModelWithArrowBlocksAndOutlinesEveryBlockLabel()
        {
            LevelCanvasElement canvas = new LevelCanvasElement();

            Assert.DoesNotThrow(() => canvas.SetModel(LevelCanvasModel.Create(CreateLevel("Vertical"))));

            var labels = canvas.Query<Label>(className: LevelCanvasElement.k_labelClass).ToList();
            Assert.That(labels, Has.Count.EqualTo(5), "The badge adds no label or child element.");
            Assert.That(canvas.GetEffectLayer<ArrowCanvasEffectLayer>().GetMarker(0).HasMarker, Is.True);
            foreach (Label label in labels)
            {
                Assert.That(label.style.unityTextOutlineWidth.value, Is.GreaterThan(0f), label.text + " has an outline.");
                Color outline = label.style.unityTextOutlineColor.value;
                Assert.That(outline.r + outline.g + outline.b, Is.LessThan(0.6f), label.text + "'s outline is dark.");
                Assert.That(outline.a, Is.EqualTo(1f), label.text + "'s outline is opaque.");
            }
        }

        [Test]
        public void CanvasRepaintDrawsOneBadgePerArrowBlockWithoutErrors()
        {
            // A plain utility window hosts the canvas, as ChoiceSearchTests hosts its popup; no Level Editor opens.
            EditorWindow host = ScriptableObject.CreateInstance<EditorWindow>();
            LevelCanvasElement canvas = new LevelCanvasElement();
            try
            {
                host.ShowUtility();
                host.position = new Rect(60f, 60f, 640f, 640f);
                host.rootVisualElement.Add(canvas);
                canvas.SetModel(LevelCanvasModel.Create(CreateLevel("Vertical")));

                // The first pass lays the canvas out and sizes its geometry; the second draws the blocks.
                RepaintNow(host);
                RepaintNow(host);

                Assert.That(canvas.Geometry.IsValid, Is.True, "The canvas has a board to draw.");
                Assert.That(canvas.GetEffectLayer<ArrowCanvasEffectLayer>().DrawnBadgeCount, Is.EqualTo(3),
                    "b1, b2 and b5 get a badge; b3 (no effect) and b4 (no axis) get none.");

                canvas.SetModel(LevelCanvasModel.Create(CreatePlainLevel()));
                RepaintNow(host);

                Assert.That(canvas.GetEffectLayer<ArrowCanvasEffectLayer>().DrawnBadgeCount, Is.Zero,
                    "A level without Arrow blocks draws no badge.");
                LogAssert.NoUnexpectedReceived();
            }
            finally
            {
                host.Close();
            }
        }

        // Lays the window out and repaints it now: RepaintImmediately runs generateVisualContent synchronously.
        private static void RepaintNow(EditorWindow host)
        {
            object panel = host.rootVisualElement.panel;
            panel.GetType().GetMethod("ValidateLayout", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                .Invoke(panel, null);
            typeof(EditorWindow).GetMethod("RepaintImmediately", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(host, null);
        }

        // A 6 x 6 board: b1 a vertical I3 at (1, 1) with an Arrow of the given axis, b2 a T at (3, 1) with a
        // Horizontal Arrow, b3 a single with no effect, b4 a single whose Arrow has no axis and b5 a 2 x 2 at (2, 4)
        // with a Vertical Arrow.
        private static LevelData CreateLevel(string b1Axis)
        {
            return new LevelData("arrows", "Arrows", 6, 6, 60, new LevelDoorData[0], new[]
            {
                new LevelBlockData("b1", "Pink", new Vector2Int(1, 1), s_verticalI3, new[] { Arrow(b1Axis) }),
                new LevelBlockData("b2", "Blue", new Vector2Int(3, 1), s_t, new[] { Arrow("Horizontal") }),
                new LevelBlockData("b3", "Orange", new Vector2Int(5, 5), s_single),
                new LevelBlockData("b4", "Red", new Vector2Int(0, 5), s_single,
                    new[] { new LevelEffectData("arrow", new LevelEffectParamData[0]) }),
                new LevelBlockData("b5", "Purple", new Vector2Int(2, 4), s_square, new[] { Arrow("Vertical") })
            });
        }

        // The same 6 x 6 board with b1 and b5 only, neither with an effect.
        private static LevelData CreatePlainLevel()
        {
            return new LevelData("plain", "Plain", 6, 6, 60, new LevelDoorData[0], new[]
            {
                new LevelBlockData("b1", "Pink", new Vector2Int(1, 1), s_verticalI3),
                new LevelBlockData("b5", "Purple", new Vector2Int(2, 4), s_square)
            });
        }

        private static LevelEffectData Arrow(string axis)
        {
            return new LevelEffectData("arrow", new[] { new LevelEffectParamData("axis", axis) });
        }

        private static Vector2 GetMeanCentre(LevelCanvasGeometry geometry, Vector2Int[] cells)
        {
            Vector2 sum = Vector2.zero;
            for (int index = 0; index < cells.Length; index++)
            {
                sum += geometry.GetCellRect(cells[index]).center;
            }

            return sum / cells.Length;
        }

        private static Vector2Int[] Offset(Vector2Int[] cells, Vector2Int origin)
        {
            Vector2Int[] moved = new Vector2Int[cells.Length];
            for (int index = 0; index < cells.Length; index++)
            {
                moved[index] = cells[index] + origin;
            }

            return moved;
        }
    }
}
