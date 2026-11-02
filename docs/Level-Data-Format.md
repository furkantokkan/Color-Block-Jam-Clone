# Level Data Format

Canonical level JSON under `Assets/ColorBlockJam/Data/Json/Levels`. A level
database is a folder there: one manifest and its pack files, each pack a
version 3 level JSON (DEC-196, [Level databases](#level-databases-manifest-and-packs-dec-196)).
`LevelDatabaseJson` reads and writes a pack; `LevelManifestJson` reads and
writes the manifest; `LevelData` holds one parsed level. Newtonsoft Json.NET
with `MissingMemberHandling.Error`, `TypeNameHandling.None` and no `$type`
metadata. Parsing runs only at project-scope load (the manifest), at the Game
scene's pack load on Home and at editor save, never in a `Playing` tick. Level
JSON is the only runtime source of block shapes: the game builds every block
from its `cells` and never loads the piece library (DEC-193).
Tasks: `production/tasks/TASK-20260925-102657710-f7bfcf6a-level-json-v2-board-objects`
(version 2), PROJECT.md DEC-193 (version 3) and DEC-196 (manifest and packs).

## Versions

| `formatVersion` | Reads | Writes |
| --- | --- | --- |
| 1, 2 | No: `Expected level database formatVersion 3 and a levels array.` | Never. |
| 3 | Yes. | Always. |

`Color Block Jam/Data/Migrate Level JSON to v3` (`LevelJsonMigration`, Editor)
converts a version 1 or 2 file once: it drops `shapeId`, re-encodes cells and
params and writes through the version 3 writer. A level whose old `shapeId` named
a footprint the piece library lacks keeps that id as the piece's name in its
`pieces` table. Backups under `Library/ColorBlockJam/LevelBackups` written before
version 3 need the same menu before Restore Previous Save can read them.

## Level databases: manifest and packs (DEC-196)

A database is the folder `Data/Json/Levels/<Name>/` holding `manifest.json`
and `pack-001.json`, `pack-002.json` and so on. The shipped default is `Main`.
The user's goal (2026-09-29) is a format that stays clean for 5,000 levels, so
the game never parses more than one pack of about 100 levels at a time.

```json
{
  "formatVersion": 1,
  "name": "Main",
  "packs": [
    { "file": "pack-001.json", "levels": [ "level-01", "level-02" ] }
  ]
}
```

- Play order is the packs in order, then each pack's IDs in order. A level's
  index in that order is the player's level index (DEC-173).
- Manifest rules: `formatVersion` exactly 1; `name` is 1 to 64 letters, digits,
  hyphens or underscores and equals the folder name; `packs` may be empty (a
  new database); a pack's `file` is a `.json` name without folders, not
  `manifest.json`, listed once (ignoring case); each pack lists at least one ID;
  an ID is listed once in the whole manifest. The reader is as strict as the
  level reader and all or nothing.
- A pack is an unchanged version 3 level JSON. A pack read also checks that its
  levels are exactly the IDs the manifest lists for it, in order, so a pack from
  another version of the database is refused.
- Addresses are never stored. `LevelJsonFiles.GetAddress(name, file)` gives
  `LevelDatabase/<name>/<file>` for the editor's registration and the game's
  load, and every manifest and pack carries the Addressables label `level-json`.
- Pack assignment (`LevelPackLayout`, editor): a saved level keeps its pack; a
  level inserted before or among saved ones joins the pack of the level next to
  it (a pack can then hold more than 100); levels appended after the last saved
  one fill the last pack to `LevelsPerPack = 100`, then open `pack-<n+1>.json`,
  where `n` is the highest pack number of the manifest. A pack left without a
  level is deleted. Packs are never rebalanced. An edit therefore rewrites one
  pack, and an append or a delete rewrites one pack and the manifest.
- The game (`LevelCatalogLoader`): the project scope reads only the manifest
  (`LoadManifestAsync`), so Hub has the level count. The Game scene's
  `GameAttemptLoader` picks the level index, then `LoadPackAsync` loads that
  level's pack by address on Home with the runtime gate and keeps only that
  pack. Restart and Next Level inside the same pack load nothing. A pack that
  cannot be loaded or read skips the attempt with one warning; it never faults
  the Game scene.
- The Level Editor (`LevelDatabaseFiles`) reads the manifest and every pack,
  because unique IDs, template re-stamps, template deletes and piece names work
  across the whole database. A save refuses when any database file changed on
  disk since the read, then copies the whole database into
  `Library/ColorBlockJam/LevelBackups/<manifest GUID>/`, writes the packs whose
  text changes, writes the manifest when its text changes (the commit point),
  deletes every `pack-NNN.json` the manifest no longer lists, and registers the
  files in the Level Database group. Restore Previous Save reads that backup
  folder. The files are not replaced together: a failure part way names what was
  written, the next save removes leftover packs, and Restore brings the backup
  back.
- `Color Block Jam/Data/Split Level JSON into Packs` (`LevelPackSplit`) turns a
  single-file version 3 JSON directly in `Data/Json/Levels` into a database: the
  file moves to `<name>/manifest.json` with its GUID, so the project scope's
  reference stays valid, and its levels go into packs. The shipped
  `LevelDatabase.json` became `Main` this way; its five levels are
  `Main/pack-001.json`, byte for byte the old file.

## Shape (version 3)

```json
{
  "formatVersion": 3,
  "levels": [
    {
      "id": "level-09",
      "name": "Level 9",
      "board": { "width": 6, "height": 6, "voidCells": "0,0 5,0" },
      "timerSeconds": 120,
      "doors": [
        { "id": "d1", "side": "Top", "start": 1, "length": 3, "colorId": "Red" }
      ],
      "blocks": [
        {
          "id": "b1", "colorId": "Red", "origin": "1,1", "cells": "0,0 1,0 0,1 1,1",
          "effects": [ { "type": "arrow", "params": { "axis": "Horizontal" } } ]
        },
        { "id": "b2", "colorId": "Blue", "origin": "3,2", "cells": "0,0 1,0 2,0 1,1 1,2" }
      ],
      "cellObjects": [
        { "id": "o1", "cells": "4,2", "effect": { "type": "barrier", "params": { "startsOpen": "false" } } }
      ],
      "pieces": [ { "key": "00008E2", "name": "Level 9 Piece 1" } ]
    }
  ]
}
```

The writer puts every property on its own line (`Formatting.Indented`); the
example is compacted.

- Cell text (`CellText`): one cell is `"x,y"` in invariant integers; a list is
  cells separated by one space, `"0,0 1,0 0,1"`, kept in authored order. `origin`
  is one cell; block `cells` are relative to it; `voidCells` and cell-object
  `cells` are absolute. The reader refuses anything else (a `+`, decimals, tabs,
  leading, trailing or double spaces).
- Optional members: `board.voidCells`, `doors[].effects`, `blocks[].effects`,
  `cellObjects` and `pieces`. The writer omits them when empty; the reader
  accepts them absent and refuses an explicit `null`.
- Effect record (DEC-MECH-1): `{ "type": "<id>", "params": { "<key>": "<string>" } }`.
  Values must be JSON strings; `params` may be `{}`. The writer lists params in
  the definition's key order, so a write/read/write round trip is identical.
- Cell object: `{ "id", "cells", "effect" }`, exactly one effect.
- `pieces`: the names of this level's level-only pieces, one entry per key (see
  "Level piece names"). The runtime validates and ignores it.
- Kept ids: level `id` (stable identity: Test Level, the level launch), block and
  door `id` (Level Editor handles and messages). There is no `shapeId`: a block's
  type is its footprint key.

Size: the five shipped levels are 6,475 characters in version 3 and 13,501 in
version 2. A realistic ten-block level is about 1.4 KB, so 5,000 levels are about
7 MB. Splitting a database into packs of about 100 levels behind a manifest is
the planned next step of DEC-193; the version 3 document is already one pack.

## Shape identity (DEC-193)

A block type is its footprint: `ShapeKey` (`Runtime/Core/Board/ShapeKey.cs`), the
smallest normalized cell mask among the footprint's 8 rotations and mirrors in a
5 x 5 box, bit `y * 5 + x`, written as 7 uppercase hex digits. Examples: single
`0000001`, domino `0000003`, triple `0000007`, square `0000063`, L `0000027`,
T `0000047`, plus `00008E2`.

- Nothing assigns or stores the key; the editor, the game and every client derive
  it from the cells (`LevelBlockData.Key`). The same shape in any level, level
  database or client is therefore the same piece, with no registry, and a level
  delivered without a client update can use any new shape.
- A piece and its mirror are one type; the Level Editor places the mirror with F.
- Limits: a block's cells are 4-connected, at most 25, inside 5 x 5.
  `LevelData.Validate` refuses anything else, and the Piece Editor's grid is 5 x 5.
- The piece library (`Data/Json/Pieces/BlockShapeLibrary.json`, `{name, cells}`
  records) is editor-only: it holds the templates the Piece Editor saves, one per
  key. Level Editor tiles show a template's name for its key; the level JSON never
  names the library.

This replaces the `shapeId` resolution, the `Custom_` ids, the runtime-learned
shape catalog and its `runtime-shapes.json` cache of DEC-124. An old cache file
on a device is no longer read or written.

## Level piece names

A block whose key the library lacks is a level piece. Its cells live only in the
block. Its human name lives in the level's `pieces` table (`LevelData.PieceNames`,
`LevelPieceName { Key, Name }`), default `<Level Name> Piece n`. Save Level writes
a name only for a key that the library lacks and some block of the level uses,
and drops the rest. Drawing a shape that already exists reuses it: a library key
shows the template, and a key another level of the database names takes that
level's name. Only the Piece Editor's Save as template adds a library record
(DEC-180 kept).

