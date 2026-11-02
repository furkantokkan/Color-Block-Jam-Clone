using System;
using ColorBlockJam.Editor.Levels;
using ColorBlockJam.Runtime.Levels;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UIElements;

namespace ColorBlockJam.Tests.EditMode
{
    /// <summary>
    /// Verifies the Level Editor canvas's Ice effect badge and its shared badge layout: count and dispose/reattach
    /// without duplicates, the below-name position, and the side-column overflow when several badges compete.
    /// </summary>
    public sealed class LevelCanvasEffectLayerTests
    {
        [Test]
        public void IceCountSurvivesSnapshotAndLayerDisposesAndReattachesWithoutDuplicates()
        {
            var level = new LevelData("ice-canvas", "Ice", 3, 3, 60, Array.Empty<LevelDoorData>(), new[]
            {
                new LevelBlockData("b1", "Red", Vector2Int.zero, new[] { Vector2Int.zero },
                    new[] { new LevelEffectData("ice", new[] { new LevelEffectParamData("clearsToThaw", "20") }) })
            });
            var model = LevelCanvasModel.Create(level);
            Assert.That(model.GetBlock(0).TryGetEffect(out IceEffect ice), Is.True);
            Assert.That(ice.ClearsToThaw, Is.EqualTo(20));
            var parent = new VisualElement();
            var layer = new IceCanvasEffectLayer();
            layer.SetModel(model, parent);
            Assert.That(parent.Q<Label>("ice-authoring-count").text, Is.EqualTo("20"));
            Assert.That(parent.Q<Label>("ice-authoring-count").pickingMode, Is.EqualTo(PickingMode.Ignore));
            layer.Dispose();
            Assert.That(parent.childCount, Is.Zero);
            layer.SetModel(model, parent);
            layer.SetModel(model, parent);
            Assert.That(parent.childCount, Is.EqualTo(1));
            layer.SetModel(LevelCanvasModel.Empty, parent);
            Assert.That(parent.childCount, Is.Zero);
        }

        [TestCase(1)]
        [TestCase(20)]
        public void FrostBadgeKeepsCountBelowNameAcrossRemovalAndRestoredSnapshot(int count)
        {
            LevelCanvasModel frozen = Model(count);
            var parent = new VisualElement();
            var id = new Label("B1") { name = "block-id" };
            var arrow = new VisualElement { name = "arrow-marker" };
            parent.Add(id);
            parent.Add(arrow);
            var layer = new IceCanvasEffectLayer();
            var layout = new LevelCanvasEffectBadgeLayout();
            try
            {
                layer.SetModel(frozen, parent);
                VisualElement badge = parent.Q("ice-authoring-badge");
                Label label = badge.Q<Label>("ice-authoring-count");
                Assert.That(label.text, Is.EqualTo(count.ToString()));
                Assert.That(badge.pickingMode, Is.EqualTo(PickingMode.Ignore));
                Assert.That(badge.style.backgroundColor.value.b, Is.GreaterThan(badge.style.backgroundColor.value.r));
                var geometry = LevelCanvasGeometry.Create(new Vector2(600, 600), 3, 3);
                layer.Position(geometry);
                Rect cell = geometry.GetCellRect(frozen.GetBlock(0).LabelCell);
                layout.SetModel(frozen, parent);
                layout.Position(0, frozen.GetBlock(0), geometry, cell.center, 20f, new ILevelCanvasEffectLayer[] { layer });
                VisualElement row = parent.Q("effect-badge-row");
                Assert.That(row.style.top.value.value, Is.GreaterThan(cell.center.y + 10f));
                Assert.That(row.style.left.value.value + badge.style.width.value.value * .5f, Is.EqualTo(cell.center.x));
                Assert.That(row.style.top.value.value + badge.style.height.value.value, Is.LessThan(cell.yMax));
                Assert.That(label.style.left.value.value, Is.GreaterThan(0), "the vector snowflake has its own space");
                layer.SetModel(Model(0), parent);
                layout.Position(0, Model(0).GetBlock(0), geometry, cell.center, 20f, new ILevelCanvasEffectLayer[] { layer });
                Assert.That(badge.style.display.value, Is.EqualTo(DisplayStyle.None));
                layer.SetModel(frozen, parent); // The same snapshot route used by draft Undo/Redo.
                layer.Position(geometry);
                layout.Position(0, frozen.GetBlock(0), geometry, cell.center, 20f, new ILevelCanvasEffectLayer[] { layer });
                Assert.That(parent.Q("ice-authoring-badge"), Is.SameAs(badge));
                Assert.That(label.text, Is.EqualTo(count.ToString()));
                Assert.That(badge.style.display.value, Is.EqualTo(DisplayStyle.Flex));
                Assert.That(id.text, Is.EqualTo("B1"));
                Assert.That(arrow.parent, Is.SameAs(parent));
                Assert.That(parent.childCount, Is.EqualTo(3));
            }
            finally { layer.Dispose(); layout.Dispose(); }
            Assert.That(parent.childCount, Is.EqualTo(2));
        }

