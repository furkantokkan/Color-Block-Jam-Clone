using System.Collections;
using ColorBlockJam.Presentation.Boosters;
using NUnit.Framework;
using PrimeTween;
using UnityEngine;
using UnityEngine.TestTools;

namespace ColorBlockJam.Tests.Gameplay.PlayMode
{
    /// <summary>
    /// The Time Freeze screen-edge frost (B-25, AC-3, AC-15): one quad in front of the camera, created on Home and never
    /// again, drawn only while there is frost, faded in and out by one view-owned tween that Suspend holds and Teardown
    /// stops, and inert without a material.
    /// </summary>
    public sealed class TimeFreezeFrostViewTests
    {
        private GameObject m_cameraObject;
        private Camera m_camera;
        private Material m_material;
        private TimeFreezeFrostView m_frost;

        [SetUp]
        public void SetUp()
        {
            m_cameraObject = new GameObject("Frost test camera", typeof(Camera));
            m_camera = m_cameraObject.GetComponent<Camera>();
            m_camera.fieldOfView = 60f;
            m_camera.nearClipPlane = 0.3f;
            Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
            Assert.That(shader, Is.Not.Null);
            m_material = new Material(shader);
        }

        [TearDown]
        public void TearDown()
        {
            m_frost?.Dispose();
            m_frost = null;
            Object.Destroy(m_cameraObject);
            Object.Destroy(m_material);
        }

        [Test]
        public void HomePreparationCreatesOneHiddenQuadThatCoversTheView()
        {
            m_frost = new TimeFreezeFrostView(new TimeFreezeFrostContext(m_camera, m_material));
            Assert.That(m_frost.IsPrepared, Is.False, "nothing is created before Home");

            Assert.That(m_frost.Prepare(), Is.True);
            GameObject quad = m_frost.Quad;
            Assert.That(m_frost.Prepare(), Is.True);

            Assert.That(m_frost.Quad, Is.SameAs(quad), "a second Prepare only refits");
            Assert.That(quad.transform.parent, Is.SameAs(m_camera.transform));
            Assert.That(m_frost.IsVisible, Is.False);
            Assert.That(m_frost.Amount, Is.Zero);
            float distance = quad.transform.localPosition.z;
            Assert.That(distance, Is.GreaterThan(m_camera.nearClipPlane));
            float height = 2f * distance * Mathf.Tan(30f * Mathf.Deg2Rad);
            Assert.That(quad.transform.localScale.y, Is.GreaterThanOrEqualTo(height));
            Assert.That(quad.transform.localScale.x, Is.GreaterThanOrEqualTo(height * m_camera.aspect));
            Assert.That(quad.GetComponent<MeshRenderer>().sharedMaterial, Is.SameAs(m_material), "the shared material, no instance");
        }

        [UnityTest]
        public IEnumerator AFreezeFadesTheFrostInAndItsEndFadesItOut()
        {
            m_frost = new TimeFreezeFrostView(new TimeFreezeFrostContext(m_camera, m_material));
            m_frost.Prepare();

            m_frost.SetFrozen(true);
            Assert.That(m_frost.IsFading, Is.True);
            yield return WaitSeconds(TimeFreezeFrostView.k_fadeSeconds + 0.15f);

            Assert.That(m_frost.Amount, Is.EqualTo(1f));
            Assert.That(m_frost.IsVisible, Is.True);
            Assert.That(ReadAmount(), Is.EqualTo(1f), "the amount reaches the renderer through its property block");

            m_frost.SetFrozen(false);
            yield return WaitSeconds(TimeFreezeFrostView.k_fadeSeconds + 0.15f);

            Assert.That(m_frost.Amount, Is.Zero);
            Assert.That(m_frost.IsVisible, Is.False);
            Assert.That(Tween.GetTweensCount(m_frost), Is.Zero);
        }

        [UnityTest]
        public IEnumerator SuspendHoldsTheFadeAndTeardownClearsTheFrostAtOnce()
        {
            m_frost = new TimeFreezeFrostView(new TimeFreezeFrostContext(m_camera, m_material));
            m_frost.Prepare();
            m_frost.SetFrozen(true);
            m_frost.SetPaused(true);
            float held = m_frost.Amount;
            yield return WaitSeconds(TimeFreezeFrostView.k_fadeSeconds + 0.1f);

            Assert.That(m_frost.IsPaused, Is.True);
            Assert.That(m_frost.Amount, Is.EqualTo(held), "a paused fade holds its amount");
            Assert.That(held, Is.LessThan(1f));

            m_frost.SetPaused(false);
            yield return WaitSeconds(TimeFreezeFrostView.k_fadeSeconds + 0.15f);
            Assert.That(m_frost.Amount, Is.EqualTo(1f), "Resume continues the fade");

            m_frost.SetFrozen(false);
            m_frost.Stop();

            Assert.That(m_frost.Amount, Is.Zero);
            Assert.That(m_frost.IsVisible, Is.False);
            Assert.That(m_frost.IsFrozen, Is.False);
            Assert.That(Tween.GetTweensCount(m_frost), Is.Zero, "no fade survives Teardown");
        }

        [Test]
        public void WithoutAMaterialTheFrostCreatesAndDrawsNothing()
        {
            m_frost = new TimeFreezeFrostView(new TimeFreezeFrostContext(m_camera, null));

            Assert.That(m_frost.Prepare(), Is.False);
            m_frost.SetFrozen(true);
            m_frost.SetPaused(true);
            m_frost.Stop();

            Assert.That(m_frost.IsPrepared, Is.False);
            Assert.That(m_camera.transform.childCount, Is.Zero);
            Assert.That(Tween.GetTweensCount(m_frost), Is.Zero);
        }

        [UnityTest]
        public IEnumerator DisposeDestroysTheQuadAndLaterCallsDoNothing()
        {
            m_frost = new TimeFreezeFrostView(new TimeFreezeFrostContext(m_camera, m_material));
            m_frost.Prepare();
            GameObject quad = m_frost.Quad;
            m_frost.SetFrozen(true);

            m_frost.Dispose();
            yield return null;

            Assert.That(quad == null, Is.True);
            Assert.That(Tween.GetTweensCount(m_frost), Is.Zero);
            Assert.DoesNotThrow(() =>
            {
                m_frost.SetFrozen(false);
                m_frost.SetPaused(false);
                m_frost.Stop();
                m_frost.Prepare();
            });
            Assert.That(m_frost.IsPrepared, Is.False);
        }

        private float ReadAmount()
        {
            MaterialPropertyBlock block = new MaterialPropertyBlock();
            m_frost.Quad.GetComponent<MeshRenderer>().GetPropertyBlock(block);
            return block.GetFloat(TimeFreezeFrostView.AmountProperty);
        }

        private static IEnumerator WaitSeconds(float seconds)
        {
            float end = Time.realtimeSinceStartup + seconds;
            while (Time.realtimeSinceStartup < end)
            {
                yield return null;
            }
        }
    }
}
