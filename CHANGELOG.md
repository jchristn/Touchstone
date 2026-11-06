# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

## [v1.0.0] - 2026-11-05

The first stable release. Every breaking change is listed below and in MIGRATION.md.

### Breaking

- Packages: `Touchstone.XunitAdapter` is renamed `Touchstone.Xunit2Adapter`. The `Touchstone` metapackage contains only Core and Cli; add the adapter for your framework.
- Descriptors are immutable. `TestCaseDescriptor` and `TestSuiteDescriptor` have no setters; use `WithTags`, `WithSkip`, `WithoutSkip`, and `WithDisplayName`. `TestCaseDescriptor.ExecuteAsync` is no longer public.
- The `skip` constructor parameter is removed; a non-null `skipReason` means skipped.
- Identities follow a grammar: suite identities use letters, digits, `_`, and `-`; case identities also allow `.`. Display names cannot be blank. Duplicate case and suite identities throw `DuplicateTestIdException`.
- `TestResult` and `TestRunSummary` are immutable. `TestResult.Success`, `Skipped`, and `Canceled` are replaced by `Outcome`.
- Every `TestExecutor` entry point takes `TestRunOptions? options`, and the `CancellationToken` is always last.
- A failing `beforeSuiteAsync` no longer aborts the run: the suite's cases fail, teardown runs, and the failure is reported in `TestRunSummary.LifecycleErrors`.
- Caller cancellation no longer throws from `RunAsync`; the summary reports `WasCanceled`, and unstarted cases are `NotRun`. Cancellation classification is the same for ordinary cases and scenarios.
- `IScenarioStepSink` is merged into `ITestResultSink`; `OnTestCompletedAsync` is renamed `OnCaseCompletedAsync`.
- Console runner: `ConsoleRunner.RunAsync(suites, ConsoleRunnerOptions?, ct)` and `RunMainAsync(suites, args, ct)` replace the previous overloads; exit codes are 0, 1, 2 (canceled), and 3 (invalid arguments). `ConsoleResultSink` takes `ConsoleRunnerOptions`.
- `ScenarioHtmlReport` is replaced by `HtmlReport`. `JsonResultEntry` and `JsonStepEntry` are no longer public.
- JSON results are a versioned document (`schemaVersion` 1.0) instead of a bare array, with `outcome` in place of `success` and `skipped`.
- Adapter types follow one shape per framework: `Touchstone{Framework}Cases`, `Touchstone{Framework}`, and `Touchstone{Framework}FactBase`. The xUnit and MSTest data sources take an `ITestSuiteProvider` type and yield serializable row keys.
- Telemetry names are frozen with fixed span names (`touchstone.suite`, `touchstone.case`, and so on), renamed count attributes, the `case_completed` sink event, and lowercase scenario labels. See TELEMETRY.md, "Changes in 1.0".

### Added

- `Touchstone.Xunit3Adapter` for xUnit v3 4.0.2 and later, with native row skips, display names, and traits.
- `TestRunOptions` and `TestFilter`: tag, suite, and test identity filtering; `SkipBehavior` (`Report`, `Omit`, `Fail`); `StopOnFirstFailure`; `CleanupTimeout` for teardown after cancellation.
- `TestOutcome` with `Canceled` and `NotRun`; `TestRunSummary.Filtered`, `WasCanceled`, and `LifecycleErrors`; `SuiteLifecycleError`.
- `ITestResultSink` run, case-start, and step callbacks, each with a default implementation.
- `TestRunAssert` for hosts that turn a run or a case into a pass or failure; `ITestSuiteProvider`.
- Console runner command-line options (`--tag`, `--exclude-tag`, `--suite`, `--test`, `--skip`, `--stop-on-failure`, `--results`, `--html`, `--expand`, `--no-color`, `--help`) and Ctrl+C cancellation.
- `JsonReport` with the published results schema (`JsonReport.Schema`, and `schemas/touchstone-results-1.0.schema.json` in the package); `ReportOptions`.
- Native skips, display names, and tag categories in every adapter, and serializable row keys so Test Explorer and `--list-tests` show one test per case.
- Native AOT support for Touchstone.Core and Touchstone.Cli, verified by `src/Test.Aot` on Linux, Windows, and macOS.
- `touchstone.cases.filtered` metric, `not_run` case outcome, and canceled, not-run, and filtered counts on run and suite spans.
- MIGRATION.md.

