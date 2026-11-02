using System;
using System.Reflection;
using ColorBlockJam.Editor.Levels;
using ColorBlockJam.Runtime.Composition;
using ColorBlockJam.Runtime.Levels;
using UnityEditor;
using UnityEngine;

namespace ColorBlockJam.Tests.EditMode
{
    /// <summary>
    /// Private fixture access for the Level Editor. State follows its concrete owner; the production window keeps
    /// no duplicate fields for tests. Existing interaction fixtures continue to invoke the window's gesture seams.
    /// </summary>
    internal static class LevelEditorTestAccess
    {
        private const BindingFlags k_members = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

        internal static object Get(LevelDatabaseWindow window, string name)
        {
            object owner = GetOwner(window, name);
            return FindField(owner, name).GetValue(owner);
        }

        internal static T Get<T>(LevelDatabaseWindow window, string name)
        {
            return (T)Get(window, name);
        }

        internal static void Set(LevelDatabaseWindow window, string name, object value)
        {
            object owner = GetOwner(window, name);
            FindField(owner, name).SetValue(owner, value);
        }

        /// <summary>The window's own saved levels: its save controller's catalog, apart from the game's (DEC-192).</summary>
        internal static LevelCatalog GetLevels(LevelDatabaseWindow window)
        {
            return Get<LevelCatalog>(window, "m_levels");
        }

        /// <summary>Loads <paramref name="json"/> into the window's own saved levels, as a Reload or a save's reload does.</summary>
        internal static void LoadLevels(LevelDatabaseWindow window, string json)
        {
            if (!GetLevels(window).LoadJson(json, out string error))
            {
                throw new InvalidOperationException(error);
            }
        }

        /// <summary>
        /// Makes the levels of <paramref name="json"/> the window's loaded source files: a database snapshot on no disk
        /// (DEC-196), so a save over it is refused as an external change and a test window never writes the project's
        /// database.
        /// </summary>
        internal static void SetLoadedSource(LevelDatabaseWindow window, string json)
        {
            Set(window, "m_loadedSource", CreateUnsavedSource(json));
        }

        /// <summary>The window's loaded source files, or null.</summary>
        internal static LevelDatabaseSnapshot GetLoadedSource(LevelDatabaseWindow window)
        {
            return Get<LevelDatabaseSnapshot>(window, "m_loadedSource");
        }

        /// <summary>A snapshot of the levels in <paramref name="json"/> whose files exist on no disk.</summary>
        internal static LevelDatabaseSnapshot CreateUnsavedSource(string json)
        {
            const string folder = "Temp/ColorBlockJam/UnsavedLevelDatabase/Fixture";
            if (!LevelDatabaseJson.TryRead(json, out LevelData[] levels, out string error))
            {
                throw new InvalidOperationException(error);
            }

            string[] ids = new string[levels.Length];
            for (int index = 0; index < ids.Length; index++)
            {
                ids[index] = levels[index].Id;
            }

            if (!LevelPackLayout.TryPlan("Fixture", null, ids, out LevelManifest manifest, out error)
                || !LevelManifestJson.TryWrite(manifest, out string manifestJson, out error))
            {
                throw new InvalidOperationException(error);
            }

            LevelPackFile[] packs = new LevelPackFile[manifest.PackCount];
            for (int packIndex = 0; packIndex < packs.Length; packIndex++)
            {
                LevelPackInfo pack = manifest.GetPackAt(packIndex);
                LevelData[] packLevels = new LevelData[pack.LevelCount];
                Array.Copy(levels, pack.FirstLevelIndex, packLevels, 0, pack.LevelCount);
                if (!LevelDatabaseJson.TryWrite(packLevels, out string packJson, out error))
                {
                    throw new InvalidOperationException(error);
                }

                packs[packIndex] = new LevelPackFile($"{folder}/{pack.File}", packJson);
            }

            return new LevelDatabaseSnapshot($"{folder}/{LevelJsonFiles.ManifestFile}", manifest, manifestJson, packs,
                levels);
        }

        /// <summary>
        /// A stand-in project scope installer naming <paramref name="levelJsonGuid"/> as the game's level JSON
        /// (DEC-194). It lives on a hidden GameObject that is never saved, so a test never writes ProjectContext.prefab;
        /// destroy it with <see cref="DestroyProjectScope"/>.
        /// </summary>
        internal static ProjectScopeInstaller CreateProjectScope(string levelJsonGuid)
        {
            GameObject owner = new GameObject("Stand-in project scope") { hideFlags = HideFlags.HideAndDontSave };
            ProjectScopeInstaller projectScope = owner.AddComponent<ProjectScopeInstaller>();
            using (SerializedObject serialized = new SerializedObject(projectScope))
            {
                serialized.FindProperty(LevelDatabaseAddressables.LevelJsonGuidProperty).stringValue = levelJsonGuid;
                serialized.ApplyModifiedPropertiesWithoutUndo();
            }

            return projectScope;
        }

        internal static void DestroyProjectScope(ProjectScopeInstaller projectScope)
        {
            if (projectScope != null)
            {
                UnityEngine.Object.DestroyImmediate(projectScope.gameObject);
            }
        }

        internal static object Invoke(LevelDatabaseWindow window, string name, params object[] arguments)
        {
            MethodInfo method = typeof(LevelDatabaseWindow).GetMethod(name, k_members);
            if (method == null)
            {
                throw new MissingMethodException(typeof(LevelDatabaseWindow).FullName, name);
            }

            return method.Invoke(window, arguments);
        }

        private static object GetOwner(LevelDatabaseWindow window, string name)
        {
            switch (name)
            {
                case "m_draft":
                case "m_draftObject":
                case "m_savedDraftJson":
                case "m_syncedDraftJson":
                    return FindField(window, "m_draftController").GetValue(window);
                case "m_loadedSource":
                case "m_loadedGuid":
                case "m_sourceValid":
                case "m_levels":
                    return FindField(window, "m_saveController").GetValue(window);
                case "m_shapeLibrary":
                case "m_shapeCatalog":
                case "m_shapeCatalogJson":
                case "m_customPieces":
                case "m_lastMirror":
                case "m_penEditBlockId":
                case "m_otherLevelPieces":
                case "m_otherLevelData":
                case "m_penEditsLevelPiece":
                case "m_penMemberIds":
                case "m_otherLevelsSource":
                case "m_otherLevelsCatalog":
                case "m_otherLevelsIndex":
                case "m_otherLevelsVersion":
                case "m_otherLevels":
                case "m_editCatalog":
                case "m_savedCatalog":
                case "m_bannerPlan":
                case "m_bannerPlanKey":
                case "m_bannerPlanLibrary":
                    return FindField(window, "m_pieceEditController").GetValue(window);
                default:
                    return window;
            }
        }

        private static FieldInfo FindField(object owner, string name)
        {
            return owner.GetType().GetField(name, k_members)
                   ?? throw new MissingFieldException(owner.GetType().FullName, name);
        }
    }
}
