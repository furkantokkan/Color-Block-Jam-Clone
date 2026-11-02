# TASK-20260924-125312690-d6a25edf-vacuum-suction — Correct upright Vacuum suction motion

## Identity

- Contract status: ready
- Waiting reason: none
- Action required: none
- Task ID: TASK-20260924-125312690-d6a25edf-vacuum-suction
- Supersedes: none
- Title: Correct upright Vacuum suction motion
- Repository (absolute path): C:/Users/e-fur/Documents/Repos/ColorBlockJamCase2026
- Branch/worktree or Plastic workspace: /main, ColorBlockJamCase2026
- Base revision: cs:1
- Task kind: bug
- Area: Booster Vacuum presentation
- Workflow: direct

## Objective and baseline

- Observable objective: Replace the floor-facing Vacuum suction pose with an upright hose and upper intake, visibly deforming during suction.
- Current behavior/evidence: Previous bake retains the icon's inverted-U hose and floor nozzle, with minor sway. Six new user screenshots show an upper intake and pronounced hose bulge. No prior tracked contract owns the ad-hoc delivery.
- Expected behavior: Body below the intake; rigid head/grip above a deforming hose; a visible bulge passes down the hose before it relaxes. Preserve current colors.
- Behavior that must remain unchanged: Existing entry/exit visibility tween, 0.75-second duration, pause/resume/stop, no autoplay, shared 16-frame atlas budget, Hammer/Rocket production assets and code.

## Ownership