The Level Editor (`LevelPieceNames.Resolve`) takes, for each such key, the
level's own name, else the first name another level of the same database gives
it, else the name the editor session gave the piece, else a new default with the
lowest free n, unique ignoring case among the database's and the library's names.
A key used only in another level database is not looked up. A rename in the
Piece Editor writes the level's entry through the undoable draft. Deleting a
template while keeping its blocks names their key after the template in every
level that uses it (DEC-126), and a template change that leaves a level on the
old footprint names that footprint after the template in the same save.

A block root's Inspector (`BlockPieceEditor`) draws the cells the root shows, with
the block origin outlined, and a summary line with the key and the template name
or "level piece". A scene root is read again every 250 ms, so pool checkouts and
Rocket parts show up while they happen.

## LevelData API

- Void cells: `VoidCellCount`, `GetVoidCellAt(i)`, `IsFloor(cell)`.
- Blocks: `Key` (computed from the cells on every access, allocation-free),
  `Origin`, `CellCount`, `GetCellAt`. Blocks and doors: `EffectCount`,
  `GetEffectAt(i)` (`LevelEffectData`: `Type`, `ParamCount`, `GetParamAt`,
  `FindValue`, typed `Parameters`) and `TryGetEffect<T>(out T)`, for example
  `block.TryGetEffect(out ArrowEffect arrow)`. A door keeps its `effects` list,
  but no gate effect type is registered, so a valid door's list is empty.
