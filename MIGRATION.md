# Migrating from 0.3.0-alpha to 1.0.0

Touchstone 1.0.0 is the first stable release. It makes every breaking change in one step so that 1.x can stay compatible. This guide lists each change with before and after code. Most projects need a few mechanical edits; the compiler points at each one.

## Packages

| 0.3.0-alpha | 1.0.0 |
|---|---|
| `Touchstone.XunitAdapter` (xUnit v2) | `Touchstone.Xunit2Adapter`. The old package is deprecated. |
| (none) | `Touchstone.Xunit3Adapter` for xUnit v3. |
| `Touchstone` metapackage with every adapter | `Touchstone` contains Core and Cli only. Add the adapter for your framework. |
| NUnit 5.0, MSTest.TestFramework 4.4 | Unchanged minimums. xUnit v2 minimum 2.9.3; xUnit v3 minimum 4.0.2. |

Namespaces follow the package names: `Touchstone.XunitAdapter` becomes `Touchstone.Xunit2Adapter`.

## Descriptors

Descriptors are immutable, identities follow a grammar, and skipping takes a reason only.

```csharp
// 0.3.0-alpha
new TestCaseDescriptor("Cart", "Discounts", "Discounts apply", body, skip: true, skipReason: "Not implemented");
descriptor.Skip = true;
descriptor.Tags = new[] { "slow" };

// 1.0.0
new TestCaseDescriptor("Cart", "Discounts", "Discounts apply", body, skipReason: "Not implemented");
TestCaseDescriptor skipped = descriptor.WithSkip("Not implemented");
TestCaseDescriptor tagged = descriptor.WithTags(new[] { "slow" });
```

- `skip` is gone; a non-null `skipReason` means skipped, and `IsSkipped` reports it. A blank reason is rejected.
- Setters are gone from `TestCaseDescriptor` and `TestSuiteDescriptor`. Use `WithTags`, `WithSkip`, `WithoutSkip`, and `WithDisplayName`.
- `TestCaseDescriptor.ExecuteAsync` is no longer public. Run cases through `TestExecutor` or an adapter, so skips, cancellation, and telemetry apply.
- Suite identities use letters, digits, `_`, and `-` (no `.`). Case identities also allow `.`. Display names cannot be blank.
- Duplicate case identities in a suite throw `DuplicateTestIdException`. Duplicate suite identities in a run do too, before anything executes.

## Results and summaries

```csharp
// 0.3.0-alpha
if (result.Success && !result.Skipped) { }
if (result.Canceled) { }

// 1.0.0
if (result.Outcome == TestOutcome.Passed) { }
if (result.Outcome == TestOutcome.Canceled) { }
```

- `TestResult.Success`, `Skipped`, and `Canceled` are replaced by `Outcome` (`Passed`, `Failed`, `Skipped`, `Canceled`, `NotRun`).
- `TestResult` and `TestRunSummary` are immutable, with validating constructors for tests of custom sinks.
- `TestRunSummary` adds `Canceled`, `NotRun`, `Filtered`, `WasCanceled`, and `LifecycleErrors`. `Total` excludes filtered cases. `IsSuccess` is false when anything failed, was canceled, did not run, or a suite hook failed.

## Execution

```csharp
// 0.3.0-alpha
await TestExecutor.RunAsync(suites, sink, cancellationToken);
TestResult result = await TestExecutor.ExecuteCaseResultAsync(testCase, cancellationToken, stepSink);

// 1.0.0
TestRunOptions options = new TestRunOptions();
options.Filter.IncludeTags.Add("fast");
await TestExecutor.RunAsync(suites, sink, options, cancellationToken);
TestResult result = await TestExecutor.ExecuteCaseResultAsync(testCase, sink, options, cancellationToken);
```

- Every entry point takes `TestRunOptions? options` before the `CancellationToken`, which is always the last parameter.
- `TestRunOptions` holds `Filter`, `SkipBehavior`, `StopOnFirstFailure`, and `CleanupTimeout`.
- A failing `beforeSuiteAsync` no longer aborts the run. The suite's cases fail with the hook exception, `afterSuiteAsync` still runs, the failure is recorded in `LifecycleErrors`, and the next suite runs.
- Caller cancellation no longer throws from `RunAsync`. The run stops, teardown runs, remaining cases are `NotRun`, and `WasCanceled` is set. Throwing entry points still throw `OperationCanceledException`.
- An `OperationCanceledException` counts as Canceled whenever the caller's token is canceled, for ordinary cases and scenarios alike. In 0.3.0-alpha an ordinary case's cancellation was a failure, and a scenario required a matching token.

## Sinks

`IScenarioStepSink` is merged into `ITestResultSink`, and every member has a default implementation.

```csharp
// 0.3.0-alpha
public ValueTask OnTestCompletedAsync(TestResult result, CancellationToken cancellationToken) { ... }

// 1.0.0
public ValueTask OnCaseCompletedAsync(TestResult result, CancellationToken cancellationToken) { ... }
```

- Rename `OnTestCompletedAsync` to `OnCaseCompletedAsync`, and delete callbacks you do not need.
- New callbacks: `OnRunStartedAsync`, `OnCaseStartedAsync`, `OnRunCompletedAsync`, and the step callbacks formerly on `IScenarioStepSink`.
- Completion callbacks receive a token that is not canceled.
- `TestResultCollector.Failures` now includes canceled cases, and `Summary` holds the run summary.

