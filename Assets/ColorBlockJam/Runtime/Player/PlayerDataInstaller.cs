using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using ColorBlockJam.Haptics;
using ColorBlockJam.Player.Lives;
using ColorBlockJam.Player.Storage;
using ColorBlockJam.Runtime.Currencies;
using global::Haptics;
using Onity.DI;
using Onity.Unity.Installers;
using UnityEngine;

namespace ColorBlockJam.Player
{
    /// <summary>
    /// Binds the currency catalog, the player snapshot store and the single PlayerSaveService to the persistent
    /// ProjectContext, after ProjectScopeInstaller, and loads the player data before Init routes to Hub (design 4.6).
    /// Editor play uses an in-memory store; players use the encrypted file store under persistentDataPath/player.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PlayerDataInstaller : MonoInstaller
    {
        private const string k_saveFolder = "player";

        [Tooltip("Every currency the game knows, in Home top-bar order (design 3.1).")]
        [SerializeField] private CurrencyCatalog m_currencyCatalog;

        public override void InstallBindings(OnityContainer container)
        {
            if (m_currencyCatalog == null)
            {
                throw new InvalidOperationException("Project currency catalog is missing.");
            }

            if (!m_currencyCatalog.Validate(out string error))
            {
                throw new InvalidOperationException($"Project currency catalog is invalid: {error}");
            }

            container.BindScriptableObject(m_currencyCatalog);
            container.BindInstance<IPlayerSnapshotStore>(CreateStore());
            container.BindInstance<IUtcClock>(new SystemUtcClock());

            // A container singleton, so the project container disposes it at the app scope's end. Its constructor
            // takes the BoosterCatalog that ProjectScopeInstaller bound before this installer.
            container.BindInterfacesAndSelfTo<PlayerSaveService>().AsSingle();

            // Lives over the save service's `life` currency (DEC-174); Hub and Game resolve ILives from this scope.
            container.BindInterfacesAndSelfTo<LivesService>().AsSingle();

            // Synchronous build callbacks run on the main thread inside Build(), before any async callback, so the
            // service is constructed there and the async load only awaits it.
            PlayerSaveService service = null;
            container.RegisterBuildCallback(resolver => service = resolver.Resolve<PlayerSaveService>());

            // Haptics follow the committed Vibration setting (DEC-156). The haptics domain owns the service and the
            // game-event adapter; the standalone Haptics library owns the device output. This scope binds them
            // because the setting lives here. Both are container
            // singletons, so the project container disposes them at the app scope's end. The synchronous build
            // callback constructs the output on the main thread, where the Android side sets up its JNI references.
            container.BindInterfacesAndSelfTo<PlayerVibrationToggle>().AsSingle();
            container.BindInterfacesAndSelfTo<DeviceHapticOutput>().AsSingle();
            container.BindInterfacesAndSelfTo<HapticsService>().AsSingle();
            container.RegisterBuildCallback(resolver => resolver.Resolve<IHapticOutput>());
            CancellationToken ownerToken = destroyCancellationToken;
            container.RegisterBuildCallbackAsync((resolver, cancellationToken) =>
                LoadAsync(service, cancellationToken, ownerToken));
        }

        // Unity APIs are read here, on the main thread: Onity runs build callbacks after ConfigureAwait(false), so
        // the load can resume on a worker thread (design 4.3). Editor play never touches the player's save.
        private static IPlayerSnapshotStore CreateStore()
        {
            if (Application.isEditor)
            {
                return new MemoryPlayerSnapshotStore();
            }

            string directory = Path.Combine(Application.persistentDataPath, k_saveFolder);
            PlayerSaveCipher cipher = PlayerSaveCipher.FromDeviceId(SystemInfo.deviceUniqueIdentifier);
            return new FilePlayerSnapshotStore(directory, new SystemFileOperations(), cipher);
        }

        // Owner: the project scope build, which awaits this callback after ProjectScopeInstaller's. Stops: build
        // cancellation, installer destruction, or service disposal (the service links its own lifetime token).
        private static async Task LoadAsync(PlayerSaveService service, CancellationToken cancellationToken,
            CancellationToken ownerToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ownerToken.ThrowIfCancellationRequested();
            if (service == null)
            {
                throw new InvalidOperationException("The player save service was not resolved during the build.");
            }

            using (CancellationTokenSource lifetime =
                   CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, ownerToken))
            {
                await service.LoadAsync(lifetime.Token);
            }
        }
    }
}
