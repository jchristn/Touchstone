#!/usr/bin/env python3
"""Synchronize PublicAPI.Unshipped.txt with the current public API of every shipping project.

Removes declared symbols that no longer exist (RS0017) and records new public symbols (RS0016).
Run after an intentional public API change, then review the diff of the PublicAPI files.
Usage: python3 scripts/update-public-api.py [ProjectName ...]
"""
import os
import re
import subprocess
import sys

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
PROJECTS = [
    "Touchstone.Core", "Touchstone.Cli", "Touchstone.Xunit2Adapter", "Touchstone.Xunit3Adapter",
    "Touchstone.NunitAdapter", "Touchstone.MstestAdapter", "Touchstone",
]
RS0017 = re.compile(r"PublicAPI\.Unshipped\.txt\((\d+),\d+\): error RS0017: Symbol '(.*?)' is part of the declared API")


def build(csproj):
    result = subprocess.run(["dotnet", "build", csproj, "-c", "Release", "-v", "q", "-nologo"],
                            capture_output=True, text=True)
    return result.stdout + result.stderr


def remove_stale(project_dir, output):
    stale = set(m.group(2) for m in RS0017.finditer(output))
    if not stale:
        return 0
    path = os.path.join(project_dir, "PublicAPI.Unshipped.txt")
    with open(path) as handle:
        lines = handle.read().splitlines()
    kept = [line for line in lines if line.strip() not in stale]
    with open(path, "w") as handle:
        handle.write("\n".join(kept) + "\n")
    return len(lines) - len(kept)


def main():
    names = sys.argv[1:] or PROJECTS
    for name in names:
        project_dir = os.path.join(ROOT, "src", name)
        csproj = os.path.join(project_dir, name + ".csproj")
        if not os.path.exists(csproj):
            continue
        for path in ("PublicAPI.Shipped.txt", "PublicAPI.Unshipped.txt"):
            full = os.path.join(project_dir, path)
            if not os.path.exists(full):
                with open(full, "w") as handle:
                    handle.write("#nullable enable\n")
        for _ in range(5):
            removed = remove_stale(project_dir, build(csproj))
            subprocess.run(["dotnet", "format", "analyzers", csproj, "--diagnostics", "RS0016", "--severity", "warn"],
                           capture_output=True, text=True)
            output = build(csproj)
            if "RS0016" not in output and "RS0017" not in output:
                break
            if removed == 0 and "RS0017" not in output and "RS0016" in output:
                continue
        compiled = " 0 Error(s)" in output
        status = "clean" if (compiled and "RS0016" not in output and "RS0017" not in output) else "NOT clean"
        print(name + ": " + status)


if __name__ == "__main__":
    main()
