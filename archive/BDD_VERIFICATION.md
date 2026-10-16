# BDD implementation verification

Date: 2026-10-16. Environment: macOS arm64, .NET SDKs 8.0.425 and 10.0.401. Release version: 0.3.0-alpha, explicitly authorized by the user's implementation and publication request.

## Implemented behavior

Core now supports code-first typed scenarios, immutable step definitions, isolated contexts, feature/requirement metadata, typed examples, sequential step execution, cancellation, owned-resource cleanup, separate cleanup errors, live step observers, structured results, and original-exception rethrowing. Existing ordinary cases remain compatible. Console/JSON/native host diagnostics and self-contained offline HTML reporting are implemented. New operation metrics and child activities use the existing Touchstone sources with bounded labels.

The Notes sample demonstrates create/fetch persistence, missing-note lookup, and invalid-input rejection using a fresh application per scenario execution. No update endpoint existed, so no unrelated API surface was added. Both compact and expanded output remain available.

## Verification evidence

| Check | Command or method | Result |
|---|---|---|
| Original baseline | Release build and solution tests in detached worktree at the pre-implementation commit | Pass; zero build warnings/errors; original framework suites pass |
| Release build | `dotnet build Touchstone.slnx -c Release` | Pass; 0 warnings, 0 errors |
| Shared console, .NET 8 | `dotnet run --project src/Test.Automated -c Release -f net8.0 --no-build -- --steps --results /tmp/touchstone-bdd-net8.json --html /tmp/touchstone-bdd.html` | 153 passed, 0 failed |
| Shared console, .NET 10 | Equivalent command with `-f net10.0` | 153 passed, 0 failed |
| All native framework hosts | `dotnet test Touchstone.slnx -c Release --no-build --logger trx` | Six shared host/target runs: 154 passed each. Three sample hosts: 10 passed each, plus one expected xUnit skip. Total: 954 passed, 0 failed, 1 skipped |
| Notes console | `dotnet run --project tests/Touchstone.SampleApp.Tests.Console -c Release --no-build -- --steps --results /tmp/touchstone-notes.json --html /tmp/touchstone-notes.html` | 9 passed, 0 failed, 1 expected skipped descriptor; exit 0 |
| Intentional failure | Sample console with `--fail --steps --results /tmp/touchstone-failure.json --html /tmp/touchstone-failure.html` | Exit 1; ordinary and scenario failures recorded |
| API signature compatibility | Reflection comparison of original and updated Core/Cli/three adapter assemblies | No removed signatures: 82 Core, 28 Cli, 6 xUnit, 4 NUnit, 3 MSTest signatures retained |
| Binary compatibility | Compile consumer against original 0.2.1 Core/Cli DLLs, replace only those DLLs with the updated assemblies, execute without rebuilding consumer | Pass; old executor, result collector, descriptor, and console calls work |
| Documentation example | Extract CounterScenarios and CounterContext from BDD.md into standalone Core-only consumer and execute | Pass, warnings treated as errors |
| Lifecycle coverage | Shared ScenarioSuites, consumed by all hosts | Covers guards, ordering, stop-on-failure, original stack, snapshot isolation, repeated/concurrent execution, skip, factory error/null, matching/unrelated cancellation, cleanup errors, observer failures, typed examples, counts, output, telemetry, and throwing listeners |
| Reporting coverage | Shared ScenarioSuites | Typed JSON and redaction, hostile HTML text, empty and 1,000-case reports, canceled export, and export I/O errors pass |
| Visual HTML review | Open generated Notes report in Chrome through native accessibility and screenshot APIs | Feature headings, statuses, step tables, and timing data render. Removed empty tables for ordinary tests after review |
| Benchmark gate contracts | `python3 benchmarks/verify-gate.py <complete-report.json>` | Seven checks pass: unchanged result, synthetic regression, missing probe, configuration mismatch, incomplete run, incorrect count, duplicate probe |
| Package build | `dotnet pack Touchstone.slnx -c Release --no-build -o /tmp/touchstone-packages` | Six .nupkg and six .snupkg files built |
| Package inspection | ZIP/nuspec inspection | Both target DLLs, XML docs, README, BDD/telemetry docs, icon, MIT license, and portable PDBs present. Core has no package dependencies |
| Diff hygiene | `git diff --check`, documentation scan, archive scan | Pass; no prohibited framework name in plan; no new em-dashes or credential files |

Temporary build/test logs and generated artifacts are under `/tmp/touchstone-*`; native test TRX files are under the ignored project TestResults directories. Reproducible commands are preserved here so the evidence does not depend on keeping temporary files.

## Issues found and resolved

The initial lifecycle tests found an incorrect success flag for skipped scenarios. Corrected it and verified that skipped scenarios neither create nor dispose contexts. An HTML export test was corrected to expect the actual DirectoryNotFoundException, rather than requiring an exact IOException type.

An intentionally throwing measurement listener originally faulted all activity traces, which interfered with subsequent .NET 8 capture tests when wrappers and rows ran in the same process. The test now scopes the injected error to its own trace, verifies that injection happened, and confirms that execution still succeeds. Combined .NET 8 host runs pass; no telemetry tests were disabled.

On macOS the managed peak-working-set API returned zero. The workload now uses Darwin getrusage as a documented fallback and emits its measurement in the CLI contract. The independent harness rejects missing memory data instead of silently accepting a partial run.

## Release completion

Implementation commit: `454907e4d14c717e6005447b7cb7bc38dda790ef`. Packages were rebuilt after that commit so their repository metadata identifies the actual implementation source. A separate consumer installed the metapackage from the built packages in an isolated cache and executed a scenario on both .NET 8 and .NET 10.

The two clean-source benchmark runs each contain all 96 expected samples and identical source/dataset/configuration hashes. The paired comparison reports no regression. Full provenance and measurements are committed under `benchmarks/baselines/`; `benchmarks/RESULTS.md` explains the measured costs and limitations. All seven gate-contract tests pass against the final baseline.

NuGet accepted every `.nupkg` and corresponding `.snupkg` with HTTP Created. The six published package IDs are Touchstone.Core, Touchstone.Cli, Touchstone.XunitAdapter, Touchstone.NunitAdapter, Touchstone.MstestAdapter, and Touchstone, all version 0.3.0-alpha. See BDD_NUGET_PUBLICATION.txt for the sanitized upload receipts and BDD_RELEASE_MANIFEST.json for local artifact hashes (before NuGet server signing). No credentials are stored in repository files, generated reports, or package contents.

Cross-platform CI is configured for macOS, Linux, and Windows; only macOS was available locally. A dedicated self-hosted benchmark runner must be provisioned before enabling the optional timing gate. Hosted runners still execute the correctness, build, and packaging checks. This is an operational follow-up, not a claim that remote CI has already passed.
