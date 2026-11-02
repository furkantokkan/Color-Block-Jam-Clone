"""Runs one scripted drag session on the connected Android phone and summarizes the CBJPERF probe lines.

Needs a development APK built by device_build.ps1 (it carries the copy-only DevPerfProbe). Steps: optional install,
launch, wait for the probe's block list, idle, lead-only drags (no cell step) with the outline alternating on/off
per press, cell-step drags, idle again. Every swipe is one touch (down, move for the duration, up).

Usage:
  python device_session.py [--apk path.apk] [--out perf_lines.txt] [--swipes 6] [--no-launch]

Prints a table per group (idle, held outline on, held outline off, step drags) with fps, frame-time percentiles,
counts of frames over 20 ms / 34 ms, main thread, GPU, GC bytes per frame, SetPass and batches.
"""
import argparse
import os
import re
import subprocess
import sys
import threading
import time

PACKAGE = "com.furkantokkan.colorblockjamcase2026"
ACTIVITY = "com.unity3d.player.UnityPlayerActivity"


def find_adb():
    program_files = os.environ.get("ProgramFiles", r"C:\Program Files")
    candidate = os.path.join(program_files, r"Unity\Hub\Editor\2022.3.62f2\Editor\Data\PlaybackEngines\AndroidPlayer\SDK\platform-tools\adb.exe")
    return candidate if os.path.exists(candidate) else "adb"


ADB = find_adb()


def adb(*args, check=True, timeout=120):
    # dumpsys output is UTF-8; the Windows console code page (cp1254 here) cannot decode it.
    result = subprocess.run([ADB, *args], capture_output=True, text=True, encoding="utf-8", errors="replace", timeout=timeout)
    if check and result.returncode != 0:
        raise SystemExit(f"adb {' '.join(args)} failed: {(result.stderr or '').strip() or (result.stdout or '').strip()}")
    return result.stdout or ""


class LogReader(threading.Thread):
    """Collects CBJPERF lines from logcat while the session runs."""

    def __init__(self):
        super().__init__(daemon=True)
        self.lines = []
        self.process = subprocess.Popen([ADB, "logcat", "-v", "brief", "Unity:I", "*:S"], stdout=subprocess.PIPE,
                                        stderr=subprocess.DEVNULL, text=True, encoding="utf-8", errors="replace")

    def run(self):
        for line in self.process.stdout:
            if "CBJPERF" in line:
                self.lines.append((time.time(), line[line.index("CBJPERF"):].strip()))

    def stop(self):
        self.process.terminate()


BLOCK = re.compile(r"(?P<name>[^|]+?) adb=\((?P<x>\d+),(?P<y>\d+)\) sizePx=\((?P<w>\d+)x(?P<h>\d+)\) renderers=(?P<r>\d+)(?: cellPx=(?P<cell>[\d.]+))?")
WINDOW = re.compile(r"CBJPERF window held=(?P<held>\w+) outline=(?P<outline>\S+) frames=(?P<frames>\d+) fps=(?P<fps>[\d.]+) dtAvg=(?P<dt>[\d.]+) p95=(?P<p95>[\d.]+) max=(?P<max>[\d.]+) over20=(?P<o20>\d+) over34=(?P<o34>\d+) main=(?P<main>[-\d.]+) cpuFrame=(?P<cpu>[-\d.]+) gpu=(?P<gpu>[-\d.]+) gcB=(?P<gc>\d+) gcN=(?P<gcn>[\d.]+) setpass=(?P<sp>\d+) batches=(?P<b>\d+) tris=(?P<t>\d+)")


def wait_for(reader, prefix, timeout):
    deadline = time.time() + timeout
    while time.time() < deadline:
        for _, line in reader.lines:
            if line.startswith(prefix):
                return line
        time.sleep(0.5)
    return None


def swipe(x1, y1, x2, y2, ms, shot_path=None):
    """One touch: down, move for ms, up. With shot_path, screenshots the screen while the finger is still down."""
    args = [ADB, "shell", "input", "swipe", str(int(x1)), str(int(y1)), str(int(x2)), str(int(y2)), str(int(ms))]
    if shot_path is None:
        subprocess.run(args, capture_output=True, timeout=ms / 1000 + 30)
        return
    process = subprocess.Popen(args, stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL)
    time.sleep(min(0.8, ms / 2000))
    with open(shot_path, "wb") as handle:
        handle.write(subprocess.run([ADB, "exec-out", "screencap", "-p"], capture_output=True, timeout=30).stdout)
    process.wait(timeout=ms / 1000 + 30)


