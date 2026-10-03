namespace Touchstone.Core
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Runtime.ExceptionServices;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// Runs test suites and reports results through an optional sink.
    /// Every run, suite, lifecycle stage, case, and sink callback emits spans on the
    /// <see cref="TouchstoneTelemetry.ActivitySourceName"/> source and metrics on the
    /// <see cref="TouchstoneTelemetry.MeterName"/> meter. See TELEMETRY.md.
    /// </summary>
    public static class TestExecutor
    {
        /// <summary>
        /// Execute several suites in order and aggregate their results.
        /// Suites run one after another. An exception escaping a suite (from a hook or the sink) stops the run and propagates.
        /// </summary>
        /// <param name="suites">Suites to run, in order.</param>
        /// <param name="sink">Optional sink for real-time result reporting.</param>
        /// <param name="cancellationToken">Cancellation token, forwarded to hooks, cases, and the sink.</param>
        /// <returns>Aggregated summary of every suite.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="suites"/> or any suite in it is null.</exception>
        public static async Task<TestRunSummary> RunAsync(
            IReadOnlyList<TestSuiteDescriptor> suites,
            ITestResultSink sink = null,
            CancellationToken cancellationToken = default)
        {
            if (suites == null) throw new ArgumentNullException(nameof(suites));

            Activity activity = TouchstoneInstrumentation.StartRun(suites.Count);
            Stopwatch stopwatch = Stopwatch.StartNew();
            TestRunSummary total = new TestRunSummary();

            try
            {
                foreach (TestSuiteDescriptor suite in suites)
                {
                    TestRunSummary summary = await RunSuiteAsync(suite, sink, cancellationToken).ConfigureAwait(false);
                    total.Total += summary.Total;
                    total.Passed += summary.Passed;
                    total.Failed += summary.Failed;
                    total.Skipped += summary.Skipped;
                }

                stopwatch.Stop();
                total.Duration = stopwatch.Elapsed;
                TouchstoneInstrumentation.CompleteRun(activity, total, stopwatch.Elapsed, null);
                return total;
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                total.Duration = stopwatch.Elapsed;
                TouchstoneInstrumentation.CompleteRun(activity, total, stopwatch.Elapsed, ex);
                throw;
            }
        }

        /// <summary>
        /// Execute all test cases in a suite.
        /// </summary>
        /// <param name="suite">Suite to run.</param>
        /// <param name="sink">Optional sink for real-time result reporting.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Aggregated summary of the suite run.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="suite"/> is null.</exception>
        public static async Task<TestRunSummary> RunSuiteAsync(
            TestSuiteDescriptor suite,
            ITestResultSink sink = null,
            CancellationToken cancellationToken = default)
        {
            if (suite == null) throw new ArgumentNullException(nameof(suite));

            Activity activity = TouchstoneInstrumentation.StartSuite(suite);
            Stopwatch spanStopwatch = Stopwatch.StartNew();
            Stopwatch totalStopwatch = null;
            int passed = 0;
            int failed = 0;
            int skipped = 0;

            try
            {
                if (sink != null)
                {
                    await InvokeSinkAsync(suite.SuiteId, TouchstoneTelemetry.EventSuiteStarted,
                        () => sink.OnSuiteStartedAsync(suite, cancellationToken)).ConfigureAwait(false);
                }

                totalStopwatch = Stopwatch.StartNew();

                if (suite.BeforeSuiteAsync != null)
                {
                    await RunStageAsync(suite.SuiteId, TouchstoneTelemetry.StageBeforeSuite,
                        suite.BeforeSuiteAsync, cancellationToken).ConfigureAwait(false);
                }

                try
                {
                    foreach (TestCaseDescriptor testCase in suite.Cases)
                    {
                        TestResult result = await RunCaseAsync(testCase, cancellationToken).ConfigureAwait(false);

                        if (result.Skipped)
                            skipped++;
                        else if (result.Success)
                            passed++;
                        else
                            failed++;

                        if (sink != null)
                        {
                            await InvokeSinkAsync(suite.SuiteId, TouchstoneTelemetry.EventTestCompleted,
                                () => sink.OnTestCompletedAsync(result, cancellationToken)).ConfigureAwait(false);
                        }
                    }
                }
                finally
                {
                    if (suite.AfterSuiteAsync != null)
                    {
                        await RunStageAsync(suite.SuiteId, TouchstoneTelemetry.StageAfterSuite,
                            suite.AfterSuiteAsync, cancellationToken).ConfigureAwait(false);
                    }
                }

                totalStopwatch.Stop();

                TestRunSummary summary = new TestRunSummary
                {
                    Total = suite.Cases.Count,
                    Passed = passed,
                    Failed = failed,
                    Skipped = skipped,
                    Duration = totalStopwatch.Elapsed,
                };

                if (sink != null)
                {
                    await InvokeSinkAsync(suite.SuiteId, TouchstoneTelemetry.EventSuiteCompleted,
                        () => sink.OnSuiteCompletedAsync(suite, summary, cancellationToken)).ConfigureAwait(false);
                }

                spanStopwatch.Stop();
                TouchstoneInstrumentation.CompleteSuite(activity, suite.SuiteId, summary, spanStopwatch.Elapsed, null);
                return summary;
            }
            catch (Exception ex)
            {
                spanStopwatch.Stop();
                TestRunSummary partial = new TestRunSummary
                {
                    Total = passed + failed + skipped,
                    Passed = passed,
                    Failed = failed,
                    Skipped = skipped,
                    Duration = totalStopwatch?.Elapsed ?? TimeSpan.Zero,
                };
                TouchstoneInstrumentation.CompleteSuite(activity, suite.SuiteId, partial, spanStopwatch.Elapsed, ex);
                throw;
            }
        }

        /// <summary>
        /// Execute a single test case with full telemetry and rethrow its exception on failure.
        /// Use this from theory-style hosts (xUnit MemberData, NUnit TestCaseSource, MSTest DynamicData) in place of
        /// calling <see cref="TestCaseDescriptor.ExecuteAsync"/> directly, so each case emits its span and metrics.
        /// Skipped cases return without executing. Suite hooks are not run.
        /// </summary>
        /// <param name="testCase">Case to execute.</param>
        /// <param name="cancellationToken">Cancellation token forwarded to the case.</param>
        /// <returns>Task that completes when the case passes or is skipped.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="testCase"/> is null.</exception>
        /// <exception cref="Exception">Rethrows whatever the case body threw, with its original stack trace.</exception>
        public static async Task ExecuteCaseAsync(
            TestCaseDescriptor testCase,
            CancellationToken cancellationToken = default)
        {
            if (testCase == null) throw new ArgumentNullException(nameof(testCase));

            TestResult result = await RunCaseAsync(testCase, cancellationToken).ConfigureAwait(false);
            if (result.Exception != null)
                ExceptionDispatchInfo.Capture(result.Exception).Throw();
        }

        private static async Task<TestResult> RunCaseAsync(
            TestCaseDescriptor testCase,
            CancellationToken cancellationToken)
        {
            Activity activity = TouchstoneInstrumentation.StartCase(testCase);

            if (testCase.Skip)
            {
                TouchstoneInstrumentation.CompleteCase(
                    activity, testCase.SuiteId, TouchstoneTelemetry.OutcomeSkipped, TimeSpan.Zero, null, false);

                return new TestResult
                {
                    TestId = testCase.TestId,
                    SuiteId = testCase.SuiteId,
                    CaseId = testCase.CaseId,
                    DisplayName = testCase.DisplayName,
                    Success = true,
                    Skipped = true,
                    Message = testCase.SkipReason ?? "Skipped",
                };
            }

            TouchstoneInstrumentation.CaseStarted(testCase.SuiteId);
            Stopwatch stopwatch = Stopwatch.StartNew();

            try
            {
                await testCase.ExecuteAsync(cancellationToken).ConfigureAwait(false);
                stopwatch.Stop();

                TouchstoneInstrumentation.CompleteCase(
                    activity, testCase.SuiteId, TouchstoneTelemetry.OutcomePassed, stopwatch.Elapsed, null, true);

                return new TestResult
                {
                    TestId = testCase.TestId,
                    SuiteId = testCase.SuiteId,
                    CaseId = testCase.CaseId,
                    DisplayName = testCase.DisplayName,
                    Success = true,
                    Duration = stopwatch.Elapsed,
                };
            }
            catch (Exception ex)
            {
                stopwatch.Stop();

                TouchstoneInstrumentation.CompleteCase(
                    activity, testCase.SuiteId, TouchstoneTelemetry.OutcomeFailed, stopwatch.Elapsed, ex, true);

                return new TestResult
                {
                    TestId = testCase.TestId,
                    SuiteId = testCase.SuiteId,
                    CaseId = testCase.CaseId,
                    DisplayName = testCase.DisplayName,
                    Success = false,
                    Duration = stopwatch.Elapsed,
                    Exception = ex,
                    Message = ex.Message,
                };
            }
        }

        private static async Task RunStageAsync(
            string suiteId,
            string stage,
            Func<CancellationToken, ValueTask> hook,
            CancellationToken cancellationToken)
        {
            Activity activity = TouchstoneInstrumentation.StartStage(suiteId, stage);
            long start = Stopwatch.GetTimestamp();

            try
            {
                await hook(cancellationToken).ConfigureAwait(false);
                TouchstoneInstrumentation.CompleteStage(activity, suiteId, stage, Stopwatch.GetElapsedTime(start), null);
            }
            catch (Exception ex)
            {
                TouchstoneInstrumentation.CompleteStage(activity, suiteId, stage, Stopwatch.GetElapsedTime(start), ex);
                throw;
            }
        }

        private static async Task InvokeSinkAsync(string suiteId, string sinkEvent, Func<ValueTask> callback)
        {
            Activity activity = TouchstoneInstrumentation.StartSink(suiteId, sinkEvent);
            long start = Stopwatch.GetTimestamp();

            try
            {
                await callback().ConfigureAwait(false);
                TouchstoneInstrumentation.CompleteSink(activity, suiteId, sinkEvent, Stopwatch.GetElapsedTime(start), null);
            }
            catch (Exception ex)
            {
                TouchstoneInstrumentation.CompleteSink(activity, suiteId, sinkEvent, Stopwatch.GetElapsedTime(start), ex);
                throw;
            }
        }
    }
}
