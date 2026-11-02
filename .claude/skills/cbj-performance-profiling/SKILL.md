---
name: cbj-performance-profiling
description: Measure and fix frame-rate, stutter, GC and draw-call problems in ColorBlockJamCase2026 (Unity 2022.3, Android). Use it whenever someone reports FPS drops, jank, hitches, slow or choppy block drags, "kasıyor", "fps düşüyor", "takılıyor", heavy GC, too many SetPass calls or batches, device or Editor performance, or asks to optimize or profile the game, even without the word "profile". It separates Editor-only cost from game cost with a live Profiler capture over the MCP 8082 bridge, measures on the USB phone with a copy-only development build and adb-driven drags, and maps the numbers to PROJECT.md Performance Rules and NFR budgets before anything is changed.
---

# CBJ performance profiling (Claude entry point)

The skill body is shared with Codex and lives in `.agents/skills/cbj-performance-profiling/`.
Read `.agents/skills/cbj-performance-profiling/SKILL.md` now and follow it. Run its scripts from that folder
(`scripts/live_editor.py`, `scripts/csharp/*.cs`, `scripts/device_build.ps1`, `scripts/device_session.py`,
`scripts/check_16kb.py`, `scripts/outline_check.py`).
