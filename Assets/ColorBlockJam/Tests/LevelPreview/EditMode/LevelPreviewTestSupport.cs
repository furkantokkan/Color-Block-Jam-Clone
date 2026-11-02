using System;
using System.Reflection;
using ColorBlockJam.Editor.BoardKit;
using ColorBlockJam.Editor.LevelPreview;
using ColorBlockJam.Editor.Levels;
using ColorBlockJam.Environment;
using ColorBlockJam.Presentation.Board;
using ColorBlockJam.Runtime.Art;
using ColorBlockJam.Runtime.Board;
using ColorBlockJam.Runtime.Levels;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace ColorBlockJam.Tests.LevelPreview
{
    /// <summary>
    /// Shared arrange/assert helpers for the LevelEditor scene preview EditMode tests (DEC-102):
    /// LevelPreviewTests, LevelPreviewPlaybackTests, PreviewArrowMarkerTests and PreviewIceTests. Owns the preview
    /// asset paths, host and board art construction, the DontSave/object-count assertions and the reflective access
    /// into LevelPreviewHost/LevelPreviewOwner/LevelDatabaseWindow privates the fault-injection and owner tests
    /// need. Member names GetOwnerHost, SetLastPublishedLevel, Raise and HasOwnerHandler mirror the window/owner
    /// members they reach by name and must stay in step with them.
    /// </summary>
    internal static class LevelPreviewTestSupport
    {
        internal const string PrefabPath = "Assets/ColorBlockJam/Prefabs/Environment/Environment.prefab";
        internal const string DatabasePath = "Assets/ColorBlockJam/Data/Environment/EnvironmentDatabase.asset";

        private const BindingFlags k_static = BindingFlags.Static | BindingFlags.NonPublic;
        private const BindingFlags k_instance = BindingFlags.Instance | BindingFlags.NonPublic;

        // The board art LevelPreviewOwner builds from asset loads.
        internal static BoardArt CreateBoardArt()
        {
            return new BoardArt(AssetDatabase.LoadAssetAtPath<GamePalette>(BoardKitAssets.PalettePath),
                AssetDatabase.LoadAssetAtPath<BoardPieceCatalog>(BoardKitAssets.CatalogPath));
        }

        internal static LevelPreviewHost CreateHost(Scene scene, BoardArt art, Func<ILevelPreviewEffect[]> effectFactory = null)
        {
            return new LevelPreviewHost(scene,
                AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath),
                AssetDatabase.LoadAssetAtPath<EnvironmentDatabase>(DatabasePath),
                art, effectFactory);
        }

        // An in-memory copy of the board piece catalog with an empty environment list, so it has no Arrow prefab;
        // the catalog asset itself is not changed.
        internal static BoardPieceCatalog CreateCatalogWithoutArrows(BoardPieceCatalog catalog)
        {
            BoardPieceCatalog copy = Object.Instantiate(catalog);
            using (SerializedObject serialized = new SerializedObject(copy))
            {
                serialized.FindProperty("m_environment").ClearArray();
                serialized.ApplyModifiedPropertiesWithoutUndo();
            }

            Assert.That(copy.TryGetArrow(1, out _), Is.False);
            return copy;
        }

        internal static void AssertEverythingIsDontSave(GameObject root)
        {
            foreach (Transform transform in root.GetComponentsInChildren<Transform>(true))
            {
                Assert.That(transform.gameObject.hideFlags & HideFlags.DontSave, Is.EqualTo(HideFlags.DontSave),
                    transform.name);
                foreach (Component component in transform.GetComponents<Component>())
                {
                    Assert.That(component.hideFlags & HideFlags.DontSave, Is.EqualTo(HideFlags.DontSave),
                        transform.name + " " + component.GetType().Name);
                }
            }
        }

        internal static int CountObjects(GameObject root)
        {
            return root.GetComponentsInChildren<Transform>(true).Length;
        }

        // Fault injection: points the host's blocks-root parent at a destroyed transform, so the next rebuild that
        // needs it throws mid-build.
        internal static void SetBlocksRoot(LevelPreviewHost host, Transform blocksRoot)
        {
            FieldInfo field = typeof(LevelPreviewHost).GetField("m_blocksRoot", k_instance);
            Assert.That(field, Is.Not.Null, "LevelPreviewHost.m_blocksRoot");
            field.SetValue(host, blocksRoot);
        }

        internal static LevelPreviewHost GetOwnerHost()
        {
            FieldInfo field = typeof(LevelPreviewOwner).GetField("s_host", k_static);
            Assert.That(field, Is.Not.Null, "LevelPreviewOwner.s_host");
            return (LevelPreviewHost)field.GetValue(null);
        }

        // LastPublishedLevel has a private setter: only the window's draft publish sets it.
        internal static void SetLastPublishedLevel(LevelData level)
        {
            PropertyInfo property = typeof(LevelDatabaseWindow).GetProperty(nameof(LevelDatabaseWindow.LastPublishedLevel));
            Assert.That(property, Is.Not.Null, "LevelDatabaseWindow.LastPublishedLevel");
            property.SetValue(null, level);
        }

        // Raises one of the window's editor-tool events to its listeners, as the window does.
        internal static void Raise(string eventName, LevelData level)
        {
            FieldInfo field = typeof(LevelDatabaseWindow).GetField(eventName, k_static);
            Assert.That(field, Is.Not.Null, "LevelDatabaseWindow." + eventName);
            ((Action<LevelData>)field.GetValue(null))?.Invoke(level);
        }

        // True when one of the event's listeners is a LevelPreviewOwner handler.
        internal static bool HasOwnerHandler(string eventName)
        {
            FieldInfo field = typeof(LevelDatabaseWindow).GetField(eventName, k_static);
            Assert.That(field, Is.Not.Null, "LevelDatabaseWindow." + eventName);
            Delegate listeners = (Delegate)field.GetValue(null);
            if (listeners == null)
            {
                return false;
            }

            foreach (Delegate listener in listeners.GetInvocationList())
            {
                if (listener.Method.DeclaringType == typeof(LevelPreviewOwner))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
