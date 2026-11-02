# TASK-20260926-204508638-a3ad6ee3-level-editor-architecture — Clear Level Editor ownership and scoped persistence

## Identity

- Contract status: ready
- Waiting reason: none
- Action required: none
- Task ID: TASK-20260926-204508638-a3ad6ee3-level-editor-architecture
- Supersedes: none
- Title: Clear Level Editor ownership and scoped persistence
- Repository (absolute path): C:/Users/e-fur/Documents/Repos/ColorBlockJamCase2026
- Branch/worktree or Plastic workspace: /main@ColorBlockJamCase2026/ColorBlockJamCase2026@efurk64@unity
- Base revision: cs:10 with existing local changes; planning provenance only
- Task kind: refactor
- Area: Level Editor authoring window and database persistence
- Workflow: direct

## Objective and baseline

- Observable objective: The existing Level Editor keeps its authoring behavior while draft ownership, piece editing and save sequencing become readable internal responsibilities, and its database actions save only the assets they own.
- Current behavior/evidence:
  - User request: "kodları incele ve gördüğün mimari hatalarını düzelt ve human readable olduğundan emin ol." The user subsequently approved completing the remaining Editor plan and said "bitirene kadar devam et". The coordinating root retains that execution authorization; it is not closure permission.
  - `docs/Architecture-Review-2026-09-26.md` records the review and proposed three controller extractions. `LevelDatabaseWindow.cs` currently combines controls, input, draft lifetime, undo, reload recovery, piece editing and persistence in more than 5,000 lines. Its predecessor changes, including template deletion and colour switching, have now landed and must be preserved.
  - `LevelDatabaseAddressables.cs` calls global `AssetDatabase.SaveAssets()` at reviewed lines 51, 128, 215 and 259. A database action can therefore persist unrelated dirty assets. Reviewed SHA-256: `46b1e8fbd6e6bb7e0e52575b4e77988bff30614133e6dab6ff1a23e0e3272aaa`. `LevelDatabaseAddressablesTests.cs` teardown also calls it; reviewed SHA-256: `c25260f9009e341b1330ef2e6846d2b73c0e1f8cbf2cbdfe93437705dc98148e`.
  - Read-only `task-bug` resolution found no existing task owning this scoped-save failure. The remote-level-json task remains an unimplemented remote-delivery plan, while the Inspector, shell and settings-registration tasks preserved or explicitly forbade changing this existing helper. Path overlap is not a same-task defect relation.
- Expected behavior:
  1. Replace global asset saves in scoped database actions with persistence of only the database/settings/group/schema objects changed by that action. Public signatures remain unchanged. Internal settings-parameter overloads may isolate tests from the project's real Addressables settings. The helper must not bootstrap missing global Addressables settings: return an actionable missing-settings error with zero writes, leaving configuration to project setup. Avoid vendor convenience paths that call global SaveAssets internally, including GetSettings(true) and persisted schema creation; an owned missing group may use the public AddressableAssetGroupSchemaSet.AddSchema API followed by scoped saves.
  2. Extract `LevelDraftController` first: draft creation/disposal, baseline and synchronized snapshots, dirty comparison and reload restoration. `LevelDraft` remains the single Undo target with unchanged serialized fields. The window owns Unity subscriptions and forwards changes to controls.
  3. Extract `LevelPieceEditController` second: pen/template/custom/named-piece edits and Piece Editor synchronization, reusing existing edit commands, shape plans and `LevelPieceSession`. Keep one owner for mutable authoring state rather than duplicate window/controller copies.
  4. Extract `LevelSaveController` third: validation and the existing save sequence, using existing file/library helpers and injected test seams. Keep atomic level JSON write before pending-template transfer, template library commit and new-family commit. Preserve the completed template-delete flow and its save/baseline integration.
  5. Keep the controllers internal to the existing Editor assembly. Co-locate small necessary internal result/helper types in the owned controller files. Use explicit methods and names; do not hide the extraction behind a new service framework, broad forwarding facade or partial-class split.
  6. Centralize window test access in `LevelEditorTestAccess.cs` and add direct controller tests for ownership and failure paths. Existing assertions continue to express the same behavior; adapting a private access route must not weaken them.
