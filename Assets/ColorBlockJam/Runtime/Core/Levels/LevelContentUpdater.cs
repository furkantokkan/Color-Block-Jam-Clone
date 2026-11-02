using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.AddressableAssets.ResourceLocators;
using UnityEngine.ResourceManagement;
using UnityEngine.ResourceManagement.AsyncOperations;

namespace ColorBlockJam.Runtime.Levels
{
    /// <summary>Awaits an Addressables handle without allocating, shared by every project-scope Addressables wait.</summary>
    internal static class AddressableWait
    {
        public static async Task UntilDoneAsync(AsyncOperationHandle handle, CancellationToken cancellationToken)
        {
            while (!handle.IsDone)
            {
                cancellationToken.ThrowIfCancellationRequested();
                await Task.Yield();
            }

            cancellationToken.ThrowIfCancellationRequested();
        }
    }

    /// <summary>
    /// Applies a published level content update once, before any level JSON is read: it checks the remote catalog,
    /// updates it and downloads the bundles of every manifest and pack (the <see cref="LevelJsonFiles.Label"/> label),
    /// so a launch plays the updated levels or the shipped ones, never a mix. On any failure it logs one warning and
    /// keeps the cached or shipped levels (DEC-162, DEC-196, docs/Level-Content-Updates.md).
    /// </summary>
    internal static class LevelContentUpdater
    {
        private const string k_warningPrefix = "Level content update skipped";

        /// <summary>
        /// Owner: <see cref="ColorBlockJam.Runtime.Composition.ProjectScopeInstaller.LoadAsync"/>, which awaits it
        /// once per app start. Stops: when it returns, or on the build token. Every handle is released before it
        /// returns. Requests are bounded by the Addressables catalog request timeout and the content update group's
        /// bundle timeout.
        /// </summary>
        public static async Task ApplyAsync(CancellationToken cancellationToken)
        {
            AsyncOperationHandle<IResourceLocator> initialize = Addressables.InitializeAsync(false);
            try
            {
                await AddressableWait.UntilDoneAsync(initialize, cancellationToken);
                if (initialize.Status != AsyncOperationStatus.Succeeded)
                {
                    return;
                }
            }
            finally
            {
                Addressables.Release(initialize);
            }

            // The Editor's "Use Asset Database" play mode script and a build without a remote catalog have no
            // catalog that can update, so the check ends here without a request (DEC-162).
            if (!TryFindUpdatableCatalog(out _, out _))
            {
                return;
            }

            // Addressables logs every failed request as an exception. Offline those failures are expected, so
            // while this update runs they are held back and its one warning reports them instead.
            Action<AsyncOperationHandle, Exception> exceptionHandler = ResourceManager.ExceptionHandler;
            ResourceManager.ExceptionHandler = IgnoreException;
            string catalogFailure;
            string downloadFailure;
            try
            {
                catalogFailure = await UpdateCatalogAsync(cancellationToken);

                // Runs even when the check failed: an earlier launch may have updated the catalog but not the bundles.
                downloadFailure = await DownloadLevelJsonAsync(cancellationToken);
            }
            finally
            {
                ResourceManager.ExceptionHandler = exceptionHandler;
            }

            if (downloadFailure != null)
            {
                Debug.LogWarning(catalogFailure == null
                    ? $"{k_warningPrefix}: {downloadFailure} (DEC-162)."
                    : $"{k_warningPrefix}: {catalogFailure}; {downloadFailure} (DEC-162).");
            }
            else if (catalogFailure != null)
            {
                Debug.LogWarning($"{k_warningPrefix}: {catalogFailure}. The cached or shipped levels play (DEC-162).");
            }
        }

