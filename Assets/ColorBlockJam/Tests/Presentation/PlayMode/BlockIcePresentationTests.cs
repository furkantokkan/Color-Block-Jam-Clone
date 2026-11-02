#if UNITY_EDITOR
using ColorBlockJam.Runtime.Art;
using System.Collections;
using System.Collections.Generic;
using ColorBlockJam.Presentation.Board;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

namespace ColorBlockJam.Tests.PlayMode
{
    /// <summary>
    /// The Ice look's material/clip/crack/tween behaviour on the block's own tiles (BlockIceOverlays, DEC-184) and the
    /// counter badge's numeral, layout and picking (IceCounterLabels).
    /// </summary>
    public sealed class BlockIcePresentationTests
    {
        // Ice counter badge height above the board plane, matching IceCounterLabels.GetLabelOffset's authored y.
        private const float k_labelHeight = 0.72f;
        // IceCounterLabels' cyan numeral face colour.
        private static readonly Color k_iceFaceColor = new Color(163f / 255f, 241f / 255f, 253f / 255f);

        private static readonly Vector2Int[][] s_footprints =
        {
            new[] { Vector2Int.zero, Vector2Int.up, Vector2Int.right, Vector2Int.one },
            new[] { Vector2Int.zero, Vector2Int.up, Vector2Int.right },
            new[] { Vector2Int.zero, Vector2Int.right, new Vector2Int(2,0), Vector2Int.up,
                new Vector2Int(2,1),new Vector2Int(0,2),new Vector2Int(1,2),new Vector2Int(2,2) },
        };

        private GameObject m_storage;
        private BlockIceOverlays m_shells;
        private IceCounterLabels m_labels;
        private Material m_material;

        [TearDown]
        public void TearDown()
        {
            m_labels?.Dispose();
            m_shells?.Dispose();
            if (m_storage != null) Object.DestroyImmediate(m_storage);
        }

        /// <summary>
        /// Builds three block views from s_footprints, shows ice on each and returns the look of block 2 (DEC-184).
        /// </summary>
        private BlockIceOverlay AttachShells(out Transform[] roots)
        {
            m_storage = new GameObject("Ice presentation fixture");
            m_material = AssetDatabase.LoadAssetAtPath<Material>("Assets/ColorBlockJam/Art/Materials/Effects/MAT_Ice.mat");
            var catalog = AssetDatabase.LoadAssetAtPath<BoardPieceCatalog>("Assets/ColorBlockJam/Data/Board/BoardPieceCatalog.asset");
            m_shells = new BlockIceOverlays(m_material, s_footprints.Length);
            roots = new Transform[s_footprints.Length];
            for (int i = 0; i < roots.Length; i++)
            {
                roots[i] = new GameObject("Block fixture").transform;
                roots[i].SetParent(m_storage.transform);
                var view = roots[i].gameObject.AddComponent<ModularBlockView>();
                view.Prepare(catalog, s_footprints[i].Length);
                view.SetCells(s_footprints[i], AssetDatabase.LoadAssetAtPath<GamePalette>("Assets/ColorBlockJam/Data/Art/GamePalette.asset").GetAtlasColor(PaletteColor.Red));
                m_shells.Attach(i, view, 2, 2);
            }
            return m_shells.GetAt(2);
        }

        [Test]
        public void AttachedIceIsTheSharedMaterialOnTheBlocksOwnTiles()
        {
            AttachShells(out Transform[] roots);
            for (int i = 0; i < roots.Length; i++)
            {
                ModularBlockView view = m_shells.GetAt(i).View;
                for (int tile = 0; tile < view.ActivePieceCount; tile++)
                {
                    Material[] materials = view.GetShownPiece(tile).Renderer.sharedMaterials;
                    Assert.That(materials, Has.Length.EqualTo(2), "the block material, then Ice");
                    Assert.That(materials[1], Is.SameAs(m_material));
                }
            }
        }

        [Test]
        public void SettingCountAppliesCrackAmountAndTheIceReadsTheBlocksClipPlane()
        {
            BlockIceOverlay shell = AttachShells(out _);
            m_shells.SetCount(2, 1, 2);
            var plane = new Vector4(1, 0, 0, -1);
            shell.View.SetClipPlane(plane, true);
            var properties = new MaterialPropertyBlock();
            shell.View.GetShownPiece(0).Renderer.GetPropertyBlock(properties);
            Assert.That(properties.GetFloat("_IceCracks"), Is.EqualTo(.5f));
            Assert.That(properties.GetVector("_ClipPlane"), Is.EqualTo(plane));
        }

