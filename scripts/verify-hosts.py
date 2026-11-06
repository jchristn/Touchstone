#!/usr/bin/env python3
"""Verify console host behavior and native AOT (catalog rows CRUN-13 and AOT-01 to AOT-05).

Usage: python3 scripts/verify-hosts.py --rid RID [--results PATH] [--extra-publish-args ARGS]
Publishes src/Test.Aot for RID, checks that the publish has no IL trim or AOT warnings, runs the native binary,
compares its exit codes with Test.Automated (net10.0, built beforehand), validates the native binary's JSON output
against schemas/touchstone-results-1.0.schema.json, and writes a results file for scripts/verify-catalog.py.
"""
import argparse
import json
import os
import re
import shlex
import subprocess
import sys
import tempfile

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))


def run(args, env=None):
    merged = dict(os.environ)
    if env:
        merged.update(env)
    return subprocess.run(args, capture_output=True, text=True, env=merged, cwd=ROOT)


def validate(schema, value, path, errors):
    kind = schema.get("type")
    if kind is not None:
        kinds = kind if isinstance(kind, list) else [kind]
        checks = {
            "object": lambda v: isinstance(v, dict), "array": lambda v: isinstance(v, list),
            "string": lambda v: isinstance(v, str), "boolean": lambda v: isinstance(v, bool),
            "integer": lambda v: isinstance(v, int) and not isinstance(v, bool),
            "number": lambda v: isinstance(v, (int, float)) and not isinstance(v, bool), "null": lambda v: v is None,
        }
        if not any(checks[k](value) for k in kinds):
            errors.append(path + ": expected " + "|".join(kinds))
            return
    if "const" in schema and value != schema["const"]:
        errors.append(path + ": expected " + json.dumps(schema["const"]))
    if "enum" in schema and value not in schema["enum"]:
        errors.append(path + ": " + json.dumps(value) + " not in enum")
    if "minimum" in schema and isinstance(value, (int, float)) and value < schema["minimum"]:
        errors.append(path + ": below minimum")
    if isinstance(value, dict):
        for name in schema.get("required", []):
            if name not in value:
                errors.append(path + ": missing " + name)
        properties = schema.get("properties", {})
        for name, child in value.items():
            if name in properties:
                validate(properties[name], child, path + "." + name, errors)
            elif schema.get("additionalProperties") is False:
                errors.append(path + ": unexpected " + name)
    if isinstance(value, list) and "items" in schema:
        for index, item in enumerate(value):
            validate(schema["items"], item, path + "[" + str(index) + "]", errors)


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--rid", required=True)
    parser.add_argument("--results", default=os.path.join(ROOT, "hosts-results.json"))
    parser.add_argument("--extra-publish-args", default="")
    args = parser.parse_args()

    rows = {}

    def record(row, ok, message):
        previous = rows.get(row, (True, ""))
        rows[row] = (previous[0] and ok, previous[1] if ok else ((previous[1] + "; ") if previous[1] else "") + message)
        print(("PASS  " if ok else "FAIL  ") + row + "  " + message)

    out = tempfile.mkdtemp(prefix="touchstone-aot-")
    publish = run(["dotnet", "publish", "src/Test.Aot", "-c", "Release", "-r", args.rid, "-o", out] + shlex.split(args.extra_publish_args))
    il_warnings = sorted(set(re.findall(r"(IL[23]\d{3})", publish.stdout + publish.stderr)))
    record("AOT-01", publish.returncode == 0 and not il_warnings,
           "native AOT publish for " + args.rid + (" has warnings " + ", ".join(il_warnings) if il_warnings else " has no IL2xxx or IL3xxx warnings"))
    if publish.returncode != 0:
        print(publish.stdout[-4000:])
        return finish(rows, args.results)

    binary = os.path.join(out, "Test.Aot.exe" if args.rid.startswith("win") else "Test.Aot")
    aot_json = os.path.join(out, "aot-results.json")
    native = run([binary, "--no-color", "--results", aot_json])
    record("AOT-02", native.returncode == 0, "native binary runs every Test.Shared suite, exit " + str(native.returncode))

    automated = ["dotnet", "run", "--project", "src/Test.Automated", "-c", "Release", "-f", "net10.0", "--no-build", "--"]
    for label, extra, env, expected in (("help", ["--help"], None, 0), ("invalid arguments", ["--bogus"], None, 3),
                                        ("failing case", ["--no-color", "--test", "ExitProbe.Fails"], {"TOUCHSTONE_EXIT_PROBE": "fail"}, 1)):
        jit = run(automated + extra, env).returncode
        aot = run([binary] + extra, env).returncode
        record("AOT-03", aot == expected and jit == expected, label + ": native " + str(aot) + ", JIT " + str(jit) + ", expected " + str(expected))
        if label != "failing case":
            record("CRUN-13", jit == expected, "Test.Automated " + label + " exit " + str(jit))

    if os.path.exists(aot_json):
        with open(os.path.join(ROOT, "schemas", "touchstone-results-1.0.schema.json"), encoding="utf-8") as handle:
            schema = json.load(handle)
        with open(aot_json, encoding="utf-8") as handle:
            document = json.load(handle)
        errors = []
        validate(schema, document, "$", errors)
        record("AOT-04", not errors, "native JSON export validates against the schema" + (": " + "; ".join(errors[:5]) if errors else ""))
        telemetry = [r for r in document["results"] if r["caseId"].startswith("TEL-")]
        record("AOT-05", telemetry and all(r["outcome"] == "passed" for r in telemetry), str(len(telemetry)) + " telemetry cases passed under native AOT")
        with open(args.results.replace(".json", "-aot-cases.json"), "w", encoding="utf-8") as handle:
            json.dump(document, handle)
    else:
        record("AOT-04", False, "native binary wrote no JSON")

    return finish(rows, args.results)


def finish(rows, path):
    document = {"schemaVersion": "1.0", "results": [
        {"caseId": row, "outcome": "passed" if ok else "failed", "message": message or None} for row, (ok, message) in sorted(rows.items())
    ]}
    with open(path, "w", encoding="utf-8") as handle:
        json.dump(document, handle, indent=2)
    failed = [row for row, (ok, _) in rows.items() if not ok]
    print("Host rows: " + str(len(rows)) + ", failed: " + str(len(failed)))
    return 1 if failed else 0


if __name__ == "__main__":
    sys.exit(main())
