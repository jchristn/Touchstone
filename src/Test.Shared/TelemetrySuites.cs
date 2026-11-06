namespace Test.Shared
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Diagnostics.Metrics;
    using System.IO;
    using System.Linq;
    using System.Reflection;
    using System.Threading;
    using System.Threading.Tasks;

    using Touchstone.Cli;
    using Touchstone.Core;

    /// <summary>
    /// Catalog section 11.21: telemetry. Cases run one at a time because some change process-wide settings.
    /// </summary>
    public static class TelemetrySuites
    {
        private static readonly SemaphoreSlim _Gate = new SemaphoreSlim(1, 1);

        /// <summary>
        /// Every suite in this file.
        /// </summary>
        /// <returns>The suites.</returns>
        public static IEnumerable<TestSuiteDescriptor> All()
        {
            yield return TelemetrySuite();
        }

        /// <summary>
        /// Wrap a telemetry case so cases never overlap, even under hosts that run tests in parallel.
        /// </summary>
        /// <param name="suiteId">Suite identity.</param>
        /// <param name="catalogId">Catalog ID.</param>
        /// <param name="name">What the case verifies.</param>
        /// <param name="body">Case body.</param>
        /// <returns>The descriptor.</returns>
        public static TestCaseDescriptor Guarded(string suiteId, string catalogId, string name, Func<CancellationToken, Task> body)
        {
            return Check.Async(suiteId, catalogId, name, async ct =>
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
            return Fixtures.HookedSuite(id, _ => default, _ => default,
                Fixtures.Passing(id, "Pass", null, new[] { "fast" }),
                Fixtures.Failing(id, "Fail", "case broke"),
                Fixtures.Skipped(id, "Skip", null, "later"),
                new ScenarioBuilder<ScenarioContext>(id, "Scn", "Scenario", _ => Task.FromResult(new ScenarioContext()))
                    .Given("secret-step", "secret step name", (_, _) => Task.CompletedTask)
                    .Build());
        }

        private static TestSuiteDescriptor TelemetrySuite()
        {
            const string S = "Telemetry";
            return new TestSuiteDescriptor(S, "Telemetry", new[]
            {
                Guarded(S, "TEL-01", "Spans nest in one trace with fixed names", async ct =>
                {
                    string id = UniqueSuiteId();
                    using (TelemetryCapture capture = new TelemetryCapture())
                    {
                        await TestExecutor.RunAsync(new[] { MixedSuite(id) }, new RecordingSink(), null, ct);

                        Activity run = capture.Span(TouchstoneTelemetry.RunSpanName);
                        Activity suite = capture.Span(TouchstoneTelemetry.SuiteSpanName);
                        Activity before = capture.Span(TouchstoneTelemetry.StageSpanName, TouchstoneTelemetry.LabelStage, TouchstoneTelemetry.StageBeforeSuite);
                        Activity after = capture.Span(TouchstoneTelemetry.StageSpanName, TouchstoneTelemetry.LabelStage, TouchstoneTelemetry.StageAfterSuite);
                        Activity pass = capture.CaseSpan(id + ".Pass");
                        Activity fail = capture.CaseSpan(id + ".Fail");
                        Activity skip = capture.CaseSpan(id + ".Skip");
                        Activity scenario = capture.CaseSpan(id + ".Scn");
                        Activity step = capture.Span(TouchstoneTelemetry.ScenarioStepSpanName);

                        TestAssert.Equal(capture.Root.SpanId, run.ParentSpanId, "run parent");
                        TestAssert.Equal(run.SpanId, suite.ParentSpanId, "suite parent");
                        foreach (Activity child in new[] { before, after, pass, fail, skip, scenario })
                            TestAssert.Equal(suite.SpanId, child.ParentSpanId, child.DisplayName + " parent");
                        TestAssert.Equal(scenario.SpanId, step.ParentSpanId, "step parent is the case");
                        TestAssert.Equal(scenario.SpanId, capture.Span(TouchstoneTelemetry.ScenarioContextSpanName).ParentSpanId, "context parent");
                        TestAssert.Equal(scenario.SpanId, capture.Span(TouchstoneTelemetry.ScenarioCleanupSpanName).ParentSpanId, "cleanup parent");

                        TestAssert.Equal(ActivityStatusCode.Error, run.Status, "run status");
                        TestAssert.Equal(TouchstoneTelemetry.OutcomeFailed, run.GetTagItem(TouchstoneTelemetry.AttributeOutcome), "run outcome");
                        TestAssert.Equal(4, run.GetTagItem(TouchstoneTelemetry.AttributeCaseCount), "case count");
                        TestAssert.Equal(1, run.GetTagItem(TouchstoneTelemetry.AttributeFailedCount), "failed count");
                        TestAssert.Equal(2, run.GetTagItem(TouchstoneTelemetry.AttributePassedCount), "passed count");
                        TestAssert.Equal(0, run.GetTagItem(TouchstoneTelemetry.AttributeNotRunCount), "not run count");
                        TestAssert.Equal(id, suite.GetTagItem(TouchstoneTelemetry.AttributeSuiteId), "suite id");
                        TestAssert.Equal(ActivityStatusCode.Ok, before.Status, "before status");
                        TestAssert.Equal(ActivityStatusCode.Ok, pass.Status, "pass status");
                        TestAssert.Equal("Pass", pass.GetTagItem(TouchstoneTelemetry.AttributeCaseId), "case id");
                        TestAssert.Equal("fast", ((string[])pass.GetTagItem(TouchstoneTelemetry.AttributeCaseTags)!)[0], "tags");
                        TestAssert.Equal(ActivityStatusCode.Error, fail.Status, "fail status");
                        TestAssert.Equal("case broke", fail.StatusDescription, "fail description");
                        TestAssert.Equal(typeof(InvalidOperationException).FullName, fail.GetTagItem(TouchstoneTelemetry.LabelErrorType), "error.type");
                        TestAssert.True(fail.Events.Any(e => e.Name == "exception"), "exception event");
                        TestAssert.Equal(TouchstoneTelemetry.OutcomeSkipped, skip.GetTagItem(TouchstoneTelemetry.AttributeOutcome), "skip outcome");
                        TestAssert.Equal("later", skip.GetTagItem(TouchstoneTelemetry.AttributeSkipReason), "skip reason");
                        TestAssert.Equal("given", step.GetTagItem(TouchstoneTelemetry.AttributeStepKind), "step kind");

                        List<Activity> sinkSpans = capture.Spans().Where(a => a.DisplayName == TouchstoneTelemetry.SinkSpanName).ToList();
                        TestAssert.Equal(11, sinkSpans.Count, "sink spans: run start, suite start, 3 case starts, 4 case completions, suite end, run end");
                    }
                }),
                Guarded(S, "TEL-02", "Identities appear only as attributes, never in span names", async ct =>
                {
                    string id = UniqueSuiteId();
                    using (TelemetryCapture capture = new TelemetryCapture())
                    {
                        await TestExecutor.RunAsync(new[] { MixedSuite(id) }, new RecordingSink(), null, ct);
                        HashSet<string> allowed = new HashSet<string>
                        {
                            TouchstoneTelemetry.RunSpanName, TouchstoneTelemetry.SuiteSpanName, TouchstoneTelemetry.StageSpanName, TouchstoneTelemetry.CaseSpanName,
                            TouchstoneTelemetry.SinkSpanName, TouchstoneTelemetry.ExportSpanName, TouchstoneTelemetry.ScenarioContextSpanName,
                            TouchstoneTelemetry.ScenarioStepSpanName, TouchstoneTelemetry.ScenarioCleanupSpanName,
                        };
                        foreach (Activity span in capture.Spans())
                        {
                            TestAssert.True(allowed.Contains(span.DisplayName), "fixed span name: " + span.DisplayName);
                            TestAssert.DoesNotContain(id, span.DisplayName, "suite id in span name");
                        }
                    }
                }),
                Guarded(S, "TEL-03", "Metric labels are bounded: no test identity, case identity, name, or tag", async ct =>
                {
                    string id = UniqueSuiteId();
                    using (TelemetryCapture capture = new TelemetryCapture())
                    {
                        await TestExecutor.RunAsync(new[] { MixedSuite(id) }, new RecordingSink(), null, ct);
                        HashSet<string> labels = new HashSet<string>
                        {
                            TouchstoneTelemetry.LabelSuite, TouchstoneTelemetry.LabelOutcome, TouchstoneTelemetry.LabelStage, TouchstoneTelemetry.LabelEvent,
                            TouchstoneTelemetry.LabelErrorType, TouchstoneTelemetry.LabelFormat, TouchstoneTelemetry.LabelVersion,
                            TouchstoneTelemetry.LabelOperation, TouchstoneTelemetry.LabelKind,
                        };
                        foreach (string instrument in new[] { TouchstoneTelemetry.Cases, TouchstoneTelemetry.CaseDuration, TouchstoneTelemetry.Suites, TouchstoneTelemetry.Runs,
                            TouchstoneTelemetry.Stages, TouchstoneTelemetry.SinkEvents, TouchstoneTelemetry.Errors, TouchstoneTelemetry.ScenarioOperations })
                        {
                            foreach (CapturedMeasurement m in capture.Measurements(instrument))
                            {
                                foreach (KeyValuePair<string, object?> tag in m.Tags)
                                {
                                    TestAssert.True(labels.Contains(tag.Key), instrument + " label " + tag.Key);
                                    string value = tag.Value?.ToString() ?? string.Empty;
                                    TestAssert.False(value.Contains(".Pass", StringComparison.Ordinal) || value.Contains("secret", StringComparison.Ordinal) || value == "fast",
                                        instrument + " value " + value);
                                }
                            }
                        }

                        TestAssert.True(capture.Measurements(TouchstoneTelemetry.Cases).Count >= 4, "case metrics recorded");
                        TestAssert.Equal("s", capture.Measurements(TouchstoneTelemetry.CaseDuration)[0].Unit, "duration unit");
                        TestAssert.Equal(0.0, capture.Measurements(TouchstoneTelemetry.CasesActive).Sum(a => a.Value), "active returns to zero");
                    }
                }),
                Guarded(S, "TEL-04", "A passing run records passed and updates the last-success gauges", async ct =>
                {
                    string id = UniqueSuiteId();
                    using (TelemetryCapture capture = new TelemetryCapture())
                    {
                        double before = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() / 1000.0 - 1;
                        await TestExecutor.RunAsync(new[] { Fixtures.Suite(id, Fixtures.Passing(id, "A")) }, null, null, ct);
                        TestAssert.Equal(TouchstoneTelemetry.OutcomePassed, capture.Measurements(TouchstoneTelemetry.Runs).Single().Tag(TouchstoneTelemetry.LabelOutcome), "run outcome");
                        TestAssert.Equal(ActivityStatusCode.Ok, capture.Span(TouchstoneTelemetry.RunSpanName).Status, "run status");
                        TestAssert.Equal(0, capture.Spans().Count(a => a.DisplayName == TouchstoneTelemetry.SinkSpanName), "no sink spans without a sink");
                        TestAssert.True(capture.Observe(TouchstoneTelemetry.SuiteLastSuccessTime).Last(m => m.Tag(TouchstoneTelemetry.LabelSuite) == id).Value >= before, "suite gauge");
                        TestAssert.True(capture.Observe(TouchstoneTelemetry.RunLastSuccessTime).Last().Value >= before, "run gauge");
                    }
                }),
                Guarded(S, "TEL-05", "touchstone.build.info reports 1 with the version label", async ct =>
                {
                    using (TelemetryCapture capture = new TelemetryCapture())
                    {
                        await TestExecutor.RunSuiteAsync(Fixtures.Suite(UniqueSuiteId()), null, null, ct);
                        CapturedMeasurement info = capture.Observe(TouchstoneTelemetry.BuildInfo).Last();
                        TestAssert.Equal(1.0, info.Value, "value");
                        string? version = info.Tag(TouchstoneTelemetry.LabelVersion);
                        TestAssert.True(!string.IsNullOrEmpty(version) && version != "unknown", "version: " + version);
                    }
                }),
                Guarded(S, "TEL-06", "A before_suite failure records a failed stage, an error, and outcome error", async ct =>
                {
                    string id = UniqueSuiteId();
                    using (TelemetryCapture capture = new TelemetryCapture())
                    {
                        await TestExecutor.RunAsync(new[] { Fixtures.HookedSuite(id, _ => throw new InvalidOperationException("setup failed"), null, Fixtures.Passing(id, "A")) }, null, null, ct);
                        AssertStageFailure(capture, TouchstoneTelemetry.StageBeforeSuite);
                    }
                }),
                Guarded(S, "TEL-07", "An after_suite failure records a failed stage and outcome error", async ct =>
                {
                    string id = UniqueSuiteId();
                    using (TelemetryCapture capture = new TelemetryCapture())
                    {
                        await TestExecutor.RunAsync(new[] { Fixtures.HookedSuite(id, null, _ => throw new InvalidOperationException("teardown failed"), Fixtures.Passing(id, "A")) }, null, null, ct);
                        AssertStageFailure(capture, TouchstoneTelemetry.StageAfterSuite);
                        TestAssert.Equal(1, capture.Measurements(TouchstoneTelemetry.Cases).Count, "case ran");
                    }
                }),
                Guarded(S, "TEL-08", "A canceled run records canceled at run, suite, and case level", async ct =>
                {
                    string id = UniqueSuiteId();
                    using (TelemetryCapture capture = new TelemetryCapture())
                    using (CancellationTokenSource source = new CancellationTokenSource())
                    {
                        await TestExecutor.RunAsync(new[] { Fixtures.Suite(id, Fixtures.CancelsRun(id, "A", source)) }, null, null, source.Token);
                        TestAssert.Equal("canceled", capture.Measurements(TouchstoneTelemetry.Runs).Single().Tag(TouchstoneTelemetry.LabelOutcome), "run");
                        TestAssert.Equal("canceled", capture.Measurements(TouchstoneTelemetry.Suites).Single().Tag(TouchstoneTelemetry.LabelOutcome), "suite");
                        TestAssert.Equal("canceled", capture.Measurements(TouchstoneTelemetry.Cases).Single().Tag(TouchstoneTelemetry.LabelOutcome), "case");
                        TestAssert.Equal("canceled", capture.CaseSpan(id + ".A").GetTagItem(TouchstoneTelemetry.AttributeOutcome), "case span");
                    }
                }),
                Guarded(S, "TEL-09", "A throwing sink records a failed sink event and a sink error", async ct =>
                {
                    string id = UniqueSuiteId();
                    using (TelemetryCapture capture = new TelemetryCapture())
                    {
                        await TestAssert.ThrowsAsync<InvalidOperationException>(
                            () => TestExecutor.RunSuiteAsync(Fixtures.Suite(id, Fixtures.Passing(id, "A")), new RecordingSink(TouchstoneTelemetry.EventCaseCompleted), null, ct), "sink");
                        CapturedMeasurement failed = capture.Measurements(TouchstoneTelemetry.SinkEvents).Single(e => e.Tag(TouchstoneTelemetry.LabelOutcome) == TouchstoneTelemetry.OutcomeFailed);
                        TestAssert.Equal(TouchstoneTelemetry.EventCaseCompleted, failed.Tag(TouchstoneTelemetry.LabelEvent), "event");
                        CapturedMeasurement error = capture.Measurements(TouchstoneTelemetry.Errors).Single();
                        TestAssert.Equal(TouchstoneTelemetry.StageSink, error.Tag(TouchstoneTelemetry.LabelStage), "stage");
                        TestAssert.Equal(TouchstoneTelemetry.OutcomeError, capture.Measurements(TouchstoneTelemetry.Suites).Single().Tag(TouchstoneTelemetry.LabelOutcome), "suite outcome");
                        TestAssert.Equal(ActivityStatusCode.Error, capture.Span(TouchstoneTelemetry.SinkSpanName, TouchstoneTelemetry.LabelEvent, TouchstoneTelemetry.EventCaseCompleted).Status, "sink span");
                    }
                }),
                Guarded(S, "TEL-10", "ExecuteCaseAsync emits a case span and metrics", async ct =>
                {
                    string id = UniqueSuiteId();
                    using (TelemetryCapture capture = new TelemetryCapture())
                    {
                        await TestExecutor.ExecuteCaseAsync(Fixtures.Passing(id, "Good"), null, ct);
                        await TestAssert.ThrowsAsync<InvalidOperationException>(() => TestExecutor.ExecuteCaseAsync(Fixtures.Failing(id, "Bad"), null, ct), "failing");
                        await TestExecutor.ExecuteCaseAsync(Fixtures.Skipped(id, "Later"), null, ct);
                        TestAssert.Equal(capture.Root.SpanId, capture.CaseSpan(id + ".Good").ParentSpanId, "parent is caller");
                        TestAssert.Equal(ActivityStatusCode.Error, capture.CaseSpan(id + ".Bad").Status, "failing status");
                        TestAssert.Equal(3, capture.Measurements(TouchstoneTelemetry.Cases).Count, "case measurements");
                        TestAssert.Equal(0, capture.Measurements(TouchstoneTelemetry.Runs).Count, "no run measurement");
                    }
                }),
                Guarded(S, "TEL-11", "The case span is current inside the body, so outbound calls inherit its traceparent", async ct =>
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
                        await TestExecutor.RunAsync(new[] { Fixtures.Suite(id, probe) }, null, null, ct);
                        Activity span = capture.CaseSpan(id + ".Probe");
                        TestAssert.Same(span, seen, "current activity");
                        TestAssert.Equal(ActivityIdFormat.W3C, span.IdFormat, "W3C");
                        TestAssert.Contains(span.SpanId.ToHexString(), headers["traceparent"], "traceparent span id");
                    }
                }),
                Guarded(S, "TEL-13", "Report exports emit telemetry, including a failed export with error.type", async ct =>
                {
                    string id = UniqueSuiteId();
                    string path = Path.Combine(Path.GetTempPath(), "touchstone-telemetry-" + Guid.NewGuid().ToString("N") + ".json");
                    string badPath = Path.Combine(Path.GetTempPath(), "touchstone-missing-" + Guid.NewGuid().ToString("N"), "results.json");
                    try
                    {
                        using (TelemetryCapture capture = new TelemetryCapture())
                        {
                            await CliSuites.RunToText(new[] { Fixtures.Suite(id, Fixtures.Passing(id, "A")) }, o => { o.JsonResultsPath = path; o.HtmlReportPath = path + ".html"; }, ct);
                            List<CapturedMeasurement> exports = capture.Measurements(TouchstoneTelemetry.Exports);
                            TestAssert.Equal("html,json", string.Join(",", exports.Select(e => e.Tag(TouchstoneTelemetry.LabelFormat)).OrderBy(f => f, StringComparer.Ordinal)), "formats");
                            TestAssert.True(exports.All(e => e.Tag(TouchstoneTelemetry.LabelOutcome) == TouchstoneTelemetry.OutcomeOk), "outcomes");
                            Activity span = capture.Span(TouchstoneTelemetry.ExportSpanName, TouchstoneTelemetry.LabelFormat, "json");
                            TestAssert.Equal(1, span.GetTagItem(TouchstoneTelemetry.AttributeResultCount), "result count");
                        }

                        using (TelemetryCapture capture = new TelemetryCapture())
                        {
                            await TestAssert.ThrowsAsync<DirectoryNotFoundException>(
                                () => CliSuites.RunToText(new[] { Fixtures.Suite(id, Fixtures.Passing(id, "A")) }, o => o.JsonResultsPath = badPath, ct), "bad path");
                            CapturedMeasurement export = capture.Measurements(TouchstoneTelemetry.Exports).Single();
                            TestAssert.Equal(TouchstoneTelemetry.OutcomeFailed, export.Tag(TouchstoneTelemetry.LabelOutcome), "outcome");
                            TestAssert.Equal(typeof(DirectoryNotFoundException).FullName, export.Tag(TouchstoneTelemetry.LabelErrorType), "error.type");
                        }
                    }
                    finally
                    {
                        File.Delete(path);
                        File.Delete(path + ".html");
                    }
                }),
                Guarded(S, "TEL-14", "SuiteLabelEnabled=false drops the suite label from metrics but keeps it on spans", async ct =>
                {
                    string id = UniqueSuiteId();
                    bool previous = TouchstoneTelemetry.SuiteLabelEnabled;
                    TouchstoneTelemetry.SuiteLabelEnabled = false;
                    try
                    {
                        using (TelemetryCapture capture = new TelemetryCapture())
                        {
                            await TestExecutor.RunSuiteAsync(Fixtures.Suite(id, Fixtures.Passing(id, "A")), null, null, ct);
                            TestAssert.False(capture.Measurements(TouchstoneTelemetry.Cases).Single().Tags.ContainsKey(TouchstoneTelemetry.LabelSuite), "case metric");
                            TestAssert.False(capture.Measurements(TouchstoneTelemetry.Suites).Single().Tags.ContainsKey(TouchstoneTelemetry.LabelSuite), "suite metric");
                            TestAssert.Equal(id, capture.Span(TouchstoneTelemetry.SuiteSpanName).GetTagItem(TouchstoneTelemetry.AttributeSuiteId), "span");
                        }
                    }
                    finally
                    {
                        TouchstoneTelemetry.SuiteLabelEnabled = previous;
                    }
                }),
                Guarded(S, "TEL-15", "Execution with no listener behaves identically", async ct =>
                {
                    Activity.Current = null;
                    string id = UniqueSuiteId();
                    TestRunSummary summary = await TestExecutor.RunAsync(new[] { MixedSuite(id) }, new RecordingSink(), null, ct);
                    TestAssert.Equal(4, summary.Total, "total");
                    TestAssert.Equal(2, summary.Passed, "passed");
                    TestAssert.Equal(1, summary.Failed, "failed");
                    TestAssert.Equal(1, summary.Skipped, "skipped");
                }),
                Guarded(S, "TEL-16", "Throwing metric and trace listeners never change outcomes", async ct =>
                {
                    string id = UniqueSuiteId();
                    int injected = 0;
                    using (TelemetryCapture capture = new TelemetryCapture())
                    using (MeterListener faulty = new MeterListener())
                    using (ActivityListener faultyTraces = new ActivityListener())
                    {
                        ActivityTraceId trace = capture.TraceId;
                        faulty.InstrumentPublished = (instrument, listener) =>
                        {
                            if (instrument.Meter.Name == TouchstoneTelemetry.MeterName) listener.EnableMeasurementEvents(instrument);
                        };
                        faulty.SetMeasurementEventCallback<double>((i, v, t, s) => { if (Activity.Current?.TraceId == trace) { injected++; throw new InvalidOperationException("faulty metrics"); } });
                        faulty.SetMeasurementEventCallback<long>((i, v, t, s) => { if (Activity.Current?.TraceId == trace) { injected++; throw new InvalidOperationException("faulty metrics"); } });
                        faulty.Start();
                        faultyTraces.ShouldListenTo = s => s.Name == TouchstoneTelemetry.ActivitySourceName;
                        faultyTraces.Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded;
                        faultyTraces.ActivityStopped = a => { if (a.TraceId == trace) throw new InvalidOperationException("faulty traces"); };
                        ActivitySource.AddActivityListener(faultyTraces);

                        RecordingSink sink = new RecordingSink();
                        TestRunSummary summary = await TestExecutor.RunAsync(new[] { MixedSuite(id) }, sink, null, ct);
                        TestAssert.Equal(2, summary.Passed, "passed");
                        TestAssert.Equal(1, summary.Failed, "failed");
                        TestAssert.Equal(4, sink.Results.Count, "every result delivered");
                        TestAssert.Equal("case broke", sink.Results[1].Message, "message unchanged");
                        TestAssert.True(injected > 0, "fault exercised");
                    }
                }),
                Guarded(S, "TEL-17", "Scenario operation metrics use lowercase outcomes and the public labels", async ct =>
                {
                    string id = UniqueSuiteId();
                    using (TelemetryCapture capture = new TelemetryCapture())
                    {
                        await TestExecutor.ExecuteCaseResultAsync(Fixtures.Scenario(id, "Scn", (c, t) => throw new InvalidOperationException("x")), null, null, ct);
                        List<CapturedMeasurement> ops = capture.Measurements(TouchstoneTelemetry.ScenarioOperations);
                        TestAssert.Equal("context:passed:none,step:failed:when,cleanup:passed:none",
                            string.Join(",", ops.Select(m => m.Tag(TouchstoneTelemetry.LabelOperation) + ":" + m.Tag(TouchstoneTelemetry.LabelOutcome) + ":" + m.Tag(TouchstoneTelemetry.LabelKind))), "operations");
                        TestAssert.Equal(3, capture.Measurements(TouchstoneTelemetry.ScenarioOperationDuration).Count, "durations");
                    }
                }),
                Guarded(S, "TEL-18", "Every public telemetry constant is emitted or documented as a value", async ct =>
                {
                    string id = UniqueSuiteId();
                    string path = Path.Combine(Path.GetTempPath(), "touchstone-tel18-" + Guid.NewGuid().ToString("N"));
                    HashSet<string> seen = new HashSet<string>(StringComparer.Ordinal);
                    try
                    {
                        using (TelemetryCapture capture = new TelemetryCapture())
                        using (CancellationTokenSource source = new CancellationTokenSource())
                        {
                            TestRunOptions options = new TestRunOptions();
                            options.Filter.ExcludeTags.Add("off");
                            await CliSuites.RunToText(new[]
                            {
                                MixedSuite(id),
                                Fixtures.HookedSuite(id + "b", _ => throw new InvalidOperationException("x"), null, Fixtures.Passing(id + "b", "A")),
                                Fixtures.Suite(id + "c", Fixtures.Passing(id + "c", "Off", null, new[] { "off" }), Fixtures.CancelsRun(id + "c", "Stop", source), Fixtures.Passing(id + "c", "Never")),
                            }, o => { o.Run = options; o.JsonResultsPath = path; o.HtmlReportPath = path + ".html"; o.AdditionalSinks.Add(new RecordingSink()); }, source.Token);

                            TestRunOptions timed = new TestRunOptions { CaseTimeout = TimeSpan.FromMilliseconds(50), MaxDegreeOfParallelism = 2 };
                            await TestExecutor.RunSuiteAsync(Fixtures.Suite(id + "d", new TestCaseDescriptor(id + "d", "Hang", "Hang", t => Task.Delay(Timeout.Infinite, t)), Fixtures.Passing(id + "d", "A")), null, timed, ct);

                            foreach (Activity span in capture.Spans())
                            {
                                seen.Add(span.DisplayName);
                                foreach (KeyValuePair<string, object?> tag in span.TagObjects)
                                {
                                    seen.Add(tag.Key);
                                    if (tag.Value is string text) seen.Add(text);
                                }
                            }

                            foreach (CapturedMeasurement m in capture.MeasurementsAll())
                            {
                                seen.Add(m.Instrument);
                                foreach (KeyValuePair<string, object?> tag in m.Tags)
                                {
                                    seen.Add(tag.Key);
                                    if (tag.Value is string text) seen.Add(text);
                                }
                            }

                            foreach (string observable in new[] { TouchstoneTelemetry.BuildInfo, TouchstoneTelemetry.SuiteLastSuccessTime, TouchstoneTelemetry.RunLastSuccessTime })
                            {
                                foreach (CapturedMeasurement m in capture.Observe(observable))
                                {
                                    seen.Add(observable);
                                    foreach (KeyValuePair<string, object?> tag in m.Tags) seen.Add(tag.Key);
                                }
                            }
                        }
                    }
                    finally
                    {
                        File.Delete(path);
                        File.Delete(path + ".html");
                    }

                    seen.Add(TouchstoneTelemetry.MeterName);
                    seen.Add(TouchstoneTelemetry.ActivitySourceName);
                    HashSet<string> notEmittedHere = new HashSet<string>(StringComparer.Ordinal)
                    {
                        TouchstoneTelemetry.StageRun,
                        TouchstoneTelemetry.StageSink,
                    };
                    foreach (FieldInfo field in typeof(TouchstoneTelemetry).GetFields(BindingFlags.Public | BindingFlags.Static).Where(f => f.IsLiteral))
                    {
                        string value = (string)field.GetRawConstantValue()!;
                        if (notEmittedHere.Contains(value)) continue;
                        TestAssert.True(seen.Contains(value), "TouchstoneTelemetry." + field.Name + " (" + value + ") was emitted");
                    }
                }),
                Guarded(S, "TEL-19", "Core uses one meter and one activity source", async ct =>
                {
                    List<string> meters = new List<string>();
                    using (MeterListener listener = new MeterListener())
                    {
                        listener.InstrumentPublished = (instrument, l) =>
                        {
                            if (instrument.Meter.Name == TouchstoneTelemetry.MeterName && instrument.Name.StartsWith("touchstone.", StringComparison.Ordinal) && !instrument.Name.StartsWith("touchstone.export", StringComparison.Ordinal))
                                meters.Add(instrument.Meter.GetHashCode().ToString(System.Globalization.CultureInfo.InvariantCulture));
                        };
                        listener.Start();
                        await TestExecutor.ExecuteCaseResultAsync(Fixtures.Scenario(UniqueSuiteId(), "Scn", (c, t) => Task.CompletedTask), null, null, ct);
                    }

                    TestAssert.True(meters.Count > 0, "instruments observed");
                    TestAssert.Equal(1, meters.Distinct().Count(), "distinct Core meters");
                }),
                Guarded(S, "TEL-20", "Filtered cases increment touchstone.cases.filtered; not-run cases record not_run", async ct =>
                {
                    string id = UniqueSuiteId();
                    using (TelemetryCapture capture = new TelemetryCapture())
                    {
                        TestRunOptions options = new TestRunOptions { StopOnFirstFailure = true };
                        options.Filter.ExcludeTags.Add("off");
                        await TestExecutor.RunSuiteAsync(Fixtures.Suite(id, Fixtures.Passing(id, "Off", null, new[] { "off" }), Fixtures.Failing(id, "Bad"), Fixtures.Passing(id, "Later")), null, options, ct);
                        TestAssert.Equal(1.0, capture.Measurements(TouchstoneTelemetry.CasesFiltered).Sum(m => m.Value), "filtered");
                        TestAssert.Equal(1, capture.Measurements(TouchstoneTelemetry.Cases).Count(m => m.Tag(TouchstoneTelemetry.LabelOutcome) == TouchstoneTelemetry.OutcomeNotRun), "not run");
                    }
                }),
                Guarded(S, "TEL-21", "Lifecycle errors increment touchstone.errors with the stage label", async ct =>
                {
                    string id = UniqueSuiteId();
                    using (TelemetryCapture capture = new TelemetryCapture())
                    {
                        await TestExecutor.RunSuiteAsync(Fixtures.HookedSuite(id, _ => throw new InvalidOperationException("a"), _ => throw new FormatException("b"), Fixtures.Passing(id, "A")), null, null, ct);
                        List<CapturedMeasurement> errors = capture.Measurements(TouchstoneTelemetry.Errors);
                        TestAssert.Equal(1, errors.Count(e => e.Tag(TouchstoneTelemetry.LabelStage) == TouchstoneTelemetry.StageBeforeSuite), "before");
                        TestAssert.Equal(typeof(FormatException).FullName, errors.Single(e => e.Tag(TouchstoneTelemetry.LabelStage) == TouchstoneTelemetry.StageAfterSuite).Tag(TouchstoneTelemetry.LabelErrorType), "after");
                    }
                }),
                Guarded(S, "TEL-23", "A timed-out case records touchstone.case.timeouts, a failed case, its error type, and the span attribute", async ct =>
                {
                    string id = UniqueSuiteId();
                    using (TelemetryCapture capture = new TelemetryCapture())
                    {
                        TestRunOptions options = new TestRunOptions { CaseTimeout = TimeSpan.FromMilliseconds(100) };
                        TestSuiteDescriptor suite = Fixtures.Suite(id, new TestCaseDescriptor(id, "Hang", "Hang", t => Task.Delay(Timeout.Infinite, t)), Fixtures.Passing(id, "A"));
                        await TestExecutor.RunSuiteAsync(suite, null, options, ct);
                        TestAssert.Equal(1, capture.Measurements(TouchstoneTelemetry.CaseTimeouts).Count, "timeout counter");
                        TestAssert.Equal(id, capture.Measurements(TouchstoneTelemetry.CaseTimeouts).Single().Tag(TouchstoneTelemetry.LabelSuite), "suite label");
                        TestAssert.Equal(1, capture.Measurements(TouchstoneTelemetry.Cases).Count(m => m.Tag(TouchstoneTelemetry.LabelOutcome) == TouchstoneTelemetry.OutcomeFailed), "failed case");
                        TestAssert.Equal(typeof(CaseTimeoutException).FullName, capture.Measurements(TouchstoneTelemetry.Errors).Single().Tag(TouchstoneTelemetry.LabelErrorType), "error.type");
                        TestAssert.Equal(true, capture.CaseSpan(id + ".Hang").GetTagItem(TouchstoneTelemetry.AttributeCaseTimedOut), "timed-out span");
                        TestAssert.Null(capture.CaseSpan(id + ".A").GetTagItem(TouchstoneTelemetry.AttributeCaseTimedOut), "passing span");
                    }
                }),
                Guarded(S, "TEL-24", "Parallel runs tag the degree of parallelism and keep every case span under its suite span", async ct =>
                {
                    string id = UniqueSuiteId();
                    using (TelemetryCapture capture = new TelemetryCapture())
                    {
                        TestRunOptions options = new TestRunOptions { MaxDegreeOfParallelism = 4 };
                        TestCaseDescriptor[] cases = Enumerable.Range(0, 8)
                            .Select(i => new TestCaseDescriptor(id, "C" + i, "C" + i, async t => await Task.Delay(10, t).ConfigureAwait(false)))
                            .ToArray();
                        await TestExecutor.RunAsync(new[] { Fixtures.Suite(id, cases) }, null, options, ct);
                        Activity run = capture.Span(TouchstoneTelemetry.RunSpanName);
                        Activity suite = capture.Span(TouchstoneTelemetry.SuiteSpanName);
                        TestAssert.Equal(4, run.GetTagItem(TouchstoneTelemetry.AttributeMaxDegreeOfParallelism), "run attribute");
                        TestAssert.Equal(4, suite.GetTagItem(TouchstoneTelemetry.AttributeMaxDegreeOfParallelism), "suite attribute");
                        for (int i = 0; i < 8; i++)
                            TestAssert.Equal(suite.SpanId, capture.CaseSpan(id + ".C" + i).ParentSpanId, "case " + i + " parent");
                        TestAssert.Equal(8, capture.Measurements(TouchstoneTelemetry.Cases).Count, "case metrics");
                        TestAssert.Equal(0.0, capture.Measurements(TouchstoneTelemetry.CasesActive).Sum(m => m.Value), "active cases return to zero");
                    }
                }),
            });
        }

        private static void AssertStageFailure(TelemetryCapture capture, string stage)
        {
            CapturedMeasurement stageMeasurement = capture.Measurements(TouchstoneTelemetry.Stages).Single(s => s.Tag(TouchstoneTelemetry.LabelStage) == stage);
            TestAssert.Equal(TouchstoneTelemetry.OutcomeFailed, stageMeasurement.Tag(TouchstoneTelemetry.LabelOutcome), stage + " outcome");
            CapturedMeasurement error = capture.Measurements(TouchstoneTelemetry.Errors).Single(e => e.Tag(TouchstoneTelemetry.LabelStage) == stage);
            TestAssert.Equal(typeof(InvalidOperationException).FullName, error.Tag(TouchstoneTelemetry.LabelErrorType), stage + " error.type");
            TestAssert.Equal(TouchstoneTelemetry.OutcomeError, capture.Measurements(TouchstoneTelemetry.Suites).Single().Tag(TouchstoneTelemetry.LabelOutcome), "suite outcome");
            TestAssert.Equal(TouchstoneTelemetry.OutcomeError, capture.Measurements(TouchstoneTelemetry.Runs).Single().Tag(TouchstoneTelemetry.LabelOutcome), "run outcome");
            Activity span = capture.Span(TouchstoneTelemetry.StageSpanName, TouchstoneTelemetry.LabelStage, stage);
            TestAssert.Equal(ActivityStatusCode.Error, span.Status, stage + " span status");
            TestAssert.True(span.Events.Any(e => e.Name == "exception"), stage + " exception event");
            TestAssert.Equal(ActivityStatusCode.Error, capture.Span(TouchstoneTelemetry.RunSpanName).Status, "run span status");
        }
    }
}
