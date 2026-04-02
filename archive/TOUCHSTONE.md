# Touchstone Plan

Last updated: 2026-04-02

## Status Key

- `[ ]` not started
- `[-]` in progress
- `[x]` complete
- `Owner:` assign a developer when work begins
- `Notes:` use for links, blockers, and decisions

## Decision Summary

- [x] Externalize the reusable test scaffolding into `Touchstone.*`.
- [x] Keep product-specific tests and fixtures in each consumer repo.
- [x] Use three packages in v1: `Touchstone.Core`, `Touchstone.Cli`, and `Touchstone.Xunit`.
- [x] Build the package surface intentionally from scratch; do not extract Watson types verbatim.
- [x] Validate the API with a sample app before treating the package shape as stable.
- [x] Avoid reflection discovery, source generators, and Watson-specific helpers in v1.

## Goals

- [ ] Provide a reusable, runner-agnostic test descriptor model.
- [ ] Support both CI execution via xUnit and readable local execution via a console runner.
- [ ] Make it easy for a consumer repo to keep all real tests in one shared project with thin host projects.
- [ ] Preserve strong per-test isolation by default.
- [ ] Allow optional suite lifecycle hooks without making them mandatory.

## Non-Goals

- [ ] Do not package Watson-specific fixtures such as loopback server helpers.
- [ ] Do not standardize the legacy parity-fixture pattern.
- [ ] Do not make xUnit the core execution model.
- [ ] Do not force one xUnit style; support both theory-driven and fact-style adapters.
- [ ] Do not optimize for plugin-style discovery in v1.

## Proposed Solution Layout

```text
C:\Code\Touchstone
|-- TOUCHSTONE.md
|-- src
|   |-- Touchstone.Core
|   |-- Touchstone.Cli
|   |-- Touchstone.Xunit
|   `-- Touchstone.SampleApp
`-- tests
    |-- Touchstone.SampleApp.Tests.Shared
    |-- Touchstone.SampleApp.Tests.Xunit
    `-- Touchstone.SampleApp.Tests.Console
```

## Package Boundaries

### `Touchstone.Core`

- [ ] `TestCaseDescriptor`
- [ ] `TestSuiteDescriptor`
- [ ] `TestResult`
- [ ] `TestRunSummary`
- [ ] `TestExecutor`
- [ ] `ITestResultSink`
- [ ] Optional suite hooks only
- [ ] Stable composite test identity
- [ ] Full exception capture, not just exception messages

### `Touchstone.Cli`

- [ ] Console runner over `Touchstone.Core`
- [ ] Colored output using `System.Console`
- [ ] Per-suite and overall summaries
- [ ] Exit code contract for CI or local scripts
- [ ] Optional JSON result export

### `Touchstone.Xunit`

- [ ] Theory adapter for descriptor-driven tests
- [ ] Fact-style adapter for repos that want one method per test case
- [ ] Minimal xUnit-only utilities
- [ ] Strong display names for discovered test cases

## Core API Sketch

This is not final code. It is the intended v1 shape to guide implementation.

```csharp
namespace Touchstone.Core;

public sealed class TestCaseDescriptor
{
    public string SuiteId { get; }
    public string CaseId { get; }
    public string DisplayName { get; }
    public IReadOnlyList<string> Tags { get; }
    public Func<CancellationToken, Task> ExecuteAsync { get; }
    public override string ToString();
}

public sealed class TestSuiteDescriptor
{
    public string SuiteId { get; }
    public string DisplayName { get; }
    public IReadOnlyList<TestCaseDescriptor> Cases { get; }
    public Func<CancellationToken, ValueTask>? BeforeSuiteAsync { get; }
    public Func<CancellationToken, ValueTask>? AfterSuiteAsync { get; }
}

public sealed class TestResult
{
    public string TestId { get; }
    public string SuiteId { get; }
    public string CaseId { get; }
    public string DisplayName { get; }
    public bool Success { get; }
    public bool Skipped { get; }
    public TimeSpan Duration { get; }
    public Exception? Exception { get; }
    public string? Message { get; }
}

public sealed class TestRunSummary
{
    public int Total { get; }
    public int Passed { get; }
    public int Failed { get; }
    public int Skipped { get; }
    public TimeSpan Duration { get; }
}

