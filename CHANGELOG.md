# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

### Added

- Test.Shared, Test.Automated, Test.Xunit, Test.Nunit, and Test.Mstest projects. All runners execute the same shared suites covering descriptors, result models, the executor, the console runner and sink, JSON export, and every framework adapter, with positive and negative cases

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