        [UnityTest]
        public IEnumerator ShakePunchesTheBlockRootThatCarriesItsIcedTiles()
        {
            AttachShells(out Transform[] roots);
            Vector3 original = roots[2].localPosition;
            m_shells.Shake(2);
            yield return null;
            Assert.That(roots[2].localPosition, Is.Not.EqualTo(original), "feedback punches the block root");
        }

        [UnityTest]
        public IEnumerator InterruptedPunchReturnsTheRootImmediately()
        {
            AttachShells(out Transform[] roots);
            Vector3 original = roots[2].localPosition;
            m_shells.Shake(2);
            yield return null;
            m_shells.SetCount(2, 0, 2);
            Assert.That(roots[2].localPosition, Is.EqualTo(original), "an interrupted punch returns the root");
        }

        [UnityTest]
        public IEnumerator PausedThawFreezesItsOpacity()
        {
            BlockIceOverlay shell = AttachShells(out _);
            float authored = m_material.GetFloat("_IceOpacity");
            m_shells.SetCount(2, 0, 2);
            yield return null;
            m_shells.Sync();
            Assert.That(ReadOpacity(shell), Is.LessThan(authored), "the thaw fade runs before pause");
            m_shells.SetPaused(true);
            float paused = ReadOpacity(shell);
            yield return null;
            yield return null;
            m_shells.Sync();
            Assert.That(ReadOpacity(shell), Is.EqualTo(paused), "a paused thaw leaves the opacity untouched");
        }

        [Test]
        public void ReturnAllRemovesTheIceAndReuseStartsWithNoCracks()
        {
            BlockIceOverlay shell = AttachShells(out Transform[] roots);
            ModularBlockView view = shell.View;
            m_shells.SetCount(2, 1, 2);
            m_shells.SetCount(2, 0, 2);
            Assert.That(shell.IsThawing, Is.True, "thawing starts before the look returns");
            m_shells.ReturnAll();
            Assert.That(m_shells.ActiveCount, Is.Zero);
            Assert.That(view.GetShownPiece(0).Renderer.sharedMaterials, Has.Length.EqualTo(1), "the block keeps only its own material");
            m_shells.Attach(2, view, 2, 2);
            Assert.That(m_shells.GetAt(2), Is.SameAs(shell), "the reused look keeps its identity");
            var properties = new MaterialPropertyBlock();
            view.GetShownPiece(0).Renderer.GetPropertyBlock(properties);
            Assert.That(properties.GetFloat("_IceCracks"), Is.Zero);
            Assert.That(shell.IsThawing, Is.False, "Detach resets the thaw flag");
        }

        [UnityTest]
        public IEnumerator ThawCompletesAndReturnsShellToThePool()
        {
            AttachShells(out Transform[] roots);
            // Only the look under test stays attached, so the count can reach zero once it thaws.
            ModularBlockView view = m_shells.GetAt(2).View;
            m_shells.ReturnAll();
            m_shells.Attach(2, view, 2, 2);
            Assert.That(m_shells.ActiveCount, Is.EqualTo(1));
            m_shells.SetCount(2, 0, 2);
            float deadline = Time.realtimeSinceStartup + 1f;
            while (m_shells.ActiveCount > 0 && Time.realtimeSinceStartup < deadline)
            {
                yield return null;
                m_shells.Sync();
            }
            Assert.That(m_shells.ActiveCount, Is.Zero);
        }

        private static float ReadOpacity(BlockIceOverlay shell)
        {
            var properties = new MaterialPropertyBlock();
            shell.View.GetShownPiece(0).Renderer.GetPropertyBlock(properties);
            return properties.GetFloat("_IceOpacity");
        }

