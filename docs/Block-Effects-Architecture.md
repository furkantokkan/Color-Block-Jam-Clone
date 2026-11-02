# Block Effects Architecture

Status: accepted implementation design, revision 6 of 2026-09-27. The user directly confirmed that Ice builds the shared architecture first; revision 2 established the attempt-state and view lifecycle contracts. Revision 3 adds the user's centered, name-under canvas badge columns. Revisions 4–5 established shared preview/Game projection and initial frost calibration. Revision 6 follows the user's new paired-image reference: dense cyan Ice and a large centered outlined numeral in Preview/Game, replacing their blue badge. Module/view interfaces remain unchanged. Revision 7 (2026-09-28) records the Arrow step as implemented (DEC-158): the Arrow module, hooks and views below, the canvas overlay pass and the optional preview pre-build check.
It is written by session "Edit dropdown ve colors kareleri", owner of TASK-20260926-230921278-a3ffdc40 (Arrow gameplay), for:
- TASK-20260926-231349009-945d78fe (Ice, owner "Codex /root Ice visibility implementation"), which builds the shared parts first;
- the Arrow gameplay task, which plugs in afterwards.
The Ice owner maintains this document with the implemented contracts so the Arrow successor uses the same version.

## Goal

A block effect (Arrow, Ice, then Chained, Key, Layer and others) is added as one self-contained module: data reading, Domain hooks, game view and editor views. Adding an effect never edits `LevelSession`, `AttemptLevelConverter`, the movement and exit algorithms, `BoardPresenter`'s own logic, the hook source, or the canvas and preview hosts. This is the Open/Closed and Single Responsibility answer to the conflict of 2026-09-27, where two tasks edited the same presenter, installer and hook source for their own effect.

## Layers and ownership

1. **Authored data** (`ColorBlockJam.Runtime`). `LevelEffectRegistry` and the typed effects (`ArrowEffect`, `IceEffect`) stay the only parser. Each effect task flips its own `k_*RuntimeImplemented` flag after its rule and views pass verification.
2. **Effect context** (`ColorBlockJam.Gameplay/Effects`).
   - `BlockEffectContext` holds per-level typed metadata, indexed by the attempt's block index (the `AttemptLevel` order).
   - It is built once on Home at the composition boundary, `GameAttemptLoader.Load`, from the authored `LevelData` by the registered modules, before `session.TryPrepare`, and is tied to that exact `AttemptLevel` identity.
   - It is immutable for the level. Restart reuses it.
   - Nothing reads JSON or authored records during Playing.
   - One scene-scoped `BlockEffectContextSource` (bound in `GameSceneInstaller`) holds the current level context and completed attempt envelope. The hook source and the game views share it. `AttemptLevel` and `AttemptLevelConverter` stay effect-free.
3. **Modules** (`Gameplay/Effects`). `IBlockEffectModule` has one implementation per effect (`ArrowEffectModule`, `IceEffectModule`):
   - `Read` runs on Home and returns immutable `IBlockEffectLevel` metadata for that module, such as Arrow axes or Ice starting counts. Each module owns its typed data; there is no generic mutable builder or service bag.
   - `AddHooks` runs once per attempt, registers Domain hooks and returns a fresh `IBlockEffectState`, such as Ice counters reset to the starting counts. `EffectAttemptHookSource` constructs a `BlockEffectAttempt` with completed hooks and state slots, and publishes it only after every module succeeds. The immutable level context never holds changing counters.
   - `CreateView` returns the module's optional scene-lifetime `IBlockEffectView` using a `BlockEffectViewContext` (see 5).
   - `GameSceneInstaller` binds the modules as one ordered list. Blockers come first (Ice before Arrow: catalog "Arrow + Ice: Ice first"). A new effect adds one line there.