public interface ITestResultSink
{
    ValueTask OnSuiteStartedAsync(TestSuiteDescriptor suite, CancellationToken cancellationToken);
    ValueTask OnTestCompletedAsync(TestResult result, CancellationToken cancellationToken);
    ValueTask OnSuiteCompletedAsync(TestSuiteDescriptor suite, TestRunSummary summary, CancellationToken cancellationToken);
}

public static class TestExecutor
{
    public static Task<TestRunSummary> RunSuiteAsync(
        TestSuiteDescriptor suite,
        ITestResultSink? sink = null,
        CancellationToken cancellationToken = default);
}
```

## Sample App

The sample app is required. It proves the package surface before Watson migration.

### App Shape

- [ ] `Touchstone.SampleApp` is a small ASP.NET Core minimal API.
- [ ] Endpoints:
  - `GET /health`
  - `POST /notes`
  - `GET /notes/{id}`
  - `DELETE /notes/{id}`
- [ ] Storage is in-memory only.
- [ ] The app must be simple enough to understand quickly and rich enough to require async tests, setup, and failure reporting.

### Sample Test Projects

- [ ] `Touchstone.SampleApp.Tests.Shared`
  - Contains all real test cases as shared descriptors
  - Mix of unit and integration-style cases
  - Includes at least one skipped case to verify reporting
- [ ] `Touchstone.SampleApp.Tests.Xunit`
  - Runs the shared cases in xUnit
  - Demonstrates theory-based execution
  - Demonstrates fact-style execution
- [ ] `Touchstone.SampleApp.Tests.Console`
  - Runs the shared cases through the console runner
  - Produces readable local output and non-zero exit codes on failure

### Required Sample Cases

- [ ] `GET /health` returns `200 OK`
- [ ] Creating a note returns an id
- [ ] Fetching a created note returns the stored payload
- [ ] Deleting a note removes it
- [ ] Fetching a missing note returns `404`
- [ ] Invalid payload returns `400`
- [ ] At least one intentional skip path is represented
- [ ] At least one failing-case fixture can be toggled locally to verify failure rendering

## Consumer Repo Shape

Each adopting repo should look like this:

```text
tests
|-- MyLib.Tests.Shared
|-- MyLib.Tests.Xunit
`-- MyLib.Tests.Console
```

- [ ] `MyLib.Tests.Shared` contains the actual test descriptors and repo-specific helpers.
- [ ] `MyLib.Tests.Xunit` is a thin host over `Touchstone.Xunit`.
- [ ] `MyLib.Tests.Console` is a thin host over `Touchstone.Cli`.

## Implementation Phases

## Phase 1: Bootstrap

- [ ] Create the solution and package projects
  - Owner:
  - Notes:
- [ ] Add baseline `Directory.Build.props`
  - Owner:
  - Notes:
- [ ] Decide target framework(s)
  - Owner:
  - Notes:
- [ ] Add package metadata placeholders
  - Owner:
  - Notes:

Acceptance criteria:

- [ ] `dotnet build` succeeds on an empty but wired solution
- [ ] Project references match the intended package boundaries

## Phase 2: Core Contracts

- [ ] Implement `TestCaseDescriptor`
  - Owner:
  - Notes:
- [ ] Implement `TestSuiteDescriptor`
  - Owner:
  - Notes:
- [ ] Implement `TestResult`
  - Owner:
  - Notes:
- [ ] Implement `TestRunSummary`
  - Owner:
  - Notes:
- [ ] Implement `ITestResultSink`
  - Owner:
  - Notes:
- [ ] Implement `TestExecutor`
  - Owner:
  - Notes:
- [ ] Ensure `ToString()` returns a useful display name
  - Owner:
  - Notes:
- [ ] Ensure result identity is composite and stable
  - Owner:
  - Notes:
- [ ] Capture full exception objects and useful messages
  - Owner:
  - Notes:

Acceptance criteria:

- [ ] Core tests pass
- [ ] The executor can run a suite with no sink
- [ ] The executor can run a suite with a sink
- [ ] A failing test captures duration, ids, and exception details

## Phase 3: Console Runner

- [ ] Implement console sink/formatter
  - Owner:
  - Notes:
- [ ] Implement console app host
  - Owner:
  - Notes:
- [ ] Add colored pass/fail/skip rendering
  - Owner:
  - Notes:
- [ ] Add summary and exit code behavior
  - Owner:
  - Notes:
- [ ] Add optional JSON export
  - Owner:
  - Notes:

