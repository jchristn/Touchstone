#!/usr/bin/env python3
"""Documentation, sample, style, and benchmark checks (catalog rows DOC-01 to DOC-06, TEL-22, SAMPLE-01 to SAMPLE-04,
PKG-07, BENCH-01 to BENCH-03).

Usage: python3 scripts/verify-docs.py [--results PATH] [--skip-slow]
Expects the solution to be built in Release. Writes a results file for scripts/verify-catalog.py.
"""
import argparse
import glob
import json
import os
import re
import shutil
import subprocess
import sys
import tempfile

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
DOCS_WITH_SAMPLES = ["README.md", "BDD.md", "MIGRATION.md", "TELEMETRY.md"]
FLAVORS = {
    "Touchstone.Xunit3Adapter": ("DocsXunit3", ["src/Touchstone.Xunit3Adapter/Touchstone.Xunit3Adapter.csproj"], []),
    "Touchstone.Xunit2Adapter": ("DocsXunit2", ["src/Touchstone.Xunit2Adapter/Touchstone.Xunit2Adapter.csproj"], []),
    "Touchstone.NunitAdapter": ("DocsNunit", ["src/Touchstone.NunitAdapter/Touchstone.NunitAdapter.csproj"], []),
    "Touchstone.MstestAdapter": ("DocsMstest", ["src/Touchstone.MstestAdapter/Touchstone.MstestAdapter.csproj"], []),
}
EXTERNAL = {"OpenTelemetry": ['<PackageReference Include="OpenTelemetry" Version="1.13.1" />', '<PackageReference Include="OpenTelemetry.Exporter.OpenTelemetryProtocol" Version="1.13.1" />']}
DASH_EXEMPT = ("archive/", "benchmarks/baselines/", "benchmarks/results/")


def run(args, env=None, cwd=ROOT):
    merged = dict(os.environ)
    if env:
        merged.update(env)
    return subprocess.run(args, capture_output=True, text=True, env=merged, cwd=cwd)


def csharp_blocks(path):
    with open(os.path.join(ROOT, path), encoding="utf-8") as handle:
        text = handle.read()
    return [b for b in re.findall(r"```csharp\n(.*?)```", text, re.S) if "namespace " in b]


def project(directory, name, references, packages):
    os.makedirs(directory, exist_ok=True)
    refs = "\n".join('    <ProjectReference Include="' + os.path.join(ROOT, r) + '" />' for r in references)
    pkgs = "\n".join("    " + p for p in packages)
    with open(os.path.join(directory, name + ".csproj"), "w", encoding="utf-8") as handle:
        handle.write('<Project Sdk="Microsoft.NET.Sdk">\n  <PropertyGroup>\n    <TargetFramework>net10.0</TargetFramework>\n'
                     "    <Nullable>enable</Nullable>\n    <ImplicitUsings>disable</ImplicitUsings>\n    <TreatWarningsAsErrors>true</TreatWarningsAsErrors>\n"
                     "    <NoWarn>CS1591</NoWarn>\n  </PropertyGroup>\n  <ItemGroup>\n" + refs + "\n" + pkgs + "\n  </ItemGroup>\n</Project>\n")


