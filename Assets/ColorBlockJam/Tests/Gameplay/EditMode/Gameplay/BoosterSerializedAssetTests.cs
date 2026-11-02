using System.Collections.Generic;
using System.IO;
using ColorBlockJam.Runtime.Boosters;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

namespace ColorBlockJam.Tests.Gameplay.Gameplay
{
    /// <summary>
    /// Guards the booster serialized targets: Hammer data (B-26), the reticle import (B-24), the HUD sprite borders
    /// (B-27), the Time Freeze frost material (B-25) and the Game scene HUD and installer wiring (B-21), with the one
    /// EventSystem on the ProjectContext instead of the scene (DEC-199).
    /// </summary>
    /// <remarks>
    /// The EventSystem, its Input System module and the scene installer are matched by type name and read through
    /// SerializedObject, so this Editor test assembly needs no UnityEngine.UI or Onity.Unity reference.
    /// </remarks>
    public sealed class BoosterSerializedAssetTests
    {
        private const string k_hammer = "Assets/ColorBlockJam/Data/Boosters/Definitions/Hammer.asset";
        private const string k_reticle = "Assets/ColorBlockJam/Art/UI/Supplied/Icons/Gameplay/ic_target.png";
        private const string k_timeHolder = "Assets/ColorBlockJam/Art/UI/Supplied/Panels/bg_gameplay_time_holder.png";
        private const string k_boosterButton = "Assets/ColorBlockJam/Art/UI/Supplied/Buttons/btn_booster_normal.png";
        private const string k_frostGraph = "Assets/ColorBlockJam/Art/Shaders/TimeFreezeOverlay.shadergraph";
        private const string k_frostMaterial = "Assets/ColorBlockJam/Art/Materials/TimeFreezeOverlay.mat";
        private const string k_gameScene = "Assets/ColorBlockJam/Scenes/Game.unity";
        private const string k_projectContext = "Assets/ColorBlockJam/Resources/Onity/ProjectContext.prefab";
        private const string k_hudPanel = "Assets/ColorBlockJam/Data/UI/BoardOverlayPanelSettings.asset";
        private const string k_hudUxml = "Assets/ColorBlockJam/UI/Game/GameplayHud.uxml";
        private const string k_frostAmount = "_FrostAmount";
        private const string k_eventSystemType = "UnityEngine.EventSystems.EventSystem";
        private const string k_inputModuleType = "UnityEngine.InputSystem.UI.InputSystemUIInputModule";
        private const string k_installerType = "ColorBlockJam.Gameplay.Composition.GameSceneInstaller";

        private static readonly string[] s_mobilePlatforms = { "Android", "iPhone" };

        private static readonly string[] s_uiModuleActionFields =
        {
            "m_ActionsAsset", "m_PointAction", "m_MoveAction", "m_SubmitAction", "m_CancelAction",
            "m_LeftClickAction", "m_MiddleClickAction", "m_RightClickAction", "m_ScrollWheelAction",
            "m_TrackedDevicePositionAction", "m_TrackedDeviceOrientationAction"
        };

        private static readonly object[] s_borders =
        {
            new object[] { k_timeHolder, 50f },
            new object[] { k_boosterButton, 93f }
        };

        [Test]
        public void Hammer_TargetsOneWholeBlock()
        {
            var hammer = AssetDatabase.LoadAssetAtPath<BoosterDefinition>(k_hammer);

            Assert.That(hammer, Is.Not.Null, k_hammer);
            Assert.That(hammer.TargetKind, Is.EqualTo(BoosterTargetKind.Block));
            Assert.That((int)BoosterTargetKind.Block, Is.EqualTo(4), "BoosterTargetKind is append-only");
            Assert.That(hammer.Description, Is.EqualTo("Remove one whole block."));
        }

        [Test]
        public void Reticle_ImportsAsAReadableUncompressedFixedSprite()
        {
            var importer = (TextureImporter)AssetImporter.GetAtPath(k_reticle);
            Assert.That(importer, Is.Not.Null, k_reticle);
            var settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);

            Assert.That(importer.textureType, Is.EqualTo(TextureImporterType.Sprite));
            Assert.That(importer.spriteImportMode, Is.EqualTo(SpriteImportMode.Single));
            Assert.That(importer.spritePixelsPerUnit, Is.EqualTo(100f));
            Assert.That(importer.mipmapEnabled, Is.False);
            Assert.That(importer.alphaIsTransparency, Is.True);
            Assert.That(importer.wrapMode, Is.EqualTo(TextureWrapMode.Clamp));
            Assert.That(importer.filterMode, Is.EqualTo(FilterMode.Bilinear));
            Assert.That(importer.spriteBorder, Is.EqualTo(Vector4.zero), "fixed icon: no border");
            Assert.That(settings.spriteMeshType, Is.EqualTo(SpriteMeshType.FullRect));

