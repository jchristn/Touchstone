namespace Touchstone.Cli
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.IO;
    using System.Linq;
    using System.Net;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;

    using Touchstone.Core;

    /// <summary>Creates self-contained offline execution reports. Does not execute tests.</summary>
    public static class ScenarioHtmlReport
    {
        /// <summary>Write an accessible HTML report with escaped, optionally redacted text.
        /// No remote resources or executable scripts are included. The caller owns the result collection.</summary>
        /// <exception cref="ArgumentNullException">Results, path, or run identity is null.</exception>
        /// <exception cref="OperationCanceledException">The caller cancels writing.</exception>
        /// <exception cref="IOException">The report cannot be written.</exception>
        public static async Task WriteAsync(IReadOnlyList<TestResult> results, string path, string runId,
            CancellationToken cancellationToken = default, Func<string, string>? redact = null)
        {
            ArgumentNullException.ThrowIfNull(results);
            ArgumentNullException.ThrowIfNull(path);
            ArgumentNullException.ThrowIfNull(runId);
            cancellationToken.ThrowIfCancellationRequested();
            Activity? activity = CliInstrumentation.StartExport("html", results.Count);
            Stopwatch timer = Stopwatch.StartNew();
            try
            {
                Func<string, string> transform = redact ?? (text => text);
                string Encode(string text) { return WebUtility.HtmlEncode(transform(text)); }
                StringBuilder html = new StringBuilder("<!doctype html><html lang=\"en\"><head><meta charset=\"utf-8\"><meta name=\"viewport\" content=\"width=device-width\"><title>Touchstone execution report</title><style>body{font-family:system-ui;max-width:72rem;margin:2rem auto;padding:1rem}table{border-collapse:collapse;width:100%}th,td{text-align:left;padding:.5rem;border:1px solid #aaa}pre{white-space:pre-wrap}summary{cursor:pointer}details{margin-block:1rem}td{overflow-wrap:anywhere}section{margin-block:2rem}</style></head><body><h1>Touchstone execution report</h1>");
                html.Append("<p>Run: ").Append(Encode(runId)).Append(". Collected: ")
                    .Append(DateTimeOffset.UtcNow.ToString("O")).Append("</p><p>Total: ").Append(results.Count)
                    .Append(". Passed: ").Append(results.Count(r => r.Success && !r.Skipped))
                    .Append(". Failed: ").Append(results.Count(r => !r.Success))
                    .Append(". Skipped: ").Append(results.Count(r => r.Skipped)).Append(".</p>");
                foreach (IGrouping<string, TestResult> feature in results.GroupBy(r => string.IsNullOrEmpty(r.Metadata?.Feature) ? r.SuiteId : r.Metadata.Feature))
                {
                    html.Append("<section><h2>").Append(Encode(feature.Key)).Append("</h2>");
                    foreach (TestResult result in feature)
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        html.Append("<details open><summary>").Append(Encode(result.DisplayName)).Append(" : ")
                            .Append(result.Skipped ? "Skipped" : result.Canceled ? "Canceled" : result.Success ? "Passed" : "Failed")
                            .Append(" (").Append(result.Duration.TotalMilliseconds.ToString("F1", System.Globalization.CultureInfo.InvariantCulture)).Append("ms)</summary><p>").Append(Encode(result.TestId)).Append("</p>");
                        if (result.Metadata != null)
                            html.Append("<p>Example: ").Append(Encode(result.Metadata.ExampleId ?? "none"))
                                .Append(". Requirements: ").Append(Encode(string.Join(", ", result.Metadata.Requirements))).Append("</p>");
                        if (result.Message != null) html.Append("<pre>").Append(Encode(result.Message)).Append("</pre>");
                        if (result.Steps.Count > 0)
                        {
                            html.Append("<table><caption>Steps</caption><thead><tr><th scope=\"col\">Behavior</th><th scope=\"col\">Outcome</th><th scope=\"col\">Duration (ms)</th><th scope=\"col\">Details</th></tr></thead><tbody>");
                            foreach (ScenarioStepResult step in result.Steps)
                                html.Append("<tr><td>").Append(step.Kind).Append(' ').Append(Encode(step.DisplayName))
                                    .Append("</td><td>").Append(step.Outcome).Append("</td><td>")
                                    .Append(step.Duration.TotalMilliseconds.ToString("F1", System.Globalization.CultureInfo.InvariantCulture))
                                    .Append("</td><td>").Append(Encode(step.Message ?? "")).Append("</td></tr>");
                            html.Append("</tbody></table>");
                        }
                        foreach (Exception error in result.CleanupExceptions)
                            html.Append("<pre>Cleanup: ").Append(Encode(error.Message)).Append("</pre>");
                        html.Append("</details>");
                    }
                    html.Append("</section>");
                }
                html.Append("</body></html>");
                await File.WriteAllTextAsync(path, html.ToString(), cancellationToken).ConfigureAwait(false);
                CliInstrumentation.CompleteExport(activity, "html", timer.Elapsed, null);
            }
            catch (Exception error)
            {
                CliInstrumentation.CompleteExport(activity, "html", timer.Elapsed, error);
                throw;
            }
        }
    }
}
