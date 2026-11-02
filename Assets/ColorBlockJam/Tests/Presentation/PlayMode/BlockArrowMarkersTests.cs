#if UNITY_EDITOR
using System;
using System.Collections;
using ColorBlockJam.Presentation.Board;
using ColorBlockJam.Runtime.Art;
using ColorBlockJam.Runtime.Levels;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.TestTools.Constraints;
using Is = UnityEngine.TestTools.Constraints.Is;
using Object = UnityEngine.Object;

namespace ColorBlockJam.Tests.PlayMode
{
    /// <summary>
    /// Verifies the shared Arrow marker owner (TASK-20260926-212320271-98b25efa-level-editor-arrow-markers revision 1,
    /// catalog BLK-01): Prepare warms one pool per Arrow_N from the board piece catalog, Attach parents the variant's
    /// marker to the block root at the footprint's bounding-box centre, turned for the axis and tinted, SetClipPlane
    /// clips it, Return and ReturnAll put it back without instantiating, exhaustion rejects and Dispose leaves nothing.
    /// </summary>
    public sealed class BlockArrowMarkersTests
    {
        private const string k_catalogPath = "Assets/ColorBlockJam/Data/Board/BoardPieceCatalog.asset";
        private static readonly int s_paletteUVId = Shader.PropertyToID("_PaletteUV");
        private static readonly int s_clipPlaneId = Shader.PropertyToID("_ClipPlane");
        private static readonly int s_clipEnabledId = Shader.PropertyToID("_ClipEnabled");
        // Channels of 0 and 1 read back unchanged in either colour space; the default tint is white.
        private static readonly AtlasColor s_tint = AtlasColor.FromCell(new Vector2Int(13, 2));
        private static readonly Vector4 s_plane = new Vector4(0f, 0f, -1f, 2.5f);

        private static readonly Vector2Int[] s_square =
        {
            new Vector2Int(0, 0), new Vector2Int(1, 0), new Vector2Int(0, 1), new Vector2Int(1, 1)
        };
        private static readonly Vector2Int[] s_triple = { new Vector2Int(0, 0), new Vector2Int(1, 0), new Vector2Int(2, 0) };
        private static readonly Vector2Int[] s_single = { new Vector2Int(0, 0) };

        private BoardPieceCatalog m_catalog;
        private GameObject m_storage;
        private GameObject m_blockA;
        private GameObject m_blockB;
        private BlockArrowMarkers m_markers;

        [SetUp]
        public void SetUp()
        {
            m_catalog = AssetDatabase.LoadAssetAtPath<BoardPieceCatalog>(k_catalogPath);
            Assert.That(m_catalog, Is.Not.Null);
            m_storage = new GameObject("Arrow marker storage (test)");
            m_blockA = new GameObject("Block A (test)");
            m_blockB = new GameObject("Block B (test)");
            m_blockA.transform.SetParent(m_storage.transform, false);
            m_blockB.transform.SetParent(m_storage.transform, false);
            m_blockB.transform.localPosition = new Vector3(3f, 0f, 0f);
            m_markers = new BlockArrowMarkers(m_catalog, m_storage.transform);
        }

        [TearDown]
        public void TearDown()
        {
            m_markers.Dispose();
            Object.DestroyImmediate(m_storage);
        }

        [Test]
        public void PrepareWarmsEachVariantsPoolFromTheCatalogUnderStorage()
        {
            m_markers.Prepare(new[] { 1, 2, 0 });

            Assert.That(m_markers.GetPreparedCount(1), Is.EqualTo(1));
            Assert.That(m_markers.GetPreparedCount(2), Is.EqualTo(2));
            Assert.That(m_markers.GetPreparedCount(3), Is.Zero, "A variant nobody needs gets no pool.");
            Assert.That(m_markers.PreparedCount, Is.EqualTo(3));
            Assert.That(CountInactiveMarkersUnder(m_storage.transform), Is.EqualTo(3), "Warmed markers wait, inactive, under storage.");
            Assert.That(m_markers.AttachedCount, Is.Zero);
        }

