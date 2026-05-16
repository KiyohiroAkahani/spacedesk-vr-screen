"""Shared helper: forcibly stop the VrDesktopBridge app.

Why this exists: the mirror window is borderless / no-activate / input
pass-through, so it cannot always be closed from the UI, and a stale
instance locks VrDesktopBridge.exe (rebuilds fail with MSB3021) and makes
new code "not take effect". A deterministic kill switch is essential.

Import from a sibling tool (tools/ is on sys.path[0] when run as a script):

    from _proc import kill_app
"""

from __future__ import annotations

import ctypes
import subprocess

APP_IMAGE = "VrDesktopBridge.exe"

# SPI_SETCURSORS — reload the user's real cursors. The app hides the OS
# cursor in duplicate mode; a force-kill (taskkill) skips its restore
# handlers, so we reset cursors here as a safety net.
_SPI_SETCURSORS = 0x0057


def restore_cursors() -> None:
    """Reload default system cursors (undo a left-over cursor hide). Safe."""
    try:
        ctypes.windll.user32.SystemParametersInfoW(_SPI_SETCURSORS, 0, None, 0)
    except Exception:
        pass


def kill_app() -> int:
    """Force-kill all VrDesktopBridge processes (and their children).

    Returns the number of image instances taskkill reported terminating
    (0 if none were running). Never raises.
    """
    try:
        res = subprocess.run(
            ["taskkill", "/F", "/T", "/IM", APP_IMAGE],
            capture_output=True, text=True,
        )
    except FileNotFoundError:
        return 0
    finally:
        # Always undo a possible left-over cursor hide.
        restore_cursors()
    # taskkill exit 0 = killed something, 128 = no such process.
    if res.returncode != 0:
        return 0
    # Count "SUCCESS:" lines (one per PID).
    return sum(1 for ln in res.stdout.splitlines() if "SUCCESS" in ln.upper())