- Cell objects: `CellObjectCount`, `GetCellObjectAt(i)` (`LevelCellObjectData`:
  `Id`, `CellCount`, `GetCellAt`, `ContainsCell`, `Effect`, `TryGetEffect<T>`),
  for example `cellObject.TryGetEffect(out BarrierObject barrier)`.
- Piece names: `PieceNameCount`, `GetPieceNameAt(i)`.
- The short constructors of `LevelData` pass an empty piece-name table; the long
  overload takes `voidCells`, `doors`, `blocks`, `cellObjects` and `pieceNames`.
- All fields are `[SerializeField]` and Unity-serializable, so editor drafts,
  Undo and the `JsonUtility` dirty check see them. The typed parameter object and
  the block key are derived and not serialized.
- `LevelData.Validate` is authoring validation. `LevelData.ValidateRuntimeSupport`
  is the project-scope gate below. `LevelData.MaxDoorLength` is 3.

## Effect registry

`Runtime/Levels/LevelEffectRegistry.cs` holds one immutable definition per
catalog type id: target, occupancy role for board objects, key rules, an
explicit converter to an immutable typed parameter object, an optional level
check, and the runtime support flag. No reflection.

Only two block effects exist, Arrow and Ice, and there is no gate (door)
effect: on the user's order of 2026-09-28 every other block effect and every
gate effect was removed from the registry and the code (DEC-177). A level that
names a removed type fails with `... effect {T} is not a known effect type.`
The board objects are unchanged. Value rules:

