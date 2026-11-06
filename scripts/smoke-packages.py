#!/usr/bin/env python3
"""Pack every Touchstone package into a local feed, inspect the packages, and run consumer projects against them
(catalog section 11.26 and ADP-25).

Usage: python3 scripts/smoke-packages.py [--version VERSION] [--results PATH] [--keep]
The version defaults to the Directory.Build.props version with a -smoke suffix, so its assembly version matches the
release and package validation against PackageValidationBaselineVersion still applies. It is used only for the local
pack (-p:Version) and is never committed.
A private NuGet cache is used so a stale package with the same version is never picked up.
"""
import argparse
import json
import os
import re
import shutil
import subprocess
import sys
import tempfile
import zipfile
from xml.sax.saxutils import escape

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
SMOKE = os.path.join(ROOT, "test", "Consumer.Smoke")
PACKAGES = ["Touchstone.Core", "Touchstone.Cli", "Touchstone.Xunit2Adapter", "Touchstone.Xunit3Adapter",
            "Touchstone.NunitAdapter", "Touchstone.MstestAdapter", "Touchstone"]
LIBRARIES = PACKAGES[:-1]
ADAPTER_DEPENDENCIES = {
    "Touchstone.Xunit2Adapter": ("xunit.extensibility.core", "2.9.3"),
    "Touchstone.Xunit3Adapter": ("xunit.v3.extensibility.core", "4.0.2"),
    "Touchstone.NunitAdapter": ("NUnit", "5.0.0"),
    "Touchstone.MstestAdapter": ("MSTest.TestFramework", "4.4.1"),
}
TEST_ONLY = ("Microsoft.NET.Test.Sdk", "xunit.runner.visualstudio", "NUnit3TestAdapter", "MSTest.TestAdapter", "xunit.v3.mtp")


def run(args, env, cwd=ROOT):
    return subprocess.run(args, capture_output=True, text=True, env=env, cwd=cwd)


def nuspec(package_path):
    with zipfile.ZipFile(package_path) as archive:
        name = next(n for n in archive.namelist() if n.endswith(".nuspec"))
        return archive.read(name).decode("utf-8"), archive.namelist()


