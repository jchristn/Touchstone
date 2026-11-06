namespace Test.Shared
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;

    using Touchstone.Core;

    /// <summary>
    /// Catalog section 11.13: behavior scenarios.
    /// </summary>
    public static class ScenarioSuites
    {
        /// <summary>
        /// Every suite in this file.
        /// </summary>
        /// <returns>The suites.</returns>
        public static IEnumerable<TestSuiteDescriptor> All()
        {
            yield return ScenarioSuite();
        }

        private static ScenarioBuilder<int> Builder(string caseId = "case")
        {
            return new ScenarioBuilder<int>("BDD", caseId, "behavior", _ => Task.FromResult(0));
        }

        private static TestSuiteDescriptor ScenarioSuite()
        {
            const string S = "Scenario";
            return new TestSuiteDescriptor(S, "Behavior scenarios", new[]
            {
                Check.Sync(S, "SCN-01", "Invalid scenario definitions fail early", () =>
                {
                    TestAssert.Throws<ArgumentException>(() => new ScenarioBuilder<int>("", "c", "n", _ => Task.FromResult(0)), "blank suite");
                    TestAssert.Throws<ArgumentException>(() => new ScenarioBuilder<int>("s", "c", " ", _ => Task.FromResult(0)), "blank name");
                    TestAssert.Throws<ArgumentNullException>(() => new ScenarioBuilder<int>("s", "c", "n", null!), "null factory");
                    ScenarioBuilder<int> builder = Builder();
                    TestAssert.Throws<InvalidOperationException>(() => builder.Build(), "no steps");
                    TestAssert.Throws<InvalidOperationException>(() => builder.And("a", "and", (_, _) => Task.CompletedTask), "And first");
                    TestAssert.Throws<ArgumentOutOfRangeException>(() => builder.CleanupTimeout = TimeSpan.Zero, "cleanup budget");
                    TestAssert.Throws<ArgumentException>(() => new ScenarioStepDescriptor<int>("s", "", ScenarioStepKind.Given, (_, _) => Task.CompletedTask), "blank step name");
                    TestAssert.Throws<ArgumentException>(() => new ScenarioStepDescriptor<int>("s", "n", (ScenarioStepKind)7, (_, _) => Task.CompletedTask), "unknown kind");
                    TestAssert.Throws<ArgumentException>(() => new ScenarioMetadata(exampleId: " "), "blank example");
                    TestAssert.Throws<ArgumentNullException>(() => builder.WithMetadata(null!), "null metadata");
                    TestAssert.Throws<ArgumentNullException>(() => builder.WithCleanup(null!), "null cleanup");
                }),
                Check.Async(S, "SCN-02", "Steps share context and emit ordered live events", async ct =>
                {
                    RecordingSink sink = new RecordingSink();
                    TestCaseDescriptor descriptor = Builder().Given("given", "a value", (_, _) => Task.CompletedTask)
                        .When("when", "an action", (_, _) => Task.CompletedTask)
                        .Then("then", "a result", (_, _) => Task.CompletedTask)
                        .And("and", "another result", (_, _) => Task.CompletedTask).Build();
                    TestResult result = await TestExecutor.ExecuteCaseResultAsync(descriptor, sink, null, ct);
                    TestAssert.Equal(TestOutcome.Passed, result.Outcome, "outcome");
                    TestAssert.Equal(4, result.Steps.Count, "steps");
                    string steps = string.Join(",", sink.Events.Where(e => e.StartsWith("step", StringComparison.Ordinal)));
                    TestAssert.Equal("step_started:BDD.case/given,step_completed:BDD.case/given=Passed,step_started:BDD.case/when,step_completed:BDD.case/when=Passed,"
                        + "step_started:BDD.case/then,step_completed:BDD.case/then=Passed,step_started:BDD.case/and,step_completed:BDD.case/and=Passed", steps, "order");
                }),
                Check.Async(S, "SCN-03", "A failing step keeps its exception, stops later steps, and rethrows", async ct =>
                {
                    InvalidOperationException error = new InvalidOperationException("primary");
                    int later = 0;
                    TestCaseDescriptor descriptor = Builder().Given("a", "ready", (_, _) => Task.CompletedTask)
                        .When("b", "fails", (_, _) => throw error)
                        .Then("c", "not run", (_, _) => { later++; return Task.CompletedTask; }).Build();
                    TestResult result = await TestExecutor.ExecuteCaseResultAsync(descriptor, null, null, ct);
                    TestAssert.Same(error, result.Exception, "original");
                    TestAssert.Equal("Passed,Failed,NotRun", string.Join(",", result.Steps.Select(s => s.Outcome)), "steps");
                    TestAssert.Equal(0, later, "later prevented");
                    InvalidOperationException rethrown = await TestAssert.ThrowsAsync<InvalidOperationException>(() => TestExecutor.ExecuteCaseAsync(descriptor, null, ct), "throwing entry");
                    TestAssert.Same(error, rethrown, "same instance");
                }),
                Check.Async(S, "SCN-04", "Building freezes the steps", async ct =>
                {
                    ScenarioBuilder<int> builder = Builder().Given("a", "first", (_, _) => Task.CompletedTask);
                    TestCaseDescriptor descriptor = builder.Build();
                    builder.Then("b", "later", (_, _) => Task.CompletedTask).WithTags(new[] { "late" }).WithSkip("late");
                    TestResult result = await TestExecutor.ExecuteCaseResultAsync(descriptor, null, null, ct);
                    TestAssert.Equal(1, result.Steps.Count, "snapshot");
                    TestAssert.Equal(0, descriptor.Tags.Count, "tags snapshot");
                    TestAssert.False(descriptor.IsSkipped, "skip snapshot");
                }),
                Check.Async(S, "SCN-05", "Skipped scenarios neither create nor dispose context", async ct =>
                {
                    int created = 0;
                    TestCaseDescriptor descriptor = new ScenarioBuilder<int>("BDD", "skip", "skip", _ => { created++; return Task.FromResult(0); })
                        .Given("a", "not entered", (_, _) => Task.CompletedTask).WithSkip("not ready").Build();
                    TestResult result = await TestExecutor.ExecuteCaseResultAsync(descriptor, null, null, ct);
                    TestAssert.Equal(TestOutcome.Skipped, result.Outcome, "outcome");
                    TestAssert.Equal(0, created, "context");
                    TestAssert.Equal(ScenarioStepOutcome.Skipped, result.Steps[0].Outcome, "step");
                    TestAssert.Equal(TimeSpan.Zero, result.Duration, "duration");
                    TestAssert.Equal("not ready", result.Message, "reason");
                }),
                Check.Async(S, "SCN-06", "Context factory failure leaves every step unexecuted", async ct =>
                {
                    TestCaseDescriptor descriptor = new ScenarioBuilder<int>("BDD", "factory", "factory", _ => throw new FormatException("factory"))
                        .Given("a", "not entered", (_, _) => Task.CompletedTask).Build();
                    TestResult result = await TestExecutor.ExecuteCaseResultAsync(descriptor, null, null, ct);
                    TestAssert.True(result.Exception is FormatException, "exception");
                    TestAssert.Contains("Context creation", result.Message, "stage");
                    TestAssert.Equal(ScenarioStepOutcome.NotRun, result.Steps[0].Outcome, "step");
                }),
                Check.Async(S, "SCN-07", "A null context is a contextual failure", async ct =>
                {
                    TestCaseDescriptor descriptor = new ScenarioBuilder<object>("BDD", "null", "null", _ => Task.FromResult<object>(null!))
                        .Given("a", "not entered", (_, _) => Task.CompletedTask).Build();
                    TestResult result = await TestExecutor.ExecuteCaseResultAsync(descriptor, null, null, ct);
                    TestAssert.True(result.Exception is InvalidOperationException, "exception");
                    TestAssert.Contains("BDD.null", result.Message, "names case");
                }),
                Check.Async(S, "SCN-08", "Caller cancellation is Canceled and cleanup runs with a live token", async ct =>
                {
                    using (CancellationTokenSource source = new CancellationTokenSource())
                    {
                        ScenarioContext context = new ScenarioContext();
                        bool cleanup = false;
                        TestCaseDescriptor descriptor = new ScenarioBuilder<ScenarioContext>("BDD", "cancel", "cancel", _ => Task.FromResult(context))
                            .When("a", "cancel", (_, token) => { source.Cancel(); token.ThrowIfCancellationRequested(); return Task.CompletedTask; })
                            .Then("b", "not run", (_, _) => Task.CompletedTask)
                            .WithCleanup((_, token) => { cleanup = !token.IsCancellationRequested; return Task.CompletedTask; }).Build();
                        TestResult result = await TestExecutor.ExecuteCaseResultAsync(descriptor, null, null, source.Token);
                        TestAssert.Equal(TestOutcome.Canceled, result.Outcome, "outcome");
                        TestAssert.Equal("Canceled,NotRun", string.Join(",", result.Steps.Select(s => s.Outcome)), "steps");
                        TestAssert.True(cleanup, "cleanup token live");
                        TestAssert.Equal(1, context.AsyncDisposals, "disposed once");
                    }
                }),
                Check.Async(S, "SCN-09", "Pre-cancellation does not create context", async ct =>
                {
                    using (CancellationTokenSource source = new CancellationTokenSource())
                    {
                        source.Cancel();
                        int created = 0;
                        TestCaseDescriptor descriptor = new ScenarioBuilder<int>("BDD", "before", "before", _ => { created++; return Task.FromResult(0); })
                            .Given("a", "not run", (_, _) => Task.CompletedTask).Build();
                        TestResult result = await TestExecutor.ExecuteCaseResultAsync(descriptor, null, null, source.Token);
                        TestAssert.Equal(TestOutcome.Canceled, result.Outcome, "outcome");
                        TestAssert.Equal(0, created, "context");
                    }
                }),
                Check.Async(S, "SCN-10", "Unrelated cancellation is a failure", async ct =>
                {
                    TestResult result = await TestExecutor.ExecuteCaseResultAsync(Builder().When("a", "fails", (_, _) => throw new OperationCanceledException()).Build(), null, null, ct);
                    TestAssert.Equal(TestOutcome.Failed, result.Outcome, "outcome");
                }),
                Check.Async(S, "SCN-11", "The primary failure and both cleanup errors survive", async ct =>
                {
                    ScenarioContext context = new ScenarioContext { FailDisposal = true };
                    InvalidOperationException primary = new InvalidOperationException("primary");
                    TestCaseDescriptor descriptor = new ScenarioBuilder<ScenarioContext>("BDD", "cleanup", "cleanup", _ => Task.FromResult(context))
                        .When("a", "fails", (_, _) => throw primary)
                        .WithCleanup((_, _) => throw new FormatException("cleanup failure")).Build();
                    TestResult result = await TestExecutor.ExecuteCaseResultAsync(descriptor, null, null, ct);
                    TestAssert.Same(primary, result.Exception, "primary");
                    TestAssert.Equal(2, result.CleanupExceptions.Count, "cleanup errors");
                }),
                Check.Async(S, "SCN-12", "A cleanup failure fails an otherwise passing case", async ct =>
                {
                    TestResult result = await TestExecutor.ExecuteCaseResultAsync(Builder().Given("a", "pass", (_, _) => Task.CompletedTask)
                        .WithCleanup((_, _) => throw new InvalidOperationException("cleanup only")).Build(), null, null, ct);
                    TestAssert.Equal(TestOutcome.Failed, result.Outcome, "outcome");
                    TestAssert.Contains("Context cleanup", result.Message, "message");
                }),
                Check.Async(S, "SCN-13", "Observer failures propagate after the context is disposed", async ct =>
                {
                    foreach (string failOn in new[] { "step_started", "step_completed" })
                    {
                        ScenarioContext context = new ScenarioContext();
                        TestCaseDescriptor descriptor = new ScenarioBuilder<ScenarioContext>("BDD", "observer", "observer", _ => Task.FromResult(context))
                            .Given("a", "observed", (_, _) => Task.CompletedTask).Build();
                        await TestAssert.ThrowsAsync<InvalidOperationException>(() => TestExecutor.ExecuteCaseResultAsync(descriptor, new RecordingSink(failOn), null, ct), failOn);
                        TestAssert.Equal(1, context.AsyncDisposals, "disposed before propagation (" + failOn + ")");
                    }
                }),
                Check.Async(S, "SCN-14", "Concurrent and repeated executions own independent contexts", async ct =>
                {
                    int created = 0;
                    TestCaseDescriptor descriptor = new ScenarioBuilder<ScenarioContext>("BDD", "isolation", "isolation", _ =>
                        { Interlocked.Increment(ref created); return Task.FromResult(new ScenarioContext()); })
                        .When("a", "increment", async (context, token) => { await Task.Yield(); context.Value++; })
                        .Then("b", "one", (context, _) => { TestAssert.Equal(1, context.Value, "isolated"); return Task.CompletedTask; }).Build();
                    TestResult[] results = await Task.WhenAll(Enumerable.Range(0, 8).Select(_ => TestExecutor.ExecuteCaseResultAsync(descriptor, null, null, ct)));
                    TestAssert.Equal(8, created, "contexts");
                    TestAssert.True(results.All(r => r.Outcome == TestOutcome.Passed), "all passed");
                }),
                Check.Async(S, "SCN-15", "Typed examples have stable independent identities", async ct =>
                {
                    ScenarioExample<int>[] rows = { new ScenarioExample<int>("zero", 0), new ScenarioExample<int>("one", 1) };
                    IReadOnlyList<TestCaseDescriptor> cases = ScenarioExamples.Expand(rows, row => new ScenarioBuilder<int>("BDD", row.ExampleId, "example", _ => Task.FromResult(row.Value))
                        .Then("a", "expected", (value, _) => { TestAssert.Equal(row.Value, value, "typed row"); return Task.CompletedTask; })
                        .WithMetadata(new ScenarioMetadata("Examples", new[] { "REQ-1" }, row.ExampleId)).Build());
                    TestAssert.Equal("BDD.zero", cases[0].TestId, "identity");
                    TestAssert.Equal("one", cases[1].Metadata!.ExampleId, "example metadata");
                    foreach (TestCaseDescriptor descriptor in cases) await TestExecutor.ExecuteCaseAsync(descriptor, null, ct);
                }),
                Check.Async(S, "SCN-16", "Steps do not inflate summary counts or run hooks per case", async ct =>
                {
                    int before = 0;
                    int after = 0;
                    TestResultCollector collector = new TestResultCollector();
                    TestSuiteDescriptor suite = new TestSuiteDescriptor("BDD", "mixed", new[] { Builder().Given("a", "pass", (_, _) => Task.CompletedTask).And("b", "pass", (_, _) => Task.CompletedTask).Build(), Fixtures.Passing("BDD", "ordinary") },
                        _ => { before++; return default; }, _ => { after++; return default; });
                    TestRunSummary summary = await TestExecutor.RunSuiteAsync(suite, collector, null, ct);
                    TestAssert.Equal(2, summary.Total, "total");
                    TestAssert.Equal(1, before, "before");
                    TestAssert.Equal(1, after, "after");
                    TestAssert.Equal(0, collector.Results[1].Steps.Count, "ordinary steps");
                }),
                Check.Async(S, "SCN-17", "Host diagnostics keep the original exception", async ct =>
                {
                    string output = string.Empty;
                    TestCaseDescriptor descriptor = Builder().Then("a", "visible result", (_, _) => throw new FormatException("bad")).Build();
                    await TestAssert.ThrowsAsync<FormatException>(() => ScenarioDiagnostics.ExecuteAsync(descriptor, text => output = text, null, ct), "diagnostics");
                    TestAssert.Contains("visible result", output, "step name");
                    TestAssert.Contains("Failed", output, "outcome");
                }),
                Check.Async(S, "SCN-18", "Disposal prefers IAsyncDisposable, falls back to IDisposable, and disposes once", async ct =>
                {
                    ScenarioContext both = new ScenarioContext();
                    await TestExecutor.ExecuteCaseAsync(new ScenarioBuilder<ScenarioContext>("BDD", "both", "both", _ => Task.FromResult(both)).Given("a", "a", (_, _) => Task.CompletedTask).Build(), null, ct);
                    TestAssert.Equal(1, both.AsyncDisposals, "async once");
                    TestAssert.Equal(0, both.SyncDisposals, "sync not called");
                    SyncOnlyContext sync = new SyncOnlyContext();
                    await TestExecutor.ExecuteCaseAsync(new ScenarioBuilder<SyncOnlyContext>("BDD", "sync", "sync", _ => Task.FromResult(sync)).Given("a", "a", (_, _) => Task.CompletedTask).Build(), null, ct);
                    TestAssert.Equal(1, sync.Disposals, "sync once");
                }),
                Check.Async(S, "SCN-19", "Cleanup that exceeds CleanupTimeout sees its token cancel and records an error", async ct =>
                {
                    ScenarioBuilder<int> builder = Builder().Given("a", "a", (_, _) => Task.CompletedTask)
                        .WithCleanup(async (_, token) => await Task.Delay(Timeout.Infinite, token));
                    builder.CleanupTimeout = TimeSpan.FromMilliseconds(50);
                    TestResult result = await TestExecutor.ExecuteCaseResultAsync(builder.Build(), null, null, ct);
                    TestAssert.Equal(TestOutcome.Failed, result.Outcome, "outcome");
                    TestAssert.True(result.CleanupExceptions.Single() is OperationCanceledException, "cleanup canceled");
                }),
                Check.Sync(S, "SCN-20", "Builder tags and skip flow to the descriptor", () =>
                {
                    TestCaseDescriptor descriptor = Builder().WithTags(new[] { "bdd", "BDD", "smoke" }).WithSkip("later").Given("a", "a", (_, _) => Task.CompletedTask).Build();
                    TestAssert.Equal("bdd,smoke", string.Join(",", descriptor.Tags), "tags");
                    TestAssert.Equal("later", descriptor.SkipReason, "skip");
                    TestAssert.Throws<ArgumentNullException>(() => Builder().WithSkip(null!), "null reason");
                    TestAssert.Throws<ArgumentException>(() => Builder().WithTags(new[] { "a b" }), "bad tag");
                }),
                Check.Async(S, "SCN-21", "And inherits the preceding step's kind", async ct =>
                {
                    TestResult result = await TestExecutor.ExecuteCaseResultAsync(Builder().Given("a", "a", (_, _) => Task.CompletedTask).And("b", "b", (_, _) => Task.CompletedTask)
                        .When("c", "c", (_, _) => Task.CompletedTask).And("d", "d", (_, _) => Task.CompletedTask).Build(), null, null, ct);
                    TestAssert.Equal("Given,Given,When,When", string.Join(",", result.Steps.Select(s => s.Kind)), "kinds");
                }),
                Check.Sync(S, "SCN-22", "Metadata copies requirements and rejects blanks", () =>
                {
                    List<string> requirements = new List<string> { "REQ-1" };
                    ScenarioMetadata metadata = new ScenarioMetadata("feature", requirements);
                    requirements.Add("REQ-2");
                    TestAssert.Equal(1, metadata.Requirements.Count, "copied");
                    TestAssert.Throws<ArgumentException>(() => new ScenarioMetadata("f", new[] { " " }), "blank requirement");
                    TestAssert.Throws<ArgumentNullException>(() => new ScenarioMetadata(null!), "null feature");
                }),
                Check.Async(S, "SCN-23", "A copied scenario still creates a fresh context per execution", async ct =>
                {
                    List<ScenarioContext> contexts = new List<ScenarioContext>();
                    TestCaseDescriptor copy = Fixtures.Scenario("BDD", "copy", (c, t) => { c.Value++; return Task.CompletedTask; }, contexts).WithTags(new[] { "x" });
                    await TestExecutor.ExecuteCaseAsync(copy, null, ct);
                    await TestExecutor.ExecuteCaseAsync(copy, null, ct);
                    TestAssert.Equal(2, contexts.Count, "contexts");
                    TestAssert.True(contexts.All(c => c.Value == 1 && c.AsyncDisposals == 1), "isolated and disposed");
                }),
                new ScenarioBuilder<ScenarioContext>(S, "SCN-24", "SCN-24 Incrementing a fresh context changes its observable value", _ => Task.FromResult(new ScenarioContext()))
                    .WithTags(new[] { "bdd" })
                    .Given("empty", "a fresh context", (context, _) => { TestAssert.Equal(0, context.Value, "initial"); return Task.CompletedTask; })
                    .When("increment", "the value is incremented", (context, _) => { context.Value++; return Task.CompletedTask; })
                    .Then("one", "the value is one", (context, _) => { TestAssert.Equal(1, context.Value, "result"); return Task.CompletedTask; })
                    .WithMetadata(new ScenarioMetadata("Context behavior", new[] { "BDD-ISOLATION" }))
                    .Build(),
            });
        }

        /// <summary>
        /// Context that implements only <see cref="IDisposable"/>.
        /// </summary>
        private sealed class SyncOnlyContext : IDisposable
        {
            public int Disposals { get; private set; }

            public void Dispose()
            {
                Disposals++;
            }
        }
    }
}
