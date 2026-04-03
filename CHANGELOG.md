# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [v0.1.0] - 2026-04-02

### Added

- Touchstone.Core: runner-agnostic test descriptor model (TestCaseDescriptor, TestSuiteDescriptor, TestResult, TestRunSummary, ITestResultSink, TestExecutor)
- Touchstone.Cli: console test runner with colored tabular output, JSON export, and exit code contract
- Touchstone.XunitAdapter: xUnit adapters (theory-driven and fact-style) for running shared descriptors under dotnet test
- Touchstone.SampleApp: reference Notes CRUD API with shared integration tests exercising both runners
- Multi-target support for net8.0 and net10.0
