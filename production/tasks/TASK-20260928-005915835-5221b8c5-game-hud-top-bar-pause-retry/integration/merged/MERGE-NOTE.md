# GameSceneInstaller.cs integration merge (2026-09-28)

- Command: `git merge-file -p <live> <cs16 base> <ours>`; exit 0, no conflict markers.
- Live side (cs:17 + pending) removed the held-outline wiring: `using ColorBlockJam.Gameplay.Board.Outline`,
  `m_outlineMaterial` field and its null check, and the `HeldOutlineView` binding. The merge keeps those removals.
- Task side added the HUD usings (`Gameplay.Hud`, `.Menu`, `.Navigation`, `.TopBar`), the `PopupPauseOverlay` /
  `NoPauseOverlay` choice on `m_hudDocument`, the nine HUD-branch bindings (`AttemptLevelNumber` through
  `GameTopBar`), the `SetBackFallback` build callback and its method. The merge keeps all of them.
- No new serialized fields, so `Game.unity` needs no change for this file.
- Result: 195 lines, CRLF, sha256 prefix `e6e9d3c65181e4b2`. Verified in the rehearsal copy
  `%TEMP%/cbj-hud-live-4effc5` (see the rehearsal report for compile and test results).
- The other five integration files (GameplayHud.uxml/.uss, GameAttemptLoader.cs, DragInputRouter.cs,
  ApplicationPauseRelay.cs) were unchanged since `base/*.live`, so `ours/` applies as-is.
