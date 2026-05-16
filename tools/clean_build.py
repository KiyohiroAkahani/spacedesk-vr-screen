#!/usr/bin/env python3
"""Deterministic clean build for the C#/.NET PC-side app.

Pipeline: dotnet clean -> dotnet restore -> dotnet build --no-restore.
Auto-detects a single .sln; falls back to a single .csproj. Full build
output is written to .tmp/ so the agent can inspect failures.

Usage:
    python tools/clean_build.py [--config Debug|Release] [--target PATH]

Exit codes:
    0  build succeeded
    1  build failed (a dotnet step returned non-zero)
    2  setup error (dotnet missing, no/ambiguous project, bad args)
"""

from __future__ import annotations

import argparse
import datetime as _dt
import subprocess
import sys
from pathlib import Path

from _dotnet import find_dotnet

REPO_ROOT = Path(__file__).resolve().parent.parent
TMP_DIR = REPO_ROOT / ".tmp"


def _fail(msg: str, code: int = 2) -> "NoReturn":  # type: ignore[name-defined]
    print(f"ERROR: {msg}", file=sys.stderr)
    raise SystemExit(code)


def find_build_target(explicit: str | None) -> Path:
    """Return the .sln/.csproj to build, or exit with a clear message."""
    if explicit:
        p = (REPO_ROOT / explicit).resolve() if not Path(explicit).is_absolute() else Path(explicit)
        if not p.exists():
            _fail(f"--target not found: {p}")
        return p

    slns = sorted(REPO_ROOT.rglob("*.sln"))
    slns = [s for s in slns if ".tmp" not in s.parts]
    if len(slns) == 1:
        return slns[0]
    if len(slns) > 1:
        listing = "\n  ".join(str(s.relative_to(REPO_ROOT)) for s in slns)
        _fail(f"Multiple .sln files found; pass --target:\n  {listing}")

    csprojs = sorted(REPO_ROOT.rglob("*.csproj"))
    csprojs = [c for c in csprojs if ".tmp" not in c.parts]
    if len(csprojs) == 1:
        return csprojs[0]
    if len(csprojs) > 1:
        listing = "\n  ".join(str(c.relative_to(REPO_ROOT)) for c in csprojs)
        _fail(f"Multiple .csproj files found; pass --target:\n  {listing}")

    _fail("No .sln or .csproj found. Create the .NET project first.")


def run_step(name: str, args: list[str], log) -> None:
    """Run one dotnet step, stream+capture output, exit 1 on failure."""
    header = f"\n===== {name}: {' '.join(args)} =====\n"
    print(header.rstrip())
    log.write(header)
    log.flush()

    proc = subprocess.Popen(
        args, cwd=REPO_ROOT, stdout=subprocess.PIPE,
        stderr=subprocess.STDOUT, text=True, encoding="utf-8", errors="replace",
    )
    assert proc.stdout is not None
    for line in proc.stdout:
        sys.stdout.write(line)
        log.write(line)
    proc.wait()
    log.flush()

    if proc.returncode != 0:
        print(f"\n{name} FAILED (exit {proc.returncode}). See log: {log.name}",
              file=sys.stderr)
        raise SystemExit(1)


def main() -> None:
    parser = argparse.ArgumentParser(description="Clean build the .NET PC-side app.")
    parser.add_argument("--config", default="Debug", choices=["Debug", "Release"])
    parser.add_argument("--target", default=None,
                        help="Path to .sln/.csproj (relative to repo root). "
                             "Auto-detected if omitted.")
    args = parser.parse_args()

    dotnet = find_dotnet()
    if dotnet is None:
        _fail("'dotnet' CLI not found (PATH or standard install dirs). "
              "Install the .NET SDK.")

    target = find_build_target(args.target)
    rel_target = target.relative_to(REPO_ROOT) if REPO_ROOT in target.parents else target

    TMP_DIR.mkdir(exist_ok=True)
    stamp = _dt.datetime.now().strftime("%Y%m%d_%H%M%S")
    log_path = TMP_DIR / f"build_{stamp}.log"

    print(f"Target : {rel_target}")
    print(f"Config : {args.config}")
    print(f"Log    : {log_path.relative_to(REPO_ROOT)}")

    with log_path.open("w", encoding="utf-8") as log:
        log.write(f"Clean build {stamp}\nTarget: {rel_target}\nConfig: {args.config}\n")
        t = str(target)
        run_step("clean",   [dotnet, "clean",   t, "-c", args.config], log)
        run_step("restore", [dotnet, "restore", t], log)
        run_step("build",   [dotnet, "build",   t, "-c", args.config,
                             "--no-restore"], log)

    print(f"\nBUILD SUCCEEDED ({args.config}). Log: {log_path.relative_to(REPO_ROOT)}")
    raise SystemExit(0)


if __name__ == "__main__":
    main()
