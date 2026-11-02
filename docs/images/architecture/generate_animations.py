"""Render the README's three schematic animations with Pillow.

Run: python docs/images/architecture/generate_animations.py
Optional: --preview-dir <directory> writes contact sheets for visual review.
Use --only level-validation-bfs to rebuild just the grid search animation.
These are explanatory diagrams, not captured gameplay or solver traces.
"""

from argparse import ArgumentParser
from collections import deque
from pathlib import Path
import math

from PIL import Image, ImageDraw, ImageFont


WIDTH, HEIGHT, SCALE, FPS = 960, 440, 2, 10
BACKGROUND = "#0d1117"
SURFACE = "#161d27"
LINE = "#303b4c"
TEXT = "#e6edf3"
MUTED = "#98a7b9"
BLUE = "#58a6ff"
GREEN = "#62d6ab"
PURPLE = "#bc9cff"
RED = "#ff808b"
AMBER = "#e8bf6a"
CARD_X = (64, 368, 672)
CARD_Y, CARD_SIZE = 112, 224
OUTPUT = Path(__file__).resolve().parent
FONTS = {}


def rgb(color):
    return tuple(int(color[index:index + 2], 16) for index in (1, 3, 5))


def mix(first, second, amount):
    amount = max(0, min(1, amount))
    return tuple(round(a + (b - a) * amount) for a, b in zip(rgb(first), rgb(second)))


def font(size, bold=False):
    key = size, bold
    if key not in FONTS:
        filename = "segoeuib.ttf" if bold else "segoeui.ttf"
        candidates = [Path("C:/Windows/Fonts") / filename,
                      Path("/usr/share/fonts/truetype/dejavu") /
                      ("DejaVuSans-Bold.ttf" if bold else "DejaVuSans.ttf")]
        path = next((item for item in candidates if item.exists()), None)
        if path is None:
            raise FileNotFoundError("Install Segoe UI or DejaVu Sans to render the diagrams.")
        FONTS[key] = ImageFont.truetype(str(path), size * SCALE)
    return FONTS[key]


class Canvas:
    def __init__(self, title, subtitle):
        self.image = Image.new("RGB", (WIDTH * SCALE, HEIGHT * SCALE), BACKGROUND)
        self.draw = ImageDraw.Draw(self.image)
        self.text(32, 21, title, 28, bold=True)
        self.text(32, 62, subtitle, 16, MUTED)

    def rect(self, box, fill, outline=None, width=1):
        self.draw.rectangle(tuple(round(v * SCALE) for v in box), fill=fill,
                            outline=outline, width=width * SCALE)

    def text(self, x, y, value, size=18, color=TEXT, bold=False, center=False):
        face = font(size, bold)
        if center:
            x -= self.draw.textlength(value, font=face) / (2 * SCALE)
        self.draw.text((round(x * SCALE), round(y * SCALE)), value,
                       font=face, fill=color)

    def line(self, points, color=LINE, width=2):
        self.draw.line([(round(x * SCALE), round(y * SCALE)) for x, y in points],
                       fill=color, width=width * SCALE, joint="curve")

    def arrow(self, index, color=LINE):
        x = CARD_X[index] + CARD_SIZE + 17
        y = CARD_Y + CARD_SIZE // 2
        self.line([(x, y), (x + 43, y)], color)
        self.line([(x + 35, y - 7), (x + 43, y), (x + 35, y + 7)], color)

    def card(self, index, color=BLUE, intensity=0, heading=None):
        x = CARD_X[index]
        self.rect((x, CARD_Y, x + CARD_SIZE, CARD_Y + CARD_SIZE),
                  mix(SURFACE, color, intensity * .08),
                  mix(LINE, color, intensity), 2)
        if intensity:
            self.rect((x, CARD_Y, x + CARD_SIZE, CARD_Y + 3),
                      mix(LINE, color, intensity))
        if heading:
            self.text(x + CARD_SIZE / 2, CARD_Y + 17, heading, 18,
                      TEXT if intensity else MUTED, True, True)

    def footer(self, message, step, count, accent=BLUE):
        self.line([(32, 374), (928, 374)])
        self.text(32, 393, message, 17)
        for i in range(count):
            x = 928 - count * 15 + i * 15
            self.rect((x, 403, x + 7, 410), accent if i == step else LINE)

    def finish(self):
        return self.image.resize((WIDTH, HEIGHT), Image.Resampling.LANCZOS)