| Rule | Accepted values |
| --- | --- |
| Enum | Exact spelling from the list. |
| Integer | Canonical invariant digits (`"7"`, not `"07"` or `"+7"`) inside the range. |
| Bool | `true` or `false`. |
| Colour | An exact gate `PaletteColor` name (`BoardLayout.TryParseDoorColor`; not `Wall`/`Ground`). |
| Reference | A non-blank ID of an existing block, door or cell object in the same level. |
| Cell | `"x:y"` with canonical integers, for example `"2:3"`. |
| List | Items separated by `,` inside the one string value, for example `"Red,Blue"` (user decision 2026-09-25). Each item follows the item rule; nothing is trimmed, so `"Red, Blue"` and `"Red,,Blue"` are refused. `""` has 0 items. Each list key has an item count range. |

List and cell encoding for effect values (user decision 2026-09-25): a list is
comma-separated items in one string value; a cell is `"x:y"`; a list of cells is
`"x:y;x:y"`. No current key holds a cell or a list of cells; the only list key
is `flower-crate.leaves`. Because values are refused unless canonical, the writer
keeps each value unchanged and a write/read/write round trip is identical. An ID
inside a list cannot contain a comma.

| Type | Catalog | Target | Keys (`*` required) | Level check |
| --- | --- | --- | --- | --- |
| `arrow` | BLK-01 | block | `axis`* Horizontal/Vertical | |
| `ice` | BLK-03 | block | `clearsToThaw`* 1-20 | |
| `colorful-path` | OBJ-01 | cell object (ColorGate) | `colorId`* colour | no block of another colour on a path cell at start |
| `crate` | OBJ-02 | cell object (Wall) | `clearsToOpen`* 1+ | |
| `moveable-crate` | OBJ-03 | cell object (Movable) | `gatePassesToOpen`* 1+ | |
| `flower-crate` | OBJ-04 | cell object (Movable) | `leaves`* list of 1+ block IDs | no leaf listed twice |
| `barrier` | OBJ-05 | cell object (WallOrFloor) | `startsOpen` bool, default false | four-connected; closed barrier on no block |
| `colorful-crate` | OBJ-06 | cell object (Wall) | `colorId`* colour | |
| `colorful-moving-path` | OBJ-07 | cell object (ColorGate) | `colorId`* colour, `platformIndex`* 0+ | straight ordered track, index inside it |
| `ivy` | OBJ-08 | cell object (Wall) | none | |
| `elevator` | OBJ-09 | cell object (Floor) | none | |

The Barrier is the one board obstacle authored today. The Level Editor's
Obstacles section paints it, and its `startsOpen` param is set in the Effect
Editor window (`LevelBarrierCommands.TrySetStartsOpen`). The earlier
permanent `solid-block` cell object was removed on 2026-09-25 by user
decision: it has no registry entry, parameter type or check, and reading it
fails with `... effect solid-block is not a known effect type.`
(`TASK-20260925-184759859-c962353f-barrier-starts-closed-no-solid-block`).

Stacking (catalog section 3): one effect of each type per entity. No pair of
registered types is exclusive, so a block may carry Arrow and Ice together.

### Assumptions and open questions

Every registered type is authoring-valid. These points are assumptions the
code implements; the first two were confirmed by the user on 2026-09-25, the
rest wait for a user decision:

- Confirmed: no catalog maximum for `crate.clearsToOpen` and
  `moveable-crate.gatePassesToOpen` (1 or more).
