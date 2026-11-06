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

Use `WithTags` to tag a scenario for filtering and `WithSkip` to skip it with a reason; both flow to the built descriptor.

Reusable steps can be ordinary methods matching `Func<TContext, CancellationToken, Task>`, or `ScenarioStepDescriptor<TContext>` objects passed to `Step`. Assertions use specific exceptions or an assertion library chosen by the consumer. Core does not depend on any assertion framework.

## Context and cleanup

The factory transfers ownership of a fresh context to one execution. Do not capture and reuse a mutable context instance in the factory. Building a descriptor does not create a context. The same descriptor may execute concurrently, each execution with its own context, but delegates and externally shared clients remain the author's synchronization responsibility. Builders are intended for one authoring flow, and result sinks for one run.

`WithCleanup` runs after successful context creation, even when a step fails, an observer throws, or the caller cancels. It receives its own token, canceled after `CleanupTimeout` (default 30 seconds; greater than zero and at most one day). The context is then disposed once, preferring `IAsyncDisposable` over `IDisposable`, even when custom cleanup fails. A factory that throws before returning a context owns cleanup of any partially created resources.

Cancellation is cooperative. Neither the cleanup budget nor caller cancellation can stop a delegate that ignores its token, and `DisposeAsync` has no cancellation parameter.

The primary exception keeps its original stack trace. Cleanup exceptions are recorded separately in `TestResult.CleanupExceptions`. When cleanup alone fails, the case fails with the first cleanup exception. Descriptors are immutable, so a built scenario cannot be changed; `WithTags`, `WithSkip`, `WithoutSkip`, and `WithDisplayName` return copies that keep the scenario.

## Outcomes and execution APIs

A scenario counts as one case regardless of how many steps it contains. A failed step stops later steps, which become NotRun. A skipped scenario creates no context and marks its steps Skipped. When the caller cancels during a step, the scenario's outcome is Canceled, the active step is Canceled, and later steps are NotRun; cancellation before context creation leaves every step NotRun. An `OperationCanceledException` while the caller's token is not canceled is a failure. These are the same rules ordinary cases follow.

| Entry point | Behavior |
|---|---|
| `TestExecutor.RunAsync` / `RunSuiteAsync` | Run suites and report every result through the sink. Cancellation and failed suite hooks are reported in the summary, not thrown. |
| `TestExecutor.ExecuteCaseResultAsync` | Return a structured result without throwing for case failures. The optional sink receives case and step events. No suite hooks run. |
| `TestExecutor.ExecuteCaseAsync` | Throw the original exception for a failed case and `OperationCanceledException` for a canceled one. No suite hooks run. |
| `ScenarioDiagnostics.ExecuteAsync` | Send step diagnostics to a host callback, then throw like `ExecuteCaseAsync`. |
| Adapter execute helpers | Run one case and report it through the framework's native outcome, writing step diagnostics to the framework's output. |

Step events arrive through `ITestResultSink.OnStepStartedAsync` and `OnStepCompletedAsync`. Start callbacks receive a NotRun snapshot; completion callbacks receive the final step and a token that is not canceled. Skipped and unreached steps appear in final results without live events. An observer that throws stops the run after the scenario's context is cleaned up.

## Typed examples

`ScenarioExamples.Expand` accepts named typed rows and a factory. It returns an eager read-only list, rejects duplicate example identities and duplicate resulting test identities, and never records input values. Choose deterministic case identities from example identities; do not generate a GUID during discovery.

```csharp
namespace MyTests
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;

    using Touchstone.Core;

    /// <summary>Scenarios expanded from typed example rows.</summary>
    public static class ValueScenarios
    {
        /// <summary>One case per example row.</summary>
        /// <returns>The cases.</returns>
        public static IReadOnlyList<TestCaseDescriptor> Cases()
        {
            ScenarioExample<int>[] rows =
            {
                new ScenarioExample<int>("zero", 0),
                new ScenarioExample<int>("one", 1),
            };

            return ScenarioExamples.Expand(rows, row =>
                new ScenarioBuilder<int>("Values", row.ExampleId, "A value is preserved", ct => Task.FromResult(row.Value))
                    .Then("equal", "the value matches the example", (value, ct) =>
                    {
                        if (value != row.Value) throw new InvalidOperationException("Example value changed.");
                        return Task.CompletedTask;
                    })
                    .WithMetadata(new ScenarioMetadata("Values", exampleId: row.ExampleId))
                    .Build());
        }
    }
}
```

Input values should be immutable, or copied by the factory, if executions may overlap. Display names and requirement references are explicit metadata, separate from stable identities.

## Console, JSON, and offline reports

Pass `--expand` to the console runner (or set `ConsoleRunnerOptions.ExpandSteps`) to see steps live:

```sh
dotnet run --project src/Test.Automated -f net10.0 -- --expand --results /tmp/results.json --html /tmp/results.html
dotnet run --project test/Touchstone.SampleApp.Tests.Console -- --expand --html /tmp/notes.html
```

Pass `--fail` to the sample console to add intentional failures. A failed scenario makes the console exit with code 1; a canceled run exits with code 2.

JSON results follow `schemas/touchstone-results-1.0.schema.json`. Each result carries `outcome`, `tags`, `feature`, `exampleId`, `requirements`, `cleanupErrors`, and `steps`; each step carries `stepId`, `displayName`, `kind` (`given`, `when`, `then`), `outcome`, `durationMs` (fractional milliseconds), and `message`. Outcomes are lowercase strings. Exception objects are never serialized.

`HtmlReport.WriteAsync(summary, results, path, options, cancellationToken)` writes an offline report grouped by feature, with the run start time, counts, tags, requirement references, examples, steps, timings, and errors. Text is encoded, and no scripts or remote resources are included. It works with results from `TestResultCollector`, `ConsoleResultSink.AllResults`, or `ExecuteCaseResultAsync`.

Input values are never captured automatically. If you put values into step names or messages, they become report text. Set `ConsoleRunnerOptions.Redact` or `ReportOptions.Redact` to sanitize every consumer string before it is written; in-memory results stay raw. Native output callbacks should redact `ScenarioDiagnostics.Format` output themselves. Telemetry can contain exception messages; consult TELEMETRY.md before exporting it.

## Native framework output

Every adapter writes step diagnostics to its framework's output after each case: `TestContext.Current.TestOutputHelper` for xUnit v3, the `ITestOutputHelper` you pass for xUnit v2, `TestContext.Out` (or a writer you pass) for NUnit, and the `TestContext` you pass for MSTest. Native runners do not show steps as separate tests. Each case or example is one native test, and run-all base classes include step diagnostics in their failure message.

The Notes sample contexts own a separate in-process application per execution, so the scenarios work without suite hooks in one-test-per-case hosts.

## Migration and limits

When converting a delegate case to a scenario, keep its suite and case identities and tags. Move setup into the context factory, split observable operations into named steps, put assertions in Then steps, and let the context own resource disposal. Keep suite-level resources in suite hooks only when every host you use runs them (the console runner and run-all base classes do; one-test-per-case hosts do not).

Feature-file parsing, binding discovery, dependency injection, retries, parallel steps, nested scenarios, and parameter capture are out of scope. A scenario helps when ordered steps explain the outcome; a single delegate remains right for a short assertion.
