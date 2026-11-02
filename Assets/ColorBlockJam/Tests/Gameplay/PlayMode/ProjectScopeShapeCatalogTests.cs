#if UNITY_EDITOR
using System;
using ColorBlockJam.Runtime.Composition;
using NUnit.Framework;
using Onity.DI;
using UnityEditor;
using UnityEngine;

namespace ColorBlockJam.Tests.Gameplay.PlayMode
{
    /// <summary>
    /// Builds the real project-scope installer and checks that destroying it before the build cancels before the level
    /// JSON loads. The project scope loads no piece library and learns no shapes any more (DEC-193), so the
    /// catalog and cache cases of this fixture were removed. It lives with the gameplay tests only because its
    /// assembly references Onity.DI.
    /// </summary>
    public sealed class ProjectScopeShapeCatalogTests
    {
        private const string k_projectContextPath = "Assets/ColorBlockJam/Resources/Onity/ProjectContext.prefab";

        [Test]
        public void DestroyingTheInstallerBeforeBuildCancelsBeforeLoading()
        {
            GameObject owner = new GameObject("destroyed-project-scope-check");
            OnityContainer container = new OnityContainer();
            try
            {
                ProjectScopeInstaller installer = CreateInstaller(owner);
                // No level JSON is named (DEC-194), so a load that ran would fail rather than read the project's file.
                SerializedObject serialized = new SerializedObject(installer);
                serialized.FindProperty("m_levelJson.m_AssetGUID").stringValue = string.Empty;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                installer.InstallBindings(container);
                UnityEngine.Object.DestroyImmediate(owner);

                Assert.That(() => container.BuildAsync().GetAwaiter().GetResult(),
                    Throws.InstanceOf<OperationCanceledException>(),
                    "a destroyed installer must cancel before the empty-GUID level JSON is loaded");
            }
            finally
            {
                container.Dispose();
                UnityEngine.Object.DestroyImmediate(owner);
            }
        }

        private static ProjectScopeInstaller CreateInstaller(GameObject owner)
        {
            ProjectScopeInstaller installer = owner.AddComponent<ProjectScopeInstaller>();
            EditorUtility.CopySerialized(
                AssetDatabase.LoadAssetAtPath<GameObject>(k_projectContextPath).GetComponent<ProjectScopeInstaller>(), installer);
            return installer;
        }
    }
}
#endif
