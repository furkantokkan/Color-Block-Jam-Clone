---
title: "Color Block Jam Case: PRD Appendix, Mechanics Catalog"
created: 2026-09-24
modified: 2026-09-24
type: prd
status: final v7
tags: [rollic, color-block-jam, game-developer-case, prd, mechanics, boosters, mechanics]
---
# Color Block Jam Case: PRD Appendix, Mechanics Catalog

## 1. Purpose and How to Read

This appendix is part of the single PRD scope. It specifies boosters, block effects, gate effects and board objects with rules, data, editor support, visuals, acceptance criteria and verification. The main PRD defines the completed case vertical slice. The user chooses each next implementation task and the order. A selected mechanic must satisfy its entry and shared requirements; an unselected entry is a specification without a claim of implementation.

The build registers only mechanics that have been implemented and verified. A level with an unknown or unregistered type fails with a clear error (DEC-MECH-30). Dependencies are checked when a mechanic is selected; the catalog does not prescribe a fixed sequence.

**ID scheme**

| Prefix | Range | Source order |
| --- | --- | --- |
| BST | BST-1 Time Freeze, BST-2 Hammer, BST-3 Rocket, BST-4 Vacuum, BST-5 Extra Time | Four in-level controls plus one pre-level-only effect; Rocket is shared |
| BLK | BLK-01 to BLK-18 | Research note B1 Arrow to B18 Magnet Block |
| GTE | GTE-01 to GTE-11 | Research note G1 Door to G11 Laser Door |
| OBJ | OBJ-01 to OBJ-09 | Research note I1 Colorful Path to I9 Elevator |
| AC | AC-BST-n.m, AC-BLK-nn.m, AC-GTE-nn.m, AC-OBJ-nn.m | One per acceptance criterion |
| DEC | DEC-BST-n, DEC-MECH-n | Section 8 |

**Sources.** Mechanic rules are based on the official help pages: [obstacles][rollic-obs], [boosters][rollic-bst], [how to play][rollic-play] and [tips for hard levels][rollic-tips]. The block, gate and board grouping organizes their obstacle list. Supplied assets are under `Assets/ColorBlockJam/Art`.

[rollic-obs]: https://rollic.helpshift.com/hc/en/24-color-block-jam/faq/1234-obstacles-in-color-block-jam/
[rollic-bst]: https://rollic.helpshift.com/hc/en/24-color-block-jam/faq/1232-what-is-a-booster-what-are-they-for/
[rollic-play]: https://rollic.helpshift.com/hc/en/24-color-block-jam/faq/1222-how-do-i-play-color-block-jam/
[rollic-tips]: https://rollic.helpshift.com/hc/en/24-color-block-jam/faq/1223-tips-for-hard-levels/

### 1.1 Shared Model Every Entry Uses

The shared event model defines `BlockExited` for Ice: each other block's door exit advances its counter, while a booster removal does not. Other selected mechanics use the event named in their own rule. Both Rocket entry points share the same booster behavior.

- **Registration, not core edits.** A mechanic is one registry definition (type id, target, parameter schema, validation, rule factory, solver support), one plain C# rule class, one pooled view entry, one editor badge, and its tests. Board, movement, exit, session, timer, tick owner, JSON envelope, and composers do not change. An EditMode completeness test fails when a definition has no view or badge entry.
- **Hook points.** The core asks registered rules fixed questions: may this block start a drag, on which axes may it move, may it enter this cell, may it exit through this door, does it count for the win. Rules also receive domain events. Hook arrays are built once at attempt build and iterated with `for` loops: no LINQ, no closures, no boxing per frame.
- **Domain events** (DEC-MECH-2, DEC-MECH-3). Raised in the command phase of the tick, only while `Playing`, as readonly structs:
  - `BlockCleared(blockId, cause)`: one per block that leaves the board. Cause is `Exit`, `Peel` (one layer), `MemberExit` (one member of a group), or `Booster`.
  - `GatePassed(blockId, doorId)`: one per pass through a door (`Exit`, `Peel`, `MemberExit`). Never raised by a booster.
  - `MoveCommitted(blockId)`: a drag release that changed the board.
  - `OccupancyChanged`, `FootprintChanged(blockId)`, `ColorChanged(blockId)`.
- **Which event a mechanic listens to follows Rollic's wording.** "Removed" or "clear" means `BlockCleared` (booster removals count). "Passes through a gate" means `GatePassed` (booster removals do not count).
- **JSON** (DEC-MECH-1). Keep the main PRD's `formatVersion: 1` envelope and each effect's `{ "type": "<id>", "params": [ { "key": "<name>", "value": "<string>" } ] }` representation on a block, door, or cell. Newtonsoft Json.NET reads and writes this representation; an explicit converter registered for each shipped type parses its string pairs into typed domain parameters at load. `TypeNameHandling.None` is mandatory; unknown type ids, unknown or duplicate parameter keys, missing required fields, and invalid typed values fail validation. Preserve level DTOs and converter targets in `link.xml` for IL2CPP. Parsing runs on level load/save, never in the `Playing` tick. Entries below list type ids and parameter keys. Colors are palette ids; references are entity ids (`b4`, `d2`, `o1`).
- **Runtime state** lives in the attempt. Restart and Next Level rebuild it from level data. No static state. Counters, flags, and ledgers belong to the session.
- **Views** come from prewarmed Onity pools owned by a disposable scene-scoped owner; `IPoolHooks` reset pieces on return. Original modular FBX tile prefabs remain shared; each library shape additionally has a derived prefab with a BlockPiece root. BlockPieceFactory uses Onity PooledFactory/PrefabComponentPool for bounded checkout and return. Canonical JSON identities/footprints are cached through the reference-only SO and copied into attempt state, and runtime type changes retain original pool ownership. One shared block material with `MaterialPropertyBlock` color, one shared ice material, and atlas sprites on 3D quads provide the board visuals. Every screen-space counter, digit, glyph, and badge is a UI Toolkit element projected from its board target and updated when its value or layout changes; there are no TextMeshPro components. A font asset may supply glyphs to UI Toolkit. Gameplay tweens target gameplay objects so Suspend and Teardown reach them. No view has `Update`.
- **Solver support.** Each definition declares `Static`, `Simulated`, or `Unsupported`. A level with an `Unsupported` mechanic gets "Not verified: contains <mechanic>", never "Solvable" (DEC-MECH-12). **Complexity:** S about 0.5 day, M about 1 day, L about 2 days, XL 3 days or more, including tests.

## 2. Boosters

