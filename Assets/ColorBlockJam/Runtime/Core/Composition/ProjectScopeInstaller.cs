using System;
using System.Threading;
using System.Threading.Tasks;
using ColorBlockJam.Runtime.Boosters;
using ColorBlockJam.Runtime.Levels;
using ColorBlockJam.Runtime.SceneFlow;
using Onity.DI;
using Onity.Unity.Installers;
using Onity.Unity.SceneFlow;
using UnityEngine;

namespace ColorBlockJam.Runtime.Composition
{
    /// <summary>
    /// Binds app-wide scene routing and the level catalog to the persistent ProjectContext, and reads the manifest of
    /// the level database it names into the catalog (after any published level content update, DEC-162) before Init
    /// routes to Hub; the Game scene loads a level's pack on demand (DEC-192, DEC-194, DEC-196). Every block builds
    /// from its own cells, so the project scope loads no piece library and learns no shapes (DEC-193). Every scene
    /// change runs behind its black screen fade (DEC-198).
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ProjectScopeInstaller : MonoInstaller
    {
        [SerializeField] private OnitySceneFlowProfile m_sceneFlowProfile;
        [SerializeField] private BoosterCatalog m_boosterCatalog;
        [Tooltip("The black fade over every scene change: the ScreenFade child of this prefab (DEC-198).")]
        [SerializeField] private ScreenFadeView m_screenFade;
        [Tooltip("The manifest of the level database the game plays. The Level Editor's database choice sets it.")]
        [SerializeField] private LevelJsonReference m_levelJson = new LevelJsonReference(string.Empty);

        /// <summary>The GUID of the manifest of the level database the game plays, or empty when none is chosen.</summary>
        public string LevelJsonGuid => m_levelJson.AssetGUID;

        public override void InstallBindings(OnityContainer container)
        {
            if (m_sceneFlowProfile == null)
            {
                throw new InvalidOperationException("Project scene flow profile is missing.");
            }

            if (m_boosterCatalog == null)
            {
                throw new InvalidOperationException("Project booster catalog is missing.");
            }

            if (!m_boosterCatalog.Validate(out string error))
            {
                throw new InvalidOperationException($"Project booster catalog is invalid: {error}");
            }

            if (m_screenFade == null)
            {
                throw new InvalidOperationException("Project screen fade is missing.");
            }

            container.BindScriptableObject(m_sceneFlowProfile);
            container.BindScriptableObject(m_boosterCatalog);
            // The project scope owns the scene fade and the prepared next level, because a scene change and a prepared
            // scene outlive the scene that asks for them (DEC-198, DEC-199).
            container.BindInstance<IScreenFade>(m_screenFade);
            container.BindInterfacesAndSelfTo<ActiveSceneReadiness>().AsSingle();
            container.BindInterfacesAndSelfTo<SceneFadeTransition>().AsSingle();
            container.BindInterfacesAndSelfTo<LevelPreloader>().AsSingle();
            // The installer names the active database's manifest; the container holds the loaded levels (DEC-192,
            // DEC-194). A missing name fails the load, not the binding, like an unreadable file.
            LevelCatalog levels = new LevelCatalog(LevelJsonGuid);
            container.BindInstance(levels);
            CancellationToken ownerToken = destroyCancellationToken;
            container.RegisterBuildCallbackAsync((resolver, cancellationToken) =>
                LoadAsync(levels, cancellationToken, ownerToken));
        }

        // Owner: the project scope build, which awaits this callback. Stops: build cancellation or installer destruction;
        // capture the component token during installation.
        private static async Task LoadAsync(LevelCatalog levels, CancellationToken cancellationToken,
            CancellationToken ownerToken)
        {
            using (CancellationTokenSource lifetime =
                   CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, ownerToken))
            {
                lifetime.Token.ThrowIfCancellationRequested();
                // Once per app start and before any level JSON is read; nothing checks for updates afterwards (DEC-162).
                await LevelContentUpdater.ApplyAsync(lifetime.Token);
                // Only the manifest: the Game scene loads the pack of the level it plays (DEC-196).
                await LevelCatalogLoader.LoadManifestAsync(levels, lifetime.Token);
            }
        }
    }
}