- Behavior that must remain unchanged: Public APIs, assembly boundaries, JSON formats and active-database authority; serialized draft fields; UI layout, labels, tooltips and shortcuts; selection, preview publishing and Effect/Piece Editor links; one Undo step per accepted edit and Undo history after Save; dirty markers and close prompts; domain-reload draft recovery and mismatch rules; named/custom/template piece behavior; template-delete choices and emptied-level handling; stale-file refusal, backup/restore, validation, save order and retry semantics. The approved exception is that missing global Addressables settings now produce an actionable error without writes instead of implicit configuration bootstrap; normal configured behavior remains unchanged. Runtime gameplay and authored project data are outside this task.

## Ownership

- Owner/writer role: The coordinating root is the sole `implement-task` lifecycle owner. It may delegate sequential source work to one scoped implementer at a time, followed by an independent verifier; no parallel writers to the same workspace paths. This creation agent writes only this new task artifact.
- Allowed paths (exact or narrow globs):
  - Assets/ColorBlockJam/Editor/Levels/LevelDatabaseAddressables.cs
  - Assets/ColorBlockJam/Editor/Levels/LevelDatabaseWindow.cs
  - Assets/ColorBlockJam/Editor/Levels/LevelDraftController.cs
  - Assets/ColorBlockJam/Editor/Levels/LevelDraftController.cs.meta
  - Assets/ColorBlockJam/Editor/Levels/LevelPieceEditController.cs
  - Assets/ColorBlockJam/Editor/Levels/LevelPieceEditController.cs.meta
  - Assets/ColorBlockJam/Editor/Levels/LevelSaveController.cs
  - Assets/ColorBlockJam/Editor/Levels/LevelSaveController.cs.meta
  - Assets/ColorBlockJam/Tests/EditMode/LevelEditorTestAccess.cs
  - Assets/ColorBlockJam/Tests/EditMode/LevelEditorTestAccess.cs.meta
  - Assets/ColorBlockJam/Tests/EditMode/Level*Tests.cs
  - Assets/ColorBlockJam/Tests/EditMode/Level*Tests.cs.meta
  - Assets/ColorBlockJam/Tests/EditMode/DatabaseControlsTests.cs
  - Assets/ColorBlockJam/Tests/EditMode/PieceEditorLinkTests.cs
  - Assets/ColorBlockJam/Data/Levels/__ScopedSaveTests_*/**
  - Assets/ColorBlockJam/Data/Levels/__ScopedSaveTests_*.meta
  - docs/Architecture-Review-2026-09-26.md
  - production/tasks/TASK-20260926-204508638-a3ad6ee3-level-editor-architecture/**
- Forbidden paths/actions:
  - Packages/**
  - ProjectSettings/**
  - Assets/ColorBlockJam/Runtime/**
  - Assets/ColorBlockJam/Domain/**
  - Assets/ColorBlockJam/Gameplay/**
  - Assets/ColorBlockJam/Presentation/**
  - Assets/ColorBlockJam/Environment/**
  - Assets/ColorBlockJam/Scenes/**
  - Assets/ColorBlockJam/Prefabs/**
  - Assets/ColorBlockJam/Art/**
  - Assets/ColorBlockJam/Editor/BoardKit/**
  - Assets/ColorBlockJam/Editor/LevelPreview/**
  - Assets/ColorBlockJam/Editor/Levels/LevelDraft.cs
  - Assets/ColorBlockJam/Editor/Levels/Canvas/**
  - Assets/ColorBlockJam/Editor/ColorBlockJam.Editor.asmdef
  - Assets/AddressableAssetsData/**
  - Assets/ColorBlockJam/Data/** except the newly created, test-owned __ScopedSaveTests_* scratch folder and its own metadata listed above
  - Any other source or documentation file not listed as allowed
  - New public APIs, new assemblies, new packages, schema changes or serialized-field migrations
  - Weakening existing behavioral assertions to make a refactor pass
  - Saving authored assets, the real Addressables settings/groups or the user's unsaved scene/draft during verification
  - Plastic checkin, shelve, publish, git operations, or task closure
- Serialized assets: none (no persistent authored asset ownership). Verification alone may create a unique `Assets/ColorBlockJam/Data/Levels/__ScopedSaveTests_<guid>/` folder containing test-owned JSON, settings, groups, schemas and sentinel assets. The scoped verifier is its sole writer and owns generated metadata. Use isolated settings-parameter overloads, never the real default settings or database. Capture prior state as needed, clean only the exact created folder, and restore state on every success/failure path. Existing window fixtures keep their transient objects, injected write delegates and live-file hash safeguards. New script metadata belongs to the source writer through Unity's normal import.
- Other active writers/worktrees checked: At 2026-09-26T20:42:13Z, `Library/ccgs/task-lifecycle-locks` had no `.claim` or live `.lock` files, only a historical runtime-shapes audit. Template-delete completed verification and released its claim; colour-order and pen-colour-key claims were released during inspection and their contracts record stable 690/690 Level tests and predecessor PATHS FREE signals. Root subsequently revalidated zero claims, the exact idle Editor and no project-authored dirty assets. Preserve the existing local changes and pending user-acceptance tasks. Recheck immediately before acquiring execution ownership.
- Execution dependency: none; predecessor source work is released. A newly appearing overlapping live claim queues this already-authorized task behind its exact owner until stable handoff and release.
- Queue policy: none

## Acceptance scenarios

1. Given unrelated dirty assets, when selecting, registering, creating or deleting a level database through the scoped helper, then only the operation's owned assets are saved; unrelated disk bytes and dirty state stay unchanged. The intended database/settings/group/schema changes still persist, including an owned missing-group creation. Given missing global Addressables settings, the operation returns an actionable error and writes nothing instead of bootstrapping configuration. Verification uses only isolated scratch assets and cleans them.
2. Given edits to a level, when Undo/Redo, Save, close/discard or a domain reload occurs, then the extracted draft controller retains today's single Undo target, dirty baseline, field values, recovery/mismatch rules and preview notifications; disposal leaves no scheduled publish or retained draft owned by the closed window.
3. Given pen, template, custom or named pieces, when editing cells, changing selection or synchronizing with the Piece Editor, then the extracted piece controller produces the same accepted edits, refusals, one-step Undo, re-stamping and selection behavior. Template deletion preserves Make Custom, Delete Blocks, Cancel, emptied-level handling and pending-template refusals.
4. Given a valid or invalid draft and possible stale disk, file-write failure or library-commit failure, when Save/Reload/Restore/Create/Delete runs through the extracted save sequence, then today's validation, conflicts, backups, write-before-library order, pending retries, no-double-commit behavior and draft history remain correct. A failed write does not report a successful commit.
5. Given the resulting source, when reviewed, then the window owns controls and Unity subscription lifetime while the three internal controllers own their stated work. Their mutable state has one owner; controllers use existing commands/helpers, preserve public and serialized contracts, and have clear direct names and readable control flow.
6. Given the updated tests, when the focused direct-controller and scoped-save fixtures and affected Level/Piece Editor window suites run in the pinned Editor, then they pass with unchanged behavioral assertions, clean compilation and no new Console errors. Test-access migration is centralized; no authored asset bytes or user dirty state change.
7. Given the completed implementation, when the review note and allowed diff are inspected, then the note records actual changes, verification and remaining limitations, the owned source/test/meta files are accounted for, and no forbidden project files or persistent authored assets changed because of this task.

## References and constraints

- Story/GDD/ADR/design/log/image: The user's original architecture/readability request and subsequent "bitirene kadar devam et" approval; `AGENTS.md`; `PROJECT.md`; `docs/Architecture-Review-2026-09-26.md`; `docs/Level-Editor-Design.md`; read-only task-bug resolution; completed template-delete/colour-order/pen-colour-key contracts as preservation references.
- Unity version/platform: Unity 2022.3.62f2; Editor authoring only. Unity CLI 1.0.0-beta.11 is mandatory; the documented pre-Unity-6 exception uses pinned MCP for Unity for live Editor verification.
- Package/architecture constraints: Existing `ColorBlockJam.Editor` and EditMode test assemblies; existing UI Toolkit, Newtonsoft, Addressables, Onity and board authoring APIs. No dependency additions. Keep `m_`, `s_`, `k_` naming and explicit ownership comments for scheduled work. No manual Unity YAML editing. Implementation order is scoped-save correction, then draft, piece and save controller extraction with verification between steps.
- Explicit out of scope: New authoring features, UI redesign, level/shape format changes, remote content delivery, gameplay fixes, asset migrations, project settings, builds, profiler/device work, broad cleanup of unrelated tests, commits and closing this or predecessor tasks.

## Verification and risk

- Focused test path/command: Run the exact-project EditMode fixtures for `LevelDatabaseAddressablesTests` and the new `LevelDraftControllerTests`, `LevelPieceEditControllerTests`, `LevelSaveControllerTests`; then affected `ColorBlockJam.Tests.EditMode.Level` window fixtures plus `DatabaseControlsTests` and `PieceEditorLinkTests`. Use catalog-confirmed MCP `run_tests` under the repository's version gate and retain result/job evidence in this task directory. Run only one Editor test job at a time.
- Required execution preflight: `unity-preflight`
- Required Unity domain workflow: `unity-game-dev`
- Unity compilation/Console evidence: After imports/recompilation, the pinned project's Editor is idle with no new compilation errors or task-caused Console errors. Root-provided creation preflight: exact Unity 2022.3.62f2 project, process 45732, LevelEditor scene clean, no project-authored dirty assets, CLI 1.0.0-beta.11 verified, no live ownership claims. Revalidate before execution and verification.
- EditMode: Characterize scoped-save failure before fixing where practical; test direct controller ownership and important failure paths; preserve existing window interaction assertions. Final independent verification covers the affected suites and scratch cleanup, with current source fingerprints.
- PlayMode: not applicable; this task does not change player runtime code or scene behavior.
- Manual/built-player/profiler/device evidence: No built player, profiler or device proof is required for unchanged Editor behavior. A read-only live-window smoke check may supplement the automated UI fixtures; it must preserve unsaved work and save no authored data. Explicit user acceptance remains a valid alternative channel for a specific criterion.
- Risk: medium
- Approval already granted (exact target only): The user approved the reviewed three-controller Editor refactor and continuing until it is finished. This includes source-only scoped-save preservation, unchanged public/UI/JSON/Undo behavior, focused verification and isolated disposable scratch assets as scoped above. It does not authorize persisted authored data changes or closure.
- Commit/checkin constraint: forbidden
- Current invocation permission: implementation and focused verification authorized by the user's "bitirene kadar devam et" approval; no commit/checkin, publishing, authored-asset write or closure permission
- Handoff destination: `$implement-task TASK-20260926-204508638-a3ad6ee3-level-editor-architecture` under the coordinating root's sole lifecycle ownership, sequential scoped implementer and independent verifier

## Stop conditions

- Another live owner takes an overlapping path: queue under orchestration and resume automatically after its stable handoff and claim release, without a second writer or another user confirmation.
- The proposed edit needs a public API, serialized field, package, asmdef, schema, authored asset or source path outside the approved scope: stop that dependent change and report the concrete boundary.
- A test would persist real Addressables settings/group data or authored level/piece assets, or cleanup cannot prove its exact scratch directory: stop that test and use an isolated verification path.
- An extraction changes expected UI/Undo/save behavior rather than preserving it: repair the regression inside this scope before proceeding to the next extraction.
- Exact Unity identity cannot be proven or compilation/import/test activity makes live verification unsafe: wait or report the concrete verification limitation; never mutate another Editor.

<!-- TASK-LIFECYCLE:START -->
## Lifecycle (managed)

- Task state: closed
- Current phase: closed
- Last cycle verdict: USER_CONFIRMED
- Contract fingerprint: sha256:6fd2eef4a1d00d7b2bbd74a476413c845cc3dcbfe609762c23cb57a92ca12b00
- Evidence fingerprint: sha256:7a5e535ff1083f155993c270bd466023c18e98b74928c98ce019c54264ca2f36
- Reviewed revision: vcs:plastic:cs:10@ColorBlockJamCase2026/ColorBlockJamCase2026@efurk64@unity+owned-sha256:4208767241f19f976baf9185d2674b2b516de3f7e7399bbf610928eb4be2782d
- Attempt count: 2
- Last feedback: Approved architecture/readability work completed; final automated and manual verification passed within the documented protective skip and static vendor-branch limits.
- Blocker: none
- Next action: None
- Closed revision: vcs:plastic:cs:10@ColorBlockJamCase2026/ColorBlockJamCase2026@efurk64@unity+owned-sha256:4208767241f19f976baf9185d2674b2b516de3f7e7399bbf610928eb4be2782d
- Closed at: 2026-09-28T08:46:47Z
- Closure mode: user_confirmed
- Closed by: explicit_user_confirmation
- Superseded by: none

### Acceptance criteria

| Criterion | Result | Verification | Freshness |
|---|---|---|---|
| AC-1 Database saves preserve unrelated dirty assets | PASS | automated: 13 scoped-save cases in final-tests.xml; manual: pinned Addressables 1.22.3 persisted-creation branch review (valid configured settings) | vcs:plastic:cs:10@ColorBlockJamCase2026/ColorBlockJamCase2026@efurk64@unity+owned-sha256:4208767241f19f976baf9185d2674b2b516de3f7e7399bbf610928eb4be2782d |
| AC-2 Draft lifetime, Undo, dirty state and reload recovery preserved | PASS | automated: 7 direct draft cases and existing shell/reload/colour/Undo fixtures in final-tests.xml | vcs:plastic:cs:10@ColorBlockJamCase2026/ColorBlockJamCase2026@efurk64@unity+owned-sha256:4208767241f19f976baf9185d2674b2b516de3f7e7399bbf610928eb4be2782d |
| AC-3 Piece edits, cross-window synchronization and template-delete behavior preserved | PASS | automated: 11 direct piece/adapter cases and existing pen/custom/mirror/delete fixtures; independent source parity review | vcs:plastic:cs:10@ColorBlockJamCase2026/ColorBlockJamCase2026@efurk64@unity+owned-sha256:4208767241f19f976baf9185d2674b2b516de3f7e7399bbf610928eb4be2782d |
| AC-4 Save validation, atomic order, conflict, backup and retry behavior preserved | PASS | automated: 8 direct save cases and existing file/template/plus/delete fixtures; independent persistence sequence review | vcs:plastic:cs:10@ColorBlockJamCase2026/ColorBlockJamCase2026@efurk64@unity+owned-sha256:4208767241f19f976baf9185d2674b2b516de3f7e7399bbf610928eb4be2782d |
| AC-5 Clear internal responsibilities with single state owners | PASS | manual: independent frozen three-controller review; unchanged public/serialized contracts; no UI subscriptions in controllers | vcs:plastic:cs:10@ColorBlockJamCase2026/ColorBlockJamCase2026@efurk64@unity+owned-sha256:4208767241f19f976baf9185d2674b2b516de3f7e7399bbf610928eb4be2782d |
| AC-6 Affected suites pass without authored data or user-state changes | PASS | automated: 752 passed, 0 failed, 1 protective skip in final-tests.xml; all 26 controller cases passed; 81 authored files and live draft preserved; manual: skipped focus path unchanged and outside controllers | vcs:plastic:cs:10@ColorBlockJamCase2026/ColorBlockJamCase2026@efurk64@unity+owned-sha256:4208767241f19f976baf9185d2674b2b516de3f7e7399bbf610928eb4be2782d |
| AC-7 Review note and final diff accurately describe only authorized changes | PASS | manual: final diff/assertion audit, all new metadata accounted for, external Canvas work preserved, review note and final-verification.json current | vcs:plastic:cs:10@ColorBlockJamCase2026/ColorBlockJamCase2026@efurk64@unity+owned-sha256:4208767241f19f976baf9185d2674b2b516de3f7e7399bbf610928eb4be2782d |

Each criterion may be verified by an automated test, manual verification, or explicit user acceptance. Explicit user acceptance requires no extra artifact.

### Defect ledger

| ID | Dedupe key | Status | Relation | Maps to | Symptom / repro | Latest evidence | Verified revision |
|---|---|---|---|---|---|---|---|
| [D-001](defects/D-001.md) | task_regression:ac-6:scoped-save-tests-mutate-authored-addressables | VERIFIED | TASK_REGRESSION | AC-6 | Running scoped-save tests renames the real Level Database group and persists temporary group references in real settings. | Exact 81-file restoration; isolated fixture passes final run and preserves all authored bytes/dirty state. | vcs:plastic:cs:10@ColorBlockJamCase2026/ColorBlockJamCase2026@efurk64@unity+owned-sha256:4208767241f19f976baf9185d2674b2b516de3f7e7399bbf610928eb4be2782d |
| [D-002](defects/D-002.md) | task_regression:ac-6:detached-draft-test-window-close-throws | VERIFIED | TASK_REGRESSION | AC-6 | The new no-op field refresh fixture throws while closing its never-shown window. | Window.Show lifecycle correction; all seven direct draft tests pass in final-tests.xml. | vcs:plastic:cs:10@ColorBlockJamCase2026/ColorBlockJamCase2026@efurk64@unity+owned-sha256:4208767241f19f976baf9185d2674b2b516de3f7e7399bbf610928eb4be2782d |
| [D-003](defects/D-003.md) | task_regression:ac-3:refused-piece-edit-consumes-palette-invalidation | VERIFIED | TASK_REGRESSION | AC-3, AC-6 | A refused piece edit consumes a library catalog change before palette/disarm refresh. | Three adapter regressions pass in final-tests.xml; independent parity review clean. | vcs:plastic:cs:10@ColorBlockJamCase2026/ColorBlockJamCase2026@efurk64@unity+owned-sha256:4208767241f19f976baf9185d2674b2b516de3f7e7399bbf610928eb4be2782d |

### Transition history

- 2026-09-26T22:11:41Z — READY_TO_CLOSE — All three extractions independently reviewed. Final job 9d229f8aef3c4daa8738dc571b547e4f and retained NUnit XML: 752 passed, zero failed, one protective skip, all 26 direct-controller cases passed. All 270 monitored source inputs, 81 authored files and the live user's unsaved state stayed unchanged. D-001 to D-003 verified at the exact current reviewed revision; no closure or VCS action.
- 2026-09-26T21:54:00Z — verification — Stage 2 job b8c8d16a7fbd4f6cbd359398d0ff6b03 completed 744 passed, zero failed and one unchanged Piece Editor protective skip. All 81 protected files, live unsaved Level Editor state and scratch cleanup passed. Stage 3 dispatched to the sole source writer. Concurrent arrow-marker work owns the disjoint Canvas/preview paths; those external changes are preserved and not attributed to this task. The Effect Editor title task records that it is queued behind this task's test ownership.
- 2026-09-26T21:26:40Z — verification — Stage 1 draft extraction passed 52/52 independent tests, including seven direct controller cases and the prior 45 window/reload/colour/effect cases. All 81 protected files and the open user's unsaved window/draft/session snapshot remained identical. Stage 2 piece extraction dispatched to the sole source writer.
- 2026-09-26T21:10:00Z — implementation — attempt 2 isolates settings/groups/schemas from vendor main-asset imports; exact authored-file recovery passed 81/81. Root independent verification follows the frozen source handoff.
- 2026-09-26T21:03:44Z — task-cycle — direct implement-task claim released and root cycle claim acquired; NEW_DEFECT D-001 from independent verification, not user feedback. Source writers and tests paused pending exact authored-state recovery and fixture isolation.
- 2026-09-26T20:50:28Z — implementation — root acquired the exact atomic claim and verified both owner tokens; implement-task direct lane, empty defect ledger. Baseline draft/window EditMode fixtures passed 45/45 (job 5d446bc5de2f4326b5928a3760de7023). One source writer at a time; scoped-save implementer first, root independent verifier next.
- Created — planning — 2026-09-26T20:45:08Z: one task-artifact-only create-task dispatch after task-bug NO_TASK_MATCH. The user approved the Editor cleanup and execution. ID uses cryptographic suffix a3ad6ee3; the exact non-Git task directory was acquired atomically through Windows CreateDirectoryW by /root/review_editor on FURKAN. No implementation, evidence or defect is claimed; lifecycle execution ownership transfers to the coordinating root.
- 2026-09-28T08:46:47Z — Closed (user_confirmed) — closed — user order 2026-09-28 ("task dosyalarını serbest bırak dün geceye kadarkileri sadece hiç yapılmayanları açık bırak kilitsiz şekilde"; cut-off 2026-09-28 03:00 chosen by the user). Every worked task up to that cut-off is closed; unmet, unverified or pending criteria, blockers and any defect rows are preserved as recorded, not verified. Latest automated evidence for reference only: combined batch run of 2026-09-28 (isolated copy, 11:14 local snapshot sha256:A5BE49CC…B366 of cs:17 plus pending changes): EditMode 2103/2107 passed (failures: the parked remote-level-json settings test, the order-dependent ShapePrefabTests.JsonImportAndUndo… which passes alone, and a stale DEC-161 CurrencyCatalogTests assertion since fixed by its owner), PlayMode 247/250 passed with 0 failures and 3 skipped.
<!-- TASK-LIFECYCLE:END -->