        [Test]
        public void AttachParentsTheVariantToTheRootAtTheBoxCentreTurnedForItsAxisAndTinted()
        {
            m_markers.Prepare(new[] { 0, 1, 1 });

            BoardPieceView vertical = m_markers.Attach(m_blockA.transform, s_square, LevelEffectAxis.Vertical, s_tint);
            BoardPieceView horizontal = m_markers.Attach(m_blockB.transform, s_triple, LevelEffectAxis.Horizontal, s_tint);

            AssertMarker(vertical, m_blockA.transform, 2, new Vector3(0.5f, 0f, 0.5f), Vector3.forward);
            AssertMarker(horizontal, m_blockB.transform, 3, new Vector3(1f, 0f, 0f), Vector3.right);
            Assert.That(m_markers.AttachedCount, Is.EqualTo(2));
        }

        [Test]
        public void SetClipPlaneClipsTheMarkerAndReturnClearsItForTheNextAttach()
        {
            m_markers.Prepare(new[] { 1, 0, 0 });
            BoardPieceView marker = m_markers.Attach(m_blockA.transform, s_single, LevelEffectAxis.Vertical, s_tint);

            m_markers.SetClipPlane(marker, s_plane, true);

            MaterialPropertyBlock properties = ReadProperties(marker);
            Assert.That(properties.GetFloat(s_clipEnabledId), Is.EqualTo(1f));
            Assert.That(properties.GetVector(s_clipPlaneId), Is.EqualTo(s_plane));

            m_markers.Return(marker);
            BoardPieceView again = m_markers.Attach(m_blockB.transform, s_single, LevelEffectAxis.Horizontal, s_tint);

            Assert.That(again, Is.SameAs(marker), "The pool hands back the same marker.");
            Assert.That(ReadProperties(again).GetFloat(s_clipEnabledId), Is.Zero, "The next attach draws it unclipped.");
            Assert.That(again.transform.parent, Is.SameAs(m_blockB.transform));
        }

        [Test]
        public void ReturnAndReturnAllPutMarkersBackUnderStorageWithoutInstantiating()
        {
            m_markers.Prepare(new[] { 1, 1, 0 });
            int objects = CountObjects();
            BoardPieceView one = m_markers.Attach(m_blockA.transform, s_single, LevelEffectAxis.Vertical, s_tint);
            BoardPieceView two = m_markers.Attach(m_blockB.transform, s_square, LevelEffectAxis.Horizontal, s_tint);

            m_markers.Return(one);

            Assert.That(one.transform.parent, Is.SameAs(m_storage.transform), "A returned marker leaves its block root.");
            Assert.That(one.gameObject.activeSelf, Is.False);
            Assert.That(m_markers.AttachedCount, Is.EqualTo(1));
            Assert.Throws<ArgumentException>(() => m_markers.Return(one), "A returned marker cannot return twice.");

            m_markers.ReturnAll();

            Assert.That(two.transform.parent, Is.SameAs(m_storage.transform));
            Assert.That(two.gameObject.activeSelf, Is.False);
            Assert.That(m_markers.AttachedCount, Is.Zero);
            Assert.That(m_blockA.transform.childCount + m_blockB.transform.childCount, Is.Zero, "No marker rides a block root.");
            Assert.That(CountObjects(), Is.EqualTo(objects), "Attach and return create no object.");
        }

        [Test]
        public void AnExhaustedPoolRejectsInsteadOfGrowing()
        {
            m_markers.Prepare(new[] { 1, 0, 0 });
            m_markers.Attach(m_blockA.transform, s_single, LevelEffectAxis.Vertical, s_tint);
            int objects = CountObjects();

            Assert.Throws<InvalidOperationException>(() =>
                m_markers.Attach(m_blockB.transform, s_single, LevelEffectAxis.Vertical, s_tint));
            Assert.Throws<InvalidOperationException>(() =>
                m_markers.Attach(m_blockB.transform, s_square, LevelEffectAxis.Vertical, s_tint), "Arrow_2 was never prepared.");

            Assert.That(CountObjects(), Is.EqualTo(objects), "A rejected attach creates nothing.");
            Assert.That(m_markers.AttachedCount, Is.EqualTo(1));
        }

        [Test]
        public void PrepareOnlyGrowsAndRejectsWhileMarkersAreAttached()
        {
            m_markers.Prepare(new[] { 2, 0, 0 });
            m_markers.Prepare(new[] { 1, 0, 0 });
            Assert.That(m_markers.GetPreparedCount(1), Is.EqualTo(2), "A smaller need keeps the pool.");

            m_markers.Prepare(new[] { 3, 0, 1 });
            Assert.That(m_markers.GetPreparedCount(1), Is.EqualTo(3));
            Assert.That(m_markers.GetPreparedCount(3), Is.EqualTo(1));

            m_markers.Attach(m_blockA.transform, s_single, LevelEffectAxis.Vertical, s_tint);
            Assert.Throws<InvalidOperationException>(() => m_markers.Prepare(new[] { 4, 0, 1 }));
        }

