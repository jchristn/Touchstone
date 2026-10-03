# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

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