- Confirmed: only the Barrier's `startsOpen` has a default (false).
- Open: `leaves` has no upper item count.
- Open: `colorful-path` compares the path colour with the block's `colorId`.
- Open: no block effect places a block inside a crate, moveable crate, flower
  crate or elevator, so those board objects hold no contents today.

Catalog warnings are editor warnings, not validation errors, and none is
checked here, for example OBJ-06 "no block of that colour exists".

Solver static checks are not validation errors and are not checked here, for
example BLK-01 "no door of the block's colour on either axis side".

Every catalog-declared validation error of a registered type has its own
message below.

## Runtime support gate

Each definition has one `private const bool k_<type>RuntimeImplemented` line in
`LevelEffectRegistry.cs`; all are `false` today except `ice` and `arrow`.
Arrow (BLK-01) is runtime-implemented since 2026-09-28 (DEC-158,
`TASK-20260926-230921278-a3ffdc40-arrow-block-gameplay`): project scope loads
levels with Arrow blocks, and the Game scene moves and exits them only along
their axis. The Barrier's line is
`Assets/ColorBlockJam/Runtime/Core/Levels/LevelEffectRegistry.cs` line 20:

```csharp
private const bool k_barrierRuntimeImplemented = false;
```

The gameplay owner flips that one line to `true` when the Barrier rule lands.

Read modes (clarified after verification defect D-001):

- `LevelDatabaseJson.TryRead(json, out levels, out error)` and
  `LevelCatalog.LoadJson(json, out error)` are authoring reads: every catalog
  type, implemented in gameplay or not. Every Level Editor path uses them:
  `LevelDatabaseFiles.TryRead` (Save's pre-read, Reload, Create, Restore
  Previous Save, the Addressables registration and data setup).
- `LevelDatabaseJson.TryRead(json, LevelReadMode.Runtime, ...)` adds the gate.
  Only the game's pack load, `LevelCatalogLoader.LoadPackAsync`, uses it,
  through `LevelCatalog.LoadPackJson(pack, json, LevelReadMode.Runtime, out error)`,
  and the content update publisher checks every pack with it first (DEC-162).
  The Game scene therefore skips a level whose pack uses an unimplemented effect
  or board object and names it; the same level stays editable (DEC-196: startup
  reads only the manifest, so such a pack no longer blocks the whole game).
- `LevelDatabaseJson.TryWrite` validates with authoring rules.
- The read is all or nothing per file (user decision 2026-09-29): one invalid
  level fails the file, and editor validation keeps invalid levels out of it.

Play Mode interplay (DEC-192): Play Mode starts without a domain reload
(`ProjectSettings/EditorSettings.asset`, Enter Play Mode Options), but the
Level Editor and the game no longer share levels. The project scope installer
only names the active database's manifest (DEC-194). The project scope reads
that manifest into its own `LevelCatalog`, the Game scene loads a pack into it
with the runtime read (DEC-196), and each Level Editor window keeps its own
catalog. A failed manifest load leaves the game's catalog unloaded and faults
the project scope's ready task, which Hub and `GameAttemptLoader` wait for; the
editor's levels are untouched. A Level Editor Save during Play Mode reloads
only the editor's catalog, so the game keeps the pack it loaded through the
runtime gate.

## Validation messages

Stable texts; the Level Editor reuses them for refusals. This is every message
that `LevelData.Validate`, `LevelEffectValidator`, `LevelDatabaseJson.TryRead`
and `LevelDatabaseJson.TryWrite` return. "(unchanged)" marks texts that existed
before version 2. `{L}` is the level ID, `{i}` a list index, `{entity}` is
`block {id}`, `door {id}` or `cell object {id}`, `{T}` the type and `{K}` the
key. `LevelData.Validate` checks in this order: level fields, void cells,
doors, blocks, cell objects, piece names, effects (blocks, then doors, then cell
objects).

Level fields (unchanged):

- `Level ID and name are required.`
- `Level {L} has a board size outside 3-8 by 3-10.`
- `Level {L} has a timer outside 10-300 seconds.`
- `Level {L} has no door list.`
- `Level {L} needs at least one block.`

Void cells:

