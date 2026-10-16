"""Integration checks for comparison exit codes and incomplete-data rejection."""
from copy import deepcopy
from pathlib import Path
import json
import subprocess
import sys
import tempfile

root = Path(__file__).resolve().parent.parent
baseline = root / (sys.argv[1] if len(sys.argv) > 1 else "benchmarks/baselines/macos-arm64.json")
report = json.loads(baseline.read_text())
dll = root / "src/Test.Benchmark/bin/Release/net10.0/Touchstone.Benchmark.dll"
with tempfile.TemporaryDirectory(prefix="touchstone-gate-") as scratch:
    directory = Path(scratch)
    variants = [("same", deepcopy(report), 0)]
    slower = deepcopy(report)
    for sample in slower["Samples"]:
        sample["ElapsedMs"] *= 3
        sample["ProcessMs"] *= 3
        sample["AllocatedBytes"] *= 3
        sample["PeakWorkingSetBytes"] *= 3
    variants.append(("regression", slower, 1))
    partial = deepcopy(report)
    partial["Samples"].pop()
    variants.append(("missing-probe", partial, 2))
    mismatch = deepcopy(report)
    mismatch["ConfigurationHash"] = "different"
    variants.append(("mismatched-configuration", mismatch, 2))
    incomplete = deepcopy(report)
    incomplete["Complete"] = False
    variants.append(("incomplete", incomplete, 2))
    incorrect = deepcopy(report)
    incorrect["Samples"][0]["Cases"] = -1
    variants.append(("incorrect-count", incorrect, 2))
    duplicate = deepcopy(report)
    duplicate["Samples"][1] = deepcopy(duplicate["Samples"][0])
    variants.append(("duplicate-probe", duplicate, 2))
    for name, candidate, expected in variants:
        path = directory / f"{name}.json"
        path.write_text(json.dumps(candidate))
        result = subprocess.run(["dotnet", str(dll), "compare", str(baseline), str(path), str(directory / f"{name}-comparison.json")], cwd=root, text=True, capture_output=True)
        if result.returncode != expected:
            raise SystemExit(f"{name}: expected {expected}, got {result.returncode}: {result.stdout}{result.stderr}")
        if expected in (0, 1):
            assert (directory / f"{name}-comparison.json").exists()
            assert (directory / f"{name}-comparison.md").exists()
        print(f"PASS {name}: exit {expected}")
