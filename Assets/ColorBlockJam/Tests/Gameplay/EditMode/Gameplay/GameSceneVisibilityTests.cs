using ColorBlockJam.Gameplay.Composition;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UIElements;

namespace ColorBlockJam.Tests.Gameplay.Gameplay
{
    /// <summary>
    /// Proves how a Game scene prepared behind another scene stays out of sight (DEC-199): Hide turns off its camera,
    /// audio listener and lights and moves its play area far away; Show moves the play area and the camera framed with
    /// it back by the same offset and turns on only what Hide turned off.
    /// </summary>
    public sealed class GameSceneVisibilityTests
    {
        private GameObject m_cameraObject;
        private Camera m_camera;
        private AudioListener m_listener;
        private Light m_light;
        private Light m_offLight;
        private Transform m_playArea;

        [SetUp]
        public void SetUp()
        {
            m_cameraObject = new GameObject("visibility camera");
            m_camera = m_cameraObject.AddComponent<Camera>();
            m_listener = m_cameraObject.AddComponent<AudioListener>();
            m_light = new GameObject("visibility light").AddComponent<Light>();
            m_offLight = new GameObject("visibility light that stays off").AddComponent<Light>();
            m_offLight.enabled = false;
            m_playArea = new GameObject("visibility play area").transform;
            m_playArea.position = new Vector3(1f, 0f, 2f);
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(m_cameraObject);
            Object.DestroyImmediate(m_light.gameObject);
            Object.DestroyImmediate(m_offLight.gameObject);
            Object.DestroyImmediate(m_playArea.gameObject);
        }

        [Test]
        public void HideTurnsTheSceneOffAndMovesItsPlayAreaFarAway()
        {
            var visibility = new GameSceneVisibility(m_camera, m_playArea, new UIDocument[0]);

            visibility.Hide();

            Assert.That(visibility.IsHidden, Is.True);
            Assert.That(m_camera.enabled, Is.False, "the camera draws nothing");
            Assert.That(m_listener.enabled, Is.False, "one audio listener stays in the visible scene");
            Assert.That(m_light.enabled, Is.False, "the light does not light the visible scene");
            Assert.That(m_playArea.position, Is.EqualTo(new Vector3(1f, 0f, 2f) + GameSceneVisibility.HiddenOffset));
            Assert.That(GameSceneVisibility.HiddenOffset.magnitude, Is.GreaterThan(1000f),
                "beyond the Hub camera's far plane");
        }

        [Test]
        public void ShowBringsBackOnlyWhatHideTurnedOffWithTheCameraFramedAtTheOffset()
        {
            var visibility = new GameSceneVisibility(m_camera, m_playArea, new UIDocument[0]);
            visibility.Hide();

            // The build frames the camera on the hidden board.
            Vector3 framed = m_playArea.position + new Vector3(0f, 10f, -6f);
            m_camera.transform.position = framed;

            visibility.Show();

            Assert.That(visibility.IsHidden, Is.False);
            Assert.That(m_playArea.position, Is.EqualTo(new Vector3(1f, 0f, 2f)));
            Assert.That(m_camera.transform.position, Is.EqualTo(framed - GameSceneVisibility.HiddenOffset),
                "the camera keeps its framing relative to the board");
            Assert.That(m_camera.enabled, Is.True);
            Assert.That(m_listener.enabled, Is.True);
            Assert.That(m_light.enabled, Is.True);
            Assert.That(m_offLight.enabled, Is.False, "a light the scene had off stays off");
        }

        [Test]
        public void ShowAndShowDocumentsHiddenDoNothingForAVisibleScene()
        {
            var visibility = new GameSceneVisibility(m_camera, m_playArea, new UIDocument[0]);

            visibility.ShowDocumentsHidden();
            visibility.Show();

            Assert.That(m_playArea.position, Is.EqualTo(new Vector3(1f, 0f, 2f)));
            Assert.That(m_camera.enabled, Is.True);
            Assert.That(m_offLight.enabled, Is.False);
        }
    }
}