        [Test]
        public void CounterUsesOutlinedNumeralAndCachedCountsAndIgnoresPicking()
        {
            m_storage = new GameObject("Ice labels fixture", typeof(Camera));
            IReadOnlyList<Vector2Int>[] cells = { new[] { Vector2Int.zero, Vector2Int.up, Vector2Int.right } };
            var parent = new VisualElement();
            m_labels = new IceCounterLabels(m_storage.GetComponent<Camera>(), parent, cells);
            m_labels.Attach(0, m_storage.transform, 1);
            Label label = m_labels.GetLabel(0);
            Assert.That(m_labels.Layer.ClassListContains("ice-counter-label-layer"), Is.True);
            Assert.That(label.ClassListContains("ice-counter-label"), Is.True);
            Assert.That(label.childCount, Is.EqualTo(1), "the prewarmed inner stroke belongs to the numeral");
            Label fill = (Label)label[0];
            Assert.That(fill.ClassListContains("ice-counter-label__fill"), Is.True);
            Assert.That(fill.pickingMode, Is.EqualTo(PickingMode.Ignore));
            Assert.That(fill.style.unityTextOutlineColor.value, Is.EqualTo(label.style.color.value));
            Assert.That(label.text, Is.EqualTo("1"));
            Assert.That(label.style.backgroundColor.value.a, Is.Zero);
            Assert.That(label.style.color.value.r, Is.EqualTo(k_iceFaceColor.r).Within(.001f));
            Assert.That(label.style.color.value.g, Is.EqualTo(k_iceFaceColor.g).Within(.001f));
            Assert.That(label.style.color.value.b, Is.EqualTo(k_iceFaceColor.b).Within(.001f));
            Assert.That(label.style.unityTextOutlineColor.value.b, Is.LessThan(.4f));
            int writes = m_labels.TextWriteCount;
            m_labels.SetCount(0, 1);
            m_labels.Reproject();
            Assert.That(m_labels.TextWriteCount, Is.EqualTo(writes));
            m_labels.SetCount(0, 20);
            string twenty = label.text;
            m_labels.SetCount(0, 1);
            m_labels.SetCount(0, 20);
            Assert.That(label.text, Is.SameAs(twenty));
            Assert.That(label.text, Is.EqualTo("20"));
            Assert.That(m_labels.GetLabel(0), Is.SameAs(label));
            Assert.That(label.childCount, Is.EqualTo(1));
            Assert.That(label[0], Is.SameAs(fill));
            Assert.That(fill.text, Is.SameAs(twenty));
            Assert.That(m_labels.Layer.pickingMode, Is.EqualTo(PickingMode.Ignore));
            Assert.That(m_labels.GetLabel(0).pickingMode, Is.EqualTo(PickingMode.Ignore));
            Vector3 anchor = IceCounterLabels.GetLabelOffset(cells[0]);
            Assert.That(cells[0], Does.Contain(new Vector2Int((int)anchor.x, (int)anchor.z)));
            m_labels.ReturnAll();
            Assert.That(m_labels.ActiveCount, Is.Zero);
            Assert.That(label.style.display.value, Is.EqualTo(DisplayStyle.None));
            m_labels.Dispose();
            Assert.That(parent.childCount, Is.Zero);
        }

        [Test]
        public void CounterUsesContinuousSolidMidpointAndDeterministicOccupiedFallback()
        {
            Vector3[] expected = { new Vector3(.5f, k_labelHeight, .5f),
                new Vector3(0, k_labelHeight, 0), new Vector3(1, k_labelHeight, 0) };
            for (int shape = 0; shape < s_footprints.Length; shape++)
            {
                Vector2Int[] reversed = (Vector2Int[])s_footprints[shape].Clone();
                System.Array.Reverse(reversed);
                Assert.That(IceCounterLabels.GetLabelOffset(s_footprints[shape]), Is.EqualTo(expected[shape]));
                Assert.That(IceCounterLabels.GetLabelOffset(reversed), Is.EqualTo(expected[shape]));
            }
        }

