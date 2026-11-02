using ColorBlockJam.Presentation.UI;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace ColorBlockJam.Tests.Gameplay.Hud
{
    /// <summary>
    /// The Level Complete effects (reference 05, DEC-173): the confetti stage's lifecycle (created on Home with the camera
    /// off, a render texture only during a show, everything released by Stop and Dispose), the title's arc placement and
    /// the letter stacks it builds, and the ray element's attribute limits.
    /// </summary>
    public sealed class LevelCompleteEffectsTests
    {
        private const string k_confettiPath = "Assets/ColorBlockJam/Prefabs/Effects/Confetti/ConfettiBlastRainbow.prefab";

        private GameObject m_parent;
        private UiParticleOverlay m_overlay;

        [SetUp]
        public void SetUp()
        {
            m_parent = new GameObject("Level Complete effects test");
            ParticleSystem prefab = AssetDatabase.LoadAssetAtPath<ParticleSystem>(k_confettiPath);
            Assert.That(prefab, Is.Not.Null, k_confettiPath);
            m_overlay = new UiParticleOverlay(prefab, m_parent.transform, 7f);
        }

        [TearDown]
        public void TearDown()
        {
            m_overlay.Dispose();
            Object.DestroyImmediate(m_parent);
        }

        [Test]
        public void PrepareBuildsTheStageOnceWithTheCameraOffAndOneBurstOnly()
        {
            Assert.That(m_overlay.Prepare(), Is.True);
            Assert.That(m_overlay.Prepare(), Is.True, "a second call keeps the stage");

            Assert.That(m_parent.transform.childCount, Is.EqualTo(1), "one stage");
            Camera camera = m_overlay.Camera;
            Assert.That(camera.enabled, Is.False, "nothing renders before a show");
            Assert.That(camera.orthographic, Is.True);
            Assert.That(camera.orthographicSize, Is.EqualTo(3.5f), "half the 7 unit view height");
            Assert.That(camera.targetTexture, Is.Null);
            Assert.That(camera.cullingMask, Is.EqualTo(1 << LayerMask.NameToLayer("UI")), "the camera sees only the stage");
            Assert.That(m_overlay.Target, Is.Null, "no render texture between shows");

            foreach (ParticleSystem system in m_parent.GetComponentsInChildren<ParticleSystem>(true))
            {
                Assert.That(system.main.playOnAwake, Is.False, system.name);
                Assert.That(system.main.loop, Is.False, system.name);
                Assert.That(system.gameObject.layer, Is.EqualTo(LayerMask.NameToLayer("UI")), system.name);
            }

            // ConfettiBlastRainbow bursts at 0 s and its longest particle lives 2.2 s.
            Assert.That(m_overlay.ShowSeconds, Is.EqualTo(2.2f).Within(0.01f));
        }

        [Test]
        public void PlayShowsTheCameraImageUntilStopReleasesIt()
        {
            m_overlay.Prepare();
            VisualElement surface = new VisualElement();
            surface.style.display = DisplayStyle.None;

            Assert.That(m_overlay.Play(surface, null), Is.True);
            Assert.That(m_overlay.IsPlaying, Is.True);
            Assert.That(m_overlay.Target, Is.Not.Null);
            Assert.That(m_overlay.Camera.targetTexture, Is.SameAs(m_overlay.Target));
            Assert.That(surface.style.display.value, Is.EqualTo(DisplayStyle.Flex));

            // Unity 2022.3 does not read an inline render-texture background back (the style returns no value), so the
            // texture on the surface is proven by the Level Complete captures instead of here.
            m_overlay.Stop();

            Assert.That(m_overlay.IsPlaying, Is.False);
            Assert.That(m_overlay.Target, Is.Null, "the texture lives only during a show");
            Assert.That(m_overlay.Camera.enabled, Is.False);
            Assert.That(m_overlay.Camera.targetTexture, Is.Null);
            Assert.That(surface.style.display.value, Is.EqualTo(DisplayStyle.None));
        }

        [Test]
        public void WithoutAPrefabNothingIsCreatedAndPlayIsRefused()
        {
            UiParticleOverlay empty = new UiParticleOverlay(null, m_parent.transform, 7f);

            Assert.That(empty.Prepare(), Is.False);
            Assert.That(empty.Play(new VisualElement(), null), Is.False);
            Assert.That(m_parent.transform.childCount, Is.EqualTo(0));
            empty.Dispose();
        }

        [Test]
        public void DisposeDestroysTheStageAndRefusesLaterShows()
        {
            m_overlay.Prepare();
            m_overlay.Play(new VisualElement(), null);

            m_overlay.Dispose();

            Assert.That(m_parent.transform.childCount, Is.EqualTo(0));
            Assert.That(m_overlay.Target, Is.Null);
            Assert.That(m_overlay.Prepare(), Is.False);
            Assert.That(m_overlay.Play(new VisualElement(), null), Is.False);
        }

        [TestCase(0f, 2700f, 0f, 0f)]
        [TestCase(270f, 2700f, 13.53f, 5.74f)]
        [TestCase(-270f, 2700f, 13.53f, -5.74f)]
        [TestCase(270f, 0f, 0f, 0f)]
        [TestCase(3000f, 2700f, 0f, 0f)]
        public void ALetterDropsAndTurnsOnTheArc(float offset, float radius, float expectedDrop, float expectedDegrees)
        {
            ArcTextElement.GetArcPlacement(offset, radius, out float drop, out float degrees);

            Assert.That(drop, Is.EqualTo(expectedDrop).Within(0.01f));
            Assert.That(degrees, Is.EqualTo(expectedDegrees).Within(0.01f));
        }

        [Test]
        public void ArcTextBuildsOneFourLayerStackPerLetter()
        {
            ArcTextElement line = new ArcTextElement { Text = "LEVEL", Radius = 2700f };

            Assert.That(line.childCount, Is.EqualTo(5));
            VisualElement first = line[0];
            Assert.That(first.ClassListContains(ArcTextElement.LetterUssClassName), Is.True);
            Assert.That(first.childCount, Is.EqualTo(4));
            Assert.That(first[0].ClassListContains(ArcTextElement.ShadowUssClassName), Is.True);
            Assert.That(first[1].ClassListContains(ArcTextElement.RimUssClassName), Is.True);
            Assert.That(first[2].ClassListContains(ArcTextElement.StrokeUssClassName), Is.True);
            Assert.That(first[3].ClassListContains(ArcTextElement.FaceUssClassName), Is.True);
            Assert.That(((Label)first[3]).text, Is.EqualTo("L"));
            Assert.That(((Label)line[4][0]).text, Is.EqualTo("L"));

            line.Text = "COMPLETE";
            Assert.That(line.childCount, Is.EqualTo(8), "a new text rebuilds the letters");
        }

        [Test]
        public void SunburstKeepsItsAttributesInRange()
        {
            SunburstElement rays = new SunburstElement { RayCount = 500, RayWidth = -3f, GlowRadius = -1f };

            Assert.That(rays.RayCount, Is.EqualTo(64));
            Assert.That(rays.RayWidth, Is.EqualTo(0f));
            Assert.That(rays.GlowRadius, Is.EqualTo(0f));
            Assert.That(rays.pickingMode, Is.EqualTo(PickingMode.Ignore));
        }
    }
}