### Changed

- The README walkthrough covers every host, filtering, skip behavior, cancellation, results, AOT support, and the support policy.
- Public API tracking (`PublicAPI.Shipped.txt` and `PublicAPI.Unshipped.txt`) and package validation for every package.
- Test architecture: a 355-row test catalog in archive/V1.0_PLAN.md; contract suites keyed by catalog ID; per-adapter contract suites; native outcome parity fixtures; consumer smoke tests against locally packed packages; and CI on every branch for Linux, Windows, and macOS.
- Nullable reference types and explicit usings for every project; `tests/` renamed to `test/`.

### Fixed

- Skipped cases behaved differently per framework: NUnit and MSTest dropped them, and xUnit reported them as passed.
- Run-all base classes discarded the original exceptions; `AggregateException.InnerExceptions` now holds them.
- `ConsoleResultSink` changed the process console color even when writing to another writer.
- MSTest rows were not serializable, so Test Explorer and category filters saw a single test.
- Ordinary cases ignored cancellation between cases and reported caller cancellation as a failure.
- Test identities could collide when a suite identity contained `.`.
- Scenario metric labels used PascalCase outcomes while every other metric used lowercase.

### Removed

- `TestCaseDescriptor.Skip`, the `skip` parameter, and every descriptor and result setter.
- `IScenarioStepSink`, `ScenarioHtmlReport`, `JsonResultEntry`, `JsonStepEntry`, `TouchstoneTheoryData`, `TouchstoneTestBase`, `TouchstoneFactBase`, `TouchstoneTestCaseSource`, `TouchstoneNunitBase`, `TouchstoneDynamicData`, and `TouchstoneMstestBase`.

## [v0.3.0-alpha] - 2026-10-16

### Added

- Optional code-first BDD scenarios in Core: typed per-execution context, reusable Given/When/Then/And steps, immutable definitions, feature/requirement metadata, and typed example expansion.
- Structured single-case execution results, step timing and outcomes, matching caller cancellation, guaranteed context disposal, and separate cleanup diagnostics that preserve primary exceptions.
- Optional live step observers, expanded console output, additive typed JSON fields, redaction callbacks, and self-contained accessible HTML execution reports.
- Host-neutral scenario diagnostics for native xUnit, NUnit, and MSTest output. Shared test and sample hosts exercise the same scenario definitions.
- Scenario context/step/cleanup metrics and nested spans on the existing Touchstone sources, plus shared lifecycle, reporting, telemetry, and adapter coverage.
- Reproducible CLI overhead workload, independent benchmark harness, paired comparison gate, committed baselines, and CI checks.
- BDD authoring and execution documentation; sample Notes persistence, missing-note, and validation scenarios.

### Changed

- Library projects enable nullable reference analysis and explicit usings without suppressing diagnostics.
- NuGet packages include the MIT license file and BDD/telemetry documentation alongside README, icon, and portable symbol packages.
- Sample HTTP clients use `127.0.0.1` loopback base addresses.

### Compatibility

- Existing descriptor constructors, executor overloads, compact console output, JSON root array, and case counts remain compatible. Built scenario delegates cannot be replaced; ordinary descriptor delegates remain mutable.
- Scenario cancellation returns a failed logical case with `Canceled = true`; throwing entry points preserve `OperationCanceledException`. Native runner outcome labels may differ.


## [v0.2.1] - 2026-10-03

### Changed

