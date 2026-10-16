namespace Test.Shared
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Diagnostics.Metrics;
    using System.IO;
    using System.Linq;
    using System.Text.Json;
    using System.Threading;
    using System.Threading.Tasks;

    using Touchstone.Cli;
    using Touchstone.Core;

    /// <summary>Shared behavioral contract tests executed by every host.</summary>
    public static class ScenarioSuites
    {
        /// <summary>Lifecycle, result, reporting, and telemetry contract suite.</summary>
        public static TestSuiteDescriptor All()
        {
            const string S = "Scenarios";
            return new TestSuiteDescriptor(S, "Behavior driven scenarios", new List<TestCaseDescriptor>
            {
                CaseBuilder.Sync(S, "Guards", "Invalid scenario definitions fail early", () =>
                {
                    TestAssert.Throws<ArgumentException>(() => new ScenarioBuilder<int>("", "c", "n", _ => Task.FromResult(0)), "blank suite");
                    TestAssert.Throws<ArgumentNullException>(() => new ScenarioBuilder<int>("s", "c", "n", null!), "null factory");
                    ScenarioBuilder<int> builder = Builder();
                    TestAssert.Throws<InvalidOperationException>(() => builder.Build(), "empty steps");
                    TestAssert.Throws<InvalidOperationException>(() => builder.And("a", "and", (_, _) => Task.CompletedTask), "first And");
                    builder.Given("a", "ready", (_, _) => Task.CompletedTask);
                    TestAssert.Throws<ArgumentException>(() => builder.Then("a", "duplicate", (_, _) => Task.CompletedTask), "duplicate identity");
                    TestAssert.Throws<ArgumentOutOfRangeException>(() => builder.CleanupTimeout = TimeSpan.Zero, "cleanup budget");
                    TestAssert.Throws<ArgumentException>(() => new ScenarioStepDescriptor<int>("s", "", ScenarioStepKind.Given, (_, _) => Task.CompletedTask), "blank name");
                    TestAssert.Throws<ArgumentException>(() => new ScenarioMetadata(exampleId: " "), "blank example");
                }),
                CaseBuilder.Async(S, "StepOrder", "Steps share context and emit ordered live events", async ct =>
                {
                    ScenarioObserver observer = new ScenarioObserver();
                    TestCaseDescriptor descriptor = Builder().Given("given", "a value", (_, _) => Task.CompletedTask)
                        .When("when", "an action", (_, _) => Task.CompletedTask)
                        .Then("then", "a result", (_, _) => Task.CompletedTask)
                        .And("and", "another result", (_, _) => Task.CompletedTask).Build();
                    TestResult result = await TestExecutor.ExecuteCaseResultAsync(descriptor, ct, observer).ConfigureAwait(false);
                    TestAssert.True(result.Success, "scenario passed");
                    TestAssert.Equal(4, result.Steps.Count, "step count");
                    TestAssert.Equal(ScenarioStepKind.Then, result.Steps[3].Kind, "And inherits");
                    TestAssert.Equal("start:given,complete:given,start:when,complete:when,start:then,complete:then,start:and,complete:and", string.Join(",", observer.Events), "event order");
                }),
                CaseBuilder.Async(S, "Failure", "Failure preserves exception, prevents subsequent actions, and rethrows", async ct =>
                {
                    InvalidOperationException error = new InvalidOperationException("primary");
                    int later = 0;
                    TestCaseDescriptor descriptor = Builder().Given("a", "ready", (_, _) => Task.CompletedTask)
                        .When("b", "fails", (_, _) => throw error)
                        .Then("c", "not run", (_, _) => { later++; return Task.CompletedTask; }).Build();
                    TestResult result = await TestExecutor.ExecuteCaseResultAsync(descriptor, ct).ConfigureAwait(false);
                    TestAssert.Same(error, result.Exception, "original exception");
                    TestAssert.Equal(ScenarioStepOutcome.Failed, result.Steps[1].Outcome, "failed step");
                    TestAssert.Equal(ScenarioStepOutcome.NotRun, result.Steps[2].Outcome, "not run");
                    TestAssert.Equal(0, later, "later actions prevented");
                    InvalidOperationException rethrown = await TestAssert.ThrowsAsync<InvalidOperationException>(() => TestExecutor.ExecuteCaseAsync(descriptor, ct), "throwing API").ConfigureAwait(false);
                    TestAssert.Same(error, rethrown, "same exception");
                    TestAssert.Contains("ScenarioSuites", rethrown.StackTrace, "original stack");
                    await TestAssert.ThrowsAsync<InvalidOperationException>(() => descriptor.ExecuteAsync(ct), "direct delegate").ConfigureAwait(false);
                }),
                CaseBuilder.Async(S, "Snapshots", "Building freezes steps and execution cannot be replaced", async ct =>
                {
                    ScenarioBuilder<int> builder = Builder().Given("a", "first", (_, _) => Task.CompletedTask);
                    TestCaseDescriptor descriptor = builder.Build();
                    builder.Then("b", "later", (_, _) => Task.CompletedTask);
                    TestResult result = await TestExecutor.ExecuteCaseResultAsync(descriptor, ct).ConfigureAwait(false);
                    TestAssert.Equal(1, result.Steps.Count, "snapshot");
                    TestAssert.Throws<InvalidOperationException>(() => descriptor.ExecuteAsync = _ => Task.CompletedTask, "ambiguous body rejected");
                }),
                CaseBuilder.Async(S, "Skipped", "Skipped scenarios neither create nor dispose context", async ct =>
                {
                    int created = 0;
                    TestCaseDescriptor descriptor = new ScenarioBuilder<int>("BDD", "skip", "skip", _ => { created++; return Task.FromResult(0); })
                        .Given("a", "not entered", (_, _) => Task.CompletedTask).Build();
                    descriptor.Skip = true;
                    descriptor.SkipReason = "not ready";
                    TestResult result = await TestExecutor.ExecuteCaseResultAsync(descriptor, ct).ConfigureAwait(false);
                    TestAssert.True(result.Skipped && result.Success, "legacy skip");
                    TestAssert.Equal(0, created, "no context");
                    TestAssert.Equal(ScenarioStepOutcome.Skipped, result.Steps[0].Outcome, "skipped step");
                    TestAssert.Equal(TimeSpan.Zero, result.Duration, "skip time");
                }),
                CaseBuilder.Async(S, "FactoryFailure", "Context factory failure leaves every step unexecuted", async ct =>
                {
                    TestCaseDescriptor descriptor = new ScenarioBuilder<int>("BDD", "factory", "factory", _ => throw new FormatException("factory"))
                        .Given("a", "not entered", (_, _) => Task.CompletedTask).Build();
                    TestResult result = await TestExecutor.ExecuteCaseResultAsync(descriptor, ct).ConfigureAwait(false);
                    TestAssert.True(result.Exception is FormatException, "factory exception");
                    TestAssert.Contains("Context creation", result.Message, "stage");
                    TestAssert.Equal(ScenarioStepOutcome.NotRun, result.Steps[0].Outcome, "not run");
                }),
                CaseBuilder.Async(S, "NullFactory", "Null context is a contextual failure", async ct =>
                {
                    TestCaseDescriptor descriptor = new ScenarioBuilder<object>("BDD", "null", "null", _ => Task.FromResult<object>(null!))
                        .Given("a", "not entered", (_, _) => Task.CompletedTask).Build();
                    TestResult result = await TestExecutor.ExecuteCaseResultAsync(descriptor, ct).ConfigureAwait(false);
                    TestAssert.True(result.Exception is InvalidOperationException, "null factory result");
                }),
                CaseBuilder.Async(S, "Cancellation", "Matching caller cancellation is distinct and cleanup still runs", async ct =>
                {
                    using (CancellationTokenSource source = new CancellationTokenSource())
                    {
                        ScenarioContext context = new ScenarioContext();
                        bool cleanup = false;
                        TestCaseDescriptor descriptor = new ScenarioBuilder<ScenarioContext>("BDD", "cancel", "cancel", _ => Task.FromResult(context))
                            .When("a", "cancel", (_, token) => { source.Cancel(); token.ThrowIfCancellationRequested(); return Task.CompletedTask; })
                            .Then("b", "not run", (_, _) => Task.CompletedTask)
                            .WithCleanup((_, token) => { cleanup = !token.IsCancellationRequested; return Task.CompletedTask; }).Build();
                        TestResult result = await TestExecutor.ExecuteCaseResultAsync(descriptor, source.Token).ConfigureAwait(false);
                        TestAssert.True(result.Canceled && !result.Success, "canceled is not success");
                        TestAssert.Equal(ScenarioStepOutcome.Canceled, result.Steps[0].Outcome, "active cancellation");
                        TestAssert.Equal(ScenarioStepOutcome.NotRun, result.Steps[1].Outcome, "remaining step");
                        TestAssert.True(cleanup, "fresh cleanup token");
                        TestAssert.Equal(1, context.AsyncDisposals, "disposed once");
                    }
                }),
                CaseBuilder.Async(S, "PreCanceled", "Pre-cancellation does not create context", async ct =>
                {
                    using (CancellationTokenSource source = new CancellationTokenSource())
                    {
                        source.Cancel();
                        int created = 0;
                        TestCaseDescriptor descriptor = new ScenarioBuilder<int>("BDD", "before", "before", _ => { created++; return Task.FromResult(0); })
                            .Given("a", "not run", (_, _) => Task.CompletedTask).Build();
                        TestResult result = await TestExecutor.ExecuteCaseResultAsync(descriptor, source.Token).ConfigureAwait(false);
                        TestAssert.True(result.Canceled, "pre-canceled");
                        TestAssert.Equal(0, created, "no context");
                    }
                }),
                CaseBuilder.Async(S, "UnrelatedCancellation", "Unrelated cancellation remains a failure", async ct =>
                {
                    TestCaseDescriptor descriptor = Builder().When("a", "fails", (_, _) => throw new OperationCanceledException()).Build();
                    TestResult result = await TestExecutor.ExecuteCaseResultAsync(descriptor, ct).ConfigureAwait(false);
                    TestAssert.False(result.Canceled || result.Success, "failure");
                }),
                CaseBuilder.Async(S, "CleanupErrors", "Primary failure and both cleanup errors survive", async ct =>
                {
                    ScenarioContext context = new ScenarioContext { FailDisposal = true };
                    InvalidOperationException primary = new InvalidOperationException("primary");
                    TestCaseDescriptor descriptor = new ScenarioBuilder<ScenarioContext>("BDD", "cleanup", "cleanup", _ => Task.FromResult(context))
                        .When("a", "fails", (_, _) => throw primary)
                        .WithCleanup((_, _) => throw new FormatException("cleanup failure")).Build();
                    TestResult result = await TestExecutor.ExecuteCaseResultAsync(descriptor, ct).ConfigureAwait(false);
                    TestAssert.Same(primary, result.Exception, "primary preserved");
                    TestAssert.Equal(2, result.CleanupExceptions.Count, "both errors");
                    TestAssert.Equal(1, context.AsyncDisposals, "async disposal once");
                    TestAssert.Equal(0, context.SyncDisposals, "async preferred");
                }),
                CaseBuilder.Async(S, "CleanupOnlyFailure", "Cleanup failure fails an otherwise passing case", async ct =>
                {
                    TestResult result = await TestExecutor.ExecuteCaseResultAsync(Builder().Given("a", "pass", (_, _) => Task.CompletedTask)
                        .WithCleanup((_, _) => throw new InvalidOperationException("cleanup only")).Build(), ct).ConfigureAwait(false);
                    TestAssert.False(result.Success, "cleanup failure");
                    TestAssert.Contains("Context cleanup", result.Message, "context");
                }),
                CaseBuilder.Async(S, "ObserverFailure", "Observer failures propagate after owned context disposal", async ct =>
                {
                    foreach (bool failStart in new[] { true, false })
                    {
                        ScenarioContext context = new ScenarioContext();
                        ScenarioObserver observer = new ScenarioObserver { FailStart = failStart, FailCompletion = !failStart };
                        TestCaseDescriptor descriptor = new ScenarioBuilder<ScenarioContext>("BDD", "observer", "observer", _ => Task.FromResult(context))
                            .Given("a", "observed", (_, _) => Task.CompletedTask).Build();
                        await TestAssert.ThrowsAsync<InvalidOperationException>(() => TestExecutor.ExecuteCaseResultAsync(descriptor, ct, observer), "observer").ConfigureAwait(false);
                        TestAssert.Equal(1, context.AsyncDisposals, "cleanup after observer");
                    }
                }),
                CaseBuilder.Async(S, "Isolation", "Concurrent and repeated executions own independent context", async ct =>
                {
                    int created = 0;
                    TestCaseDescriptor descriptor = new ScenarioBuilder<ScenarioContext>("BDD", "isolation", "isolation", _ =>
                        { Interlocked.Increment(ref created); return Task.FromResult(new ScenarioContext()); })
                        .When("a", "increment", async (context, token) => { await Task.Yield(); context.Value++; })
                        .Then("b", "one", (context, _) => { TestAssert.Equal(1, context.Value, "isolated value"); return Task.CompletedTask; }).Build();
                    TestResult[] results = await Task.WhenAll(Enumerable.Range(0, 8).Select(_ => TestExecutor.ExecuteCaseResultAsync(descriptor, ct))).ConfigureAwait(false);
                    TestAssert.Equal(8, created, "context per execution");
                    TestAssert.True(results.All(r => r.Success), "all pass");
                    TestAssert.False(ReferenceEquals(results[0].Steps, results[1].Steps), "separate snapshots");
                }),
                CaseBuilder.Async(S, "Examples", "Typed examples have stable independent identities", async ct =>
                {
                    ScenarioExample<int>[] rows = { new ScenarioExample<int>("zero", 0), new ScenarioExample<int>("one", 1) };
                    IReadOnlyList<TestCaseDescriptor> cases = ScenarioExamples.Expand(rows, row => new ScenarioBuilder<int>("BDD", row.ExampleId, "example", _ => Task.FromResult(row.Value))
                        .Then("a", "expected", (value, _) => { TestAssert.Equal(row.Value, value, "typed row"); return Task.CompletedTask; })
                        .WithMetadata(new ScenarioMetadata("Examples", new[] { "REQ-1" }, row.ExampleId)).Build());
                    TestAssert.Equal("BDD.zero", cases[0].TestId, "stable identity");
                    TestAssert.Equal("one", cases[1].Metadata!.ExampleId, "example metadata");
                    foreach (TestCaseDescriptor descriptor in cases) await TestExecutor.ExecuteCaseAsync(descriptor, ct).ConfigureAwait(false);
                    TestAssert.Throws<ArgumentException>(() => ScenarioExamples.Expand(new[] { rows[0], rows[0] }, _ => cases[0]), "duplicate row");
                    TestAssert.Throws<ArgumentException>(() => ScenarioExamples.Expand(rows, _ => cases[0]), "duplicate case");
                }),
                CaseBuilder.Async(S, "MixedSuite", "Steps do not inflate summary counts or run suite hooks per case", async ct =>
                {
                    int before = 0;
                    int after = 0;
                    TestResultCollector collector = new TestResultCollector();
                    TestSuiteDescriptor suite = new TestSuiteDescriptor("BDD", "mixed", new[] { Builder().Given("a", "pass", (_, _) => Task.CompletedTask).Build(), Fixtures.Passing("BDD", "ordinary") },
                        _ => { before++; return ValueTask.CompletedTask; }, _ => { after++; return ValueTask.CompletedTask; });
                    TestRunSummary summary = await TestExecutor.RunSuiteAsync(suite, collector, ct).ConfigureAwait(false);
                    TestAssert.Equal(2, summary.Total, "case count");
                    TestAssert.Equal(2, summary.Passed, "passed count");
                    TestAssert.Equal(1, before, "before hook");
                    TestAssert.Equal(1, after, "after hook");
                    TestAssert.Equal(0, collector.Results[1].Steps.Count, "ordinary case unchanged");
                }),
                CaseBuilder.Async(S, "NativeOutput", "Host diagnostics retain the original exception", async ct =>
                {
                    string output = "";
                    TestCaseDescriptor descriptor = Builder().Then("a", "secret result", (_, _) => throw new FormatException("bad")).Build();
                    await TestAssert.ThrowsAsync<FormatException>(() => ScenarioDiagnostics.ExecuteAsync(descriptor, text => output = text, ct), "native output failure").ConfigureAwait(false);
                    TestAssert.Contains("secret result", output, "step name");
                    TestAssert.Contains("Failed", output, "outcome");
                }),
                CaseBuilder.Async(S, "ConsoleJson", "Expanded console and additive JSON redact consumer text", async ct =>
                {
                    string path = Path.GetTempFileName();
                    try
                    {
                        using (StringWriter writer = new StringWriter())
                        {
                            ConsoleResultSink sink = new ConsoleResultSink(writer, false) { ExpandSteps = true, Redact = text => text.Replace("secret", "[redacted]", StringComparison.Ordinal) };
                            TestCaseDescriptor descriptor = Builder().Then("a", "secret", (_, _) => throw new InvalidOperationException("secret"))
                                .And("b", "later", (_, _) => Task.CompletedTask).Build();
                            int code = await ConsoleRunner.RunAsync(new[] { Fixtures.Suite("BDD", descriptor) }, sink, path, ct).ConfigureAwait(false);
                            TestAssert.Equal(1, code, "failure exit");
                            TestAssert.False(writer.ToString().Contains("secret", StringComparison.Ordinal), "redacted console");
                            TestAssert.Contains("NotRun", writer.ToString(), "remaining step");
                            string json = await File.ReadAllTextAsync(path, ct).ConfigureAwait(false);
                            TestAssert.False(json.Contains("secret", StringComparison.Ordinal), "redacted JSON");
                            JsonResultEntry[] rows = JsonSerializer.Deserialize<JsonResultEntry[]>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true })!;
                            TestAssert.Equal(2, rows[0].Steps.Count, "typed JSON");
                            TestAssert.Equal("Failed", rows[0].Steps[0].Outcome, "string outcome");
                            TestAssert.False(rows[0].Success, "legacy field");
                        }
                    }
                    finally { File.Delete(path); }
                }),
                CaseBuilder.Async(S, "HtmlSafety", "Offline report escapes hostile names and redacts diagnostics", async ct =>
                {
                    string path = Path.GetTempFileName();
                    try
                    {
                        TestResult result = await TestExecutor.ExecuteCaseResultAsync(Builder().Then("a", "<script>alert('secret')</script>", (_, _) => throw new FormatException("secret")).Build(), ct).ConfigureAwait(false);
                        await ScenarioHtmlReport.WriteAsync(new[] { result }, path, "<run>", ct, text => text.Replace("secret", "hidden", StringComparison.Ordinal)).ConfigureAwait(false);
                        string html = await File.ReadAllTextAsync(path, ct).ConfigureAwait(false);
                        TestAssert.False(html.Contains("<script>", StringComparison.Ordinal), "no active scripts");
                        TestAssert.False(html.Contains("secret", StringComparison.Ordinal), "redacted");
                        TestAssert.Contains("&lt;script&gt;", html, "escaped name");
                        TestAssert.Contains("scope=\"col\"", html, "accessible headings");
                        await ScenarioHtmlReport.WriteAsync(Array.Empty<TestResult>(), path, "empty", ct).ConfigureAwait(false);
                        TestAssert.Contains("Total: 0", await File.ReadAllTextAsync(path, ct).ConfigureAwait(false), "empty report");
                        await ScenarioHtmlReport.WriteAsync(Enumerable.Repeat(result, 1000).ToArray(), path, "large", ct).ConfigureAwait(false);
                        TestAssert.Contains("Total: 1000", await File.ReadAllTextAsync(path, ct).ConfigureAwait(false), "large report");
                        using (CancellationTokenSource canceled = new CancellationTokenSource())
                        {
                            canceled.Cancel();
                            await TestAssert.ThrowsAsync<OperationCanceledException>(() => ScenarioHtmlReport.WriteAsync(new[] { result }, path, "canceled", canceled.Token), "canceled export").ConfigureAwait(false);
                        }
                        await TestAssert.ThrowsAsync<DirectoryNotFoundException>(() => ScenarioHtmlReport.WriteAsync(new[] { result }, Path.Combine(path, "missing.html"), "bad", ct), "export error").ConfigureAwait(false);
                    }
                    finally { File.Delete(path); }
                }),
                CaseBuilder.Async(S, "Telemetry", "Step spans nest below cases and metric labels stay bounded", async ct =>
                {
                    using (TelemetryCapture capture = new TelemetryCapture())
                    {
                        await TestExecutor.ExecuteCaseAsync(Builder().Given("secret-id", "secret name", (_, _) => Task.CompletedTask).Build(), ct).ConfigureAwait(false);
                        List<Activity> spans = capture.Spans().Where(span => span.DisplayName == "scenario:step").ToList();
                        TestAssert.Equal(1, spans.Count, "one step span");
                        TestAssert.Equal(capture.Span("case:BDD.case").SpanId, spans[0].ParentSpanId, "step parent is case");
                        TestAssert.False(spans[0].TagObjects.Any(tag => tag.Value?.ToString()?.Contains("secret", StringComparison.Ordinal) == true), "no consumer text");
                        TestAssert.True(capture.Measurements("touchstone.scenario.operations").Count >= 3, "context step cleanup metrics");
                    }
                }),
                CaseBuilder.Async(S, "ThrowingTelemetry", "Throwing measurement listeners cannot alter scenario outcomes", async ct =>
                {
                    int injected = 0;
                    using (TelemetryCapture capture = new TelemetryCapture())
                    using (MeterListener listener = new MeterListener())
                    {
                        listener.InstrumentPublished = (instrument, l) => { if (instrument.Meter.Name == "Touchstone") l.EnableMeasurementEvents(instrument); };
                        listener.SetMeasurementEventCallback<long>((_, _, _, _) => { if (Activity.Current?.TraceId == capture.TraceId) { injected++; throw new InvalidOperationException("listener"); } });
                        listener.SetMeasurementEventCallback<double>((_, _, _, _) => { if (Activity.Current?.TraceId == capture.TraceId) { injected++; throw new InvalidOperationException("listener"); } });
                        listener.Start();
                        TestResult result = await TestExecutor.ExecuteCaseResultAsync(Builder().Then("a", "pass", (_, _) => Task.CompletedTask).Build(), ct).ConfigureAwait(false);
                        TestAssert.True(result.Success, "listeners are best effort");
                        TestAssert.True(injected > 0, "fault was exercised");
                    }
                }),
                new ScenarioBuilder<ScenarioContext>(S, "ActualScenario", "Incrementing a fresh context changes its observable value", _ => Task.FromResult(new ScenarioContext()))
                    .Given("empty", "a fresh context", (context, _) => { TestAssert.Equal(0, context.Value, "initial value"); return Task.CompletedTask; })
                    .When("increment", "the value is incremented", (context, _) => { context.Value++; return Task.CompletedTask; })
                    .Then("one", "the value is one", (context, _) => { TestAssert.Equal(1, context.Value, "result"); return Task.CompletedTask; })
                    .WithMetadata(new ScenarioMetadata("Context behavior", new[] { "BDD-ISOLATION" })).Build()
            });
        }

        private static ScenarioBuilder<int> Builder()
        {
            return new ScenarioBuilder<int>("BDD", "case", "behavior", _ => Task.FromResult(0));
        }
    }
}