Acceptance criteria:

- [ ] Local execution is readable without `dotnet test`
- [ ] Exit code is non-zero on any failure
- [ ] Output remains usable in plain terminals without third-party UI packages

## Phase 4: xUnit Adapter

- [ ] Implement theory adapter
  - Owner:
  - Notes:
- [ ] Implement fact-style adapter
  - Owner:
  - Notes:
- [ ] Verify discovered test names are meaningful
  - Owner:
  - Notes:
- [ ] Keep xUnit references isolated to this package
  - Owner:
  - Notes:

Acceptance criteria:

- [ ] Shared descriptors run under xUnit
- [ ] Theory-based and fact-style paths both work
- [ ] Discovery names are acceptable in IDE and CI output

## Phase 5: Sample App

- [ ] Build `Touchstone.SampleApp`
  - Owner:
  - Notes:
- [ ] Build `Touchstone.SampleApp.Tests.Shared`
  - Owner:
  - Notes:
- [ ] Build `Touchstone.SampleApp.Tests.Xunit`
  - Owner:
  - Notes:
- [ ] Build `Touchstone.SampleApp.Tests.Console`
  - Owner:
  - Notes:
- [ ] Add at least one skipped case
  - Owner:
  - Notes:
- [ ] Add at least one intentional local failure toggle
  - Owner:
  - Notes:

Acceptance criteria:

- [ ] `dotnet test` passes for the sample app
- [ ] Console runner shows pass, fail, and skip behavior correctly
- [ ] The sample is understandable without Watson context

## Phase 6: Hardening

- [ ] Add XML docs for public surface area
  - Owner:
  - Notes:
- [ ] Review naming for permanence before publish
  - Owner:
  - Notes:
- [ ] Add versioning policy
  - Owner:
  - Notes:
- [ ] Add changelog and release notes template
  - Owner:
  - Notes:
- [ ] Add CI workflow
  - Owner:
  - Notes:

Acceptance criteria:

- [ ] Package can ship as `0.x`
- [ ] Public API feels intentionally small
- [ ] No Watson-specific types remain in the packages

## Phase 7: Real-World Validation

- [ ] Migrate one descriptor-based Watson suite onto `Touchstone.*`
  - Owner:
  - Notes:
- [ ] Migrate one non-descriptor or fact-heavy Watson suite
  - Owner:
  - Notes:
- [ ] Record gaps, breaking changes, and missing hooks
  - Owner:
  - Notes:
- [ ] Revise API only after this validation pass
  - Owner:
  - Notes:

Acceptance criteria:

- [ ] Touchstone works in a real consumer repo
- [ ] No parity-fixture pattern is required
- [ ] Any remaining lifecycle hooks are optional, not central

## Phase 8: Publish

- [ ] Publish prerelease packages
  - Owner:
  - Notes:
- [ ] Consume from a second repo
  - Owner:
  - Notes:
- [ ] Decide private-only vs public `0.x`
  - Owner:
  - Notes:
- [ ] Publish `README.md` and quick-start examples
  - Owner:
  - Notes:

Acceptance criteria:

- [ ] At least two repos consume the package
- [ ] Package installation and first-test setup are documented end to end

## Guardrails

- [ ] Do not move Watson-specific helpers into `Touchstone.*`.
- [ ] Do not use a static global reporter callback.
- [ ] Do not make suite lifecycle mandatory.
- [ ] Do not tie result identity to display names alone.
- [ ] Do not add reflection discovery in v1.
- [ ] Do not add source generators in v1.
- [ ] Do not depend on `Spectre.Console` in v1.

## First Concrete Tasks

The following is the recommended starting sequence.

1. [ ] Create the solution and the three package projects.
2. [ ] Create `Touchstone.SampleApp` and its three test host projects.
3. [ ] Implement the core contracts and executor with unit tests.
4. [ ] Implement the console runner and verify pass/fail/skip output.
5. [ ] Implement the xUnit theory adapter.
6. [ ] Implement the xUnit fact-style adapter.
7. [ ] Finish the sample app shared tests.
8. [ ] Run the full sample app suite through both xUnit and the console runner.
9. [ ] Migrate one real Watson suite as the first external validation step.

## Progress Log

Use this section as work begins.

| Date | Developer | Area | Status | Notes |
| --- | --- | --- | --- | --- |
| 2026-04-02 |  | Initial planning | Complete | Consensus plan written |
