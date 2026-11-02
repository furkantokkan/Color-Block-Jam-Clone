# Level Content Updates

How a level JSON fix reaches installed games after release, without a store
update. Decision: DEC-162 in `PROJECT.md`. Task:
`production/tasks/TASK-20260924-210432886-2f31f26b-remote-level-json`.

The mechanism is the Addressables content-update workflow of
`com.unity.addressables` 1.22.3. There is no custom downloader and no second
level authority: the Addressable level JSON stays the only source. The project
scope reads the database's manifest into its `LevelCatalog`
(`LevelCatalogLoader.LoadManifestAsync`, DEC-192), and the Game scene loads the
pack of the level it plays (`LoadPackAsync`, DEC-196). A fix to one level ships
one pack; adding, removing or moving a level also ships the manifest.

The hosting URL is not chosen yet. The `Release` profile holds the placeholder
`https://configure-level-content-url.invalid/[BuildTarget]`. The `.invalid`
domain never resolves, so a release build fails its check at once and plays
the shipped levels.

## Settings

| Setting | Value | Why |
| --- | --- | --- |
| Build Remote Catalog | On, build path `Remote.BuildPath`, load path `Remote.LoadPath` | The player can compare its catalog with a published one. |
| Disable Catalog Update On Startup | On | The game checks once, explicitly, at a point it owns. |
| Catalog request timeout | 10 s | No catalog or hash request waits forever. |
| `Level Database` group | Local build and load paths, Cannot Change Post Release (`StaticContent`) | Every build ships the JSON, so a first or offline launch always has levels. |
| Profiles | `Default` (unchanged: `http://[PrivateIpAddress]:[HostingServicePort]`) and `Release` (a copy of Default with the placeholder `Remote.LoadPath`), `Release` active | A player build bakes the active profile's URL. Default is only for Hosting Service tests. |
| Content state | Default path `Assets/AddressableAssetsData/<Platform>/addressables_content_state.bin` | Each player build writes it; a content update is built against the released one. |

`LevelContentUpdate.TryConfigure` applies these values through the Addressables
API. It is idempotent: it never replaces an existing `Release` profile, its URL
or the active profile choice, and it saves only the settings asset and the
level group's two schemas. `LevelDatabaseAddressables` creates a missing
`Level Database` group static too.

## Startup

`ProjectScopeInstaller`'s build callback runs `LevelContentUpdater.ApplyAsync`
once, right before `LevelCatalogLoader.LoadManifestAsync`, while Init shows the
loading screen (DEC-140).

1. It initializes Addressables. When no catalog can update, it returns without
   a request. This covers the Editor's "Use Asset Database" play mode script
   and builds without a remote catalog.
2. `CheckForCatalogUpdates`, and `UpdateCatalogs` when a newer catalog exists.
3. `GetDownloadSizeAsync` for the `level-json` label, which every manifest and
   pack carries, and `DownloadDependenciesAsync` for it when a bundle is not on
   the device. Only an update's bundles are remote, so this downloads just the
   changed packs and manifest, before any of them is read (DEC-196). This step
   runs even when step 2 failed, because an earlier launch may have updated the
   catalog but not the bundles.
4. When those bundles can neither be found nor downloaded, it removes the
   updated catalog and loads the catalog shipped in the build for this launch,
   so a launch plays the updated levels or the shipped ones, never a mix of a
   new manifest and old packs.

- **Owner and stop point.** The project scope build awaits it. It ends when it
  returns, or on the build token (scope disposal or installer destruction).
- **Lifecycle.** No `async void`, no work after startup, and every
  `AsyncOperationHandle` is released in a `finally`.
- **Bounds.** The 10 s catalog timeout bounds the hash and catalog requests.
  The bundle download is bounded by the update group's 10 s no-data timeout,
  which the publishing command sets.
- **One warning.** Addressables logs every failed request as an exception. While
  the update runs, `ResourceManager.ExceptionHandler` holds those back, and the
  update logs one warning, `Level content update skipped: ... (DEC-162).`, that
  names the reason. The previous handler comes back in a `finally`.

| Launch | Result |
| --- | --- |
| First launch, offline or with the placeholder URL | The check fails with one warning; the shipped JSON loads. |
| Online, update published | The catalog updates, the JSON bundle downloads, and the changed levels load. |
| Offline after an update was downloaded | The cached catalog and bundle load; one warning for the failed check. |
| Killed after the catalog update, before the bundle download, then offline | The bundle is missing and cannot download, so this launch loads the shipped catalog and the shipped levels, with one warning. The next online launch downloads the bundle. |
| Editor, "Use Asset Database" | Returns before any request; no warning. |

## Publishing a fix

