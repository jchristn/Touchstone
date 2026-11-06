#!/usr/bin/env python3
"""Run every test project in Touchstone.slnx under every target framework, with one uniquely named TRX file each.

Usage: python3 scripts/run-native-hosts.py --results-directory DIR [--configuration Release]
A solution-level `dotnet test --logger trx` writes files whose default names can collide when several projects finish
in the same second, so this script runs each project and framework separately. Exits 1 when any run fails.
"""
import argparse
import os
import re
import subprocess
import sys

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
TEST_MARKERS = ("Microsoft.NET.Test.Sdk", "xunit.v3.mtp-off", "\"MSTest\"")


def frameworks(text):
    match = re.search(r"<TargetFrameworks?>([^<]+)</TargetFrameworks?>", text)
    return match.group(1).split(";") if match else ["net8.0", "net10.0"]


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--results-directory", required=True)
    parser.add_argument("--configuration", default="Release")
    args = parser.parse_args()

    with open(os.path.join(ROOT, "Touchstone.slnx"), encoding="utf-8") as handle:
        projects = re.findall(r'<Project Path="([^"]+\.csproj)"', handle.read())

    failures = []
    for project in projects:
        with open(os.path.join(ROOT, project), encoding="utf-8") as handle:
            text = handle.read()
        if not any(marker in text for marker in TEST_MARKERS) or "<IsTestProject>false</IsTestProject>" in text:
            continue
        name = os.path.splitext(os.path.basename(project))[0]
        for framework in frameworks(text):
            log = "trx;LogFileName=" + name + "." + framework + ".trx"
            result = subprocess.run(["dotnet", "test", project, "-c", args.configuration, "--no-build", "-f", framework,
                                     "--logger", log, "--results-directory", args.results_directory],
                                    capture_output=True, text=True, cwd=ROOT)
            summary = re.search(r"(Passed!|Failed!).*", result.stdout)
            print(name + " " + framework + ": " + (summary.group(0) if summary else "no summary, exit " + str(result.returncode)))
            # A host that loads no tests can exit 0 without a summary; treat that as a failure.
            if result.returncode != 0 or summary is None or summary.group(1) != "Passed!":
                failures.append(name + " " + framework)
                print(result.stdout[-3000:])

    print("Native host runs failed: " + (", ".join(failures) if failures else "none"))
    return 1 if failures else 0


if __name__ == "__main__":
    sys.exit(main())
