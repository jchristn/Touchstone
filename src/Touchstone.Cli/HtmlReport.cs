namespace Touchstone.Cli
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Globalization;
    using System.IO;
    using System.Linq;
    using System.Net;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;

    using Touchstone.Core;

    /// <summary>
    /// Writes a self-contained, accessible HTML report. Consumer text is redacted (when configured) and HTML-encoded.
    /// The report contains no scripts and no remote resources, so it works offline. It does not run tests.
    /// Thread safety: all members are safe to call concurrently for different paths.
    /// </summary>
    public static class HtmlReport
    {
        private const string Head =
            "<!doctype html><html lang=\"en\"><head><meta charset=\"utf-8\">"
            + "<meta name=\"viewport\" content=\"width=device-width\">"
            + "<title>Touchstone execution report</title>"
            + "<style>"
            + "body{font-family:system-ui;max-width:72rem;margin:2rem auto;padding:1rem}"
            + "table{border-collapse:collapse;width:100%}"
            + "th,td{text-align:left;padding:.5rem;border:1px solid #aaa}"
            + "td{overflow-wrap:anywhere}"
            + "pre{white-space:pre-wrap}"
            + "summary{cursor:pointer}"
            + "details{margin-block:1rem}"
            + "section{margin-block:2rem}"
            + "</style></head><body><h1>Touchstone execution report</h1>";

        private const string StepTableHead =
            "<table><caption>Steps</caption><thead><tr>"
            + "<th scope=\"col\">Behavior</th><th scope=\"col\">Outcome</th>"
            + "<th scope=\"col\">Duration (ms)</th><th scope=\"col\">Details</th>"
            + "</tr></thead><tbody>";

        /// <summary>
        /// Write an HTML report, replacing any existing file.
        /// </summary>
        /// <param name="summary">The run summary. Counts in the report come from it.</param>
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
            Activity? activity = CliInstrumentation.StartExport(CliInstrumentation.FormatHtml, results.Count);
            Stopwatch timer = Stopwatch.StartNew();

            try
            {
                cancellationToken.ThrowIfCancellationRequested();
                string html = Render(summary, results, settings, cancellationToken);
                await File.WriteAllTextAsync(path, html, cancellationToken).ConfigureAwait(false);
                CliInstrumentation.CompleteExport(activity, CliInstrumentation.FormatHtml, timer.Elapsed, null);
            }
            catch (Exception ex)
            {
                CliInstrumentation.CompleteExport(activity, CliInstrumentation.FormatHtml, timer.Elapsed, ex);
                throw;
            }
        }

        internal static string Render(TestRunSummary summary, IReadOnlyList<TestResult> results, ReportOptions options, CancellationToken cancellationToken)
        {
            Func<string, string> redact = options.Redact;
            string Encode(string text)
            {
                return WebUtility.HtmlEncode(redact(text));
            }

            DateTimeOffset started = (options.StartedUtc ?? DateTimeOffset.UtcNow).ToUniversalTime();
            StringBuilder html = new StringBuilder(Head);
            html.Append("<p>Run: ").Append(Encode(options.RunId))
                .Append(". Started: ").Append(started.ToString("O", CultureInfo.InvariantCulture))
                .Append(". Duration: ").Append(Milliseconds(summary.Duration)).Append(" ms.</p>");
            html.Append("<p>Total: ").Append(summary.Total)
                .Append(". Passed: ").Append(summary.Passed)
                .Append(". Failed: ").Append(summary.Failed)
                .Append(". Skipped: ").Append(summary.Skipped)
                .Append(". Canceled: ").Append(summary.Canceled)
                .Append(". Not run: ").Append(summary.NotRun)
                .Append(". Filtered: ").Append(summary.Filtered).Append(".</p>");

            if (summary.LifecycleErrors.Count > 0)
            {
                html.Append("<section><h2>Suite lifecycle errors</h2><ul>");
                foreach (SuiteLifecycleError error in summary.LifecycleErrors)
                {
                    html.Append("<li>").Append(Encode(error.SuiteId)).Append(' ').Append(error.Stage)
                        .Append(": ").Append(Encode(error.Exception.Message)).Append("</li>");
                }

                html.Append("</ul></section>");
            }

            foreach (IGrouping<string, TestResult> group in results.GroupBy(r => string.IsNullOrEmpty(r.Metadata?.Feature) ? r.SuiteId : r.Metadata!.Feature))
            {
                html.Append("<section><h2>").Append(Encode(group.Key)).Append("</h2>");
                foreach (TestResult result in group)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    AppendResult(html, result, Encode);
                }

                html.Append("</section>");
            }

            html.Append("</body></html>");
            return html.ToString();
        }

        private static void AppendResult(StringBuilder html, TestResult result, Func<string, string> encode)
        {
            html.Append("<details open><summary>").Append(encode(result.DisplayName)).Append(" : ")
                .Append(OutcomeNames.Of(result.Outcome))
                .Append(" (").Append(Milliseconds(result.Duration)).Append(" ms)</summary>")
                .Append("<p>").Append(encode(result.TestId)).Append("</p>");

            if (result.Tags.Count > 0)
                html.Append("<p>Tags: ").Append(encode(string.Join(", ", result.Tags))).Append("</p>");

            if (result.Metadata != null)
            {
                html.Append("<p>Example: ").Append(encode(result.Metadata.ExampleId ?? "none"))
                    .Append(". Requirements: ").Append(encode(string.Join(", ", result.Metadata.Requirements))).Append("</p>");
            }

            if (result.Message != null) html.Append("<pre>").Append(encode(result.Message)).Append("</pre>");

            if (result.Steps.Count > 0)
            {
                html.Append(StepTableHead);
                foreach (ScenarioStepResult step in result.Steps)
                {
                    html.Append("<tr><td>").Append(step.Kind).Append(' ').Append(encode(step.DisplayName))
                        .Append("</td><td>").Append(OutcomeNames.Of(step.Outcome))
                        .Append("</td><td>").Append(Milliseconds(step.Duration))
                        .Append("</td><td>").Append(encode(step.Message ?? string.Empty)).Append("</td></tr>");
                }

                html.Append("</tbody></table>");
            }

            foreach (Exception error in result.CleanupExceptions)
                html.Append("<pre>Cleanup: ").Append(encode(error.Message)).Append("</pre>");

            html.Append("</details>");
        }

        private static string Milliseconds(TimeSpan duration)
        {
            return duration.TotalMilliseconds.ToString("F1", CultureInfo.InvariantCulture);
        }
    }
}
