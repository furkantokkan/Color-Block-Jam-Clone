# Brick proof: rounded wall bricks from the supplied kit

Can reusable rounded bricks be composed from the unchanged `WallAndDoor.fbx` meshes, with the
real prefab calibration, instead of cutting gaps into the continuous frame? No mesh is modelled,
no asset is written, and the FBX is only read.

## Run

```
blender --background --factory-startup --python brick_proof.py -- --out out
```

Options: `--brick-cells N` (default 1), `--no-backer`, `--variants baseline,single,hairpin,hairpin_gate`.
It targets Blender 4.1+ (it uses `Mesh.set_sharp_from_angle` when present).

## What it does

1. It imports the FBX and maps it into Unity mesh space by searching every signed axis
   permutation for the one that reproduces D-001's Wall bounds (X −0.49905..0.498, Z −0.4983..0.5017)
   and the roof at Y −0.61087. `evidence.json` → `axis_fit.error` should be about 0.
2. It applies the `Visual` calibration from `BoardKitAssets.cs`: X180 flip, 0.72 height, Wall X
   stretch 1.01, Corner_1 X offset −0.0083 with stretch 1.0166, and Door_1x1 Z stretch 1.02.
   Each piece then gets a root position, yaw and scale, as `EnvironmentPiecePlacement` already
   supports.
3. It finds each piece's connection mouths, the sides where the roof reaches the bound, and solves
   every Corner_1 yaw from them. It never assumes a direction.
4. It composes a 4 x 4 board with a blue 1-cell top gate and a red left gate, the reference crop,
   in four variants:
   - `baseline`: today's continuous frame (quadrant Walls, 4 ring Corner_1, Door_1x1).
   - `single`: quadrant-scale bricks. Each brick end is one Corner_1 with its rounded edge facing
     outward, so each end has one rounded vertical edge.
   - `hairpin`: quarter-scale bricks in two columns. Each end is two Corner_1 joined mouth to
     mouth (a U cap), so both end edges are rounded. The brick is 0.5 thick, the ring thickness.
   - `hairpin_gate`: the same, with each gate built as a hairpin brick in the door colour.
     The arrow is kept.
   Bricks are `--brick-cells` long. The four ring corners stay Corner_1 segments. Holes are not
   part of this proof. A dark quad under each run stands in for the existing FloorBase prefab.
5. It renders each variant twice with Workbench (studio light and cavity): at the Game camera's
   pitch 75 / FOV 60 (1080 x 1920, DEC-103 framing), and as a top-left close-up (1080 x 1080).
6. It raycasts roof profiles across the ring strip on a two-cell test brick for each style:
   at mid-length, at the cap centre, and 4 mm on each side of the cap/wall join.

## Outputs (`out/`)

- `<variant>_game.png` and `<variant>_close.png`.
- `evidence.json`:
  - `axis_fit`.
  - `mouths_at_yaw_0`.
  - `mesh_space_side_roof_y`, which checks the −0.61087 mouth claim for each side.
  - `baseline_corner_yaws_match_mouths`, a sanity check against the composer's corner yaws
    (270/0/180/90).
  - `roof.<style>`: profiles, `centre_groove_depth` and `cap_join_max_step`.
  - Per-variant piece counts.

## Decision criteria

- **Pass (hairpin):**
  - `axis_fit.error` < 0.005.
  - Corner_1 reports exactly two adjacent mouths.
  - `cap_join_max_step` ≤ 0.005, so there is no visible step where a cap meets a wall.
  - `centre_groove_depth` is small enough that the close-up reads as one rounded brick, not two
    parallel rails. At the quarter scale a groove about 0.05 or more will likely show; judge it in
    `hairpin_close.png`.
  - The bricks read as separate rounded units in the game render, like reference 1.
- **Fallback (single):** if the hairpin groove shows, `single` gives a rounded outer vertex per end
  with no groove, but its floor-side end edge stays square.
- **Stop:** if neither reads as the reference brick, the kit has no clean capped brick. Proper
  capped ends would then need Blender-authored geometry, which needs an explicit asset decision.

## If it passes: source-only integration (no serialized writes)

- A plain C# `EnvironmentWallRun` in an allowed path would split each outer-ring run into bricks
  of N cells. The composer would place the existing Wall and Corner_1 prefabs at 0.25 root scale
  with solved yaws. Ring corners, inner holes and notches keep today's rule output.
- The existing FloorBase prefab goes under each run in the wall colour, dark enough to read as a
  groove.
- Gates stay one span per authored door, which keeps the gate-motion patch's grouping intact.
  `hairpin_gate` swaps Door_1x1 for brick pieces tinted with the door colour.
- **Cost to check:** hairpin uses 8 pieces per ring cell against 2 today, about 4× the ring
  pieces. That means pool warm-up and draw-call checks on device (Performance Rule 1).
