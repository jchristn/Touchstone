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

            TestRunSummary summary = await TestExecutor.RunAsync(suites, sink, cancellationToken).ConfigureAwait(false);
            sink.WriteOverall(summary.Duration, summary.Total, summary.Passed, summary.Failed, summary.Skipped);

            if (!string.IsNullOrEmpty(resultsPath))
            {
                ExportResults(sink.AllResults, resultsPath);
            }

            return summary.Failed == 0 ? 0 : 1;
        }

        private static void ExportResults(IReadOnlyList<TestResult> results, string path)
        {
            Activity activity = CliInstrumentation.StartExport(CliInstrumentation.FormatJson, results.Count);
            long start = Stopwatch.GetTimestamp();

            try
            {
                WriteJson(results, path);
                CliInstrumentation.CompleteExport(activity, CliInstrumentation.FormatJson, Stopwatch.GetElapsedTime(start), null);
            }
            catch (Exception ex)
            {
                CliInstrumentation.CompleteExport(activity, CliInstrumentation.FormatJson, Stopwatch.GetElapsedTime(start), ex);
                throw;
            }
        }

        private static void WriteJson(IReadOnlyList<TestResult> results, string path)
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
