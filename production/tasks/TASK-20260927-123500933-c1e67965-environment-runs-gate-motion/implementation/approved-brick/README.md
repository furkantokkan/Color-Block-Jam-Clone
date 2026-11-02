# Approved Brick_End

This generator builds the approved rounded brick cap from the supplied `Wall` profile. It saves
the cap as a reusable `.blend`/`.fbx` and renders bricks for review. It only reads
`Assets/ColorBlockJam/Art/Models/WallAndDoor.fbx` and writes to `out/`. It changes no Assets and
no lifecycle state.

## Run

```
blender --background --factory-startup --python build_brick_end.py -- --out out
```

It needs Blender 4.1+ and reuses `../brick-proof/brick_proof.py` (FBX axis fit, prefab
calibration, Workbench renders).

## The model

- `Brick_End` fills Wall's exact bounds in Wall's own mesh space: closed roof toward −Y, length
  along Z, the same X centre and Y range.
- **Mouth end:** the +Z end is Wall's flat mouth. The straight half is Wall's cross-section, sampled
  by ray-casting Wall's roof at mid-length and averaging both sides.
- **Rounded end:** the other half revolves that half-profile 180° around the vertical axis. Both
  vertical end edges are rounded with Wall's own pillow profile, and the tip reaches Wall's −Z
  bound.
- Like Wall, it has an open underside, smooth normals (sharp over 40°) and zero UVs. BoardLit
  samples a fixed atlas texel, so zero UVs are enough.
- **Reuse:** a brick over a straight run of n quadrants is `Brick_End`, then n−2 `Wall` pieces,
  then `Brick_End` turned 180°. Each end's mouth faces into the brick. Every run between corners
  and gates is one brick, as in the reference. A 1-cell brick is two ends back to back.
- **Existing pieces are unchanged:** `Corner_1`, `Wall`, `Door_1x1` and the supplied FBX are not
  modified.

## Outputs (`out/`)

- `Brick_End.blend`: `Brick_End` and an unchanged `Wall_Reference` copy.
- `WallBrick.fbx`: the same two meshes. `Wall_Reference` is only for checking calibration.
- `evidence.json`:
  - The axis fit and the sampled profile.
  - Local bounds before and after an FBX re-import (`round_trip_ok`).
  - `brick_end_mouth_at_yaw_0`.
  - `joins.end_to_wall_max_step`, expected ≈ 0.
  - `joins.middle_vs_plain_wall_max_difference`, expected ≈ 0.
- Renders:
  - `bricks_1_2_3.png`: 1-, 2- and 3-cell bricks above today's continuous 3-cell run.
  - `board_door_gates_game.png` / `_close.png`: the 4 × 4 reference board with bricked runs and
    today's `Door_1x1` gates.
  - `board_brick_gates_close.png`: the same board with gates built from the same brick pieces in
    the door colour.
  - `brick_end_close.png`: the piece alone.

## Review checks

- `round_trip_ok` is true, and the End/Wall step and middle difference are ≤ 0.002.
- `brick_end_mouth_at_yaw_0` has exactly one entry.
- The renders read as separate rounded bricks that meet the corners and gates with a groove, as in
  the reference.

## Integration once the Editor is free (separate approved step)

1. Import `WallBrick.fbx` under `Art/Models`, with materials off and Read/Write off. Confirm the
   `Wall_Reference` mesh bounds equal `WallAndDoor`'s `Wall` bounds. If they differ, fix the model
   Scale Factor before continuing.
2. `BoardKitAssets`: build `Brick_End.prefab` with Wall's calibration (X180, 0.72 height,
   −centre X, X stretch 1.01).
3. `EnvironmentDatabase`: add a `Brick_End` piece field and add it to `GetPieces` for pooling.
4. Composer: one brick per straight outer-ring run, split at corners and gates, as in `add_brick`.
   Holes, notches and barriers keep the rule output.