def dependencies(spec):
    return re.findall(r'<dependency id="([^"]+)" version="([^"]+)"', spec)


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--version", default=None)
    parser.add_argument("--results", default=os.path.join(ROOT, "smoke-results.json"))
    parser.add_argument("--keep", action="store_true")
    args = parser.parse_args()
    if args.version is None:
        with open(os.path.join(ROOT, "Directory.Build.props"), encoding="utf-8") as handle:
            release = re.search(r"<Version>([^<-]+)", handle.read()).group(1)
        args.version = release + "-smoke"

    work = tempfile.mkdtemp(prefix="touchstone-smoke-")
    feed = os.path.join(work, "feed")
    env = dict(os.environ)
    env["NUGET_PACKAGES"] = os.path.join(work, "nuget-cache")
    rows = {}

    def record(row, ok, message):
        previous = rows.get(row, (True, ""))
        rows[row] = (previous[0] and ok, previous[1] if ok else ((previous[1] + "; ") if previous[1] else "") + message)
        print(("PASS  " if ok else "FAIL  ") + row + "  " + message)

    try:
        # --artifacts-path keeps this build out of bin/ and obj/, so the smoke version never replaces the development build.
        packed = run(["dotnet", "pack", "Touchstone.slnx", "-c", "Release", "-p:Version=" + args.version, "-o", feed,
                      "--artifacts-path", os.path.join(work, "artifacts"), "-v", "q", "-nologo"], env)
        record("PKG-01", packed.returncode == 0, "pack succeeded" if packed.returncode == 0 else packed.stdout[-2000:])
        record("PKG-06", packed.returncode == 0, "package validation passed during pack")
        if packed.returncode != 0:
            return finish(rows, args.results)

        for package in PACKAGES:
            path = os.path.join(feed, package + "." + args.version + ".nupkg")
            symbols = os.path.join(feed, package + "." + args.version + ".snupkg")
            if not os.path.exists(path):
                record("PKG-01", False, package + " package missing")
                continue
            spec, files = nuspec(path)
            for content in ("README.md", "LICENSE.md", "logo-black.png", "BDD.md", "TELEMETRY.md"):
                record("PKG-02", content in files, package + " contains " + content)
            if package in LIBRARIES:
                for framework in ("net8.0", "net10.0"):
                    record("PKG-01", "lib/" + framework + "/" + package + ".dll" in files, package + " has lib/" + framework)
                    record("PKG-02", "lib/" + framework + "/" + package + ".xml" in files, package + " has XML docs for " + framework)
                record("PKG-03", os.path.exists(symbols), package + " has a symbol package")
                record("PKG-03", 'repository type="git"' in spec and "commit=" in spec, package + " records its repository commit for SourceLink")
            deps = dependencies(spec)
            ids = sorted(set(d[0] for d in deps))
            if package == "Touchstone":
                record("PKG-04", ids == ["Touchstone.Cli", "Touchstone.Core"], "metapackage depends on " + ", ".join(ids))
            if package == "Touchstone.Core":
                record("PKG-10", ids == [], "Touchstone.Core has no dependencies (" + ", ".join(ids) + ")")
            if package == "Touchstone.Cli":
                record("JSON-12", "schemas/touchstone-results-1.0.schema.json" in files, "Touchstone.Cli ships the results schema")
            if package in ADAPTER_DEPENDENCIES:
                expected = ADAPTER_DEPENDENCIES[package]
                record("PKG-05", any(d[0] == expected[0] and d[1].strip("[]()").split(",")[0] == expected[1] for d in deps),
                       package + " depends on " + expected[0] + " " + expected[1])
                record("ADP-25", not any(d[0].startswith(TEST_ONLY) for d in deps), package + " brings no test-runner dependency")

        for name in ("PublicAPI.Unshipped.txt",):
            pending = []
            for package in PACKAGES:
                with open(os.path.join(ROOT, "src", package, name), encoding="utf-8") as handle:
                    lines = [l for l in handle.read().splitlines() if l.strip() and not l.startswith("#")]
                if lines:
                    pending.append(package + " (" + str(len(lines)) + ")")
            print("INFO  PKG-07 unshipped API lines: " + (", ".join(pending) if pending else "none") + " (must be empty only at release)")

        # A config file, not repeated --source options: on Windows, solution restore resolves a URL passed through
        # --source as a path relative to the project directory.
        config = os.path.join(work, "nuget.config")
        with open(config, "w", encoding="utf-8") as handle:
            handle.write('<?xml version="1.0" encoding="utf-8"?>\n<configuration>\n  <packageSources>\n    <clear />\n'
                         '    <add key="local" value="' + escape(feed) + '" />\n'
                         '    <add key="nuget.org" value="https://api.nuget.org/v3/index.json" />\n'
                         '  </packageSources>\n</configuration>\n')
        restore = run(["dotnet", "restore", os.path.join(SMOKE, "Consumer.Smoke.slnx"), "-p:TouchstoneVersion=" + args.version,
                       "--configfile", config, "-v", "q"], env)
        build = run(["dotnet", "build", os.path.join(SMOKE, "Consumer.Smoke.slnx"), "-c", "Release", "--no-restore",
                     "-p:TouchstoneVersion=" + args.version, "-v", "q", "-nologo"], env)
        ok = restore.returncode == 0 and build.returncode == 0
        record("PKG-08", ok, "smoke solution restored and built from the local feed" if ok else (restore.stdout + build.stdout)[-3000:])
        if ok:
            console = run(["dotnet", "run", "--project", os.path.join(SMOKE, "Smoke.Console"), "-c", "Release", "--no-build", "--", "--no-color"], env)
            record("PKG-08", console.returncode == 0 and "Total: 3  Passed: 2  Failed: 0  Skipped: 1" in console.stdout,
                   "console host exit " + str(console.returncode))
            for host in ("Smoke.Xunit2", "Smoke.Xunit3", "Smoke.Nunit", "Smoke.Mstest"):
                tested = run(["dotnet", "test", os.path.join(SMOKE, host), "-c", "Release", "--no-build"], env)
                expected = "Passed:     3, Skipped:     0" if host == "Smoke.Xunit2" else "Passed:     2, Skipped:     1"
                record("PKG-09", tested.returncode == 0 and expected in tested.stdout, host + " " + (re.search(r"(Passed!|Failed!).*", tested.stdout) or re.search(r".*", "no summary")).group(0))
    finally:
        if not args.keep:
            shutil.rmtree(work, ignore_errors=True)
        else:
            print("Kept " + work)

    return finish(rows, args.results)


def finish(rows, path):
    document = {"schemaVersion": "1.0", "results": [
        {"caseId": row, "outcome": "passed" if ok else "failed", "message": message or None} for row, (ok, message) in sorted(rows.items())
    ]}
    with open(path, "w", encoding="utf-8") as handle:
        json.dump(document, handle, indent=2)
    failed = [row for row, (ok, _) in rows.items() if not ok]
    print("Smoke rows: " + str(len(rows)) + ", failed: " + str(len(failed)))
    return 1 if failed else 0


if __name__ == "__main__":
    sys.exit(main())
