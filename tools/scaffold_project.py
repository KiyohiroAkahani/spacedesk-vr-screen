#!/usr/bin/env python3
"""Scaffold the C#/.NET solution + a GUI/host project.

Creates, at the repo root:
    <Name>.sln
    src/<Name>/<Name>.csproj   (from the chosen dotnet template)
adds the project to the solution, and pins the NuGet packages the
SBS-mirror app needs (skipped for the 'worker' template).

Templates:
    wpf    -> dotnet new wpf     (DEFAULT; the visible mirror window)
    winui  -> dotnet new winui   (needs WindowsAppSDK templates installed)
    worker -> dotnet new worker  (kept for compatibility; has NO GUI)

For wpf/winui, a bare `--framework net8.0` is auto-rewritten to
`net8.0-windows10.0.19041.0` so the WinRT Windows.Graphics.Capture
projection is available.

Idempotent: if the .sln or project already exists it refuses (exit 3)
unless --force is given. Nothing is deleted automatically.

Usage:
    py -3 .\\tools\\scaffold_project.py [--name VrDesktopBridge]
        [--template wpf|winui|worker] [--framework net8.0] [--force]

Exit codes:
    0  scaffold created
    2  setup error (dotnet missing, bad args, winui template not installed)
    3  refused: target already exists (use --force)
    1  a dotnet step failed
"""

from __future__ import annotations

import argparse
import re
import subprocess
import sys
from pathlib import Path

from _dotnet import find_dotnet

REPO_ROOT = Path(__file__).resolve().parent.parent

VALID_NAME = re.compile(r"^[A-Za-z][A-Za-z0-9_.]*$")

# template alias -> dotnet template id
TEMPLATE_IDS = {"wpf": "wpf", "winui": "winui", "worker": "worker"}

# Windows TFM required for the WinRT Windows.Graphics.Capture projection.
WIN_TFM = "net8.0-windows10.0.19041.0"

# NuGet packages the SBS app needs (alias -> version). Not added for 'worker'.
NUGET_PACKAGES = {
    "Vortice.Direct3D11": "3.6.2",
    "Vortice.DXGI": "3.6.2",
    "Vortice.D3DCompiler": "3.6.2",  # runtime HLSL compile for the SBS shader
    "Microsoft.Windows.CsWin32": "0.3.106",
}


def _fail(msg: str, code: int = 2) -> "NoReturn":  # type: ignore[name-defined]
    print(f"ERROR: {msg}", file=sys.stderr)
    raise SystemExit(code)


def run_step(name: str, args: list[str], *, fatal_code: int = 1) -> None:
    print(f"\n===== {name}: {' '.join(args)} =====")
    proc = subprocess.run(args, cwd=REPO_ROOT)
    if proc.returncode != 0:
        print(f"\n{name} FAILED (exit {proc.returncode}).", file=sys.stderr)
        raise SystemExit(fatal_code)


def retarget_tfm(csproj: Path, tfm: str) -> None:
    """Rewrite the single <TargetFramework> in the .csproj to `tfm`.

    The WPF template emits <TargetFramework>net8.0-windows</TargetFramework>;
    we need the versioned Windows TFM (e.g. net8.0-windows10.0.19041.0) so
    the WinRT Windows.Graphics.Capture projection is available.
    """
    text = csproj.read_text(encoding="utf-8")
    new_text, n = re.subn(
        r"<TargetFramework>[^<]*</TargetFramework>",
        f"<TargetFramework>{tfm}</TargetFramework>",
        text,
        count=1,
    )
    if n != 1:
        _fail(f"Could not find a single <TargetFramework> in {csproj.name} "
              f"to retarget (found {n}). Inspect the generated project.",
              code=1)
    csproj.write_text(new_text, encoding="utf-8")
    print(f"  retargeted {csproj.name} -> <TargetFramework>{tfm}</TargetFramework>")


