namespace Test.Shared
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Runtime.ExceptionServices;
    using System.Threading;
    using System.Threading.Tasks;

    using Touchstone.Core;

    /// <summary>
    /// Catalog section 11.8: engine execution.
    /// </summary>
    public static class ExecutionSuites
    {
        /// <summary>
        /// Every suite in this file.
        /// </summary>
        /// <returns>The suites.</returns>
        public static IEnumerable<TestSuiteDescriptor> All()
        {
            yield return ExecutionSuite();
        }

        private static TestSuiteDescriptor ExecutionSuite()
        {
            const string S = "Execution";
            return new TestSuiteDescriptor(S, "Engine execution", new[]
            {
                Check.Async(S, "EXE-01", "Entry points reject null inputs", async ct =>
                {
                    await TestAssert.ThrowsAsync<ArgumentNullException>(() => TestExecutor.RunAsync(null!, null, null, ct), "RunAsync");
                    await TestAssert.ThrowsAsync<ArgumentException>(() => TestExecutor.RunAsync(new TestSuiteDescriptor[] { null! }, null, null, ct), "RunAsync null suite");
                    await TestAssert.ThrowsAsync<ArgumentNullException>(() => TestExecutor.RunSuiteAsync(null!, null, null, ct), "RunSuiteAsync");
                    await TestAssert.ThrowsAsync<ArgumentNullException>(() => TestExecutor.ExecuteCaseAsync(null!, null, ct), "ExecuteCaseAsync");
                    await TestAssert.ThrowsAsync<ArgumentNullException>(() => TestExecutor.ExecuteCaseResultAsync(null!, null, null, ct), "ExecuteCaseResultAsync");
                }),
                Check.Async(S, "EXE-02", "A passing case reports Passed with identity and tags", async ct =>
                {
                    TestResult r = await TestExecutor.ExecuteCaseResultAsync(Fixtures.Passing("E", "A", null, new[] { "t" }), null, null, ct);
                    TestAssert.Equal(TestOutcome.Passed, r.Outcome, "outcome");
                    TestAssert.Equal("E.A", r.TestId, "id");
                    TestAssert.Equal("A display", r.DisplayName, "name");
                    TestAssert.Equal("t", r.Tags.Single(), "tag");
                    TestAssert.True(r.Duration >= TimeSpan.Zero, "duration");
                    TestAssert.Null(r.Exception, "exception");
                }),
                Check.Async(S, "EXE-03", "A throwing case reports Failed with the original exception", async ct =>
                {
                    TestResult r = await TestExecutor.ExecuteCaseResultAsync(Fixtures.Failing("E", "A", "kaput"), null, null, ct);
                    TestAssert.Equal(TestOutcome.Failed, r.Outcome, "outcome");
                    TestAssert.Equal("kaput", r.Message, "message");
                    TestAssert.True(r.Exception is InvalidOperationException, "type");
                }),
                Check.Async(S, "EXE-04", "An exception thrown after an await is captured", async ct =>
                {
                    TestCaseDescriptor c = new TestCaseDescriptor("E", "A", "N", async t =>
                    {
                        await Task.Yield();
                        throw new FormatException("late");
                    });
                    TestResult r = await TestExecutor.ExecuteCaseResultAsync(c, null, null, ct);
                    TestAssert.True(r.Exception is FormatException, "type");
                }),
                Check.Async(S, "EXE-05", "A faulted task returned without throwing is captured", async ct =>
                {
                    TestCaseDescriptor c = new TestCaseDescriptor("E", "A", "N", _ => Task.FromException(new TimeoutException("faulted")));
                    TestResult r = await TestExecutor.ExecuteCaseResultAsync(c, null, null, ct);
                    TestAssert.True(r.Exception is TimeoutException, "type");
                }),
                Check.Async(S, "EXE-06", "A null task fails with InvalidOperationException", async ct =>
                {
                    TestCaseDescriptor c = new TestCaseDescriptor("E", "A", "N", _ => null!);
                    TestResult r = await TestExecutor.ExecuteCaseResultAsync(c, null, null, ct);
                    TestAssert.Equal(TestOutcome.Failed, r.Outcome, "outcome");
                    TestAssert.True(r.Exception is InvalidOperationException, "type");
                    TestAssert.Contains("null task", r.Message, "message");
                }),
                Check.Async(S, "EXE-07", "A failure does not stop later cases", async ct =>
                {
                    TestRunSummary s = await TestExecutor.RunSuiteAsync(Fixtures.Suite("E", Fixtures.Failing("E", "A"), Fixtures.Passing("E", "B")), null, null, ct);
                    TestAssert.Equal(1, s.Passed, "passed");
                    TestAssert.Equal(1, s.Failed, "failed");
                }),
                Check.Async(S, "EXE-08", "Cases and suites run in declaration order", async ct =>
                {
                    List<string> order = new List<string>();
                    TestSuiteDescriptor a = Fixtures.Suite("A", Fixtures.Passing("A", "One", _ => order.Add("A1")), Fixtures.Passing("A", "Two", _ => order.Add("A2")));
                    TestSuiteDescriptor b = Fixtures.Suite("B", Fixtures.Passing("B", "One", _ => order.Add("B1")));
                    RecordingSink sink = new RecordingSink();
                    await TestExecutor.RunAsync(new[] { a, b }, sink, null, ct);
                    TestAssert.Equal("A1,A2,B1", string.Join(",", order), "execution order");
                    TestAssert.Equal("A.One,A.Two,B.One", string.Join(",", sink.Results.Select(r => r.TestId)), "report order");
                }),
                Check.Async(S, "EXE-09", "Empty suites and empty runs succeed", async ct =>
                {
                    TestRunSummary suite = await TestExecutor.RunSuiteAsync(Fixtures.Suite("E"), null, null, ct);
                    TestRunSummary run = await TestExecutor.RunAsync(Array.Empty<TestSuiteDescriptor>(), null, null, ct);
                    TestAssert.True(suite.IsSuccess && suite.Total == 0, "suite");
                    TestAssert.True(run.IsSuccess && run.Total == 0, "run");
                }),
                Check.Async(S, "EXE-10", "The run summary aggregates suite summaries", async ct =>
                {
                    RecordingSink sink = new RecordingSink();
                    TestRunSummary run = await TestExecutor.RunAsync(new[]
                    {
                        Fixtures.Suite("A", Fixtures.Passing("A", "1"), Fixtures.Failing("A", "2")),
                        Fixtures.Suite("B", Fixtures.Skipped("B", "1"), Fixtures.Passing("B", "2")),
                    }, sink, null, ct);
                    TestAssert.Equal(4, run.Total, "total");
                    TestAssert.Equal(2, run.Passed, "passed");
                    TestAssert.Equal(1, run.Failed, "failed");
                    TestAssert.Equal(1, run.Skipped, "skipped");
                    TestAssert.Equal(run.Total, sink.SuiteSummaries.Sum(s => s.Total), "suite totals");
                }),
                Check.Async(S, "EXE-11", "The same suite can run repeatedly with independent results", async ct =>
                {
                    int runs = 0;
                    TestSuiteDescriptor suite = Fixtures.Suite("E", Fixtures.Passing("E", "A", _ => runs++));
                    await TestExecutor.RunSuiteAsync(suite, null, null, ct);
                    TestRunSummary second = await TestExecutor.RunSuiteAsync(suite, null, null, ct);
                    TestAssert.Equal(2, runs, "runs");
                    TestAssert.Equal(1, second.Passed, "second");
                }),
                Check.Async(S, "EXE-12", "Runs without a sink match runs with one", async ct =>
                {
                    TestSuiteDescriptor suite = Fixtures.Suite("E", Fixtures.Passing("E", "A"), Fixtures.Failing("E", "B"));
                    TestRunSummary without = await TestExecutor.RunSuiteAsync(suite, null, null, ct);
                    TestRunSummary with = await TestExecutor.RunSuiteAsync(suite, new RecordingSink(), null, ct);
                    TestAssert.Equal(with.Passed + "/" + with.Failed, without.Passed + "/" + without.Failed, "counts");
                }),
                Check.Async(S, "EXE-13", "The caller's token reaches hooks, cases, steps, and start callbacks", async ct =>
                {
                    using (CancellationTokenSource source = new CancellationTokenSource())
                    {
                        List<CancellationToken> seen = new List<CancellationToken>();
                        TestCaseDescriptor scenario = Fixtures.Scenario("E", "Scn", (c, t) => { seen.Add(t); return Task.CompletedTask; });
                        TestSuiteDescriptor suite = Fixtures.HookedSuite("E", t => { seen.Add(t); return default; }, null,
                            Fixtures.Passing("E", "A", t => seen.Add(t)), scenario);
                        RecordingSink sink = new RecordingSink();
                        await TestExecutor.RunAsync(new[] { suite }, sink, null, source.Token);
                        TestAssert.True(seen.All(t => t == source.Token), "hook, case, step tokens");
                        TestAssert.Equal(3, seen.Count, "observations");
                        TestAssert.True(sink.Tokens[TouchstoneTelemetry.EventSuiteStarted] == source.Token, "suite start token");
                        TestAssert.True(sink.Tokens[TouchstoneTelemetry.EventCaseStarted] == source.Token, "case start token");
                        TestAssert.True(sink.Tokens["step_started"] == source.Token, "step start token");
                    }
                }),
                Check.Async(S, "EXE-14", "ExecuteCaseAsync rethrows the original exception and stack trace", async ct =>
                {
                    InvalidOperationException original = new InvalidOperationException("original");
                    TestCaseDescriptor c = new TestCaseDescriptor("E", "A", "N", _ => Thrower(original));
                    InvalidOperationException thrown = await TestAssert.ThrowsAsync<InvalidOperationException>(() => TestExecutor.ExecuteCaseAsync(c, null, ct), "rethrow");
                    TestAssert.Same(original, thrown, "same instance");
                    TestAssert.Contains(nameof(Thrower), thrown.StackTrace, "original frame kept");
                    await TestExecutor.ExecuteCaseAsync(Fixtures.Passing("E", "B"), null, ct);
                }),
                Check.Async(S, "EXE-15", "ExecuteCaseResultAsync returns failures instead of throwing", async ct =>
                {
                    TestResult r = await TestExecutor.ExecuteCaseResultAsync(Fixtures.Failing("E", "A"), null, null, ct);
                    TestAssert.Equal(TestOutcome.Failed, r.Outcome, "outcome");
                }),
                Check.Async(S, "EXE-16", "ExecuteCaseResultAsync delivers case and step callbacks", async ct =>
                {
                    RecordingSink sink = new RecordingSink();
                    await TestExecutor.ExecuteCaseResultAsync(Fixtures.Scenario("E", "Scn", (c, t) => Task.CompletedTask), sink, null, ct);
                    TestAssert.Equal("case_started:E.Scn|step_started:E.Scn/act|step_completed:E.Scn/act=Passed|case_completed:E.Scn=Passed",
                        string.Join("|", sink.Events), "events");
                }),
                Check.Async(S, "EXE-17", "Single-case entry points do not run suite hooks", async ct =>
                {
                    bool hooked = false;
                    TestSuiteDescriptor suite = Fixtures.HookedSuite("E", _ => { hooked = true; return default; }, _ => { hooked = true; return default; }, Fixtures.Passing("E", "A"));
                    await TestExecutor.ExecuteCaseAsync(suite.Cases[0], null, ct);
                    await TestExecutor.ExecuteCaseResultAsync(suite.Cases[0], null, null, ct);
                    TestAssert.False(hooked, "hooks");
                }),
                Check.Async(S, "EXE-18", "Independent runs execute concurrently without interfering", async ct =>
                {
                    int counter = 0;
                    TestSuiteDescriptor suite = Fixtures.Suite("E", Enumerable.Range(0, 20).Select(i => new TestCaseDescriptor("E", "C" + i, "N", async t =>
                    {
                        Interlocked.Increment(ref counter);
                        await Task.Yield();
                    })).ToArray());
                    TestRunSummary[] runs = await Task.WhenAll(Enumerable.Range(0, 8).Select(_ => TestExecutor.RunSuiteAsync(suite, new TestResultCollector(), null, ct)));
                    TestAssert.True(runs.All(r => r.Passed == 20), "every run passed every case");
                    TestAssert.Equal(160, counter, "executions");
                }),
                Check.Async(S, "EXE-19", "Execution does not deadlock under a single-threaded synchronization context", ct =>
                {
                    Exception? failure = null;
                    Thread thread = new Thread(() =>
                    {
                        SynchronizationContext previous = SynchronizationContext.Current!;
                        SynchronizationContext.SetSynchronizationContext(new BlockingContext());
                        try
                        {
                            TestSuiteDescriptor suite = Fixtures.Suite("E", new TestCaseDescriptor("E", "A", "N", async t => await Task.Delay(1, t).ConfigureAwait(false)));
                            Task<TestRunSummary> run = TestExecutor.RunSuiteAsync(suite, null, null, ct);
                            if (!run.Wait(TimeSpan.FromSeconds(10))) failure = new TimeoutException("Run deadlocked under a blocking synchronization context.");
                            else if (run.Result.Passed != 1) failure = new InvalidOperationException("Case did not pass.");
                        }
                        catch (Exception ex)
                        {
                            failure = ex;
                        }
                        finally
                        {
                            SynchronizationContext.SetSynchronizationContext(previous);
                        }
                    });
                    thread.Start();
                    thread.Join();
                    if (failure != null) ExceptionDispatchInfo.Capture(failure).Throw();
                    return Task.CompletedTask;
                }),
            });
        }

        private static Task Thrower(Exception error)
        {
            throw error;
        }

        /// <summary>
        /// A synchronization context that queues posts and never runs them, which deadlocks any await that resumes on it.
        /// </summary>
        private sealed class BlockingContext : SynchronizationContext
        {
            public override void Post(SendOrPostCallback d, object? state)
            {
                // Intentionally never run: work that needs this context deadlocks, which the test detects.
            }
        }
    }
}
