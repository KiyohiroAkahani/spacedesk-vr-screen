#!/usr/bin/env python3
"""Force-stop the VrDesktopBridge mirror app — a guaranteed kill switch.

The mirror window is borderless / no-activate / click-through, so it
can't always be closed from the UI, and global hotkeys may be unreliable.
This always works regardless of focus or window state.

Usage:
    py -3 .\\tools\\stop_app.py

Exit codes:
    0  done (whether or not an instance was running)
"""

from __future__ import annotations

from _proc import kill_app

if __name__ == "__main__":
    n = kill_app()
    print(f"Stopped {n} VrDesktopBridge instance(s)."
          if n else "No VrDesktopBridge instance was running.")
    raise SystemExit(0)