def template_installed(dotnet: str, template_id: str) -> bool:
    """True if `dotnet new <id>` is available locally."""
    res = subprocess.run([dotnet, "new", template_id, "--dry-run", "-o", "."],
                          cwd=REPO_ROOT, capture_output=True, text=True)
    # --dry-run on an unknown template returns non-zero with a "No templates
    # found" message; on a known one it succeeds without writing files.
    return res.returncode == 0


def main() -> None:
    parser = argparse.ArgumentParser(
        description="Scaffold .NET solution + GUI project (WPF by default).")
    parser.add_argument("--name", default="VrDesktopBridge",
                        help="Solution & project name (PascalCase). Default: VrDesktopBridge")
    parser.add_argument("--template", default="wpf",
                        choices=sorted(TEMPLATE_IDS),
                        help="Project template. Default: wpf")
    parser.add_argument("--framework", default="net8.0",
                        help="Target framework moniker. Default: net8.0 "
                             "(auto-rewritten to %s for wpf/winui)" % WIN_TFM)
    parser.add_argument("--force", action="store_true",
                        help="Proceed even if .sln/project already exist.")
    args = parser.parse_args()

    if not VALID_NAME.match(args.name):
        _fail(f"Invalid --name {args.name!r}: must start with a letter and "
              f"contain only letters/digits/_/.")

    dotnet = find_dotnet()
    if dotnet is None:
        _fail("'dotnet' CLI not found (PATH or standard install dirs). "
              "Install the .NET SDK first (see workflows/build.md learnings).")

    template = args.template
    template_id = TEMPLATE_IDS[template]
    is_gui = template in ("wpf", "winui")

    # `dotnet new wpf -f` only accepts a BASE moniker (net8.0 etc.), not a
    # full Windows TFM. So pass the base to the template, then post-edit
    # the generated .csproj to the Windows TFM that exposes the WinRT
    # Windows.Graphics.Capture projection.
    base_fw = args.framework.split("-", 1)[0]  # net8.0-windows... -> net8.0
    target_tfm = WIN_TFM if is_gui else base_fw

    if template == "winui" and not template_installed(dotnet, "winui"):
        _fail("The 'winui' template is not installed. Install it with:\n"
              "  dotnet new install Microsoft.WindowsAppSDK.ProjectTemplates\n"
              "(not auto-installed on purpose).")

    name = args.name
    sln_path = REPO_ROOT / f"{name}.sln"
    proj_dir = REPO_ROOT / "src" / name
    proj_path = proj_dir / f"{name}.csproj"

    existing = [p for p in (sln_path, proj_path) if p.exists()]
    if existing and not args.force:
        listing = "\n  ".join(str(p.relative_to(REPO_ROOT)) for p in existing)
        _fail(f"Already exists (use --force to proceed):\n  {listing}", code=3)

    print(f"Name      : {name}")
    print(f"Template  : {template} (dotnet new {template_id})")
    print(f"Template -f: {base_fw}")
    print(f"Final TFM : {target_tfm}")
    print(f"Solution  : {sln_path.relative_to(REPO_ROOT)}")
    print(f"Project   : {proj_path.relative_to(REPO_ROOT)}")

    proj_dir.mkdir(parents=True, exist_ok=True)

    if not sln_path.exists():
        run_step("new sln", [dotnet, "new", "sln", "-n", name])
    run_step(f"new {template}",
             [dotnet, "new", template_id, "-n", name, "-o", str(proj_dir),
              "-f", base_fw])
    run_step("sln add",
             [dotnet, "sln", f"{name}.sln", "add", str(proj_path)])

    if is_gui:
        retarget_tfm(proj_path, target_tfm)

    if template != "worker":
        for pkg, ver in NUGET_PACKAGES.items():
            run_step(f"add {pkg}",
                     [dotnet, "add", str(proj_path), "package", pkg,
                      "-v", ver])

    print(f"\nSCAFFOLD CREATED. Next:\n"
          f"  py -3 .\\tools\\clean_build.py   # verify it builds\n"
          f"  py -3 .\\tools\\run_app.py       # launch it")
    raise SystemExit(0)


if __name__ == "__main__":
    main()