def compile_samples(record):
    work = os.path.realpath(tempfile.mkdtemp(prefix="touchstone-docs-"))
    try:
        for doc in DOCS_WITH_SAMPLES:
            if not os.path.exists(os.path.join(ROOT, doc)):
                continue
            blocks = csharp_blocks(doc)
            if not blocks:
                continue
            base = os.path.join(work, doc.replace(".", "_"))
            shared_dir = os.path.join(base, "Shared")
            shared_refs = ["src/Touchstone.Core/Touchstone.Core.csproj", "src/Touchstone.Cli/Touchstone.Cli.csproj"]
            shared_packages = []
            flavored = {}
            shared_count = 0
            for index, block in enumerate(blocks):
                flavor = next((f for f in FLAVORS if "using " + f + ";" in block), None)
                for marker, packages in EXTERNAL.items():
                    if "using " + marker in block:
                        shared_packages += packages
                if flavor is None:
                    os.makedirs(shared_dir, exist_ok=True)
                    with open(os.path.join(shared_dir, "Sample" + str(index) + ".cs"), "w", encoding="utf-8") as handle:
                        handle.write(block)
                    shared_count += 1
                else:
                    flavored.setdefault(flavor, []).append((index, block))
            project(shared_dir, "DocsShared", shared_refs, sorted(set(shared_packages)))
            packages = {
                "DocsXunit3": ['<PackageReference Include="xunit.v3.extensibility.core" Version="4.0.2" />'],
                "DocsXunit2": ['<PackageReference Include="xunit.extensibility.core" Version="2.9.3" />'],
                "DocsNunit": ['<PackageReference Include="NUnit" Version="5.0.0" />'],
                "DocsMstest": ['<PackageReference Include="MSTest.TestFramework" Version="4.4.1" />'],
            }
            results = []
            build = run(["dotnet", "build", os.path.join(shared_dir, "DocsShared.csproj"), "-v", "q", "-nologo"])
            results.append(("shared", build))
            for flavor, items in flavored.items():
                name, refs, _ = FLAVORS[flavor]
                directory = os.path.join(base, name)
                os.makedirs(directory, exist_ok=True)
                for index, block in items:
                    with open(os.path.join(directory, "Sample" + str(index) + ".cs"), "w", encoding="utf-8") as handle:
                        handle.write(block)
                project(directory, name, refs + [os.path.join(shared_dir, "DocsShared.csproj")], packages[name])
                results.append((name, run(["dotnet", "build", os.path.join(directory, name + ".csproj"), "-v", "q", "-nologo"])))
            for label, result in results:
                errors = sorted(set(re.findall(r"(Sample\d+\.cs\(\d+,\d+\): error [A-Z0-9]+: [^\[]+)", result.stdout)))
                if result.returncode != 0 and not errors:
                    errors = sorted(set(re.findall(r"(error [A-Z0-9]+: [^\[]+)", result.stdout + result.stderr))) or [(result.stdout + result.stderr)[-600:]]
                record("DOC-01", result.returncode == 0, doc + " " + label + " samples compile" + (": " + "; ".join(errors[:6]) if result.returncode else ""))
            print("INFO  " + doc + ": " + str(len(blocks)) + " C# samples compiled")
    finally:
        shutil.rmtree(work, ignore_errors=True)


