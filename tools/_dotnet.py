"""Shared helper: locate the `dotnet` CLI robustly.

Why this exists: when the .NET SDK is installed via winget into an
already-open shell, PATH is NOT refreshed for that session, so
`shutil.which("dotnet")` returns None even though the SDK is installed
at the well-known location. WAT tools must still work, so we fall back
to the standard install directories.

Import from a sibling tool (same tools/ dir is on sys.path[0] when a
tool is run as a script):

    from _dotnet import find_dotnet
"""

from __future__ import annotations

import os
import shutil
from pathlib import Path


def find_dotnet() -> str | None:
    """Return an invokable path to `dotnet`, or None if not found.

    Search order: PATH, then the standard install dirs (covers the
    'PATH not refreshed in this session' case after a fresh install).
    """
    found = shutil.which("dotnet")
    if found:
        return found

    candidates = []
    for env in ("ProgramW6432", "ProgramFiles", "ProgramFiles(x86)"):
        base = os.environ.get(env)
        if base:
            candidates.append(Path(base) / "dotnet" / "dotnet.exe")
    local = os.environ.get("LOCALAPPDATA")
    if local:
        candidates.append(Path(local) / "Microsoft" / "dotnet" / "dotnet.exe")
    candidates.append(Path(r"C:\Program Files\dotnet\dotnet.exe"))

    for c in candidates:
        if c.is_file():
            return str(c)
    return None
