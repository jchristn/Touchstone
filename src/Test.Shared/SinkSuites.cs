namespace Test.Shared
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Reflection;
    using System.Threading;
    using System.Threading.Tasks;

    using Touchstone.Core;

    /// <summary>
    /// Catalog sections 11.14 and 11.15: the sink contract and the result collector.
    /// </summary>
    public static class SinkSuites
    {
        /// <summary>
        /// Every suite in this file.
        /// </summary>
        /// <returns>The suites.</returns>
        public static IEnumerable<TestSuiteDescriptor> All()
        {
            yield return SinkSuite();
            yield return CollectorSuite();
        }

        private static TestSuiteDescriptor SinkSuite()
        {
            const string S = "Sink";
            Func<TestSuiteDescriptor> mixed = () => Fixtures.Suite("K",
                Fixtures.Passing("K", "A"),
                Fixtures.Scenario("K", "Scn", (c, t) => Task.CompletedTask),
                Fixtures.Skipped("K", "Skip"));

            return new TestSuiteDescriptor(S, "Sink contract", new[]
            {
                Check.Async(S, "SINK-01", "A sink that implements no members compiles and runs", async ct =>
                {
                    TestRunSummary s = await TestExecutor.RunAsync(new[] { mixed() }, new EmptySink(), null, ct);
                    TestAssert.Equal(2, s.Passed, "passed");
                }),
                Check.Sync(S, "SINK-02", "No ITestResultSink member is abstract", () =>
                {
                    MethodInfo[] methods = typeof(ITestResultSink).GetMethods();
                    TestAssert.Equal(8, methods.Length, "member count");
                    foreach (MethodInfo method in methods)
                        TestAssert.False(method.IsAbstract, method.Name + " has no default implementation");
                }),
                Check.Async(S, "SINK-03", "Callbacks arrive in run, suite, case, step order", async ct =>
                {
                    RecordingSink sink = new RecordingSink();
                    await TestExecutor.RunAsync(new[] { mixed() }, sink, null, ct);
                    TestAssert.Equal(string.Join("|", new[]
                    {
                        "run_started:1", "suite_started:K",
                        "case_started:K.A", "case_completed:K.A=Passed",
                        "case_started:K.Scn", "step_started:K.Scn/act", "step_completed:K.Scn/act=Passed", "case_completed:K.Scn=Passed",
                        "case_completed:K.Skip=Skipped",
                        "suite_completed:K", "run_completed:run",
                    }), string.Join("|", sink.Events), "events");
                }),
                Check.Async(S, "SINK-04", "OnRunCompletedAsync receives the summary RunAsync returns", async ct =>
                {
                    RecordingSink sink = new RecordingSink();
                    TestRunSummary s = await TestExecutor.RunAsync(new[] { mixed() }, sink, null, ct);
                    TestAssert.Same(s, sink.RunSummary, "same summary");
                }),
                Check.Async(S, "SINK-05", "OnCaseStartedAsync is not called for skipped, filtered, or not-run cases", async ct =>
                {
                    using (CancellationTokenSource source = new CancellationTokenSource())
                    {
                        RecordingSink sink = new RecordingSink();
                        TestRunOptions options = new TestRunOptions();
                        options.Filter.ExcludeTags.Add("off");
                        TestSuiteDescriptor suite = Fixtures.Suite("K", Fixtures.Skipped("K", "Skip"), Fixtures.Passing("K", "Off", null, new[] { "off" }),
                            Fixtures.CancelsRun("K", "Cancel", source), Fixtures.Passing("K", "Later"));
                        await TestExecutor.RunSuiteAsync(suite, sink, options, source.Token);
                        TestAssert.Equal("case_started:K.Cancel", string.Join("|", sink.Events.Where(e => e.StartsWith("case_started", StringComparison.Ordinal))), "starts");
                    }
                }),
                Check.Async(S, "SINK-06", "A throwing callback aborts the run and propagates", async ct =>
                {
                    string[] events =
                    {
                        TouchstoneTelemetry.EventRunStarted, TouchstoneTelemetry.EventSuiteStarted, TouchstoneTelemetry.EventCaseStarted,
                        "step_started", "step_completed", TouchstoneTelemetry.EventCaseCompleted, TouchstoneTelemetry.EventSuiteCompleted,
                        TouchstoneTelemetry.EventRunCompleted,
                    };
                    foreach (string e in events)
                    {
                        InvalidOperationException ex = await TestAssert.ThrowsAsync<InvalidOperationException>(
                            () => TestExecutor.RunAsync(new[] { mixed() }, new RecordingSink(e), null, ct), e);
                        TestAssert.Contains(e, ex.Message, "original exception for " + e);
                    }
                }),
                Check.Async(S, "SINK-07", "Completion callbacks receive a token that is not canceled", async ct =>
                {
                    RecordingSink sink = new RecordingSink();
                    using (CancellationTokenSource source = new CancellationTokenSource())
                    {
                        await TestExecutor.RunAsync(new[] { mixed() }, sink, null, source.Token);
                        foreach (string e in new[] { "step_completed", TouchstoneTelemetry.EventCaseCompleted, TouchstoneTelemetry.EventSuiteCompleted, TouchstoneTelemetry.EventRunCompleted })
                            TestAssert.False(sink.Tokens[e] == source.Token, e + " receives a separate token");
                    }
                }),
                Check.Async(S, "SINK-08", "RunSuiteAsync does not call run-level callbacks", async ct =>
                {
                    RecordingSink sink = new RecordingSink();
                    await TestExecutor.RunSuiteAsync(mixed(), sink, null, ct);
                    TestAssert.False(sink.Events.Any(e => e.StartsWith("run_", StringComparison.Ordinal)), "run callbacks");
                }),
            });
        }

        private static TestSuiteDescriptor CollectorSuite()
        {
            const string S = "Collector";
            return new TestSuiteDescriptor(S, "Result collector", new[]
            {
                Check.Async(S, "COL-01", "Collects every result in completion order", async ct =>
                {
                    TestResultCollector c = new TestResultCollector();
                    await TestExecutor.RunSuiteAsync(Fixtures.Suite("C", Fixtures.Passing("C", "A"), Fixtures.Failing("C", "B"), Fixtures.Skipped("C", "D")), c, null, ct);
                    TestAssert.Equal("A,B,D", string.Join(",", c.Results.Select(r => r.CaseId)), "order");
                }),
                Check.Async(S, "COL-02", "Failures lists only failed and canceled results", async ct =>
                {
                    using (CancellationTokenSource source = new CancellationTokenSource())
                    {
                        TestResultCollector c = new TestResultCollector();
                        await TestExecutor.RunSuiteAsync(Fixtures.Suite("C", Fixtures.Passing("C", "A"), Fixtures.Failing("C", "B"), Fixtures.Skipped("C", "D"),
                            Fixtures.CancelsRun("C", "E", source), Fixtures.Passing("C", "F")), c, null, source.Token);
                        TestAssert.Equal("B,E", string.Join(",", c.Failures.Select(r => r.CaseId)), "failures");
                    }
                }),
                Check.Async(S, "COL-03", "Repeated access to Failures reuses the computed list", async ct =>
                {
                    TestResultCollector c = new TestResultCollector();
                    await TestExecutor.RunSuiteAsync(Fixtures.Suite("C", Fixtures.Failing("C", "A")), c, null, ct);
                    TestAssert.Same(c.Failures, c.Failures, "same instance");
                    IReadOnlyList<TestResult> before = c.Failures;
                    await c.OnCaseCompletedAsync(new TestResult("C", "Z", "N", TestOutcome.Failed, TimeSpan.Zero), ct);
                    TestAssert.Equal(before.Count + 1, c.Failures.Count, "recomputed after change");
                }),
                Check.Async(S, "COL-04", "Summary is null until a run completes", async ct =>
                {
                    TestResultCollector c = new TestResultCollector();
                    TestAssert.Null(c.Summary, "before");
                    await TestExecutor.RunSuiteAsync(Fixtures.Suite("C", Fixtures.Passing("C", "A")), c, null, ct);
                    TestAssert.Null(c.Summary, "after a suite run");
                    TestRunSummary s = await TestExecutor.RunAsync(new[] { Fixtures.Suite("C", Fixtures.Passing("C", "A")) }, c, null, ct);
                    TestAssert.Same(s, c.Summary, "after a run");
                }),
                Check.Async(S, "COL-05", "Rejects a null result", async ct =>
                {
                    await TestAssert.ThrowsAsync<ArgumentNullException>(() => new TestResultCollector().OnCaseCompletedAsync(null!, ct).AsTask(), "null");
                }),
            });
        }

        /// <summary>
        /// A sink that relies entirely on the default interface members.
        /// </summary>
        private sealed class EmptySink : ITestResultSink
        {
        }
    }
}