        [Test]
        public void AMissingArrowPrefabOrABadFootprintIsRejected()
        {
            BoardPieceCatalog empty = ScriptableObject.CreateInstance<BoardPieceCatalog>();
            BlockArrowMarkers markers = new BlockArrowMarkers(empty, m_storage.transform);
            try
            {
                Assert.That(empty.TryGetArrow(1, out _), Is.False);
                InvalidOperationException missing =
                    Assert.Throws<InvalidOperationException>(() => markers.Prepare(new[] { 0, 1, 0 }));
                Assert.That(missing.Message, Does.Contain("Arrow_2"));
                Assert.That(markers.PreparedCount, Is.Zero);
            }
            finally
            {
                markers.Dispose();
                Object.DestroyImmediate(empty);
            }

            m_markers.Prepare(new[] { 1, 0, 0 });
            Assert.Throws<ArgumentException>(() =>
                m_markers.Attach(m_blockA.transform, Array.Empty<Vector2Int>(), LevelEffectAxis.Vertical, s_tint));
            Assert.Throws<ArgumentException>(() =>
                m_markers.Attach(m_blockA.transform, s_single, (LevelEffectAxis)7, s_tint));
            Assert.That(m_markers.AttachedCount, Is.Zero);
        }

        [Test]
        public void ClippingAndReturningAMarkerAllocateNothing()
        {
            // The Playing calls: an exit clips the marker, and Sync returns it once the exit ends. One warm-up round
            // first, so only the measured calls count.
            m_markers.Prepare(new[] { 1, 0, 0 });
            BoardPieceView marker = m_markers.Attach(m_blockA.transform, s_single, LevelEffectAxis.Vertical, s_tint);
            m_markers.SetClipPlane(marker, s_plane, true);
            m_markers.Return(marker);
            marker = m_markers.Attach(m_blockA.transform, s_single, LevelEffectAxis.Vertical, s_tint);

            Assert.That(() => m_markers.SetClipPlane(marker, s_plane, true), Is.Not.AllocatingGCMemory());
            Assert.That(() => m_markers.Return(marker), Is.Not.AllocatingGCMemory());
            Assert.That(m_markers.AttachedCount, Is.Zero);
        }

        [UnityTest]
        public IEnumerator ReturnAllAndDisposeSkipMarkersASceneUnloadDestroyedWithTheirRoots()
        {
            m_markers.Prepare(new[] { 1, 1, 0 });
            m_markers.Attach(m_blockA.transform, s_square, LevelEffectAxis.Vertical, s_tint);
            m_markers.Attach(m_blockB.transform, s_single, LevelEffectAxis.Vertical, s_tint);

            // A scene unload destroys the block roots and the markers riding on them.
            Object.DestroyImmediate(m_blockA);
            Object.DestroyImmediate(m_blockB);

            Assert.DoesNotThrow(() => m_markers.ReturnAll());
            Assert.That(m_markers.AttachedCount, Is.Zero);
            Assert.DoesNotThrow(() => m_markers.Dispose());
            yield return null;

            Assert.That(CountMarkersUnder(m_storage.transform), Is.Zero, "Dispose leaves no marker.");
            Assert.That(m_markers.PreparedCount, Is.Zero);
        }