- `Level {L} void cell ({x}, {y}) is outside the board.`
- `Level {L} lists void cell ({x}, {y}) twice.`
- `Level {L} has no floor cell.`
- `Level {L} floor cells are not four-connected.`

Doors:

- `Level {L} door {i} needs an ID and color ID.` (unchanged)
- `Level {L} door {id} has an invalid side.` (unchanged)
- `Level {L} door {id} spans {n} cells; gates span 1 to 3.`
- `Level {L} door {id} is outside its board edge.` (unchanged)
- `Level {L} door {id} uses '{colorId}', which is not a gate colour.`
- `Level {L} door {id} opens onto void cell ({x}, {y}).`
- `Level {L} has duplicate door ID {id}.` (unchanged)
- `Level {L} doors {a} and {b} overlap.` (unchanged)

Blocks:

- `Level {L} block {i} needs an ID and color ID.`
- `Level {L} block {id} has no cells.` (unchanged)
- `Level {L} has duplicate block ID {id}.` (unchanged)
- `Level {L} block {id} has a cell outside the board.` (unchanged)
- `Level {L} block {id} has a cell on void cell ({x}, {y}).`
- `Level {L} block {id} lists a cell twice.` (unchanged)
- `Level {L} blocks {a} and {b} overlap.` (unchanged)
- `Level {L} block {id} has disconnected cells.` (unchanged)
- `Level {L} block {id}: it has {n} cells; a piece holds at most 25.` and
  `Level {L} block {id}: it spans {w} x {h} cells; a piece fits inside 5 x 5.`
  (the shape key limits)

Cell objects:

- `Level {L} cell object {index} needs an ID.`
- `Level {L} has duplicate cell object ID {id}.`
- `Level {L} cell object {id} has no cells.`
- `Level {L} cell object {id} has cell ({x}, {y}) outside the board.`
- `Level {L} cell object {id} has cell ({x}, {y}) on a void cell.`
- `Level {L} cell object {id} lists cell ({x}, {y}) twice.`
- `Level {L} cell objects {a} and {b} share cell ({x}, {y}).`

Piece names:

- `Level {L} piece name {i} has no valid piece key.`
- `Level {L} piece {key} needs a name of 1 to 128 characters.`
- `Level {L} names piece {key} more than once.`

Effects:

- `Level {L} {entity} has an empty effect.`
- `Level {L} {entity} effect {T} is not a known effect type.`
- `Level {L} {entity} effect {T} cannot be used on a {block|door|cell object}.`
- `Level {L} {entity} lists effect {T} twice.`
- `Level {L} {entity} effect {T} has an empty key.` (a blank key, `""` or
  whitespace, from JSON or the API; through the API also a `null` key or
  parameter)
- `Level {L} {entity} effect {T} has unknown key {K}.`
- `Level {L} {entity} effect {T} lists key {K} twice.` (through the API; in JSON a
  repeated key is already a parse error)
- `Level {L} {entity} effect {T} is missing required key {K}.`
- `Level {L} {entity} effect {T} key {K} has value '{v}'; expected {rule}.`
  where `{rule}` is `one of A, B`, `an integer from {min} to {max}`,
  `an integer of {min} or more`, `true or false`, `a gate colour name`,
  `a block ID`, `a door ID`, `a cell object ID` (a blank reference) or
  `a cell written x:y`. An empty value `""` (from JSON or the API) and a `null`
  value (API only; the reader refuses a JSON `null` first) print as `''`; for a list key
  `{rule}` is `{n} to {m} items separated by ',', each {item rule}` or
  `{n} or more items separated by ',', each {item rule}`.
- List keys: `Level {L} {entity} effect {T} key {K} has {c} items in '{v}'; expected {n} to {m} items.`
  or `... expected {n} or more items.` (`1 item` when `{c}` is 1), and
  `Level {L} {entity} effect {T} key {K} item '{i}' in '{v}' is not {item rule}.`
  with the item rules above.
- `Level {L} {entity} effect {T} key {K} names missing {block|door|cell object} {v}.`
  (for a list key, `{v}` is the first missing item)
