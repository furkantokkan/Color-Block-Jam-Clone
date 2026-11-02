"""Build a README evidence figure from supplied files and an archived Unity capture.

Run with --archive pointing to drive-download-20260924T081806Z-1-001.zip.
No Unity asset or source screenshot is modified.
"""

import argparse
import hashlib
import json
import re
from pathlib import Path
from zipfile import ZipFile

from PIL import Image, ImageDraw, ImageFont


ROOT = Path(__file__).resolve().parents[3]
HERE = Path(__file__).resolve().parent
UI = ROOT / "Assets/ColorBlockJam/Art/UI/Supplied"
USS = ROOT / "Assets/ColorBlockJam/UI/Settings/SettingsPopup.uss"
CAPTURE = ROOT / "docs/verification/hub-settings/settings-1080x1920.png"
ARCHIVE_HASH = "861df8ed9fc4765df3db7cdaabcb38ca2d3bda6f3efd71baf65cd5dad9e03664"
INK = "#182B45"
MUTED = "#51657E"
BLUE = "#155DAD"


def sha256(data):
    return hashlib.sha256(data).hexdigest()


def font(size, bold=False, mono=False):
    name = "consola.ttf" if mono else "segoeuib.ttf" if bold else "segoeui.ttf"
    return ImageFont.truetype(str(Path("C:/Windows/Fonts") / name), size)


