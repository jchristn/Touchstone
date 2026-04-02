using System.Diagnostics;
using Touchstone.Core;

namespace Touchstone.Cli;

public static class ConsoleRunner
{
    public static async Task<int> RunAsync(
        IReadOnlyList<TestSuiteDescriptor> suites,
        ConsoleResultSink? sink = null,
        CancellationToken cancellationToken = default)
    {
        sink ??= new ConsoleResultSink();
        sink.WriteHeader();

        var stopwatch = Stopwatch.StartNew();
        int totalCount = 0, passedCount = 0, failedCount = 0, skippedCount = 0;

        foreach (var suite in suites)
        {
            var summary = await TestExecutor.RunSuiteAsync(suite, sink, cancellationToken);
            totalCount += summary.Total;
            passedCount += summary.Passed;
            failedCount += summary.Failed;
            skippedCount += summary.Skipped;
        }

        stopwatch.Stop();
        sink.WriteOverall(stopwatch.Elapsed, totalCount, passedCount, failedCount, skippedCount);

        return failedCount == 0 ? 0 : 1;
    }
}
