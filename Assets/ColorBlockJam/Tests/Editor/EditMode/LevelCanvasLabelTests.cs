using System.Reflection;
using ColorBlockJam.Editor.Levels;
using ColorBlockJam.Runtime.Levels;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace ColorBlockJam.Tests.EditMode
{
    /// <summary>
    /// Verifies that the canvas block labels show the block id in capitals, centred on the block's footprint, and
    /// fall back to the nearest block cell when the footprint's centre is off the block.
    /// </summary>
    public sealed class LevelCanvasLabelTests
    {
        private EditorWindow m_host;
        private LevelCanvasElement m_canvas;

        [SetUp]
        public void OpenCanvas()
        {
            // A shown utility window lays the canvas out, so it has a board geometry.
            m_host = ScriptableObject.CreateInstance<EditorWindow>();
            m_host.ShowUtility();
            m_host.position = new Rect(40f, 40f, 600f, 700f);
            m_canvas = new LevelCanvasElement();
            m_canvas.style.width = 560f;
            m_canvas.style.height = 660f;
            m_host.rootVisualElement.Add(m_canvas);
            m_canvas.SetModel(LevelCanvasModel.Create(CreateLevel()));
            object panel = m_host.rootVisualElement.panel;
            panel.GetType().GetMethod("ValidateLayout", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                .Invoke(panel, null);
            Assume.That(m_canvas.Geometry.IsValid, "The canvas has a laid-out board.");
        }

        [TearDown]
        public void CloseCanvas()
        {
            m_host.Close();
        }

        [Test]
        public void LabelsShowTheIdInCapitals()
        {
            var labels = m_canvas.Query<Label>(className: LevelCanvasElement.k_labelClass).ToList();

            Assert.That(labels, Has.Count.EqualTo(3));
            Assert.That(labels[0].text, Is.EqualTo("B1"));
            Assert.That(labels[1].text, Is.EqualTo("B2"));
            Assert.That(labels[2].text, Is.EqualTo("B3"));
            Assert.That(labels[0].style.unityFontStyleAndWeight.value, Is.EqualTo(FontStyle.Bold));
        }

        [Test]
        public void LabelsAreBlackOnEveryBlockColor()
        {
            // Pink, Blue and Red: light and dark fills alike.
            var labels = m_canvas.Query<Label>(className: LevelCanvasElement.k_labelClass).ToList();

            foreach (Label label in labels)
            {
                Assert.That(label.style.color.value, Is.EqualTo(Color.black), label.text);
            }
        }

        [Test]
        public void SquareLabelSitsOnTheVertexItsFourCellsShare()
        {
            // b1: 2 x 2 at (0, 0); the centre is the corner where (0,0), (1,0), (0,1) and (1,1) meet.
            Vector2 expected = Mean(Centre(0, 0), Centre(1, 0), Centre(0, 1), Centre(1, 1));

            Assert.That(Vector2.Distance(GetLabelCentre(0), expected), Is.LessThan(0.5f));
        }

        [Test]
        public void TwoCellLabelSitsOnTheEdgeTheCellsShare()
        {
            // b2: 1 x 2 horizontal at (3, 0).
            Vector2 expected = Mean(Centre(3, 0), Centre(4, 0));

            Assert.That(Vector2.Distance(GetLabelCentre(1), expected), Is.LessThan(0.5f));
        }

        [Test]
        public void LabelFallsBackToTheNearestBlockCellWhenTheCentreIsInANotch()
        {
            // b3: an arch at (0, 4); its centre (1.5, 1.64 in cell units) lies in the empty notch cell (1, 1),
            // so the label keeps the block cell nearest the centre, (1, 2) + origin.
            LevelCanvasBlock arch = m_canvas.Model.GetBlock(2);
            Assert.That(arch.LabelCell, Is.EqualTo(new Vector2Int(1, 6)));

            Assert.That(Vector2.Distance(GetLabelCentre(2), Centre(1, 6)), Is.LessThan(0.5f));
        }

        [Test]
        public void SevenShapeLabelSitsOnItsColumnNotOverTheBlockBesideIt()
        {
            // D-001: a 2 x 2 block with a 7 shape beside it (a three-cell top row whose right end has a four-cell
            // column under it). The 7's mean lies 0.07 cells inside its column, so a label there would straddle b1.
            Vector2Int[] seven =
            {
                new Vector2Int(0, 4), new Vector2Int(1, 4), new Vector2Int(2, 4), new Vector2Int(2, 3),
                new Vector2Int(2, 2), new Vector2Int(2, 1), new Vector2Int(2, 0)
            };
            m_canvas.SetModel(LevelCanvasModel.Create(new LevelData("seven", "Seven", 6, 8, 60, new LevelDoorData[0], new[]
            {
                new LevelBlockData("b1", "Red", new Vector2Int(0, 1),
                    new[] { Vector2Int.zero, Vector2Int.right, Vector2Int.up, Vector2Int.one }),
                new LevelBlockData("b3", "Red", new Vector2Int(0, 0), seven)
            })));

            LevelCanvasBlock block = m_canvas.Model.GetBlock(1);
            Assert.That(block.LabelCell.x, Is.EqualTo(2), "The nearest block cell is on the column.");
            Vector2 label = GetLabelCentre(1);
            Assert.That(Vector2.Distance(label, Centre(block.LabelCell.x, block.LabelCell.y)), Is.LessThan(0.5f),
                "The label keeps the nearest block cell instead of the edge of the column.");
            Rect column = m_canvas.Geometry.GetCellRect(block.LabelCell);
            float reach = m_canvas.Geometry.CellSize * 0.25f;
            Assert.That(label.x - reach, Is.GreaterThanOrEqualTo(column.xMin), "No part of the id lies over b1.");
        }

        private static LevelData CreateLevel()
        {
            Vector2Int[] arch =
            {
                new Vector2Int(0, 0), new Vector2Int(0, 1), new Vector2Int(0, 2), new Vector2Int(1, 2),
                new Vector2Int(2, 2), new Vector2Int(2, 1), new Vector2Int(2, 0)
            };
            return new LevelData("labels", "Labels", 6, 8, 60, new LevelDoorData[0], new[]
            {
                new LevelBlockData("b1", "Pink", new Vector2Int(0, 0),
                    new[] { Vector2Int.zero, Vector2Int.right, Vector2Int.up, Vector2Int.one }),
                new LevelBlockData("b2", "Blue", new Vector2Int(3, 0),
                    new[] { Vector2Int.zero, Vector2Int.right }),
                new LevelBlockData("b3", "Red", new Vector2Int(0, 4), arch)
            });
        }

        private Vector2 Centre(int x, int y)
        {
            return m_canvas.Geometry.GetCellRect(new Vector2Int(x, y)).center;
        }

        private Vector2 GetLabelCentre(int index)
        {
            Label label = m_canvas.Query<Label>(className: LevelCanvasElement.k_labelClass).AtIndex(index);
            IStyle style = label.style;
            return new Vector2(
                style.left.value.value + style.width.value.value * 0.5f,
                style.top.value.value + style.height.value.value * 0.5f);
        }

        private static Vector2 Mean(params Vector2[] points)
        {
            Vector2 sum = Vector2.zero;
            foreach (Vector2 point in points)
            {
                sum += point;
            }

            return sum / points.Length;
        }
    }
}
