namespace Touchstone.Cli
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Globalization;
    using System.IO;
    using System.Reflection;
    using System.Text.Json;
    using System.Threading;
    using System.Threading.Tasks;

    using Touchstone.Core;

    /// <summary>
    /// Writes run results as JSON that follows <c>schemas/touchstone-results-1.0.schema.json</c>, which ships in this
    /// package. Exception objects are never serialized; only their messages.
    /// Thread safety: all members are safe to call concurrently for different paths.
    /// </summary>
    public static class JsonReport
    {
        /// <summary>
        /// Schema version written to every document.
        /// </summary>
        public const string SchemaVersion = "1.0";

        private const string SchemaResourceName = "Touchstone.Cli.touchstone-results-1.0.schema.json";

        private static readonly Lazy<string> _Schema = new Lazy<string>(LoadSchema);

        /// <summary>
        /// The JSON Schema (draft 2020-12) that every document written by this class follows. Never null.
        /// The same file ships in the package as <c>schemas/touchstone-results-1.0.schema.json</c>.
        /// </summary>
        public static string Schema
        {
            get { return _Schema.Value; }
        }

        /// <summary>
        /// Write a JSON results document, replacing any existing file.
        /// </summary>
        /// <param name="summary">The run summary.</param>
        /// <param name="results">Every case result, in order.</param>
        /// <param name="path">Destination file. Its directory must exist.</param>
        /// <param name="options">Report settings. Null uses the defaults.</param>
        /// <param name="cancellationToken">Cancels writing.</param>
        /// <returns>A task that completes when the file is written.</returns>
        /// <exception cref="ArgumentNullException">The summary, results, or path is null.</exception>
        /// <exception cref="ArgumentException">The path is empty.</exception>
        /// <exception cref="OperationCanceledException">Writing was canceled.</exception>
        /// <exception cref="IOException">The file cannot be written, for example because its directory does not exist.</exception>
        /// <exception cref="UnauthorizedAccessException">The file cannot be written because of permissions.</exception>
        public static async Task WriteAsync(
            TestRunSummary summary,
            IReadOnlyList<TestResult> results,
            string path,
            ReportOptions? options = null,
            CancellationToken cancellationToken = default)
        {
            if (summary == null) throw new ArgumentNullException(nameof(summary));
            if (results == null) throw new ArgumentNullException(nameof(results));
            if (path == null) throw new ArgumentNullException(nameof(path));
            if (path.Length == 0) throw new ArgumentException("Path cannot be empty.", nameof(path));

            ReportOptions settings = options ?? new ReportOptions();
            Activity? activity = CliInstrumentation.StartExport(CliInstrumentation.FormatJson, results.Count);
            Stopwatch timer = Stopwatch.StartNew();

            try
            {
                cancellationToken.ThrowIfCancellationRequested();
                string json = Serialize(summary, results, settings);
                await File.WriteAllTextAsync(path, json, cancellationToken).ConfigureAwait(false);
                CliInstrumentation.CompleteExport(activity, CliInstrumentation.FormatJson, timer.Elapsed, null);
            }
            catch (Exception ex)
            {
                CliInstrumentation.CompleteExport(activity, CliInstrumentation.FormatJson, timer.Elapsed, ex);
                throw;
            }
        }

        internal static string Serialize(TestRunSummary summary, IReadOnlyList<TestResult> results, ReportOptions options)
        {
            Func<string, string> redact = options.Redact;
            JsonResultsDocument document = new JsonResultsDocument();
            document.Tool.Version = ToolVersion();
            document.Run.StartedUtc = (options.StartedUtc ?? DateTimeOffset.UtcNow).ToUniversalTime().ToString("O", CultureInfo.InvariantCulture);
            document.Run.DurationMs = summary.Duration.TotalMilliseconds;
            document.Run.Canceled = summary.WasCanceled;
            document.Run.Summary = new JsonSummary
            {
                Total = summary.Total,
                Passed = summary.Passed,
                Failed = summary.Failed,
                Skipped = summary.Skipped,
                Canceled = summary.Canceled,
                NotRun = summary.NotRun,
                Filtered = summary.Filtered,
            };

            foreach (SuiteLifecycleError error in summary.LifecycleErrors)
            {
                document.Run.LifecycleErrors.Add(new JsonLifecycleError
                {
                    SuiteId = redact(error.SuiteId),
                    Stage = error.Stage,
                    Message = redact(error.Exception.Message),
                });
            }

            foreach (TestResult result in results)
            {
                JsonResult entry = new JsonResult
                {
                    TestId = redact(result.TestId),
                    SuiteId = redact(result.SuiteId),
                    CaseId = redact(result.CaseId),
                    DisplayName = redact(result.DisplayName),
                    Outcome = OutcomeNames.Of(result.Outcome),
                    DurationMs = result.Duration.TotalMilliseconds,
                    Message = result.Message == null ? null : redact(result.Message),
                    Feature = result.Metadata == null ? null : redact(result.Metadata.Feature),
                    ExampleId = result.Metadata?.ExampleId == null ? null : redact(result.Metadata.ExampleId),
                };

                foreach (string tag in result.Tags) entry.Tags.Add(redact(tag));
                if (result.Metadata != null)
                {
                    foreach (string requirement in result.Metadata.Requirements) entry.Requirements.Add(redact(requirement));
                }

                foreach (Exception error in result.CleanupExceptions) entry.CleanupErrors.Add(redact(error.Message));
                foreach (ScenarioStepResult step in result.Steps)
                {
                    entry.Steps.Add(new JsonStep
                    {
                        StepId = redact(step.StepId),
                        DisplayName = redact(step.DisplayName),
                        Kind = OutcomeNames.Of(step.Kind),
                        Outcome = OutcomeNames.Of(step.Outcome),
                        DurationMs = step.Duration.TotalMilliseconds,
                        Message = step.Message == null ? null : redact(step.Message),
                    });
                }

                document.Results.Add(entry);
            }

            return JsonSerializer.Serialize(document, TouchstoneJsonContext.Default.JsonResultsDocument);
        }

        private static string LoadSchema()
        {
            using (Stream? stream = typeof(JsonReport).Assembly.GetManifestResourceStream(SchemaResourceName))
            {
                if (stream == null) throw new InvalidOperationException("The embedded results schema is missing from Touchstone.Cli.");
                using (StreamReader reader = new StreamReader(stream))
                {
                    return reader.ReadToEnd();
                }
            }
        }

        internal static string ToolVersion()
        {
            AssemblyInformationalVersionAttribute? info = typeof(TestExecutor).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>();
            string version = info?.InformationalVersion ?? "unknown";
            int plus = version.IndexOf('+');
            return plus > 0 ? version.Substring(0, plus) : version;
        }
    }
}
