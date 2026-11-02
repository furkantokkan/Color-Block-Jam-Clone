# Evidence — Addressables settings registration

Recorded 2026-09-25 against the live Editor `ColorBlockJamCase2026@99077e4df1ecfaa4` (MCP for Unity, HTTP 127.0.0.1:8082, one transport).

## Before

- `AddressableAssetSettingsDefaultObject.GetSettings(false)` = null; `LevelDatabaseAddressables.GetEntries()` = empty.
- `ProjectSettings/EditorBuildSettings.asset`: `m_configObjects: {}`.

## Repair (user approval: "Yes, fix it", 2026-09-25)

- Checked `DefaultObject.asset` `m_AddressableAssetSettingsGuid` = `a7274985f03c24f41abdf9b5c1c09c7d` (AddressableAssetSettings.asset).
- `EditorBuildSettings.AddConfigObject("com.unity.addressableassets", DefaultObject.asset, true)` + `AssetDatabase.SaveAssets()`.
- Result: `settings=Assets/AddressableAssetsData/AddressableAssetSettings.asset`; `listed b93477650b31db14b8d2e3b792a56b08 Assets/ColorBlockJam/Data/Levels/LevelDatabase.json`; `scenes=4`.
- File now contains `com.unity.addressableassets: {fileID: 11400000, guid: 1a5f6faa0e2b5304893ecbadc126cc9d, type: 2}`; scene list unchanged (4 entries).

## Verification

- AC-1/AC-3: EditMode `AddressablesSettingsRegistrationTests` (3) + `LevelDatabaseAddressablesTests` (4): 7/7 passed (job `9bcf21d974e4457bae1c608abf29511f`). Clean compile, 0 Console errors.
- AC-2: live window read after domain reload: `Level Database = LevelDatabase`; `Target Level` choices `1. Level 1 … 5. Level 5`; status "Editing Level 1."
- AC-4: `sha256sum -c` of all `Assets/AddressableAssetsData` `.asset` files against the pre-repair snapshot: unchanged.

## Likely origin

`AddressableAssetsData` was created at 22:05 on 2026-09-24 while `EditorBuildSettings.asset` was last written at 18:49 the same day; the registration was most likely lost when assets from an isolated batchmode copy were copied back without `ProjectSettings/EditorBuildSettings.asset`. The new test catches this.
