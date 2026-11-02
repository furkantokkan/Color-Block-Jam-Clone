# TASK-20260929-171341270-fecc73ae-ui-builder-canvas-preview — Correct UI Builder canvas previews for runtime UXML

## Identity

- Contract status: ready
- Waiting reason: none
- Action required: none
- Task ID: TASK-20260929-171341270-fecc73ae-ui-builder-canvas-preview
- Supersedes: none
- Title: Correct UI Builder canvas previews for runtime UXML
- Repository (absolute path): C:/Users/e-fur/Documents/Repos/ColorBlockJamCase2026
- Branch/worktree or Plastic workspace: /main@ColorBlockJamCase2026/ColorBlockJamCase2026@efurk64@unity (cs:38)
- Base revision: cs:38
- Task kind: UI
- Area: UI Toolkit authoring preview
- Workflow: direct

## Objective and baseline

- Observable objective: Configure the UI Builder preview canvas for every runtime UXML document to the project's mobile reference resolution so authored screens no longer overflow or appear stacked in UI Builder while keeping the running game layout unchanged.
- Current behavior/evidence: Unity 2022.3 UI Builder persisted Home.uxml at 350 x 450 in Library/UIBuilder/DocumentSettings and in the active BuilderDocument. The other two opened runtime documents (Hub.uxml and GameplayHud.uxml) are also 350 x 450. The project authors screen layouts against 1080 x 1920 PanelSettings, so the smaller design-time canvas produces the attached overflow/overlap while Play Mode remains correct. Unity stores canvas dimensions as per-document UI Builder settings outside the UXML.
- Expected behavior: All 13 runtime UXML documents have a per-document UI Builder canvas of 1080 x 1920, including the currently open Home.uxml state. The project/global instructions and relevant Unity skills explain that the canvas header's size must match the document's runtime reference resolution and that Fit Canvas changes zoom only.
- Behavior that must remain unchanged: Runtime UXML/USS layout, PanelSettings, scenes, gameplay, and all imported art remain untouched. The work changes UI Builder preview state and authoring instructions only.

## Ownership