            // The HUD derives its white reticle copy on Home with GetPixels32, so the icon stays readable and
            // uncompressed on every platform (a deliberate exception to the ASTC 6x6 rule for this 256 px icon).
            Assert.That(importer.isReadable, Is.True);
            Assert.That(importer.maxTextureSize, Is.EqualTo(256));
            TextureImporterPlatformSettings standard = importer.GetDefaultPlatformTextureSettings();
            Assert.That(standard.format, Is.EqualTo(TextureImporterFormat.RGBA32));
            Assert.That(standard.textureCompression, Is.EqualTo(TextureImporterCompression.Uncompressed));
            foreach (string platform in s_mobilePlatforms)
            {
                TextureImporterPlatformSettings mobile = importer.GetPlatformTextureSettings(platform);
                Assert.That(mobile.overridden, Is.True, platform);
                Assert.That(mobile.format, Is.EqualTo(TextureImporterFormat.RGBA32), platform);
                Assert.That(mobile.maxTextureSize, Is.EqualTo(256), platform);
            }

            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(k_reticle);
            Assert.That(texture.isReadable, Is.True);
            Assert.That(texture.width, Is.EqualTo(256));
            Assert.That(texture.format, Is.EqualTo(TextureFormat.RGBA32));
            Assert.That(AssetDatabase.LoadAssetAtPath<Sprite>(k_reticle), Is.Not.Null);
        }

        [TestCaseSource(nameof(s_borders))]
        public void HudSprite_HasItsMeasuredBorder_FullRectAndUncompressedSource(string path, float border)
        {
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            Assert.That(importer, Is.Not.Null, path);
            var settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            var expected = new Vector4(border, border, border, border);

            Assert.That(importer.spriteBorder, Is.EqualTo(expected), path + " spriteBorder (L, B, R, T)");
            Assert.That(settings.spriteMeshType, Is.EqualTo(SpriteMeshType.FullRect), path);
            Assert.That(importer.maxTextureSize, Is.EqualTo(256), path);

            // Packed UI sprites stay uncompressed sources; only the Sprite Atlas compresses (no per-sprite override).
            Assert.That(importer.GetDefaultPlatformTextureSettings().textureCompression,
                Is.EqualTo(TextureImporterCompression.Uncompressed), path);
            foreach (string platform in s_mobilePlatforms)
            {
                Assert.That(importer.GetPlatformTextureSettings(platform).overridden, Is.False, path + " " + platform);
            }

            Assert.That(AssetDatabase.LoadAssetAtPath<Sprite>(path).border, Is.EqualTo(expected), path + " Sprite.border");
        }

        [Test]
        public void FrostMaterial_UsesTheGraphShader_ThatExposesFrostAmount()
        {
            var shader = AssetDatabase.LoadAssetAtPath<Shader>(k_frostGraph);
            var material = AssetDatabase.LoadAssetAtPath<Material>(k_frostMaterial);

            Assert.That(shader, Is.Not.Null, k_frostGraph);
            Assert.That(ShaderUtil.ShaderHasError(shader), Is.False, "the frost graph compiles");
            Assert.That(material, Is.Not.Null, k_frostMaterial);
            Assert.That(material.shader, Is.SameAs(shader));
            Assert.That(material.renderQueue, Is.EqualTo((int)RenderQueue.Transparent));
            Assert.That(material.GetFloat(k_frostAmount), Is.EqualTo(0f));

            int amount = shader.FindPropertyIndex(k_frostAmount);
            Assert.That(amount, Is.GreaterThanOrEqualTo(0), "the graph exposes _FrostAmount");
            Assert.That(shader.GetPropertyType(amount), Is.EqualTo(ShaderPropertyType.Range));
            Assert.That(shader.GetPropertyRangeLimits(amount), Is.EqualTo(new Vector2(0f, 1f)));
            Assert.That(shader.GetPropertyDefaultFloatValue(amount), Is.EqualTo(0f));

            // B-25 render state, authored in the graph's URP target: Unlit, Transparent, Alpha blend, ZWrite Off,
            // ZTest Always and both faces.
            string graph = File.ReadAllText(k_frostGraph);
            StringAssert.Contains("UniversalUnlitSubTarget", graph);
            StringAssert.Contains("\"m_SurfaceType\": 1,", graph);
            StringAssert.Contains("\"m_AlphaMode\": 0,", graph);
            StringAssert.Contains("\"m_ZWriteControl\": 2,", graph);
            StringAssert.Contains("\"m_ZTestMode\": 8,", graph);
            StringAssert.Contains("\"m_RenderFace\": 0,", graph);
        }

