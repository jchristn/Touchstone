namespace Touchstone.Core
{
    using System;
    using System.Diagnostics;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// Runs test suites and reports results through an optional sink.
    /// </summary>
    public static class TestExecutor
    {
        /// <summary>
        /// Execute all test cases in a suite.
        /// </summary>
        /// <param name="suite">Suite to run.</param>
        /// <param name="sink">Optional sink for real-time result reporting.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Aggregated summary of the suite run.</returns>
        public static async Task<TestRunSummary> RunSuiteAsync(
            TestSuiteDescriptor suite,
            ITestResultSink sink = null,
            CancellationToken cancellationToken = default)
        {
            if (suite == null) throw new ArgumentNullException(nameof(suite));

            if (sink != null)
                await sink.OnSuiteStartedAsync(suite, cancellationToken);

            Stopwatch totalStopwatch = Stopwatch.StartNew();
            int passed = 0;
            int failed = 0;
            int skipped = 0;

            if (suite.BeforeSuiteAsync != null)
                await suite.BeforeSuiteAsync(cancellationToken);

            try
            {
                foreach (TestCaseDescriptor testCase in suite.Cases)
                {
                    TestResult result = await RunCaseAsync(testCase, cancellationToken);

                    if (result.Skipped)
                        skipped++;
                    else if (result.Success)
                        passed++;
                    else
                        failed++;

                    if (sink != null)
                        await sink.OnTestCompletedAsync(result, cancellationToken);
                }
            }
            finally
            {
                if (suite.AfterSuiteAsync != null)
                    await suite.AfterSuiteAsync(cancellationToken);
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
                await sink.OnSuiteCompletedAsync(suite, summary, cancellationToken);

            return summary;
        }

        private static async Task<TestResult> RunCaseAsync(
            TestCaseDescriptor testCase,
            CancellationToken cancellationToken)
        {
            if (testCase.Skip)
            {
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

            Stopwatch stopwatch = Stopwatch.StartNew();

            try
            {
                await testCase.ExecuteAsync(cancellationToken);
                stopwatch.Stop();

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
    }
}
