namespace Test.Shared
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Diagnostics.Metrics;
    using System.IO;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Touchstone.Cli;
    using Touchstone.Core;

    /// <summary>
    /// Suites proving that Touchstone emits its documented spans and metrics (see TELEMETRY.md), including failure paths.
    /// Every case serializes on a shared gate because <see cref="TouchstoneTelemetry.SuiteLabelEnabled"/> is process-wide,
    /// and every capture filters on its own trace id so concurrent runners in the same process cannot interfere.
    /// </summary>
    public static class TelemetrySuites
    {
        private static readonly SemaphoreSlim _Gate = new SemaphoreSlim(1, 1);

        /// <summary>
        /// Suite covering spans and metrics emitted by <see cref="TestExecutor"/>, the adapters, and the console runner.
        /// </summary>
        /// <returns>Suite descriptor.</returns>
        public static TestSuiteDescriptor TelemetrySuite()
        {
            const string S = "Telemetry";

            return new TestSuiteDescriptor(S, "Telemetry", new List<TestCaseDescriptor>
            {
                Guarded(S, "SpanTree", "RunAsync emits run, suite, stage, case, and sink spans nested in one trace", async ct =>
                {
                    string id = UniqueSuiteId();
                    using (TelemetryCapture capture = new TelemetryCapture())
                    {
                        await TestExecutor.RunAsync(new List<TestSuiteDescriptor> { MixedSuite(id) }, new RecordingSink(), ct).ConfigureAwait(false);

                        Activity run = capture.Span(TouchstoneTelemetry.RunSpanName);
                        Activity suite = capture.Span(TouchstoneTelemetry.SuiteSpanPrefix + id);
                        Activity before = capture.Span(TouchstoneTelemetry.StageSpanPrefix + TouchstoneTelemetry.StageBeforeSuite);
                        Activity after = capture.Span(TouchstoneTelemetry.StageSpanPrefix + TouchstoneTelemetry.StageAfterSuite);
                        Activity pass = capture.Span(TouchstoneTelemetry.CaseSpanPrefix + id + ".Pass");
                        Activity fail = capture.Span(TouchstoneTelemetry.CaseSpanPrefix + id + ".Fail");
                        Activity skip = capture.Span(TouchstoneTelemetry.CaseSpanPrefix + id + ".Skip");

                        TestAssert.Equal(capture.Root.SpanId, run.ParentSpanId, "run parent");
                        foreach (Activity child in new[] { before, after, pass, fail, skip })
                            TestAssert.Equal(suite.SpanId, child.ParentSpanId, child.DisplayName + " parent");
                        TestAssert.Equal(run.SpanId, suite.ParentSpanId, "suite parent");

                        TestAssert.Equal(ActivityStatusCode.Error, run.Status, "run status");
                        TestAssert.Equal(TouchstoneTelemetry.OutcomeFailed, run.GetTagItem(TouchstoneTelemetry.AttributeOutcome), "run outcome");
                        TestAssert.Equal(3, run.GetTagItem(TouchstoneTelemetry.AttributeCaseCount), "run case count");
                        TestAssert.Equal(1, run.GetTagItem(TouchstoneTelemetry.AttributeFailedCount), "run failed count");
                        TestAssert.Equal(ActivityStatusCode.Error, suite.Status, "suite status");
                        TestAssert.Equal(id, suite.GetTagItem(TouchstoneTelemetry.AttributeSuiteId), "suite id attribute");
                        TestAssert.Equal(ActivityStatusCode.Ok, before.Status, "before status");
                        TestAssert.Equal(ActivityStatusCode.Ok, after.Status, "after status");

                        TestAssert.Equal(ActivityStatusCode.Ok, pass.Status, "pass status");
                        TestAssert.Equal(id + ".Pass", pass.GetTagItem(TouchstoneTelemetry.AttributeTestId), "test id attribute");
                        TestAssert.Equal("Pass", pass.GetTagItem(TouchstoneTelemetry.AttributeCaseId), "case id attribute");
                        TestAssert.Equal(ActivityStatusCode.Error, fail.Status, "fail status");
                        TestAssert.Equal("case broke", fail.StatusDescription, "fail status description");
                        TestAssert.Equal(typeof(InvalidOperationException).FullName, fail.GetTagItem(TouchstoneTelemetry.LabelErrorType), "fail error.type");
                        ActivityEvent exceptionEvent = fail.Events.Single(e => e.Name == "exception");
                        TestAssert.Equal(typeof(InvalidOperationException).FullName, exceptionEvent.Tags.First(t => t.Key == "exception.type").Value, "exception.type");
                        TestAssert.Equal(TouchstoneTelemetry.OutcomeSkipped, skip.GetTagItem(TouchstoneTelemetry.AttributeOutcome), "skip outcome");
                        TestAssert.Equal("later", skip.GetTagItem(TouchstoneTelemetry.AttributeSkipReason), "skip reason");

                        List<Activity> sinkSpans = capture.Spans().Where(a => a.DisplayName.StartsWith(TouchstoneTelemetry.SinkSpanPrefix, StringComparison.Ordinal)).ToList();
                        TestAssert.Equal(5, sinkSpans.Count, "sink span count (start, 3 results, complete)");
                        TestAssert.True(sinkSpans.All(a => a.ParentSpanId == suite.SpanId), "sink spans nest under the suite");
                    }
                }),

                Guarded(S, "Metrics", "RunAsync records run, suite, stage, case, sink, and error metrics with bounded labels", async ct =>
                {
                    string id = UniqueSuiteId();
                    using (TelemetryCapture capture = new TelemetryCapture())
                    {
                        await TestExecutor.RunAsync(new List<TestSuiteDescriptor> { MixedSuite(id) }, new RecordingSink(), ct).ConfigureAwait(false);

                        CapturedMeasurement run = capture.Measurements(TouchstoneTelemetry.Runs).Single();
                        TestAssert.Equal(TouchstoneTelemetry.OutcomeFailed, run.Tag(TouchstoneTelemetry.LabelOutcome), "run outcome");
                        CapturedMeasurement runDuration = capture.Measurements(TouchstoneTelemetry.RunDuration).Single();
                        TestAssert.Equal("s", runDuration.Unit, "run duration unit");
                        TestAssert.True(runDuration.Value >= 0, "run duration non-negative");

                        CapturedMeasurement suite = capture.Measurements(TouchstoneTelemetry.Suites).Single();
                        TestAssert.Equal(id, suite.Tag(TouchstoneTelemetry.LabelSuite), "suite label");
                        TestAssert.Equal(TouchstoneTelemetry.OutcomeFailed, suite.Tag(TouchstoneTelemetry.LabelOutcome), "suite outcome");
                        TestAssert.Equal(1, capture.Measurements(TouchstoneTelemetry.SuiteDuration).Count, "suite duration count");

                        List<CapturedMeasurement> cases = capture.Measurements(TouchstoneTelemetry.Cases);
                        TestAssert.Equal(3, cases.Count, "case count");
                        foreach (string outcome in new[] { TouchstoneTelemetry.OutcomePassed, TouchstoneTelemetry.OutcomeFailed, TouchstoneTelemetry.OutcomeSkipped })
                            TestAssert.Equal(1, cases.Count(c => c.Tag(TouchstoneTelemetry.LabelOutcome) == outcome), "cases " + outcome);
                        TestAssert.True(cases.All(c => c.Tag(TouchstoneTelemetry.LabelSuite) == id), "cases carry the suite label");
                        TestAssert.True(cases.All(c => !c.Tags.ContainsKey(TouchstoneTelemetry.AttributeCaseId) && !c.Tags.ContainsKey("case")), "no case id on metrics");
                        TestAssert.Equal(3, capture.Measurements(TouchstoneTelemetry.CaseDuration).Count, "case duration count");
                        TestAssert.Equal("s", capture.Measurements(TouchstoneTelemetry.CaseDuration)[0].Unit, "case duration unit");

                        List<CapturedMeasurement> active = capture.Measurements(TouchstoneTelemetry.CasesActive);
                        TestAssert.Equal(4, active.Count, "active +1/-1 for two executed cases");
                        TestAssert.Equal(0.0, active.Sum(a => a.Value), "active returns to zero");

                        List<CapturedMeasurement> stages = capture.Measurements(TouchstoneTelemetry.Stages);
                        TestAssert.Equal(2, stages.Count, "stage count");
                        TestAssert.True(stages.All(s => s.Tag(TouchstoneTelemetry.LabelOutcome) == TouchstoneTelemetry.OutcomeOk), "stages ok");
                        TestAssert.Equal(1, stages.Count(s => s.Tag(TouchstoneTelemetry.LabelStage) == TouchstoneTelemetry.StageBeforeSuite), "before_suite stage");
                        TestAssert.Equal(2, capture.Measurements(TouchstoneTelemetry.StageDuration).Count, "stage duration count");

                        List<CapturedMeasurement> sinkEvents = capture.Measurements(TouchstoneTelemetry.SinkEvents);
                        TestAssert.Equal(5, sinkEvents.Count, "sink event count");
                        TestAssert.Equal(3, sinkEvents.Count(e => e.Tag(TouchstoneTelemetry.LabelEvent) == TouchstoneTelemetry.EventTestCompleted), "test_completed events");
                        TestAssert.Equal(5, capture.Measurements(TouchstoneTelemetry.SinkDuration).Count, "sink duration count");

                        CapturedMeasurement error = capture.Measurements(TouchstoneTelemetry.Errors).Single();
                        TestAssert.Equal(TouchstoneTelemetry.StageCase, error.Tag(TouchstoneTelemetry.LabelStage), "error stage");
                        TestAssert.Equal(typeof(InvalidOperationException).FullName, error.Tag(TouchstoneTelemetry.LabelErrorType), "error.type");
                    }
                }),

                Guarded(S, "PassingRunAndLastSuccess", "A passing run records outcome passed and updates the last-success gauges", async ct =>
                {
                    string id = UniqueSuiteId();
                    using (TelemetryCapture capture = new TelemetryCapture())
                    {
                        double before = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() / 1000.0 - 1;
                        TestRunSummary summary = await TestExecutor.RunAsync(
                            new List<TestSuiteDescriptor> { Fixtures.Suite(id, Fixtures.Passing(id, "A")) }, null, ct).ConfigureAwait(false);
                        TestAssert.True(summary.IsSuccess, "IsSuccess");

                        TestAssert.Equal(TouchstoneTelemetry.OutcomePassed, capture.Measurements(TouchstoneTelemetry.Runs).Single().Tag(TouchstoneTelemetry.LabelOutcome), "run outcome");
                        TestAssert.Equal(ActivityStatusCode.Ok, capture.Span(TouchstoneTelemetry.RunSpanName).Status, "run status");
                        TestAssert.Equal(0, capture.Spans().Count(a => a.DisplayName.StartsWith(TouchstoneTelemetry.SinkSpanPrefix, StringComparison.Ordinal)), "no sink spans without a sink");

                        CapturedMeasurement suiteSuccess = capture.Observe(TouchstoneTelemetry.SuiteLastSuccessTime)
                            .Last(m => m.Tag(TouchstoneTelemetry.LabelSuite) == id);
                        TestAssert.True(suiteSuccess.Value >= before, "suite last success is recent");
                        TestAssert.Equal("s", suiteSuccess.Unit, "suite last success unit");
                        CapturedMeasurement runSuccess = capture.Observe(TouchstoneTelemetry.RunLastSuccessTime).Last();
                        TestAssert.True(runSuccess.Value >= before, "run last success is recent");
                    }
                }),

                Guarded(S, "BuildInfo", "touchstone.build.info reports 1 with the version label", async ct =>
                {
                    using (TelemetryCapture capture = new TelemetryCapture())
                    {
                        await TestExecutor.RunSuiteAsync(Fixtures.Suite(UniqueSuiteId()), null, ct).ConfigureAwait(false);
                        CapturedMeasurement info = capture.Observe(TouchstoneTelemetry.BuildInfo).Last();
                        TestAssert.Equal(1.0, info.Value, "value");
                        string? version = info.Tag(TouchstoneTelemetry.LabelVersion);
                        TestAssert.True(!string.IsNullOrEmpty(version) && version != "unknown", "version label is set: " + version);
                    }
                }),

                Guarded(S, "BeforeHookFailure", "A BeforeSuite failure records a failed stage, an error, and suite and run outcome error", async ct =>
                {
                    string id = UniqueSuiteId();
                    TestSuiteDescriptor suite = new TestSuiteDescriptor(id, id,
                        new List<TestCaseDescriptor> { Fixtures.Passing(id, "A") },
                        beforeSuiteAsync: _ => throw new InvalidOperationException("setup failed"));

                    using (TelemetryCapture capture = new TelemetryCapture())
                    {
                        await TestAssert.ThrowsAsync<InvalidOperationException>(
                            () => TestExecutor.RunAsync(new List<TestSuiteDescriptor> { suite }, null, ct), "setup failure").ConfigureAwait(false);

                        AssertStageFailure(capture, id, TouchstoneTelemetry.StageBeforeSuite, TouchstoneTelemetry.OutcomeError);
                        TestAssert.Equal(0, capture.Measurements(TouchstoneTelemetry.Cases).Count, "no cases ran");
                        List<CapturedMeasurement> errors = capture.Measurements(TouchstoneTelemetry.Errors);
                        TestAssert.Equal(1, errors.Count(e => e.Tag(TouchstoneTelemetry.LabelStage) == TouchstoneTelemetry.StageRun), "run error recorded");
                    }
                }),

                Guarded(S, "AfterHookFailure", "An AfterSuite failure records a failed stage and suite outcome error", async ct =>
                {
                    string id = UniqueSuiteId();
                    TestSuiteDescriptor suite = new TestSuiteDescriptor(id, id,
                        new List<TestCaseDescriptor> { Fixtures.Passing(id, "A") },
                        afterSuiteAsync: _ => throw new InvalidOperationException("teardown failed"));

                    using (TelemetryCapture capture = new TelemetryCapture())
                    {
                        await TestAssert.ThrowsAsync<InvalidOperationException>(
                            () => TestExecutor.RunAsync(new List<TestSuiteDescriptor> { suite }, null, ct), "teardown failure").ConfigureAwait(false);

                        AssertStageFailure(capture, id, TouchstoneTelemetry.StageAfterSuite, TouchstoneTelemetry.OutcomeError);
                        TestAssert.Equal(1, capture.Measurements(TouchstoneTelemetry.Cases).Count, "case ran before teardown");
                    }
                }),

                Guarded(S, "CanceledOutcome", "An OperationCanceledException escaping a suite records outcome canceled", async ct =>
                {
                    string id = UniqueSuiteId();
                    TestSuiteDescriptor suite = new TestSuiteDescriptor(id, id,
                        new List<TestCaseDescriptor> { Fixtures.Passing(id, "A") },
                        beforeSuiteAsync: _ => throw new OperationCanceledException("stop"));

                    using (TelemetryCapture capture = new TelemetryCapture())
                    {
                        await TestAssert.ThrowsAsync<OperationCanceledException>(
                            () => TestExecutor.RunAsync(new List<TestSuiteDescriptor> { suite }, null, ct), "canceled run").ConfigureAwait(false);

                        TestAssert.Equal(TouchstoneTelemetry.OutcomeCanceled, capture.Measurements(TouchstoneTelemetry.Suites).Single().Tag(TouchstoneTelemetry.LabelOutcome), "suite outcome");
                        TestAssert.Equal(TouchstoneTelemetry.OutcomeCanceled, capture.Measurements(TouchstoneTelemetry.Runs).Single().Tag(TouchstoneTelemetry.LabelOutcome), "run outcome");
                    }
                }),

                Guarded(S, "SinkFailure", "A throwing sink records a failed sink event, a sink error, and suite outcome error", async ct =>
                {
                    string id = UniqueSuiteId();
                    RecordingSink sink = new RecordingSink(_ => throw new IOException("sink down"));

                    using (TelemetryCapture capture = new TelemetryCapture())
                    {
                        await TestAssert.ThrowsAsync<IOException>(
                            () => TestExecutor.RunSuiteAsync(Fixtures.Suite(id, Fixtures.Passing(id, "A")), sink, ct), "sink failure").ConfigureAwait(false);

                        CapturedMeasurement failedEvent = capture.Measurements(TouchstoneTelemetry.SinkEvents)
                            .Single(e => e.Tag(TouchstoneTelemetry.LabelOutcome) == TouchstoneTelemetry.OutcomeFailed);
                        TestAssert.Equal(TouchstoneTelemetry.EventTestCompleted, failedEvent.Tag(TouchstoneTelemetry.LabelEvent), "failed sink event");
                        CapturedMeasurement error = capture.Measurements(TouchstoneTelemetry.Errors).Single();
                        TestAssert.Equal(TouchstoneTelemetry.StageSink, error.Tag(TouchstoneTelemetry.LabelStage), "error stage");
                        TestAssert.Equal(typeof(IOException).FullName, error.Tag(TouchstoneTelemetry.LabelErrorType), "error.type");
                        TestAssert.Equal(TouchstoneTelemetry.OutcomeError, capture.Measurements(TouchstoneTelemetry.Suites).Single().Tag(TouchstoneTelemetry.LabelOutcome), "suite outcome");

                        Activity sinkSpan = capture.Span(TouchstoneTelemetry.SinkSpanPrefix + TouchstoneTelemetry.EventTestCompleted);
                        TestAssert.Equal(ActivityStatusCode.Error, sinkSpan.Status, "sink span status");
                        TestAssert.Equal(ActivityStatusCode.Error, capture.Span(TouchstoneTelemetry.SuiteSpanPrefix + id).Status, "suite span status");
                    }
                }),

                Guarded(S, "ExecuteCaseAsync", "ExecuteCaseAsync emits a case span and metrics, rethrows the original exception, and skips skipped cases", async ct =>
                {
                    string id = UniqueSuiteId();
                    InvalidOperationException thrown = new InvalidOperationException("direct failure");
                    TestCaseDescriptor failing = new TestCaseDescriptor(id, "Bad", "Bad", _ => throw thrown);

                    using (TelemetryCapture capture = new TelemetryCapture())
                    {
                        await TestExecutor.ExecuteCaseAsync(Fixtures.Passing(id, "Good"), ct).ConfigureAwait(false);
                        InvalidOperationException caught = await TestAssert.ThrowsAsync<InvalidOperationException>(
                            () => TestExecutor.ExecuteCaseAsync(failing, ct), "failing case").ConfigureAwait(false);
                        TestAssert.Same(thrown, caught, "original exception rethrown");
                        await TestExecutor.ExecuteCaseAsync(Fixtures.Skipped(id, "Later"), ct).ConfigureAwait(false);

                        Activity good = capture.Span(TouchstoneTelemetry.CaseSpanPrefix + id + ".Good");
                        TestAssert.Equal(capture.Root.SpanId, good.ParentSpanId, "case span parent is the caller's activity");
                        TestAssert.Equal(ActivityStatusCode.Error, capture.Span(TouchstoneTelemetry.CaseSpanPrefix + id + ".Bad").Status, "failing status");

                        List<CapturedMeasurement> cases = capture.Measurements(TouchstoneTelemetry.Cases);
                        TestAssert.Equal(3, cases.Count, "case measurements");
                        TestAssert.Equal(1, cases.Count(c => c.Tag(TouchstoneTelemetry.LabelOutcome) == TouchstoneTelemetry.OutcomeSkipped), "skipped measurement");
                        TestAssert.Equal(0, capture.Measurements(TouchstoneTelemetry.Runs).Count, "no run measurement");
                    }

                    await TestAssert.ThrowsAsync<ArgumentNullException>(
                        () => TestExecutor.ExecuteCaseAsync(null!, ct), "null case").ConfigureAwait(false);
                }),

                Guarded(S, "ContextPropagation", "The case span is current inside the case body, so outbound calls inherit its W3C traceparent", async ct =>
                {
                    string id = UniqueSuiteId();
                    Activity? seen = null;
                    Dictionary<string, string> headers = new Dictionary<string, string>();
                    TestCaseDescriptor probe = new TestCaseDescriptor(id, "Probe", "Probe", async _ =>
                    {
                        await Task.Yield();
                        seen = Activity.Current;
                        DistributedContextPropagator.Current.Inject(seen, headers, (carrier, key, value) =>
                        {
                            if (carrier is Dictionary<string, string> map && value != null) map[key] = value;
                        });
                    });

                    using (TelemetryCapture capture = new TelemetryCapture())
                    {
                        await TestExecutor.RunAsync(new List<TestSuiteDescriptor> { Fixtures.Suite(id, probe) }, null, ct).ConfigureAwait(false);

                        Activity caseSpan = capture.Span(TouchstoneTelemetry.CaseSpanPrefix + id + ".Probe");
                        TestAssert.Same(caseSpan, seen, "Activity.Current inside the body is the case span");
                        TestAssert.Equal(ActivityIdFormat.W3C, caseSpan.IdFormat, "W3C id format");
                        TestAssert.True(headers.ContainsKey("traceparent"), "traceparent injected");
                        TestAssert.Contains(capture.TraceId.ToHexString(), headers["traceparent"], "traceparent trace id");
                        TestAssert.Contains(caseSpan.SpanId.ToHexString(), headers["traceparent"], "traceparent span id");
                    }
                }),

                Guarded(S, "AdaptersEmit", "xUnit, NUnit, and MSTest RunAllAsync emit a run span and case metrics", async ct =>
                {
                    List<Func<IReadOnlyList<TestSuiteDescriptor>, CancellationToken, Task>> runners = new List<Func<IReadOnlyList<TestSuiteDescriptor>, CancellationToken, Task>>
                    {
                        (suites, t) => new XunitFactHarness(suites).InvokeRunAllAsync(t),
                        (suites, t) => new NunitBaseHarness(suites).InvokeRunAllAsync(t),
                        (suites, t) => new MstestBaseHarness(suites).InvokeRunAllAsync(t),
                    };

                    foreach (Func<IReadOnlyList<TestSuiteDescriptor>, CancellationToken, Task> runAll in runners)
                    {
                        string id = UniqueSuiteId();
                        using (TelemetryCapture capture = new TelemetryCapture())
                        {
                            await TestAssert.ThrowsAsync<AggregateException>(
                                () => runAll(new List<TestSuiteDescriptor> { Fixtures.Suite(id, Fixtures.Passing(id, "A"), Fixtures.Failing(id, "B", "adapter failure")) }, ct),
                                "adapter run").ConfigureAwait(false);

                            Activity run = capture.Span(TouchstoneTelemetry.RunSpanName);
                            TestAssert.Equal(ActivityStatusCode.Error, run.Status, "run status");
                            TestAssert.Equal(2, capture.Measurements(TouchstoneTelemetry.Cases).Count, "case measurements");
                            TestAssert.Equal(1, capture.Measurements(TouchstoneTelemetry.Errors).Count, "error measurements");
                        }
                    }
                }),

                Guarded(S, "CliExport", "ConsoleRunner emits run and export telemetry, including a failed export with its error.type", async ct =>
                {
                    string id = UniqueSuiteId();
                    string path = Path.Combine(Path.GetTempPath(), "touchstone-telemetry-" + Guid.NewGuid().ToString("N") + ".json");
                    string badPath = Path.Combine(Path.GetTempPath(), "touchstone-missing-" + Guid.NewGuid().ToString("N"), "results.json");

                    try
                    {
                        using (TelemetryCapture capture = new TelemetryCapture())
                        {
                            int exit = await ConsoleRunner.RunAsync(
                                new List<TestSuiteDescriptor> { Fixtures.Suite(id, Fixtures.Passing(id, "A")) },
                                new ConsoleResultSink(new StringWriter(), useColor: false), path, ct).ConfigureAwait(false);
                            TestAssert.Equal(0, exit, "exit code");

                            TestAssert.Equal(1, capture.Measurements(TouchstoneTelemetry.Runs).Count, "run measurement");
                            CapturedMeasurement export = capture.Measurements(TouchstoneTelemetry.Exports).Single();
                            TestAssert.Equal("json", export.Tag(TouchstoneTelemetry.LabelFormat), "format");
                            TestAssert.Equal(TouchstoneTelemetry.OutcomeOk, export.Tag(TouchstoneTelemetry.LabelOutcome), "outcome");
                            TestAssert.Equal("s", capture.Measurements(TouchstoneTelemetry.ExportDuration).Single().Unit, "export duration unit");
                            Activity span = capture.Span(TouchstoneTelemetry.ExportSpanPrefix + "json");
                            TestAssert.Equal(ActivityStatusCode.Ok, span.Status, "export status");
                            TestAssert.Equal(1, span.GetTagItem(TouchstoneTelemetry.AttributeResultCount), "result count");
                        }

                        using (TelemetryCapture capture = new TelemetryCapture())
                        {
                            await TestAssert.ThrowsAsync<DirectoryNotFoundException>(
                                () => ConsoleRunner.RunAsync(
                                    new List<TestSuiteDescriptor> { Fixtures.Suite(id, Fixtures.Passing(id, "A")) },
                                    new ConsoleResultSink(new StringWriter(), useColor: false), badPath, ct),
                                "bad export path").ConfigureAwait(false);

                            CapturedMeasurement export = capture.Measurements(TouchstoneTelemetry.Exports).Single();
                            TestAssert.Equal(TouchstoneTelemetry.OutcomeFailed, export.Tag(TouchstoneTelemetry.LabelOutcome), "outcome");
                            TestAssert.Equal(typeof(DirectoryNotFoundException).FullName, export.Tag(TouchstoneTelemetry.LabelErrorType), "error.type");
                            TestAssert.Equal(ActivityStatusCode.Error, capture.Span(TouchstoneTelemetry.ExportSpanPrefix + "json").Status, "export status");
                        }
                    }
                    finally
                    {
                        if (File.Exists(path)) File.Delete(path);
                    }
                }),

                Guarded(S, "SuiteLabelDisabled", "SuiteLabelEnabled=false drops the suite label from metrics but keeps it on spans", async ct =>
                {
                    string id = UniqueSuiteId();
                    bool previous = TouchstoneTelemetry.SuiteLabelEnabled;
                    TouchstoneTelemetry.SuiteLabelEnabled = false;

                    try
                    {
                        using (TelemetryCapture capture = new TelemetryCapture())
                        {
                            await TestExecutor.RunSuiteAsync(Fixtures.Suite(id, Fixtures.Passing(id, "A")), null, ct).ConfigureAwait(false);

                            TestAssert.False(capture.Measurements(TouchstoneTelemetry.Cases).Single().Tags.ContainsKey(TouchstoneTelemetry.LabelSuite), "case metric has no suite label");
                            TestAssert.False(capture.Measurements(TouchstoneTelemetry.Suites).Single().Tags.ContainsKey(TouchstoneTelemetry.LabelSuite), "suite metric has no suite label");
                            TestAssert.Equal(id, capture.Span(TouchstoneTelemetry.SuiteSpanPrefix + id).GetTagItem(TouchstoneTelemetry.AttributeSuiteId), "span keeps suite id");
                        }
                    }
                    finally
                    {
                        TouchstoneTelemetry.SuiteLabelEnabled = previous;
                    }
                }),

                Guarded(S, "NoListener", "Execution without any subscribed listener behaves identically and does not throw", async ct =>
                {
                    Activity.Current = null;
                    string id = UniqueSuiteId();
                    TestRunSummary summary = await TestExecutor.RunAsync(new List<TestSuiteDescriptor>
                    {
                        Fixtures.Suite(id, Fixtures.Passing(id, "A"), Fixtures.Failing(id, "B", "b"), Fixtures.Skipped(id, "C")),
                    }, new RecordingSink(), ct).ConfigureAwait(false);

                    TestAssert.Equal(3, summary.Total, "Total");
                    TestAssert.Equal(1, summary.Passed, "Passed");
                    TestAssert.Equal(1, summary.Failed, "Failed");
                    TestAssert.Equal(1, summary.Skipped, "Skipped");
                    await TestExecutor.ExecuteCaseAsync(Fixtures.Passing(id, "D"), ct).ConfigureAwait(false);
                }),

                Guarded(S, "ListenerFailureIsolated", "A collector that throws while recording never changes test outcomes", async ct =>
                {
                    string id = UniqueSuiteId();
                    using (TelemetryCapture capture = new TelemetryCapture())
                    using (MeterListener faulty = new MeterListener())
                    using (ActivityListener faultyTraces = new ActivityListener())
                    {
                        ActivityTraceId trace = capture.TraceId;
                        faulty.InstrumentPublished = (instrument, listener) =>
                        {
                            if (instrument.Meter.Name == TouchstoneTelemetry.MeterName)
                                listener.EnableMeasurementEvents(instrument);
                        };
                        faulty.SetMeasurementEventCallback<double>((i, v, t, s) => ThrowInTrace(trace));
                        faulty.SetMeasurementEventCallback<long>((i, v, t, s) => ThrowInTrace(trace));
                        faulty.Start();

                        faultyTraces.ShouldListenTo = s => s.Name == TouchstoneTelemetry.ActivitySourceName;
                        faultyTraces.Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded;
                        faultyTraces.ActivityStopped = a =>
                        {
                            if (a.TraceId == trace) throw new InvalidOperationException("faulty trace exporter");
                        };
                        ActivitySource.AddActivityListener(faultyTraces);

                        RecordingSink sink = new RecordingSink();
                        TestRunSummary summary = await TestExecutor.RunAsync(new List<TestSuiteDescriptor>
                        {
                            Fixtures.Suite(id, Fixtures.Passing(id, "A"), Fixtures.Failing(id, "B", "b")),
                        }, sink, ct).ConfigureAwait(false);

                        TestAssert.Equal(1, summary.Passed, "Passed");
                        TestAssert.Equal(1, summary.Failed, "Failed");
                        TestAssert.Equal(2, sink.Results.Count, "sink still received every result");
                        TestAssert.Equal("b", sink.Results[1].Message, "failure message unchanged");
                    }
                }),
            });
        }

        private static TestCaseDescriptor Guarded(string suiteId, string caseId, string displayName, Func<CancellationToken, Task> body)
        {
            return CaseBuilder.Async(suiteId, caseId, displayName, async ct =>
            {
                await _Gate.WaitAsync(ct).ConfigureAwait(false);
                try
                {
                    await body(ct).ConfigureAwait(false);
                }
                finally
                {
                    _Gate.Release();
                }
            });
        }

        private static string UniqueSuiteId()
        {
            return "Tel" + Guid.NewGuid().ToString("N").Substring(0, 12);
        }

        private static TestSuiteDescriptor MixedSuite(string id)
        {
            return new TestSuiteDescriptor(id, id + " display",
                new List<TestCaseDescriptor>
                {
                    Fixtures.Passing(id, "Pass"),
                    Fixtures.Failing(id, "Fail", "case broke"),
                    Fixtures.Skipped(id, "Skip", "later"),
                },
                beforeSuiteAsync: _ => ValueTask.CompletedTask,
                afterSuiteAsync: _ => ValueTask.CompletedTask);
        }

        private static void AssertStageFailure(TelemetryCapture capture, string id, string stage, string suiteOutcome)
        {
            CapturedMeasurement stageMeasurement = capture.Measurements(TouchstoneTelemetry.Stages).Single(s => s.Tag(TouchstoneTelemetry.LabelStage) == stage);
            TestAssert.Equal(TouchstoneTelemetry.OutcomeFailed, stageMeasurement.Tag(TouchstoneTelemetry.LabelOutcome), stage + " outcome");
            TestAssert.Equal(id, stageMeasurement.Tag(TouchstoneTelemetry.LabelSuite), stage + " suite label");

            CapturedMeasurement error = capture.Measurements(TouchstoneTelemetry.Errors).Single(e => e.Tag(TouchstoneTelemetry.LabelStage) == stage);
            TestAssert.Equal(typeof(InvalidOperationException).FullName, error.Tag(TouchstoneTelemetry.LabelErrorType), stage + " error.type");

            TestAssert.Equal(suiteOutcome, capture.Measurements(TouchstoneTelemetry.Suites).Single().Tag(TouchstoneTelemetry.LabelOutcome), "suite outcome");
            TestAssert.Equal(suiteOutcome, capture.Measurements(TouchstoneTelemetry.Runs).Single().Tag(TouchstoneTelemetry.LabelOutcome), "run outcome");

            Activity stageSpan = capture.Span(TouchstoneTelemetry.StageSpanPrefix + stage);
            TestAssert.Equal(ActivityStatusCode.Error, stageSpan.Status, stage + " span status");
            TestAssert.True(stageSpan.Events.Any(e => e.Name == "exception"), stage + " exception event");
            TestAssert.Equal(ActivityStatusCode.Error, capture.Span(TouchstoneTelemetry.SuiteSpanPrefix + id).Status, "suite span status");
            TestAssert.Equal(ActivityStatusCode.Error, capture.Span(TouchstoneTelemetry.RunSpanName).Status, "run span status");
        }

        private static void ThrowInTrace(ActivityTraceId trace)
        {
            if (Activity.Current != null && Activity.Current.TraceId == trace)
                throw new InvalidOperationException("faulty metrics exporter");
        }
    }
}