def stage_at(time, durations):
    elapsed = 0
    for index, duration in enumerate(durations):
        if time < elapsed + duration:
            return index, (time - elapsed) / duration
        elapsed += duration
    return len(durations) - 1, 1


BFS_GRID = (
    "...#..#..",
    "...#..#.B",
    "......#..",
    "...#..#..",
    "A..#.....",
    "...#.....",
)
BFS_START = (0, 4)
BFS_GOAL = (8, 1)
BFS_ORIGIN = (66, 108)
BFS_PITCH = 48
BFS_TILE = 42


def build_bfs_trace():
    """Run four-neighbor FIFO search; save each discovered cell's first parent."""
    queue = deque([BFS_START])
    parents = {BFS_START: None}
    depths = {BFS_START: 0}
    layers = []
    while queue:
        cell = queue.popleft()
        depth = depths[cell]
        if depth == len(layers):
            layers.append([])
        layers[depth].append(cell)
        if cell == BFS_GOAL:
            path = []
            while cell is not None:
                path.append(cell)
                cell = parents[cell]
            return layers, parents, list(reversed(path))
        x, y = cell
        for dx, dy in ((1, 0), (0, -1), (0, 1), (-1, 0)):
            neighbor = (x + dx, y + dy)
            nx, ny = neighbor
            if (0 <= ny < len(BFS_GRID) and 0 <= nx < len(BFS_GRID[ny])
                    and BFS_GRID[ny][nx] != "#" and neighbor not in parents):
                parents[neighbor] = cell
                depths[neighbor] = depth + 1
                queue.append(neighbor)
    raise ValueError("The example grid needs a path from A to B.")


BFS_LAYERS, BFS_PARENTS, BFS_PATH = build_bfs_trace()
BFS_DURATIONS = (1.3, (len(BFS_LAYERS) - 1) * .32, .7, 1.5, 3.4, 2.2)
BFS_DURATION = sum(BFS_DURATIONS)
BFS_POSTER_TIME = BFS_DURATION - 1


def grid_center(cell):
    x, y = cell
    return (BFS_ORIGIN[0] + x * BFS_PITCH + BFS_TILE / 2,
            BFS_ORIGIN[1] + y * BFS_PITCH + BFS_TILE / 2)


def bfs_tile(canvas, box, fill, outline=None, raised=False):
    """Simple square tiles, with a small bevel for walls and the moving block."""
    x0, y0, x1, y1 = box
    def rectangle(bounds, color, edge=None):
        canvas.draw.rounded_rectangle(tuple(round(v * SCALE) for v in bounds),
                                      radius=4 * SCALE, fill=color, outline=edge,
                                      width=SCALE)
    if raised:
        rectangle((x0, y0 + 4, x1, y1 + 4), mix(fill, BACKGROUND, .4))
    rectangle(box, fill, outline)
    if raised:
        canvas.line([(x0 + 6, y0 + 3), (x1 - 6, y0 + 3)], mix(fill, "#ffffff", .18), 1)


def bfs_piece(canvas, center, label=None):
    x, y = center
    bfs_tile(canvas, (x - 17, y - 18, x + 17, y + 16), "#d849ad", raised=True)
    if label:
        canvas.text(x, y - 15, label, 21, TEXT, True, True)
    else:
        canvas.draw.ellipse(((x - 7) * SCALE, (y - 9) * SCALE,
                             (x + 7) * SCALE, (y + 5) * SCALE),
                            fill="#ed74c5", outline="#a63285", width=SCALE)


