# Behavior Driven Development

Agree on examples of observable behavior before implementing steps. A Notes API example might read: Given a note exists, When it is fetched, Then its identity and content match. Names should explain the behavior to someone investigating a failure. Existing delegate tests remain useful for short checks.

## Authoring

Scenarios use only `Touchstone.Core`. The builder returns a regular `TestCaseDescriptor`, which can be placed in any existing suite. No runner attributes, parser, assertion package, or additional dependency is needed. The following complete definition uses named classes, explicit types, and cancellation-aware delegates.

```csharp
namespace MyTests
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;

    using Touchstone.Core;

    /// <summary>Shared definitions for counter behavior.</summary>
    public static class CounterScenarios
    {
        /// <summary>Build a reusable definition. Context is created during execution.</summary>
        public static TestCaseDescriptor Increment()
        {
            return new ScenarioBuilder<CounterContext>(
                "Counter", "Increment", "Incrementing a fresh counter produces one",
                ct =>
                {
                    ct.ThrowIfCancellationRequested();
                    return Task.FromResult(new CounterContext());
                })
                .Given("empty", "a fresh counter", (context, ct) =>
                {
                    ct.ThrowIfCancellationRequested();
                    if (context.Value != 0) throw new InvalidOperationException("Expected an empty counter.");
                    return Task.CompletedTask;
                })
                .When("increment", "the counter is incremented", (context, ct) =>
                {
                    ct.ThrowIfCancellationRequested();
                    context.Value++;
                    return Task.CompletedTask;
                })
                .Then("one", "the counter is one", (context, ct) =>
                {
                    ct.ThrowIfCancellationRequested();
                    if (context.Value != 1) throw new InvalidOperationException("Expected counter value one.");
                    return Task.CompletedTask;
                })
                .WithMetadata(new ScenarioMetadata("Counter behavior", new[] { "COUNTER-1" }))
                .Build();
        }
    }
}
```

Put the context in its own `CounterContext.cs` file:

```csharp
namespace MyTests
{
    /// <summary>Mutable state owned by one execution.</summary>
    public sealed class CounterContext
    {
        /// <summary>Current count.</summary>
        public int Value { get; set; }
    }
}
```

Given establishes preconditions, When performs an action, and Then checks an outcome. And inherits the preceding kind and cannot be the first step. It is displayed using that inherited kind. Steps run sequentially; the builder does not enforce a particular Given/When/Then grammar. Step IDs must be unique within a scenario. Adding steps to a builder after Build does not change previously built descriptors.

Reusable steps can be ordinary methods matching `Func<TContext, CancellationToken, Task>`, or `ScenarioStepDescriptor<TContext>` objects passed to `Step`. Assertions use specific exceptions or an assertion library chosen by the consumer. Core does not depend on any assertion framework.

## Context and cleanup

The factory transfers ownership of a fresh context to one execution. Do not capture and reuse a mutable context instance in the factory. Discovery and Build do not create contexts. The same descriptor may execute concurrently, but delegates and externally shared clients remain the consumer's synchronization responsibility. Builders and result sinks are intended for one authoring flow or run, respectively.

`WithCleanup` runs after successful context creation even when a step fails, an observer throws, or the caller cancels. It receives a fresh token with a default 30-second cancellation budget, configurable through `CleanupTimeout` (greater than zero and at most one day). The context is then disposed once, preferring `IAsyncDisposable` over `IDisposable`, even when custom cleanup fails. A factory that throws before returning a context owns cleanup of any partially created resources.

Cancellation is cooperative. Neither the cleanup budget nor caller cancellation forcibly terminates a delegate that ignores its token. `DisposeAsync` has no cancellation parameter and may also take longer than the budget.

The primary exception and original stack remain intact. Cleanup exceptions are recorded separately in `TestResult.CleanupExceptions`. If cleanup alone fails, the case fails using the first cleanup exception. A built scenario's `ExecuteAsync` cannot be replaced; doing so throws `InvalidOperationException` to prevent conflicting executable definitions. Ordinary descriptors remain mutable.

## Outcomes and execution APIs

A scenario counts as one case regardless of how many steps it contains. A failed step stops subsequent steps, which become NotRun. Skipping the descriptor prevents context creation and marks its planned steps Skipped. Matching caller cancellation sets `TestResult.Canceled`, marks an active step Canceled, and leaves later steps NotRun. Cancellation before context creation leaves all steps NotRun. An unrelated `OperationCanceledException` is a failure.

| Entry point | Behavior |
|---|---|
| `TestExecutor.RunAsync` / `RunSuiteAsync` | Collect case results through the sink. Scenario cancellation counts as a failed case so an incomplete run cannot succeed. Existing suite iteration behavior is preserved. |
| `TestExecutor.ExecuteCaseResultAsync` | Return a structured result without throwing assertion failures. Optional step sink receives live events. No suite hooks run. |
| `TestExecutor.ExecuteCaseAsync` | Rethrow the original case exception and stack, including caller cancellation. No suite hooks run. |
| Scenario descriptor `ExecuteAsync` | Use the same throwing path, including telemetry and context ownership. |
| `ScenarioDiagnostics.ExecuteAsync` | Send completed step diagnostics to a host callback, then rethrow the original exception. |

