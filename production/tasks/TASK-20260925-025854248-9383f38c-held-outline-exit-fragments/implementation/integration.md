# Held outline: shared-workspace integration (2026-09-28)

Integrated at the user's order ("görünüş uygun sadece resimdeki gibi çakışma hatalarını düzelt ardından implemente geç")
while TASK-20260927-004448215-54655902-booster-gameplay-flow still owns `Gameplay/Board/*.cs`, `GameSceneInstaller.cs`
and `Game.unity` in its isolated copy. Same pattern as the gate-motion change recorded in that task's handoff: the
Booster copy-back must three-way merge these hunks, not overwrite them.

## Changed shared files

| File | Base (before) | Now | Change |
| --- | --- | --- | --- |
| `Assets/ColorBlockJam/Gameplay/Board/BoardPresenter.cs` | `34b331b08dc8a130bdf059262eb602c2d7f5a5f9e440cf6fd7ad9ecaa0593051` | `b0330154fe41deef7daf189fc91fd256c0bf0de9b6c6b79dfbcf5aab00004178` | `using ...Board.Outline`; field `m_outline`; the 4-argument constructor loses `[Inject]` and a new 5-argument `[Inject]` constructor takes `HeldOutlineView`; `TryBuild` calls `m_outline?.Prepare(GetLargestFootprint(needs))`; `SetHeld` shows/hides it; `SnapHeld`, `ReturnPieces`, `StartExit` (held root) hide it; `Dispose` disposes it; new helper `GetLargestFootprint`. |
| `Assets/ColorBlockJam/Gameplay/Composition/GameSceneInstaller.cs` | `d40f1d3a9de8d3766498f806057aa389718802ff5bcb58ff6a1dc4018e9646ce` | `b750ad0c1f1f496159ce4dda31f8772af4e62d97d083d97742a5faed9013b0ed` | `using ...Board.Outline`; `[SerializeField] Material m_outlineMaterial` after `m_iceMaterial`; a missing-material check; `container.BindInstance(new HeldOutlineView(m_outlineMaterial, m_environment.BoardRoot))` after the hook source. |
| `Assets/ColorBlockJam/Scenes/Game.unity` | mtime 2026-09-27 03:30 | `c55a492d76fa4922283cb03a70533357e623a2144d0881ce30bd5696e5840211` | One line on the GameSceneInstaller: `m_outlineMaterial: {fileID: 2100000, guid: cd5738d6bd9318f42be66da0a3062da9, type: 2}` after `m_iceMaterial`. Written by Unity in the isolated copy; the file was otherwise identical. |
| `PROJECT.md` | — | `ddda490a…` | DEC-145 row appended after DEC-144. |
| `docs/Gameplay-Drag-And-Gate.md` | — | `58bee2e7…` | "Held outline" section and one lifecycle-table row. |

The exact C# hunks are in `integration.patch`. If Booster's `Game.unity` replaces the shared scene, keep the
`m_outlineMaterial` line: without it `GameSceneInstaller` throws "the held-block outline material is missing".

## New files (this task owns them)

- `Assets/ColorBlockJam/Gameplay/Board/Outline/` — `FootprintOutline.cs`, `BlockOutlineMesh.cs`, `HeldOutlineView.cs`, `HeldOutlineSettings.cs` (+ folder and file metas)
- `Assets/ColorBlockJam/Art/Shaders/Gameplay/BlockOutline.shader` (+ metas)
- `Assets/ColorBlockJam/Art/Materials/Gameplay/MAT_BlockOutline.mat` (guid `cd5738d6bd9318f42be66da0a3062da9`, + metas)
- `Assets/ColorBlockJam/Tests/Gameplay/EditMode/FootprintOutlineTests.cs`, `Assets/ColorBlockJam/Tests/Gameplay/PlayMode/HeldOutlineTests.cs` (+ metas)

## Verification at these bytes

An isolated copy mirrored from the shared workspace after integration (`diff -rq` clean apart from copy-only helpers):
Gameplay EditMode 325/325, Gameplay PlayMode 65/65 (includes 9 HeldOutlineTests and every existing presenter,
drag, Ice, gate and fragment test). Roslyn compile check on the live project: COMPILE_OK for Gameplay and all
dependents. Shader compiles for Vulkan, GLES3 and Metal with no messages.