def bfs_frame(time):
    stage, progress = stage_at(time, BFS_DURATIONS)
    canvas = Canvas("BFS · find a path from A to B",
                    "Explore nearby squares first. Go around walls. Follow the found path.")
    depth = (0 if stage == 0 else min(len(BFS_LAYERS) - 1,
             1 + int(progress * (len(BFS_LAYERS) - 1))) if stage == 1
             else len(BFS_LAYERS) - 1)
    explored = {cell for layer in BFS_LAYERS[:depth] for cell in layer}
    frontier = set(BFS_LAYERS[depth]) if stage < 3 else set()
    if stage >= 3:
        explored.update(BFS_LAYERS[-1])
    route = (BFS_PATH[-(1 + int(progress * (len(BFS_PATH) - 1))):]
             if stage == 3 else BFS_PATH if stage >= 4 else [])

    bfs_tile(canvas, (52, 94, 510, 406), "#202b3b", "#465469")
    for row, cells in enumerate(BFS_GRID):
        for col, value in enumerate(cells):
            cell = col, row
            x, y = grid_center(cell)
            fill, edge = "#313f54", "#41516a"
            if value == "#":
                fill, edge = "#727d8b", "#8792a0"
            elif cell in route:
                fill, edge = "#256451", GREEN
            elif cell in frontier:
                fill, edge = "#b08a32", AMBER
            elif cell in explored:
                fill, edge = "#255a80", "#4082af"
            bfs_tile(canvas, (x - BFS_TILE / 2, y - BFS_TILE / 2,
                              x + BFS_TILE / 2, y + BFS_TILE / 2),
                     fill, edge, raised=value == "#")
    if len(route) > 1:
        canvas.line([grid_center(cell) for cell in route], GREEN, 4)

    ax, ay = grid_center(BFS_START)
    bx, by = grid_center(BFS_GOAL)
    bfs_tile(canvas, (bx - 18, by - 18, bx + 18, by + 18), "#1b5045", GREEN)
    if stage < 4:
        bfs_piece(canvas, (ax, ay), "A")
    else:
        canvas.text(ax, ay - 15, "A", 21, TEXT, True, True)
        step = progress * (len(BFS_PATH) - 1) if stage == 4 else len(BFS_PATH) - 1
        index = min(int(step), len(BFS_PATH) - 2)
        amount = step - index
        first, second = grid_center(BFS_PATH[index]), grid_center(BFS_PATH[index + 1])
        position = tuple(a + (b - a) * amount for a, b in zip(first, second))
        bfs_piece(canvas, position)
    canvas.text(bx, by - 15, "B", 21, TEXT, True, True)

    headings = ("Start at A", "Explore the grid", "B reached", "Trace the route",
                "Move from A to B", "Path found")
    descriptions = (("Find B without crossing", "the gray walls."),
                    (f"Search distance: {depth} grid steps", "Already explored? Skip it."),
                    ("The search found the target.", "Now recover the route."),
                    ("Follow saved parent links", "from B back to A."),
                    ("The block follows the route", "around the obstacles."),
                    (f"{len(BFS_PATH) - 1} grid steps from A to B.", "Search → trace → move."))
    canvas.text(554, 113, f"{stage + 1:02d} / BFS", 14, GREEN if stage >= 3 else BLUE, True)
    canvas.text(554, 142, headings[stage], 27, bold=True)
    for i, description in enumerate(descriptions[stage]):
        canvas.text(554, 188 + i * 25, description, 18, MUTED)
    canvas.line([(554, 250), (920, 250)])
    for i, (label, color) in enumerate((("Explored", "#4082af"),
                                       ("Current search layer", AMBER),
                                       ("Found path", GREEN), ("Wall", "#8792a0"))):
        y = 271 + i * 31
        bfs_tile(canvas, (554, y + 2, 570, y + 18), color)
        canvas.text(584, y - 2, label, 17, MUTED)
    canvas.text(54, 415, "A: start     B: target     ·     Illustrative grid search", 13, MUTED)
    return canvas.finish()


