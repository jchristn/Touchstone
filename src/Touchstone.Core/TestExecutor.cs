namespace Touchstone.Core
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// Runs suites and cases and reports results through an optional sink.
    /// Case failures, cancellation, and failed suite hooks are reported as results, not thrown. Only invalid arguments,
    /// duplicate identities, and exceptions thrown by a sink escape the multi-case entry points.
    /// Every run, suite, lifecycle stage, case, and sink callback emits spans on the
    /// <see cref="TouchstoneTelemetry.ActivitySourceName"/> source and metrics on the
    /// <see cref="TouchstoneTelemetry.MeterName"/> meter. See TELEMETRY.md.
    /// Thread safety: all members are safe to call concurrently. Each call is an independent run.
    /// </summary>
    public static class TestExecutor
    {
        private const string MessageCanceled = "Not run: the run was canceled.";
        private const string MessageStopped = "Not run: an earlier case failed and StopOnFirstFailure is set.";
        private const string MessageFiltered = "Not run: the case is excluded by the filter.";
        private const string MessageOmitted = "Not run: the case is skipped and SkipBehavior is Omit.";

        /// <summary>
        /// Execute several suites in order and aggregate their results.
        /// </summary>
        /// <param name="suites">Suites to run, in order. Suite identities must be unique.</param>
        /// <param name="sink">Optional sink for live results. Null runs without one.</param>
        /// <param name="options">Run settings. Null uses the defaults.</param>
        /// <param name="cancellationToken">Caller cancellation. When canceled, the in-flight case finishes or cancels,
        /// the current suite's teardown hook runs, remaining cases are reported as <see cref="TestOutcome.NotRun"/>,
        /// and the returned summary has <see cref="TestRunSummary.WasCanceled"/> set. This method does not throw
        /// <see cref="OperationCanceledException"/> for caller cancellation.</param>
        /// <returns>The aggregated summary, which the sink also receives through
        /// <see cref="ITestResultSink.OnRunCompletedAsync"/>.</returns>
        /// <exception cref="ArgumentNullException">The suite list is null.</exception>
        /// <exception cref="ArgumentException">The suite list contains null, or the filter contains an invalid value.</exception>
        /// <exception cref="DuplicateTestIdException">Two suites share an identity.</exception>
        public static async Task<TestRunSummary> RunAsync(
            IReadOnlyList<TestSuiteDescriptor> suites,
            ITestResultSink? sink = null,
            TestRunOptions? options = null,
            CancellationToken cancellationToken = default)
        {
            if (suites == null) throw new ArgumentNullException(nameof(suites));
            ValidateSuites(suites);
            TestRunOptions snapshot = (options ?? new TestRunOptions()).Snapshot();

            RunState state = new RunState();
            Activity? activity = TouchstoneInstrumentation.StartRun(suites.Count);
            Stopwatch stopwatch = Stopwatch.StartNew();
            SummaryBuilder total = new SummaryBuilder();

            try
            {
                if (sink != null)
                {
                    await InvokeSinkAsync(string.Empty, TouchstoneTelemetry.EventRunStarted,
                        () => sink.OnRunStartedAsync(suites, cancellationToken)).ConfigureAwait(false);
                }

                foreach (TestSuiteDescriptor suite in suites)
                {
                    if (!state.Halted && cancellationToken.IsCancellationRequested) state.Canceled = true;

                    if (state.Halted)
                    {
                        Selection skipped = Select(suite, snapshot);
                        total.NotRun += skipped.Cases.Count;
                        total.Filtered += skipped.Filtered;
                        continue;
                    }

                    TestRunSummary summary = await RunSuiteCoreAsync(suite, sink, snapshot, state, cancellationToken).ConfigureAwait(false);
                    total.Add(summary);
                }

                stopwatch.Stop();
                total.Duration = stopwatch.Elapsed;
                total.WasCanceled = state.Canceled;
                TestRunSummary result = total.Build();

                if (sink != null)
                {
                    await InvokeSinkAsync(string.Empty, TouchstoneTelemetry.EventRunCompleted,
                        () => sink.OnRunCompletedAsync(result, CancellationToken.None)).ConfigureAwait(false);
                }

                TouchstoneInstrumentation.CompleteRun(activity, result, stopwatch.Elapsed, null);
                return result;
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                total.Duration = stopwatch.Elapsed;
                total.WasCanceled = state.Canceled;
                TouchstoneInstrumentation.CompleteRun(activity, total.Build(), stopwatch.Elapsed, ex);
                throw;
            }
        }

        /// <summary>
        /// Execute every selected case in one suite. Run-level sink callbacks are not called.
        /// </summary>
        /// <param name="suite">Suite to run.</param>
        /// <param name="sink">Optional sink for live results. Null runs without one.</param>
        /// <param name="options">Run settings. Null uses the defaults.</param>
        /// <param name="cancellationToken">Caller cancellation, with the same behavior as in <see cref="RunAsync"/>.</param>
        /// <returns>The suite summary.</returns>
        /// <exception cref="ArgumentNullException">The suite is null.</exception>
        /// <exception cref="ArgumentException">The filter contains an invalid value.</exception>
        public static Task<TestRunSummary> RunSuiteAsync(
            TestSuiteDescriptor suite,
            ITestResultSink? sink = null,
            TestRunOptions? options = null,
            CancellationToken cancellationToken = default)
        {
            if (suite == null) throw new ArgumentNullException(nameof(suite));
            TestRunOptions snapshot = (options ?? new TestRunOptions()).Snapshot();
            return RunSuiteCoreAsync(suite, sink, snapshot, new RunState(), cancellationToken);
        }

        /// <summary>
        /// Execute one case and return its result without throwing for case failures. Suite hooks do not run.
        /// A case the filter excludes, or a skipped case under <see cref="SkipBehavior.Omit"/>, returns
        /// <see cref="TestOutcome.NotRun"/> without running.
        /// </summary>
        /// <param name="testCase">Case to execute.</param>
        /// <param name="sink">Optional sink. Receives case and step callbacks only.</param>
        /// <param name="options">Run settings. Null uses the defaults.</param>
        /// <param name="cancellationToken">Caller cancellation. A case that observes it returns <see cref="TestOutcome.Canceled"/>.</param>
        /// <returns>The case result.</returns>
        /// <exception cref="ArgumentNullException">The case is null.</exception>
        /// <exception cref="ArgumentException">The filter contains an invalid value.</exception>
        /// <remarks>An exception thrown by the sink propagates unchanged.</remarks>
        public static async Task<TestResult> ExecuteCaseResultAsync(
            TestCaseDescriptor testCase,
            ITestResultSink? sink = null,
            TestRunOptions? options = null,
            CancellationToken cancellationToken = default)
        {
            if (testCase == null) throw new ArgumentNullException(nameof(testCase));
            TestRunOptions snapshot = (options ?? new TestRunOptions()).Snapshot();

            TestResult result;
            if (!snapshot.Filter.Matches(testCase))
                result = NotRunResult(testCase, MessageFiltered);
            else if (testCase.IsSkipped && snapshot.SkipBehavior == SkipBehavior.Omit)
                result = NotRunResult(testCase, MessageOmitted);
            else
                result = await RunCaseCoreAsync(testCase, sink, snapshot, cancellationToken).ConfigureAwait(false);

            if (sink != null)
            {
                await InvokeSinkAsync(testCase.SuiteId, TouchstoneTelemetry.EventCaseCompleted,
                    () => sink.OnCaseCompletedAsync(result, CancellationToken.None)).ConfigureAwait(false);
            }

            return result;
        }

        /// <summary>
        /// Execute one case and throw if it does not pass. Use this from theory-style hosts so each case emits its span
        /// and metrics. Suite hooks do not run.
        /// Returns normally for <see cref="TestOutcome.Passed"/>, <see cref="TestOutcome.Skipped"/>, and
        /// <see cref="TestOutcome.NotRun"/> (filtered or omitted).
        /// </summary>
        /// <param name="testCase">Case to execute.</param>
        /// <param name="options">Run settings. Null uses the defaults.</param>
        /// <param name="cancellationToken">Caller cancellation.</param>
        /// <returns>A task that completes when the case passes, is skipped, or is not selected.</returns>
        /// <exception cref="ArgumentNullException">The case is null.</exception>
        /// <exception cref="ArgumentException">The filter contains an invalid value.</exception>
        /// <exception cref="OperationCanceledException">The case observed caller cancellation.</exception>
        /// <exception cref="SkippedCaseNotAllowedException">The case is skipped and the skip behavior is <see cref="SkipBehavior.Fail"/>.</exception>
        /// <remarks>For a failed case, the case's original exception is rethrown with its original stack trace.</remarks>
        public static async Task ExecuteCaseAsync(
            TestCaseDescriptor testCase,
            TestRunOptions? options = null,
            CancellationToken cancellationToken = default)
        {
            TestResult result = await ExecuteCaseResultAsync(testCase, null, options, cancellationToken).ConfigureAwait(false);
            TestRunAssert.ThrowIfNotPassed(result, cancellationToken);
        }

        internal static bool IsCallerCancellation(Exception exception, CancellationToken cancellationToken)
        {
            return exception is OperationCanceledException && cancellationToken.IsCancellationRequested;
        }

        private static void ValidateSuites(IReadOnlyList<TestSuiteDescriptor> suites)
        {
            HashSet<string> suiteIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (TestSuiteDescriptor? suite in suites)
            {
                if (suite == null) throw new ArgumentException("The suite list contains null.", nameof(suites));
                if (!suiteIds.Add(suite.SuiteId))
                    throw new DuplicateTestIdException(
                        suite.SuiteId,
                        "The run contains more than one suite with identity " + suite.SuiteId + ".",
                        nameof(suites));
            }
        }

        private static Selection Select(TestSuiteDescriptor suite, TestRunOptions options)
        {
            Selection selection = new Selection();
            foreach (TestCaseDescriptor testCase in suite.Cases)
            {
                if (!options.Filter.Matches(testCase) || (testCase.IsSkipped && options.SkipBehavior == SkipBehavior.Omit))
                    selection.Filtered++;
                else
                    selection.Cases.Add(testCase);
            }

            if (selection.Filtered > 0) TouchstoneInstrumentation.RecordFiltered(suite.SuiteId, selection.Filtered);
            return selection;
        }

        private static async Task<TestRunSummary> RunSuiteCoreAsync(
            TestSuiteDescriptor suite,
            ITestResultSink? sink,
            TestRunOptions options,
            RunState state,
            CancellationToken cancellationToken)
        {
            Selection selection = Select(suite, options);
            SummaryBuilder counts = new SummaryBuilder();
            counts.Filtered = selection.Filtered;
            if (selection.Cases.Count == 0) return counts.Build();

            Activity? activity = TouchstoneInstrumentation.StartSuite(suite);
            Stopwatch stopwatch = Stopwatch.StartNew();
            List<SuiteLifecycleError> lifecycleErrors = new List<SuiteLifecycleError>();
            Exception? setupError = null;

            try
            {
                if (sink != null)
                {
                    await InvokeSinkAsync(suite.SuiteId, TouchstoneTelemetry.EventSuiteStarted,
                        () => sink.OnSuiteStartedAsync(suite, cancellationToken)).ConfigureAwait(false);
                }

                try
                {
                    if (cancellationToken.IsCancellationRequested)
                    {
                        state.Canceled = true;
                    }
                    else if (suite.BeforeSuiteAsync != null)
                    {
                        try
                        {
                            await RunStageAsync(suite.SuiteId, TouchstoneTelemetry.StageBeforeSuite,
                                suite.BeforeSuiteAsync, cancellationToken).ConfigureAwait(false);
                        }
                        catch (Exception ex) when (IsCallerCancellation(ex, cancellationToken))
                        {
                            state.Canceled = true;
                        }
                        catch (Exception ex)
                        {
                            setupError = ex;
                            lifecycleErrors.Add(new SuiteLifecycleError(suite.SuiteId, TouchstoneTelemetry.StageBeforeSuite, ex));
                        }
                    }

                    foreach (TestCaseDescriptor testCase in selection.Cases)
                    {
                        if (!state.Halted && cancellationToken.IsCancellationRequested) state.Canceled = true;

                        TestResult result;
                        if (state.Canceled)
                            result = NotRunResult(testCase, MessageCanceled);
                        else if (state.Stopped)
                            result = NotRunResult(testCase, MessageStopped);
                        else if (setupError != null && !(testCase.IsSkipped && options.SkipBehavior == SkipBehavior.Report))
                            result = SetupFailedResult(testCase, setupError);
                        else
                            result = await RunCaseCoreAsync(testCase, sink, options, cancellationToken).ConfigureAwait(false);

                        if (result.Outcome == TestOutcome.Canceled) state.Canceled = true;
                        if (result.Outcome == TestOutcome.Failed && options.StopOnFirstFailure) state.Stopped = true;
                        if (result.Outcome == TestOutcome.NotRun) TouchstoneInstrumentation.RecordNotRun(suite.SuiteId);

                        counts.Count(result.Outcome);

                        if (sink != null)
                        {
                            await InvokeSinkAsync(suite.SuiteId, TouchstoneTelemetry.EventCaseCompleted,
                                () => sink.OnCaseCompletedAsync(result, CancellationToken.None)).ConfigureAwait(false);
                        }
                    }
                }
                finally
                {
                    if (suite.AfterSuiteAsync != null)
                    {
                        Exception? teardownError = await RunTeardownAsync(suite, options, cancellationToken).ConfigureAwait(false);
                        if (teardownError != null)
                            lifecycleErrors.Add(new SuiteLifecycleError(suite.SuiteId, TouchstoneTelemetry.StageAfterSuite, teardownError));
                    }
                }

                stopwatch.Stop();
                counts.Duration = stopwatch.Elapsed;
                counts.WasCanceled = state.Canceled;
                counts.LifecycleErrors.AddRange(lifecycleErrors);
                TestRunSummary summary = counts.Build();

                if (sink != null)
                {
                    await InvokeSinkAsync(suite.SuiteId, TouchstoneTelemetry.EventSuiteCompleted,
                        () => sink.OnSuiteCompletedAsync(suite, summary, CancellationToken.None)).ConfigureAwait(false);
                }

                TouchstoneInstrumentation.CompleteSuite(activity, suite.SuiteId, summary, stopwatch.Elapsed, null);
                return summary;
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                counts.Duration = stopwatch.Elapsed;
                counts.WasCanceled = state.Canceled;
                counts.LifecycleErrors.AddRange(lifecycleErrors);
                TouchstoneInstrumentation.CompleteSuite(activity, suite.SuiteId, counts.Build(), stopwatch.Elapsed, ex);
                throw;
            }
        }

        private static async Task<Exception?> RunTeardownAsync(
            TestSuiteDescriptor suite,
            TestRunOptions options,
            CancellationToken cancellationToken)
        {
            // The teardown hook gets its own token. It is canceled only once the cleanup budget has elapsed after the
            // caller cancels, so teardown can finish even when the run is being canceled.
            using (CancellationTokenSource budget = new CancellationTokenSource())
            using (CancellationTokenRegistration registration = cancellationToken.Register(
                state => ((CancellationTokenSource)state!).CancelAfter(options.CleanupTimeout), budget))
            {
                try
                {
                    await RunStageAsync(suite.SuiteId, TouchstoneTelemetry.StageAfterSuite,
                        suite.AfterSuiteAsync!, budget.Token).ConfigureAwait(false);
                    return null;
                }
                catch (Exception ex)
                {
                    return ex;
                }
            }
        }

        private static async Task<TestResult> RunCaseCoreAsync(
            TestCaseDescriptor testCase,
            ITestResultSink? sink,
            TestRunOptions options,
            CancellationToken cancellationToken)
        {
            if (testCase.IsSkipped && options.SkipBehavior == SkipBehavior.Fail)
            {
                SkippedCaseNotAllowedException error = new SkippedCaseNotAllowedException(testCase.TestId, testCase.SkipReason!);
                Activity? skipActivity = TouchstoneInstrumentation.StartCase(testCase);
                TouchstoneInstrumentation.CompleteCase(skipActivity, testCase.SuiteId, TouchstoneTelemetry.OutcomeFailed, TimeSpan.Zero, error, false);
                return new TestResult(testCase.SuiteId, testCase.CaseId, testCase.DisplayName, TestOutcome.Failed, TimeSpan.Zero,
                    error.Message, error, testCase.Tags, testCase.Metadata);
            }

            Activity? activity = TouchstoneInstrumentation.StartCase(testCase);
            bool active = !testCase.IsSkipped;
            Stopwatch stopwatch = Stopwatch.StartNew();
            TestResult? result = null;
            Exception? escaped = null;

            try
            {
                if (testCase.IsSkipped && testCase.Scenario == null)
                {
                    result = new TestResult(testCase.SuiteId, testCase.CaseId, testCase.DisplayName, TestOutcome.Skipped,
                        TimeSpan.Zero, testCase.SkipReason, null, testCase.Tags);
                    return result;
                }

                if (active)
                {
                    TouchstoneInstrumentation.CaseStarted(testCase.SuiteId);
                    if (sink != null)
                    {
                        await InvokeSinkAsync(testCase.SuiteId, TouchstoneTelemetry.EventCaseStarted,
                            () => sink.OnCaseStartedAsync(testCase, cancellationToken)).ConfigureAwait(false);
                    }
                }

                if (testCase.Scenario != null)
                {
                    result = await testCase.Scenario.ExecuteAsync(testCase, sink, cancellationToken).ConfigureAwait(false);
                    return result;
                }

                result = await RunBodyAsync(testCase, cancellationToken, stopwatch).ConfigureAwait(false);
                return result;
            }
            catch (Exception ex)
            {
                escaped = ex;
                throw;
            }
            finally
            {
                stopwatch.Stop();
                string outcome = escaped != null ? TouchstoneTelemetry.OutcomeError : TelemetryOutcome(result!.Outcome);
                TimeSpan elapsed = result?.Duration ?? stopwatch.Elapsed;
                TouchstoneInstrumentation.CompleteCase(activity, testCase.SuiteId, outcome, elapsed, escaped ?? result?.Exception, active);
            }
        }

        private static async Task<TestResult> RunBodyAsync(TestCaseDescriptor testCase, CancellationToken cancellationToken, Stopwatch stopwatch)
        {
            try
            {
                Task? task = testCase.Body!(cancellationToken);
                if (task == null) throw new InvalidOperationException("The execution delegate of " + testCase.TestId + " returned a null task.");
                await task.ConfigureAwait(false);
                stopwatch.Stop();
                return new TestResult(testCase.SuiteId, testCase.CaseId, testCase.DisplayName, TestOutcome.Passed,
                    stopwatch.Elapsed, null, null, testCase.Tags);
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                TestOutcome outcome = IsCallerCancellation(ex, cancellationToken) ? TestOutcome.Canceled : TestOutcome.Failed;
                return new TestResult(testCase.SuiteId, testCase.CaseId, testCase.DisplayName, outcome,
                    stopwatch.Elapsed, ex.Message, ex, testCase.Tags);
            }
        }

        private static TestResult NotRunResult(TestCaseDescriptor testCase, string message)
        {
            return new TestResult(testCase.SuiteId, testCase.CaseId, testCase.DisplayName, TestOutcome.NotRun, TimeSpan.Zero,
                message, null, testCase.Tags, testCase.Metadata);
        }

        private static TestResult SetupFailedResult(TestCaseDescriptor testCase, Exception setupError)
        {
            return new TestResult(testCase.SuiteId, testCase.CaseId, testCase.DisplayName, TestOutcome.Failed, TimeSpan.Zero,
                "Suite setup (before_suite) failed: " + setupError.Message, setupError, testCase.Tags, testCase.Metadata);
        }

        private static string TelemetryOutcome(TestOutcome outcome)
        {
            switch (outcome)
            {
                case TestOutcome.Passed: return TouchstoneTelemetry.OutcomePassed;
                case TestOutcome.Skipped: return TouchstoneTelemetry.OutcomeSkipped;
                case TestOutcome.Canceled: return TouchstoneTelemetry.OutcomeCanceled;
                case TestOutcome.NotRun: return TouchstoneTelemetry.OutcomeNotRun;
                default: return TouchstoneTelemetry.OutcomeFailed;
            }
        }

        private static async Task RunStageAsync(
            string suiteId,
            string stage,
            Func<CancellationToken, ValueTask> hook,
            CancellationToken cancellationToken)
        {
            Activity? activity = TouchstoneInstrumentation.StartStage(suiteId, stage);
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
            Activity? activity = TouchstoneInstrumentation.StartSink(suiteId, sinkEvent);
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

        private sealed class RunState
        {
            internal bool Canceled { get; set; }

            internal bool Stopped { get; set; }

            internal bool Halted
            {
                get { return Canceled || Stopped; }
            }
        }

        private sealed class Selection
        {
            internal List<TestCaseDescriptor> Cases { get; } = new List<TestCaseDescriptor>();

            internal int Filtered { get; set; }
        }

        private sealed class SummaryBuilder
        {
            internal int Passed { get; set; }

            internal int Failed { get; set; }

            internal int Skipped { get; set; }

            internal int Canceled { get; set; }

            internal int NotRun { get; set; }

            internal int Filtered { get; set; }

            internal TimeSpan Duration { get; set; }

            internal bool WasCanceled { get; set; }

            internal List<SuiteLifecycleError> LifecycleErrors { get; } = new List<SuiteLifecycleError>();

            internal void Count(TestOutcome outcome)
            {
                switch (outcome)
                {
                    case TestOutcome.Passed: Passed++; break;
                    case TestOutcome.Failed: Failed++; break;
                    case TestOutcome.Skipped: Skipped++; break;
                    case TestOutcome.Canceled: Canceled++; break;
                    default: NotRun++; break;
                }
            }

            internal void Add(TestRunSummary summary)
            {
                Passed += summary.Passed;
                Failed += summary.Failed;
                Skipped += summary.Skipped;
                Canceled += summary.Canceled;
                NotRun += summary.NotRun;
                Filtered += summary.Filtered;
                LifecycleErrors.AddRange(summary.LifecycleErrors);
            }

            internal TestRunSummary Build()
            {
                return new TestRunSummary(Passed, Failed, Skipped, Canceled, NotRun, Filtered, Duration, WasCanceled, LifecycleErrors);
            }
        }
    }
}
