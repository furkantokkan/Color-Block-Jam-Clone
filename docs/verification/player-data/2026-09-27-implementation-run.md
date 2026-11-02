---
title: "Player data and currencies: implementation run"
created: 2026-09-27
modified: 2026-09-27
type: verification
status: active
tags: [player-data, currencies, save, tests]
---

# Player data and currencies: implementation run (2026-09-27)

Task: `TASK-20260926-235915696-6b7f82f4-player-data-currencies` (implementer run; the
independent verifier still owns the verdict).

## Compile

- Out-of-Editor pre-check with Unity 2022.3.62f2 Roslyn from Bee response files:
  `ColorBlockJam.Runtime` (with the new `Runtime/Currencies` and changed
  `BoosterDefinition.cs`), `ColorBlockJam.Player` and `ColorBlockJam.Player.EditModeTests`:
  0 errors, 0 project warnings each.
- Live Editor after `refresh_unity` (force, compile requested): both new assemblies
  loaded; no `error CS` from any file this task owns. One unrelated compile error was
  present at that moment in `Tests/Gameplay/PlayMode/GameSceneIceTests.cs` (edited by
  another session at 03:31; `BoardCamera` not found); a later check showed
  `EditorUtility.scriptCompilationFailed == false`.

## EditMode: `ColorBlockJam.Player.EditModeTests`

| Run | Job id | Total | Passed | Failed |
| --- | --- | --- | --- | --- |
| 1 | `785492215505428fb304bd6cf95f1e93` | 58 | 58 | 0 |
| 2 (repeat for a clean summary) | `414a9a6595a74618b8893a8016b56572` | 58 | 58 | 0 |

Both jobs reported `succeeded` with `completed 58 / total 58` and an empty
`failures_so_far`; the MCP plugin reconnected at the end of each run, so the detailed
per-test result payload was not returned.

Fixtures: `CurrencyCatalogTests` (14), `PlayerSnapshotJsonTests` (21),
`PlayerSaveServiceTests` (10), `FilePlayerSnapshotStoreTests` (13).

## PlayMode: `ColorBlockJam.Tests.PlayMode.SceneFlowSmokeTests`

Not run by the implementer. The first attempt was refused with `tests_running`
(another session's run); afterwards the shared Editor stayed in Play Mode with only the
`LevelEditor` scene loaded for the rest of the 15-minute wait window, so the run was
not started over another session's Play Mode. AC-10 stays unproven.

## Serialized asset readback (through the Editor)

- `ProjectContext.prefab` `m_installers`: `0: ProjectScopeInstaller`, `1: PlayerDataInstaller`.
- `ProjectScopeInstaller` fields unchanged: scene flow `Data/SceneFlow/SceneFlowProfile.asset`,
  booster catalog `Data/Boosters/BoosterCatalog.asset`, level database
  `Data/Levels/LevelDatabase.asset`, `m_shapeLibrary` null (the save wrote the previously
  unserialized null field as `m_shapeLibrary: {fileID: 0}`; value unchanged).
- `PlayerDataInstaller.m_currencyCatalog`: `Data/Currencies/CurrencyCatalog.asset`.
- `CurrencyCatalog`: `life` (Lives, 5/5, CountOnIcon, `Full`, `Heart.png`), then `coin`
  (Coins, 0/999999999, Amount, empty label, `Coin_Single.png`); `Validate` true.
- Five booster assets: `m_purchasePrice: 10`, `m_purchaseAmount: 1`,
  `m_purchaseCurrency` = `Coin.asset` (guid `5fd1561e40c5a6444af5a47c6a11914c`);
  `BoosterCatalog.Validate` true.

## Not run

- PlayMode `SceneFlowSmokeTests` (see above).
- Device checks: IL2CPP smoke build with the managed crypto types, fault injection and
  kill/relaunch durability on the Android phone (AC-6.12 device half).
