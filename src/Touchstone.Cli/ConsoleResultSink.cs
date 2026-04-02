namespace Touchstone.Cli
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Threading;
    using System.Threading.Tasks;

    using Touchstone.Core;

    /// <summary>
    /// Writes test results to the console in a tabular format with colored output.
    /// </summary>
    public sealed class ConsoleResultSink : ITestResultSink
    {
        private readonly TextWriter _Writer;
        private readonly bool _UseColor;
        private readonly int _DescriptionWidth;
        private readonly List<TestResult> _AllResults = new List<TestResult>();

        /// <summary>
        /// Initialize a new console result sink.
        /// </summary>
        /// <param name="writer">Output writer. Defaults to Console.Out.</param>
        /// <param name="useColor">Whether to use ANSI color. Defaults to true when output is not redirected.</param>
        public ConsoleResultSink(TextWriter writer = null, bool? useColor = null)
        {
            _Writer = writer ?? Console.Out;
            _UseColor = useColor ?? !Console.IsOutputRedirected;
            _DescriptionWidth = GetMaximumDescriptionWidth();
        }

        /// <summary>
        /// All results collected during this run.
        /// </summary>
        public IReadOnlyList<TestResult> AllResults
        {
            get { return _AllResults; }
        }

        /// <summary>
        /// Write the column header and separator line.
        /// </summary>
        public void WriteHeader()
        {
            _Writer.WriteLine(
                "Test".PadRight(_DescriptionWidth)
                + "  "
                + "Result".PadRight(6)
                + "  "
                + "Runtime".PadLeft(8));
            _Writer.WriteLine(
                new string('-', _DescriptionWidth)
                + "  "
                + new string('-', 6)
                + "  "
                + new string('-', 8));
        }

        /// <inheritdoc />
        public ValueTask OnSuiteStartedAsync(TestSuiteDescriptor suite, CancellationToken cancellationToken)
        {
            return ValueTask.CompletedTask;
        }

        /// <inheritdoc />
        public ValueTask OnTestCompletedAsync(TestResult result, CancellationToken cancellationToken)
        {
            if (result == null) throw new ArgumentNullException(nameof(result));

            _AllResults.Add(result);
            WriteResultLine(result);
            return ValueTask.CompletedTask;
        }

        /// <inheritdoc />
        public ValueTask OnSuiteCompletedAsync(TestSuiteDescriptor suite, TestRunSummary summary, CancellationToken cancellationToken)
        {
            return ValueTask.CompletedTask;
        }

        /// <summary>
        /// Write the overall summary row and failed test listing.
        /// </summary>
        /// <param name="totalRuntime">Total elapsed time.</param>
        /// <param name="totalCount">Total test count.</param>
        /// <param name="passedCount">Passed test count.</param>
        /// <param name="failedCount">Failed test count.</param>
        /// <param name="skippedCount">Skipped test count.</param>
        public void WriteOverall(TimeSpan totalRuntime, int totalCount, int passedCount, int failedCount, int skippedCount)
        {
            _Writer.WriteLine();

            bool passed = failedCount == 0;
            string runtime = FormatRuntime(totalRuntime);

            _Writer.Write("OVERALL".PadRight(_DescriptionWidth));
            _Writer.Write("  ");
            WriteStatus(passed);
            _Writer.Write("  ");
            _Writer.WriteLine(runtime.PadLeft(8));

            _Writer.WriteLine(
                "Total: " + totalCount.ToString()
                + "  Passed: " + passedCount.ToString()
                + "  Failed: " + failedCount.ToString()
                + "  Skipped: " + skippedCount.ToString());

            if (failedCount > 0)
            {
                _Writer.WriteLine("Failed Tests:");

                foreach (TestResult result in _AllResults)
                {
                    if (!result.Success && !result.Skipped)
                    {
                        _Writer.WriteLine("  " + result.DisplayName);

                        if (result.Message != null)
                            _Writer.WriteLine("    " + result.Message);
                    }
                }
            }
        }

        private void WriteResultLine(TestResult result)
        {
            string description = FormatDescription(result.DisplayName, _DescriptionWidth);
            _Writer.Write(description);
            _Writer.Write("  ");

            if (result.Skipped)
                WriteStatus("SKIP", ConsoleColor.Yellow);
            else
                WriteStatus(result.Success);

            _Writer.Write("  ");
            _Writer.WriteLine(FormatRuntime(result.Duration).PadLeft(8));
        }

        private void WriteStatus(bool passed)
        {
            WriteStatus(
                passed ? "PASS" : "FAIL",
                passed ? ConsoleColor.Green : ConsoleColor.Red);
        }

        private void WriteStatus(string label, ConsoleColor color)
        {
            if (!_UseColor)
            {
                _Writer.Write(label.PadRight(6));
                return;
            }

            ConsoleColor previous = Console.ForegroundColor;
            Console.ForegroundColor = color;
            _Writer.Write(label.PadRight(6));
            Console.ForegroundColor = previous;
        }

        private static string FormatRuntime(TimeSpan runtime)
        {
            long ms = (long)Math.Round(runtime.TotalMilliseconds, MidpointRounding.AwayFromZero);
            return ms.ToString() + "ms";
        }

        private static string FormatDescription(string description, int descriptionWidth)
        {
            if (string.IsNullOrEmpty(description))
                return string.Empty.PadRight(descriptionWidth);

            if (description.Length <= descriptionWidth)
                return description.PadRight(descriptionWidth);

            if (descriptionWidth <= 3)
                return description.Substring(0, descriptionWidth);

            return description.Substring(0, descriptionWidth - 3) + "...";
        }

        private static int GetMaximumDescriptionWidth()
        {
            const int ResultColumnWidth = 6;
            const int RuntimeColumnWidth = 8;
            const int SeparatorWidth = 4;
            const int MinimumDescriptionWidth = 40;
            const int DefaultDescriptionWidth = 100;

            try
            {
                if (Console.IsOutputRedirected)
                    return DefaultDescriptionWidth;

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