- Touchstone.NunitAdapter now depends on NUnit 5.0.0 (was 4.3.2). Consumers must reference NUnit 5 or later
- Touchstone.MstestAdapter now depends on MSTest.TestFramework 4.4.1 (was 4.0.2)
- Test and sample projects: MSTest 4.4.1, NUnit 5.0.0, NUnit.Analyzers 4.15.0, NUnit3TestAdapter 6.3.0, xunit.runner.visualstudio 4.0.0, Microsoft.NET.Test.Sdk 18.10.1, coverlet.collector 10.1.0, Microsoft.AspNetCore.Mvc.Testing 10.0.12

### Added

- `AdapterSuites.FrameworkVersionSuite`: verifies the resolved xunit, NUnit, and MSTest.TestFramework versions meet the adapters' minimums

## [v0.2.0] - 2026-10-02

### Added

- Telemetry: a `Meter` and `ActivitySource` named `Touchstone` (BCL only, no exporter dependency). Spans for runs, suites, `before_suite`/`after_suite` stages, cases, sink callbacks, and JSON export, with explicit status and exception events. Metrics for run, suite, stage, case, sink, and export counts and durations by outcome, `touchstone.errors` by stage and `error.type`, `touchstone.cases.active`, per-suite and per-run last-success timestamps, and `touchstone.build.info`. See TELEMETRY.md
- `TouchstoneTelemetry`: public constants for every meter, source, metric, span, label, and attribute name, plus the `SuiteLabelEnabled` setting
- `TestExecutor.RunAsync`: run several suites under one run span and return an aggregated summary
- `TestExecutor.ExecuteCaseAsync`: run one case with telemetry and rethrow its exception, for theory-style hosts
- `TestResultCollector`: an in-memory `ITestResultSink`
- Telemetry test suite (`src/Test.Shared/TelemetrySuites.cs`) using in-memory listeners, covering every span and metric, the failure paths, a throwing collector, and the no-listener path
- Test.Shared, Test.Automated, Test.Xunit, Test.Nunit, and Test.Mstest projects. All runners execute the same shared suites covering descriptors, result models, the executor, the console runner and sink, JSON export, and every framework adapter, with positive and negative cases

### Changed

- `ConsoleRunner.RunAsync` and the xUnit, NUnit, and MSTest `RunAllAsync` base methods now run through `TestExecutor.RunAsync`, so they emit telemetry. Behavior and failure messages are unchanged
- Theory-style test hosts and README examples call `TestExecutor.ExecuteCaseAsync`
- `TestExecutor` awaits with `ConfigureAwait(false)`

### Fixed

- Touchstone.XunitAdapter no longer gets picked up as a test project by `dotnet test` at the solution level

### Removed

- tests/Touchstone.Core.Tests (its cases were migrated into Test.Shared)

## [v0.1.12] - 2026-04-03

### Changed

- NugetPublish.bat now publishes symbol packages (.snupkg) alongside NuGet packages

## [v0.1.1] - 2026-04-03

### Added

- Touchstone.NunitAdapter: NUnit adapter (TestCaseSource-driven and single-test) for running shared descriptors under NUnit
- Touchstone.MstestAdapter: MSTest adapter (DynamicData-driven and single-test) for running shared descriptors under MSTest
- Sample app test hosts for NUnit and MSTest
- Touchstone metapackage now includes all five adapter packages

## [v0.1.0] - 2026-04-02

### Added

- Touchstone.Core: runner-agnostic test descriptor model (TestCaseDescriptor, TestSuiteDescriptor, TestResult, TestRunSummary, ITestResultSink, TestExecutor)
- Touchstone.Cli: console test runner with colored tabular output, JSON export, and exit code contract
- Touchstone.XunitAdapter: xUnit adapters (theory-driven and fact-style) for running shared descriptors under dotnet test
- Touchstone.SampleApp: reference Notes CRUD API with shared integration tests exercising both runners
- Multi-target support for net8.0 and net10.0