- Owner/writer role: root implementer; sequential independent verifier; documentation reviewer owns only reference copies/catalog.
- Allowed paths (exact or narrow globs):
  - ArtSource/Boosters/render_vacuum_motion.py
  - ArtSource/Boosters/VacuumMotion.blend
  - ArtSource/Boosters/import_vacuum_motion.cs.txt
  - ArtSource/Boosters/render_vacuum_suction_preview.cs.txt
  - ArtSource/Boosters/package_motion_previews.py
  - Assets/Art/Models/Boosters/Effects/VacuumSuctionAtlas.png
  - Assets/Art/Models/Boosters/Effects/VacuumSuctionAtlas.png.meta
  - Assets/Art/Models/Boosters/Effects/VacuumEffect.prefab
  - Assets/Art/Models/Boosters/Effects/VacuumEffect.prefab.meta
  - Assets/Tests/PlayMode/BoosterEffectViewTests.cs
  - docs/art/boosters/motion/**
  - docs/Color-Block-Jam-Visual-References.md
  - docs/media/color-block-jam/user-references/18?-vacuum-*.png
  - README.md
  - PROJECT.md
- Forbidden paths/actions:
  - Packages/**
  - ProjectSettings/**
  - Assets/Scenes/**
  - Assets/Scripts/**
  - Assets/Art/Models/Boosters/Hammer*
  - Assets/Art/Models/Boosters/RocketPurple*
  - Assets/Art/Models/Boosters/Effects/HammerEffect*
  - Assets/Art/Models/Boosters/Effects/RocketPurpleEffect*
  - Original UI icons, reference project, commits/checkins and publishing
- Serialized assets: exact paths below
  - Asset: Assets/Art/Models/Boosters/Effects/VacuumEffect.prefab
    Sole writer: root
    Matching `.meta` owner: root
  - Asset: Assets/Art/Models/Boosters/Effects/VacuumSuctionAtlas.png.meta
    Sole writer: root
    Matching `.meta` owner: root
- Other active writers/worktrees checked: Read-only reference agent, then disjoint documentation ownership. Existing dirty work preserved. No shared lifecycle lock service in Plastic; serialize manually in this conversation, without claiming cross-terminal locking.
- Execution dependency: none
- Queue policy: none

## Acceptance scenarios

1. At full visibility the body sits below the hose and rigid grip/head; the mouth faces upward toward the board and its sprite-local intake stays fixed.
2. Ordered active frames show a localized hose bulge traveling toward the body and recovering; rigid head/grip do not deform, connectors stay joined, and no geometry clips.
3. Existing entry/exit, duration, pause/resume/stop and Hammer/Rocket behavior are preserved; no runtime rig/camera or extra texture memory is introduced.
4. Six original references are preserved unchanged, indexed, and distinguished from inferred timing and existing gameplay rules.

## References and constraints

- Story/GDD/ADR/design/log/image: User's six screenshots in this conversation, preserved as reference 18; PROJECT.md booster presentation; docs/art/boosters/motion/README.md.
- Unity version/platform: 2022.3.62f2; Android/iOS portrait
- Package/architecture constraints: Blender modeling; PrimeTween lifecycle unchanged; offline 16-frame 1024-square ASTC6x6 sprite sheet.
- Explicit out of scope: Domain removal, block suction gameplay integration, color redesign, Hammer/Rocket changes, device optimization claims.

## Verification and risk

- Focused test path/command: Assets/Tests/PlayMode/BoosterEffectViewTests.cs; PlayMode test assembly ColorBlockJam.Presentation.PlayModeTests
- Required execution preflight: unity-preflight
- Required Unity domain workflow: unity-game-dev
- Unity compilation/Console evidence: Pinned Editor check after import; preserve existing errors separately.
- EditMode: Read back sprite import and intake reference.
- PlayMode: Existing suite plus meaningful upright intake regression assertion.
- Manual/built-player/profiler/device evidence: Blender sequence and Unity sampled-pose preview; device/build not required.
- Risk: high
- Approval already granted (exact target only): User requests correction of Vacuum suction design/animation and addition of these references, preserving accepted entry/exit; covers scoped bake/import/prefab changes.
- Commit/checkin constraint: forbidden
- Current invocation permission: none
- Handoff destination: sequential verification, then report ready-to-close

## Stop conditions

- Do not mutate a mismatched/busy Editor or another writer's serialized assets. Continue safe offline Blender work while live transport is unavailable.
- Do not expand into gameplay or change accepted visibility motion.

<!-- TASK-LIFECYCLE:START -->
## Lifecycle (managed)

- Task state: closed
- Current phase: closed
- Last cycle verdict: USER_CONFIRMED
- Contract fingerprint: sha256:81ac202e534cd462ddaec64a06c60a9223e55bb5772f05e02097b94981f0de47
- Evidence fingerprint: sha256:d67748a06d8d831281354f673896f46194d61cf7f9fad58420efbd27b7d7beec
- Reviewed revision: vcs:plastic:cs:1@ColorBlockJamCase2026@efurk64@unity+owned-sha256:b6c9c9662ae637a68e8dfcf15dfdbe1cf1274cc792c01e6631c138f31f8bb495
- Attempt count: 1
- Last feedback: Current suction section is wrong; entry/exit is accepted. Correct upright hose/grip design and add six references.
- Blocker: none
- Next action: None
- Closed revision: vcs:plastic:cs:1@ColorBlockJamCase2026@efurk64@unity+owned-sha256:b6c9c9662ae637a68e8dfcf15dfdbe1cf1274cc792c01e6631c138f31f8bb495
- Closed at: 2026-09-28T08:46:45Z
- Closure mode: user_confirmed
- Closed by: explicit_user_confirmation
- Superseded by: none

### Acceptance criteria

| Criterion | Result | Verification | Freshness |
|---|---|---|---|
| AC-1 Upright silhouette and fixed upper intake | PASS | Manual Unity/Blender preview and automated upper-intake regression | vcs:plastic:cs:1@ColorBlockJamCase2026@efurk64@unity+owned-sha256:b6c9c9662ae637a68e8dfcf15dfdbe1cf1274cc792c01e6631c138f31f8bb495 |
| AC-2 Visible localized hose motion and rigid connectors | PASS | Independent visual frame review; vacuum-visual-checks.json | vcs:plastic:cs:1@ColorBlockJamCase2026@efurk64@unity+owned-sha256:b6c9c9662ae637a68e8dfcf15dfdbe1cf1274cc792c01e6631c138f31f8bb495 |
| AC-3 Preserved lifecycle and runtime budget | PASS | 11/11 PlayMode; vacuum-import.json; preserved source/prefab SHA-256 | vcs:plastic:cs:1@ColorBlockJamCase2026@efurk64@unity+owned-sha256:b6c9c9662ae637a68e8dfcf15dfdbe1cf1274cc792c01e6631c138f31f8bb495 |
| AC-4 Original references preserved and documented | PASS | Six SHA-256-equal copies; visual reference 18; 37 local links resolve | vcs:plastic:cs:1@ColorBlockJamCase2026@efurk64@unity+owned-sha256:b6c9c9662ae637a68e8dfcf15dfdbe1cf1274cc792c01e6631c138f31f8bb495 |

### Defect ledger

| ID | Dedupe key | Status | Relation | Maps to | Symptom / repro | Latest evidence | Verified revision |
|---|---|---|---|---|---|---|---|

### Transition history

- Created — planning — user feedback routed from contractless ad-hoc delivery; execution already authorized by the correction request.
- Implementation — manually serialized Plastic lane, root owns asset changes; offline Blender work proceeded during live transport downtime.
- Ownership erratum — reference filename glob corrected to match the six authorized 18a–18f copies; no scope expansion.
- Verification — exact Editor confirmed on its active MCP HTTP 8081 session after restart; no project/transport configuration changed.
- Ready to close — independent render review and 11/11 PlayMode tests pass; serialized scene preserved; 149 reviewed files/explicit missing paths fingerprinted. No closure or checkin performed.
- 2026-09-28T08:46:45Z — Closed (user_confirmed) — closed — user order 2026-09-28 ("task dosyalarını serbest bırak dün geceye kadarkileri sadece hiç yapılmayanları açık bırak kilitsiz şekilde"; cut-off 2026-09-28 03:00 chosen by the user). Every worked task up to that cut-off is closed; unmet, unverified or pending criteria, blockers and any defect rows are preserved as recorded, not verified. Latest automated evidence for reference only: combined batch run of 2026-09-28 (isolated copy, 11:14 local snapshot sha256:A5BE49CC…B366 of cs:17 plus pending changes): EditMode 2103/2107 passed (failures: the parked remote-level-json settings test, the order-dependent ShapePrefabTests.JsonImportAndUndo… which passes alone, and a stale DEC-161 CurrencyCatalogTests assertion since fixed by its owner), PlayMode 247/250 passed with 0 failures and 3 skipped.
<!-- TASK-LIFECYCLE:END -->