4. **Domain hooks** (`ColorBlockJam.Domain/Hooks`) are engine-free and take plain arrays at construction. The existing ports are `IAxisConstraint`, `IDragGate`, `ICellGate`, `IExitGate` and `IBoardEventListener`.
   - Arrow: `ArrowAxisConstraint` (the block's `AxisMask`) and `ArrowExitGate` (vetoes a door side perpendicular to the axis, so `ExitRules.Evaluate` returns `Vetoed` for every caller).
   - Ice: drag and exit gates refuse movement while the counter is above 0. Its `IBoardEventListener` lowers counters once per other block's real door leave, never for self events, ordinary moves, non-leave outcomes or booster removals.
   - Hooks allocate nothing per call.
   - `EffectAttemptHookSource : IAttemptHookSource` only iterates the modules. It holds no effect-specific code. Module, metadata, state and view order is fixed and validated on Home. A context prepared for a different attempt level is rejected.
   - While a new attempt is being created, no previous or partial envelope is exposed as current. A later module failure leaves no usable new attempt. Views bind only to the successfully published envelope for the exact level.
5. **Game views**.
   - The contract `IBlockEffectView` lives in `Gameplay/Board`, beside `IBoardPresenter`. `BoardPresenter` holds an ordered `IBlockEffectView[]` from the modules and calls it only at its existing lifecycle points:
     - `TryPrepare(context, metadata, out error)` on Home warms the pools and rejects missing assets.
     - `BindAttempt` in every `Place`, before attachment, reads the new attempt's state even when same-level `TryBuild` reused its pools.
     - `Attach(block, root)` in `Place`.
     - `Sync` in the existing view-sync phase reads counter changes and recycles completed thaw visuals.
     - `Reproject` after the initial runtime camera framing and after layout changes positions screen-space labels.
     - `SetClipPlane(block, plane, enabled)` wherever the block's clip plane is set: exit, and effects.
     - `Detach(block)` in `ReturnPiece`.
     - `SetPaused(bool)` in Suspend.
     - `StopTweens()` at the existing stop point, before pool return.
     - `ReturnAll()` / `Dispose()` in Teardown, level change and dispose.
   - Domain listeners change state in the command phase; presentation only reads it in the same frame's existing `Sync`. There are no Domain-to-view callbacks, circular context/view dependencies or new `Update` methods.
   - A same-level `TryBuild` reuses pools on Restart, while hook creation and `BindAttempt` supply fresh state. Every subscription, feedback/thaw tween and checked-out visual has an explicit teardown path.
   - Failed preparation/attachment releases resources acquired by that failed operation; no partial shell, label or callback remains. Preview operations that promise to retain the last valid display must restore it. Dispose is safe after partial initialization.
   - Each view wraps a Presentation owner:
     - Arrow: `ArrowEffectView` wraps `Presentation/Board/BlockArrowMarkers`. The Arrow gameplay task moves today's inline arrow code in `BoardPresenter` into this view.
     - Ice: its overlay and counter owners. Preview and Game share a large pale-cyan numeral with a dark outline and no chip background or snowflake. Center it on the projected footprint midpoint and size it from the projected grid cell, targeting a visible glyph height around 60% of a cell. Retain a continuous center for solid even-sized footprints and a deterministic occupied-cell fallback for concavities/holes. Verify two-digit fit and Arrow head readability. Runtime block-name labels are not introduced. Canvas badges retain the separate authoring layout below.
6. **Editor views**. `ColorBlockJam.Editor.LevelPreview` does not reference Gameplay, so the editor uses the Presentation owners directly through thin adapters:
   - **Canvas:** `LevelCanvasModel` keeps per-block typed effect data (read with the Runtime typed effects), and `LevelCanvasElement` draws an ordered list of `ILevelCanvasEffectLayer`s in its single `generateVisualContent` pass. Layers prepare cached data/labels on model changes, size them on layout changes, draw frost below foreground badges, and dispose their labels/callbacks. `GetBadge(block)` exposes an optional reusable badge to `LevelCanvasEffectBadgeLayout`; the host owns placement across effect layers in registration order. Its pointer-transparent UI Toolkit row contains columns: the first starts centered below the actual block name, badges stack downward, and a neighboring column starts when the available vertical area is exhausted. At very small zoom, if even the first badge cannot fit below the name, a side column uses the block's available height. Keep every badge visible rather than collapsing into +N. Existing IDs, Arrow and selection remain readable. Adding a later effect supplies its badge through this port instead of positioning another independent overlay.
   - **Preview:** `LevelPreviewHost` keeps an ordered list of `ILevelPreviewEffect` adapters that call the same Presentation owners as the game. Adapters prepare, attach, reproject explicitly after camera Frame, detach and dispose. The host itself holds no effect-specific branches once the Ice and Arrow adapters exist.
   - Ice installs these extension lists while retaining the current inline Arrow display. The subsequent Arrow task migrates that existing display; neither task creates a second Arrow renderer.
   - Arrow (done): `ArrowCanvasEffectLayer` draws the badge the canvas drew inline, unchanged in look. `LevelCanvasElement` keeps two ordered lists, underlays drawn just above the blocks (Ice frost) and overlays drawn above the grid (the Arrow badge, so a grid line cannot cross its shaft); registration order, Ice then Arrow, sets the badge order. `ArrowPreviewEffect` attaches the markers the host attached inline, through the same `BlockArrowMarkers`. An adapter may also implement `ILevelPreviewEffectCheck` (declared beside `LevelPreviewHost`), which the host runs after mapping and before any scene change, so a missing Arrow prefab still rejects the level with the last valid preview untouched. `LevelPreviewOwner` registers Ice then Arrow. Neither host has an effect-specific branch left.

## Rules kept

PROJECT.md Runtime Lifecycle and Performance Rules apply unchanged:
- one tick owner;
- pooled views warmed on Home;
- no Instantiate, JSON or material instance in Playing;
- zero allocation per frame;
- every subscription, tween and pool with an owner and a stop point;
- Suspend and Teardown reach every view.

The command log (DEC-122) and replay are unaffected: hooks are pure functions of the board and the attempt state.

## Sequence

1. **Ice** (TASK-…945d78fe, first, on the user's order of 2026-09-27):
   - builds `BlockEffectContext`/`Source`/`Attempt`, `IBlockEffectModule`/`Level`/`State`, `EffectAttemptHookSource`, `IBlockEffectView` and its `BoardPresenter` call points, the canvas layer list and the preview adapter list;
   - adds `IceEffectModule` and the Ice views;
   - keeps today's Arrow marker behaviour working as it is.
   - verifies generic module/hook composition without enabling the still-unsupported Arrow gameplay flag. Runtime Arrow+Ice acceptance belongs to the following Arrow task.
2. **Arrow gameplay** (TASK-…a3ffdc40, after the Ice lock is released):
   - adds `ArrowEffectModule`, `ArrowAxisConstraint`, `ArrowExitGate` and `ArrowEffectView`, moving the inline arrow marker code out of `BoardPresenter`;
   - adds the canvas and preview Arrow adapters, the Arrow runtime flag and the PRE-1 issue.
   Its first partial attempt (converter-based axes) is parked unchanged under `Library/ccgs/parked/a3ffdc40` and is reused only where it fits this architecture.
   - Status (revision 7, 2026-09-28): implemented. Files, under `Assets/ColorBlockJam`:
     - Domain: `Runtime/Domain/Hooks/ArrowAxisConstraint.cs`, `ArrowExitGate.cs`;
     - Gameplay: `Runtime/Gameplay/Effects/ArrowEffectModule.cs`, `ArrowEffectLevel.cs`, `ArrowEffectState.cs`, `ArrowEffectView.cs`; the inline marker code left `BoardPresenter`;
     - runtime gate: `k_arrowRuntimeImplemented = true` in `Runtime/Core/Levels/LevelEffectRegistry.cs`;
     - editor: `Editor/Core/Levels/Canvas/ArrowCanvasEffectLayer.cs`, `Editor/LevelPreview/ArrowPreviewEffect.cs`, and the PRE-1 warning in `Editor/Core/Levels/Canvas/LevelDraftIssues.cs`.

A later effect follows step 2's shape.

## Case and reference alignment

- The supplied Rollic case is `docs/Game-Developer-Case-2026.md`: keep Unity 2022.3.62f2, the supplied art, portrait 1080 x 1920 and 20:9 usability. Ice is optional in the brief and explicitly selected for this task; this change does not claim the full vertical slice is complete.
- Preserve the brief's data/logic/view separation, low coupling, extension seams and no unnecessary update allocations. Keep designer-authored JSON as the game's same data format; no second Ice schema or hand-edited level requirement.
- Follow PROJECT.md's Ice reference decision: one Shader Graph material using the supplied texture, Onity-pooled copies of composed pieces, cached per-block MPBs and projected UI Toolkit counts. Frozen blocks remain obstacles; a real other-block leave reduces the count, with fresh state on Restart.
- Rendering choice: retain the transparent blend for thaw and clipping, with dense cyan coverage while frozen as requested in the latest paired-image reference. The unchanged base color becomes visible during thaw. Expand the copied surfaces along their object-space normals instead of scaling/translating the complete shell: this keeps stud centers aligned and covers the exposed base-color crescents. The older rendering review's opaque template Ice is a visual reference for texture, highlights and smoothness, not a render-mode requirement. Use no scene-color/depth refraction or copied template framework. Do not claim GPU instancing from material count or a checkbox; inspect generated passes and record verification limits.
- Reference refinement: the user's 2026-09-27 paired-image reference (`codex-clipboard-7388f9e7-e578-4ae2-89bc-7a180255c529.png`) now governs Preview/Game frost and numerals, superseding the older blue-chip target. Match its dense cyan frozen body, pale stud/footprint rings, quiet surface variation and large outlined count. Use the supplied `icetexture.jpg` with planar sampling; keep its photographic facets subordinate to the cyan body. High steady-state coverage and restrained emission avoid a hologram appearance while the existing transparent blend retains thaw and clipping. Material calibration and the cached overlay MPB must agree; rebuild prepared shells after material calibration. Preview and Game use the same owners. Judge matched block crops and full views at 1080 × 1920 and 1080 × 2400, including counts 1/4/6/10/20 and narrow/tall blocks. Canvas continues to use the earlier snowflake/count badge example.
- The user's final close comparison preserves the same target: [current before-state](../production/tasks/TASK-20260926-231349009-945d78fe-ice-visibility/evidence/dense-reference-user-current.png) versus [reference target](../production/tasks/TASK-20260926-231349009-945d78fe-ice-visibility/evidence/dense-reference-target.png). Match the heavier numeral fill, soft broad pale-cyan stud rings and defined cyan lower rim; thin white outlines alone are insufficient. Preserve the already corrected footprint, count position and dense coverage.
- The active schema key is `clearsToThaw`; the older PRD's `exitsToThaw` snippet is historical. Visual reference 04 is Time Freeze screen-edge frost, not block Ice. Follow the Level Editor reference's pale-blue footprint tint and counter badge for canvas presentation.
- Verification includes both editor and game visuals, frozen input and command paths, layout/readability, pause/teardown and focused allocation evidence. Device frame-time, the complete five-level flow and delivery artifacts remain separately verified work.
- Full BLK-03 follow-ups remain explicit: PRE-2 solver validation, the Hammer interaction, and device/Frame Debugger performance evidence. The bounded Ice tests should exercise save/reopen through a disposable fixture without writing the user's authored database.

## Booster restrictions (DEC-171)

A module's attempt state may implement `IBlockEffectPresenceSource` and return an `IBlockEffectPresence` (Ice: `IceRules` while frozen; Arrow: `ArrowEffectPresence` by authored source). `EffectAttemptHookSource` passes every state to `BoosterRestrictionHooks`, which reads `BoosterRestrictionMatrix` by the state's `TypeId` and registers one `BoosterRestrictionGate` (XP-6). `BoosterTargets` reads it through `IBoardReader.AllowsBoosterTarget`. A later effect joins the matrix by implementing the presence source; neither the hook source nor the gate changes.
