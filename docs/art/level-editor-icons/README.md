# Level Editor corner and arrow icons

Icons for the Level Editor canvas corner buttons and edge bars. The user supplied them on 2026-09-25 as black-on-transparent 512 px masters: three corner brackets and one up chevron. The user asked for the missing orientations to be drawn.

- Corners (`corner-top-left`, `corner-top-right`, `corner-bottom-left`, `corner-bottom-right`) are rotations of the user's top-right bracket, so all four have the same stroke and padding.
- Arrows (`arrow-up`, `arrow-right`, `arrow-down`, `arrow-left`) redraw the user's chevron in the same 320 px content box, with the corners' 48 px stroke (at 512 px), round caps and join, and about 45 degree arms.

Files follow Unity's editor icon naming:

| File | Use |
|---|---|
| `<name>.png`, `<name>@2x.png` | Black, 32 and 64 px, for the light editor skin |
| `d_<name>.png`, `d_<name>@2x.png` | White, 32 and 64 px, for the dark editor skin |
| `source/<name>.png` | Black 512 px master |
| `contact-sheet.png` | Review sheet: dark skin row over light skin row |

Suggested mapping (the Level Editor owner decides):

- A corner button uses the bracket of its own corner: top-left uses `corner-top-left`, and so on.
- Each edge bar uses the arrow that points in its direction: grow outward, shrink inward.

Generator: `make_level_editor_icons.py`, kept in the environment session's scratchpad. The PNGs here are the deliverable.

## Colour order icons (2026-09-26)

The user supplied three black-on-transparent 512 px masters for the Level Editor's colour order buttons (`TASK-20260926-145804549-da16f02f-level-editor-color-order`):

| Master | Button |
|---|---|
| `source/color-switch.png` (two squares with arrows) | Color Switch |
| `source/color-shuffle.png` (crossing arrows) | Color Shuffle |
| `source/reset-order.png` (circular arrow) | Reset Order (removed 2026-09-27 on the user's order "reset order kaldır"; the master is kept, and the editor copy was deleted) |

The editor copies in `Assets/ColorBlockJam/Editor/Core/Levels/Icons` are white 64 px versions that keep the master's alpha, like the Level Editor tool icons, so the skin tint can colour them.
