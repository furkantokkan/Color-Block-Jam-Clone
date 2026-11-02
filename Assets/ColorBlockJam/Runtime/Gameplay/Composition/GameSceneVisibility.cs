using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

namespace ColorBlockJam.Gameplay.Composition
{
    /// <summary>
    /// Keeps a Game scene out of sight while it prepares its level behind another scene (DEC-199): its camera, audio
    /// listener, lights and UI documents stay off and its play area sits far from every other scene's camera, until the
    /// level starts and <see cref="Show"/> brings everything back in one frame.
    /// </summary>
    /// <remarks>
    /// Plain C#, no Update. Owner: the Game scene installer, which calls <see cref="Hide"/> while the scene wakes, before
    /// the UI documents build their trees, so no frame ever draws the hidden scene; the attempt loader calls
    /// <see cref="ShowDocumentsHidden"/> before the HUD binds and <see cref="Show"/> when the level starts.
    /// </remarks>
    public sealed class GameSceneVisibility
    {
        /// <summary>
        /// Where the hidden play area sits: beyond the Hub camera's 1000-unit far plane and the Game camera's 50, so no
        /// visible camera draws it. The camera framed there moves back with it, so both stay consistent.
        /// </summary>
        public static readonly Vector3 HiddenOffset = new Vector3(5000f, 0f, 0f);

        private readonly Camera m_camera;
        private readonly Transform m_playArea;
        private readonly UIDocument[] m_documents;
        private readonly List<Behaviour> m_turnedOff = new List<Behaviour>();
        private bool m_isHidden;

        /// <param name="camera">The scene's gameplay camera; its audio listener, if any, is hidden with it.</param>
        /// <param name="playArea">The root that holds the board; it moves by <see cref="HiddenOffset"/>.</param>
        /// <param name="documents">The scene's UI documents.</param>
        public GameSceneVisibility(Camera camera, Transform playArea, UIDocument[] documents)
        {
            m_camera = camera != null ? camera : throw new ArgumentNullException(nameof(camera));
            m_playArea = playArea != null ? playArea : throw new ArgumentNullException(nameof(playArea));
            m_documents = documents ?? Array.Empty<UIDocument>();
        }

        /// <summary>True from <see cref="Hide"/> until <see cref="Show"/>.</summary>
        public bool IsHidden => m_isHidden;

        /// <summary>
        /// Hides the scene: the camera, its audio listener, the scene's lights and the UI documents turn off, and the
        /// play area moves by <see cref="HiddenOffset"/>. Called while the scene wakes, before the documents' trees exist.
        /// </summary>
        public void Hide()
        {
            if (m_isHidden)
            {
                return;
            }

            m_isHidden = true;
            TurnOff(m_camera);
            TurnOff(m_camera.GetComponent<AudioListener>());
            foreach (Light light in FindLights(m_camera.gameObject.scene))
            {
                TurnOff(light);
            }

            for (int index = 0; index < m_documents.Length; index++)
            {
                TurnOff(m_documents[index]);
            }

            m_playArea.position += HiddenOffset;
        }

        /// <summary>
        /// Turns the UI documents back on with their trees hidden, in one call, so the HUD can bind them on Home while
        /// nothing draws. No effect unless hidden.
        /// </summary>
        public void ShowDocumentsHidden()
        {
            if (!m_isHidden)
            {
                return;
            }

            for (int index = 0; index < m_documents.Length; index++)
            {
                UIDocument document = m_documents[index];
                if (document == null || !m_turnedOff.Remove(document))
                {
                    continue;
                }

                document.enabled = true;
                if (document.rootVisualElement != null)
                {
                    document.rootVisualElement.style.display = DisplayStyle.None;
                }
            }
        }

        /// <summary>
        /// Shows the scene when its level starts: the play area and the camera framed with it move back by the offset,
        /// everything <see cref="Hide"/> turned off turns on, and the documents' trees show. No effect unless hidden.
        /// </summary>
        public void Show()
        {
            if (!m_isHidden)
            {
                return;
            }

            m_isHidden = false;
            m_playArea.position -= HiddenOffset;
            m_camera.transform.position -= HiddenOffset;
            for (int index = 0; index < m_turnedOff.Count; index++)
            {
                if (m_turnedOff[index] != null)
                {
                    m_turnedOff[index].enabled = true;
                }
            }

            m_turnedOff.Clear();
            for (int index = 0; index < m_documents.Length; index++)
            {
                UIDocument document = m_documents[index];
                if (document != null && document.rootVisualElement != null)
                {
                    document.rootVisualElement.style.display = StyleKeyword.Null;
                }
            }
        }

        // Records only what was on, so Show never turns on something the scene had off.
        private void TurnOff(Behaviour behaviour)
        {
            if (behaviour == null || !behaviour.enabled)
            {
                return;
            }

            behaviour.enabled = false;
            m_turnedOff.Add(behaviour);
        }

        // The scene may still be integrating while it wakes, when its root list is not available yet, so every light is
        // searched and those of this scene are kept.
        private static List<Light> FindLights(Scene scene)
        {
            var lights = new List<Light>();
            Light[] all = UnityEngine.Object.FindObjectsOfType<Light>(true);
            for (int index = 0; index < all.Length; index++)
            {
                if (all[index].gameObject.scene == scene)
                {
                    lights.Add(all[index]);
                }
            }

            return lights;
        }
    }
}
