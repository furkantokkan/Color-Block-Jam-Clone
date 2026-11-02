# Handoff: `voidCells` level schema (for the level-pipeline session)

> **2026-09-25:** this schema change is now tracked, together with typed block/door effects, as `TASK-20260924-215645373-33a6a7a4-level-json-v2-effects` (one `formatVersion` 2 change owned by the level-pipeline session).

Requested by the play-area environment task
(`TASK-20260924-185655569-dea430a7-play-area-environment`). On 2026-09-24 the
user assigned this schema change to the session that currently owns
`Runtime/Levels/**`, `Editor/Levels/**` and the level tests. The environment
task consumes the result and does not edit those files.

## Why

References 02, 04 and 17 show boards with an interior hole and an interior
wall block. The current schema is a plain `width x height` rectangle, so it
cannot express them. The environment builds walls on every floor/void boundary
once the level can mark cells as void.

## JSON (`formatVersion` 2)

```json
"board": { "width": 6, "height": 8, "voidCells": [ { "x": 2, "y": 3 }, { "x": 3, "y": 3 } ] }
```

- `k_formatVersion` becomes `2`. `TryRead` accepts `1` or `2`; anything else
  fails as today.
- `BoardDto.voidCells`: `[JsonProperty("voidCells", Required = Required.Default)] PositionDto[]`.
  Absent or `null` means no void cells (all floor), so every v1 file loads
  unchanged. A v1 document that contains `voidCells` is rejected.
- `TryWrite` always emits `formatVersion` 2 and an explicit `voidCells` array
  (empty when none). `MissingMemberHandling.Error` and
  `TypeNameHandling.None` stay.

## `LevelData` API the environment consumes

- `[SerializeField] private Vector2Int[] m_voidCells = Array.Empty<Vector2Int>();`
- Keep the existing constructor signature for current callers and add an
  overload that takes `Vector2Int[] voidCells`. `Clone` copies the array.
- `public int VoidCellCount { get; }`
- `public Vector2Int GetVoidCellAt(int index)`

When these exist, the environment task changes one call in
`Runtime/Layout/BoardLayout.cs` (`TryCreate(LevelData, ...)`) to pass the
level's void cells instead of none.

## Validation (`LevelData.Validate`, existing `Level {id} ...` message style)

1. Every void cell is inside `0..Width-1 x 0..Height-1`.
2. No void cell is listed twice.
3. At least one floor cell remains.
4. All floor cells are four-connected (load-time flood fill; allocation is
   allowed outside `Playing`).
5. Every block cell is a floor cell.
6. For every edge segment a door covers, the perimeter cell behind it is floor:
   Top `(column, Height-1)`, Bottom `(column, 0)`, Left `(0, row)`,
   Right `(Width-1, row)`.
7. A door `colorId` must be a gate colour. Use
   `ColorBlockJam.Runtime.Layout.BoardLayout.TryParseDoorColor(colorId, out PaletteColor)`
   so the level and the environment share one rule: exact enum name, not
   `Wall` or `Ground`, no numbers or comma lists.

## Tests to add (`LevelDatabaseJsonTests`)

- v1 JSON without `voidCells` loads with zero void cells.
- v2 round-trip keeps `voidCells` in order; `TryWrite` emits `formatVersion` 2
  and an explicit `voidCells` array.
- Each rule above rejects its invalid input with a `Level {id}` message.

## Editor

Painting void cells in the Level Database window is a separate follow-up.
Because `m_voidCells` is a serialized `LevelData` field, drafts round-trip it
through Save and Reload unchanged.

## Reference levels to load (user decision, 2026-09-24)

Update 2026-09-25: on the user's order the environment task wrote levels 1–5
(from `docs/levels/reference-levels.v1.json`) into
`Data/Levels/LevelDatabase.json`; the user limited the game database to those
five levels. Levels 6–10 are not to be loaded unless the user asks. The notes
below stay as the plan for the v2 file.

The user asked the level-pipeline session to load the ten transcribed
reference levels into the canonical `Data/Levels/LevelDatabase.json` once the
loader reads `formatVersion` 2:

- Source: `docs/levels/reference-levels.json` (ten levels, `formatVersion` 2,
  level 9 has `voidCells` `(0,0)` and `(5,0)`, levels 7-9 carry `arrow`
  effects in the PRD record shape `{"type": "arrow", "params": [{"key": "axis", "value": "Horizontal"}]}`).
- Description, captures and transcription conventions: `docs/Reference-Levels.md`.
- If arrow effects are not supported yet, load the levels with empty block
  `effects`; the arrows matter only for the later block task.
- `docs/levels/reference-levels.v1.json` is the current-loader subset (arrows
  dropped, level 9 omitted) and can be removed once v2 loads.
- The environment tests `EnvironmentComposerTests.ReferenceLevels*` read both
  files; replace their small v2 reader with `LevelDatabaseJson` after the
  schema lands.