        [UnityTest]
        public IEnumerator AttachedCounterStaysCentredAndScalesWithProjectedCell()
        {
            m_storage = new GameObject("Attached Ice badge fixture", typeof(Camera), typeof(UIDocument));
            Camera camera = m_storage.GetComponent<Camera>();
            camera.orthographic = true;
            camera.orthographicSize = 5f;
            camera.transform.position = new Vector3(.5f, 6f, -6f);
            camera.transform.LookAt(new Vector3(.5f, k_labelHeight, .5f));
            UIDocument document = m_storage.GetComponent<UIDocument>();
            document.panelSettings = AssetDatabase.LoadAssetAtPath<PanelSettings>(
                "Assets/ColorBlockJam/Data/UI/BoardOverlayPanelSettings.asset");
            Assert.That(document.panelSettings, Is.Not.Null);
            Transform target = new GameObject("Badge target").transform;
            target.SetParent(m_storage.transform, false);
            target.position = Vector3.zero;
            target.rotation = Quaternion.identity;
            IReadOnlyList<Vector2Int>[] cells =
                { new[] { Vector2Int.zero, Vector2Int.up, Vector2Int.right, Vector2Int.one } };
            m_labels = new IceCounterLabels(camera, document.rootVisualElement, cells);
            m_labels.Attach(0, target, 20);
            yield return null;
            yield return null;
            Assert.That(m_labels.Layer.panel, Is.Not.Null);
            m_labels.Reproject();
            yield return null;
            Vector3 screen = camera.WorldToScreenPoint(target.TransformPoint(new Vector3(.5f, k_labelHeight, .5f)));
            Vector2 anchor = RuntimePanelUtils.ScreenToPanel(m_labels.Layer.panel,
                new Vector2(screen.x, Screen.height - screen.y));
            Label label = m_labels.GetLabel(0);
            Assert.That(label.worldBound.center.x, Is.EqualTo(anchor.x).Within(.1f));
            Assert.That(label.worldBound.center.y, Is.EqualTo(anchor.y).Within(.1f));
            Assert.That(label.resolvedStyle.visibility, Is.EqualTo(Visibility.Visible));
            Assert.That(label.childCount, Is.EqualTo(1));
            Label fill = (Label)label[0];
            Assert.That(fill.worldBound.center, Is.EqualTo(label.worldBound.center));
            Assert.That(fill.resolvedStyle.fontSize, Is.EqualTo(label.resolvedStyle.fontSize));
            Assert.That(fill.resolvedStyle.unityTextAlign, Is.EqualTo(label.resolvedStyle.unityTextAlign));
            Assert.That(fill.resolvedStyle.color, Is.EqualTo(label.resolvedStyle.color));
            Assert.That(fill.resolvedStyle.unityFontStyleAndWeight, Is.EqualTo(label.resolvedStyle.unityFontStyleAndWeight));
            Vector3 cellScreen = camera.WorldToScreenPoint(target.TransformPoint(new Vector3(1.5f, k_labelHeight, .5f)));
            Vector2 cellPoint = RuntimePanelUtils.ScreenToPanel(m_labels.Layer.panel,
                new Vector2(cellScreen.x, Screen.height - cellScreen.y));
            float projectedCell = Vector2.Distance(anchor, cellPoint);
            Assert.That(label.resolvedStyle.fontSize / projectedCell, Is.InRange(.6f, .7f),
                "two-digit counts reserve room for both strokes inside a single cell");
            Assert.That(fill.resolvedStyle.unityTextOutlineWidth / projectedCell, Is.InRange(.02f, .03f));
            Assert.That((label.resolvedStyle.unityTextOutlineWidth - fill.resolvedStyle.unityTextOutlineWidth) /
                projectedCell, Is.InRange(.035f, .045f), "navy edge matches the reference around the expanded cyan face");
            Vector2 countSize = label.MeasureTextSize("20", 0f, VisualElement.MeasureMode.Undefined,
                0f, VisualElement.MeasureMode.Undefined);
            Assert.That(label.contentRect.width, Is.GreaterThanOrEqualTo(countSize.x + 2f * label.resolvedStyle.unityTextOutlineWidth),
                "the largest supported count fits without clipping");
            float labelWidth = label.worldBound.width;
            float fontSize = label.resolvedStyle.fontSize;
            m_labels.SetCount(0, 10);
            Assert.That(label.style.fontSize.value.value, Is.EqualTo(fontSize).Within(.001f));
            m_labels.SetCount(0, 9);
            Assert.That(label.style.fontSize.value.value, Is.EqualTo(projectedCell * .75f).Within(.001f));
            Assert.That(fill.style.fontSize.value, Is.EqualTo(label.style.fontSize.value));
            m_labels.SetCount(0, 20);
            m_labels.SetCount(0, 0);
            m_labels.SetCount(0, 9);
            Assert.That(label.style.fontSize.value.value, Is.EqualTo(projectedCell * .75f).Within(.001f),
                "reshowing a single digit clears the previous two-digit size");
            Assert.That(fill.style.fontSize.value, Is.EqualTo(label.style.fontSize.value));
            yield return null;
            Assert.That(label.resolvedStyle.fontSize / projectedCell, Is.EqualTo(.75f).Within(.001f),
                "single-digit counts retain the large reference size");
            fontSize = label.resolvedStyle.fontSize;
            Assert.That(label.worldBound.center.x, Is.EqualTo(anchor.x).Within(.1f), "digit width cannot move the numeral");
            Assert.That(label.worldBound.width, Is.EqualTo(labelWidth).Within(.1f));
            camera.orthographicSize *= 2f;
            m_labels.Reproject();
            yield return null;
            Assert.That(label.resolvedStyle.fontSize, Is.EqualTo(fontSize * .5f).Within(.1f),
                "camera reframing scales the numeral with its block");
        }
    }
}
#endif