        // Returns why the catalog could not be checked or updated, or null.
        private static async Task<string> UpdateCatalogAsync(CancellationToken cancellationToken)
        {
            AsyncOperationHandle<List<string>> check = Addressables.CheckForCatalogUpdates(false);
            try
            {
                await AddressableWait.UntilDoneAsync(check, cancellationToken);
                if (check.Status != AsyncOperationStatus.Succeeded)
                {
                    return Describe("the catalog check failed", check);
                }

                if (check.Result == null || check.Result.Count == 0)
                {
                    return null;
                }

                AsyncOperationHandle<List<IResourceLocator>> update = Addressables.UpdateCatalogs(check.Result, false);
                try
                {
                    await AddressableWait.UntilDoneAsync(update, cancellationToken);
                    return update.Status == AsyncOperationStatus.Succeeded
                        ? null
                        : Describe("the catalog update failed", update);
                }
                finally
                {
                    Addressables.Release(update);
                }
            }
            finally
            {
                Addressables.Release(check);
            }
        }

        // Makes sure the bundles of every manifest and pack are on the device before any of them is read; returns why
        // they are not, or null. The shipped bundle is local, so only an update's bundles are downloaded.
        private static async Task<string> DownloadLevelJsonAsync(CancellationToken cancellationToken)
        {
            long bytes;
            AsyncOperationHandle<long> size = Addressables.GetDownloadSizeAsync(LevelJsonFiles.Label);
            try
            {
                await AddressableWait.UntilDoneAsync(size, cancellationToken);
                if (size.Status != AsyncOperationStatus.Succeeded)
                {
                    return Describe("the level JSON's download size is unknown", size);
                }

                bytes = size.Result;
            }
            finally
            {
                Addressables.Release(size);
            }

            if (bytes <= 0)
            {
                return null;
            }

            string failure;
            AsyncOperationHandle download = Addressables.DownloadDependenciesAsync(LevelJsonFiles.Label, false);
            try
            {
                await AddressableWait.UntilDoneAsync(download, cancellationToken);
                if (download.Status == AsyncOperationStatus.Succeeded)
                {
                    return null;
                }

                failure = Describe("the updated level bundle could not be downloaded", download);
            }
            finally
            {
                Addressables.Release(download);
            }

            // The updated catalog names a bundle this device lacks and cannot download now, for example on an
            // offline launch after an update was interrupted. This launch plays the shipped catalog instead; the
            // next online launch downloads the bundle (DEC-162).
            string fallback = await LoadShippedCatalogAsync(cancellationToken);
            return fallback == null
                ? $"{failure}. This launch plays the shipped levels"
                : $"{failure}; {fallback}";
        }

        // Replaces the updated catalog with the one shipped in the build for this launch; returns why not, or null.
        private static async Task<string> LoadShippedCatalogAsync(CancellationToken cancellationToken)
        {
            if (!TryFindUpdatableCatalog(out IResourceLocator updated, out ResourceLocatorInfo info))
            {
                return "the shipped catalog could not be found";
            }

            Addressables.RemoveResourceLocator(updated);
            AsyncOperationHandle<IResourceLocator> load =
                Addressables.LoadContentCatalogAsync(info.CatalogLocation.InternalId, false);
            try
            {
                await AddressableWait.UntilDoneAsync(load, cancellationToken);
                if (load.Status == AsyncOperationStatus.Succeeded)
                {
                    return null;
                }

                Addressables.AddResourceLocator(updated, info.LocalHash, info.CatalogLocation);
                return Describe("the shipped catalog could not be loaded", load);
            }
            finally
            {
                Addressables.Release(load);
            }
        }

        // The build's main content catalog: the only locator with a remote hash to compare.
        private static bool TryFindUpdatableCatalog(out IResourceLocator locator, out ResourceLocatorInfo info)
        {
            foreach (IResourceLocator candidate in Addressables.ResourceLocators)
            {
                ResourceLocatorInfo candidateInfo = Addressables.GetLocatorInfo(candidate);
                if (candidateInfo != null && candidateInfo.CanUpdateContent)
                {
                    locator = candidate;
                    info = candidateInfo;
                    return true;
                }
            }

            locator = null;
            info = null;
            return false;
        }

        private static string Describe(string what, AsyncOperationHandle handle)
        {
            Exception exception = handle.OperationException;
            return exception == null ? what : $"{what}: {exception.Message}";
        }

        private static void IgnoreException(AsyncOperationHandle handle, Exception exception)
        {
        }
    }
}
