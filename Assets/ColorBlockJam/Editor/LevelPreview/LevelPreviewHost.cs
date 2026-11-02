using System;
using System.Collections.Generic;
using ColorBlockJam.Environment;
using ColorBlockJam.Presentation.Board;
using ColorBlockJam.Runtime.Layout;
using ColorBlockJam.Runtime.Levels;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace ColorBlockJam.Editor.LevelPreview
{
    /// <summary>
    /// Shows one level in a target scene under a single DontSave host root: the
    /// environment prefab clone builds the play area, a BlockPieceFactory supplies
    /// the blocks, the ordered effect adapters (Ice, Arrow) dress them through the
    /// game's Presentation owners and a preview BoardCamera frames them from the game
    /// angle. It holds no effect-specific branch. Its owner calls TryShow for each
    /// published draft, TryShowFrame for each solution playback frame, and Dispose once.
    /// </summary>
    internal sealed class LevelPreviewHost : IDisposable
    {
        public const string RootName = "Level Preview (not saved)";

        // The preview light and camera mirror the Game scene Directional Light and Main
        // Camera, so the preview looks like the game (DEC-118).
        private const float k_lightIntensity = 0.45f;
        private const float k_shadowStrength = 0.39f;
        private const float k_farClipPlane = 50f;
        private static readonly Vector3 s_lightEuler = new Vector3(65f, 120f, 0f);
        private static readonly Color s_lightColor = new Color(1f, 0.9915804f, 0.9198113f);
        private static readonly Color s_backgroundColor = new Color(0.839f, 0.894f, 0.988f);

        private readonly Scene m_scene;
        private readonly GameObject m_environmentPrefab;
        private readonly EnvironmentDatabase m_database;
        private readonly BoardArt m_art;
        private readonly Func<ILevelPreviewEffect[]> m_effectFactory;
        // The effect adapters of the current scene root, in registration order; null until GetEffects creates them,
        // and disposed and cleared by ClearScene.
        private ILevelPreviewEffect[] m_effects;
        private readonly List<LevelPreviewBlock> m_blocks = new List<LevelPreviewBlock>();
        private readonly List<BlockPiece> m_active = new List<BlockPiece>();
        // Prepared and required size of the one root pool (DEC-193).
        private readonly LevelPreviewPoolPlan m_pool = new LevelPreviewPoolPlan();
        private GameObject m_root;
        private EnvironmentView m_view;
        private Transform m_blocksRoot;
        private BoardCamera m_camera;
        private BlockPieceFactory m_factory;
        private IBoardLayout m_layout;
        // The last level or playback frame shown in full; a failed rebuild shows it again.
        private LevelData m_shownLevel;
        // True when m_shownLevel is a solution playback frame (TryShowFrame), false for a draft (TryShow).
        private bool m_shownIsFrame;
        // The last draft shown in full, kept apart from the frames: a draft TryShow refuses keeps it on screen (DEC-102),
        // so when a playback frame shows instead, it is built again.
        private LevelData m_validDraft;
        private bool m_disposed;

        public LevelPreviewHost(
            Scene scene, GameObject environmentPrefab, EnvironmentDatabase database, BoardArt art,
            Func<ILevelPreviewEffect[]> effectFactory = null)
        {
            if (!scene.IsValid() || !scene.isLoaded) throw new ArgumentException("The preview scene must be loaded.", nameof(scene));
            m_scene = scene;
            m_environmentPrefab = environmentPrefab != null ? environmentPrefab : throw new ArgumentNullException(nameof(environmentPrefab));
            m_database = database != null ? database : throw new ArgumentNullException(nameof(database));
            m_art = art ?? throw new ArgumentNullException(nameof(art));
            m_effectFactory = effectFactory;
        }

        public string LevelId { get; private set; }
        public GameObject Root => m_root;
        public Transform BlocksRoot => m_blocksRoot;
        public IReadOnlyList<BlockPiece> ActiveBlocks => m_active;

        /// <summary>The current scene root's effect adapter of this type, or null; for tests.</summary>
        public T GetEffect<T>() where T : class, ILevelPreviewEffect
        {
            ILevelPreviewEffect[] effects = m_effects ?? Array.Empty<ILevelPreviewEffect>();
            for (int index = 0; index < effects.Length; index++)
            {
                if (effects[index] is T effect)
                {
                    return effect;
                }
            }

            return null;
        }

        /// <summary>
        /// Validates the level fully before touching the scene, so an invalid level
        /// keeps the last valid draft on screen and returns the reason instead; when a
        /// solution playback frame shows, that draft is built again. A rebuild that
        /// still fails clears the scene and shows the last level or frame shown in full
        /// again, and also returns the reason instead of throwing.
        /// </summary>
        public bool TryShow(LevelData level, out string error)
        {
            if (m_disposed) throw new ObjectDisposedException(nameof(LevelPreviewHost));
            if (level == null) throw new ArgumentNullException(nameof(level));

            if (level.Validate(out error) && TryBuild(level, false, out error))
            {
                m_validDraft = level;
                return true;
            }

            // A refused draft keeps the last valid draft (DEC-102), not a frame a playback left on screen. If that
            // draft cannot be built again, the preview stays as the failed build's restore left it.
            if (m_shownLevel != null && m_shownIsFrame && m_validDraft != null)
            {
                TryBuild(m_validDraft, false, out _);
            }

            return false;
        }

        /// <summary>
        /// Shows one solution playback frame (DEC-170): the validated level after legal
        /// solver moves. It skips only LevelData.Validate, which refuses the cleared board
        /// because it has no block; the layout, block mapping and effect checks still run
        /// before the scene changes, and a failed rebuild shows the last shown board again.
        /// A frame it cannot show keeps the previous one and returns the reason.
        /// </summary>
        public bool TryShowFrame(LevelData frame, out string error)
        {
            if (m_disposed) throw new ObjectDisposedException(nameof(LevelPreviewHost));
            if (frame == null) throw new ArgumentNullException(nameof(frame));

            return TryBuild(frame, true, out error);
        }

        // The show path after the caller's validation: every check that can refuse the level
        // runs before the scene changes, then the build replaces the shown level, a draft or
        // a playback frame.
        private bool TryBuild(LevelData level, bool isFrame, out string error)
        {
            if (!TryPrepareLevel(level, out BoardLayout layout, out error))
            {
                return false;
            }

            try
            {
                Build(layout, level);
            }
            catch (Exception exception)
            {
                // The failed build may have returned the shown blocks or rebuilt the
                // floor, so it never stays on screen as a mix of two levels.
                error = exception.Message;
                if (!TryRestoreShownLevel())
                {
                    error += " The last valid preview could not be rebuilt, so the preview is empty.";
                }

                return false;
            }

            m_layout = layout;
            m_shownLevel = level;
            m_shownIsFrame = isFrame;
            LevelId = level.Id;
            return true;
        }

        // The pre-build chain shared by TryBuild and TryRestoreShownLevel: layout, block mapping, then every
        // effect adapter's own check, so the two paths never disagree about what they refuse.
        private bool TryPrepareLevel(LevelData level, out BoardLayout layout, out string error)
        {
            return BoardLayout.TryCreate(level, out layout, out error)
                && LevelPreviewBlockMapper.TryMap(level, layout, m_art.Catalog, m_blocks, out error)
                && TryCheckEffects(level, out error);
        }

        /// <summary>World bounds of the shown play area, for framing the Scene view.</summary>
        public bool TryGetBounds(out Bounds bounds)
        {
            if (m_layout == null || m_view == null)
            {
                bounds = default;
                return false;
            }

            bounds = GetWorldBounds(m_layout);
            return true;
        }

        // The board's local bounds placed under the environment's board root, shared by the Scene-view framing
        // and the preview camera's own framing in Build.
        private Bounds GetWorldBounds(IBoardLayout layout)
        {
            Bounds local = layout.LocalBounds;
            return new Bounds(m_view.BoardRoot.TransformPoint(local.center), local.size);
        }

        /// <summary>Returns every block, releases every pool and destroys the host root.</summary>
        public void Dispose()
        {
            if (m_disposed)
            {
                return;
            }

            m_disposed = true;
            m_shownLevel = null;
            m_validDraft = null;
            LevelId = null;
            ClearScene();
        }

        // Shows the blocks already mapped into m_blocks and the level's barriers in their start
        // state; creates the root on first use.
        private void Build(BoardLayout layout, LevelData level)
        {
            try
            {
                CreateRoot();
                ReturnBlocks();
                PrepareFactory();
                m_view.Build(layout, level);
                ILevelPreviewEffect[] effects = GetEffects();
                for (int effect = 0; effect < effects.Length; effect++)
                    effects[effect].Prepare(level, m_blocks, m_art, m_blocksRoot, m_camera.GetComponent<Camera>());
                for (int index = 0; index < m_blocks.Count; index++)
                {
                    LevelPreviewBlock block = m_blocks[index];
                    BlockPiece piece = m_factory.Create(new BlockPieceSpawnData(
                        block.Color, block.Cells, block.LocalPosition));
                    m_active.Add(piece);
                    for (int effect = 0; effect < effects.Length; effect++) effects[effect].Attach(index, piece.transform);
                }

                // Framed once per build, like the Game scene, so the Game view shows the game angle.
                m_camera.Frame(GetWorldBounds(layout));
                for (int effect = 0; effect < effects.Length; effect++) effects[effect].Reproject();
            }
            finally
            {
                // Pools create their clones lazily, so flag the whole hierarchy after
                // each build, even a failed one, so no clone can reach the scene file.
                if (m_root != null)
                {
                    SetDontSave(m_root.transform);
                }
            }
        }

        // Starts from an empty scene, so a root left half-built by CreateRoot is
        // never reused, and shows the last valid level again. On failure the preview
        // stays empty and LevelId becomes null, so the next valid level is framed.
        private bool TryRestoreShownLevel()
        {
            ClearScene();
            if (m_shownLevel == null)
            {
                return true;
            }

            try
            {
                if (TryPrepareLevel(m_shownLevel, out BoardLayout layout, out _))
                {
                    Build(layout, m_shownLevel);
                    m_layout = layout;
                    return true;
                }
            }
            catch (Exception)
            {
                // The caller reports the original failure and that the preview is empty.
            }

            ClearScene();
            m_shownLevel = null;
            LevelId = null;
            return false;
        }

        // Resets every scene reference first, so a failing release step cannot leave a
        // stale root for the next build; the root is destroyed even if a release throws.
        private void ClearScene()
        {
            GameObject root = m_root;
            EnvironmentView view = m_view;
            BlockPieceFactory factory = m_factory;
            ILevelPreviewEffect[] effects = m_effects ?? Array.Empty<ILevelPreviewEffect>();
            m_effects = null;
            m_root = null;
            m_view = null;
            m_blocksRoot = null;
            m_camera = null;
            m_factory = null;
            m_layout = null;
            m_active.Clear();
            m_pool.Clear();
            try
            {
                // Effect visuals (Ice shells, Arrow markers) ride on block roots, so they leave them before the factory
                // destroys those roots.
                DisposeEffects(effects, 0);
            }
            finally
            {
                try
                {
                    // Dispose returns every checked-out piece before releasing its pools.
                    factory?.Dispose();
                }
                finally
                {
                    try
                    {
                        if (view != null)
                        {
                            view.Release();
                        }
                    }
                    finally
                    {
                        if (root != null)
                        {
                            Object.DestroyImmediate(root);
                        }
                    }
                }
            }
        }

        private void CreateRoot()
        {
            if (m_root != null)
            {
                return;
            }

            m_root = EditorUtility.CreateGameObjectWithHideFlags(RootName, HideFlags.DontSave);
            SceneManager.MoveGameObjectToScene(m_root, m_scene);

            // A plain clone: the preview must never hold a prefab connection to save.
            GameObject environment = Object.Instantiate(m_environmentPrefab, m_root.transform);
            environment.name = m_environmentPrefab.name;
            m_view = environment.GetComponent<EnvironmentView>();
            if (m_view == null) throw new InvalidOperationException("The environment prefab has no EnvironmentView.");
            m_view.Construct(m_database);

            GameObject blocks = EditorUtility.CreateGameObjectWithHideFlags("Blocks", HideFlags.DontSave);
            m_blocksRoot = blocks.transform;
            m_blocksRoot.SetParent(m_view.BoardRoot, false);

            GameObject light = EditorUtility.CreateGameObjectWithHideFlags(
                "Preview Light", HideFlags.DontSave, typeof(Light));
            light.transform.SetParent(m_root.transform, false);
            light.transform.localRotation = Quaternion.Euler(s_lightEuler);
            Light directional = light.GetComponent<Light>();
            directional.type = LightType.Directional;
            directional.color = s_lightColor;
            directional.intensity = k_lightIntensity;
            directional.shadows = LightShadows.Soft;
            directional.shadowStrength = k_shadowStrength;

            GameObject cameraObject = EditorUtility.CreateGameObjectWithHideFlags(
                "Preview Camera", HideFlags.DontSave, typeof(Camera), typeof(BoardCamera));
            cameraObject.transform.SetParent(m_root.transform, false);
            Camera previewCamera = cameraObject.GetComponent<Camera>();
            previewCamera.clearFlags = CameraClearFlags.SolidColor;
            previewCamera.backgroundColor = s_backgroundColor;
            previewCamera.farClipPlane = k_farClipPlane;
            // URP adds this component on the first render otherwise, after the build has
            // flagged the hierarchy DontSave; adding it here lets that flag cover it.
            previewCamera.GetUniversalAdditionalCameraData();
            m_camera = cameraObject.GetComponent<BoardCamera>();
        }

        // The current scene root's effect adapters, created on first use: the pre-build check may need them before
        // the root exists. ClearScene disposes and forgets them.
        private ILevelPreviewEffect[] GetEffects()
        {
            return m_effects ??= m_effectFactory?.Invoke() ?? Array.Empty<ILevelPreviewEffect>();
        }

        // Lets every adapter that checks its needs reject the mapped level before the scene changes, for example when
        // the board piece catalog lacks a prefab it would show.
        private bool TryCheckEffects(LevelData level, out string error)
        {
            ILevelPreviewEffect[] effects = GetEffects();
            for (int effect = 0; effect < effects.Length; effect++)
            {
                if (effects[effect] is ILevelPreviewEffectCheck check && !check.TryCheck(level, m_blocks, m_art, out error))
                {
                    return false;
                }
            }

            error = null;
            return true;
        }

        private void ReturnBlocks()
        {
            // Effect visuals ride on the block roots, so they return to their own pools first.
            ILevelPreviewEffect[] effects = m_effects ?? Array.Empty<ILevelPreviewEffect>();
            for (int effect = 0; effect < effects.Length; effect++) effects[effect].ReturnAll();
            for (int index = 0; index < m_active.Count; index++)
            {
                m_factory.Return(m_active[index]);
            }

            m_active.Clear();
        }

        // The factory cannot grow its prepared root pool, so a level that needs more roots
        // or cells than prepared replaces it with one sized for both the old and new
        // needs. The root pool therefore only grows to the largest level shown. Roots hold no
        // tiles; the shared tile pool is warmed with the level's tiles and keeps them (DEC-184).
        private void PrepareFactory()
        {
            m_pool.StartLevel();
            for (int index = 0; index < m_blocks.Count; index++)
            {
                m_pool.AddBlock(m_blocks[index]);
            }

            // GrowToCover always runs, so the prepared size grows even when the factory is still missing.
            bool covered = m_pool.GrowToCover();
            if (m_factory != null && covered)
            {
                m_factory.WarmTiles(m_pool.Tiles);
                return;
            }

            DisposeFactory();
            m_factory = new BlockPieceFactory(m_art, m_blocksRoot);
            try
            {
                // A board without blocks (a cleared playback frame) needs no root, and Prepare refuses a count of 0.
                LevelPreviewPoolPlan.Size prepared = m_pool.Prepared;
                if (prepared.Roots > 0)
                {
                    m_factory.Prepare(prepared.Roots, prepared.Cells);
                }

                m_factory.WarmTiles(m_pool.Tiles);
            }
            catch
            {
                // A half-prepared factory would not match the prepared sizes; start clean next time.
                DisposeFactory();
                m_pool.Clear();
                throw;
            }
        }

        private void DisposeFactory()
        {
            if (m_factory == null)
            {
                return;
            }

            ReturnBlocks();
            m_factory.Dispose();
            m_factory = null;
        }

        // Disposes every adapter from this index on, even when an earlier one throws, so no pool outlives the root.
        private static void DisposeEffects(ILevelPreviewEffect[] effects, int from)
        {
            if (from >= effects.Length)
            {
                return;
            }

            try
            {
                effects[from]?.Dispose();
            }
            finally
            {
                DisposeEffects(effects, from + 1);
            }
        }

        private static void SetDontSave(Transform root)
        {
            root.gameObject.hideFlags |= HideFlags.DontSave;
            foreach (Component component in root.GetComponents<Component>())
            {
                component.hideFlags |= HideFlags.DontSave;
            }

            for (int index = 0; index < root.childCount; index++)
            {
                SetDontSave(root.GetChild(index));
            }
        }
    }
}