Ordinary delegate cases retain their existing cancellation behavior. An `IScenarioStepSink` can observe steps without changing `ITestResultSink`. Start callbacks receive a NotRun snapshot; completion callbacks receive final executed-step outcomes. Skipped and unreached steps appear in final results, without live start/completion events. Observer errors propagate after owned-context cleanup, consistent with existing sink failures; they are not collected as assertion results. Completion observation receives a non-canceled token.

## Typed examples

`ScenarioExamples.Expand` accepts named typed rows and a descriptor factory. It returns an eager read-only list, rejects duplicate example IDs and duplicate resulting test IDs, and never automatically records parameter values. Choose deterministic case IDs from example IDs; do not generate a GUID during discovery.

```csharp
ScenarioExample<int>[] rows =
{
    new ScenarioExample<int>("zero", 0),
    new ScenarioExample<int>("one", 1)
};
IReadOnlyList<TestCaseDescriptor> cases = ScenarioExamples.Expand(rows, row =>
    new ScenarioBuilder<int>("Values", row.ExampleId, "A value is preserved",
        ct => Task.FromResult(row.Value))
        .Then("equal", "the value matches the example", (value, ct) =>
        {
            ct.ThrowIfCancellationRequested();
            if (value != row.Value) throw new InvalidOperationException("Example value changed.");
            return Task.CompletedTask;
        })
        .WithMetadata(new ScenarioMetadata("Values", exampleId: row.ExampleId))
        .Build());
```

Input values should be immutable or copied by the factory if multiple executions may overlap. Display names and requirement references are explicit metadata, separate from stable IDs.

## Console, JSON, and offline reports

Compact console output is unchanged. Set `ConsoleResultSink.ExpandSteps = true` to see live step progress. The shared and sample console programs accept these flags:

```sh
dotnet run --project src/Test.Automated -f net10.0 -- --steps --results /tmp/results.json --html /tmp/results.html
dotnet run --project tests/Touchstone.SampleApp.Tests.Console -- --steps --html /tmp/notes.html
```

Use `--fail` with the sample console to include intentional ordinary and scenario failures. A failed or canceled scenario produces console exit code 1. Export or observer errors propagate and the supplied console applications terminate unsuccessfully.

JSON retains the root array and legacy fields. Added fields are `steps`, `canceled`, `feature`, `requirements`, `exampleId`, and `cleanupErrors`. Step entries include identity, name, kind, outcome, `durationMs` (fractional milliseconds), and optional message. Outcomes and kinds are strings. Ordinary cases have empty step/error arrays. `success` remains true for skipped cases. Exception objects are not serialized.

`ScenarioHtmlReport.WriteAsync(results, path, runId, cancellationToken, redact)` generates an offline report from collected results. It groups features and scenarios, identifies the run and collection time, and shows counts, requirement references, examples, steps, timings, and errors. Text is escaped, references are rendered as text, and no scripts or remote assets are included. It works with `TestResultCollector.Results` from any host or with individually collected results from `ExecuteCaseResultAsync`.

Parameter capture is absent by default. If a consumer deliberately includes values in step names or messages, those values become report text. Set `ConsoleResultSink.Redact` before execution to sanitize console and JSON output; pass the same function to HTML reporting. Redaction applies before writing to a report, not to in-memory raw results. Native output callbacks should apply their own redactor to `ScenarioDiagnostics.Format` output. Existing case telemetry can contain exception messages; consult `TELEMETRY.md` before exporting consumer exceptions.

## Native framework output

Use the same shared descriptors for every framework. For row-based hosts, call `ScenarioDiagnostics.ExecuteAsync` with the output callback appropriate to your framework:

| Framework | Callback |
|---|---|
| xUnit 2 | `text => output.WriteLine(text)` using injected `ITestOutputHelper` |
| NUnit | `text => TestContext.Progress.WriteLine(text)` |
| MSTest | `text => TestContext.WriteLine(text)` using the host's TestContext property |

Output is sent after each case completes; native runners do not gain individually discoverable steps or guaranteed live nested output. Row-based discovery creates one native test per case/example. Full-suite wrappers retain their aggregation behavior and include step diagnostics in failure messages. Native labels for cancellation may be failure or cancellation depending on the host. Descriptor skip handling follows the existing adapter/discovery pattern; the diagnostic callback does not introduce a new native skip mechanism.

The repository's shared suite includes real scenario descriptors plus contract checks; both wrapper and row hosts execute them. Notes sample contexts own a separate in-process application per execution, so scenarios work without suite lifecycle hooks in row-based hosts. The Notes API currently supports create, fetch, and delete; its sample scenarios cover persistence through create/fetch, missing notes, and validation.

## Migration and limits

Keep suite/case IDs and tags when migrating an existing delegate. Move setup into the context factory, split observable operations into named steps, put assertions in Then steps, and transfer resource disposal to context ownership. Keep suite-level resources in existing hooks only when every selected host actually runs those hooks.

Feature-file parsing, binding discovery, DI containers, retries, parallel steps, nested scenarios, and parameter capture are outside this release. A behavior scenario is useful when its ordered steps help explain the outcome; a single ordinary delegate remains appropriate for a short assertion.