def build(archive):
    archive_bytes = archive.read_bytes()
    assert sha256(archive_bytes) == ARCHIVE_HASH, "The delivery archive differs from the source audit."
    with ZipFile(archive) as source:
        entries = sorted(item.filename for item in source.infolist() if not item.is_dir())
        pngs = [name for name in entries if name.lower().endswith(".png")]
        metas = [name for name in entries if name.lower().endswith(".meta")]
        other_files = [name for name in entries if Path(name).suffix.lower() not in {".png", ".jpg", ".fbx"}]
        toggles = [name for name in pngs if "toggle" in Path(name).name.lower()]
        assert len(pngs) == 47 and not metas and not other_files
        assert toggles == ["UI/btn_toggle_on.png"]
        for entry, current in (("UI/btn_toggle_on.png", UI / "Buttons/btn_toggle_on.png"),
                               ("UI/bg_popup.png", UI / "Panels/bg_popup.png")):
            assert source.read(entry) == current.read_bytes(), f"Source art changed: {entry}"

    styles = USS.read_text(encoding="utf-8-sig")
    popup = re.search(r"\.settings-popup\s*\{([^}]+)\}", styles).group(1)
    borders = {edge: int(value) for edge, value in
               re.findall(r"-unity-slice-(left|top|right|bottom):\s*(\d+)", popup)}
    assert borders == {"left": 106, "top": 74, "right": 107, "bottom": 104}
    meta = (UI / "Panels/bg_popup.png.meta").read_text(encoding="utf-8-sig")
    assert "spriteBorder: {x: 106, y: 104, z: 107, w: 74}" in meta
    off_rule = re.search(r"\.settings-toggle__knob-off\s*\{([^}]+)\}", styles).group(1)
    assert "background-color: rgb(190, 212, 240)" in off_rule
    assert "border-top-left-radius: 52px" in off_rule
    assert "background-image" not in off_rule

    image = Image.new("RGB", (1600, 950), "#F0F4F8")
    draw = ImageDraw.Draw(image)

    def text(x, y, value, size=22, color=INK, bold=False, mono=False):
        draw.multiline_text((x, y), value, font=font(size, bold, mono), fill=color, spacing=8)

    def paste(source_image, bounds):
        x, y, width, height = bounds
        source_image = source_image.convert("RGBA")
        source_image.thumbnail((width, height), Image.Resampling.LANCZOS)
        position = (x + (width - source_image.width) // 2, y + (height - source_image.height) // 2)
        image.paste(source_image, position, source_image)
        return position, source_image.size

    text(44, 28, "UI setup and missing OFF toggle", 42, bold=True)
    text(44, 88, "Original delivery, project slice settings, and a recorded Unity Settings screenshot", 23, MUTED)
    for x in (40, 560, 1080):
        draw.rounded_rectangle((x, 150, x + 480, 850), radius=20, fill="white", outline="#D5DFEB", width=2)

    text(64, 174, "1. ORIGINAL DELIVERY", 25, BLUE, bold=True)
    text(64, 223, "ZIP inventory, read directly", 22, MUTED)
    for y, count, label in ((282, "47", "UI PNG files"),
                            (350, "0", "Unity .meta files"),
                            (418, "0", "setup / guidance files")):
        text(66, y, count, 37, BLUE, bold=True)
        text(134, y + 10, label, 22)
    text(64, 503, "Only toggle image supplied:", 22)
    text(64, 538, "btn_toggle_on.png", 22, BLUE, mono=True)
    paste(Image.open(UI / "Buttons/btn_toggle_on.png"), (174, 579, 200, 192))
    text(64, 795, "No OFF toggle image in the ZIP.", 22, bold=True)

    text(584, 174, "2. PROJECT SLICE BORDERS", 24, BLUE, bold=True)
    text(584, 223, "bg_popup.png   |   976 x 976 px", 22, MUTED)
    panel = Image.open(UI / "Panels/bg_popup.png")
    (px, py), (pw, ph) = paste(panel, (650, 287, 300, 300))
    for x in (px + pw * borders["left"] / panel.width,
              px + pw * (1 - borders["right"] / panel.width)):
        draw.line((x, py, x, py + ph), fill="#EF4A72", width=3)
    for y in (py + ph * borders["top"] / panel.height,
              py + ph * (1 - borders["bottom"] / panel.height)):
        draw.line((px, y, px + pw, y), fill="#EF4A72", width=3)
    text(735, 253, "Top: 74", 20, BLUE)
    text(584, 428, "106", 21, BLUE)
    text(971, 428, "107", 21, BLUE)
    text(708, 605, "Bottom: 104", 20, BLUE)
    text(584, 657, "Defined in SettingsPopup.uss:", 21)
    text(584, 698, "-unity-slice-left: 106;\n-unity-slice-top: 74;\n-unity-slice-right: 107;\n-unity-slice-bottom: 104;", 19, mono=True)

    text(1104, 174, "3. OFF CREATED IN USS", 25, BLUE, bold=True)
    text(1104, 223, "Crops from the Unity capture", 22, MUTED)
    capture = Image.open(CAPTURE)
    assert capture.size == (1080, 1920)
    text(1104, 272, "OFF  /  USS knob and track", 22, bold=True)
    paste(capture.crop((526, 350, 765, 482)), (1121, 312, 390, 205))
    text(1104, 550, "ON  /  supplied PNG knob", 22, bold=True)
    paste(capture.crop((526, 500, 765, 632)), (1121, 590, 390, 205))
    text(1104, 812, "Actual rendered states; no redraw.", 20, MUTED)

    text(44, 875, "Source ZIP: " + archive.name, 18, MUTED)
    text(44, 908, "Recorded capture: docs/verification/hub-settings/settings-1080x1920.png", 18, MUTED)
    output = HERE / "ui-setup-and-toggle.png"
    image.save(output, optimize=True)
    receipt = {
        "archive": archive.name,
        "archive_sha256": ARCHIVE_HASH,
        "archive_entries": entries,
        "ui_png_count": len(pngs),
        "unity_meta_count": len(metas),
        "non_image_model_files": other_files,
        "toggle_pngs": toggles,
        "slice_source": USS.relative_to(ROOT).as_posix(),
        "popup_slice_borders": borders,
        "source_capture": CAPTURE.relative_to(ROOT).as_posix(),
        "source_capture_sha256": sha256(CAPTURE.read_bytes()),
        "capture_crops": {"off": [526, 350, 765, 482], "on": [526, 500, 765, 632]},
        "figure": output.relative_to(ROOT).as_posix(),
    }
    (HERE / "ui-setup-and-toggle-evidence.json").write_text(json.dumps(receipt, indent=2) + "\n", encoding="utf-8")
    print(json.dumps({"figure": str(output), "archive_verified": True,
                      "ui_pngs": len(pngs), "meta_files": len(metas), "toggle_pngs": toggles}))


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--archive", type=Path, required=True)
    build(parser.parse_args().archive.resolve())
