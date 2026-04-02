namespace Touchstone.Cli
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.IO;
    using System.Text.Json;
    using System.Threading;
    using System.Threading.Tasks;

    using Touchstone.Core;

    /// <summary>
    /// Runs test suites through the console with tabular output and optional JSON export.
    /// </summary>
    public static class ConsoleRunner
    {
        /// <summary>
        /// Execute all suites and write results to the console.
        /// </summary>
        /// <param name="suites">Suites to run.</param>
        /// <param name="sink">Optional custom sink. A default sink is created when null.</param>
        /// <param name="resultsPath">Optional file path for JSON result export.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Exit code: 0 when all tests pass, 1 when any test fails.</returns>
        public static async Task<int> RunAsync(
            IReadOnlyList<TestSuiteDescriptor> suites,
            ConsoleResultSink sink = null,
            string resultsPath = null,
            CancellationToken cancellationToken = default)
        {
            if (suites == null) throw new ArgumentNullException(nameof(suites));

            sink = sink ?? new ConsoleResultSink();
            sink.WriteHeader();

            Stopwatch stopwatch = Stopwatch.StartNew();
            int totalCount = 0;
            int passedCount = 0;
            int failedCount = 0;
            int skippedCount = 0;

            foreach (TestSuiteDescriptor suite in suites)
            {
                TestRunSummary summary = await TestExecutor.RunSuiteAsync(suite, sink, cancellationToken);
                totalCount += summary.Total;
                passedCount += summary.Passed;
                failedCount += summary.Failed;
                skippedCount += summary.Skipped;
            }

            stopwatch.Stop();
            sink.WriteOverall(stopwatch.Elapsed, totalCount, passedCount, failedCount, skippedCount);

            if (!string.IsNullOrEmpty(resultsPath))
            {
                ExportResults(sink.AllResults, resultsPath);
            }

            return failedCount == 0 ? 0 : 1;
        }

        private static void ExportResults(IReadOnlyList<TestResult> results, string path)
        {
            List<JsonResultEntry> entries = new List<JsonResultEntry>();

            foreach (TestResult result in results)
            {
                entries.Add(new JsonResultEntry
                {
                    TestId = result.TestId,
                    SuiteId = result.SuiteId,
                    CaseId = result.CaseId,
                    DisplayName = result.DisplayName,
                    Success = result.Success,
                    Skipped = result.Skipped,
                    DurationMs = (long)Math.Round(result.Duration.TotalMilliseconds, MidpointRounding.AwayFromZero),
                    Message = result.Message,
                });
            }

            JsonSerializerOptions options = new JsonSerializerOptions
            {
                WriteIndented = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            };

            string json = JsonSerializer.Serialize(entries, options);
            File.WriteAllText(path, json);
        }
    }
}
