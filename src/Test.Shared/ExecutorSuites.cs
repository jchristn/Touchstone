namespace Test.Shared
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using Touchstone.Core;

    /// <summary>
    /// Suites covering <see cref="TestExecutor"/>.
    /// </summary>
    public static class ExecutorSuites
    {
        /// <summary>
        /// Suite covering <see cref="TestExecutor.RunSuiteAsync"/>.
        /// </summary>
        /// <returns>Suite descriptor.</returns>
        public static TestSuiteDescriptor TestExecutorSuite()
        {
            const string S = "TestExecutor";

            return new TestSuiteDescriptor(S, "TestExecutor", new List<TestCaseDescriptor>
            {
                CaseBuilder.Async(S, "NullSuiteThrows", "RunSuiteAsync rejects a null suite", async ct =>
                {
                    ArgumentNullException ex = await TestAssert.ThrowsAsync<ArgumentNullException>(
                        () => TestExecutor.RunSuiteAsync(null!, null, ct), "null suite").ConfigureAwait(false);
                    TestAssert.Equal("suite", ex.ParamName, "ParamName");
                }),

                CaseBuilder.Async(S, "PassingCase", "RunSuiteAsync reports a passing case with full identity", async ct =>
                {
                    RecordingSink sink = new RecordingSink();
                    TestRunSummary summary = await TestExecutor.RunSuiteAsync(
                        Fixtures.Suite("X", Fixtures.Passing("X", "Ok")), sink, ct).ConfigureAwait(false);

                    TestAssert.True(summary.IsSuccess, "IsSuccess");
                    TestAssert.Equal(1, summary.Total, "Total");
                    TestAssert.Equal(1, summary.Passed, "Passed");
                    TestAssert.Equal(0, summary.Failed, "Failed");
                    TestAssert.Equal(0, summary.Skipped, "Skipped");

                    TestResult r = sink.Results[0];
                    TestAssert.Equal("X.Ok", r.TestId, "TestId");
                    TestAssert.Equal("X", r.SuiteId, "SuiteId");
                    TestAssert.Equal("Ok", r.CaseId, "CaseId");
                    TestAssert.Equal("Ok display", r.DisplayName, "DisplayName");
                    TestAssert.True(r.Success, "Success");
                    TestAssert.False(r.Skipped, "Skipped");
                    TestAssert.Null(r.Exception, "Exception");
                    TestAssert.Null(r.Message, "Message");
                    TestAssert.True(r.Duration >= TimeSpan.Zero, "Duration is non-negative");
                }),

                CaseBuilder.Async(S, "FailingCaseCapturesException", "RunSuiteAsync captures the thrown exception and message", async ct =>
                {
                    InvalidOperationException thrown = new InvalidOperationException("boom", new FormatException("inner"));
                    TestCaseDescriptor failing = new TestCaseDescriptor("X", "Bad", "Bad", _ => throw thrown);
                    RecordingSink sink = new RecordingSink();

                    TestRunSummary summary = await TestExecutor.RunSuiteAsync(Fixtures.Suite("X", failing), sink, ct).ConfigureAwait(false);

                    TestAssert.False(summary.IsSuccess, "IsSuccess");
                    TestAssert.Equal(1, summary.Failed, "Failed");
                    TestResult r = sink.Results[0];
                    TestAssert.False(r.Success, "Success");
                    TestAssert.False(r.Skipped, "Skipped");
                    TestAssert.Same(thrown, r.Exception, "Exception");
                    TestAssert.Equal("boom", r.Message, "Message");
                    TestAssert.True(r.Exception!.InnerException is FormatException, "InnerException preserved");
                    TestAssert.True(r.Duration >= TimeSpan.Zero, "Duration is non-negative");
                }),

                CaseBuilder.Async(S, "AsyncFailureCaptured", "RunSuiteAsync captures exceptions thrown after an await", async ct =>
                {
                    TestCaseDescriptor failing = new TestCaseDescriptor("X", "Late", "Late", async _ =>
                    {
                        await Task.Yield();
                        throw new NotSupportedException("late failure");
                    });
                    RecordingSink sink = new RecordingSink();

                    TestRunSummary summary = await TestExecutor.RunSuiteAsync(Fixtures.Suite("X", failing), sink, ct).ConfigureAwait(false);

                    TestAssert.Equal(1, summary.Failed, "Failed");
                    TestAssert.True(sink.Results[0].Exception is NotSupportedException, "Exception type");
                    TestAssert.Equal("late failure", sink.Results[0].Message, "Message");
                }),

                CaseBuilder.Async(S, "CanceledCaseIsFailure", "RunSuiteAsync records an OperationCanceledException from a case as a failure", async ct =>
                {
                    TestCaseDescriptor canceled = new TestCaseDescriptor("X", "Cancel", "Cancel",
                        _ => throw new OperationCanceledException("case canceled"));
                    RecordingSink sink = new RecordingSink();

                    TestRunSummary summary = await TestExecutor.RunSuiteAsync(
                        Fixtures.Suite("X", canceled, Fixtures.Passing("X", "After")), sink, ct).ConfigureAwait(false);

                    TestAssert.Equal(1, summary.Failed, "Failed");
                    TestAssert.Equal(1, summary.Passed, "Passed");
                    TestAssert.True(sink.Results[0].Exception is OperationCanceledException, "Exception type");
                }),

                CaseBuilder.Async(S, "SkippedWithReason", "RunSuiteAsync reports skipped cases with their reason and does not execute them", async ct =>
                {
                    RecordingSink sink = new RecordingSink();
                    TestRunSummary summary = await TestExecutor.RunSuiteAsync(
                        Fixtures.Suite("X", Fixtures.Skipped("X", "Later", "pending feature")), sink, ct).ConfigureAwait(false);

                    TestAssert.True(summary.IsSuccess, "IsSuccess");
                    TestAssert.Equal(1, summary.Total, "Total");
                    TestAssert.Equal(1, summary.Skipped, "Skipped");
                    TestAssert.Equal(0, summary.Passed, "Passed");
                    TestResult r = sink.Results[0];
                    TestAssert.True(r.Skipped, "Skipped");
                    TestAssert.True(r.Success, "Success");
                    TestAssert.Equal("pending feature", r.Message, "Message");
                    TestAssert.Null(r.Exception, "Exception");
                    TestAssert.Equal(TimeSpan.Zero, r.Duration, "Duration");
                    TestAssert.Equal("X.Later", r.TestId, "TestId");
                }),

                CaseBuilder.Async(S, "SkippedWithoutReason", "RunSuiteAsync uses 'Skipped' as the message when no reason is given", async ct =>
                {
                    RecordingSink sink = new RecordingSink();
                    await TestExecutor.RunSuiteAsync(
                        Fixtures.Suite("X", Fixtures.Skipped("X", "NoReason", null)), sink, ct).ConfigureAwait(false);
                    TestAssert.Equal("Skipped", sink.Results[0].Message, "Message");
                }),

                CaseBuilder.Async(S, "MixedCounts", "RunSuiteAsync aggregates passed, failed, and skipped counts", async ct =>
                {
                    TestSuiteDescriptor suite = Fixtures.Suite("X",
                        Fixtures.Passing("X", "P1"),
                        Fixtures.Failing("X", "F1", "f1"),
                        Fixtures.Skipped("X", "S1"),
                        Fixtures.Passing("X", "P2"),
                        Fixtures.Failing("X", "F2", "f2"));

                    TestRunSummary summary = await TestExecutor.RunSuiteAsync(suite, null, ct).ConfigureAwait(false);

                    TestAssert.Equal(5, summary.Total, "Total");
                    TestAssert.Equal(2, summary.Passed, "Passed");
                    TestAssert.Equal(2, summary.Failed, "Failed");
                    TestAssert.Equal(1, summary.Skipped, "Skipped");
                    TestAssert.False(summary.IsSuccess, "IsSuccess");
                    TestAssert.True(summary.Duration >= TimeSpan.Zero, "Duration is non-negative");
                }),

                CaseBuilder.Async(S, "FailureDoesNotStopSuite", "RunSuiteAsync continues running cases after a failure", async ct =>
                {
                    bool laterRan = false;
                    TestSuiteDescriptor suite = Fixtures.Suite("X",
                        Fixtures.Failing("X", "First", "first"),
                        Fixtures.Passing("X", "Second", _ => laterRan = true));

                    await TestExecutor.RunSuiteAsync(suite, null, ct).ConfigureAwait(false);

                    TestAssert.True(laterRan, "case after failure executed");
                }),

                CaseBuilder.Async(S, "PreservesOrder", "RunSuiteAsync runs cases and reports results in declaration order", async ct =>
                {
                    List<string> executed = new List<string>();
                    TestSuiteDescriptor suite = Fixtures.Suite("X",
                        Fixtures.Passing("X", "A", _ => executed.Add("A")),
                        Fixtures.Skipped("X", "B"),
                        Fixtures.Passing("X", "C", _ => executed.Add("C")));
                    RecordingSink sink = new RecordingSink();

                    await TestExecutor.RunSuiteAsync(suite, sink, ct).ConfigureAwait(false);

                    TestAssert.Equal("A,C", string.Join(",", executed), "execution order");
                    TestAssert.Equal(
                        "start:X,test:X.A,test:X.B,test:X.C,complete:X",
                        string.Join(",", sink.Events),
                        "sink event order");
                }),

                CaseBuilder.Async(S, "EmptySuite", "RunSuiteAsync handles a suite with no cases", async ct =>
                {
                    RecordingSink sink = new RecordingSink();
                    TestRunSummary summary = await TestExecutor.RunSuiteAsync(Fixtures.Suite("Empty"), sink, ct).ConfigureAwait(false);

                    TestAssert.Equal(0, summary.Total, "Total");
                    TestAssert.True(summary.IsSuccess, "IsSuccess");
                    TestAssert.Equal("start:Empty,complete:Empty", string.Join(",", sink.Events), "sink events");
                }),

                CaseBuilder.Async(S, "SinkReceivesSuiteAndSummary", "RunSuiteAsync passes the suite and the returned summary to the sink", async ct =>
                {
                    TestSuiteDescriptor suite = Fixtures.Suite("X", Fixtures.Passing("X", "A"), Fixtures.Failing("X", "B", "b"));
                    RecordingSink sink = new RecordingSink();

                    TestRunSummary summary = await TestExecutor.RunSuiteAsync(suite, sink, ct).ConfigureAwait(false);

                    TestAssert.Same(suite, sink.StartedSuite, "StartedSuite");
                    TestAssert.Same(suite, sink.CompletedSuite, "CompletedSuite");
                    TestAssert.Same(summary, sink.CompletedSummary, "CompletedSummary");
                }),

                CaseBuilder.Async(S, "NullSinkAllowed", "RunSuiteAsync runs without a sink", async ct =>
                {
                    bool ran = false;
                    TestRunSummary summary = await TestExecutor.RunSuiteAsync(
                        Fixtures.Suite("X", Fixtures.Passing("X", "A", _ => ran = true)), null, ct).ConfigureAwait(false);
                    TestAssert.True(ran, "case executed");
                    TestAssert.Equal(1, summary.Passed, "Passed");
                }),

                CaseBuilder.Async(S, "HookOrder", "RunSuiteAsync runs BeforeSuite once before cases and AfterSuite once after", async ct =>
                {
                    List<string> log = new List<string>();
                    TestSuiteDescriptor suite = new TestSuiteDescriptor(
                        "X", "X",
                        new List<TestCaseDescriptor>
                        {
                            Fixtures.Passing("X", "A", _ => log.Add("A")),
                            Fixtures.Passing("X", "B", _ => log.Add("B")),
                        },
                        beforeSuiteAsync: _ => { log.Add("before"); return ValueTask.CompletedTask; },
                        afterSuiteAsync: _ => { log.Add("after"); return ValueTask.CompletedTask; });

                    await TestExecutor.RunSuiteAsync(suite, null, ct).ConfigureAwait(false);

                    TestAssert.Equal("before,A,B,after", string.Join(",", log), "lifecycle order");
                }),

                CaseBuilder.Async(S, "AfterHookRunsWhenCasesFail", "RunSuiteAsync runs AfterSuite even when cases fail", async ct =>
                {
                    bool afterRan = false;
                    TestSuiteDescriptor suite = new TestSuiteDescriptor(
                        "X", "X",
                        new List<TestCaseDescriptor> { Fixtures.Failing("X", "A", "a") },
                        afterSuiteAsync: _ => { afterRan = true; return ValueTask.CompletedTask; });

                    await TestExecutor.RunSuiteAsync(suite, null, ct).ConfigureAwait(false);

                    TestAssert.True(afterRan, "AfterSuite ran");
                }),

                CaseBuilder.Async(S, "AfterHookRunsWhenSinkThrows", "RunSuiteAsync runs AfterSuite and propagates when the sink throws", async ct =>
                {
                    bool afterRan = false;
                    TestSuiteDescriptor suite = new TestSuiteDescriptor(
                        "X", "X",
                        new List<TestCaseDescriptor> { Fixtures.Passing("X", "A"), Fixtures.Passing("X", "B") },
                        afterSuiteAsync: _ => { afterRan = true; return ValueTask.CompletedTask; });
                    RecordingSink sink = new RecordingSink(_ => throw new InvalidOperationException("sink failure"));

                    await TestAssert.ThrowsAsync<InvalidOperationException>(
                        () => TestExecutor.RunSuiteAsync(suite, sink, ct), "sink failure").ConfigureAwait(false);

                    TestAssert.True(afterRan, "AfterSuite ran");
                    TestAssert.Equal(1, sink.Results.Count, "results delivered before failure");
                }),

                CaseBuilder.Async(S, "BeforeHookFailurePropagates", "RunSuiteAsync propagates a BeforeSuite failure without running cases", async ct =>
                {
                    bool caseRan = false;
                    TestSuiteDescriptor suite = new TestSuiteDescriptor(
                        "X", "X",
                        new List<TestCaseDescriptor> { Fixtures.Passing("X", "A", _ => caseRan = true) },
                        beforeSuiteAsync: _ => throw new InvalidOperationException("setup failed"));

                    InvalidOperationException ex = await TestAssert.ThrowsAsync<InvalidOperationException>(
                        () => TestExecutor.RunSuiteAsync(suite, null, ct), "setup failure").ConfigureAwait(false);

                    TestAssert.Equal("setup failed", ex.Message, "Message");
                    TestAssert.False(caseRan, "case executed");
                }),

                CaseBuilder.Async(S, "AfterHookFailurePropagates", "RunSuiteAsync propagates an AfterSuite failure after running cases", async ct =>
                {
                    bool caseRan = false;
                    TestSuiteDescriptor suite = new TestSuiteDescriptor(
                        "X", "X",
                        new List<TestCaseDescriptor> { Fixtures.Passing("X", "A", _ => caseRan = true) },
                        afterSuiteAsync: _ => throw new InvalidOperationException("teardown failed"));

                    await TestAssert.ThrowsAsync<InvalidOperationException>(
                        () => TestExecutor.RunSuiteAsync(suite, null, ct), "teardown failure").ConfigureAwait(false);

                    TestAssert.True(caseRan, "case executed");
                }),

                CaseBuilder.Async(S, "TokenForwarded", "RunSuiteAsync forwards the cancellation token to hooks, cases, and the sink", async _ =>
                {
                    using (CancellationTokenSource cts = new CancellationTokenSource())
                    {
                        CancellationToken token = cts.Token;
                        List<CancellationToken> seen = new List<CancellationToken>();
                        TestSuiteDescriptor suite = new TestSuiteDescriptor(
                            "X", "X",
                            new List<TestCaseDescriptor> { Fixtures.Passing("X", "A", t => seen.Add(t)) },
                            beforeSuiteAsync: t => { seen.Add(t); return ValueTask.CompletedTask; },
                            afterSuiteAsync: t => { seen.Add(t); return ValueTask.CompletedTask; });
                        RecordingSink sink = new RecordingSink();

                        await TestExecutor.RunSuiteAsync(suite, sink, token).ConfigureAwait(false);

                        TestAssert.Equal(3, seen.Count, "hook and case token count");
                        foreach (CancellationToken t in seen)
                            TestAssert.True(t == token, "hook/case received the caller token");
                        TestAssert.Equal(3, sink.Tokens.Count, "sink token count");
                        foreach (CancellationToken t in sink.Tokens)
                            TestAssert.True(t == token, "sink received the caller token");
                    }
                }),

                CaseBuilder.Async(S, "SuiteIsReusable", "RunSuiteAsync can run the same suite more than once", async ct =>
                {
                    int runs = 0;
                    TestSuiteDescriptor suite = Fixtures.Suite("X", Fixtures.Passing("X", "A", _ => runs++));

                    TestRunSummary first = await TestExecutor.RunSuiteAsync(suite, null, ct).ConfigureAwait(false);
                    TestRunSummary second = await TestExecutor.RunSuiteAsync(suite, null, ct).ConfigureAwait(false);

                    TestAssert.Equal(2, runs, "execution count");
                    TestAssert.Equal(1, first.Passed, "first.Passed");
                    TestAssert.Equal(1, second.Passed, "second.Passed");
                }),
            });
        }
    }
}
