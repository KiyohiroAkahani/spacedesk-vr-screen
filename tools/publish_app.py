#!/usr/bin/env python3
"""Publish a self-contained, single-file Windows .exe to dist/.

The output needs NO .NET install on the target machine — a new user can
just double-click dist/VrDesktopBridge.exe. Per-user settings are created
automatically at %LOCALAPPDATA%\\VrDesktopBridge\\config.json on first run,
so nothing is machine-specific.

Usage:
    py -3 .\\tools\\publish_app.py [--target PATH] [--rid win-x64]

Exit codes:
    0  published OK (path printed)
    1  a dotnet step failed
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
DIST_DIR = REPO_ROOT / "dist"


def _fail(msg: str, code: int = 2) -> "NoReturn":  # type: ignore[name-defined]
    print(f"ERROR: {msg}", file=sys.stderr)
    raise SystemExit(code)


def find_csproj(explicit: str | None) -> Path:
    if explicit:
        p = (REPO_ROOT / explicit) if not Path(explicit).is_absolute() else Path(explicit)
        p = p.resolve()
        if not p.exists() or p.suffix != ".csproj":
            _fail(f"--target must be an existing .csproj: {p}")
        return p
    csprojs = [c for c in sorted(REPO_ROOT.rglob("*.csproj"))
               if ".tmp" not in c.parts and "obj" not in c.parts]
    if len(csprojs) == 1:
        return csprojs[0]
    if not csprojs:
        _fail("No .csproj found. Scaffold the project first.")
    listing = "\n  ".join(str(c.relative_to(REPO_ROOT)) for c in csprojs)
    _fail(f"Multiple .csproj found; pass --target:\n  {listing}")


def main() -> None:
    ap = argparse.ArgumentParser(description="Publish a self-contained .exe.")
    ap.add_argument("--target", default=None, help="Path to the .csproj.")
    ap.add_argument("--rid", default="win-x64", help="Runtime id. Default win-x64.")
    args = ap.parse_args()

    dotnet = find_dotnet()
    if dotnet is None:
        _fail("'dotnet' CLI not found. Install the .NET 8 SDK to publish.")

    csproj = find_csproj(args.target)
    DIST_DIR.mkdir(exist_ok=True)
    TMP_DIR.mkdir(exist_ok=True)
    stamp = _dt.datetime.now().strftime("%Y%m%d_%H%M%S")
    log_path = TMP_DIR / f"publish_{stamp}.log"

    cmd = [
        dotnet, "publish", str(csproj),
        "-c", "Release",
        "-r", args.rid,
        "--self-contained", "true",
        "-p:PublishSingleFile=true",
        "-p:IncludeNativeLibrariesForSelfExtract=true",
        "-p:EnableCompressionInSingleFile=true",
        "-p:DebugType=none",
        "-o", str(DIST_DIR),
    ]
    print(f"Project : {csproj.relative_to(REPO_ROOT)}")
    print(f"Output  : {DIST_DIR.relative_to(REPO_ROOT)}\\  (self-contained, "
          f"no .NET needed on target)")
    print(f"Log     : {log_path.relative_to(REPO_ROOT)}")
    print(f"\n===== publish: {' '.join(cmd)} =====")

    with log_path.open("w", encoding="utf-8") as log:
        proc = subprocess.Popen(
            cmd, cwd=REPO_ROOT, stdout=subprocess.PIPE,
            stderr=subprocess.STDOUT, text=True, encoding="utf-8",
            errors="replace")
        assert proc.stdout is not None
        for line in proc.stdout:
            sys.stdout.write(line)
            log.write(line)
        proc.wait()

    if proc.returncode != 0:
        print(f"\nPUBLISH FAILED (exit {proc.returncode}). See {log_path}",
              file=sys.stderr)
        raise SystemExit(1)

    exe = DIST_DIR / "VrDesktopBridge.exe"
    print(f"\nPUBLISH SUCCEEDED. Run / share: {exe.relative_to(REPO_ROOT)}"
          if exe.exists()
          else f"\nPUBLISH SUCCEEDED but {exe} not found — check the log.")
    raise SystemExit(0)


if __name__ == "__main__":
    main()