def onity_frame(time):
    stage, progress = stage_at(time, (1.3, 1.3, 2.0, 1.3, .8, 1.5, 2.8))
    canvas = Canvas("Onity lifetimes · restart the attempt",
                    "App and scene containers stay alive. LevelSession owns each attempt.")
    colors = (BLUE, PURPLE, GREEN)
    headings = ("Application", "Game scene", "Attempt")
    intensity = (1, 1 if stage >= 1 else 0, 1 if stage >= 2 else 0)
    attempt_opacity = 1
    if stage == 3:
        attempt_opacity = 1 - progress
    elif stage == 4:
        attempt_opacity = 0
    elif stage == 5:
        attempt_opacity = min(1, progress * 2)
    for i in range(3):
        amount = intensity[i] * (attempt_opacity if i == 2 else 1)
        canvas.card(i, colors[i], amount, headings[i])
        label = ("APP", "SCENE", "02" if stage >= 5 else "01")[i]
        canvas.text(CARD_X[i] + 112, 182, label, 40,
                    mix(LINE, colors[i], amount), True, True)
        name = ("ProjectContext", "SceneContext", "LevelAttempt")[i]
        canvas.text(CARD_X[i] + 112, 261, name, 19, center=True)
        detail = ("App scope", "Child scope", "Owned by LevelSession")[i]
        canvas.text(CARD_X[i] + 112, 295, detail, 14, MUTED, center=True)
    canvas.arrow(0, PURPLE if stage >= 1 else LINE)
    canvas.arrow(1, GREEN if stage >= 2 and stage != 4 else LINE)
    messages = ("Startup: create the application scope.",
                "Open Game: create its child scene scope.",
                "LevelSession creates attempt 01.",
                "Restart: cancel and dispose attempt 01.",
                "The old attempt has ended; app and scene remain.",
                "Create attempt 02 with fresh board state.",
                "New attempt. Same app services and committed player data.")
    canvas.footer(messages[stage], stage, 7, GREEN)
    return canvas.finish()


def stock(canvas, index, value, color, strength=1):
    x = CARD_X[index]
    for i in range(3):
        left = x + 47 + i * 47
        filled = i < value
        canvas.rect((left, 195, left + 35, 230),
                    mix(SURFACE, color, .8 * strength) if filled else SURFACE,
                    mix(LINE, color, strength) if filled else LINE, 2)


def save_frame(time):
    stage, progress = stage_at(time, (1.8, 2.0, .9, 2.5, 1.4, 1.4, 1.8, 2.6))
    success = stage in (2, 3)
    published = stage == 3
    failure_example = stage >= 4
    writing = stage in (1, 6)
    failed = stage == 7
    prepare = stage in (0, 5)
    canvas = Canvas("Save first. Publish after success.",
                    "Illustrative booster stock · " +
                    ("separate failure example" if failure_example else "successful save example"))
    amounts = (.95 if prepare else .4, 1 if writing or success or failed else 0,
               1 if published else 0)
    if stage == 4:
        amounts = (0, 0, 0)
    for i, heading in enumerate(("1 · Prepare", "2 · Save", "3 · Publish")):
        color = RED if i == 1 and failed else GREEN if success else BLUE
        canvas.card(i, color, amounts[i], heading)
    canvas.arrow(0, BLUE if writing or success or failed else LINE)
    canvas.arrow(1, GREEN if published else LINE)
    stock(canvas, 0, 2, BLUE, .25 if stage == 4 else 1)
    stock(canvas, 2, 2 if published else 3, GREEN if published else BLUE,
          1 if published else .3)
    canvas.text(176, 276, "Candidate: 2", 18, MUTED if stage == 4 else TEXT, center=True)
    canvas.text(784, 276, "Committed: " + ("2" if published else "3"), 18,
                GREEN if published else TEXT, True, True)
    if writing:
        lit = int(progress * 8) % 3
        for i in range(3):
            x = 424 + i * 41
            canvas.rect((x, 205, x + 28, 233), BLUE if i == lit else LINE)
        label = "Writing…"
    elif success:
        canvas.line([(457, 216), (474, 232), (507, 195)], GREEN, 5)
        label = "Saved"
    elif failed:
        canvas.line([(462, 198), (498, 234)], RED, 5)
        canvas.line([(498, 198), (462, 234)], RED, 5)
        label = "Write failed"
    else:
        canvas.rect((461, 195, 499, 233), SURFACE, LINE, 2)
        label = "Storage"
    canvas.text(480, 276, label, 18, RED if failed else GREEN if success else MUTED,
                center=True)
    messages = ("Prepare stock 2. Committed stock stays at 3.",
                "Write the candidate snapshot. Keep committed stock at 3.",
                "Storage succeeded. The new snapshot can now be published.",
                "Publish stock 2 and notify subscribers.",
                "Separate example: start again with committed stock 3.",
                "Prepare another candidate with stock 2.",
                "Try writing the candidate. Publish still waits.",
                "Write failed: keep stock 3. No publish, no booster effect.")
    canvas.footer(messages[stage], stage, 8, RED if failed else GREEN if published else BLUE)
    return canvas.finish()