def tap_sequence(taps, launch_wait):
    """Hub navigation for Init-first builds: 'x,y;x,y' in device pixels, 3 s apart, after launch_wait seconds."""
    time.sleep(launch_wait)
    for pair in [p for p in taps.split(";") if p.strip()]:
        x, y = (int(v) for v in pair.split(","))
        adb("shell", "input", "tap", str(x), str(y))
        time.sleep(3)


def summarize(reader, phases):
    groups = {}
    for stamp, line in reader.lines:
        match = WINDOW.match(line)
        if not match:
            continue
        d = match.groupdict()
        phase = "?"
        for name, start, end in phases:
            if start <= stamp <= end + 1.0:
                phase = name
        if d["held"] == "True":
            key = f"{phase} outline {d['outline']}"
        else:
            key = f"{phase} not touching"
        groups.setdefault(key, []).append(d)
    print(f"{'group':34} {'win':>3} {'frames':>6} {'fps':>5} {'dtAvg':>6} {'p95':>6} {'max':>6} {'>20ms':>5} {'>34ms':>5} {'main':>5} {'gpu':>5} {'gcB':>5} {'setp':>4} {'batch':>5}")
    for key, rows in groups.items():
        frames = sum(int(r["frames"]) for r in rows)
        if frames == 0:
            continue

        def weighted(field):
            return sum(float(r[field]) * int(r["frames"]) for r in rows) / frames

        print(f"{key:34} {len(rows):>3} {frames:>6} {1000 / weighted('dt'):>5.1f} {weighted('dt'):>6.2f} {max(float(r['p95']) for r in rows):>6.2f} "
              f"{max(float(r['max']) for r in rows):>6.2f} {sum(int(r['o20']) for r in rows):>5} {sum(int(r['o34']) for r in rows):>5} "
              f"{weighted('main'):>5.2f} {weighted('gpu'):>5.2f} {weighted('gc'):>5.0f} {weighted('sp'):>4.0f} {weighted('b'):>5.0f}")


CONFIG = re.compile(r"CBJPERF config=(?P<name>\S+) frames=(?P<frames>\d+) fps=(?P<fps>[\d.]+) dtAvg=(?P<dt>[\d.]+) p95=(?P<p95>[\d.]+) over20=(?P<o20>\d+) gpu=(?P<gpu>[-\d.]+) main=(?P<main>[-\d.]+) setpass=(?P<sp>\d+)")


def run_breakdown(reader, out_path):
    """Waits for the DevGpuBreakdown probe to finish its configurations and prints them against the baseline."""
    start = wait_for(reader, "CBJPERF config start", 90)
    print(start or "no 'config start' line (is this a DevGpuBreakdown build?)")
    done = wait_for(reader, "CBJPERF config done", 150)
    reader.stop()
    with open(out_path, "w", encoding="utf-8") as handle:
        for stamp, line in reader.lines:
            handle.write(f"{stamp:.2f} {line}\n")
    rows = [m.groupdict() for m in (CONFIG.match(line) for _, line in reader.lines) if m]
    if not done:
        print("the probe did not finish; partial results:")
    baseline = next((float(r["dt"]) for r in rows if r["name"] == "baseline"), None)
    print(f"{'config':24} {'fps':>5} {'dtAvg':>6} {'p95':>6} {'>20ms':>5} {'gpu':>6} {'main':>6} {'setp':>4} {'vs baseline':>11}")
    for r in rows:
        delta = f"{float(r['dt']) - baseline:+.2f} ms" if baseline is not None else ""
        print(f"{r['name']:24} {float(r['fps']):>5.1f} {float(r['dt']):>6.2f} {float(r['p95']):>6.2f} {r['o20']:>5} {float(r['gpu']):>6.2f} {float(r['main']):>6.2f} {r['sp']:>4} {delta:>11}")


