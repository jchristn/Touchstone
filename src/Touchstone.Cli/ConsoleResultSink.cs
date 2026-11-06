namespace Touchstone.Cli
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.IO;
    using System.Threading;
    using System.Threading.Tasks;

    using Touchstone.Core;

    /// <summary>
    /// Writes results as a table: one row per case, then an overall summary with failures and suite hook errors.
    /// Color uses ANSI sequences written to the writer; the process console color is never changed.
    /// Thread safety: not thread-safe. Use one instance per run.
    /// </summary>
    public sealed class ConsoleResultSink : ITestResultSink
    {
        private const int ResultColumnWidth = 8;
        private const int RuntimeColumnWidth = 9;
        private const int SeparatorWidth = 4;
        private const int MinimumDescriptionWidth = 40;
        private const int DefaultDescriptionWidth = 100;
        private const string AnsiReset = "\u001b[0m";
        private const string AnsiGreen = "\u001b[32m";
        private const string AnsiRed = "\u001b[31m";
        private const string AnsiYellow = "\u001b[33m";

        private readonly TextWriter _Writer;
        private readonly bool _UseColor;
        private readonly bool _ExpandSteps;
        private readonly Func<string, string> _Redact;
        private readonly int _DescriptionWidth;
        private readonly List<TestResult> _Results = new List<TestResult>();
        private TestRunSummary? _Summary = null;

        /// <summary>
        /// Initialize a console sink.
        /// </summary>
        /// <param name="options">Writer, color, step expansion, and redaction settings. Null uses the defaults. The
        /// settings are read once, here.</param>
        public ConsoleResultSink(ConsoleRunnerOptions? options = null)
        {
            ConsoleRunnerOptions settings = options ?? new ConsoleRunnerOptions();
            bool isConsole = settings.Writer == null || ReferenceEquals(settings.Writer, Console.Out);
            _Writer = settings.Writer ?? Console.Out;
            _UseColor = settings.UseColor ?? (isConsole && !Console.IsOutputRedirected);
            _ExpandSteps = settings.ExpandSteps;
            _Redact = settings.Redact;
            _DescriptionWidth = DescriptionWidth(isConsole);
        }

        /// <summary>
        /// Every result written, in completion order. Never null.
        /// </summary>
        public IReadOnlyList<TestResult> AllResults
        {
            get { return _Results.AsReadOnly(); }
        }

        /// <summary>
        /// The run summary. Null until a multi-suite run completes.
        /// </summary>
        public TestRunSummary? Summary
        {
            get { return _Summary; }
        }

        /// <summary>
        /// Width of the test name column, in characters. At least 40. When output is redirected or the console width is
        /// unavailable, 100.
        /// </summary>
        public int DescriptionColumnWidth
        {
            get { return _DescriptionWidth; }
        }

        /// <inheritdoc />
        public ValueTask OnRunStartedAsync(IReadOnlyList<TestSuiteDescriptor> suites, CancellationToken cancellationToken)
        {
            _Writer.WriteLine("Test".PadRight(_DescriptionWidth) + "  " + "Result".PadRight(ResultColumnWidth) + "  " + "Runtime".PadLeft(RuntimeColumnWidth));
            _Writer.WriteLine(new string('-', _DescriptionWidth) + "  " + new string('-', ResultColumnWidth) + "  " + new string('-', RuntimeColumnWidth));
            return default;
        }

        /// <inheritdoc />
        public ValueTask OnStepStartedAsync(string testId, ScenarioStepResult step, CancellationToken cancellationToken)
        {
            if (_ExpandSteps)
                _Writer.WriteLine("  " + _Redact(testId) + " " + step.Kind.ToString().ToUpperInvariant() + " " + _Redact(step.DisplayName) + "...");
            return default;
        }

        /// <inheritdoc />
        public ValueTask OnStepCompletedAsync(string testId, ScenarioStepResult step, CancellationToken cancellationToken)
        {
            if (_ExpandSteps)
                _Writer.WriteLine("    " + step.Outcome + " " + FormatRuntime(step.Duration) + (step.Message == null ? string.Empty : " " + _Redact(step.Message)));
            return default;
        }

        /// <inheritdoc />
        /// <exception cref="ArgumentNullException">The result is null.</exception>
        public ValueTask OnCaseCompletedAsync(TestResult result, CancellationToken cancellationToken)
        {
            if (result == null) throw new ArgumentNullException(nameof(result));

            _Results.Add(result);
            _Writer.Write(FormatDescription(_Redact(result.DisplayName), _DescriptionWidth));
            _Writer.Write("  ");
            WriteStatus(OutcomeNames.Label(result.Outcome), ColorOf(result.Outcome));
            _Writer.Write("  ");
            _Writer.WriteLine(FormatRuntime(result.Duration).PadLeft(RuntimeColumnWidth));

            if (_ExpandSteps)
            {
                foreach (ScenarioStepResult step in result.Steps)
                {
                    if (step.Outcome == ScenarioStepOutcome.NotRun || step.Outcome == ScenarioStepOutcome.Skipped)
                        _Writer.WriteLine("  " + _Redact(step.DisplayName) + " " + step.Outcome);
                }

                foreach (Exception error in result.CleanupExceptions)
                    _Writer.WriteLine("  Cleanup: " + _Redact(error.Message));
            }

            return default;
        }

        /// <inheritdoc />
        /// <exception cref="ArgumentNullException">The summary is null.</exception>
        public ValueTask OnRunCompletedAsync(TestRunSummary summary, CancellationToken cancellationToken)
        {
            _Summary = summary ?? throw new ArgumentNullException(nameof(summary));

            _Writer.WriteLine();
            _Writer.Write("OVERALL".PadRight(_DescriptionWidth));
            _Writer.Write("  ");
            if (summary.WasCanceled) WriteStatus("CANCELED", AnsiYellow);
            else if (summary.IsSuccess) WriteStatus("PASS", AnsiGreen);
            else WriteStatus("FAIL", AnsiRed);
            _Writer.Write("  ");
            _Writer.WriteLine(FormatRuntime(summary.Duration).PadLeft(RuntimeColumnWidth));

            string totals = "Total: " + Number(summary.Total)
                + "  Passed: " + Number(summary.Passed)
                + "  Failed: " + Number(summary.Failed)
                + "  Skipped: " + Number(summary.Skipped);
            if (summary.Canceled > 0) totals += "  Canceled: " + Number(summary.Canceled);
            if (summary.NotRun > 0) totals += "  Not run: " + Number(summary.NotRun);
            if (summary.Filtered > 0) totals += "  Filtered: " + Number(summary.Filtered);
            _Writer.WriteLine(totals);

            bool headerWritten = false;
            foreach (TestResult result in _Results)
            {
                if (result.Outcome != TestOutcome.Failed && result.Outcome != TestOutcome.Canceled) continue;
                if (!headerWritten)
                {
                    _Writer.WriteLine("Failed Tests:");
                    headerWritten = true;
                }

                _Writer.WriteLine("  " + _Redact(result.DisplayName));
                if (result.Message != null) _Writer.WriteLine("    " + _Redact(result.Message));
            }

            if (summary.LifecycleErrors.Count > 0)
            {
                _Writer.WriteLine("Suite Lifecycle Errors:");
                foreach (SuiteLifecycleError error in summary.LifecycleErrors)
                    _Writer.WriteLine("  " + _Redact(error.SuiteId) + " " + error.Stage + ": " + _Redact(error.Exception.Message));
            }

            return default;
        }

        private void WriteStatus(string label, string color)
        {
            if (_UseColor)
                _Writer.Write(color + label.PadRight(ResultColumnWidth) + AnsiReset);
            else
                _Writer.Write(label.PadRight(ResultColumnWidth));
        }

        private static string ColorOf(TestOutcome outcome)
        {
            switch (outcome)
            {
                case TestOutcome.Passed: return AnsiGreen;
                case TestOutcome.Failed: return AnsiRed;
                default: return AnsiYellow;
            }
        }

        private static string Number(int value)
        {
            return value.ToString(CultureInfo.InvariantCulture);
        }

        private static string FormatRuntime(TimeSpan runtime)
        {
            long ms = (long)Math.Round(runtime.TotalMilliseconds, MidpointRounding.AwayFromZero);
            return ms.ToString(CultureInfo.InvariantCulture) + "ms";
        }

        private static string FormatDescription(string description, int width)
        {
            if (description.Length <= width) return description.PadRight(width);
            if (width <= 3) return description.Substring(0, width);
            return description.Substring(0, width - 3) + "...";
        }

        private static int DescriptionWidth(bool isConsole)
        {
            if (!isConsole) return DefaultDescriptionWidth;

            try
            {
                if (Console.IsOutputRedirected) return DefaultDescriptionWidth;
                int available = Console.WindowWidth - ResultColumnWidth - RuntimeColumnWidth - SeparatorWidth;
                return available < MinimumDescriptionWidth ? MinimumDescriptionWidth : available;
            }
            catch (IOException)
            {
                return DefaultDescriptionWidth;
            }
            catch (ArgumentOutOfRangeException)
            {
                return DefaultDescriptionWidth;
            }
            catch (PlatformNotSupportedException)
            {
                return DefaultDescriptionWidth;
            }
        }
    }
}