def tracked(patterns):
    return run(["git", "ls-files", "-co", "--exclude-standard"] + patterns).stdout.split()


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--results", default=os.path.join(ROOT, "docs-results.json"))
    parser.add_argument("--skip-slow", action="store_true")
    args = parser.parse_args()
    rows = {}

    def record(row, ok, message):
        previous = rows.get(row, (True, ""))
        rows[row] = (previous[0] and ok, previous[1] if ok else ((previous[1] + "; ") if previous[1] else "") + message)
        print(("PASS  " if ok else "FAIL  ") + row + "  " + message)

    compile_samples(record)

    props = open(os.path.join(ROOT, "Directory.Build.props"), encoding="utf-8").read()
    version = re.search(r"<Version>([^<]+)</Version>", props).group(1)
    readme = open(os.path.join(ROOT, "README.md"), encoding="utf-8").read()
    record("DOC-02", re.search(r"^v" + re.escape(version) + r"$", readme, re.M) is not None, "README states version " + version)
    stale = sorted(set(v for v in re.findall(r"\b\d+\.\d+\.\d+(?:-[a-z]+)?\b", readme) if v != version and re.match(r"0\.\d+\.\d+", v)))
    record("DOC-02", not stale, "README has no other Touchstone version strings" + (": " + ", ".join(stale) if stale else ""))
    changelog = open(os.path.join(ROOT, "CHANGELOG.md"), encoding="utf-8").read()
    record("DOC-03", "## [v" + version + "]" in changelog, "CHANGELOG has a section for v" + version)
    record("DOC-04", "\n## AOT support\n" in readme, "README has the AOT support heading")

    unshipped = []
    for path in sorted(glob.glob(os.path.join(ROOT, "src", "*", "PublicAPI.Unshipped.txt"))):
        lines = [line for line in open(path, encoding="utf-8").read().splitlines() if line.strip() and not line.startswith("#")]
        if lines:
            unshipped.append(os.path.relpath(path, ROOT) + " (" + str(len(lines)) + ")")
    stable = "-" not in version
    record("PKG-07", not stable or not unshipped,
        ("PublicAPI.Unshipped.txt files are empty for stable version " + version if stable else "prerelease " + version + " may carry unshipped API")
        + (": " + ", ".join(unshipped) if unshipped and stable else ""))

    baseline = json.load(open(os.path.join(ROOT, "benchmarks", "baselines", "macos-arm64.json"), encoding="utf-8"))
    comparison = json.load(open(os.path.join(ROOT, "benchmarks", "baselines", "macos-arm64-comparison.json"), encoding="utf-8"))
    benchmark_results = open(os.path.join(ROOT, "benchmarks", "RESULTS.md"), encoding="utf-8").read()
    baseline_ok = (baseline.get("Complete") is True and baseline.get("DirtyState") == ""
        and comparison.get("Regression") is False and comparison.get("BaselineCommit") == baseline.get("Commit")
        and baseline.get("Commit", "") in benchmark_results)
    record("BENCH-03", baseline_ok, "committed baseline " + str(baseline.get("Commit", ""))[:7]
        + " is clean and complete, its confirmation comparison passes, and RESULTS.md cites it")

    offenders = []
    for path in tracked(["*.md", "*.cs", "*.csproj", "*.props", "*.targets", "*.yaml", "*.py", "*.json", "*.bat", "*.sh"]):
        if path.startswith(DASH_EXEMPT) or not os.path.isfile(os.path.join(ROOT, path)):
            continue
        text = open(os.path.join(ROOT, path), encoding="utf-8", errors="replace").read()
        fenced = False
        for number, line in enumerate(text.split("\n"), 1):
            if path.endswith(".md") and line.lstrip().startswith("```"):
                fenced = not fenced
                continue
            prose = (path.endswith(".md") and not fenced and not line.lstrip().startswith("|")) or line.lstrip().startswith(("//", "#", "<!--"))
            if "\u2014" in line or (prose and re.search(r"[A-Za-z0-9.,)] -- [A-Za-z(]", line)):
                offenders.append(path + ":" + str(number))
    record("DOC-05", not offenders, "no em-dash or ' -- ' stand-in in tracked files" + (": " + ", ".join(offenders[:10]) if offenders else ""))
    usings = run([sys.executable, os.path.join(ROOT, "scripts", "check-usings.py")])
    record("DOC-06", usings.returncode == 0, usings.stdout.strip().splitlines()[-1] if usings.stdout.strip() else "check-usings produced no output")

    constants = re.findall(r'public const string \w+ = "([^"]+)";', open(os.path.join(ROOT, "src/Touchstone.Core/TouchstoneTelemetry.cs"), encoding="utf-8").read())
    telemetry = open(os.path.join(ROOT, "TELEMETRY.md"), encoding="utf-8").read()
    missing = [c for c in constants if "`" + c + "`" not in telemetry]
    record("TEL-22", not missing, "TELEMETRY.md documents every TouchstoneTelemetry value" + (": missing " + ", ".join(missing) if missing else ""))
    current = telemetry.split("## Changes in 1.0")[0]
    documented = set(re.findall(r"`(touchstone\.[a-z0-9_.]+)`", current))
    unknown = sorted(n for n in documented if n not in constants)
    record("TEL-22", not unknown, "TELEMETRY.md names no metric or span that does not exist" + (": " + ", ".join(unknown) if unknown else ""))

    sample_files = tracked(["test/Touchstone.SampleApp.Tests.*", "src/Touchstone.SampleApp/*"])
    localhost = [p for p in sample_files if os.path.isfile(os.path.join(ROOT, p)) and "localhost" in open(os.path.join(ROOT, p), encoding="utf-8", errors="replace").read()]
    record("SAMPLE-03", not localhost, "sample clients use 127.0.0.1" + (": localhost in " + ", ".join(localhost) if localhost else ""))
    shared = os.path.join(ROOT, "test", "Touchstone.SampleApp.Tests.Shared")
    shared_sources = {name: open(os.path.join(shared, name), encoding="utf-8").read() for name in sorted(os.listdir(shared)) if name.endswith(".cs")}
    hooks = [n for n, text in shared_sources.items() if "beforeSuiteAsync" in text or "afterSuiteAsync" in text]
    factories = [n for n, text in shared_sources.items() if "WebApplicationFactory" in text and n != "SampleApiHost.cs"]
    host = shared_sources.get("SampleApiHost.cs", "")
    reload_off = 'UseSetting("hostBuilder:reloadConfigOnChange", "false")' in host
    record("SAMPLE-04", not hooks and not factories and reload_off,
           "sample suites own no servers at build time and use no suite hooks; SampleApiHost disables config reload"
           + (": hooks in " + ", ".join(hooks) if hooks else "")
           + (": WebApplicationFactory outside SampleApiHost in " + ", ".join(factories) if factories else "")
           + ("" if reload_off else ": reload not disabled"))
    if not args.skip_slow:
        sample = ["dotnet", "run", "--project", "test/Touchstone.SampleApp.Tests.Console", "-c", "Release", "--no-build", "--", "--no-color"]
        full = run(sample)
        record("SAMPLE-01", full.returncode == 0 and "Failed: 0" in full.stdout, "sample console host passes (framework hosts run in scripts/run-native-hosts.py)")
        smoke = run(sample + ["--tag", "smoke"])
        record("SAMPLE-02", smoke.returncode == 0 and "Filtered:" in smoke.stdout and "Total: 1 " in smoke.stdout, "sample console --tag smoke selects the tagged case")
        gate = run([sys.executable, os.path.join(ROOT, "benchmarks", "verify-gate.py")])
        record("BENCH-01", gate.returncode == 0, "benchmark comparison gate logic" + ("" if gate.returncode == 0 else ": " + (gate.stdout + gate.stderr)[-500:]))
        workload = os.path.join(ROOT, "src", "Benchmark.Workload", "bin", "Release", "net10.0", "Touchstone.Benchmark.Workload.dll")
        for arm, steps in (("floor", 0), ("delegate", 0), ("one", 1), ("many", 5)):
            result = run(["dotnet", workload, arm, "4", "on"])
            try:
                sample_result = json.loads(result.stdout.strip().splitlines()[-1])
                cases = 0 if arm == "floor" else 4
                ok = result.returncode == 0 and not sample_result["Failed"] and sample_result["Cases"] == cases and sample_result["Steps"] == cases * steps
            except (ValueError, IndexError, KeyError):
                ok = False
            record("BENCH-02", ok, "benchmark workload arm " + arm + " runs against the 1.0 API")

    document = {"schemaVersion": "1.0", "results": [
        {"caseId": row, "outcome": "passed" if ok else "failed", "message": message or None} for row, (ok, message) in sorted(rows.items())
    ]}
    with open(args.results, "w", encoding="utf-8") as handle:
        json.dump(document, handle, indent=2)
    failed = [row for row, (ok, _) in rows.items() if not ok]
    print("Docs rows: " + str(len(rows)) + ", failed: " + str(len(failed)))
    return 1 if failed else 0


if __name__ == "__main__":
    sys.exit(main())