        [Test]
        public void GameScene_WiresTheHudDocument_AndInstallerFields_WhileTheProjectScopeOwnsTheEventSystem()
        {
            Scene scene = SceneManager.GetSceneByPath(k_gameScene);
            bool openedHere = !scene.isLoaded;
            if (openedHere)
            {
                scene = EditorSceneManager.OpenScene(k_gameScene, OpenSceneMode.Additive);
            }

            try
            {
                GameObject[] roots = scene.GetRootGameObjects();
                var rootNames = new List<string>();
                foreach (GameObject root in roots)
                {
                    rootNames.Add(root.name);
                }

                int counters = rootNames.IndexOf("Board effect counters");
                int hudIndex = rootNames.IndexOf("Gameplay HUD");
                Assert.That(counters, Is.GreaterThanOrEqualTo(0), "Board effect counters root");
                Assert.That(hudIndex, Is.EqualTo(counters + 1), "Gameplay HUD is the root right after Board effect counters");

                GameObject hud = roots[hudIndex];
                var document = hud.GetComponent<UIDocument>();
                Assert.That(hud.activeSelf, Is.True);
                Assert.That(document, Is.Not.Null);
                Assert.That(AssetDatabase.GetAssetPath(document.panelSettings), Is.EqualTo(k_hudPanel));
                Assert.That(AssetDatabase.GetAssetPath(document.visualTreeAsset), Is.EqualTo(k_hudUxml));
                Assert.That(document.sortingOrder, Is.EqualTo(30f));

                GameObject sceneScope = roots[rootNames.IndexOf("SceneScope")];

                // One persistent EventSystem on the ProjectContext serves every scene, so a Game scene prepared next to
                // Hub never takes Hub's UI Toolkit panel handlers with it, or the reverse (DEC-188, DEC-199).
                Assert.That(FindComponents(roots, k_eventSystemType), Is.Empty, "Game.unity has no EventSystem of its own");
                GameObject projectContext = AssetDatabase.LoadAssetAtPath<GameObject>(k_projectContext);
                Assert.That(projectContext, Is.Not.Null, k_projectContext);
                List<Component> eventSystems = FindComponents(new[] { projectContext }, k_eventSystemType);
                Assert.That(eventSystems.Count, Is.EqualTo(1), "one EventSystem on the ProjectContext");
                Transform eventSystem = eventSystems[0].transform;
                Assert.That(eventSystem.parent, Is.SameAs(projectContext.transform), "the EventSystem sits under ProjectContext");
                Component module = FindComponent(eventSystem.gameObject, k_inputModuleType);
                Assert.That(module, Is.Not.Null, "InputSystemUIInputModule on the EventSystem");
                // Empty references make OnEnable assign fresh default actions; the shared DefaultInputActions asset
                // breaks after InputTestFixture swaps devices in a long-running Editor (GameSceneUiInputTests).
                var moduleFields = new SerializedObject(module);
                foreach (string action in s_uiModuleActionFields)
                {
                    Assert.That(moduleFields.FindProperty(action).objectReferenceValue, Is.Null,
                        action + " stays empty so the module creates its own default actions");
                }

                Component installer = FindComponent(sceneScope, k_installerType, true);
                Assert.That(installer, Is.Not.Null, "GameSceneInstaller under SceneScope");
                var fields = new SerializedObject(installer);
                Assert.That(fields.FindProperty("m_hudDocument").objectReferenceValue, Is.SameAs(document));
                Assert.That(AssetDatabase.GetAssetPath(fields.FindProperty("m_frostMaterial").objectReferenceValue),
                    Is.EqualTo(k_frostMaterial));
                Object reticle = fields.FindProperty("m_reticleTexture").objectReferenceValue;
                Assert.That(reticle, Is.InstanceOf<Texture2D>());
                Assert.That(AssetDatabase.GetAssetPath(reticle), Is.EqualTo(k_reticle));
            }
            finally
            {
                if (openedHere)
                {
                    EditorSceneManager.CloseScene(scene, true);
                }
            }
        }

        private static List<Component> FindComponents(GameObject[] roots, string typeName)
        {
            var found = new List<Component>();
            foreach (GameObject root in roots)
            {
                foreach (Component component in root.GetComponentsInChildren<Component>(true))
                {
                    if (component != null && component.GetType().FullName == typeName)
                    {
                        found.Add(component);
                    }
                }
            }

            return found;
        }

        private static Component FindComponent(GameObject owner, string typeName, bool children = false)
        {
            Component[] components = children ? owner.GetComponentsInChildren<Component>(true) : owner.GetComponents<Component>();
            foreach (Component component in components)
            {
                if (component != null && component.GetType().FullName == typeName)
                {
                    return component;
                }
            }

            return null;
        }
    }
}
