namespace Test.Shared
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;

    using Touchstone.Core;

    /// <summary>
    /// Catalog sections 11.10 to 11.12: cancellation, suite lifecycle, and stop on first failure.
    /// </summary>
    public static class CancellationSuites
    {
        /// <summary>
        /// Every suite in this file.
        /// </summary>
        /// <returns>The suites.</returns>
        public static IEnumerable<TestSuiteDescriptor> All()
        {
            yield return CancellationSuite();
            yield return LifecycleSuite();
            yield return StopSuite();
        }

        private static TestSuiteDescriptor CancellationSuite()
        {
            const string S = "Cancellation";
            return new TestSuiteDescriptor(S, "Cancellation", new[]
            {
                Check.Async(S, "CAN-01", "Pre-canceled runs execute nothing and mark every selected case NotRun", async ct =>
                {
                    bool ran = false;
                    using (CancellationTokenSource source = new CancellationTokenSource())
                    {
                        source.Cancel();
                        TestSuiteDescriptor a = Fixtures.HookedSuite("A", _ => { ran = true; return default; }, null, Fixtures.Passing("A", "1", _ => ran = true), Fixtures.Passing("A", "2"));
                        TestSuiteDescriptor b = Fixtures.Suite("B", Fixtures.Passing("B", "1", _ => ran = true));
                        TestRunSummary s = await TestExecutor.RunAsync(new[] { a, b }, null, null, source.Token);
                        TestAssert.False(ran, "nothing ran");
                        TestAssert.Equal(3, s.NotRun, "not run");
                        TestAssert.True(s.WasCanceled, "was canceled");
                    }
                }),
                Check.Async(S, "CAN-02", "Cancellation during setup marks cases NotRun, runs teardown, and starts no later suite", async ct =>
                {
                    bool teardown = false;
                    bool laterStarted = false;
                    using (CancellationTokenSource source = new CancellationTokenSource())
                    {
                        TestSuiteDescriptor a = Fixtures.HookedSuite("A", t => { source.Cancel(); t.ThrowIfCancellationRequested(); return default; },
                            _ => { teardown = true; return default; }, Fixtures.Passing("A", "1"));
                        TestSuiteDescriptor b = Fixtures.HookedSuite("B", _ => { laterStarted = true; return default; }, null, Fixtures.Passing("B", "1"));
                        TestRunSummary s = await TestExecutor.RunAsync(new[] { a, b }, null, null, source.Token);
                        TestAssert.True(teardown, "teardown ran");
                        TestAssert.False(laterStarted, "later suite");
                        TestAssert.Equal(2, s.NotRun, "not run");
                        TestAssert.Equal(0, s.LifecycleErrors.Count, "cancellation is not a lifecycle error");
                        TestAssert.True(s.WasCanceled, "was canceled");
                    }
                }),
                Check.Async(S, "CAN-03", "A case that observes cancellation is Canceled and later cases are NotRun", async ct =>
                {
                    foreach (bool scenario in new[] { false, true })
                    {
                        using (CancellationTokenSource source = new CancellationTokenSource())
                        {
                            TestCaseDescriptor canceling = scenario
                                ? Fixtures.Scenario("A", "Cancel", (c, t) => { source.Cancel(); t.ThrowIfCancellationRequested(); return Task.CompletedTask; })
                                : Fixtures.CancelsRun("A", "Cancel", source);
                            RecordingSink sink = new RecordingSink();
                            await TestExecutor.RunSuiteAsync(Fixtures.Suite("A", canceling, Fixtures.Passing("A", "Next")), sink, null, source.Token);
                            TestAssert.Equal("Canceled,NotRun", string.Join(",", sink.Results.Select(r => r.Outcome)), "outcomes (scenario=" + scenario + ")");
                        }
                    }
                }),
                Check.Async(S, "CAN-04", "A case that ignores cancellation keeps its outcome; later cases are NotRun", async ct =>
                {
                    using (CancellationTokenSource source = new CancellationTokenSource())
                    {
                        RecordingSink sink = new RecordingSink();
                        await TestExecutor.RunSuiteAsync(Fixtures.Suite("A", Fixtures.Passing("A", "Ignore", _ => source.Cancel()), Fixtures.Passing("A", "Next")), sink, null, source.Token);
                        TestAssert.Equal("Passed,NotRun", string.Join(",", sink.Results.Select(r => r.Outcome)), "outcomes");
                    }
                }),
                Check.Async(S, "CAN-05", "Cancellation between cases marks the next case NotRun", async ct =>
                {
                    using (CancellationTokenSource source = new CancellationTokenSource())
                    {
                        RecordingSink sink = new RecordingSink(null, e => { if (e == TouchstoneTelemetry.EventCaseCompleted) source.Cancel(); });
                        await TestExecutor.RunSuiteAsync(Fixtures.Suite("A", Fixtures.Passing("A", "1"), Fixtures.Passing("A", "2")), sink, null, source.Token);
                        TestAssert.Equal("Passed,NotRun", string.Join(",", sink.Results.Select(r => r.Outcome)), "outcomes");
                    }
                }),
                Check.Async(S, "CAN-06", "Teardown receives the cleanup-budget token, not the canceled caller token", async ct =>
                {
                    using (CancellationTokenSource source = new CancellationTokenSource())
                    {
                        CancellationToken seen = default;
                        bool canceledOnEntry = true;
                        TestSuiteDescriptor suite = Fixtures.HookedSuite("A", null, t => { seen = t; canceledOnEntry = t.IsCancellationRequested; return default; },
                            Fixtures.CancelsRun("A", "1", source));
                        await TestExecutor.RunSuiteAsync(suite, null, null, source.Token);
                        TestAssert.False(seen == source.Token, "different token");
                        TestAssert.False(canceledOnEntry, "budget token not yet canceled");
                    }
                }),
                Check.Async(S, "CAN-07", "Teardown still runs for the suite in progress", async ct =>
                {
                    using (CancellationTokenSource source = new CancellationTokenSource())
                    {
                        bool teardown = false;
                        TestSuiteDescriptor suite = Fixtures.HookedSuite("A", null, _ => { teardown = true; return default; }, Fixtures.CancelsRun("A", "1", source));
                        await TestExecutor.RunAsync(new[] { suite }, null, null, source.Token);
                        TestAssert.True(teardown, "teardown");
                    }
                }),
                Check.Async(S, "CAN-08", "Later suites are not started and their selected cases count as NotRun", async ct =>
                {
                    using (CancellationTokenSource source = new CancellationTokenSource())
                    {
                        RecordingSink sink = new RecordingSink();
                        TestRunOptions options = new TestRunOptions();
                        options.Filter.ExcludeTags.Add("off");
                        TestSuiteDescriptor a = Fixtures.Suite("A", Fixtures.CancelsRun("A", "1", source));
                        TestSuiteDescriptor b = Fixtures.Suite("B", Fixtures.Passing("B", "1"), Fixtures.Passing("B", "2"), Fixtures.Passing("B", "3", null, new[] { "off" }));
                        TestRunSummary s = await TestExecutor.RunAsync(new[] { a, b }, sink, options, source.Token);
                        TestAssert.False(sink.Events.Contains("suite_started:B"), "B not started");
                        TestAssert.Equal(2, s.NotRun, "selected cases in B");
                        TestAssert.Equal(1, s.Filtered, "filtered case in B");
                        TestAssert.Equal(1, s.Canceled, "canceled case");
                    }
                }),
                Check.Async(S, "CAN-09", "NotRun results for the current suite reach OnCaseCompletedAsync", async ct =>
                {
                    using (CancellationTokenSource source = new CancellationTokenSource())
                    {
                        RecordingSink sink = new RecordingSink();
                        await TestExecutor.RunSuiteAsync(Fixtures.Suite("A", Fixtures.CancelsRun("A", "1", source), Fixtures.Passing("A", "2"), Fixtures.Passing("A", "3")), sink, null, source.Token);
                        TestAssert.Equal(2, sink.Results.Count(r => r.Outcome == TestOutcome.NotRun), "not run results");
                        TestAssert.Contains("canceled", sink.Results.Last().Message, "message");
                    }
                }),
                Check.Async(S, "CAN-10", "RunAsync returns normally with WasCanceled set", async ct =>
                {
                    using (CancellationTokenSource source = new CancellationTokenSource())
                    {
                        TestRunSummary s = await TestExecutor.RunAsync(new[] { Fixtures.Suite("A", Fixtures.CancelsRun("A", "1", source)) }, null, null, source.Token);
                        TestAssert.True(s.WasCanceled, "was canceled");
                        TestAssert.False(s.IsSuccess, "success");
                    }
                }),
                Check.Async(S, "CAN-11", "OperationCanceledException without caller cancellation is Failed", async ct =>
                {
                    TestCaseDescriptor c = new TestCaseDescriptor("A", "1", "N", _ => throw new OperationCanceledException("internal timeout"));
                    TestResult r = await TestExecutor.ExecuteCaseResultAsync(c, null, null, ct);
                    TestAssert.Equal(TestOutcome.Failed, r.Outcome, "outcome");
                }),
                Check.Async(S, "CAN-12", "A linked-token TaskCanceledException is Canceled when the caller cancels", async ct =>
                {
                    using (CancellationTokenSource source = new CancellationTokenSource())
                    {
                        TestCaseDescriptor c = new TestCaseDescriptor("A", "1", "N", async t =>
                        {
                            using (CancellationTokenSource linked = CancellationTokenSource.CreateLinkedTokenSource(t))
                            {
                                source.Cancel();
                                await Task.Delay(Timeout.Infinite, linked.Token);
                            }
                        });
                        TestResult r = await TestExecutor.ExecuteCaseResultAsync(c, null, null, source.Token);
                        TestAssert.Equal(TestOutcome.Canceled, r.Outcome, "outcome");
                        TestAssert.True(r.Exception is TaskCanceledException, "exception kept");
                    }
                }),
                Check.Async(S, "CAN-13", "A non-cancellation exception after cancel is Failed", async ct =>
                {
                    using (CancellationTokenSource source = new CancellationTokenSource())
                    {
                        TestCaseDescriptor c = new TestCaseDescriptor("A", "1", "N", _ => { source.Cancel(); throw new InvalidOperationException("real failure"); });
                        TestResult r = await TestExecutor.ExecuteCaseResultAsync(c, null, null, source.Token);
                        TestAssert.Equal(TestOutcome.Failed, r.Outcome, "outcome");
                    }
                }),
                Check.Async(S, "CAN-14", "ExecuteCaseAsync throws OperationCanceledException for a canceled case", async ct =>
                {
                    using (CancellationTokenSource source = new CancellationTokenSource())
                    {
                        await TestAssert.ThrowsAsync<OperationCanceledException>(() => TestExecutor.ExecuteCaseAsync(Fixtures.CancelsRun("A", "1", source), null, source.Token), "throws");
                    }
                }),
                Check.Async(S, "CAN-15", "ExecuteCaseResultAsync returns Canceled", async ct =>
                {
                    using (CancellationTokenSource source = new CancellationTokenSource())
                    {
                        TestResult r = await TestExecutor.ExecuteCaseResultAsync(Fixtures.CancelsRun("A", "1", source), null, null, source.Token);
                        TestAssert.Equal(TestOutcome.Canceled, r.Outcome, "outcome");
                    }
                }),
                Check.Async(S, "CAN-16", "Scenario cancellation cancels the step, skips later steps, and still cleans up", async ct =>
                {
                    using (CancellationTokenSource source = new CancellationTokenSource())
                    {
                        ScenarioContext context = new ScenarioContext();
                        bool cleaned = false;
                        TestCaseDescriptor scenario = new ScenarioBuilder<ScenarioContext>("A", "Scn", "N", _ => Task.FromResult(context))
                            .WithCleanup((c, t) => { cleaned = true; return Task.CompletedTask; })
                            .When("cancel", "cancel", (c, t) => { source.Cancel(); t.ThrowIfCancellationRequested(); return Task.CompletedTask; })
                            .Then("later", "later", (c, t) => Task.CompletedTask)
                            .Build();
                        TestResult r = await TestExecutor.ExecuteCaseResultAsync(scenario, null, null, source.Token);
                        TestAssert.Equal(TestOutcome.Canceled, r.Outcome, "outcome");
                        TestAssert.Equal("Canceled,NotRun", string.Join(",", r.Steps.Select(s => s.Outcome)), "steps");
                        TestAssert.True(cleaned, "cleanup");
                        TestAssert.Equal(1, context.AsyncDisposals, "disposed");
                    }
                }),
                Check.Async(S, "CAN-17", "Scenario pre-cancellation creates no context", async ct =>
                {
                    using (CancellationTokenSource source = new CancellationTokenSource())
                    {
                        source.Cancel();
                        List<ScenarioContext> contexts = new List<ScenarioContext>();
                        TestResult r = await TestExecutor.ExecuteCaseResultAsync(Fixtures.Scenario("A", "Scn", (c, t) => Task.CompletedTask, contexts), null, null, source.Token);
                        TestAssert.Equal(0, contexts.Count, "contexts");
                        TestAssert.Equal(TestOutcome.Canceled, r.Outcome, "outcome");
                    }
                }),
                Check.Async(S, "CAN-18", "Completion callbacks after cancellation receive a token that is not canceled", async ct =>
                {
                    using (CancellationTokenSource source = new CancellationTokenSource())
                    {
                        RecordingSink sink = new RecordingSink();
                        await TestExecutor.RunAsync(new[] { Fixtures.Suite("A", Fixtures.CancelsRun("A", "1", source), Fixtures.Passing("A", "2")) }, sink, null, source.Token);
                        foreach (string e in new[] { TouchstoneTelemetry.EventCaseCompleted, TouchstoneTelemetry.EventSuiteCompleted, TouchstoneTelemetry.EventRunCompleted })
                            TestAssert.False(sink.Tokens[e].IsCancellationRequested, e + " token");
                    }
                }),
                Check.Async(S, "CAN-19", "The cleanup budget cancels a teardown hook that runs too long", async ct =>
                {
                    using (CancellationTokenSource source = new CancellationTokenSource())
                    {
                        TestRunOptions options = new TestRunOptions { CleanupTimeout = TimeSpan.FromMilliseconds(50) };
                        TestSuiteDescriptor suite = Fixtures.HookedSuite("A", null, async t => await Task.Delay(Timeout.Infinite, t), Fixtures.CancelsRun("A", "1", source));
                        Task<TestRunSummary> run = TestExecutor.RunSuiteAsync(suite, null, options, source.Token);
                        Task finished = await Task.WhenAny(run, Task.Delay(TimeSpan.FromSeconds(10)));
                        TestAssert.Same(run, finished, "teardown stopped by budget");
                        TestAssert.Equal(TouchstoneTelemetry.StageAfterSuite, (await run).LifecycleErrors.Single().Stage, "budget expiry recorded");
                    }
                }),
                Check.Async(S, "CAN-21", "The console runner returns exit code 2 when the run is canceled", async ct =>
                {
                    using (CancellationTokenSource source = new CancellationTokenSource())
                    {
                        StrongBox<int> code = new StrongBox<int>();
                        string text = await CliSuites.RunToText(new[] { Fixtures.Suite("A", Fixtures.CancelsRun("A", "1", source), Fixtures.Passing("A", "2")) }, null, source.Token, code);
                        TestAssert.Equal(Touchstone.Cli.ConsoleRunner.ExitCanceled, code.Value, "exit code");
                        TestAssert.Contains("OVERALL", text, "summary written");
                        TestAssert.Contains("CANCELED", text, "canceled label");
                    }
                }),
                Check.Async(S, "CAN-20", "Telemetry records canceled and not_run outcomes", async ct =>
                {
                    using (TelemetryCapture capture = new TelemetryCapture())
                    using (CancellationTokenSource source = new CancellationTokenSource())
                    {
                        await TestExecutor.RunAsync(new[] { Fixtures.Suite("A", Fixtures.CancelsRun("A", "1", source), Fixtures.Passing("A", "2")) }, null, null, source.Token);
                        List<CapturedMeasurement> cases = capture.Measurements(TouchstoneTelemetry.Cases);
                        TestAssert.True(cases.Any(m => m.Tag(TouchstoneTelemetry.LabelOutcome) == "canceled"), "case canceled");
                        TestAssert.True(cases.Any(m => m.Tag(TouchstoneTelemetry.LabelOutcome) == "not_run"), "case not_run");
                        TestAssert.Equal("canceled", capture.Measurements(TouchstoneTelemetry.Runs).Single().Tag(TouchstoneTelemetry.LabelOutcome), "run");
                        TestAssert.Equal("canceled", capture.Measurements(TouchstoneTelemetry.Suites).Single().Tag(TouchstoneTelemetry.LabelOutcome), "suite");
                    }
                }),
            });
        }

        private static TestSuiteDescriptor LifecycleSuite()
        {
            const string S = "Lifecycle";
            return new TestSuiteDescriptor(S, "Suite lifecycle", new[]
            {
                Check.Async(S, "LIFE-01", "Setup runs once before the cases and teardown once after", async ct =>
                {
                    List<string> order = new List<string>();
                    TestSuiteDescriptor suite = Fixtures.HookedSuite("L", _ => { order.Add("before"); return default; }, _ => { order.Add("after"); return default; },
                        Fixtures.Passing("L", "1", _ => order.Add("1")), Fixtures.Passing("L", "2", _ => order.Add("2")));
                    await TestExecutor.RunSuiteAsync(suite, null, null, ct);
                    TestAssert.Equal("before,1,2,after", string.Join(",", order), "order");
                }),
                Check.Async(S, "LIFE-02", "Teardown runs when cases fail", async ct =>
                {
                    bool teardown = false;
                    await TestExecutor.RunSuiteAsync(Fixtures.HookedSuite("L", null, _ => { teardown = true; return default; }, Fixtures.Failing("L", "1")), null, null, ct);
                    TestAssert.True(teardown, "teardown");
                }),
                Check.Async(S, "LIFE-03", "Setup failure fails every selected case without running it", async ct =>
                {
                    bool ran = false;
                    InvalidOperationException setup = new InvalidOperationException("setup broke");
                    RecordingSink sink = new RecordingSink();
                    TestSuiteDescriptor suite = Fixtures.HookedSuite("L", _ => throw setup, null, Fixtures.Passing("L", "1", _ => ran = true), Fixtures.Passing("L", "2", _ => ran = true));
                    await TestExecutor.RunSuiteAsync(suite, sink, null, ct);
                    TestAssert.False(ran, "cases ran");
                    TestAssert.True(sink.Results.All(r => r.Outcome == TestOutcome.Failed && ReferenceEquals(r.Exception, setup)), "failed with hook exception");
                    TestAssert.Contains("before_suite", sink.Results[0].Message, "message");
                }),
                Check.Async(S, "LIFE-04", "Teardown runs after a setup failure", async ct =>
                {
                    bool teardown = false;
                    await TestExecutor.RunSuiteAsync(Fixtures.HookedSuite("L", _ => throw new InvalidOperationException("x"), _ => { teardown = true; return default; }, Fixtures.Passing("L", "1")), null, null, ct);
                    TestAssert.True(teardown, "teardown");
                }),
                Check.Async(S, "LIFE-05", "Setup failure records a before_suite error and the next suite runs", async ct =>
                {
                    bool nextRan = false;
                    TestRunSummary s = await TestExecutor.RunAsync(new[]
                    {
                        Fixtures.HookedSuite("L", _ => throw new InvalidOperationException("x"), null, Fixtures.Passing("L", "1")),
                        Fixtures.Suite("M", Fixtures.Passing("M", "1", _ => nextRan = true)),
                    }, null, null, ct);
                    TestAssert.Equal("before_suite", s.LifecycleErrors.Single().Stage, "stage");
                    TestAssert.Equal("L", s.LifecycleErrors.Single().SuiteId, "suite");
                    TestAssert.True(nextRan, "next suite");
                }),
                Check.Async(S, "LIFE-06", "Teardown failure keeps case results, records after_suite, and the next suite runs", async ct =>
                {
                    bool nextRan = false;
                    TestRunSummary s = await TestExecutor.RunAsync(new[]
                    {
                        Fixtures.HookedSuite("L", null, _ => throw new InvalidOperationException("x"), Fixtures.Passing("L", "1")),
                        Fixtures.Suite("M", Fixtures.Passing("M", "1", _ => nextRan = true)),
                    }, null, null, ct);
                    TestAssert.Equal(2, s.Passed, "passed");
                    TestAssert.Equal("after_suite", s.LifecycleErrors.Single().Stage, "stage");
                    TestAssert.True(nextRan, "next suite");
                }),
                Check.Async(S, "LIFE-07", "Failures in both hooks record two errors", async ct =>
                {
                    TestRunSummary s = await TestExecutor.RunSuiteAsync(Fixtures.HookedSuite("L", _ => throw new InvalidOperationException("a"), _ => throw new InvalidOperationException("b"), Fixtures.Passing("L", "1")), null, null, ct);
                    TestAssert.Equal("before_suite,after_suite", string.Join(",", s.LifecycleErrors.Select(e => e.Stage)), "stages");
                }),
                Check.Async(S, "LIFE-08", "A lifecycle error makes the run unsuccessful", async ct =>
                {
                    TestRunSummary s = await TestExecutor.RunAsync(new[] { Fixtures.HookedSuite("L", null, _ => throw new InvalidOperationException("x"), Fixtures.Passing("L", "1")) }, null, null, ct);
                    TestAssert.Equal(0, s.Failed, "no failed case");
                    TestAssert.False(s.IsSuccess, "success");
                }),
                Check.Async(S, "LIFE-09", "A hook returning a faulted ValueTask is handled like a throw", async ct =>
                {
                    TestRunSummary s = await TestExecutor.RunSuiteAsync(Fixtures.HookedSuite("L", _ => new ValueTask(Task.FromException(new FormatException("faulted"))), null, Fixtures.Passing("L", "1")), null, null, ct);
                    TestAssert.True(s.LifecycleErrors.Single().Exception is FormatException, "type");
                    TestAssert.Equal(1, s.Failed, "case failed");
                }),
                Check.Async(S, "LIFE-10", "A sink exception aborts the run, runs teardown, and propagates", async ct =>
                {
                    bool teardown = false;
                    bool secondRan = false;
                    TestSuiteDescriptor suite = Fixtures.HookedSuite("L", null, _ => { teardown = true; return default; }, Fixtures.Passing("L", "1"), Fixtures.Passing("L", "2", _ => secondRan = true));
                    InvalidOperationException ex = await TestAssert.ThrowsAsync<InvalidOperationException>(
                        () => TestExecutor.RunAsync(new[] { suite }, new RecordingSink(TouchstoneTelemetry.EventCaseCompleted), null, ct), "sink failure");
                    TestAssert.Contains("sink failure", ex.Message, "original exception");
                    TestAssert.True(teardown, "teardown");
                    TestAssert.False(secondRan, "aborted");
                }),
                Check.Async(S, "LIFE-11", "Lifecycle errors appear in the suite and run summaries", async ct =>
                {
                    RecordingSink sink = new RecordingSink();
                    TestRunSummary s = await TestExecutor.RunAsync(new[] { Fixtures.HookedSuite("L", _ => throw new InvalidOperationException("x"), null, Fixtures.Passing("L", "1")) }, sink, null, ct);
                    TestAssert.Equal(1, sink.SuiteSummaries.Single().LifecycleErrors.Count, "suite summary");
                    TestAssert.Equal(1, s.LifecycleErrors.Count, "run summary");
                    TestAssert.Same(s, sink.RunSummary, "sink summary");
                }),
            });
        }

        private static TestSuiteDescriptor StopSuite()
        {
            const string S = "StopOnFailure";
            TestRunOptions stop = new TestRunOptions { StopOnFirstFailure = true };
            return new TestSuiteDescriptor(S, "Stop on first failure", new[]
            {
                Check.Async(S, "SOF-01", "With the option off, later cases run after a failure", async ct =>
                {
                    TestRunSummary s = await TestExecutor.RunSuiteAsync(Fixtures.Suite("O", Fixtures.Failing("O", "1"), Fixtures.Passing("O", "2")), null, null, ct);
                    TestAssert.Equal(1, s.Passed, "passed");
                }),
                Check.Async(S, "SOF-02", "With the option on, later cases are NotRun and later suites do not start", async ct =>
                {
                    RecordingSink sink = new RecordingSink();
                    TestRunSummary s = await TestExecutor.RunAsync(new[]
                    {
                        Fixtures.Suite("O", Fixtures.Failing("O", "1"), Fixtures.Passing("O", "2")),
                        Fixtures.Suite("P", Fixtures.Passing("P", "1")),
                    }, sink, stop, ct);
                    TestAssert.Equal("Failed,NotRun", string.Join(",", sink.Results.Select(r => r.Outcome)), "outcomes");
                    TestAssert.Equal(2, s.NotRun, "not run");
                    TestAssert.False(sink.Events.Contains("suite_started:P"), "later suite");
                    TestAssert.Contains("StopOnFirstFailure", sink.Results[1].Message, "message");
                }),
                Check.Async(S, "SOF-03", "Teardown still runs for the failing suite", async ct =>
                {
                    bool teardown = false;
                    await TestExecutor.RunSuiteAsync(Fixtures.HookedSuite("O", null, _ => { teardown = true; return default; }, Fixtures.Failing("O", "1")), null, stop, ct);
                    TestAssert.True(teardown, "teardown");
                }),
                Check.Async(S, "SOF-04", "A reported skip does not stop the run; a Fail-mode skip does", async ct =>
                {
                    TestRunSummary report = await TestExecutor.RunSuiteAsync(Fixtures.Suite("O", Fixtures.Skipped("O", "1"), Fixtures.Passing("O", "2")), null, stop, ct);
                    TestAssert.Equal(1, report.Passed, "report");
                    TestRunOptions failSkips = new TestRunOptions { StopOnFirstFailure = true, SkipBehavior = SkipBehavior.Fail };
                    TestRunSummary fail = await TestExecutor.RunSuiteAsync(Fixtures.Suite("O", Fixtures.Skipped("O", "1"), Fixtures.Passing("O", "2")), null, failSkips, ct);
                    TestAssert.Equal(1, fail.NotRun, "fail mode");
                }),
                Check.Async(S, "SOF-05", "A setup failure triggers the stop", async ct =>
                {
                    TestRunSummary s = await TestExecutor.RunAsync(new[]
                    {
                        Fixtures.HookedSuite("O", _ => throw new InvalidOperationException("x"), null, Fixtures.Passing("O", "1")),
                        Fixtures.Suite("P", Fixtures.Passing("P", "1")),
                    }, null, stop, ct);
                    TestAssert.Equal(1, s.Failed, "failed");
                    TestAssert.Equal(1, s.NotRun, "later suite not run");
                }),
                Check.Async(S, "SOF-06", "Stopping does not set WasCanceled", async ct =>
                {
                    TestRunSummary s = await TestExecutor.RunSuiteAsync(Fixtures.Suite("O", Fixtures.Failing("O", "1"), Fixtures.Passing("O", "2")), null, stop, ct);
                    TestAssert.False(s.WasCanceled, "was canceled");
                }),
            });
        }
    }
}
