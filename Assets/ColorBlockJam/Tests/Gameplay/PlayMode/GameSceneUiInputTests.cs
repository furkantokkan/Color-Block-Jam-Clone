#if UNITY_EDITOR
using System.Collections;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace ColorBlockJam.Tests.Gameplay
{
    /// <summary>
    /// The UI input module the Game scene uses (B-21), now the one persistent module on the ProjectContext (DEC-199),
    /// must not enable the Input System package's shared DefaultInputActions asset: in a long-running Editor that asset
    /// keeps controls resolved against earlier devices, and enabling it after InputTestFixture swaps the input state threw
    /// IndexOutOfRangeException in InputManagerStateMonitors for every Game-scene test with a simulated mouse. With no
    /// serialized actions the module assigns fresh default actions on enable. This test resolves the shared asset
    /// first, as that Editor had, so it reproduces the failure regardless of test order.
    /// </summary>
    public sealed class GameSceneUiInputTests
    {
        private const string k_gameScene = "Game";
        private const string k_sharedUiActionsGuid = "ca9f5fa95ffab41fb9a615ab714db018";

        private InputTestFixture m_input;
        private bool m_sceneLoaded;

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (m_sceneLoaded)
            {
                m_sceneLoaded = false;
                yield return SceneManager.UnloadSceneAsync(k_gameScene);
            }

            if (m_input != null)
            {
                m_input.TearDown();
                m_input = null;
            }
        }

        [UnityTest]
        public IEnumerator TheGameSceneUiModuleEnablesUnderAFreshInputStateAfterTheSharedActionsWereUsed()
        {
            var shared = AssetDatabase.LoadAssetAtPath<InputActionAsset>(AssetDatabase.GUIDToAssetPath(k_sharedUiActionsGuid));
            Assert.That(shared, Is.Not.Null, "the Input System package's DefaultInputActions asset");
            shared.Enable();
            shared.Disable();

            m_input = new InputTestFixture();
            m_input.Setup();
            InputSystem.AddDevice<Mouse>();

            yield return SceneManager.LoadSceneAsync(k_gameScene, LoadSceneMode.Additive);
            m_sceneLoaded = true;
            yield return null;

            InputSystemUIInputModule module = Object.FindObjectOfType<InputSystemUIInputModule>();
            Assert.That(module, Is.Not.Null, "the project scope's UI input module serves the Game scene");
            Assert.That(module.isActiveAndEnabled, Is.True);
            Assert.That(module.actionsAsset, Is.Not.Null, "the module assigned default actions on enable");
            Assert.That(module.actionsAsset, Is.Not.SameAs(shared), "the module does not use the shared asset");
            LogAssert.NoUnexpectedReceived();
        }
    }
}
#endif
