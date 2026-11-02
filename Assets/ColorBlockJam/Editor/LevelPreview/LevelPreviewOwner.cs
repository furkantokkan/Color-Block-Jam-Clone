using System;
using ColorBlockJam.Editor.BoardKit;
using ColorBlockJam.Editor.Levels;
using ColorBlockJam.Environment;
using ColorBlockJam.Presentation.Board;
using ColorBlockJam.Runtime.Art;
using ColorBlockJam.Runtime.Levels;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ColorBlockJam.Editor.LevelPreview
{
    /// <summary>
    /// Editor-only owner of the LevelEditor scene preview: while that scene is loaded it
    /// shows each level and solution playback frame the Level Editor window publishes, and it
    /// destroys the preview when the scene closes, the window publishes null, the domain
    /// reloads or the Editor quits.
    /// </summary>
    [InitializeOnLoad]
    internal static class LevelPreviewOwner
    {
        public const string ScenePath = "Assets/ColorBlockJam/Scenes/LevelEditor.unity";
        // The Environment.prefab root the preview clones (EnvironmentView plus its BoardRoot); distinct from
        // BoardKitAssets.EnvironmentPrefabPath, which names the board-kit piece-prefabs folder.
        private const string k_environmentPrefabPath = "Assets/ColorBlockJam/Prefabs/Environment/Environment.prefab";

        private static LevelPreviewHost s_host;
        private static bool s_listening;
        private static string s_lastWarning;

        static LevelPreviewOwner()
        {
            // Owner: this class, for the whole domain. Stop point: Shutdown, on
            // AssemblyReloadEvents.beforeAssemblyReload or EditorApplication.quitting.
            EditorSceneManager.sceneOpened += OnSceneOpened;
            EditorSceneManager.sceneClosing += OnSceneClosing;
            EditorSceneManager.sceneManagerSetupRestored += OnSceneSetupRestored;
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
            AssemblyReloadEvents.beforeAssemblyReload += Shutdown;
            EditorApplication.quitting += Shutdown;

            // The scene survives a domain reload; the window republishes its draft afterwards.
            if (IsSceneLoaded(out _))
            {
                StartListening();
            }
        }

        [MenuItem("Color Block Jam/Open Level Editor Scene")]
        private static void OpenScene()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                return;
            }

            // sceneOpened starts listening and shows the last published level.
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            LevelDatabaseWindow.Open();
        }

        // OpenScene throws in Play Mode, so the menu item is greyed out there, as the Level Editor launcher refuses it.
        [MenuItem("Color Block Jam/Open Level Editor Scene", true)]
        private static bool CanOpenScene()
        {
            return !EditorApplication.isPlayingOrWillChangePlaymode;
        }

        private static void OnSceneOpened(Scene scene, OpenSceneMode mode)
        {
            if (scene.path == ScenePath)
            {
                StartListening();
            }
        }

        // EditorSceneManager.RestoreSceneManagerSetup (used by the test runner) reopens
        // scenes without sceneOpened; it raises sceneManagerSetupRestored instead.
        private static void OnSceneSetupRestored(Scene[] scenes)
        {
            if (!s_listening && IsSceneLoaded(out _))
            {
                StartListening();
            }
        }

        // Test Level's Play Mode (DEC-127) closes the scene with sceneClosing, and leaving Play Mode reopens it with
        // neither sceneOpened nor sceneManagerSetupRestored; the Level Editor then publishes its draft again.
        private static void OnPlayModeStateChanged(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.EnteredEditMode && !s_listening && IsSceneLoaded(out _))
            {
                StartListening();
            }
        }

        private static void OnSceneClosing(Scene scene, bool removingScene)
        {
            if (scene.path == ScenePath)
            {
                StopListening();
            }
        }

        private static void Shutdown()
        {
            StopListening();
            EditorSceneManager.sceneOpened -= OnSceneOpened;
            EditorSceneManager.sceneClosing -= OnSceneClosing;
            EditorSceneManager.sceneManagerSetupRestored -= OnSceneSetupRestored;
            EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
            AssemblyReloadEvents.beforeAssemblyReload -= Shutdown;
            EditorApplication.quitting -= Shutdown;
        }

        private static void StartListening()
        {
            if (!s_listening)
            {
                // Owner: this class while the LevelEditor scene is loaded.
                // Stop point: StopListening (scene closing, domain reload or quit).
                LevelDatabaseWindow.DraftLevelPublished += OnLevelPublished;
                // Solution playback frames (DEC-170). Owner and stop point: as for the draft publish above.
                LevelDatabaseWindow.SolutionFramePublished += OnSolutionFrame;
                s_listening = true;
            }

            if (LevelDatabaseWindow.LastPublishedLevel != null)
            {
                OnLevelPublished(LevelDatabaseWindow.LastPublishedLevel);
            }
        }

        private static void StopListening()
        {
            if (s_listening)
            {
                LevelDatabaseWindow.DraftLevelPublished -= OnLevelPublished;
                LevelDatabaseWindow.SolutionFramePublished -= OnSolutionFrame;
                s_listening = false;
            }

            DestroyHost();
        }

        private static void OnLevelPublished(LevelData level)
        {
            try
            {
                if (level == null)
                {
                    DestroyHost();
                    return;
                }

                if (!IsSceneLoaded(out Scene scene))
                {
                    return;
                }

                if (s_host == null && !TryCreateHost(scene))
                {
                    return;
                }

                string previousId = s_host.LevelId;
                if (!s_host.TryShow(level, out string error))
                {
                    WarnUnlessRepeated(GetDraftWarning(level, error));
                    return;
                }

                s_lastWarning = null;
                if (previousId != s_host.LevelId)
                {
                    Frame();
                }
            }
            catch (Exception exception)
            {
                // The preview is a convenience: a show or teardown failure reports once
                // and never interrupts the window that published the level.
                WarnUnlessRepeated(GetDraftWarning(level, exception.Message));
            }
        }

        // One board of a solution playback (DEC-170): the listed level after some of its solver steps, the cleared
        // board included. It is never the draft: the Scene view is not framed for it, LastPublishedLevel stays the
        // draft, and the window publishes its draft again when the playback ends or stops. A frame the preview cannot
        // show keeps the previous one; a shown frame leaves the warning memory alone, so one playback warns once per
        // reason, and the next shown draft clears it.
        private static void OnSolutionFrame(LevelData frame)
        {
            if (frame == null)
            {
                return;
            }

            try
            {
                if (!IsSceneLoaded(out Scene scene))
                {
                    return;
                }

                if (s_host == null && !TryCreateHost(scene))
                {
                    return;
                }

                if (!s_host.TryShowFrame(frame, out string error))
                {
                    WarnUnlessRepeated(GetFrameWarning(frame, error));
                }
            }
            catch (Exception exception)
            {
                // As for a draft, a failure reports once and never interrupts the playing window.
                WarnUnlessRepeated(GetFrameWarning(frame, exception.Message));
            }
        }

        private static bool TryCreateHost(Scene scene)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(k_environmentPrefabPath);
            EnvironmentDatabase database = AssetDatabase.LoadAssetAtPath<EnvironmentDatabase>(BoardKitAssets.EnvironmentDatabasePath);
            GamePalette palette = AssetDatabase.LoadAssetAtPath<GamePalette>(BoardKitAssets.PalettePath);
            BoardPieceCatalog catalog = AssetDatabase.LoadAssetAtPath<BoardPieceCatalog>(BoardKitAssets.CatalogPath);
            if (prefab == null || database == null || palette == null || catalog == null)
            {
                WarnUnlessRepeated(GetDraftWarning(null,
                    "The environment prefab, environment database, palette or piece catalog asset is missing."));
                return false;
            }

            // Effect adapters in the game's module order: blockers first (catalog "Arrow + Ice: Ice first").
            s_host = new LevelPreviewHost(scene, prefab, database, new BoardArt(palette, catalog),
                () => new ILevelPreviewEffect[]
                {
                    new IcePreviewEffect(
                        AssetDatabase.LoadAssetAtPath<Material>(BoardIceAssets.MaterialPath),
                        AssetDatabase.LoadAssetAtPath<UnityEngine.UIElements.PanelSettings>(BoardIceAssets.PanelPath)),
                    new ArrowPreviewEffect()
                });
            return true;
        }

        private static void DestroyHost()
        {
            // Forget the host first, so a failing Dispose never leaves a disposed host
            // for the next publish; the next publish creates a fresh one.
            LevelPreviewHost host = s_host;
            s_host = null;
            s_lastWarning = null;
            host?.Dispose();
        }

        private static void Frame()
        {
            SceneView sceneView = SceneView.lastActiveSceneView;
            if (sceneView != null && s_host.TryGetBounds(out Bounds bounds))
            {
                sceneView.Frame(bounds, false);
            }
        }

        private static string GetDraftWarning(LevelData level, string reason)
        {
            return level == null
                ? $"Level preview skipped: {reason}"
                : $"Level preview keeps the last valid level; {level.Id} is invalid: {reason}";
        }

        private static string GetFrameWarning(LevelData frame, string reason)
        {
            return $"Level preview keeps the previous playback frame; a frame of {frame.Id} cannot be shown: {reason}";
        }

        // A repeat of the last warning stays silent until a shown draft or DestroyHost clears it.
        private static void WarnUnlessRepeated(string message)
        {
            if (message == s_lastWarning)
            {
                return;
            }

            s_lastWarning = message;
            Debug.LogWarning(message);
        }

        private static bool IsSceneLoaded(out Scene scene)
        {
            scene = SceneManager.GetSceneByPath(ScenePath);
            return scene.IsValid() && scene.isLoaded;
        }
    }
}