ANIMATIONS = (
    ("level-validation-bfs", bfs_frame, BFS_DURATION, BFS_POSTER_TIME,
     (0, 2.6, 5, 6.5, 8, BFS_POSTER_TIME)),
    ("onity-lifetimes", onity_frame, 11, 9, (0, 1.5, 3, 5, 6.2, 9)),
    ("player-save-flow", save_frame, 14.4, 6, (0, 2.5, 4, 6, 8, 13)),
)


def render(name, draw_frame, duration, poster_time, sample_times, preview_dir):
    frames = [draw_frame(index / FPS) for index in range(round(duration * FPS))]
    samples = [draw_frame(time) for time in sample_times]
    palette_sheet = Image.new("RGB", (WIDTH, HEIGHT * len(samples)))
    for i, frame in enumerate(samples):
        palette_sheet.paste(frame, (0, i * HEIGHT))
    palette = palette_sheet.quantize(colors=128, method=Image.Quantize.MEDIANCUT)
    indexed = [frame.quantize(palette=palette, dither=Image.Dither.NONE) for frame in frames]
    path = OUTPUT / f"{name}.gif"
    indexed[0].save(path, save_all=True, append_images=indexed[1:], duration=1000 // FPS,
                    loop=0, optimize=True, disposal=1)
    draw_frame(poster_time).save(OUTPUT / f"{name}-poster.png", optimize=True)
    if preview_dir:
        preview_dir.mkdir(parents=True, exist_ok=True)
        sheet = Image.new("RGB", (WIDTH, HEIGHT * math.ceil(len(samples) / 2)), BACKGROUND)
        for i, frame in enumerate(samples):
            thumbnail = frame.resize((WIDTH // 2, HEIGHT // 2), Image.Resampling.LANCZOS)
            sheet.paste(thumbnail, ((i % 2) * WIDTH // 2, (i // 2) * HEIGHT // 2))
        sheet = sheet.crop((0, 0, WIDTH, HEIGHT // 2 * math.ceil(len(samples) / 2)))
        sheet.save(preview_dir / f"{name}-contact.png")
    print(f"{path.name}: {duration:g}s, {path.stat().st_size / 1024:.0f} KiB")


if __name__ == "__main__":
    parser = ArgumentParser(description=__doc__)
    parser.add_argument("--preview-dir", type=Path)
    parser.add_argument("--only", choices=[animation[0] for animation in ANIMATIONS])
    arguments = parser.parse_args()
    for animation in ANIMATIONS:
        if arguments.only is None or animation[0] == arguments.only:
            render(*animation, arguments.preview_dir)
