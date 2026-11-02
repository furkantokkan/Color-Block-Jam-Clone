# Architecture review — 2026-09-26

Review scope: project-owned Domain, Gameplay, Runtime, Presentation,
Environment and Editor code. The review used the current Plastic workspace
(`cs:10`) with existing local changes; it is not a clean-revision audit.

## Corrected session ownership

- `Gameplay/Session/LevelSession.cs`: terminal state observers now run after
  input/countdown stop and Finish setup or Teardown. A synchronous Won → Home
  callback cannot resume the old transition over a released attempt; a Failed →
  Restart callback keeps the new attempt.
- Every public operation refuses work after session disposal. Late ticks,
  background callbacks and Finish callbacks do nothing; no new attempt is created.
- The transition order and disposal policy are explicit in the class comments
  and in `Gameplay-Drag-And-Gate.md`. No public signatures or assembly boundaries
  changed.
- Seven new `LevelSessionTests` cases failed before the fix. Independent final
  Gameplay EditMode verification passed **282/282**, including architecture and
  zero-allocation checks (job `4c0a6d4d772c47dba919266488eef498`).
- Original task: `TASK-20260925-012336316-a88e25db-block-drag-gate-exit`,
  defects D-001–D-003. Its existing live-scene acceptance requirement remains open.

## Project load ownership

- Confirmed defect: without a shape library, the project installer passes the
  Onity build token directly to the level load, but the installed context starts
  that build without a cancellation token. Destroying the installer can leave
  its load running.
- `Runtime/Composition/ProjectScopeInstaller.cs` now captures the installer's
  destruction token before registering the build callback. It checks owner/build
  cancellation before resolving services, links both tokens with the optional
  cache lifetime, and uses one database-loading path with or without a catalog.
- Three new regressions in `Tests/Gameplay/PlayMode/ProjectScopeShapeCatalogTests.cs`
  failed before the fix: destruction before build with/without a shape library,
  and destruction during an unfinished cache read while the container stays alive.
  All four fixture cases passed in the final independent PlayMode run.
- Original task: `TASK-20260925-005313341-47831bb7-runtime-shapes-from-json`,
  defect D-001. Tests use temporary in-memory objects and an in-memory cache;
  direct Addressables handle release was not separately instrumented.
- The existing runtime-shapes task still requires separate approval for its
  `ProjectContext.prefab` reference and matching `GameAttemptLoader` integration.
  This review does not treat that known integration gap as a completed feature.

## Scoped database persistence

- `Editor/Levels/LevelDatabaseAddressables.cs` now saves only the database,
  settings, groups and new or changed schemas owned by an operation. It avoids
  both direct global saves and vendor convenience methods that save globally.
  Missing global settings return an actionable error without bootstrapping them.
- The 13 scoped-save cases preserve an unrelated dirty sentinel's disk bytes and
  dirty state, verify source-group moves/deletion on disk, and clean their exact
  disposable scratch directory. Temporary settings and subassets isolate the
  Addressables import callbacks from the project's real settings.
- Early verification caught a fixture isolation failure despite passing test
  assertions. Four real Addressables files were restored to their captured
  SHA-256 baselines before verification resumed. All 81 protected authored files
  subsequently remained byte-identical. Details are retained as D-001 in the
  Editor architecture task, rather than treating the initial run as a pass.
- Genuinely persisted missing-group/schema creation has static coverage against
  Addressables 1.22.3 only. The tests exercise temporary schema creation and
  persisted group membership. The guarantee assumes the current valid project
  settings; it does not claim recovery from arbitrary corrupted settings or
  rollback after a mid-creation I/O exception. Serialized vendor property names
  must be reviewed if Addressables is upgraded.

## Level Editor responsibility split

- `LevelDraftController` owns the transient draft, its binding object, saved and
  synchronized snapshots, Undo operations and reload recovery. `LevelDraft`
  remains the single Undo target with unchanged serialized fields.
- `LevelPieceEditController` owns catalogs, pending-piece operations, Pen and
  mirror identities, and piece edit planning. It returns typed edits without
  UI controls or event subscriptions. The serialized `LevelPieceSession` is
  borrowed by the controller, with no second state copy in the window.
- The window retains controls, dialogs, selection, preview publishing and Unity
  subscription lifetime. Catalog refresh reaches the view before a refused
  piece edit can consume its invalidation; three regressions cover that seam.
- `LevelEditorTestAccess` centralizes private fixture access to each real owner.
  Existing interaction assertions remain intact. Direct controller tests cover
  ownership and failure paths, in addition to the window regressions.
- Draft extraction passed 52/52 cases. The subsequent 54-fixture Editor run
  completed **744 passed, zero failed, one protective skip** (job
  `b8c8d16a7fbd4f6cbd359398d0ff6b03`). This includes seven direct draft cases,
  eight direct piece cases and three palette invalidation regressions.
- `LevelSaveController` owns source loading/validity, validation, backup reads
  and the full confirmed save sequence: atomic JSON write, pending-template
  transfer, family-name Undo, saved baseline, view refresh, template commit,
  then new-family commit. Typed results distinguish a failed write from a
  written file that could not reload, and a saved level from a library retry.
- The window decreased from 6,014 to 4,428 lines. Controllers remain internal
  to the existing assembly; public APIs, JSON formats and serialized draft
  fields are unchanged. No speculative service framework was added.
- Final independent verification covered 55 fixtures: **752 passed, zero
  failed, one protective skip**, including all 26 direct-controller cases
  (job `9d229f8aef3c4daa8738dc571b547e4f`). The retained NUnit XML is
  `production/tasks/TASK-20260926-204508638-a3ad6ee3-level-editor-architecture/final-tests.xml`.
  All 270 monitored source inputs remained unchanged during this run.
- An independent diff audit found 17 existing fixtures only migrated private
  access while preserving all 1,654 assertions; scoped-save coverage increased
  from 28 to 114 assertions. The final barrier-gate access route was separately
  reviewed with the same behavioral expectations.
- Tracked scope and evidence:
  `TASK-20260926-204508638-a3ad6ee3-level-editor-architecture`. Work proceeded
  sequentially after the preceding Editor owner released its paths.

## Verification limits

- Runtime independent verification: **282/282 EditMode + 19/19 PlayMode**, no
  failures or skips. PlayMode covers `FinishDeadlineTests`,
  `BoardPresenterLifecycleTests` and `ProjectScopeShapeCatalogTests`
  (job `b585f1d023844e079397a9c3ef300f00`).
- Assembly dependencies follow the documented inward direction. Domain remains
  free of Unity/Onity references, with a single gameplay tick owner.
- The unchanged Piece Editor focus-routing test skips when another Piece Editor
  is open, because running it would alter that user's window. The window remains
  open; this interaction has no live test proof in the current session.
- The Level Editor's full unsaved window, draft, template and colour-order state
  was compared before and after compilation and the affected tests. It stayed
  identical, all 81 protected authored files matched their baselines, and no
  test scratch directory remained.
- The existing listener-resilience test intentionally logs a caught exception;
  it passes. This is distinguished from compilation or unexpected Console errors.
- Concurrent arrow-marker work changes Canvas/preview files outside this task's
  ownership. Those changes are preserved and are not attributed to this review.
- No intentional scene, prefab, package, project setting or authored database
  change, commit or checkin is part of this review. No device build was run.
