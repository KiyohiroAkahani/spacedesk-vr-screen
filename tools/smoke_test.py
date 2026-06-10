#!/usr/bin/env python3
"""One-command smoke test: stop -> clean build -> 6 s timeboxed run -> verdict.

Automates the manual verification dance documented in the workflows
(launch the exe, wait, kill, grep stderr). PASS criteria:
  * build exit code 0
  * stderr contains "[INFO] LensDistortion="  (device + ALL shaders compiled,
    renderer initialized — this line is only printed after Initialize())
  * stderr contains "[DIAG]"                  (render/diag loop actually ticked)
  * stderr contains no "[FATAL]"              (no unhandled exception)
  * the process did not die before the timebox elapsed

Usage:
    py -3 tools/smoke_test.py [--config Debug|Release] [--seconds 6]
                              [--no-build]

Exit codes:
    0  smoke PASS
    1  smoke FAIL (build error, crash, or missing health markers)
    2  setup error (exe not found, etc.)
"""

from __future__ import annotations

import argparse
import datetime as _dt
import subprocess
import sys
import time
from pathlib import Path

from _proc import kill_app

REPO_ROOT = Path(__file__).resolve().parent.parent
TMP_DIR = REPO_ROOT / ".tmp"
APP_NAME = "VrDesktopBridge.exe"


def _fail(msg: str, code: int = 2) -> None:
    print(f"ERROR: {msg}", file=sys.stderr)
    raise SystemExit(code)


def find_exe(config: str) -> Path:
    """Locate the freshly built exe under src/**/bin/<config>/ (not dist/)."""
    hits = [
        p for p in (REPO_ROOT / "src").rglob(APP_NAME)
        if "obj" not in p.parts and f"{config}" in p.parts
    ]
    if not hits:
        _fail(f"built exe not found under src/**/bin/{config}/. "
              "Run clean_build first.")
    # Prefer the most recently written one (multiple TFMs are unlikely).
    return max(hits, key=lambda p: p.stat().st_mtime)


def main() -> int:
    ap = argparse.ArgumentParser(description=__doc__)
    ap.add_argument("--config", default="Debug",
                    choices=["Debug", "Release"])
    ap.add_argument("--seconds", type=float, default=6.0,
                    help="timebox for the app run (default 6)")
    ap.add_argument("--no-build", action="store_true",
                    help="skip clean_build (use the existing binaries)")
    args = ap.parse_args()

    kill_app()  # never build/run against a stale locked instance

    if not args.no_build:
        rc = subprocess.run(
            [sys.executable, str(REPO_ROOT / "tools" / "clean_build.py"),
             "--config", args.config],
        ).returncode
        if rc != 0:
            print(f"SMOKE FAIL: clean_build exited {rc}")
            return 1

    exe = find_exe(args.config)
    TMP_DIR.mkdir(exist_ok=True)
    stamp = _dt.datetime.now().strftime("%Y%m%d_%H%M%S")
    log_path = TMP_DIR / f"smoke_{stamp}.log"

    print(f"Launching {exe.relative_to(REPO_ROOT)} for {args.seconds:g}s ...")
    early_exit: int | None = None
    with open(log_path, "wb") as log:
        proc = subprocess.Popen([str(exe)], cwd=str(REPO_ROOT),
                                stdout=log, stderr=subprocess.STDOUT)
        deadline = time.monotonic() + args.seconds
        while time.monotonic() < deadline:
            if proc.poll() is not None:
                early_exit = proc.returncode
                break
            time.sleep(0.2)
    kill_app()  # also restores cursors via _proc safety net

    text = log_path.read_text(encoding="utf-8", errors="replace")
    problems: list[str] = []
    if early_exit is not None:
        problems.append(f"process exited early (code {early_exit})")
    if "[FATAL]" in text:
        problems.append("found [FATAL] in stderr")
    if "[INFO] LensDistortion=" not in text:
        problems.append("renderer never initialized "
                        "(no '[INFO] LensDistortion=' line — device or "
                        "shader-compile failure; check for a MessageBox)")
    if "[DIAG]" not in text:
        problems.append("no [DIAG] tick (render/diag loop never ran)")

    # Surface the interesting lines either way.
    for ln in text.splitlines():
        if ln.startswith(("[MON]", "[INFO]", "[WARN]", "[FATAL]")):
            print("  " + ln)
    print(f"Full log: {log_path.relative_to(REPO_ROOT)}")

    if problems:
        print("SMOKE FAIL: " + "; ".join(problems))
        return 1
    print(f"SMOKE PASS ({args.config}, {args.seconds:g}s, no FATAL, "
          "renderer + diag loop healthy)")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