## Console runner

```csharp
// 0.3.0-alpha
ConsoleResultSink sink = new ConsoleResultSink(writer, false) { ExpandSteps = true };
int code = await ConsoleRunner.RunAsync(suites, sink, "results.json", cancellationToken);
await ScenarioHtmlReport.WriteAsync(sink.AllResults, "report.html", "run", cancellationToken, sink.Redact);

// 1.0.0, from Main
return await ConsoleRunner.RunMainAsync(suites, args);

// 1.0.0, configured in code
ConsoleRunnerOptions options = new ConsoleRunnerOptions
{
    Writer = writer,
    UseColor = false,
    ExpandSteps = true,
    JsonResultsPath = "results.json",
    HtmlReportPath = "report.html",
};
int code = await ConsoleRunner.RunAsync(suites, options, cancellationToken);
```

- Exit codes: 0 success, 1 failures or suite hook errors, 2 canceled, 3 invalid arguments. Scripts that only test for zero are unaffected.
- `ScenarioHtmlReport` is replaced by `HtmlReport`, alongside a new `JsonReport`. Both take `(summary, results, path, ReportOptions?, cancellationToken)`.
- `ConsoleResultSink` takes `ConsoleRunnerOptions`; `WriteHeader` and `WriteOverall` are gone (the sink writes them from run events).
- `JsonResultEntry` and `JsonStepEntry` are no longer public. The JSON format is documented by its schema.

## JSON results

The JSON file changes from a bare array to a versioned document:

```json
{
  "schemaVersion": "1.0",
  "tool": { "name": "Touchstone", "version": "1.0.0" },
  "run": { "startedUtc": "...", "durationMs": 12.5, "canceled": false, "summary": { "total": 3, "passed": 2, "failed": 0, "skipped": 1, "canceled": 0, "notRun": 0, "filtered": 0 }, "lifecycleErrors": [] },
  "results": [ { "testId": "Cart.StartsEmpty", "outcome": "passed", "durationMs": 1.2, "tags": [], "steps": [] } ]
}
```

- Read results from `results` instead of the root.
- `success` and `skipped` are replaced by `outcome` (`passed`, `failed`, `skipped`, `canceled`, `not_run`).
- Result `durationMs` is now fractional, like step `durationMs`.
- Step `kind` and `outcome` are lowercase.
- The schema ships in the Touchstone.Cli package as `schemas/touchstone-results-1.0.schema.json`.

## Framework adapters

Each adapter now has the same three parts: a data source (`Touchstone{Framework}Cases`), an execute helper (`Touchstone{Framework}`), and a run-all base class (`Touchstone{Framework}FactBase`). Rows are native tests with display names, tag traits or categories, and native skips. See the README for complete examples.

```csharp
// 0.3.0-alpha, xUnit
public static TheoryData<TestCaseDescriptor> Cases() { return new TouchstoneTheoryData(MySuites.All); }
[Theory, MemberData(nameof(Cases))]
public Task Run(TestCaseDescriptor testCase) { return TestExecutor.ExecuteCaseAsync(testCase, CancellationToken.None); }

// 1.0.0, xUnit v3 (MySuites implements ITestSuiteProvider)
public static TheoryData<TouchstoneXunit3Case> Cases() { return TouchstoneXunit3Cases.From<MySuites>(); }
[Theory, MemberData(nameof(Cases))]
public Task Run(TouchstoneXunit3Case testCase) { return TouchstoneXunit3.ExecuteAsync(testCase, TestContext.Current.CancellationToken); }
```

| 0.3.0-alpha | 1.0.0 |
|---|---|
| `TouchstoneTheoryData`, `TouchstoneTestBase` (xUnit) | `TouchstoneXunit2Cases.From<TProvider>()` or `TouchstoneXunit3Cases.From<TProvider>()` |
| `TouchstoneFactBase` (xUnit) | `TouchstoneXunit2FactBase` or `TouchstoneXunit3FactBase` |
| `TouchstoneTestCaseSource` (NUnit) | `TouchstoneNunitCases.From(suites, options)` and `TouchstoneNunit.ExecuteAsync` |
| `TouchstoneNunitBase` | `TouchstoneNunitFactBase` |
| `TouchstoneDynamicData.FromSuites` (MSTest) | `TouchstoneMstestCases.From<TProvider>()` and `TouchstoneMstest.ExecuteAsync` |
| `TouchstoneMstestBase` | `TouchstoneMstestFactBase` |

The xUnit and MSTest data sources take a suite provider type (`ITestSuiteProvider` with a parameterless constructor) because those frameworks rebuild rows in the process that runs them. Implement it on the class that already exposes your suites:

```csharp
public sealed class MySuites : ITestSuiteProvider
{
    public static IReadOnlyList<TestSuiteDescriptor> All { get { return new[] { /* suites */ }; } }

    public IReadOnlyList<TestSuiteDescriptor> Suites { get { return All; } }
}
```

0.3.0-alpha's NUnit and MSTest data sources dropped skipped cases, and xUnit reported them as passed. In 1.0 every framework reports them as native skips by default (xUnit v2, which cannot skip at run time, passes and writes the reason). Use `SkipBehavior.Omit` to keep the old NUnit and MSTest behavior.

## Telemetry

Span names no longer contain identities, several attributes were renamed, and scenario labels are lowercase. TELEMETRY.md has the full old-to-new table under "Changes in 1.0".