        [UnityTest]
        public IEnumerator AMarkerDestroyedWhileAttachedIsNotReusedUntilPrepareReplacesItsPool()
        {
            m_markers.Prepare(new[] { 1, 1, 0 });
            BoardPieceView deleted = m_markers.Attach(m_blockA.transform, s_square, LevelEffectAxis.Vertical, s_tint);
            m_markers.Attach(m_blockB.transform, s_single, LevelEffectAxis.Vertical, s_tint);

            // The block root survives; only its marker is deleted, as in the Hierarchy.
            Object.DestroyImmediate(deleted.gameObject);
            Assert.DoesNotThrow(() => m_markers.Return(deleted), "A destroyed marker detaches without error.");
            Assert.That(m_markers.AttachedCount, Is.EqualTo(1));
            int objects = CountObjects();

            InvalidOperationException exhausted = Assert.Throws<InvalidOperationException>(() =>
                m_markers.Attach(m_blockA.transform, s_square, LevelEffectAxis.Vertical, s_tint));
            Assert.That(exhausted.Message, Does.Contain("Arrow_2").And.Contain("exhausted"),
                "The destroyed marker's slot is not free capacity.");
            Assert.That(CountObjects(), Is.EqualTo(objects), "The rejected attach instantiated nothing.");

            // The next preparation, with the same counts, replaces the pool that lost its marker.
            m_markers.ReturnAll();
            m_markers.Prepare(new[] { 1, 1, 0 });
            Assert.That(m_markers.GetPreparedCount(2), Is.EqualTo(1));
            Assert.That(m_markers.PreparedCount, Is.EqualTo(2));

            BoardPieceView replacement = m_markers.Attach(m_blockA.transform, s_square, LevelEffectAxis.Vertical, s_tint);
            Assert.That(replacement != null, Is.True, "The new pool hands out a live marker.");
            AssertMarker(replacement, m_blockA.transform, 2, new Vector3(0.5f, 0f, 0.5f), Vector3.forward);
            Assert.That(CountMarkersUnder(m_storage.transform), Is.EqualTo(2), "Only the warmed markers exist.");

            m_markers.Dispose();
            yield return null;

            Assert.That(CountMarkersUnder(m_storage.transform), Is.Zero, "Dispose leaves no untracked marker.");
        }

        [UnityTest]
        public IEnumerator DisposeReturnsAndDestroysEveryMarker()
        {
            m_markers.Prepare(new[] { 1, 1, 1 });
            m_markers.Attach(m_blockA.transform, s_square, LevelEffectAxis.Vertical, s_tint);

            m_markers.Dispose();
            yield return null;

            Assert.That(CountMarkersUnder(m_storage.transform), Is.Zero, "Dispose leaves no marker, attached or pooled.");
            Assert.That(m_markers.PreparedCount, Is.Zero);
            Assert.Throws<ObjectDisposedException>(() =>
                m_markers.Attach(m_blockA.transform, s_single, LevelEffectAxis.Vertical, s_tint));
        }

        // The marker shows the catalog's Arrow_N mesh on the shared material, rides on the block root at the given local
        // position, is long along the given root direction and carries the owner's tint.
        private void AssertMarker(BoardPieceView marker, Transform root, int variant, Vector3 localPosition, Vector3 along)
        {
            Assert.That(m_catalog.TryGetArrow(variant, out BoardPieceView prefab), Is.True);
            Assert.That(marker.Filter.sharedMesh, Is.SameAs(prefab.Filter.sharedMesh), "Arrow_" + variant);
            Assert.That(marker.Renderer.sharedMaterial, Is.SameAs(prefab.Renderer.sharedMaterial), "One shared material.");
            Assert.That(marker.gameObject.activeSelf, Is.True);
            Assert.That(marker.transform.parent, Is.SameAs(root));
            Assert.That(Vector3.Distance(marker.transform.localPosition, localPosition), Is.LessThan(1e-4f), "Box centre.");
            Assert.That(marker.transform.localScale, Is.EqualTo(Vector3.one), "Not stretched.");
            Vector3 forward = root.InverseTransformDirection(marker.transform.forward);
            Assert.That(Vector3.Dot(forward, along), Is.EqualTo(1f).Within(1e-4f), "Long along its axis.");
            Assert.That(ReadProperties(marker).GetVector(s_paletteUVId), Is.EqualTo(s_tint.UV));
        }

        private static MaterialPropertyBlock ReadProperties(BoardPieceView marker)
        {
            MaterialPropertyBlock properties = new MaterialPropertyBlock();
            marker.Renderer.GetPropertyBlock(properties);
            return properties;
        }

        private int CountObjects() => m_storage.GetComponentsInChildren<Transform>(true).Length;

        private static int CountMarkersUnder(Transform root) => root.GetComponentsInChildren<BoardPieceView>(true).Length;

        private static int CountInactiveMarkersUnder(Transform root)
        {
            int count = 0;
            foreach (BoardPieceView view in root.GetComponentsInChildren<BoardPieceView>(true))
            {
                count += view.transform.parent == root && !view.gameObject.activeSelf ? 1 : 0;
            }

            return count;
        }
    }
}
#endif
