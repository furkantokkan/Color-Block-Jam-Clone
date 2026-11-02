# Baseline before the first write

Recorded 2026-09-27T22:33Z (UTC) by the orchestrating session under lifecycle lock
`Library/ccgs/task-lifecycle-locks/TASK-20260927-220047618-95397edf-level-editor-validate-level.lock.claim`.
Workspace `/main` at cs:14. Other sessions' uncommitted work is preserved; only the
contract's allowed paths change.

## Existing files this task may edit

- `Assets/ColorBlockJam/Editor/Levels/LevelDatabaseWindow.cs`: sha256:936fadb7001cd422b3320c725c81faa88114f75d6e3d9eed46183f83677ac20c (211892 bytes, 2026-09-27T00:47:10Z)
- `Assets/ColorBlockJam/Editor/ColorBlockJam.Editor.asmdef`: sha256:0464b1091235b687c722d41086e5ab2a6c54943be122f5281eb7f1b28bb76b77 (286 bytes)
- `Assets/ColorBlockJam/Tests/EditMode/LevelTestPlayTests.cs`: sha256:c45e8abb23bb8062124454b5617c0fe9d5de5c5ce5da66763c10285fd38ede70 (19204 bytes)
- `Assets/ColorBlockJam/Tests/EditMode/ColorBlockJam.Editor.EditModeTests.asmdef`: sha256:4449102ca2e7316b9128c6a7c18a1322f9e7097328157a0fb783d039b2eff036 (528 bytes)
- `docs/Level-Editor-Design.md`: sha256:9dc2f325904b7e0b788a2591a1831f5162cc86c64e7eb26d14d49758a683dc10 (118312 bytes; carries another session's uncommitted line ~382 edit)
- `PROJECT.md`: sha256:e4d305113903780b4c392a1b53ff505fac32198bb5ae9cfdc08d6380eab84b40 (96268 bytes; shared with live locks, append one row only after a fresh read)

## New folders (absent at baseline)

- `Assets/ColorBlockJam/Editor/Levels/Validation/`
- `Assets/ColorBlockJam/Tests/Validation/`

## Live Editor identity

- MCP for Unity instance `ColorBlockJamCase2026@99077e4df1ecfaa4`: the hash equals SHA1("C:/Users/e-fur/Documents/Repos/ColorBlockJamCase2026/Assets")[0..16]; `project/info` read back root `C:/Users/e-fur/Documents/Repos/ColorBlockJamCase2026`, version 2022.3.62f2. At 22:07Z the shared Editor was running another session's EditMode tests.