1. **Before the release build.** Set `Remote.LoadPath` of the `Release` profile
   to the hosting URL, keeping `/[BuildTarget]`. Keep `Release` active and build
   the player. Check in `Assets/AddressableAssetsData/<Platform>/addressables_content_state.bin`
   with the release. Any later player build of that platform overwrites the
   file, so restore the released version before you publish an update.
2. **Edit.** Fix the level with the Level Editor and save. Save Level rewrites
   only that level's pack, and Create or Delete Level also rewrites the
   manifest (DEC-196).
3. **Build the update.** Run `Color Block Jam/Data/Build Level Content Update`
   with the active build target set to the released platform. The command:
   - refuses when Build Remote Catalog is off, when the active profile's
     `Remote.LoadPath` is empty or the placeholder (it names the profile), when
     a level database does not read whole or one of its packs fails the runtime
     read (it names the file), when a manifest or pack is not registered at its
     address with the `level-json` label, or when the group holds a level JSON no
     database lists (Configure Level Databases repairs the last two);
   - refuses when the content state is missing (it names the path), or was built
     for another catalog URL;
   - gathers the changed entries. It refuses when nothing changed, or when a
     change is not a level JSON in `Level Database`: this design covers only
     that case;
   - moves the changed JSON (the packs a save rewrote, a new pack, and the
     manifest when it changed) into a temporary remote `Level Content Update`
     group with a 10 s bundle timeout, runs `BuildContentUpdate`, then moves the
     JSON back and removes the group. The Level Editor and the next update see
     the authored layout. A new pack has no cached state, so Addressables counts
     it as changed and ships it too.
4. **Upload.** Upload every file in the reported `ServerData/<BuildTarget>`
   folder to the `Remote.LoadPath` URL. The catalog keeps the name
   `catalog_<app version>`, so only players of that version take the update.

The vendor's `CreateContentUpdateGroup` and `BuildContentUpdate` save project
assets. Run the command only as a deliberate publishing step.

## Limits

- **Old bundles.** They stay in the device's bundle cache (`autoCleanBundleCache`
  is off). A level bundle is a few kilobytes; a pack of 100 levels is about
  130 KB of JSON.
- **Settings not applied yet (2026-09-29).** The workspace's Addressables
  settings still have Build Remote Catalog off and no `Release` profile, so no
  player can receive an update until `LevelContentUpdate.TryConfigure` is run
  and saved. The user chose to keep Addressables local for now (DEC-196).
- **Code.** Do not change scripts or Addressables settings between a player
  build and its content updates (Addressables restriction).
- **Real URL.** Testing it needs an Android device check once the URL exists.

## Verification

Status on 2026-09-28: implementation complete. The Roslyn compile check passes
for `ColorBlockJam.Runtime`, `ColorBlockJam.Editor` and the EditMode test
assembly. Unity test runs are deferred to one combined batch run.

| Criterion | Channel |
| --- | --- |
| AC-1 settings | EditMode `LevelContentUpdateTests.ProjectSettingsShipLevelsLocallyAndCheckARemoteCatalog`, `ConfigureTurnsOnTheRemoteCatalogAndAStaticLocalLevelGroup`, `ConfigureKeepsAnExistingReleaseUrlAndActiveProfile`, `ConfigureRefusesWithoutTheLevelGroupAndChangesNothing`; `LevelDatabaseAddressablesTests.CreatedGroupKeepsShippedLevelsLocalAndStatic` |
| AC-2 offline or placeholder start | PlayMode `SceneFlowSmokeTests.ProjectScopeLoadsActiveAddressableLevelJson` and `InitKeepsTheLoadingScreenUntilHubIsReady` (Editor, Use Asset Database); manual Android build with the `Release` placeholder (logcat shows one warning, levels play). Pending. |
| AC-3 online update | Manual. Build the player with `Default` active and the Hosting Service running, change a level, run the command, and serve `ServerData`. Pending. |
| AC-4 offline relaunch and interrupted update | Manual, same build. Relaunch offline; then clear only the bundle cache (or kill the app after the catalog update) and relaunch offline. Pending. |
| AC-5 refusals | EditMode `LevelContentUpdateTests.PlaceholderUrlRefusesBeforeBuilding`, `InvalidLevelJsonRefusesAndNamesTheFile`, `MissingContentStateRefusesAndNamesThePath` |
| AC-6 owned, bounded startup | EditMode `LevelContentUpdateTests.StartupUpdateUsesNoAsyncVoid`; code review of `ProjectScopeInstaller.LevelContentUpdater`; the existing scene-flow PlayMode tests. |

Evidence files go under `docs/verification/level-content-updates/`.
