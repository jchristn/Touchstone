namespace Test.Shared
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;

    using Touchstone.Core;

    /// <summary>
    /// Catalog section 1.1-B (archive/V1.1_PLAN.md): parallel case execution.
    /// </summary>
    public static class ParallelismSuites
    {
        private static readonly TimeSpan GateLimit = TimeSpan.FromSeconds(20);

        /// <summary>
        /// Every suite in this file.
        /// </summary>
        /// <returns>The suites.</returns>
        public static IEnumerable<TestSuiteDescriptor> All()
        {
            yield return ParallelSuite();
        }

        private static TestRunOptions Parallel(int degree)
        {
            return new TestRunOptions { MaxDegreeOfParallelism = degree };
        }

        private static TestSuiteDescriptor ParallelSuite()
        {
            const string S = "Parallelism";
            return new TestSuiteDescriptor(S, "Parallel case execution", new[]
            {
                Check.Sync(S, "PLL-01", "MaxDegreeOfParallelism defaults to 1 and accepts 1 to 1024 only", () =>
                {
                    TestRunOptions options = new TestRunOptions();
                    TestAssert.Equal(1, options.MaxDegreeOfParallelism, "default");
                    TestAssert.Throws<ArgumentOutOfRangeException>(() => options.MaxDegreeOfParallelism = 0, "zero");
                    TestAssert.Throws<ArgumentOutOfRangeException>(() => options.MaxDegreeOfParallelism = -1, "negative");
                    TestAssert.Throws<ArgumentOutOfRangeException>(() => options.MaxDegreeOfParallelism = 1025, "over the limit");
                    options.MaxDegreeOfParallelism = 1024;
                    TestAssert.Equal(1024, options.MaxDegreeOfParallelism, "limit");
                }),
                Check.Async(S, "PLL-02", "Cases run concurrently, never more than the limit at a time", async ct =>
                {
                    ConcurrencyProbe probe = new ConcurrencyProbe(2);
                    TestCaseDescriptor[] cases = Enumerable.Range(0, 6).Select(i => probe.Case("P", "C" + i)).ToArray();
                    TestRunSummary summary = await TestExecutor.RunSuiteAsync(Fixtures.Suite("P", cases), null, Parallel(2), ct);
                    TestAssert.Equal(6, summary.Passed, "passed");
                    TestAssert.Equal(2, probe.Peak, "peak concurrency");
                }),
                Check.Async(S, "PLL-03", "Every case is reported once, sink callbacks never overlap, and counts match", async ct =>
                {
                    OverlapSink sink = new OverlapSink();
                    List<TestCaseDescriptor> cases = new List<TestCaseDescriptor>();
                    for (int i = 0; i < 12; i++)
                    {
                        int index = i;
                        if (index % 4 == 1) cases.Add(Fixtures.Failing("P", "F" + index));
                        else if (index % 4 == 2) cases.Add(Fixtures.Skipped("P", "S" + index));
                        else if (index % 4 == 3) cases.Add(Fixtures.Scenario("P", "N" + index, async (c, t) => { await Task.Delay(5, t).ConfigureAwait(false); c.Value = index; }));
                        else cases.Add(new TestCaseDescriptor("P", "A" + index, "A" + index, async t => await Task.Delay(10, t).ConfigureAwait(false)));
                    }

                    TestRunSummary summary = await TestExecutor.RunAsync(new[] { Fixtures.Suite("P", cases.ToArray()) }, sink, Parallel(4), ct);
                    TestAssert.Equal(6, summary.Passed, "passed");
                    TestAssert.Equal(3, summary.Failed, "failed");
                    TestAssert.Equal(3, summary.Skipped, "skipped");
                    TestAssert.Equal(12, sink.Completed.Count, "every result delivered");
                    TestAssert.Equal(12, sink.Completed.Distinct(StringComparer.Ordinal).Count(), "each once");
                    TestAssert.False(sink.Overlapped, "no concurrent sink callbacks");
                    TestAssert.True(sink.StepEvents >= 6, "scenario step callbacks delivered");
                }),
                Check.Async(S, "PLL-04", "StopOnFirstFailure: running cases finish and cases not yet started are NotRun", async ct =>
                {
                    TestRunOptions options = Parallel(2);
                    options.StopOnFirstFailure = true;
                    TestCaseDescriptor slow = new TestCaseDescriptor("P", "Slow", "Slow", async t => await Task.Delay(200, t).ConfigureAwait(false));
                    RecordingSink sink = new RecordingSink();
                    TestRunSummary summary = await TestExecutor.RunSuiteAsync(
                        Fixtures.Suite("P", Fixtures.Failing("P", "Fail"), slow, Fixtures.Passing("P", "C"), Fixtures.Passing("P", "D")), sink, options, ct);
                    TestAssert.Equal(1, summary.Failed, "failed");
                    TestAssert.Equal(1, summary.Passed, "the running case finished");
                    TestAssert.Equal(2, summary.NotRun, "not started");
                    TestAssert.Equal(TestOutcome.Passed, sink.Results.Single(r => r.CaseId == "Slow").Outcome, "slow");
                }),
                Check.Async(S, "PLL-05", "Caller cancellation: running cases see it, cases not started are NotRun", async ct =>
                {
                    using (CancellationTokenSource source = CancellationTokenSource.CreateLinkedTokenSource(ct))
                    {
                        int started = 0;
                        Func<CancellationToken, Task> wait = async t =>
                        {
                            if (Interlocked.Increment(ref started) == 2) source.Cancel();
                            await Task.Delay(Timeout.Infinite, t).ConfigureAwait(false);
                        };
                        TestSuiteDescriptor suite = Fixtures.Suite("P",
                            new TestCaseDescriptor("P", "A", "A", wait), new TestCaseDescriptor("P", "B", "B", wait),
                            Fixtures.Passing("P", "C"), Fixtures.Passing("P", "D"));
                        TestRunSummary summary = await TestExecutor.RunSuiteAsync(suite, null, Parallel(2), source.Token);
                        TestAssert.Equal(2, summary.Canceled, "canceled");
                        TestAssert.Equal(2, summary.NotRun, "not run");
                        TestAssert.True(summary.WasCanceled, "was canceled");
                    }
                }),
                Check.Async(S, "PLL-06", "Suite hooks bracket the parallel cases, and suites still run one after another", async ct =>
                {
                    List<string> events = new List<string>();
                    object sync = new object();
                    Action<string> log = e => { lock (sync) events.Add(e); };
                    ConcurrencyProbe probe = new ConcurrencyProbe(3);
                    Func<string, TestSuiteDescriptor> suite = id => Fixtures.HookedSuite(id,
                        _ => { log(id + ":before"); return ValueTask.CompletedTask; },
                        _ => { log(id + ":after"); return ValueTask.CompletedTask; },
                        Enumerable.Range(0, 3).Select(i => probe.Case(id, "C" + i, () => log(id + ":case"))).ToArray());
                    TestRunSummary summary = await TestExecutor.RunAsync(new[] { suite("P1"), suite("P2") }, null, Parallel(3), ct);
                    TestAssert.Equal(6, summary.Passed, "passed");
                    TestAssert.Equal("P1:before,P1:case,P1:case,P1:case,P1:after,P2:before,P2:case,P2:case,P2:case,P2:after", string.Join(",", events), "order");
                }),
                Check.Async(S, "PLL-07", "Scenario contexts stay isolated and are each disposed under parallel execution", async ct =>
                {
                    List<ScenarioContext> contexts = new List<ScenarioContext>();
                    object sync = new object();
                    List<TestCaseDescriptor> cases = new List<TestCaseDescriptor>();
                    for (int i = 0; i < 16; i++)
                    {
                        int value = i;
                        cases.Add(new ScenarioBuilder<ScenarioContext>("P", "S" + i, "S" + i, _ =>
                            {
                                ScenarioContext context = new ScenarioContext();
                                lock (sync) contexts.Add(context);
                                return Task.FromResult(context);
                            })
                            .Given("set", "the value is set", async (c, t) => { c.Value = value; await Task.Delay(5, t).ConfigureAwait(false); })
                            .Then("check", "the value is unchanged", (c, _) =>
                            {
                                if (c.Value != value) throw new InvalidOperationException("context shared: " + c.Value + " != " + value);
                                return Task.CompletedTask;
                            })
                            .Build());
                    }

                    TestRunSummary summary = await TestExecutor.RunSuiteAsync(Fixtures.Suite("P", cases.ToArray()), null, Parallel(8), ct);
                    TestAssert.Equal(16, summary.Passed, "passed");
                    TestAssert.Equal(16, contexts.Count, "one context per case");
                    TestAssert.True(contexts.All(c => c.AsyncDisposals == 1), "each disposed once");
                }),
                Check.Async(S, "PLL-08", "A sink exception escapes the run after running cases finish, and teardown still runs", async ct =>
                {
                    bool teardown = false;
                    TestSuiteDescriptor suite = Fixtures.HookedSuite("P", null, _ => { teardown = true; return ValueTask.CompletedTask; },
                        Fixtures.Passing("P", "A"), Fixtures.Passing("P", "B"), Fixtures.Passing("P", "C"));
                    RecordingSink sink = new RecordingSink(TouchstoneTelemetry.EventCaseCompleted);
                    InvalidOperationException error = await TestAssert.ThrowsAsync<InvalidOperationException>(
                        () => TestExecutor.RunAsync(new[] { suite }, sink, Parallel(2), ct), "sink failure");
                    TestAssert.Contains("sink failure on case_completed", error.Message, "original exception");
                    TestAssert.True(teardown, "teardown ran");
                }),
                Check.Async(S, "PLL-09", "Parallel runs combine with case timeouts", async ct =>
                {
                    TestRunOptions options = Parallel(4);
                    options.CaseTimeout = TimeSpan.FromMilliseconds(150);
                    TestCaseDescriptor[] cases = Enumerable.Range(0, 4)
                        .Select(i => new TestCaseDescriptor("P", "W" + i, "W" + i, t => Task.Delay(Timeout.Infinite, t)))
                        .Concat(new[] { Fixtures.Passing("P", "Fast") })
                        .ToArray();
                    RecordingSink sink = new RecordingSink();
                    TestRunSummary summary = await TestExecutor.RunSuiteAsync(Fixtures.Suite("P", cases), sink, options, ct);
                    TestAssert.Equal(4, summary.Failed, "timed out");
                    TestAssert.Equal(1, summary.Passed, "passed");
                    TestAssert.True(sink.Results.Where(r => r.Outcome == TestOutcome.Failed).All(r => r.Exception is CaseTimeoutException), "timeouts");
                }),
            });
        }

        private sealed class ConcurrencyProbe
        {
            private readonly int _Expected;
            private readonly TaskCompletionSource<bool> _Gate = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            private int _Current = 0;
            private int _Peak = 0;

            internal ConcurrencyProbe(int expected)
            {
                _Expected = expected;
            }

            internal int Peak
            {
                get { return Volatile.Read(ref _Peak); }
            }

            internal TestCaseDescriptor Case(string suiteId, string caseId, Action? onRun = null)
            {
                return new TestCaseDescriptor(suiteId, caseId, caseId, async t =>
                {
                    onRun?.Invoke();
                    int now = Interlocked.Increment(ref _Current);
                    int peak;
                    do
                    {
                        peak = Volatile.Read(ref _Peak);
                    }
                    while (now > peak && Interlocked.CompareExchange(ref _Peak, now, peak) != peak);

                    if (now >= _Expected) _Gate.TrySetResult(true);

                    try
                    {
                        // Holds every case until the expected number run at once, which proves concurrency without
                        // depending on timing. The limit only bounds a broken run.
                        await _Gate.Task.WaitAsync(GateLimit, t).ConfigureAwait(false);
                    }
                    finally
                    {
                        Interlocked.Decrement(ref _Current);
                    }
                });
            }
        }

        private sealed class OverlapSink : ITestResultSink
        {
            private int _Inside = 0;

            internal List<string> Completed { get; } = new List<string>();

            internal bool Overlapped { get; private set; }

            internal int StepEvents { get; private set; }

            public async ValueTask OnCaseStartedAsync(TestCaseDescriptor testCase, CancellationToken cancellationToken)
            {
                await Guard().ConfigureAwait(false);
            }

            public async ValueTask OnStepStartedAsync(string testId, ScenarioStepResult step, CancellationToken cancellationToken)
            {
                await Guard().ConfigureAwait(false);
                StepEvents++;
            }

            public async ValueTask OnStepCompletedAsync(string testId, ScenarioStepResult step, CancellationToken cancellationToken)
            {
                await Guard().ConfigureAwait(false);
                StepEvents++;
            }

            public async ValueTask OnCaseCompletedAsync(TestResult result, CancellationToken cancellationToken)
            {
                await Guard().ConfigureAwait(false);
                Completed.Add(result.TestId);
            }

            private async Task Guard()
            {
                if (Interlocked.Increment(ref _Inside) > 1) Overlapped = true;
                await Task.Yield();
                Thread.SpinWait(1000);
                Interlocked.Decrement(ref _Inside);
            }
        }
    }
}
