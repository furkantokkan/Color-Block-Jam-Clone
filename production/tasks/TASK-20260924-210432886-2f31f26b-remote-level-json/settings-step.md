# Pending live-Editor settings write (DEC-162)

This step is not done yet. The shared Editor stayed in Play Mode (Hub) from
before 10:52 to at least 11:09 on 2026-09-28, so it could not take a settings
write. Until the write runs, `LevelContentUpdateTests.ProjectSettingsShipLevelsLocallyAndCheckARemoteCatalog`
(AC-1) fails by design.

Run it once through `UnityMCP` `execute_code` in the live Editor. Run it only
when the Editor is not compiling, not playing, and not running tests. It uses
only the tested `LevelContentUpdate.TryConfigure` (no Undo, no `SaveAssets`).

```csharp
var s = UnityEditor.AddressableAssets.AddressableAssetSettingsDefaultObject.SettingsExists
    ? UnityEditor.AddressableAssets.AddressableAssetSettingsDefaultObject.GetSettings(false) : null;
if (s == null) return "no settings";
var group = s.FindGroup("Level Database");
var update = group.GetSchema<UnityEditor.AddressableAssets.Settings.GroupSchemas.ContentUpdateGroupSchema>();
var bundled = group.GetSchema<UnityEditor.AddressableAssets.Settings.GroupSchemas.BundledAssetGroupSchema>();
// Refuse if another session left unsaved changes in these assets; saving would persist them.
if (UnityEditor.EditorUtility.IsDirty(s) || UnityEditor.EditorUtility.IsDirty(update) || UnityEditor.EditorUtility.IsDirty(bundled))
    return "dirty before write: stop";
string error;
bool ok = ColorBlockJam.Editor.Levels.LevelContentUpdate.TryConfigure(s, out error);
var p = s.profileSettings;
string rel = p.GetProfileId("Release");
return ok + " | " + error + " | remote=" + s.BuildRemoteCatalog + " disableOnStart=" + s.DisableCatalogUpdateOnStartup
    + " timeout=" + s.CatalogRequestsTimeout + " active=" + p.GetProfileName(s.activeProfileId)
    + " releaseLoad=" + p.GetValueByName(rel, "Remote.LoadPath") + " static=" + update.StaticContent
    + " dirtyAfter=" + UnityEditor.EditorUtility.IsDirty(s)
    + " entries=" + ColorBlockJam.Editor.Levels.LevelDatabaseAddressables.GetEntries().Count;
```

Then check the result on disk:

- **`AddressableAssetSettings.asset`:**
  - `m_BuildRemoteCatalog: 1`, `m_DisableCatalogUpdateOnStart: 1`, `m_CatalogRequestsTimeout: 10`.
  - `m_RemoteCatalogBuildPath`/`m_RemoteCatalogLoadPath` ids `0ba201f6…`/`c0f73993…`.
  - A second profile `Release` whose `Remote.LoadPath` is `https://configure-level-content-url.invalid/[BuildTarget]`.
  - `m_ActiveProfileId` set to the Release id.
- **`Level Database_ContentUpdateGroupSchema.asset`:** `m_StaticContent: 1`.
- **`Level Database_BundledAssetGroupSchema.asset`:** byte-identical (its paths are already local).
- **`ProjectSettings/EditorBuildSettings.asset`:** still has `com.unity.addressableassets`.
- **Level Editor:** `GetSettings(false)` is not null and `GetEntries()` is not empty, so the database dropdown still lists the group.
