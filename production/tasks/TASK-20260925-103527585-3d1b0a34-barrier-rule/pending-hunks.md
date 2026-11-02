# Pending hunks — TASK-20260925-103527585-3d1b0a34-barrier-rule

Prepared on 2026-09-28 by "Görevleri inceleme ve dağıtma [8ea911] / barrier-rule-owner". Each hunk is exact. Re-read the target file immediately before applying it, and apply each hunk once. Order: 1 and 2 together, then 3, with 4 independent.

## 1. GameSceneInstaller.cs — register the module (WAITING_FOR_OWNER)

- File: `Assets/ColorBlockJam/Runtime/Gameplay/Composition/GameSceneInstaller.cs` (the module-list line only).
- Why it waits: the Arrow task `TASK-20260926-230921278-a3ffdc40-arrow-block-gameplay` (live lifecycle lock, session "Edit dropdown ve colors kareleri [c818e4]", phase verification) owns this line, and its `GameSceneArrowTests` asserts exactly two modules. Applying this hunk alone turns that test red. Apply it together with hunk 2, after the Arrow owner releases the file or the decision owner approves hunk 2.

```diff
-            var effects = new BlockEffectContextSource(new IBlockEffectModule[] { new IceEffectModule(m_iceMaterial), new ArrowEffectModule() });
+            var effects = new BlockEffectContextSource(new IBlockEffectModule[] { new IceEffectModule(m_iceMaterial), new ArrowEffectModule(), new BarrierEffectModule(m_environment) });
```

## 2. GameSceneArrowTests.cs — module count (outside this contract; needs its owner or the decision owner)

- File: `Assets/ColorBlockJam/Tests/Gameplay/PlayMode/GameSceneArrowTests.cs` (owned by the Arrow task above).

```diff
-            Assert.That(source.Count, Is.EqualTo(2), "the installer registers Ice and Arrow");
+            Assert.That(source.Count, Is.EqualTo(3), "the installer registers Ice, Arrow and Barrier");
             Assert.That(source.GetModule(0).TypeId, Is.EqualTo("ice"));
             Assert.That(source.GetModule(1).TypeId, Is.EqualTo("arrow"), "the installer lists Arrow after Ice");
+            Assert.That(source.GetModule(2).TypeId, Is.EqualTo("barrier"), "board objects follow block effects (DEC-MECH-3)");
```

## 3. LevelEffectRegistry.cs — runtime support flag (gated on solver support)

- File: `Assets/ColorBlockJam/Runtime/Core/Levels/LevelEffectRegistry.cs` (member `k_barrierRuntimeImplemented`, line 52 on 2026-09-28).
- Why it waits: `LevelSupportGateTests.EveryRuntimeImplementedEffectHasSolverSupport` (DEC-144) fails for a runtime-implemented effect that `LevelSolverSupport` does not list. Today the list holds only `arrow` and `ice`, and the solver lives in `Assets/ColorBlockJam/Editor/Validation/**`, outside this contract. Apply this hunk only after barrier solver support lands (the decision owner widens this contract or queues a Validation task) and after hunk 1. Otherwise barrier levels would load with no rule registered.
- Solver note for that task: every barrier flips on every clear, so a barrier's state in a search position is `startsOpen` XOR (cleared blocks mod 2). The solver can derive it from the removed-block set it already tracks, with no extra key bits.

```diff
-        private const bool k_barrierRuntimeImplemented = false;
+        private const bool k_barrierRuntimeImplemented = true;
```

## 4. PROJECT.md — decision row DEC-163 (WAITING_FOR_OWNER: file changed within 15 minutes)

- File: `PROJECT.md`, decision log. Append after the last row (DEC-162, "Level content updates", at 11:02 on 2026-09-28). The orchestrator reserved DEC-161 for this task, but another session used it at 10:52 ("Dummy plus-button reward") and "Level content updates" took DEC-162, so this row takes DEC-163 (checked in PROJECT.md, docs and every task file at 11:05). The file changed at 10:52 and again at 11:02, so it was never 15 minutes quiet during this owner's run. Check the free number again before applying.

```diff
+| Barrier board objects on the shared effect architecture (DEC-163) | User decision of 2026-09-25 (OBJ-05, relayed by "Ruled tile level sistem") and the go of 2026-09-28 ("delegate the unfinished tasks to subagents"). The Barrier rule is `Runtime/Domain/Hooks/BarrierRule` (`ICellGate` + `IBoardEventListener`, engine-free, arrays copied at construction, 0 B per call). A closed barrier cell admits only the block the passed `IBoardReader` already shows on it (DEC-MECH-18): a block the barrier closes under stays and may leave, and no other block enters. Every barrier flips once per `BlockExited` with `Leave` and once per `BlockRemoved`, so a Vacuum of N blocks flips it N times (DEC-MECH-3). `Stay`, moves and Rocket splits never flip it, and a Rocket part needs no state because the reader shows its new ID (DEC-146). `BarrierEffectModule` (TypeId `barrier`, last in the module list: blocks before board objects) reads the barrier cell objects on Home into `BarrierEffectLevel` and creates one fresh rule per attempt, so Restart restores `startsOpen`. It takes the scene's `EnvironmentView` in its constructor, as `IceEffectModule` takes its material, so `IBlockEffectModule`, `BlockEffectViewContext`, `BoardPresenter`, `LevelSession`, `GameAttemptLoader` and the converter are unchanged. `BarrierEffectView` rejects a play area showing a different barrier count on Home, snaps every barrier at `BindAttempt` and makes one animated `SetBarrierOpen` per flip in `Sync`. Its `SetPaused` and `StopTweens` are the barrier tweens' Suspend and Teardown stop points (DEC-121). `k_barrierRuntimeImplemented` stays false until Validate Level models barriers (DEC-144 requires solver support for every runtime effect). Replaces the task's first `BoardEffectHookSource` design. See [Gameplay: Drag and Gate](docs/Gameplay-Drag-And-Gate.md). Task `TASK-20260925-103527585-3d1b0a34-barrier-rule`. |
```
