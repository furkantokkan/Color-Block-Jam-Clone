using System;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;

namespace ColorBlockJam.Runtime.Levels
{
    /// <summary>
    /// The game's level loads through Addressables (DEC-192, DEC-196): the database manifest once per app start before
    /// Init routes to Hub, and the pack of the level about to be played on Home, read with the runtime support gate. The
    /// catalog keeps one pack, so Restart and Next Level inside that pack load nothing (docs/Level-Data-Format.md).
    /// </summary>
    public static class LevelCatalogLoader
    {
        /// <summary>
        /// Reads the catalog's manifest; no pack is resident afterwards. Owner: the project scope build
        /// (ProjectScopeInstaller), which awaits it. Stops: when it returns, or on the token; the Addressables handle is
        /// released before it returns. A failure throws, which faults the project scope's ready task, so the game never
        /// reads a catalog whose load failed.
        /// </summary>
        public static async Task LoadManifestAsync(LevelCatalog catalog, CancellationToken cancellationToken = default)
        {
            if (catalog == null)
            {
                throw new ArgumentNullException(nameof(catalog));
            }

            if (string.IsNullOrWhiteSpace(catalog.JsonGuid))
            {
                throw new InvalidOperationException("The active level database manifest is missing.");
            }

            AsyncOperationHandle<TextAsset> handle = Addressables.LoadAssetAsync<TextAsset>(catalog.JsonGuid);
            try
            {
                await AddressableWait.UntilDoneAsync(handle, cancellationToken);
                if (handle.Status != AsyncOperationStatus.Succeeded || handle.Result == null)
                {
                    throw new InvalidOperationException(
                        $"Could not load the active level database manifest {catalog.JsonGuid}.", handle.OperationException);
                }

                if (!catalog.LoadManifestJson(handle.Result.text, out string error))
                {
                    throw new InvalidOperationException($"The project level database manifest is invalid: {error}");
                }
            }
            finally
            {
                Addressables.Release(handle);
            }
        }

        /// <summary>
        /// Makes the pack of the level at <paramref name="levelIndex"/> resident and releases every other pack. Returns
        /// null when the level can be read, else the reason it cannot; a resident pack returns at once. Owner: the Game
        /// scene's attempt build (GameAttemptLoader), which awaits it on Home. Stops: when it returns, or on the token
        /// (an OperationCanceledException); the Addressables handle is released before it returns.
        /// </summary>
        public static async Task<string> LoadPackAsync(LevelCatalog catalog, int levelIndex,
            CancellationToken cancellationToken = default)
        {
            if (catalog == null)
            {
                throw new ArgumentNullException(nameof(catalog));
            }

            LevelManifest manifest = catalog.Manifest;
            if (levelIndex < 0 || levelIndex >= manifest.Count)
            {
                return $"level index {levelIndex} is outside the database's {manifest.Count} level(s)";
            }

            if (catalog.IsResident(levelIndex))
            {
                return null;
            }

            // Only a named database has packs that are not resident; a single-file catalog has them all.
            int packIndex = manifest.GetPackIndex(levelIndex);
            string address = LevelJsonFiles.GetAddress(manifest.Name, manifest.GetPackAt(packIndex).File);
            AsyncOperationHandle<TextAsset> handle = Addressables.LoadAssetAsync<TextAsset>(address);
            try
            {
                await AddressableWait.UntilDoneAsync(handle, cancellationToken);
                if (handle.Status != AsyncOperationStatus.Succeeded || handle.Result == null)
                {
                    Exception exception = handle.OperationException;
                    return exception == null
                        ? $"level pack {address} could not be loaded"
                        : $"level pack {address} could not be loaded: {exception.Message}";
                }

                if (!catalog.LoadPackJson(packIndex, handle.Result.text, LevelReadMode.Runtime, out string error))
                {
                    return $"level pack {address} is invalid: {error}";
                }

                catalog.ReleasePacksExcept(packIndex);
                return null;
            }
            finally
            {
                Addressables.Release(handle);
            }
        }
    }
}
