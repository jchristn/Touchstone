#!/usr/bin/env python3
"""Verify native outcome parity across the Touchstone framework adapters (catalog section 11.23).

Builds test/Parity/Parity.slnx, runs each adapter's deliberately mixed fixture through `dotnet test` under every
SkipBehavior, parses the TRX output, and compares native outcomes with the expected table below.

Usage: python3 scripts/verify-parity.py [--results PATH]
Writes a results file (same shape as JsonReport output) with one entry per PAR catalog row, then exits 0 when every
row passed and 1 otherwise.
"""
import argparse
import json
import os
import re
import shutil
import subprocess
import sys
import tempfile
import xml.etree.ElementTree as ET

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
PARITY = os.path.join(ROOT, "test", "Parity")
NS = {"t": "http://microsoft.com/schemas/VisualStudio/TeamTest/2010"}
ADAPTERS = {
    "X2": "Parity.Xunit2",
    "X3": "Parity.Xunit3",
    "NU": "Parity.Nunit",
    "MS": "Parity.Mstest",
}
CATEGORY_FILTER = {"X3": "Category=parity-tag", "NU": "TestCategory=parity-tag", "MS": "TestCategory=parity-tag"}
DISPLAY = ["Parity pass", "Parity scenario pass", "Parity fail", "Parity scenario fail", "Parity skip", "Parity cancel"]

# Expected (passed, failed, skipped) per adapter and mode. The Cancel case runs with a canceled token, so the case
# reports Canceled and every framework records it as a failure.
EXPECTED = {
    ("X3", "report"): (2, 3, 1), ("NU", "report"): (2, 3, 1), ("MS", "report"): (2, 3, 1),
    ("X2", "report"): (3, 3, 0),
}
for code in ADAPTERS:
    EXPECTED[(code, "omit")] = (2, 3, 0)
    EXPECTED[(code, "fail")] = (2, 4, 0)


def run(args, env=None):
    merged = dict(os.environ)
    if env:
        merged.update(env)
    return subprocess.run(args, capture_output=True, text=True, env=merged, cwd=ROOT)


def trx_results(directory):
    results = []
    for name in os.listdir(directory):
        if not name.endswith(".trx"):
            continue
        root = ET.parse(os.path.join(directory, name)).getroot()
        outputs = {}
        for unit in root.iter("{%s}UnitTestResult" % NS["t"]):
            text = "".join(unit.itertext())
            results.append({"name": unit.get("testName"), "outcome": unit.get("outcome"), "text": text})
    return results


def dotnet_test(project, mode, extra=None):
    directory = tempfile.mkdtemp(prefix="parity-")
    args = ["dotnet", "test", os.path.join(PARITY, project, project + ".csproj"), "-c", "Release", "--no-build",
            "--logger", "trx", "--results-directory", directory]
    if extra:
        args += extra
    run(args, {"TOUCHSTONE_PARITY_SKIP": mode})
    results = trx_results(directory)
    shutil.rmtree(directory, ignore_errors=True)
    return results


def counts(results):
    passed = sum(1 for r in results if r["outcome"] == "Passed")
    failed = sum(1 for r in results if r["outcome"] == "Failed")
    skipped = sum(1 for r in results if r["outcome"] in ("NotExecuted", "Skipped", "Inconclusive"))
    return passed, failed, skipped


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--results", default=os.path.join(ROOT, "parity-results.json"))
    args = parser.parse_args()

    build = run(["dotnet", "build", os.path.join(PARITY, "Parity.slnx"), "-c", "Release", "-v", "q", "-nologo"])
    if build.returncode != 0:
        print(build.stdout + build.stderr)
        return 1

    rows = {}

    def record(row, ok, message):
        previous = rows.get(row, (True, ""))
        rows[row] = (previous[0] and ok, (previous[1] + "; " if previous[1] else "") + message if not ok else previous[1])
        print(("PASS  " if ok else "FAIL  ") + row + "  " + message)

    runs = {}
    for code, project in ADAPTERS.items():
        for mode in ("report", "omit", "fail"):
            runs[(code, mode)] = dotnet_test(project, mode)

    for code in ADAPTERS:
        report = runs[(code, "report")]
        actual = counts(report)
        row = {"X3": "PAR-01", "X2": "PAR-02", "NU": "PAR-03", "MS": "PAR-04"}[code]
        record(row, actual == EXPECTED[(code, "report")], code + " report counts " + str(actual) + " expected " + str(EXPECTED[(code, "report")]))
        record(row, not any("Parity excluded" in (r["name"] or "") for r in report), code + " filtered case absent")
        if code == "X2":
            skip = [r for r in report if "Parity skip" in (r["name"] or "")]
            record("PAR-02", len(skip) == 1 and "parity skip reason" in skip[0]["text"], "X2 skip reason written to output")

        omit = runs[(code, "omit")]
        record("PAR-05", counts(omit) == EXPECTED[(code, "omit")] and not any("Parity skip" in (r["name"] or "") for r in omit),
               code + " omit counts " + str(counts(omit)))
        fail = runs[(code, "fail")]
        record("PAR-06", counts(fail) == EXPECTED[(code, "fail")], code + " fail counts " + str(counts(fail)))

        names = [r["name"] or "" for r in report]
        missing = [d for d in DISPLAY if not any(d in n for n in names)]
        record("PAR-07", not missing, code + " native names contain display names" + (" (missing " + ", ".join(missing) + ")" if missing else ""))

        cancel = [r for r in report if "Parity cancel" in (r["name"] or "")]
        record("PAR-09", len(cancel) == 1 and cancel[0]["outcome"] == "Failed", code + " canceled case native outcome " + (cancel[0]["outcome"] if cancel else "missing"))

        if code in CATEGORY_FILTER:
            filtered = dotnet_test(ADAPTERS[code], "report", ["--filter", CATEGORY_FILTER[code]])
            record("PAR-08", counts(filtered) == (1, 0, 0), code + " native category filter " + str(counts(filtered)))

        listed = run(["dotnet", "test", os.path.join(PARITY, ADAPTERS[code], ADAPTERS[code] + ".csproj"), "-c", "Release", "--no-build", "--list-tests"],
                     {"TOUCHSTONE_PARITY_SKIP": "report"})
        entries = [line for line in listed.stdout.splitlines() if line.startswith("    ") and "Parity" in line]
        record("PAR-10", len(entries) == 6, code + " discovery lists " + str(len(entries)) + " entries, expected 6")

    document = {"schemaVersion": "1.0", "results": [
        {"caseId": row, "outcome": "passed" if ok else "failed", "message": message or None}
        for row, (ok, message) in sorted(rows.items())
    ]}
    with open(args.results, "w", encoding="utf-8") as handle:
        json.dump(document, handle, indent=2)

    failed = [row for row, (ok, _) in rows.items() if not ok]
    print("Parity rows: " + str(len(rows)) + ", failed: " + str(len(failed)))
    return 1 if failed else 0


if __name__ == "__main__":
    sys.exit(main())
