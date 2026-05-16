#!/usr/bin/env python3
"""Launch the WPF SBS-mirror app via `dotnet run`.

Resolves the project .csproj (a .sln cannot be `dotnet run`), then runs
it, streaming output to the console and to .tmp/run_<stamp>.log.

This is a GUI app: the command BLOCKS until the window closes. Stop it
with Ctrl+C here (kills the app cleanly), `py -3 tools/stop_app.py`, or
the global hotkey Ctrl+Alt+Shift+Q. Any stale instance is killed before
launch so the freshly built code always runs.

Usage:
    py -3 .\\tools\\run_app.py [--config Debug|Release]
        [--target PATH] [--no-build]

Exit codes:
    0  app ran and exited cleanly
    1  `dotnet run` returned non-zero (build or runtime failure)
    2  setup error (dotnet missing, no/ambiguous project, bad args)
"""

from __future__ import annotations

import argparse
import datetime as _dt
import subprocess
import sys
from pathlib import Path

from _dotnet import find_dotnet
from _proc import kill_app

REPO_ROOT = Path(__file__).resolve().parent.parent
TMP_DIR = REPO_ROOT / ".tmp"


def _fail(msg: str, code: int = 2) -> "NoReturn":  # type: ignore[name-defined]
    print(f"ERROR: {msg}", file=sys.stderr)
    raise SystemExit(code)


def find_csproj(explicit: str | None) -> Path:
    """Return the .csproj to run, or exit with a clear message."""
    if explicit:
        p = (REPO_ROOT / explicit) if not Path(explicit).is_absolute() else Path(explicit)
        p = p.resolve()
        if not p.exists():
            _fail(f"--target not found: {p}")
        if p.suffix != ".csproj":
            _fail(f"--target must be a .csproj (got {p.suffix or 'no suffix'}); "
                  f"`dotnet run` cannot take a .sln.")
        return p

    csprojs = [c for c in sorted(REPO_ROOT.rglob("*.csproj"))
               if ".tmp" not in c.parts]
    if len(csprojs) == 1:
        return csprojs[0]
    if len(csprojs) == 0:
        _fail("No .csproj found. Scaffold the project first:\n"
              "  py -3 .\\tools\\scaffold_project.py")
    listing = "\n  ".join(str(c.relative_to(REPO_ROOT)) for c in csprojs)
    _fail(f"Multiple .csproj files found; pass --target:\n  {listing}")


def main() -> None:
    parser = argparse.ArgumentParser(description="Run the WPF SBS-mirror app.")
    parser.add_argument("--config", default="Debug", choices=["Debug", "Release"])
    parser.add_argument("--target", default=None,
                        help="Path to the .csproj (relative to repo root). "
                             "Auto-detected if omitted.")
    parser.add_argument("--no-build", action="store_true",
                        help="Skip building before run (fast relaunch).")
    args = parser.parse_args()

    dotnet = find_dotnet()
    if dotnet is None:
        _fail("'dotnet' CLI not found (PATH or standard install dirs). "
              "Install the .NET SDK.")

    csproj = find_csproj(args.target)
    rel = csproj.relative_to(REPO_ROOT) if REPO_ROOT in csproj.parents else csproj

    TMP_DIR.mkdir(exist_ok=True)
    stamp = _dt.datetime.now().strftime("%Y%m%d_%H%M%S")
    log_path = TMP_DIR / f"run_{stamp}.log"

    cmd = [dotnet, "run", "--project", str(csproj), "-c", args.config]
    if args.no_build:
        cmd.append("--no-build")

    # Kill any stale instance FIRST: a running VrDesktopBridge.exe locks
    # the build output (MSB3021) so the rebuild silently fails and the
    # user keeps seeing the OLD code. This is the usual cause of
    # "nothing changed after the fix".
    killed = kill_app()
    if killed:
        print(f"Stopped {killed} stale VrDesktopBridge instance(s) "
              f"before launch.")

    print(f"Project : {rel}")
    print(f"Config  : {args.config}")
    print(f"Log     : {log_path.relative_to(REPO_ROOT)}")
    print("Note    : GUI app — blocks until the window closes.")
    print("Stop    : Ctrl+C here, or  py -3 .\\tools\\stop_app.py,  or "
          "Ctrl+Alt+Shift+Q.")
    print(f"\n===== run: {' '.join(cmd)} =====")

    interrupted = False
    with log_path.open("w", encoding="utf-8") as log:
        log.write(f"Run {stamp}\nProject: {rel}\nConfig: {args.config}\n\n")
        log.flush()
        proc = subprocess.Popen(
            cmd, cwd=REPO_ROOT, stdout=subprocess.PIPE,
            stderr=subprocess.STDOUT, text=True, encoding="utf-8",
            errors="replace",
        )
        assert proc.stdout is not None
        try:
            for line in proc.stdout:
                sys.stdout.write(line)
                log.write(line)
            proc.wait()
        except KeyboardInterrupt:
            interrupted = True
            print("\n[stop] Ctrl+C — terminating the app...")
        finally:
            # Always make sure neither `dotnet run` nor the app survive.
            try:
                proc.terminate()
            except Exception:
                pass
            n = kill_app()
            if n:
                print(f"[stop] Killed {n} VrDesktopBridge instance(s).")

    if interrupted:
        print("APP STOPPED by Ctrl+C.")
        raise SystemExit(0)

    if proc.returncode != 0:
        print(f"\nRUN FAILED (exit {proc.returncode}). "
              f"See log: {log_path.relative_to(REPO_ROOT)}", file=sys.stderr)
        raise SystemExit(1)

    print(f"\nAPP EXITED cleanly. Log: {log_path.relative_to(REPO_ROOT)}")
    raise SystemExit(0)


if __name__ == "__main__":
    main()