def main():
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument("--apk")
    parser.add_argument("--out", default="cbjperf_lines.txt")
    parser.add_argument("--swipes", type=int, default=6)
    parser.add_argument("--no-launch", action="store_true")
    parser.add_argument("--ignore-call", action="store_true", help="run although telecom lists a call (user approved)")
    parser.add_argument("--mode", choices=["drag", "breakdown"], default="drag",
                        help="drag: DevPerfProbe build, scripted swipes; breakdown: DevGpuBreakdown build, no touches")
    parser.add_argument("--taps", default="", help="Init-first builds: 'x,y;x,y' taps that open the level from Hub")
    parser.add_argument("--launch-wait", type=float, default=10.0, help="seconds between launch and the first tap")
    parser.add_argument("--shots", help="folder for one screenshot per lead-drag press, taken while the finger is down")
    options = parser.parse_args()

    devices = [l for l in adb("devices").splitlines()[1:] if l.strip().endswith("device")]
    if not devices:
        raise SystemExit("No Android device in 'adb devices'. Connect the phone with USB debugging allowed.")
    print("device:", adb("shell", "getprop", "ro.product.model").strip(), "|", adb("shell", "wm", "size").strip())
    # The phone belongs to a person: never take the screen during a call or behind a lock screen.
    # The audio mode is the reliable call signal: "dumpsys telecom" also prints its history ("Enter RINGING" lines
    # from hours ago), which a state regex mistakes for a live call.
    audio = adb("shell", "dumpsys audio", check=False)
    in_call = re.search(r"Actual mode = MODE_(IN_CALL|IN_COMMUNICATION|RINGTONE|CALL_SCREENING)", audio)
    if in_call and not options.ignore_call:
        raise SystemExit("A call is active on the phone. Wait until it ends (or pass --ignore-call only when the "
                         "user said to go ahead); nothing was installed or launched.")
    if "isKeyguardShowing=true" in adb("shell", "dumpsys window", check=False):
        raise SystemExit("The phone is locked. Ask the user to unlock it; nothing was installed or launched.")
    if options.apk:
        print("installing", options.apk)
        adb("install", "-r", "-g", options.apk, timeout=600)

    adb("logcat", "-c")
    reader = LogReader()
    reader.start()
    if not options.no_launch:
        adb("shell", "am", "force-stop", PACKAGE)
        adb("shell", "am", "start", "-n", f"{PACKAGE}/{ACTIVITY}")

    if options.mode == "breakdown":
        run_breakdown(reader, options.out)
        return

    device_line = wait_for(reader, "CBJPERF device", 60)
    print(device_line or "no device line from the probe (is this a DevPerfProbe build?)")
    if options.taps:
        tap_sequence(options.taps, options.launch_wait)
    blocks_line = wait_for(reader, "CBJPERF blocks", 90)
    if not blocks_line:
        reader.stop()
        raise SystemExit("The probe logged no blocks within 90 s.")
    blocks = [m.groupdict() for m in BLOCK.finditer(blocks_line.split("|", 1)[1])]
    target = max(blocks, key=lambda b: int(b["r"]))
    cell = float(target["cell"]) if target.get("cell") else min(min(int(b["w"]), int(b["h"])) for b in blocks)
    x, y = int(target["x"]), int(target["y"])
    print(f"target block {target['name'].strip()} at ({x},{y}), about {cell:.0f} px per cell, {len(blocks)} blocks")

    time.sleep(3)  # let the opening dim and hold finish (DEC-166)
    phases = []
    start = time.time(); time.sleep(4); phases.append(("idle", start, time.time()))
    start = time.time()
    lead = 0.45 * cell
    if options.shots:
        os.makedirs(options.shots, exist_ok=True)
    for i in range(options.swipes):
        direction = 1 if i % 2 == 0 else -1
        shot = os.path.join(options.shots, f"press_{i + 1}.png") if options.shots else None
        swipe(x, y, x + direction * lead, y, 1500, shot)
        time.sleep(0.6)
    phases.append(("lead drag", start, time.time()))
    start = time.time()
    for i in range(4):
        direction = 1 if i % 2 == 0 else -1
        swipe(x, y, x + direction * 1.4 * cell, y, 1500)
        time.sleep(0.6)
    phases.append(("step drag", start, time.time()))
    start = time.time(); time.sleep(4); phases.append(("idle after", start, time.time()))
    time.sleep(2)
    reader.stop()

    with open(options.out, "w", encoding="utf-8") as handle:
        for stamp, line in reader.lines:
            handle.write(f"{stamp:.2f} {line}\n")
    print(f"{len(reader.lines)} probe lines saved to {options.out}")
    summarize(reader, phases)


if __name__ == "__main__":
    main()
