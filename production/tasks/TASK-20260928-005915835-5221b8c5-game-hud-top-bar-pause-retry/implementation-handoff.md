# Implementation handoff: Game HUD top bar, pause menu and Retry

## Coordination record

- 2026-09-28T00:3xZ, Booster [f1ed7e]: carve-out `Gameplay/Hud/**` for this task's C#; element names `timer-band`,
  `timer-holder`, `timer-icon`, `timer-label`, `freeze-counter` and its children stay unique under `hud-root`;
  Restart/GoHome are illegal from Playing (Pause(User) first) and refused while `IsBoosterWritePending`; Back while
  Playing is read only by `DragInputRouter` (targeting cancel first, then this task's fallback).
- 2026-09-28T00:3xZ, reorg [7d9d8c]: order is Hub checkin -> Booster copy-back + checkin -> folder move (about
  30 min) -> this task's window, at the final paths (`Runtime/Gameplay/Hud/**`, `UI/Game/**`,
  `Runtime/Gameplay/Composition/GameSceneInstaller.cs`, `Runtime/Gameplay/Session/DragInputRouter.cs`,
  `UI/Game/GameplayHud.uxml`, `Scenes/Game.unity` unmoved). The reorg messages "restructure done"; this task
  messages "done" when its window closes. No shared writes while the move runs.
- 2026-09-28T00:38Z: the Booster copy after its held-outline merge (EditMode 626/626 Gameplay+Player, PlayMode
  172/172 Gameplay+Presentation per Booster) was snapshotted read-only into `%TEMP%/cbj-hud-4effc5`; baseline manifest
  `hud-copy-baseline.sha256` (1871 files) in the session scratchpad.
- 2026-09-28T01:1xZ, Booster [f1ed7e]: no objection to integration hunks 1-6; they are this task's to write once
  Booster's checkin lands and its task closes (Booster messages then). Semantics to keep:
  - (a) the HUD-less installer branch keeps `NoPauseOverlay` plus `UnavailablePurchaseHost`;
  - (b) the menu calls Pause(User) before Restart/GoHome, and while a booster write is pending shows them disabled
    or ignores the tap, never queues it;
  - (c) the back fallback runs only when targeting is inactive and the state is Playing; the purchase popup already
    consumes Back while open;
  - (d) on foreground after a Saving/Purchase background the coordinator latches an ordinary pause; the listener
    opens the menu on that latched pause and never resumes;
  - (e) `GameplayHud.uxml` uses project:// paths that the folder move must fix.
  - DEC numbers: Booster plans no more rows; this task takes DEC-152 onward (re-check the live maximum at write time).
- 2026-09-28T01:2xZ, Booster [f1ed7e] correction: the user ordered CBJ Template particles for the booster casts
  before Booster's checkin, so Booster took DEC-152. This task takes DEC-153 onward (still re-check the live maximum
  at write time).
- 2026-09-28T01:3xZ, Haptics session [a39f41] asked for file ownership. Reply: this task reserves DEC-153 to DEC-155;
  Haptics takes DEC-156 onward. Haptics also edits DragInputRouter (Press), GameSceneInstaller (one binding) and a
  USS class on GameplayHud.uxml's booster buttons: the two sessions serialize their shared-window edits in those
  files (whoever writes first messages the other; the second re-reads the live file first).
- 2026-09-28T03:0xZ, Booster [f1ed7e]: checkin landed as cs:16; its window grant is open (the reorg move still
  precedes this task's writes). Keep Game.unity's UI input module action references empty (fresh default actions,
  regression test GameSceneUiInputTests). Booster's queued follow-up
  TASK-20260928-015130392-6c288254-booster-availability-extension will later rework GameplayHud.uxml's booster-row,
  GameSceneInstaller behaviour registrations and GameAttemptLoader's level number, merging on top of this task: keep
  this task's hunks to top-content, the overlay instance and the pause areas.
- Level number, one source: Booster's follow-up adds `LevelAttempt.LevelNumber` (1-based, set by GameAttemptLoader
  from the resolved index; Test Level uses the level's database position) and
  `LevelSession.TryPrepare(level, levelNumber, out error)`. When Booster reports it landed in the shared workspace,
  switch `AttemptLevelNumber` to read it and drop this task's database scan (follow-up owned by this task if it is
  still open, else a small successor change).
- 2026-09-28T04:4xZ, reorg [7d9d8c]: the move cannot be checked in on its own (Plastic records a folder move only by
  checking in the moved folder, and `cm ci` is recursive, so it would take about 180 pending items from several
  sessions, this task's included). The reorg session is asking the user how to proceed (likely one coordinated
  combined checkin). This task sent its exact file list and a comment bullet to [7d9d8c] for that case, and released
  its hold on DragInputRouter.cs, GameSceneInstaller.cs and GameplayHud.uxml to Haptics [a39f41] and Arrow
  [5c3ff984] ("done"). Its own scoped checkin authority stays valid for whichever form the user chooses.
