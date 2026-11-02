#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using ColorBlockJam.Domain.Movement;
using ColorBlockJam.Domain.Session;
using ColorBlockJam.Environment;
using ColorBlockJam.Gameplay.Board;
using ColorBlockJam.Gameplay.Effects;
using ColorBlockJam.Gameplay.Levels;
using ColorBlockJam.Gameplay.Session;
using ColorBlockJam.Presentation.Board;
using ColorBlockJam.Runtime.Boosters;
using ColorBlockJam.Runtime.Layout;
using ColorBlockJam.Runtime.Levels;
using NUnit.Framework;
using Onity.Pooling;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace ColorBlockJam.Tests.Gameplay.PlayMode.Fixtures
{
    /// <summary>
    /// The Game scene's board presenter as its installer composes it: a real Environment prefab clone, the real
    /// board art, the Ice effect module on the shared Ice material, a camera and an overlay document for the
    /// counters, and the project's booster catalog for the Hammer, Rocket and Vacuum casts (B-19, B-20). Build
    /// converts a LevelData, prepares its effect metadata, builds and places the attempt exactly in the session's
    /// Home order, and returns the attempt's simulation for booster commands.
    /// </summary>
    public sealed class BoosterPresenterScene : IDisposable
    {
        private const string k_environmentPrefabPath = "Assets/ColorBlockJam/Prefabs/Environment/Environment.prefab";
        private const string k_environmentDatabasePath = "Assets/ColorBlockJam/Data/Environment/EnvironmentDatabase.asset";
        private const string k_iceMaterialPath = "Assets/ColorBlockJam/Art/Materials/Effects/MAT_Ice.mat";
        private const string k_panelPath = "Assets/ColorBlockJam/Data/UI/BoardOverlayPanelSettings.asset";
        private const string k_boosterCatalogPath = "Assets/ColorBlockJam/Data/Boosters/BoosterCatalog.asset";

        private readonly GameObject m_root;
        private readonly GameObject m_view;
        private readonly BlockEffectContextSource m_effects;
        private readonly EffectAttemptHookSource m_hooks;

        public BoosterPresenterScene()
        {
            m_root = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(k_environmentPrefabPath));
            Environment = m_root.GetComponent<EnvironmentView>();
            Environment.Construct(AssetDatabase.LoadAssetAtPath<EnvironmentDatabase>(k_environmentDatabasePath));
            BoardArt art = PresenterScene.CreateArt();
            Art = art;

            // A camera above the board looking down, and the counters' overlay document on the Game scene's panel.
            m_view = new GameObject("Booster presenter view", typeof(Camera), typeof(UIDocument));
            Camera = m_view.GetComponent<Camera>();
            Camera.transform.SetPositionAndRotation(new Vector3(2f, 12f, -2f), Quaternion.Euler(75f, 0f, 0f));
            Document = m_view.GetComponent<UIDocument>();
            Document.panelSettings = AssetDatabase.LoadAssetAtPath<PanelSettings>(k_panelPath);
            Assert.That(Document.panelSettings, Is.Not.Null);

            Material ice = AssetDatabase.LoadAssetAtPath<Material>(k_iceMaterialPath);
            Assert.That(ice, Is.Not.Null);
            m_effects = new BlockEffectContextSource(new IBlockEffectModule[] { new IceEffectModule(ice) });
            m_hooks = new EffectAttemptHookSource(m_effects);
            Catalog = AssetDatabase.LoadAssetAtPath<BoosterCatalog>(k_boosterCatalogPath);
            Assert.That(Catalog, Is.Not.Null);
            Presenter = new BoardPresenter(Environment, art, m_effects,
                new BlockEffectViewContext(art, Environment.BoardRoot, Camera, Document), Catalog);
        }

        public EnvironmentView Environment { get; }

        public BoardPresenter Presenter { get; }

        public Camera Camera { get; }

        public UIDocument Document { get; }

        public BoosterCatalog Catalog { get; }

        public AttemptLevel Level { get; private set; }

        public BlockEffectContextSource Effects => m_effects;

        /// <summary>The board art the presenter uses: the palette resolves each block's expected tint.</summary>
        public BoardArt Art { get; }

        /// <summary>The Game scene's effect hook source over the Ice module, for a LevelSession over this presenter.</summary>
        public EffectAttemptHookSource Hooks => m_hooks;

        /// <summary>Converts data from its blocks' own cells, without building it.</summary>
        public AttemptLevel Convert(LevelData data)
        {
            Assert.That(BoardLayout.TryCreate(data, out BoardLayout layout, out string error), Is.True, error);
            Assert.That(AttemptLevelConverter.TryConvert(data, layout, out AttemptLevel level, out error), Is.True, error);
            return level;
        }

        /// <summary>Prepares the level's effect metadata on Home, as the Game scene's loader does before the session's TryPrepare.</summary>
        public void PrepareEffects(LevelData data, AttemptLevel level)
        {
            Level = level;
            m_effects.Prepare(level, data);
        }

        /// <summary>Converts, prepares, builds and places one attempt of data on Home and returns its simulation.</summary>
        public BoardSimulation Build(LevelData data) => Build(data, Convert(data));

        /// <summary>Prepares, builds and places one attempt of a level converted from data and returns its simulation.</summary>
        public BoardSimulation Build(LevelData data, AttemptLevel level)
        {
            PrepareEffects(data, level);
            Assert.That(Presenter.TryBuild(level, out string error), Is.True, error);
            BoardSimulation simulation = new BoardSimulation(level.Model, m_hooks.Create(level), DragSettings.Default);
            Presenter.Place(simulation.Preview);
            return simulation;
        }

        /// <summary>A new attempt of the level Build last built: the session's Restart after Teardown.</summary>
        public BoardSimulation Restart()
        {
            Presenter.StopTweens();
            Presenter.ReturnPieces();
            Assert.That(Presenter.TryBuild(Level, out string error), Is.True, error);
            BoardSimulation simulation = new BoardSimulation(Level.Model, m_hooks.Create(Level), DragSettings.Default);
            Presenter.Place(simulation.Preview);
            return simulation;
        }

        /// <summary>Every object a live Onity pool has created; returned objects stay counted while they wait.</summary>
        public static int CountPooledObjects()
        {
            List<OnityPoolDiagnosticsSnapshot> snapshots = new List<OnityPoolDiagnosticsSnapshot>();
            OnityPoolDiagnosticsRegistry.GetSnapshots(snapshots);
            int count = 0;
            foreach (OnityPoolDiagnosticsSnapshot snapshot in snapshots)
            {
                if (!snapshot.IsDisposed)
                {
                    count += snapshot.CountAll;
                }
            }

            return count;
        }

        /// <summary>Every object under the board root: block roots, their tiles, shells, markers and casts.</summary>
        public int CountBoardObjects() => Environment.BoardRoot.GetComponentsInChildren<Transform>(true).Length;

        public void Dispose()
        {
            Presenter.Dispose();
            Object.DestroyImmediate(m_view);
            Object.DestroyImmediate(m_root);
        }
    }
}
#endif