- Owner/writer role: current implementer; one writer for the UI Builder settings and instruction files
- Allowed paths (exact or narrow globs):
  - Library/UIBuilder/UIBuilderDocument.json
  - Library/UIBuilder/DocumentSettings/*.json
  - AGENTS.md
  - C:/Users/e-fur/.codex/AGENTS.md
  - C:/Users/e-fur/.codex/skills/unity-cli/SKILL.md
  - C:/Users/e-fur/.codex/skills/unity-game-dev/SKILL.md
  - production/tasks/TASK-20260929-171341270-fecc73ae-ui-builder-canvas-preview/contract.md
- Forbidden paths/actions:
  - Packages/**
  - ProjectSettings/**
  - Assets/ColorBlockJam/UI/**/*.uxml
  - Assets/ColorBlockJam/UI/**/*.uss
  - Assets/**
  - Scenes, prefabs, ScriptableObjects, textures, and `.meta` files
  - Unity package or project setting changes
  - Plastic checkin, shelve, or publishing
- Serialized assets: none
- Other active writers/worktrees checked: No active task contract owns UI Builder settings or the listed instruction files. The closed Hub Home task owns runtime Home UI, but this authoring-preview configuration is separate and does not fail its runtime acceptance criteria.
- Execution dependency: none
- Queue policy: none

## Acceptance scenarios

1. Given the 13 runtime UXML documents, when their UI Builder settings are read, then every document's canvas is 1080 x 1920 with Match Game View disabled, and the active Home.uxml state agrees.
2. Given the project or global Unity instructions and the `unity-cli` and `unity-game-dev` skills, when an agent opens a runtime UXML for visual authoring, then they are directed to select the Canvas header and set its size from the runtime reference resolution before judging layout; Fit Canvas is described as zoom-only.
3. Given the current runtime UI, when this task is applied, then no UXML, USS, PanelSettings, scene, prefab, ScriptableObject, package, or project setting changes.

## References and constraints

- Story/GDD/ADR/design/log/image: User screenshots in the current conversation; `ProjectSettings/ProjectVersion.txt`; existing `Library/UIBuilder/DocumentSettings/*.json`; Unity 2022.3 UI Builder canvas documentation.
- Unity version/platform: Unity 2022.3.62f2; portrait Android/iOS UI, reference resolution 1080 x 1920.
- Package/architecture constraints: Use current UI Toolkit/PanelSettings setup. Do not add a project-side editor script or package to compensate for per-document UI Builder preview state.
- Explicit out of scope: Runtime layout redesign, individual USS/UXML changes, game view sizing, scene changes, and automatic project-wide Editor extensions.

## Verification and risk

- Focused test path/command: not applicable; this is local UI Builder preview configuration and documentation only.
- Required execution preflight: `unity-cli`
- Required Unity domain workflow: `unity-game-dev`
- Unity compilation/Console evidence: not applicable; no C# changes.
- EditMode: not applicable.
- PlayMode: not applicable; runtime assets are unchanged.
- Manual/built-player/profiler/device evidence: Read back all 13 per-document JSON settings and active BuilderDocument settings; inspect the Unity 2022.3 UI Builder canvas after refreshing Home.uxml when the exact editor session is safely available.
- Risk: low
- Approval already granted (exact target only): User ordered per-UXML preview correction and updates to global/project Markdown and relevant Unity skills. Generated `Library/UIBuilder` preview settings are included.
- Commit/checkin constraint: forbidden
- Current invocation permission: implementation authorized by the user's current request
- Handoff destination: direct implement-task; report exact settings and documentation changes

## Stop conditions

- Do not alter runtime UI files to work around a UI Builder canvas-size mismatch.
- If the preview settings cannot be safely read back or the Unity target cannot be proven for live Editor work, preserve the file-only fix and report the exact visual-verification gap.

<!-- TASK-LIFECYCLE:START -->
## Lifecycle (managed)

- Task state: ready_to_close
- Current phase: verification
- Last cycle verdict: READY_TO_CLOSE
- Contract fingerprint: sha256:29f44ab2f04edaf4803d8d292a07f67a88a1e8e4e85ba8d18dea5f91a526c71d
- Evidence fingerprint: sha256:d1fcac7bde3500933bd2a240ed73e8a66eaaaeb44a3953705eaa1febc1df0399
- Reviewed revision: vcs:plastic:cs:38@ColorBlockJamCase2026@efurk64@unity+owned-sha256:5a64fc1d5f22db79dcfa533d95cbcabf4675823d2e6a410f4e976032f0efa0d1
- Attempt count: 2
- Last feedback: User reports the overlap persists in the current Home UI Builder screenshot and asks to correct all game UXML documents.
- Blocker: none
- Next action: $task-done TASK-20260929-171341270-fecc73ae-ui-builder-canvas-preview
- Closed revision: none
- Closed at: none
- Closure mode: none
- Closed by: none
- Superseded by: none

### Acceptance criteria

| Criterion | Result | Verification | Freshness |
|---|---|---|---|
| AC-1 | PASS | Manual exact-Editor readback: all 13 UXML per-document settings are 1080x1920 with MatchGameView=false; active Home BuilderCanvas is also 1080x1920 | vcs:plastic:cs:38@ColorBlockJamCase2026@efurk64@unity+owned-sha256:5a64fc1d5f22db79dcfa533d95cbcabf4675823d2e6a410f4e976032f0efa0d1 |
| AC-2 | PASS | Manual review: project/global AGENTS.md and unity-cli/unity-game-dev skills specify matching the runtime resolution, saving through UI Builder, avoiding live Library JSON edits, and reopening each UXML to verify | vcs:plastic:cs:38@ColorBlockJamCase2026@efurk64@unity+owned-sha256:5a64fc1d5f22db79dcfa533d95cbcabf4675823d2e6a410f4e976032f0efa0d1 |
| AC-3 | PASS | Scoped Plastic SCM status confirms no runtime UXML, USS, or PanelSettings changes | vcs:plastic:cs:38@ColorBlockJamCase2026@efurk64@unity+owned-sha256:5a64fc1d5f22db79dcfa533d95cbcabf4675823d2e6a410f4e976032f0efa0d1 |

### Defect ledger

| ID | Dedupe key | Status | Relation | Maps to | Symptom / repro | Latest evidence | Verified revision |
|---|---|---|---|---|---|---|---|
| [D-001](defects/D-001.md) | ac_failure:ac-1:home-ui-builder-canvas-overlap | VERIFIED | AC_FAILURE | AC-1 | Home still displays on a small UI Builder canvas with overlapping content after the prior preview-setting update. | Live Home BuilderCanvas and per-document settings now read 1080x1920; all 13 runtime UXML previews read 1080x1920 with MatchGameView=false. | vcs:plastic:cs:38@ColorBlockJamCase2026@efurk64@unity+owned-sha256:5a64fc1d5f22db79dcfa533d95cbcabf4675823d2e6a410f4e976032f0efa0d1 |

### Transition history

- Created — planning — contract authored from the user's direct request and screenshots
- Started — implementation — Unity 2022.3.62f2 and exact project process path confirmed; CLI status is Unity 6-only for another project, so the preview settings will be updated through the pinned local UI Builder settings files without live Editor calls.
- Verified — verification — all 13 per-document canvas settings and active Home state read back as 1080 x 1920; global/project guidance and both Unity skills were reviewed; Plastic SCM shows no runtime UI or PanelSettings changes. Live canvas rendering was not inspected because the exact MCP instance-to-project identity could not be proven.
- Reopened — diagnosis — user screenshot shows Home remains overlapped; the current Home document setting is 350 x 450, so the previous file-only preview update did not persist in UI Builder. Recorded D-001 as a failure of AC-1 and revalidated the exact Unity project/editor identity for live diagnosis.
- 2026-09-29T18:30:54Z — Verified — ready_to_close — Updated Home through the pinned Unity 2022.3.62f2 UI Builder window and saved with BuilderDocument.SaveSettingsToDisk; all 13 per-document settings and the active Home canvas read back at 1080 x 1920 / MatchGameView=false. Updated project/global guidance and both Unity skills to prevent live Library JSON edits from being overwritten. No runtime UI or PanelSettings changes.
<!-- TASK-LIFECYCLE:END -->
