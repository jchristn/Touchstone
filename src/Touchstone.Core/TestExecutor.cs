using System.Diagnostics;

namespace Touchstone.Core;

public static class TestExecutor
{
    public static async Task<TestRunSummary> RunSuiteAsync(
        TestSuiteDescriptor suite,
        ITestResultSink? sink = null,
        CancellationToken cancellationToken = default)
    {
        if (sink != null)
            await sink.OnSuiteStartedAsync(suite, cancellationToken);

        var totalSw = Stopwatch.StartNew();
        int passed = 0, failed = 0, skipped = 0;

        if (suite.BeforeSuiteAsync != null)
            await suite.BeforeSuiteAsync(cancellationToken);

        try
        {
            foreach (var testCase in suite.Cases)
            {
                var result = await RunCaseAsync(testCase, cancellationToken);

                if (result.Skipped) skipped++;
                else if (result.Success) passed++;
                else failed++;

                if (sink != null)
                    await sink.OnTestCompletedAsync(result, cancellationToken);
            }
        }
        finally
        {
            if (suite.AfterSuiteAsync != null)
                await suite.AfterSuiteAsync(cancellationToken);
        }

        totalSw.Stop();

        var summary = new TestRunSummary
        {
            Total = suite.Cases.Count,
            Passed = passed,
            Failed = failed,
            Skipped = skipped,
            Duration = totalSw.Elapsed,
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

        var sw = Stopwatch.StartNew();
        try
        {
            await testCase.ExecuteAsync(cancellationToken);
            sw.Stop();

            return new TestResult
            {
                TestId = testCase.TestId,
                SuiteId = testCase.SuiteId,
                CaseId = testCase.CaseId,
                DisplayName = testCase.DisplayName,
                Success = true,
                Duration = sw.Elapsed,
            };
        }
        catch (Exception ex)
        {
            sw.Stop();

            return new TestResult
            {
                TestId = testCase.TestId,
                SuiteId = testCase.SuiteId,
                CaseId = testCase.CaseId,
                DisplayName = testCase.DisplayName,
                Success = false,
                Duration = sw.Elapsed,
                Exception = ex,
                Message = ex.Message,
            };
        }
    }
}