        [Test]
        public void RegisteredEffectBadgesStackThenOpenSideColumnsWithoutHidingAnyBadge()
        {
            LevelCanvasModel model = Model(1);
            var geometry = LevelCanvasGeometry.Create(new Vector2(400, 400), 3, 3);
            var parent = new VisualElement();
            var layout = new LevelCanvasEffectBadgeLayout();
            var layers = new ILevelCanvasEffectLayer[7];
            for (int index = 0; index < layers.Length; index++)
            {
                layers[index] = new FakeLayer("effect-" + index, 30f, 10f);
                layers[index].SetModel(model, parent);
            }
            try
            {
                layout.SetModel(model, parent);
                Rect cell = geometry.GetCellRect(model.GetBlock(0).LabelCell);
                // This is the actual name anchor supplied by the canvas, not an independently guessed cell centre.
                Vector2 anchor = cell.center + new Vector2(6f, 4f);
                layout.Position(0, model.GetBlock(0), geometry, anchor, 20f, layers);
                VisualElement row = parent.Q("effect-badge-row");
                Assert.That(row.style.flexDirection.value, Is.EqualTo(FlexDirection.Row));
                Assert.That(row.style.left.value.value, Is.EqualTo(anchor.x - 15f));
                Assert.That(row.style.top.value.value, Is.EqualTo(anchor.y + 13f));
                Assert.That(row.childCount, Is.GreaterThan(1));
                int registered = 0;
                foreach (VisualElement column in row.Children())
                {
                    Assert.That(column.style.flexDirection.value, Is.EqualTo(FlexDirection.Column));
                    Assert.That(column.pickingMode, Is.EqualTo(PickingMode.Ignore));
                    float stackHeight = column.childCount * 10f + Mathf.Max(0, column.childCount - 1) * 3f;
                    Assert.That(row.style.top.value.value + stackHeight, Is.LessThanOrEqualTo(cell.yMax - 2f));
                    foreach (VisualElement badge in column.Children())
                    {
                        Assert.That(badge.name, Is.EqualTo("effect-" + registered++));
                        Assert.That(badge.pickingMode, Is.EqualTo(PickingMode.Ignore));
                        Assert.That(badge.style.display.value, Is.Not.EqualTo(DisplayStyle.None));
                    }
                }
                Assert.That(registered, Is.EqualTo(layers.Length));
                layout.Position(0, model.GetBlock(0), geometry, anchor, 20f, layers);
                Assert.That(parent.Q("effect-badge-row"), Is.SameAs(row));
                Assert.That(parent.Query<VisualElement>("effect-0").ToList().Count, Is.EqualTo(1));
                layout.SetModel(LevelCanvasModel.Empty, parent);
                Assert.That(parent.childCount, Is.Zero);
                layout.SetModel(model, parent);
                layout.Position(0, model.GetBlock(0), geometry, anchor, 20f, layers);
                Assert.That(parent.Q("effect-badge-row").childCount, Is.GreaterThan(1));
                Assert.That(parent.Query<VisualElement>("effect-0").ToList().Count, Is.EqualTo(1));
            }
            finally
            {
                for (int index = 0; index < layers.Length; index++) layers[index].Dispose();
                layout.Dispose();
            }
            Assert.That(parent.childCount, Is.Zero);
        }

        [Test]
        public void TinyCellStartsSideColumnWhenNoBadgeFitsBelowTheName()
        {
            LevelCanvasModel model = Model(1);
            var geometry = LevelCanvasGeometry.Create(new Vector2(100, 100), 3, 3);
            var parent = new VisualElement();
            var layer = new FakeLayer("fake-effect", 20f, 12f);
            var layout = new LevelCanvasEffectBadgeLayout();
            try
            {
                layer.SetModel(model, parent);
                layout.SetModel(model, parent);
                Rect cell = geometry.GetCellRect(model.GetBlock(0).LabelCell);
                layout.Position(0, model.GetBlock(0), geometry, cell.center, 11f, new ILevelCanvasEffectLayer[] { layer });
                VisualElement row = parent.Q("effect-badge-row");
                Assert.That(row.style.left.value.value, Is.GreaterThan(cell.center.x));
                Assert.That(row.style.top.value.value + 12f, Is.LessThanOrEqualTo(cell.yMax - 2f));
                Assert.That(row.style.display.value, Is.EqualTo(DisplayStyle.Flex));
                Assert.That(row.childCount, Is.EqualTo(1));
            }
            finally { layer.Dispose(); layout.Dispose(); }
        }

        private sealed class FakeLayer : ILevelCanvasEffectLayer
        {
            private readonly VisualElement m_badge;
            public FakeLayer(string name, float width, float height)
            {
                m_badge = new VisualElement { name = name };
                m_badge.style.width = width;
                m_badge.style.height = height;
            }
            public void SetModel(LevelCanvasModel model, VisualElement parent) => parent.Add(m_badge);
            public void Position(LevelCanvasGeometry geometry) { }
            public VisualElement GetBadge(int block) => block == 0 ? m_badge : null;
            public void Draw(Painter2D painter, LevelCanvasGeometry geometry) { }
            public void Dispose() => m_badge.RemoveFromHierarchy();
        }

        private static LevelCanvasModel Model(int count)
        {
            LevelEffectData[] effects = count > 0
                ? new[] { new LevelEffectData("ice", new[] { new LevelEffectParamData("clearsToThaw", count.ToString()) }) }
                : Array.Empty<LevelEffectData>();
            return LevelCanvasModel.Create(new LevelData("ice-canvas", "Ice", 3, 3, 60,
                Array.Empty<LevelDoorData>(), new[]
                {
                    new LevelBlockData("b1", "Red", Vector2Int.zero, new[] { Vector2Int.zero }, effects)
                }));
        }
    }
}
