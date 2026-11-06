#!/usr/bin/env python3
"""Check Touchstone test results against the test catalogs in archive/V1.0_PLAN.md section 11 and archive/V1.1_PLAN.md section 5.

Usage:
  python3 scripts/verify-catalog.py [--plan PATH ...] [--update-plan] [--require-all] [--trx DIR] results.json [...]

With no --plan, both catalogs are read. Repeat --plan to read a specific set.

Each results file is JSON written by ConsoleRunner --results (schema 1.0) or by the verification scripts. With
--trx, every .trx file under DIR (written by `dotnet test --logger trx`) is read as well; a native test matches the
first catalog ID in its name. A case matches a catalog row when its identity equals the catalog ID or starts with the
ID followed by '-' (adapter variants such as ADP-01-X3). Adapter rows (ADP, DUP-08, TEL-12, CAN-22) require a
passing variant for every adapter: X2, X3, NU, and MS. Rows with status `release` are checked only at release.

Exit codes: 0 when every case maps to a catalog row, every mapped case passed, and (with --require-all) every
catalog row marked as a test row has at least one passing case; 1 otherwise.
"""
import argparse
import json
import os
import re
import sys

ROW = re.compile(r"^\| ([A-Z]+-\d+) \| (.*) \| (planned|done|dropped[^|]*|script|release) \|\s*$")
NATIVE_ID = re.compile(r"((?:[A-Z]+)-\d+(?:-[A-Z0-9]{2})?)\b")
ADAPTER_CODES = ("X2", "X3", "NU", "MS")


def adapter_row(row):
    return (row.startswith("ADP-") and row != "ADP-25") or row in ("DUP-08", "TEL-12", "CAN-22")


DEFAULT_PLANS = ["archive/V1.0_PLAN.md", "archive/V1.1_PLAN.md"]


def load_catalog(paths):
    rows = {}
    for path in paths:
        with open(path, encoding="utf-8") as handle:
            for line in handle:
                match = ROW.match(line.rstrip("\n"))
                if match:
                    if match.group(1) in rows:
                        raise SystemExit("Catalog ID " + match.group(1) + " appears more than once (" + path + ")")
                    rows[match.group(1)] = match.group(3)
    return rows


def catalog_id(case_id, rows):
    if case_id in rows:
        return case_id
    parts = case_id.split("-")
    for length in range(len(parts) - 1, 1, -1):
        candidate = "-".join(parts[:length])
        if candidate in rows:
            return candidate
    return None


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--plan", action="append", default=None)
    parser.add_argument("--update-plan", action="store_true")
    parser.add_argument("--require-all", action="store_true")
    parser.add_argument("--trx", action="append", default=[])
    parser.add_argument("results", nargs="*")
    args = parser.parse_args()

    plans = args.plan or DEFAULT_PLANS
    rows = load_catalog(plans)
    passed, failed, unknown = set(), {}, set()
    variants = {}
    entries = []
    for directory in args.trx:
        for folder, _, names in os.walk(directory):
            for name in names:
                if name.endswith(".trx"):
                    text = open(os.path.join(folder, name), encoding="utf-8").read()
                    for match in re.finditer(r'<UnitTestResult [^>]*testName="([^"]*)"[^>]*outcome="([^"]*)"', text):
                        found = NATIVE_ID.search(match.group(1))
                        if found and catalog_id(found.group(1), rows):
                            outcome = {"Passed": "passed", "NotExecuted": "skipped"}.get(match.group(2), "failed")
                            entries.append({"caseId": found.group(1), "outcome": outcome, "message": match.group(1)})
    for path in args.results:
        with open(path, encoding="utf-8") as handle:
            document = json.load(handle)
        entries.extend(document["results"])
    for result in entries:
        if True:
            row = catalog_id(result["caseId"], rows)
            if row is None:
                if re.match(r"^[A-Z]+-\d+", result["caseId"]):
                    unknown.add(result["caseId"])
                continue
            if result["outcome"] in ("passed", "skipped"):
                passed.add(row)
                code = result["caseId"][len(row) + 1:]
                if code:
                    variants.setdefault(row, set()).add(code)
            else:
                failed[row] = result["outcome"] + ": " + (result["message"] or "")

    problems = 0
    for case_id in sorted(unknown):
        print("UNKNOWN  " + case_id + " is not in the catalog")
        problems += 1
    for row, reason in sorted(failed.items()):
        print("FAILED   " + row + " " + reason[:200])
        problems += 1
    if args.require_all:
        for row, status in sorted(rows.items()):
            if status in ("planned", "done") and row not in passed:
                print("MISSING  " + row + " has no passing case")
                problems += 1
            elif status in ("planned", "done") and adapter_row(row):
                missing = [c for c in ADAPTER_CODES if c not in variants.get(row, set())]
                if missing:
                    print("MISSING  " + row + " has no passing variant for " + ", ".join(missing))
                    problems += 1

    if args.update_plan:
        for plan in plans:
            with open(plan, encoding="utf-8") as handle:
                lines = handle.read().split("\n")
            for index, line in enumerate(lines):
                match = ROW.match(line)
                if match and match.group(3) == "planned" and match.group(1) in passed and match.group(1) not in failed:
                    lines[index] = line[: line.rfind("| planned |")] + "| done |"
            with open(plan, "w", encoding="utf-8") as handle:
                handle.write("\n".join(lines))

    covered = len([r for r in rows if r in passed])
    print("Catalog rows: " + str(len(rows)) + ", covered by passing cases: " + str(covered) + ", problems: " + str(problems))
    return 1 if problems else 0


if __name__ == "__main__":
    sys.exit(main())