**Terminology and scope.** Booster is one gameplay category. Four controls appear during a level: Time Freeze, Rocket, Hammer and Vacuum. Two choices appear before a level: Extra Time and Rocket. Rocket is one effect with two entry points and shared stock. These are five effects across six placements. The [main PRD](Color-Block-Jam-PRD.md#710-functional-boosters-req-o3) owns their acceptance criteria. The [user screenshots](Color-Block-Jam-Visual-References.md) guide presentation; the in-game coin purchase works while Shop stays locked.

| ID | Effect | Placement | Target and result | Visual reference |
| --- | --- | --- | --- | --- |
| BST-1 | Time Freeze | In-level | Hold the main timer and run a separate 10-to-0 `Playing`-time countdown; then resume the unchanged main timer. Shader Graph frost follows the separate countdown. | [04](Color-Block-Jam-Visual-References.md#ref-04) |
| BST-2 | Hammer | In-level | Remove one semantic layer or block piece: a normal block, the exposed layer of a layered block, or an active Ice shell. | [02](Color-Block-Jam-Visual-References.md#ref-02), [16](Color-Block-Jam-Visual-References.md#ref-16) |
| BST-3 | Rocket | Pre-level and in-level | Remove one cell while leaving at least one connected cell. In-level: player taps the cell. Pre-level: choose a valid starting cell randomly. | [01](Color-Block-Jam-Visual-References.md#ref-01), [09–12](Color-Block-Jam-Visual-References.md#ref-09) |
| BST-4 | Vacuum | In-level | Tap a block to select its color and remove all visible blocks of that color. | [03](Color-Block-Jam-Visual-References.md#ref-03), [13](Color-Block-Jam-Visual-References.md#ref-13), [18](Color-Block-Jam-Visual-References.md#ref-18) |
| BST-5 | Extra Time | Pre-level only | Add configured seconds to the attempt timer before its countdown begins. This is separate from Time Freeze. | [09–11](Color-Block-Jam-Visual-References.md#ref-09) |

### 2.1 One system, two entry points

- One booster category owns rules, available charges, selection, validation, application, and presentation intents. There is no separate `Skill` type or second Rocket effect. The use context is pre-level or in-level; it changes targeting and timing, not the effect definition.
- Seed one charge for each available booster definition once; thereafter stock persists across pre-level and in-level contexts, Restart, and Next Level. A pre-level Rocket and an in-level Rocket spend from the same Rocket stock. Selection, deselection, cancellation, an invalid target, and a refused use spend nothing. Valid use spends one charge through the [main PRD's saving transaction](Color-Block-Jam-PRD.md#76-coin-inventory-and-persistence). During play, validate the intent, stop accepting the current tick, and suspend under the existing `Paused` saving reason without a pause popup; clocks, input, and tweens hold while an atomic JSON snapshot is saved outside the tick. A durable debit resumes and applies once before the next timer advance; failure has no debit or effect. Backgrounding latches pause rather than auto-resuming; Home or Restart after pause discards a committed pending effect without refund. Before play, board preparation and valid Rocket target selection happen on `Home`, then selected charges are saved before entering `Playing`. Editor test play keeps stock in memory. The [purchase popup](Color-Block-Jam-Visual-References.md#ref-01) buys one charge for 10 coins by the current tuning default for all boosters. A purchase atomically debits coins and grants stock once, with no mutation on insufficient funds or persistence failure; double taps cannot duplicate it. Buying does not auto-select or auto-cast. An in-level modal keeps the attempt `Paused` until close; a pre-level purchase remains on `Home`. Shop remains locked and requires no IAP or backend.
- In the level popup, unselected pre-level choices show a blue tile and remaining count. A selected choice shows a green tile and check badge; tapping it again deselects it. See [09](Color-Block-Jam-Visual-References.md#ref-09) and [10](Color-Block-Jam-Visual-References.md#ref-10). The two choices may both be selected when stock is available. Selection alone does not spend.
- On Play, prepare the board and pools and resolve any eligible random Rocket cell while still on `Home`. Save selected charges only after validation; an invalid Rocket target keeps its charge. Enter `Playing` only after the save succeeds, then apply committed Extra Time and Rocket commands once before normal board input and countdown. Opening feedback holds dragging, other booster use, and the countdown until it finishes. No new session enum state is introduced. Pause preserves the opening presentation; Resume never reapplies committed effects. Home, Restart, and teardown cancel its session-owned work under PROJECT.md. The selected icons over the board and changed board are shown in [11](Color-Block-Jam-Visual-References.md#ref-11) and [12](Color-Block-Jam-Visual-References.md#ref-12).
- In-level Time Freeze is cast on button tap. Hammer, Rocket, and Vacuum enter target mode; tapping the same button, empty board, Android Back, Pause, or backgrounding cancels without spending. Targeting blocks drag input but does not pause the timer. Ordinary board input uses the main PRD's [nearest-block press rule](Color-Block-Jam-Visual-References.md#ref-17): a press within the board may select the nearest block once and preserves the grab offset. The held block shows the outline in [14](Color-Block-Jam-Visual-References.md#ref-14) until release or cancel, whether movable or frozen. This selection rule does not add an automatic destructive-booster target. When an eligible block exits through a door whose color and span fit, [15](Color-Block-Jam-Visual-References.md#ref-15) shows it separating into small cube pieces as presentation only. An invalid exit never starts that effect. The HUD has four buttons in Time Freeze, Rocket, Hammer, Vacuum order, with counts and selected or empty feedback.
- After a valid intent is durably debited, logic commits once in the resumed session command phase, then pooled views present it. A removal may complete the level through the one terminal latch. On `Won`, input, clocks, and domain simulation stop immediately, while a finite visual-only Finish phase of at most 0.8 foreground seconds may complete final Vacuum, Hammer, or exit feedback before full teardown and the win popup. Backgrounding, Home, or timeout forces cleanup. Animation callbacks never change gameplay, spending, or rewards. On Pause, targeting clears without charge spend; on teardown, the opening sequence, active effects, tweens, and views stop. Booster commands outside `Playing` are rejected.

### 2.2 Effect rules and supplied art

- **Time Freeze (BST-1):** the in-level control uses `Assets/ColorBlockJam/Art/UI/Supplied/Icons/Boosters/ic_clock_freeze.png` and needs no board target. On application, the main timer holds its displayed value while a separate 10-to-0 countdown advances only during `Playing`. When that countdown expires, the main timer resumes from its held value; no seconds are added to it. The Shader Graph frost rim and UI Toolkit duration indicator in [04](Color-Block-Jam-Visual-References.md#ref-04) track the separate countdown. Pause preserves both remaining values; activation while already active is refused without spend. This effect is distinct from the level's Ice blocker.
- **Hammer (BST-2):** `ic_hammer.png` enters target mode and shows the prompt in [02](Color-Block-Jam-Visual-References.md#ref-02). Its cast uses the 3D mallet presentation in [16](Color-Block-Jam-Visual-References.md#ref-16). One valid hit removes one semantic layer or block piece: it removes an ordinary single-layer block entirely; it removes only the exposed layer of a layered block, leaving lower layers and the footprint; or it removes an active Ice obstacle shell, sets its counter to zero, and leaves the block. These outcomes are exclusive per hit. A selected Layer mechanic follows its own entry. Hammer has no generic one-cell geometric cut or connected-component splitting rule. Door and object interactions apply when the user selects and verifies them.
- **Rocket (BST-3):** `ic_rocket_purple.png` is the supplied icon for both placements. The effect removes exactly one grid cell, unlike Hammer. A cell whose removal would split or empty the remaining block is invalid, including every cell of a 1x1 block; refusal spends nothing. Otherwise the block keeps its identity, color, and effects, and its view is recomposed from the smaller connected footprint. The pre-level use chooses randomly among only valid cells on visible starting blocks. The in-level use applies to the valid cell tapped by the player. Both use the same rule, persistent stock, and footprint-change handling. The red rocket shown in [09–11](Color-Block-Jam-Visual-References.md#ref-09) is a visual reference; the supplied purple icon is the case fallback.
- **Vacuum (BST-4):** The `ic_vacuum.png` HUD icon enters target mode and shows the prompt in [03](Color-Block-Jam-Visual-References.md#ref-03). The tapped block chooses the color; every visible block of that color is removed. During the cast in [13](Color-Block-Jam-Visual-References.md#ref-13), the chosen block has a bright white silhouette outline, the approved camera-facing `VacuumEffect.prefab` appears near the board's lower edge with reference 18's upright hose and fixed upper intake, and same-color block views shrink as they move into its nozzle. These pooled, session-owned PrimeTween visuals follow the domain removal commit; they add no physics or separate gameplay simulation. Pause suspends them and teardown returns them to Onity pools. Concealed or contained catalog blocks remain outside its target set. Exact HUD/cast assets for all boosters are recorded in [Art Asset to PRD Mapping](Art-Asset-PRD-Mapping.md); imported standalone views do not complete their domain/session integration.
- **Extra Time (BST-5):** the pre-level hourglass is separate from the in-level Time Freeze. It increases the initial timer by a configured amount once the attempt opens. The screenshot alone does not establish that amount. The supplied `ic_clock.png` plus an Extra Time text label is the case's fallback presentation.
- The supplied HUD background is `Assets/ColorBlockJam/Art/UI/Supplied/Buttons/btn_booster_normal.png`. The four HUD icons above live in `Assets/ColorBlockJam/Art/UI/Supplied/Icons/Boosters/`; that folder contains no hourglass or red rocket icon. Both Rocket placements use the supplied `ic_rocket_purple.png`. The [popup](Color-Block-Jam-Visual-References.md#ref-09) and [HUD](Color-Block-Jam-Visual-References.md#ref-04) references describe states, while actual import and atlas settings follow AGENTS.md.

### 2.3 Acceptance and verification ownership

The main PRD sections [7.6](Color-Block-Jam-PRD.md#76-coin-inventory-and-persistence) and [7.10](Color-Block-Jam-PRD.md#710-functional-boosters-req-o3) own persistence and booster acceptance IDs. This catalog adds shared-context cases: both pre-level choices spend nothing until validated opening preparation commits; opening applies each committed effect once; Rocket selects a valid cell leaving a connected nonempty block; both entry points share stock; Hammer removes one semantic layer, block piece or Ice obstacle; Pause preserves opening and both Time Freeze clocks; teardown cancels presentation. Verify rule outcomes in EditMode and presentation on a portrait device. Buy debits coins and grants a charge atomically, including failure and double-tap cases.

The official [Rollic booster guide][rollic-bst] and the [mechanics research guide](Color-Block-Jam-Mechanics-Guide.md#13-four-boosters) remain historical reference material. Its whole-block Rocket and other full-game rules must not override the case-specific one-cell rule.

## 3. Block Effects

Mechanic rules follow the [obstacle reference][rollic-obs]. Target: Block. The Listens to column names the event that advances an effect. Hammer interactions for a selected mechanic must follow its one-exposed-piece-or-obstacle rule.

| ID | Rollic name | Type id | Listens to | Solver | Size |
| --- | --- | --- | --- | --- | --- |
| BLK-01 | Arrow | `arrow` | none | Static | S |
| BLK-02 | Layer | `layer` | door contact | Simulated | M |
| BLK-03 | Ice | `ice` | `BlockExited` of another block | Static | S |
| BLK-04 | Star | `star` (+ door `star-door`) | none | Static | M |
| BLK-05 | Combined | `combined` | door contact | Simulated | L |
| BLK-06 | Chained Blocks | `chained`, companion `key` | BlockCleared of keys | Simulated | M |
| BLK-07 | Bomb | `bomb` | tick | Simulated, time as warning | M |
| BLK-08 | Ropes | `ropes`, companion `scissors` | BlockCleared of scissors | Simulated | M |
| BLK-09 | Moving Blocks | `moving-obstacle` | none | Simulated | S |
| BLK-10 | Combined Locked Blocks | `combined-locked` | GatePassed | Simulated | M |
| BLK-11 | Hidden Blocks | `hidden` | BlockCleared of revealer | Simulated | M |
| BLK-12 | Dynamite | `dynamite` | MoveCommitted | Simulated (exact) | M |
| BLK-13 | Curtain | `curtain` | GatePassed | Simulated | S |
| BLK-14 | Time Capsule | `time-capsule` | own GatePassed | Simulated, time as warning | S |
| BLK-15 | Color-Switching Block | `color-switching` | GatePassed | Simulated | M |
| BLK-16 | Color Swapping Blocks | `color-swapping` | own GatePassed | Simulated | M |
| BLK-17 | Turn Based Arrow | `turn-based-arrow` | BlockCleared | Simulated | S |
| BLK-18 | Magnet Block | `magnet` | MoveCommitted | Simulated | L |

**Stacking rules:** at most one effect of each type per block. Exclusive sets: {Arrow, Turn Based Arrow}; {Combined, Combined Locked, Magnet}; Moving Blocks excludes every exit-related effect (Star, Layer, Bomb, Dynamite, Curtain, Time Capsule, Color-Switching, Color Swapping, Key, Scissors). Blockers (Ice, Chained, Ropes, Hidden) may stack; the block is free only when all are cleared. A blocker on one member locks the whole group (DEC-MECH-25).

### BLK-01 Arrow

- **Official rule:** arrow blocks move only in their indicated direction.
- **Slice rule:**
  - The block has an axis, Horizontal or Vertical (DEC-MECH-4). During a drag its displacement on the other axis is always 0; it moves both ways along its axis through free cells (RULE-10 otherwise unchanged). A diagonal pointer delta is projected onto the axis.
  - It can exit only through doors on the two sides its axis points to. Resting flush against a door on a perpendicular side does not let it exit.
  - The axis never changes during the attempt. Drag constraints are fixed at drag start.
- **Dependencies:** base movement and exit rules; hook "which axes may this block move on". Reused by BLK-17.
- **JSON:** `arrow`, `axis` = `Horizontal` or `Vertical` (required).
- **Editor:** select a block, Add effect > Arrow, two-button axis toggle; the Arrow tool cycles a clicked block through None, Horizontal, Vertical. The grid draws a double-headed arrow badge along the axis.
- **View and assets:** full kit coverage. `Arrows.fbx` `Arrow_1`, `Arrow_2`, `Arrow_3` are double-headed and sized for 1, 2, 3 cells (measured). Pick the variant by the block's run along the axis through its bounds center, clamped to 3 (longer runs reuse `Arrow_3`, no stretching). Pooled, parented to the block view, one shared material.
  - Centring note (DEC-158, revision 1 of `TASK-20260926-212320271-98b25efa-level-editor-arrow-markers`): as built, the marker is centred on the footprint's bounding box on both axes and its length is the box's extent along the axis, clamped to 1-3 (`ArrowMarkerRun`, shared by the canvas, the scene preview and the Game scene). A 2 x 2 block's arrow runs on the line between its columns or rows, and an L or T keeps the box centre. This replaces "run through the bounds center, ties to the lower index" here and in the edge cases below.
- **Solver:** Static. Moves are generated on the axis only; exits only on the two axis sides. Pre-check error: no door of the block's color on either axis side within its row or column span.
- **Shipped acceptance criteria:** AC-BLK-01.1 to AC-BLK-01.6 are defined once in the [main PRD](Color-Block-Jam-PRD.md#712-arrow-block-t1-req-o5-blk-01). Use those criteria and verification rows as the case contract.
- **Edge cases:** 1x1 (`Arrow_1`); an axis across a long block (a vertical I4 with a horizontal axis uses the 1-cell run); L or T footprints (run through the bounds center, ties to the lower index); Arrow with Ice (ice first, then the axis).
- **Booster interaction:** Hammer removes an ordinary Arrow block as one block piece; Rocket keeps the axis and re-picks the run after a valid one-cell removal without splitting; Vacuum removes it with its color.

### BLK-02 Layer

- **Official rule:** layered blocks are removed one layer at a time, outer layer first.
- **Slice rule:** the block has a color stack above its base color; the active color is the outermost remaining one. Pushed flush against a fitting door of the active color, it peels one layer (`BlockCleared(Peel)`, `GatePassed`, `MoveCommitted`) and stays on its cells; with no layer left it exits normally. It counts as one block for the win.
- **Dependencies:** event model; exit hook outcome `Peel`. **JSON:** `layer`, `layerColors` (1 to 3 palette ids, outermost first).
- **Editor:** layer swatch list with add, remove, drag-reorder (max 3); the grid shows the outer color with nested inset borders and a count.
- **View and assets:** no layer mesh. Compose the footprint once per stacked color with the same pieces, each shell one piece height higher; the top shell carries the active color. A peel tweens the top shell through the door and returns its pieces. Pool warm-up counts layers.
- **Solver:** state adds a layer index per block. Static check: each stacked color has a door that fits the block on some side.
- **Acceptance criteria:**
  - AC-BLK-02.1: While a layer remains, a base-color door blocks the block like a wall.
  - AC-BLK-02.2: A push into an active-color door peels exactly one layer, keeps the cells, and raises one `BlockCleared(Peel)`.
  - AC-BLK-02.3: After the last peel the block exits through a base-color door and counts once for the win.
  - AC-BLK-02.4: The editor edits up to 3 layers; save and reload keep the order; Restart restores all layers.
- **Edge cases:** a layer equal to the next color (valid, warned); Layer with Ice (ice first); a peel in the frame the timer ends (commands first, the peel counts).
- **Booster interaction:** Hammer peels one layer without a door; Rocket removes the cell from every shell; Vacuum matches the active color and removes the whole block.

### BLK-03 Ice

- **Official rule:** an iced block is frozen until a set number of other blocks are removed.
- **Slice rule:**
  - The block has an ice count N from 1 to 20. While N > 0 it cannot be dragged or exit and occupies its cells like a wall; a tap or drag plays a short shake only.
  - Each `BlockExited` of another block lowers N by 1; booster removals do not. Other mechanics may use broader `BlockCleared` triggers according to their own rules (DEC-MECH-2). At 0 the ice breaks and the block is normal from the next input.
  - Its own removal never lowers its own count. Hammer targeting an active Ice shell removes that obstacle, sets N to 0, and leaves the underlying block; one hit does not also remove the block.
- **Dependencies:** Ice listens to `BlockExited` and uses the counter badge view; hook "may this block start a drag". Other counters declare their own event triggers.
- **JSON:** `ice`, `clearsToThaw` (1 to 20, required).
- **Editor:** Ice card with a clamped stepper; the grid draws a pale blue overlay with the number.
- **View and assets:** `Assets/ColorBlockJam/Art/Textures/icetexture` in one shared transparent ice material. The ice shell is that material added as a second material on the block's own pieces, whose shader expands each surface slightly; the colored block stays underneath (DEC-184). A projected UI Toolkit count sits over the center cell and changes only when N or board layout changes. Break: a short opacity fade, then the material is removed.
- **Solver:** PRE-2 reports **Proven unsolvable** when N exceeds the number of other required blocks, without search and with both numbers. Recheck this bound when Layer or Moving Blocks is implemented.
- **Shipped acceptance criteria:** AC-BLK-03.1 to AC-BLK-03.7 are defined once in the [main PRD](Color-Block-Jam-PRD.md#711-ice-blocker-t1-req-o4-blk-03). Use those criteria and verification rows as the case contract.
- **Edge cases:** only frozen blocks remain and no clear is possible (fails on time; validation warns); two iced blocks that depend on each other (search needed, the static check cannot prove it); the last other block exits in the frame the timer ends (the exit commits first, the ice breaks, then the timer fails the level because blocks remain).
- **Booster interaction:** Hammer removes the active Ice shell and sets N to 0 while leaving the block; Rocket may remove one valid cell and keeps N; Vacuum removes the block regardless of N.

### BLK-04 Star

- **Official rule:** star blocks exit only through star doors; normal blocks may also use star doors.
- **Slice rule:** a door can carry the door effect `star-door` (not a GTE id). A star block exits only through a fitting star door of its color; a normal door of its color is a wall to it. A normal block may use a star door of its color. Star applies to every exit and every peel (DEC-MECH-26).
- **Dependencies:** door effect `star-door`; door inspector. **JSON:** block `star` (no params); door `star-door` (no params).
- **Editor:** Star toggle on block and door panels; star badge on both.
- **View and assets:** no star art. Projected UI Toolkit star glyphs use a project or fallback font asset at the block and door positions; star doors get a brighter tint.
- **Solver:** the exit test adds star compatibility. Static error: a star block with no fitting star door of its color.
- **Acceptance criteria:**
  - AC-BLK-04.1: A star block is stopped by a matching normal door and exits through a matching star door.
  - AC-BLK-04.2: A normal block exits through a matching star door.
  - AC-BLK-04.3: Both flags survive save and reload and show badges.
- **Edge cases:** Star with Layer (each peel needs a star door of the active color); Color Swapping changes the color, the star flag stays.
- **Booster interaction:** none specific.

### BLK-05 Combined

- **Official rule:** combined blocks are stuck together; removing one part separates them.
- **Slice rule:** two edge-adjacent members form a rigid group. Dragging either moves both; collision uses the union footprint. Pushed against a door, each flush member is tested alone (color, own width), in ascending id; the first that fits exits (`BlockCleared(MemberExit)`, `GatePassed`), the group dissolves, and the partner stays as a normal block. The partner does not exit in the same push.
- **Dependencies:** a new movement-group hook (rigid group provider), reused by BLK-10 and BLK-18. **JSON:** `combined`, `partner` (block id, symmetric on both members).
- **Editor:** Combine link tool: select A, click an edge-adjacent B; both records are written; a pin badge sits on each shared edge. Errors: asymmetric links, non-adjacent, a block in two groups.
- **View and assets:** members compose normally; a pooled pin (`ModularBlock_Center` scaled to about 0.3, neutral tint) on each shared edge returns to its pool on dissolve.
- **Solver:** the movable unit is the group; exits are per member and dissolve the group.
- **Acceptance criteria:**
  - AC-BLK-05.1: Dragging either member moves both; neither enters a cell the other cannot also enter.
  - AC-BLK-05.2: Pushing the pair into a door that matches and fits member A exits only A; B stays and becomes normal.
  - AC-BLK-05.3: A door matching neither member blocks the pair like a wall.
- **Edge cases:** both members fit the same door (lower id exits); a frozen member locks the group; two different arrow axes on members (validation error).
- **Booster interaction:** Hammer or Vacuum removing one member dissolves the group; Rocket on a member dissolves the group when no shared edge remains.

### BLK-06 Chained Blocks

- **Official rule:** a chained block cannot move until the required number of key blocks are removed.
- **Slice rule:** the chained block has N from 1 to 9; while N > 0 it cannot move or exit. Blocks with the companion `key` (no door target) carry one key. When a key carrier is cleared (any cause, DEC-MECH-2), its key goes to exactly one chained block: smallest remaining N > 0, then lowest id (DEC-MECH-9). At 0 the chain breaks. Extra keys are discarded. The key flight is presentation only.
- **Dependencies:** session-owned `ChainLedger`; counter badge from BLK-03. **JSON:** `chained`, `keysRequired` (1 to 9); companion `key` with optional `target` (a door id, used by GTE-05; empty for chained blocks).
- **Editor:** Chain card with a stepper; Key toggle on other blocks; lock and key badges; summary "Keys 3 / Required 3"; Save is blocked when keys are fewer than required.
- **View and assets:** block darkened through the property block, `Assets/ColorBlockJam/Art/UI/Supplied/Icons/Navigation/ic_lock.png` on a pooled quad plus a count; key carriers show the `Key 1` mesh from `WallAndDoor.fbx` if it imports cleanly (unverified, OQ-M2), else a key glyph.
- **Solver:** state adds chain counts; the same deterministic assignment. Static check: key carriers >= sum of chain counts.
- **Acceptance criteria:**
  - AC-BLK-06.1: A chained block cannot move or exit and blocks others.
  - AC-BLK-06.2: Each key carrier cleared lowers exactly one chain, chosen by smallest remaining count, then lowest id.
  - AC-BLK-06.3: At 0 the lock clears and the block can be dragged on the next input; Restart restores all counts.
- **Edge cases:** a chained key carrier (its key is delivered only when it leaves after being freed); more keys than required (validation warning).
- **Booster interaction:** the earlier Hammer counter decrement is catalog research and needs review against the settled semantic layer rule; a key carrier removed by Vacuum still delivers its key. Rocket cannot remove a 1x1 carrier.

### BLK-07 Bomb

- **Official rule:** bomb blocks must be removed before they explode. Rollic gives no counter unit.
- **Rule:** the bomb has S seconds (DEC-MECH-5). Its countdown advances only in `Playing`, in the session tick after commands, pauses with Suspend, and stops on teardown or when the bomb leaves. Decide whether Time Freeze suppresses this separate countdown before implementing Bomb; Time Freeze already holds the level timer. At 0 with the bomb on the board, the session requests `Failed` with reason `BombExploded` through the terminal latch. Bombs tick before the level timer, so a same-frame tie reports the bomb.
- **Data and editor:** `bomb`, `seconds` (5 to 120). Bomb card with a stepper; badge with the seconds; warning when S >= the level timer.
- **View and assets:** no bomb art. A pooled badge backing (`ModularBlock_Center` scaled, near-black tint) has a projected UI Toolkit count that changes once per second; a scale pulse runs in the last 3 seconds. The fail popup says "The bomb exploded".
- **Solver:** time is not modeled. Heuristic warning "Bomb may be too tight" from the shortest found solution's moves times a seconds-per-move estimate (default 2). Never reported as proven.
- **Acceptance criteria:**
  - AC-BLK-07.1: The label counts only in `Playing`, holds while `Paused` or frozen, and resumes from the same value.
  - AC-BLK-07.2: At 0 with the bomb on board the state becomes `Failed` once, the popup names the bomb, and input, timer, and tweens are already stopped.
  - AC-BLK-07.3: A bomb that leaves before 0 never fails the level; the countdown tick allocates 0 B.
- **Edge cases and boosters:** the bomb's exit commits in the frame it reaches 0 (the exit wins); the last required block exits while a bomb shows 1 s (`Won` latches first); a frozen or chained bomb still counts down. Hammer and Vacuum remove an ordinary single-layer bomb safely; Rocket may shrink only a larger bomb with a valid cell and keeps its countdown.

### BLK-08 Ropes

- **Official rule:** roped blocks are stuck; removing scissors of the same color cuts their ropes.
- **Slice rule:** a roped block holds 1 to 3 rope colors and cannot move or exit while any rope remains. A block with the companion `scissors` carries scissors of one color. When a scissors carrier is cleared (any cause), it cuts one rope of its color on the lowest-id roped block that still has one; otherwise the scissors are discarded (DEC-MECH-9).
- **Data and editor:** `ropes`, `ropeColors` (1 to 3, repeats allowed); companion `scissors`, `colorId`. Rope swatches, scissors toggle; colored bands and scissors badge; Save blocked when a rope color has fewer scissors than ropes.
- **View and assets:** no rope or scissors art. Pooled bands (`ModularBlock_Edge` stretched across the bounds) tinted per rope; a cut scales the band to zero; a scissors glyph on carriers.
- **Solver:** state adds the uncut rope multiset per block. Static check per color: scissors >= ropes.
- **Acceptance criteria:**
  - AC-BLK-08.1: A roped block cannot move or exit while any rope remains.
  - AC-BLK-08.2: A scissors carrier cleared cuts exactly one rope of its color on the lowest-id roped block that has one; after the last cut the block moves.
- **Edge cases and boosters:** a roped scissors carrier (acts only after it is freed); Color Swapping does not recolor ropes or scissors. Hammer cuts one rope (lowest index); a carrier removed by a booster still cuts.

### BLK-09 Moving Blocks

- **Official rule:** moving blocks move freely unless locked, roped, or screwed, but cannot be taken out.
- **Slice rule:** a neutral obstacle. It drags like a normal block, every door treats it as a wall, it never exits, and it does not count for the win (`Won` = every required block has left). It may carry Arrow, Ice, Chained, or Ropes. Rollic's "screwed" state is not defined anywhere and is out of scope (DEC-MECH-29).
- **Data and editor:** `moving-obstacle` (no params); block color is the reserved `neutral` palette id (DEC-MECH-8). The swatch locks to Neutral grey; door color pickers never offer Neutral.
- **View and assets:** normal composition with a neutral grey tint.
- **Solver:** a movable obstacle; its position is state; it adds no transitions.
- **Acceptance criteria:**
  - AC-BLK-09.1: A moving block can be dragged but no door lets it exit.
  - AC-BLK-09.2: The level is `Won` when all other blocks leave, with moving blocks still on the board.
  - AC-BLK-09.3: A level with only moving blocks is a structural error.
- **Edge cases and boosters:** a moving block parked in front of the only door (legal, the solver handles it). Hammer and Rocket may remove or shrink it; Vacuum cannot choose Neutral; its booster removal counts as a clear.

### BLK-10 Combined Locked Blocks

- **Official rule:** two linked blocks must be separated by sending other blocks through gates before either can pass.
- **Slice rule:** a BLK-05 group with a lock count N from 1 to 9. While N > 0 the group moves as one and doors treat both members as walls. Each `GatePassed` of a block outside the pair lowers N. At 0 the link breaks and both members are independent (DEC-MECH-11).
- **Data and editor:** `combined-locked`, `partner`, `gatePassesToUnlock` (equal on both members). Combine tool with a Locked option and one stepper; pins plus a lock badge on the seam. Unequal counts are a structural error.
- **View and assets:** BLK-05 pins plus `ic_lock.png` and a count at the seam midpoint.
- **Solver:** group unit plus a counter. Static check: possible gate passes of other blocks >= N.
- **Acceptance criteria:**
  - AC-BLK-10.1: While locked, the pair moves as one and neither member exits.
  - AC-BLK-10.2: Each gate pass of another block lowers the count by 1; booster removals do not.
  - AC-BLK-10.3: At 0 the members separate and each drags and exits alone.
- **Edge cases and boosters:** only the locked pair remains with N > 0 (unsolvable, flagged). Hammer on either member lowers N by 1.

### BLK-11 Hidden Blocks

- **Official rule:** a hidden block stays invisible until a specific visible block is cleared; then it must also be cleared.
- **Slice rule:** concealed model (DEC-MECH-6). The block occupies its cells from the start, shows as neutral with a "?", cannot move or exit, and hides its color. When its revealer is cleared (any cause), it reveals its color in the same command phase. It counts for the win from the start.
- **Data and editor:** `hidden`, `revealer` (block id). "Pick revealer" highlights valid blocks; designers see the block hatched in its real color with a line to the revealer. A reveal cycle is a structural error.
- **View and assets:** neutral tint plus a projected UI Toolkit "?" glyph; the reveal tweens the tint to the real color over 0.2 s through the property block.
- **Solver:** a static obstacle until the revealer leaves; full information. Static check: the revealer can leave (not a Moving block).
- **Acceptance criteria:**
  - AC-BLK-11.1: A hidden block shows no color, cannot move, and blocks others.
  - AC-BLK-11.2: When the revealer is cleared, the block shows its color and can be dragged on the next input; the level is not `Won` while it remains.
- **Edge cases and boosters:** reveal chains (acyclic only). Boosters cannot target a concealed block and Vacuum skips it (DEC-MECH-14); a revealer removed by a booster reveals it.

### BLK-12 Dynamite

- **Official rule:** dynamite shows a move number; if it is not cleared in time it explodes and ends the level.
- **Slice rule:** move count N from 1 to 30. On each `MoveCommitted`: if the move was the dynamite's own exit, it leaves safely; otherwise N drops, and at 0 with the dynamite on board the session requests `Failed` with reason `DynamiteExploded`. Releases that change nothing and booster uses are not moves.
- **Data and editor:** `dynamite`, `moves` (1 to 30). Stepper; red badge with the count.
- **View and assets:** the BLK-07 badge tinted red, count updated per move.
- **Solver:** exact, since one solver transition equals one committed move. A strong validation case.
- **Acceptance criteria:**
  - AC-BLK-12.1: Each board-changing drag lowers the count by 1; a release on the start cell does not.
  - AC-BLK-12.2: Exiting the dynamite on its last move does not fail the level; a move that reaches 0 with it on board fails once with the dynamite reason.
  - AC-BLK-12.3: Validation reports unsolvable when no solution clears it within N moves.
- **Edge cases and boosters:** a layer peel counts as a move; a frozen dynamite still counts others' moves; two dynamites count independently. Time Freeze does not affect it; Hammer removes it without adding moves.

### BLK-13 Curtain

- **Official rule:** a curtain block can pass only while open; the curtain toggles each time any block goes through a gate.
- **Slice rule:** a flag starts at `startsOpen`. The block can always be dragged; it may exit only while open; closed, every door is a wall to it. Each `GatePassed` of another block flips the flag.
- **Data and editor:** `curtain`, `startsOpen` (bool). Open/Closed switch; striped badge when closed.
- **View and assets:** no curtain art. A pooled cover shell composed from the pieces in a neutral fabric tint; closing lowers it, opening raises it.
- **Solver:** one bit per curtain block.
- **Acceptance criteria:**
  - AC-BLK-13.1: A closed curtain block moves but cannot exit; an open one exits normally.
  - AC-BLK-13.2: Every gate pass of another block flips it exactly once; a booster removal does not.
- **Edge cases and boosters:** two passes before the player acts (parity); a closed curtain as the last block (unsolvable, reported). Hammer flips it once.

### BLK-14 Time Capsule

- **Official rule:** blocks with a +time value add that time when sent through the matching gate.
- **Slice rule:** when the capsule's own `GatePassed` commits, the level timer gains its bonus in the same command phase, before the timer advances. No cap.
- **Data and editor:** `time-capsule`, `bonusSeconds` (5 to 60). Stepper; "+10" badge.
- **View and assets:** `Assets/ColorBlockJam/Art/UI/Supplied/Icons/Gameplay/ic_clock.png` on a quad plus "+10"; on exit a short HUD timer pulse owned by the HUD view.
- **Solver:** no reachability effect; time heuristics add the bonus.
- **Acceptance criteria:**
  - AC-BLK-14.1: The capsule's exit adds exactly its bonus; an exit in the frame the timer ends adds time first and the level continues.
  - AC-BLK-14.2: A booster removal grants no time.
- **Edge cases and boosters:** the capsule as the last block (`Won`, bonus irrelevant); exit during Time Freeze (bonus applies at once).

### BLK-15 Color-Switching Block

- **Official rule:** a two-color block; an exit of its outer color clears the outer color, an exit of another color swaps inner and outer.
- **Slice rule:** outer color plus base (inner) color (DEC-MECH-7). While two-colored it can be dragged but cannot exit. On each `GatePassed` of another block with color c: c equals outer, the outer color is removed; otherwise the colors swap.
- **Data and editor:** `color-switching`, `outerColorId` (must differ from the base). Outer ring swatch; ring badge.
- **View and assets:** inner shell at normal scale and an outer ring of edge and corner pieces tinted the outer color; a swap swaps the two property-block colors; a clear returns the ring.
- **Solver:** state adds (outer, inner, cleared); the solver knows the exiting color.
- **Acceptance criteria:**
  - AC-BLK-15.1: While two-colored it moves but no door lets it exit.
  - AC-BLK-15.2: An outer-color exit clears the outer color; any other exit swaps; after clearing it exits through an inner-color door.
- **Edge cases and boosters:** no other block of either color (unsolvable, static check). Hammer clears the outer color; Vacuum matches the active (outer) color and removes the block.

### BLK-16 Color Swapping Blocks

- **Official rule:** moves like a normal block; sending it through a gate swaps the colors of blocks on the board.
- **Slice rule:** the block carries a pair (a, b). When its own `GatePassed` commits, every remaining block swaps a and b in its base color, layer colors, and switching colors, in the same command phase. Doors, ropes, and scissors keep their colors.
- **Data and editor:** `color-swapping`, `colorA`, `colorB` (different). Two swatches; swap badge.
- **View and assets:** two small pooled dots (scaled centers) tinted a and b; recolor is one property-block set per affected view.
- **Solver:** the transition rewrites block colors; state hashing includes colors.
- **Acceptance criteria:**
  - AC-BLK-16.1: On its exit every remaining a block becomes b and every b block becomes a; doors do not change.
  - AC-BLK-16.2: Recolor happens in the same frame with no material instances (Frame Debugger shows the shared material).
- **Edge cases and boosters:** two swappers of the same pair cancel out; a booster removal swaps nothing.

### BLK-17 Turn Based Arrow

- **Official rule:** starts limited to its shown direction; after the displayed number of clears it moves freely.
- **Slice rule:** the BLK-01 axis rule plus a count N from 1 to 20. Each `BlockCleared` of another block lowers N; at 0 the axis limit is removed. The new freedom applies from the next drag.
- **Data and editor:** `turn-based-arrow`, `axis`, `clearsToRelease`. Axis toggle plus stepper; arrow badge with count.
- **View and assets:** BLK-01 arrow plus a count; at 0 the arrow scales out and returns to its pool.
- **Solver:** the BLK-01 constraint while N > 0; state adds N.
- **Acceptance criteria:**
  - AC-BLK-17.1: Before release it moves only on its axis; the count drops by 1 per other block cleared.
  - AC-BLK-17.2: At 0 the arrow disappears and it moves on both axes and exits through any fitting matching door.
- **Edge cases and boosters:** release during a drag (applies from the next drag). Hammer lowers N by 1.

### BLK-18 Magnet Block

- **Official rule:** magnet blocks connect when matching colors are aligned, which unlocks the path.
- **Slice rule:** a magnet block cannot be dragged alone. When a committed move leaves a same-color block edge-adjacent to it, the two fuse into one block: union footprint, same color, no magnet effect carried over. The fused block moves and exits as one piece (its full width must fit) (DEC-MECH-10).
- **Data and editor:** `magnet` (no params). Magnet badge; warning when no other block of its color exists.
- **View and assets:** a magnet glyph; on fusion both views release their pieces and one view composes the union footprint (`FootprintChanged`).
- **Solver:** fusion changes the unit set; state hashing uses footprints, not only ids.
- **Acceptance criteria:**
  - AC-BLK-18.1: A magnet block does not move on drag.
  - AC-BLK-18.2: A same-color block moved edge-adjacent fuses in the same commit, the view matches the union footprint, and no `Instantiate` happens after warm-up.
  - AC-BLK-18.3: The fused block exits only through a door that fits its full width.
- **Edge cases and boosters:** two same-color blocks touch in one move (only the moved block fuses); a fused shape too wide for every door (solver finds it). Rocket on a fused block removes one cell and recomposes.

## 4. Gate Effects

The plain color door (side, start, span, color, no effect) is the default rule (RULE-7, RULE-12). Everything below is a door effect in the door's `effects` list. A door passes a block when the color matches the door's current color, every active effect allows it, and the block's width fits inside one contiguous open run of the span. Counters and toggles of all effects on a door advance independently, even while another effect blocks it. Door pieces follow the edge rule: every boundary segment holds one pooled `Wall` or `Door_1x1` piece; state changes swap or overlay pooled pieces and never instantiate. `corner_3.002` is unusable (self-intersecting polygon, discarded on import).

| ID | Rollic name | Type id | Listens to | Hammer target | Solver | Size |
| --- | --- | --- | --- | --- | --- | --- |
| GTE-01 | Door (open/close) | `toggle-door` | BlockCleared | No | Static (parity) | S |
| GTE-02 | Iced Door | `iced-door` | BlockCleared | Yes | Static | S |
| GTE-03 | Moving Door Lock | `moving-door-lock` | BlockCleared | No | Static | M |
| GTE-04 | Colorful Door | `colorful-door` | own GatePassed | No | Simulated | S |
| GTE-05 | Locked Door | `locked-door` | BlockCleared of keys | Yes | Static graph | M |
| GTE-06 | Size-Changing Door | `size-changing-door` | BlockCleared | No | Static | M |
| GTE-07 | Color-Switcher | `color-switcher` | GatePassed | No | Static (parity) | S |
| GTE-08 | Jumping Single Door | `jumping-blocker` | GatePassed | No | Static | M |
| GTE-09 | Colorful Jumping Door | `colorful-jumping-door` | BlockCleared | No | Static | M |
| GTE-10 | Button Doors | `button-door` | OccupancyChanged | No | Position-driven | M |
| GTE-11 | Laser Door | `laser-door` | OccupancyChanged | No | Position-driven | M |

### GTE-01 Door, open and close (S)

- **Official rule:** doors open and close each time a block is removed.
- **Slice rule:** an open flag starts at `startsOpen`; closed, the door is a wall (RULE-14). After each `BlockCleared` the flag flips. The clear that raised the event has already committed, so the flip never affects it. Code name `ToggleDoor`, so the plain door keeps the name `Door`.
- **Data and editor:** `toggle-door`, `startsOpen`. Checkbox "Starts open"; closed doors draw hatched; the "after N clears" preview scrubber shows the flip.
- **View and assets:** closed = pooled `Wall` caps slide over the `Door_1x1` pieces (tween on the cap); `Door_Arrow` hides while closed. **Solver:** state = `startsOpen XOR (clears mod 2)`.
- **Acceptance criteria:** AC-GTE-01.1: Open at start; after one clear a matching, fitting block is stopped; after a second clear it exits. AC-GTE-01.2: A Vacuum that removes 3 blocks flips the door 3 times; after `Won` or `Failed` no flip happens and no door tween remains.
- **Edge cases and boosters:** the door closes while a block is dragged toward it (the block stops at its next step); Toggle under Ice keeps flipping. Not a Hammer target.

### GTE-02 Iced Door (S)

- **Official rule:** ice locks the door; removing the required number of blocks breaks it.
- **Slice rule:** count N from 1 to 20; while N > 0 the door is a wall. Each `BlockCleared` lowers N by 1; at 0 the effect ends. Reuses the BLK-03 counter class.
- **Data and editor:** `iced-door`, `clearsToOpen`. Stepper; ice tint with the number.
- **View and assets:** ice overlays (`Door_1x1` mesh with the shared ice material) per segment and one count label; break = scale-down tween, then pool return. **Solver:** `max(0, N - clears)`; warning when N exceeds possible clears.
- **Acceptance criteria:** AC-GTE-02.1: With N = 3 a matching block is stopped; after 3 clears it exits; the label changes once per clear with no per-frame allocation. AC-GTE-02.2: Hammer on the door lowers N by 1.
- **Edge cases and boosters:** a Vacuum removing more blocks than N clamps at 0; N = 0 in JSON is rejected.

### GTE-03 Moving Door Lock (M)

- **Official rule:** locks move to the next door in a set direction.
- **Slice rule:** the lock makes its door a wall. After each `BlockCleared` (Rollic names no trigger, DEC-MECH-17) it moves to the next door in perimeter order, clockwise or counter-clockwise, skipping doors that hold a lock. Perimeter order is built once: top left to right, right top to bottom, bottom right to left, left bottom to top.
- **Data and editor:** `moving-door-lock`, `direction` (`Clockwise`, `CounterClockwise`), on the start door. Lock badge with a dashed arrow to the next door; error with one door; warning when locks >= doors.
- **View and assets:** `Wall` caps plus `ic_lock.png` on a quad and a small `Arrow_1`; the lock group tweens to the next door. **Solver:** position = `(start + clears) mod doors` with the skip rule.
- **Acceptance criteria:** AC-GTE-03.1: The locked door stops a matching block; after one clear the lock is on the next door in its direction and the previous door lets the block exit. AC-GTE-03.2: A lock skips a door that holds another lock; Restart returns it to its authored door.
- **Edge cases and boosters:** the lock lands on an iced door (both block, counted independently). Not a Hammer target.

### GTE-04 Colorful Door (S)

- **Official rule:** the door lets one color pass, then shows the next required color.
- **Slice rule:** the current color is `colorSequence[i]`, i starting at 0. Only that color passes. After a block passes this door, i increases; after the last color the door keeps it (DEC-MECH-13). Passes through other doors do not advance it.
- **Data and editor:** `colorful-door`, `colorSequence` (door color must equal the first entry; the editor writes it). Swatch list with reorder; upcoming-color strip; a sequence of 1 collapses to a plain door; warning when a color is asked more often than blocks exist.
- **View and assets:** current color through the property block; up to 3 upcoming colors as small flat tiles behind the door; a color tween on the door root. **Solver:** per-door index.
- **Acceptance criteria:** AC-GTE-04.1: Only `colorSequence[0]` exits; after it the door shows and accepts only the next color; after the last it keeps the last. AC-GTE-04.2: Exits through other doors do not advance it; Restart resets the index.
- **Edge cases and boosters:** a Vacuum removes blocks a later color needs (the door gets stuck, the timer decides). Not a Hammer target.

### GTE-05 Locked Door (M)

- **Official rule:** a chained gate tied to a color opens after key blocks are sent through their matching doors.
- **Slice rule:** count K; while K > 0 the door is a wall. A key block whose `key.target` is this door lowers K by 1 when it is cleared (a door exit or a booster, DEC-MECH-2), so a booster never makes the level unwinnable. Keys name their door explicitly (DEC-MECH-9).
- **Data and editor:** `locked-door`, `keysRequired`; key blocks carry companion `key` with `target` = door id. Link tool: mark a key, click the door; the count follows the linked keys. Errors: count differs from links; a key whose only matching door is its own target.
- **View and assets:** grey `Wall` caps, `ic_lock.png`, and a count; key blocks carry a small gold-tinted `ic_lock.png`. No key or chain art. **Solver:** a key-to-lock graph; the validator detects cycles and missing keys.
- **Acceptance criteria:** AC-GTE-05.1: With K = 2 the door stops every block; each linked key cleared lowers K and the label; at 0 a matching block exits. AC-GTE-05.2: Clears of unlinked blocks do not change K; Hammer lowers K by 1.
- **Edge cases and boosters:** a key linked to a deleted door (the editor removes the link and warns); a chain on a Colorful Door (the chain breaks first).

### GTE-06 Size-Changing Door (M)

- **Official rule:** the door changes size as blocks are removed, wide to narrow or the opposite.
- **Slice rule:** the door span is the reserved maximum. The open width is `spanSequence[i]` (each 1 to span), i rising by 1 per `BlockCleared` and then holding; `anchor` is start, center (odd difference rounds toward start), or end. Reserved segments outside the open run are walls; the exit fit uses the open run.
- **Data and editor:** `size-changing-door`, `spanSequence`, `anchor`. Width list clamped to span; anchor dropdown; the clear preview shows each width.
- **View and assets:** each reserved segment is a `Door_1x1` (open) or `Wall` (closed) piece; a change swaps pooled pieces and re-centers `Door_Arrow`; the edge composer re-runs for that side only. **Solver:** width from the clear count.
- **Acceptance criteria:** AC-GTE-06.1: With `[3,2,1]` a 3-wide block exits at start; after one clear it is stopped and a 2-wide block fits; after the last entry the width holds. AC-GTE-06.2: A width change shows no `Instantiate` and no GC allocation in the Profiler.
- **Edge cases and boosters:** a width change during another block's exit tween (already committed). Not a Hammer target.

### GTE-07 Color-Switcher (S)

- **Official rule:** the door switches between two colors each time a block passes through any gate.
- **Slice rule:** current color = `[color, altColorId][passes mod 2]`, where passes counts `GatePassed` at any door, this one included. Booster removals do not switch it.
- **Data and editor:** `color-switcher`, `altColorId` (differs from the door color). Split-color badge with the current half highlighted.
- **View and assets:** current color through the property block; a thin tile behind shows the alternate; the same color tween as GTE-04. **Solver:** parity.
- **Acceptance criteria:** AC-GTE-07.1: At start only the door color passes; after any pass at any door only the alternate passes; after the next, the door color again. AC-GTE-07.2: A Rocket or Vacuum removal does not switch it.
- **Edge cases and boosters:** two switchers flip together on one pass. Not a Hammer target.

### GTE-08 Jumping Single Door (M)

- **Official rule:** a blocker moves along the gate, covering one spot, and jumps each time a block passes through a gate.
- **Slice rule:** segment `start + offset` is a wall; the rest forms up to two open runs and a block must fit in one. After each `GatePassed` the blocker moves one segment in its direction and wraps inside the span (DEC-MECH-15). Needs span >= 2.
- **Data and editor:** `jumping-blocker`, `startOffset`, `direction` (`Forward`, `Backward`). Click a segment to place it; error on span 1.
- **View and assets:** a `Wall` cap on the blocked `Door_1x1`, tweened to the next segment. **Solver:** offset from the pass count.
- **Acceptance criteria:** AC-GTE-08.1: A block overlapping the blocked segment is stopped; a block that fits the remaining run exits. AC-GTE-08.2: After one pass the blocker is on the next segment; at the span end it wraps.
- **Edge cases and boosters:** a middle blocker splits a 3-span door into two 1-wide runs; booster removals do not move it.

### GTE-09 Colorful Jumping Door (M)

- **Official rule:** the door jumps to a new spot after each clear and changes the color of the gate it lands on.
- **Slice rule:** a color token on the start door with `route` (2 or more doors, starting here). The door holding the token uses the token color. After each `BlockCleared` the token moves to the next route door and wraps; the door it leaves returns to its own color (DEC-MECH-16).
- **Data and editor:** `colorful-jumping-door`, `tokenColorId`, `route` (door ids). Link tool clicks doors in order; numbered route badges. Errors: route shorter than 2, unknown door, two tokens on one start door.
- **View and assets:** the token is a `Door_Arrow` piece tinted the token color that arcs to the next door; the landing door takes the color through the property block. **Solver:** token = `clears mod route length`.
- **Acceptance criteria:** AC-GTE-09.1: The token door accepts only the token color; after one clear the token is on `route[1]` and the previous door accepts its own color. AC-GTE-09.2: After the last entry the token returns to `route[0]`; Restart does the same.
- **Edge cases and boosters:** the token lands on a locked or iced door (color changes, still blocked). Not a Hammer target.

### GTE-10 Button Doors (M)

- **Official rule:** the door opens when a block of the right color stands on its button; each button reacts to its color only.
- **Slice rule:** open while any block of `buttonColorId` covers `buttonCell` with one of its cells; otherwise a wall. The button cell is floor. The state updates on `OccupancyChanged`, cell by cell during a drag, never per frame. One button per door.
- **Data and editor:** `button-door`, `buttonColorId`, `buttonCell`. Button tool: select the door, click the cell; a link line connects them.
- **View and assets:** a flat tile (`ModularBlock_Center` scaled flat) tinted the button color, lowered when pressed; door poses reuse GTE-01 caps. **Solver:** position-driven; a search must include block positions.
- **Acceptance criteria:** AC-GTE-10.1: An empty button keeps the door shut; a button-color block on it opens the door; moving it off closes it; another color does nothing. AC-GTE-10.2: With nothing moving the Profiler shows no button script samples.
- **Edge cases and boosters:** the pressing block exits elsewhere (the door closes after that commit); a booster removing the pressing block closes it.

### GTE-11 Laser Door (M)

- **Official rule:** the door stays open only while its beam reaches the receiver on the other side; a blocked beam closes it.
- **Slice rule:** the beam covers every cell of one row or column between the walls. The door is open while no block or wall-role object occupies a beam cell. Updates on `OccupancyChanged` only; no raycast.
- **Data and editor:** `laser-door`, `axis` (`Row`, `Column`), `index`. Laser tool previews the beam; warning when a static object lies on it.
- **View and assets:** two `Door_Arrow` pieces as emitter and receiver; a `LineRenderer` beam with one shared unlit material ending at the first blocking cell. **Solver:** position-driven.
- **Acceptance criteria:** AC-GTE-11.1: With the beam clear a matching block exits; a block on any beam cell closes the door; moving it off opens it. AC-GTE-11.2: Ivy or a closed Barrier on the beam keeps the door closed until it clears; nothing is evaluated per frame.
- **Edge cases and boosters:** the exiting block sits on its own door's beam (it must move off first). Not a Hammer target.

## 5. Board Objects

A board object is a cell object in the level's `cellObjects` list: a set of cells plus one effect. Each cell has an occupancy role that the movement query asks for (hook "may this block enter this cell"): Wall, ColorGate (only one color may enter), Floor, or Movable (dragged like a block, never exits). Movement code never knows object types. Blocks held inside a Crate, Moveable Crate, Flower Crate, or Elevator stay in the `blocks` list with the block effect `contained` (`container` = object id, `stackIndex` for the Elevator), so the level envelope does not change (DEC-MECH-23). Contained blocks are not visible and boosters skip them (DEC-MECH-14); they count for the win.

| ID | Name | Type id | Role | Listens to | Hammer target | Solver | Size |
| --- | --- | --- | --- | --- | --- | --- | --- |
| OBJ-01 | Colorful Path | `colorful-path` | ColorGate | none | No | Static | S |
| OBJ-02 | Crate | `crate` | Wall | BlockCleared | Yes | Static | M |
| OBJ-03 | Moveable Crate | `moveable-crate` | Movable | GatePassed | Yes | Position-driven | L |
| OBJ-04 | Flower Crate | `flower-crate` | Movable | BlockCleared of leaves | Yes | Position-driven | L |
| OBJ-05 | Barrier | `barrier` | Wall or Floor | BlockCleared | No | Static (parity) | S |
| OBJ-06 | Colorful Crate | `colorful-crate` | Wall | BlockCleared | No | Static | S |
| OBJ-07 | Colorful Moving Path | `colorful-moving-path` | ColorGate | MoveCommitted | No | Position-driven | XL |
| OBJ-08 | Ivy | `ivy` | Wall | GatePassed | Yes (one tile) | Static | S |
| OBJ-09 | Elevator | `elevator` | Floor | OccupancyChanged | No | Position-driven | L |

### OBJ-01 Colorful Path (S)

- **Official rule:** only blocks of the same color can use these paths.
- **Slice rule:** path cells are ColorGate: a block of the path color moves onto and rests on them; any other block treats them as walls. A block that changes color while on the path may stay and move off, but not onto other path cells (DEC-MECH-20).
- **Data and editor:** `colorful-path`, `colorId`. Path brush: pick a color, paint cells. Error: a path cell under a block of another color at start.
- **View and assets:** flat tiles (`ModularBlock_Center` scaled flat) tinted the path color at reduced brightness; they brighten while a matching block is dragged (view-owned tween). **Solver:** static passability per color.
- **Acceptance criteria:** AC-OBJ-01.1: A matching block moves across path cells; another color stops at the first path cell. AC-OBJ-01.2: JSON round-trip keeps cells and color.
- **Edge cases and boosters:** a two-colored block (BLK-15) uses its active outer color. Not a Hammer target.

### OBJ-02 Crate (M)

- **Official rule:** crates can hide surprises, even a bomb, and clear after the required blocks are removed.
- **Slice rule:** crate cells are walls. Each `BlockCleared` lowers `clearsToOpen` by 1 (a count, DEC-MECH-23). At 0 the crate disappears and its contained blocks appear at their authored cells. A contained Bomb starts its countdown at reveal.
- **Data and editor:** `crate`, `clearsToOpen`; contents use `contained`. Crate tool paints the footprint; "Edit contents" places blocks inside. Errors: content outside the footprint, overlapping contents.
- **View and assets:** the crate body is composed from the block pieces by the same neighbor rule, brown tint, count label; reveal scales the crate to 0 while content views come from the block pool. **Solver:** hidden blocks join at a known clear count.
- **Acceptance criteria:** AC-OBJ-02.1: A crate stops every block; after the count of clears it disappears and its contents appear. AC-OBJ-02.2: `Won` requires the revealed blocks to clear; Hammer lowers the count by 1; Restart puts the contents back.
- **Edge cases and boosters:** an empty crate (a timed wall); the last visible block clears while the crate is closed (not `Won`). Rocket and Vacuum ignore crates and contents.

### OBJ-03 Moveable Crate (L)

- **Official rule:** the crate shows a number that drops each time a block passes through a gate; at zero it disappears and reveals a block underneath.
- **Slice rule:** a Movable object: dragged like a block, never exits, doors are walls to it. Each `GatePassed` lowers `gatePassesToOpen`. At 0 it disappears and its one contained block (same footprint) appears at the crate's current cells.
- **Data and editor:** `moveable-crate`, `gatePassesToOpen`; one `contained` block. Stamp a shape, set the count, assign the hidden block's color. Error: different footprint.
- **View and assets:** the OBJ-02 composed crate with a lighter tint, using the block drag view. **Solver:** position-driven plus a count.
- **Acceptance criteria:** AC-OBJ-03.1: It drags like a block and never exits; each gate pass lowers the number, booster removals do not. AC-OBJ-03.2: At 0 the hidden block appears where the crate is; Hammer lowers the number by 1; Restart returns the crate to its authored cells.
- **Edge cases and boosters:** the count reaches 0 during a drag (the drag ends; the block appears at the crate's logical cells).

### OBJ-04 Flower Crate (L)

- **Official rule:** the crate moves freely; clearing the glowing leaves connected to it unlocks it and reveals what is inside.
- **Slice rule:** a Movable object linked to leaf blocks (DEC-MECH-21). When every leaf has been cleared (any cause) it disappears and reveals its contents at its current cells.
- **Data and editor:** `flower-crate`, `leaves` (block ids); contents use `contained`. Link tool picks leaves. Errors: no leaves; a leaf inside this crate.
- **View and assets:** OBJ-03 crate with a green tint; leaves glow through a property-block emission boost (no leaf art). **Solver:** position-driven plus a dependency set.
- **Acceptance criteria:** AC-OBJ-04.1: It drags like a block and never exits; after all leaves clear, its contents appear at its current position. AC-OBJ-04.2: Hammer lowers the remaining leaf requirement by 1; a vacuumed leaf counts as cleared.
- **Edge cases and boosters:** a leaf revealed later from another crate (allowed).

### OBJ-05 Barrier (S)

- **Official rule:** barriers open and close after each block you clear.
- **Slice rule:** closed cells are walls, open cells floor; the state starts at `startsOpen` and flips on each `BlockCleared`. A barrier that closes under a block does not push it: the block may leave, and nothing may enter until it opens (DEC-MECH-18).
- **Data and editor:** `barrier`, `startsOpen`. Barrier brush; the clear preview flips it.
- **View and assets:** `Wall` pieces raised from the floor when closed, sunk when open (tween per piece). **Solver:** parity.
- **Acceptance criteria:** AC-OBJ-05.1: A closed barrier stops every block; after one clear blocks pass. AC-OBJ-05.2: A barrier closing under a block does not move it; the block can leave, others cannot enter.
- **Edge cases and boosters:** a Vacuum of 2 blocks flips it twice; a closed barrier on a laser beam blocks it.

### OBJ-06 Colorful Crate (S)

- **Official rule:** the crate clears once every block of its color has left the board.
- **Slice rule:** crate cells are walls. After each `BlockCleared` it checks a per-color remaining counter (hidden and contained blocks included); at 0 it disappears. No contents.
- **Data and editor:** `colorful-crate`, `colorId`. Warning: no block of that color exists (it vanishes at start).
- **View and assets:** composed crate in the palette color at low saturation plus a color stripe tile. **Solver:** from per-color counts.
- **Acceptance criteria:** AC-OBJ-06.1: It stops every block while blocks of its color remain and disappears when the last one clears. AC-OBJ-06.2: A Vacuum of its color clears it at once; hidden blocks of its color keep it in place.
- **Edge cases and boosters:** color-changing blocks move counts between colors (updated on `ColorChanged`). Not a Hammer target.

### OBJ-07 Colorful Moving Path (XL)

- **Official rule:** these paths move when blocks slide onto them, shifting the layout.
- **Slice rule (provisional, DEC-MECH-19):** a straight track (ordered cells) with one colored platform cell that follows OBJ-01. When a path-color block moves fully onto the platform in the track direction, platform and block slide together to the track end or the first blocked cell. Verify against gameplay footage before implementation.
- **Data and editor:** `colorful-moving-path`, `colorId`, `platformIndex`; cell order is the track. Track tool; error when the track is not straight.
- **View and assets:** dim tiles with `Arrow_1` markers, a bright platform tile; one tween moves platform and block roots. **Solver:** changes the board graph; the most expensive mechanic for a search.
- **Acceptance criteria:** AC-OBJ-07.1: A matching block moved onto the platform slides with it and stops at the first blocked track cell. AC-OBJ-07.2: A non-matching block cannot enter the platform; a block only partly on it does not trigger a slide.
- **Edge cases and boosters:** a slide ending beside a matching door (no exit until pushed). Not a Hammer target.

### OBJ-08 Ivy (S)

- **Official rule:** ivy blocks parts of the board and shrinks by one tile each time a block passes through a gate.
- **Slice rule:** ivy cells are walls. After each `GatePassed` the last listed cell is removed. Hammer removes one tile.
- **Data and editor:** `ivy` (no params); cell order is the retract order. Ivy brush with numbered, reorderable badges.
- **View and assets:** low block pieces tinted dark green per cell; removal scales a tile down and returns it. **Solver:** remaining = cells minus passes.
- **Acceptance criteria:** AC-OBJ-08.1: Ivy stops every block; each gate pass removes the last listed tile; booster removals do not. AC-OBJ-08.2: Hammer removes one tile; Restart restores all tiles in order.
- **Edge cases and boosters:** ivy on a laser beam blocks it until it retracts past the beam.

### OBJ-09 Elevator (L)

- **Official rule:** the elevator holds a set number of blocks; one starts on top, and removing it brings up the next.
- **Slice rule:** stacked blocks carry `contained` with the elevator id and `stackIndex` (0 on top, on the board at start). When no block occupies any elevator cell, the next block rises into its authored cells (DEC-MECH-22); while any cell is covered, the rise waits. An empty elevator is floor.
- **Data and editor:** `elevator` (no params). Elevator tool paints the footprint; a stack list with color, shape, and reorder. Error: a stacked block outside the footprint.
- **View and assets:** flat platform tiles and a count of remaining blocks; the rising block view scales up from the floor. **Solver:** position-driven.
- **Acceptance criteria:** AC-OBJ-09.1: At start only the top block is on the board; when it moves off or clears, the next rises; the rise waits while another block covers a cell. AC-OBJ-09.2: `Won` requires every stacked block to clear; Restart restores the stack.
- **Edge cases and boosters:** a player parks a block on the elevator to hold the stack down; Vacuum does not remove blocks that have not risen.

## 6. Interaction Matrix

Only pairs that change a rule are listed. Every other pair follows the two entries' own rules. Each row needs one EditMode test when both interacting items are implemented. Hammer interactions must follow the one-exposed-piece-or-obstacle rule when selected.

| Pair | Rule | Ref |
| --- | --- | --- |
| Ice (BLK-03) + Vacuum (BST-4) | Iced blocks of the chosen color are removed regardless of count. Their removal does not lower other Ice counters. | Main PRD DEC-79 |
| Ice + Hammer (BST-2) | Active Ice shell removed, counter set to 0, underlying block stays; one hit has one effect. | DEC-BST-9 |
| Ice + Rocket (BST-3) | One valid cell removed, count unchanged, block stays frozen. A 1x1 iced block is ineligible. | DEC-BST-9 |
| Arrow (BLK-01) + Hammer | An ordinary single-layer Arrow block is removed entirely. | DEC-BST-3 |
| `BlockCleared` counter or toggle + booster removal | Counts as a clear for Chained keys, Turn Based Arrow, Iced Door, Crate, Barrier, Toggle Door, Moving Lock and Colorful Jumping Door when each is implemented. Ice counts door exits only. | DEC-MECH-2; main PRD DEC-79 |
| Arrow (BLK-01) + Rocket | Axis kept; the arrow mesh re-picks `Arrow_1` to `Arrow_3` from the new run and re-centers. | AC-BST-3.5 |
| Arrow + Ice | Ice resolves first, then the axis applies. | BLK-01 |
| Bomb (BLK-07) + Time Freeze (BST-1) | Decide whether freeze holds Bomb's separate countdown before implementing Bomb. Time Freeze holds the main level timer during its own 10-to-0 countdown. | DEC-MECH-5 |
| Dynamite (BLK-12) + Time Freeze | No effect: dynamite counts moves. Booster uses are not moves. | DEC-MECH-5 |
| Bomb or Dynamite + removal boosters | Removed safely, no explosion. Rocket on a larger bomb keeps its countdown. | BLK-07 |
| Time Capsule (BLK-14) + removal boosters | No bonus; the bonus needs a gate pass. | BLK-14 |
| Time Capsule + Time Freeze | The capsule's own bonus may add time on a gate pass; Time Freeze holds the main timer during its separate active countdown. | BLK-14 |
| Layer (BLK-02) + Hammer / Rocket / Vacuum | Hammer peels one layer; Rocket removes the cell from every shell; Vacuum matches the active color and removes the whole block. | BLK-02 |
| Combined (BLK-05) + any booster | Removing one member dissolves the group; Rocket dissolves it when no shared edge remains. | BLK-05 |
| Chained keys, scissors (BLK-06, BLK-08) + removal boosters | The key or scissors is still delivered, so a booster never strands a level. | DEC-MECH-2, DEC-MECH-9 |
| Hidden (BLK-11) + boosters | Concealed blocks cannot be targeted and Vacuum skips them; a revealer removed by a booster reveals. | DEC-MECH-14 |
| Moving Blocks (BLK-09) + Vacuum / Hammer | Neutral cannot be chosen; Hammer removes it. | DEC-MECH-8 |
| Color-Switching (BLK-15) + Vacuum | Matches the active outer color; removes the block. | BLK-15 |
| Color Swapping (BLK-16) + Layer, Color-Switching, Colorful Crate | Layers and inner/outer colors swap too; crate color counters update on `ColorChanged`. | BLK-16, OBJ-06 |
| Magnet (BLK-18) + Rocket | Fused block loses one valid cell and recomposes. | BLK-18 |
| Rocket + any door | A narrower block may fit a door it did not fit before; no automatic exit. | AC-BST-3.4 |
| Toggle Door, Barrier (GTE-01, OBJ-05) + Vacuum | Flip once per removed block (N removals, N flips). | DEC-MECH-3 |
| Color-Switcher, Jumping Door, Moveable Crate, Ivy + Vacuum | No change; they react to gate passes only. | DEC-MECH-2 |
| Several effects on one door (for example Moving Lock + Iced Door) | Every effect must allow the pass; counters advance independently. | Section 4 |
| Laser (GTE-11) + Ivy, Barrier, Crate | Wall-role cells on the beam keep the door closed. | GTE-11 |
| Button (GTE-10) + removal boosters | Removing the pressing block closes the door. | GTE-10 |
| Contained blocks (OBJ-02, -03, -04, -09) + boosters | Not visible, not targeted, not removed. | DEC-MECH-14 |

## 7. Implementation Dependencies and Effort

The user selects the next area or mechanic. The shared JSON envelope, strict effect registry, board event dispatch, block and edge composers, pooled views and unknown-type checks are prerequisites when the selected feature needs them. Review the selected entry's explicit dependencies before implementation. A new mechanic includes its data validation, editor controls, visuals, solver declaration and focused tests. Estimates in the catalog express relative complexity, not a schedule or priority.

## 8. Decisions and Open Questions

### 8.1 Booster Decisions

| ID | Decision | Reason |
| --- | --- | --- |
| DEC-BST-1 | Rocket removes one valid cell and recomposes the block. The in-level control uses the tapped cell; pre-level activation chooses a valid starting cell randomly. | One effect with two target sources, distinct from Hammer and Rollic's whole-block description. |
| DEC-BST-2 | Rocket refuses a cell whose removal would split or empty the block (charge kept); 1x1 blocks are ineligible in both contexts. | Keeps a nonempty connected block after a one-cell result. |
| DEC-BST-3 | Hammer removes one semantic layer or block piece: an ordinary block, a layered block's exposed layer, or an active Ice shell. It does not geometrically split blocks. | The user's later clarification supersedes the earlier one-cell Hammer interpretation; Rocket owns one-cell damage. |
| DEC-BST-4 | Time Freeze holds the main timer while a separate 10-to-0 countdown runs in `Playing`; Shader Graph frost and a UI Toolkit indicator follow the separate countdown. Pause preserves both values, and reactivation is refused. | The main timer resumes from its held value when the separate countdown expires; it receives no additive bonus. |
| DEC-BST-5 | Code name Vacuum, display "Color Vacuum"; the color comes from the tapped block. | Same flow as other targeted boosters, no palette UI, only present colors are choosable. |
| DEC-BST-6 | Seed one charge per available definition once, then persist stock across contexts and attempts; both Rocket entry points share it. | Selection spends nothing; valid application spends one, with no Restart or Next Level refund. |
| DEC-BST-7 | Booster removal may satisfy the win condition. It does not advance Ice; other mechanics may subscribe to broader clear events. | Keeps one terminal check and the main PRD's door-exit Ice rule. |
| DEC-BST-8 | Targeting itself does not pause the timer; an active Time Freeze still suppresses it. | Targeting must not become a free pause outside the state table. |
| DEC-BST-9 | Hammer removes an active Ice shell entirely, sets its counter to zero, and leaves the block. Rocket keeps the Ice count after valid cell removal; Vacuum removes iced blocks. | One Hammer hit has one semantic effect. |
| DEC-BST-10 | Selection, cancellation, and refused uses spend nothing; valid application spends once. Buy atomically spends 10 coins and grants one charge by tuning default, guarded against failed persistence and double taps. | Stock is persistent; buying neither casts nor selects. In-level purchase keeps `Paused` until close; pre-level purchase stays on `Home`. |
| DEC-BST-11 | Four in-level placements and two pre-level placements use one Booster category. | Rocket is shared; Extra Time is the fifth unique effect. |
| DEC-BST-12 | Pools warm for expected Rocket cell changes and Hammer block, layer, and Ice-shell effects. | Rocket may add edge or corner pieces; Hammer does not create split block views. |

### 8.2 Mechanics Decisions

| ID | Decision | Reason |
| --- | --- | --- |
| DEC-MECH-1 | Preserve `formatVersion: 1` and `{type, params:[{key,value}]}` string pairs; the research drafts' integer ids are normalized to string type ids. Newtonsoft and an explicit registered converter per shipped type parse typed domain parameters. | Unknown types and keys fail validation; `TypeNameHandling.None` and IL2CPP `link.xml` preserve the existing data contract. |
| DEC-MECH-2 | Mechanics using "removed/clear" subscribe to `BlockCleared` (exit, peel, member exit, booster); mechanics using "passes through a gate" subscribe to `GatePassed`. Ice listens only to `BlockExited`. | Event choice follows each mechanic's stated trigger. |
| DEC-MECH-3 | The causing clear commits first; listeners then react in fixed order: blocks, doors, objects, each by id. A multi-block Vacuum raises events in ascending block id. | Deterministic and testable; no time-based deduplication. |
| DEC-MECH-4 | Arrow is an axis, both ways. | Supplied arrow meshes are double-headed; simpler to author and solve. |
| DEC-MECH-5 | Bomb counts seconds; Dynamite counts moves. Decide Bomb's interaction with Time Freeze before implementing it; Time Freeze holds the main level timer during its own 10-to-0 countdown. | Timer behavior must remain consistent across mechanics. |
| DEC-MECH-6 | Hidden blocks are concealed and occupy their cells. | No cell conflict at reveal; fair and deterministic. |
| DEC-MECH-7 | Color-Switching reacts to other blocks' gate passes and cannot exit while two-colored. | Closest reading; the alternative duplicates Layer. |
| DEC-MECH-8 | Moving blocks use a reserved `neutral` color, never offered for doors. | "Never exits" becomes a data fact the validator checks. |
| DEC-MECH-9 | Chain keys go to the smallest remaining count, then lowest id; scissors to the lowest id; Locked Door keys name their door. | Deterministic, no static manager, the dependency is visible to designers. |
| DEC-MECH-10 | A magnet fuses with a same-color block moved edge-adjacent. | Matches "connect when aligned"; reuses the composer. |
| DEC-MECH-11 | Combined Locked members separate after unlocking. | Rollic: the pair "must be separated". |
| DEC-MECH-12 | An `Unsupported` mechanic gives "Not verified", never "Solvable". | A false "solvable" is worse than no answer. |
| DEC-MECH-13 | A Colorful Door keeps its last color. | Never strands blocks. |
| DEC-MECH-14 | Boosters act only on visible blocks. | The player cannot see or choose hidden blocks. |
| DEC-MECH-15 | The Jumping Single Door blocker wraps inside its span. | Simplest reading of "moves along the gate". |
| DEC-MECH-16 | The Colorful Jumping Door recolor is temporary. | Derivable from the clear count. |
| DEC-MECH-17 | The Moving Door Lock moves per `BlockCleared`. | Rollic names no trigger. |
| DEC-MECH-18 | A barrier closing under a block leaves it in place. | No forced movement, no overlap. |
| DEC-MECH-19 | The Colorful Moving Path model is provisional. | Least specified entry; verify before building. |
| DEC-MECH-20 | A block recolored on a Colorful Path may stay and leave only. | No forced movement. |
| DEC-MECH-21 | A Flower Crate leaf is a linked block with a glow. | No new object type, no leaf art. |
| DEC-MECH-22 | The Elevator raises the next block only when its cells are free. | A rise into occupied cells would overlap blocks. |
| DEC-MECH-23 | Crates open on a count; containment is a `contained` block effect. | Matches the Iced Door model; keeps the envelope closed. |
| DEC-MECH-24 | Define Hammer targeting for a selected door or object mechanic before implementing that interaction; preserve its one-exposed-piece-or-obstacle rule. | Target acquisition and removal must stay consistent with the booster rule. |
| DEC-MECH-25 | A blocker on a group member locks the group; drag constraints are fixed at drag start. | No mid-drag rule changes. |
| DEC-MECH-26 | Star applies to every exit and peel. | One consistent rule. |
| DEC-MECH-27 | The user selects the next mechanic or area; the selected entry's dependencies and verification define its task. | No fixed mechanic order is implied by this catalog. |
| DEC-MECH-28 | Review integration dependencies before each selected mechanic. | Counter, group, spawning and merge behaviors have different prerequisite hooks. |
| DEC-MECH-29 | Rollic's "screwed" state is out of scope. | Rollic defines it nowhere. |
| DEC-MECH-30 | The build registers only implemented and verified types; an unregistered type fails clearly on load. | No incomplete mechanic reaches a player. |

### 8.3 Open Questions

- OQ-M1: Verify OBJ-07, GTE-09, OBJ-04 and BLK-11 against gameplay footage when each is selected.
- OQ-M2: Does the `Key 1` geometry in `WallAndDoor.fbx` import cleanly as a key visual?
- OQ-M3: Confirm in the Editor that `Arrow_1` to `Arrow_3` mean 1 to 3 cells and that `ModularBlock_Base` belongs inside the center piece prefab.
- OQ-M4: Hammer on doors and objects needs a door and object tap target when that interaction is selected.
- OQ-M5: Revalidate selected mechanic interactions where they differ from the specified Ice and booster rules.

### 8.4 Revision Note (2026-09-24)

- Kept `formatVersion: 1` and string-pair effect params while selecting Newtonsoft with explicit registered converters, strict keys/types, `TypeNameHandling.None`, and IL2CPP preservation.
- Aligned all projected counters and badges with UI Toolkit and scene-scoped disposable board pools.
- Recorded the separate Time Freeze countdown, durable booster use through PRD 7.6, and the bounded visual Finish after `Won`. The user selects further mechanic implementations and their order.
