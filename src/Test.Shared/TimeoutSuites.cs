namespace Test.Shared
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;

    using Touchstone.Core;

    /// <summary>
    /// Catalog section 1.1-A (archive/V1.1_PLAN.md): per-case timeouts.
    /// </summary>
    public static class TimeoutSuites
    {
        private static readonly TimeSpan Short = TimeSpan.FromMilliseconds(100);
        private static readonly TimeSpan Generous = TimeSpan.FromSeconds(30);

        /// <summary>
        /// Every suite in this file.
        /// </summary>
        /// <returns>The suites.</returns>
        public static IEnumerable<TestSuiteDescriptor> All()
        {
            yield return TimeoutSuite();
        }

        private static TestRunOptions WithTimeout(TimeSpan timeout)
        {
            return new TestRunOptions { CaseTimeout = timeout };
        }

        private static TestSuiteDescriptor TimeoutSuite()
        {
            const string S = "CaseTimeout";
            return new TestSuiteDescriptor(S, "Case timeouts", new[]
            {
                Check.Sync(S, "TMO-01", "CaseTimeout defaults to null and rejects zero, negative, and over one day", () =>
                {
                    TestRunOptions options = new TestRunOptions();
                    TestAssert.Equal<TimeSpan?>(null, options.CaseTimeout, "default");
                    TestAssert.Throws<ArgumentOutOfRangeException>(() => options.CaseTimeout = TimeSpan.Zero, "zero");
                    TestAssert.Throws<ArgumentOutOfRangeException>(() => options.CaseTimeout = TimeSpan.FromSeconds(-1), "negative");
                    TestAssert.Throws<ArgumentOutOfRangeException>(() => options.CaseTimeout = TimeSpan.FromDays(1).Add(TimeSpan.FromTicks(1)), "over one day");
                    options.CaseTimeout = TimeSpan.FromDays(1);
                    TestAssert.Equal<TimeSpan?>(TimeSpan.FromDays(1), options.CaseTimeout, "one day");
                    options.CaseTimeout = null;
                    TestAssert.Equal<TimeSpan?>(null, options.CaseTimeout, "back to null");
                }),
                Check.Async(S, "TMO-02", "A case that observes its token fails with CaseTimeoutException when the timeout elapses", async ct =>
                {
                    TestCaseDescriptor c = new TestCaseDescriptor("T", "Wait", "Wait", t => Task.Delay(Timeout.Infinite, t));
                    TestResult r = await TestExecutor.ExecuteCaseResultAsync(c, null, WithTimeout(Short), ct);
                    TestAssert.Equal(TestOutcome.Failed, r.Outcome, "outcome");
                    CaseTimeoutException? error = r.Exception as CaseTimeoutException;
                    TestAssert.NotNull(error, "exception type");
                    TestAssert.Equal("T.Wait", error!.TestId, "test id");
                    TestAssert.Equal(Short, error.Timeout, "timeout");
                    TestAssert.True(error.InnerException is OperationCanceledException, "inner is the case's own exception");
                    TestAssert.Equal(error.Message, r.Message, "result message");
                    TestAssert.Contains("T.Wait exceeded its timeout of 0.1 s", r.Message, "message text");
                }),
                Check.Async(S, "TMO-03", "A case that ignores its token is reported as timed out once it returns", async ct =>
                {
                    TestCaseDescriptor c = new TestCaseDescriptor("T", "Ignore", "Ignore", _ => Task.Delay(TimeSpan.FromMilliseconds(400)));
                    TestResult r = await TestExecutor.ExecuteCaseResultAsync(c, null, WithTimeout(TimeSpan.FromMilliseconds(50)), ct);
                    TestAssert.Equal(TestOutcome.Failed, r.Outcome, "outcome");
                    TestAssert.True(r.Exception is CaseTimeoutException, "type");
                    TestAssert.Null(r.Exception!.InnerException, "no inner exception for a case that returned normally");
                    TestAssert.True(r.Duration >= TimeSpan.FromMilliseconds(350), "the engine waited for the case: " + r.Duration);
                }),
                Check.Async(S, "TMO-04", "A case that finishes within its timeout passes, and the timer does not outlive it", async ct =>
                {
                    CancellationToken seen = default;
                    TestCaseDescriptor c = new TestCaseDescriptor("T", "Fast", "Fast", t =>
                    {
                        seen = t;
                        return Task.CompletedTask;
                    });
                    TestResult r = await TestExecutor.ExecuteCaseResultAsync(c, null, WithTimeout(TimeSpan.FromMilliseconds(200)), ct);
                    TestAssert.Equal(TestOutcome.Passed, r.Outcome, "outcome");
                    TestAssert.True(seen.CanBeCanceled, "the case received a cancelable token");
                    await Task.Delay(400, ct);
                    TestAssert.False(seen.IsCancellationRequested, "the timer was disarmed when the case finished");
                }),
                Check.Async(S, "TMO-05", "Caller cancellation reports Canceled, not a timeout", async ct =>
                {
                    using (CancellationTokenSource source = CancellationTokenSource.CreateLinkedTokenSource(ct))
                    {
                        TestCaseDescriptor c = new TestCaseDescriptor("T", "Wait", "Wait", t =>
                        {
                            source.Cancel();
                            return Task.Delay(Timeout.Infinite, t);
                        });
                        TestResult r = await TestExecutor.ExecuteCaseResultAsync(c, null, WithTimeout(Generous), source.Token);
                        TestAssert.Equal(TestOutcome.Canceled, r.Outcome, "outcome");
                        TestAssert.False(r.Exception is CaseTimeoutException, "not a timeout");
                    }
                }),
                Check.Async(S, "TMO-06", "A timed-out scenario step fails, later steps do not run, and cleanup still runs", async ct =>
                {
                    List<ScenarioContext> contexts = new List<ScenarioContext>();
                    bool cleanupTokenCanceled = true;
                    TestCaseDescriptor c = new ScenarioBuilder<ScenarioContext>("T", "Scn", "Scn", _ =>
                        {
                            ScenarioContext context = new ScenarioContext();
                            contexts.Add(context);
                            return Task.FromResult(context);
                        })
                        .Given("wait", "the step waits on its token", (_, t) => Task.Delay(Timeout.Infinite, t))
                        .Then("never", "the next step", (_, _) => Task.CompletedTask)
                        .WithCleanup((_, t) =>
                        {
                            cleanupTokenCanceled = t.IsCancellationRequested;
                            return Task.CompletedTask;
                        })
                        .Build();
                    TestResult r = await TestExecutor.ExecuteCaseResultAsync(c, null, WithTimeout(Short), ct);
                    TestAssert.Equal(TestOutcome.Failed, r.Outcome, "outcome");
                    TestAssert.True(r.Exception is CaseTimeoutException, "type");
                    TestAssert.Equal(ScenarioStepOutcome.Failed, r.Steps[0].Outcome, "timed-out step is failed, not canceled");
                    TestAssert.Equal(ScenarioStepOutcome.NotRun, r.Steps[1].Outcome, "later step");
                    TestAssert.Equal(1, contexts.Single().AsyncDisposals, "context disposed");
                    TestAssert.False(cleanupTokenCanceled, "cleanup has its own budget");
                }),
                Check.Async(S, "TMO-07", "Scenario cleanup time does not count toward the case timeout", async ct =>
                {
                    TestCaseDescriptor c = new ScenarioBuilder<ScenarioContext>("T", "Slow", "Slow", _ => Task.FromResult(new ScenarioContext()))
                        .When("act", "a fast step", (_, _) => Task.CompletedTask)
                        .WithCleanup((_, _) => Task.Delay(TimeSpan.FromMilliseconds(400)))
                        .Build();
                    TestResult r = await TestExecutor.ExecuteCaseResultAsync(c, null, WithTimeout(TimeSpan.FromMilliseconds(150)), ct);
                    TestAssert.Equal(TestOutcome.Passed, r.Outcome, "outcome");
                }),
                Check.Async(S, "TMO-08", "A timed-out case is a failure for the run: later cases run, and StopOnFirstFailure stops", async ct =>
                {
                    TestCaseDescriptor wait = new TestCaseDescriptor("T", "Wait", "Wait", t => Task.Delay(Timeout.Infinite, t));
                    TestRunOptions options = WithTimeout(Short);
                    TestRunSummary summary = await TestExecutor.RunSuiteAsync(Fixtures.Suite("T", wait, Fixtures.Passing("T", "Next")), null, options, ct);
                    TestAssert.Equal(1, summary.Failed, "failed");
                    TestAssert.Equal(1, summary.Passed, "next case ran");
                    TestAssert.False(summary.WasCanceled, "not a canceled run");

                    options.StopOnFirstFailure = true;
                    summary = await TestExecutor.RunSuiteAsync(Fixtures.Suite("T", wait, Fixtures.Passing("T", "Next")), null, options, ct);
                    TestAssert.Equal(1, summary.Failed, "failed");
                    TestAssert.Equal(1, summary.NotRun, "stopped");
                }),
                Check.Async(S, "TMO-09", "ExecuteCaseAsync throws CaseTimeoutException for a timed-out case", async ct =>
                {
                    TestCaseDescriptor c = new TestCaseDescriptor("T", "Wait", "Wait", t => Task.Delay(Timeout.Infinite, t));
                    CaseTimeoutException error = await TestAssert.ThrowsAsync<CaseTimeoutException>(() => TestExecutor.ExecuteCaseAsync(c, WithTimeout(Short), ct), "throws");
                    TestAssert.Equal("T.Wait", error.TestId, "test id");
                }),
                Check.Async(S, "TMO-10", "Suite hooks are not covered by the case timeout", async ct =>
                {
                    TestSuiteDescriptor suite = Fixtures.HookedSuite("T",
                        async t => await Task.Delay(TimeSpan.FromMilliseconds(300), t).ConfigureAwait(false),
                        async t => await Task.Delay(TimeSpan.FromMilliseconds(300), t).ConfigureAwait(false),
                        Fixtures.Passing("T", "A"));
                    Stopwatch watch = Stopwatch.StartNew();
                    TestRunSummary summary = await TestExecutor.RunSuiteAsync(suite, null, WithTimeout(TimeSpan.FromMilliseconds(100)), ct);
                    TestAssert.Equal(1, summary.Passed, "passed");
                    TestAssert.Equal(0, summary.LifecycleErrors.Count, "hooks did not time out");
                    TestAssert.True(watch.Elapsed >= TimeSpan.FromMilliseconds(500), "hooks ran to completion");
                }),
                Check.Sync(S, "TMO-11", "CaseTimeoutException follows the exception conventions", () =>
                {
                    TestAssert.True(new CaseTimeoutException() is TimeoutException, "is a TimeoutException");
                    TestAssert.Equal(string.Empty, new CaseTimeoutException().TestId, "default id");
                    TestAssert.Equal("m", new CaseTimeoutException("m").Message, "message");
                    InvalidOperationException inner = new InvalidOperationException("inner");
                    TestAssert.Same(inner, new CaseTimeoutException("m", inner).InnerException, "inner");
                    CaseTimeoutException full = new CaseTimeoutException("S.C", TimeSpan.FromSeconds(1.5), inner);
                    TestAssert.Equal("Case S.C exceeded its timeout of 1.5 s.", full.Message, "formatted message");
                    TestAssert.Equal(TimeSpan.FromSeconds(1.5), full.Timeout, "timeout");
                }),
            });
        }
    }
}