- Level checks: `cell ({x}, {y}) is under block {id} of another colour.`,
  `needs a straight track of neighbouring cells in order.`,
  `cells are not four-connected.`, `starts closed on block {id} at ({x}, {y}).` and
  `key leaves lists block {b} twice.`,
  each after `Level {L} {entity} effect {T} `. The `colorful-moving-path`
  (`platformIndex`) level check reuses the value message with
  `expected an integer from 0 to {n}.`
- Runtime gate: `Level {L} {entity} effect {T} is not implemented in gameplay yet.`

Reader (`TryRead`, record shape before `Validate`):

- `The JSON file is empty.` (unchanged)
- `The JSON must be a database object without type metadata.` (unchanged)
- `Expected level database formatVersion 3 and a levels array.` (any version
  but the integer 3, checked before any other member)
- `Invalid level JSON: {Newtonsoft message}` (unchanged): malformed JSON, a
  duplicate or unknown property (a version 2 `shapeId` included), a missing
  required property, an explicit `null` for an optional list, or a wrong token
  type. It names the JSON path, not the level.
- `Level {i} is missing board, doors or blocks.` (a `null` level entry)
- `Level {L} void cells: {problem}.`
- `Level {L} door {i} is empty or has an invalid side.` (a `null` door entry, or
  a side other than `Top`, `Bottom`, `Left`, `Right`)
- `Level {L} block {i} is empty.` (a `null` block entry)
- `Level {L} block {id} origin: {problem}.` and `Level {L} block {id} cells: {problem}.`
- `Level {L} cell object {i} has missing cells or effect.` (a `null` entry)
- `Level {L} cell object {id} cells: {problem}.`
- `{problem}` is one of `'{text}' is not an x,y cell`,
  `'{text}' has an empty cell; separate cells with one space` or
  `the text is missing`.
- `Level {L} {entity} has an effect without type or params.`
- `Level {L} {entity} effect {T} parameter {K} needs a JSON string value.` (a
  number, bool, `null`, object or array value; an empty string `""` reaches
  `Validate`, which gives the key's value message)
- `Level {L} piece {i} has key '{key}', which is not the 7-digit key of a piece.`
- `Level ID {id} appears more than once.` (unchanged; read and write)

Writer (`TryWrite`): `Level data is missing.` and `Level {i} is empty.`
(unchanged), then every `Validate` message above.

Manifest and packs (DEC-196, `LevelManifestJson`, `LevelManifest`,
`LevelDatabaseFiles`):

- `The manifest file is empty.`
- `The manifest must be a JSON object without type metadata.`
- `Expected level manifest formatVersion 1 with a name and a packs array.`
- `Invalid level manifest JSON: {Newtonsoft message}`
- `Pack {i} of the manifest is empty.` (a `null` pack entry)
- `The database name '{name}' must be 1 to 64 letters, digits, hyphens or underscores.`
- `Pack {i} has the file name '{file}', which is not a .json file name without folders.`
- `Pack file {file} is listed twice.`, `Pack {file} lists no level.`,
  `Pack {file} lists a level without an ID.`,
  `Level ID {id} is listed twice in the manifest.`
- Pack against manifest: `Pack {file} holds {n} level(s), but the manifest lists {m}.`
  and `Pack {file} holds level {id} where the manifest lists {expected}.`
- Editor reads: `The manifest in folder {folder} names the database {name}; a
  database's folder and name must match.`, `{what} is missing: {path}` and a
  pack's reader message prefixed with its file name (`pack-002.json: ...`).
- Editor saves: `Level JSON changed outside this editor. Reload before saving.`,
  `A separate backup folder is required for level saves.`,
  `Level ID {id} appears more than once.` and
  `There is no previous save to restore.`

## IL2CPP

`Runtime/Core/Levels/link.xml` preserves the manifest DTOs (`ManifestDto`,
`PackDto`, DEC-196), the version 3 DTOs (including `CellObjectDto`, `EffectDto`
and `PieceDto`), the serialized records and every typed parameter class. The
shape cache and piece library DTOs are gone from the player (DEC-193). Device
smoke check pending on the next Android IL2CPP build.
