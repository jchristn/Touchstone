namespace Touchstone.Core
{
    using System;
    using System.Globalization;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// Formats scenario step diagnostics without depending on a test host.
    /// Thread safety: all members are safe to call concurrently.
    /// </summary>
    public static class ScenarioDiagnostics
    {
        /// <summary>
        /// Format a result's steps and cleanup failures, one line each. The output may contain consumer text.
        /// </summary>
        /// <param name="result">Result to format.</param>
        /// <returns>The formatted lines. Empty for an ordinary case with no cleanup failures.</returns>
        /// <exception cref="ArgumentNullException">The result is null.</exception>
        public static string Format(TestResult result)
        {
            if (result == null) throw new ArgumentNullException(nameof(result));

            StringBuilder text = new StringBuilder();
            foreach (ScenarioStepResult step in result.Steps)
            {
                text.Append(step.Kind.ToString().ToUpperInvariant())
                    .Append(' ')
                    .Append(step.DisplayName)
                    .Append(" : ")
                    .Append(step.Outcome)
                    .Append(" (")
                    .Append(step.Duration.TotalMilliseconds.ToString("F1", CultureInfo.InvariantCulture))
                    .Append("ms)");
                if (step.Message != null) text.Append(" - ").Append(step.Message);
                text.AppendLine();
            }

            foreach (Exception error in result.CleanupExceptions)
            {
                text.Append("Cleanup: ").Append(error.Message).AppendLine();
            }

            return text.ToString();
        }

        /// <summary>
        /// Execute one case, send its step diagnostics to the host, and throw if it does not pass.
        /// </summary>
        /// <param name="testCase">Case to execute.</param>
        /// <param name="output">Receives the formatted diagnostics. Called only for scenarios and cases with cleanup failures.</param>
        /// <param name="options">Run settings. Null uses the defaults.</param>
        /// <param name="cancellationToken">Caller cancellation.</param>
        /// <returns>A task that completes when the case passes, is skipped, or is not selected.</returns>
        /// <exception cref="ArgumentNullException">The case or output callback is null.</exception>
        /// <exception cref="OperationCanceledException">The case observed caller cancellation.</exception>
        /// <exception cref="SkippedCaseNotAllowedException">The case is skipped and the skip behavior is <see cref="SkipBehavior.Fail"/>.</exception>
        /// <remarks>For a failed case, the case's original exception is rethrown with its original stack trace.
        /// An exception thrown by <paramref name="output"/> propagates.</remarks>
        public static async Task ExecuteAsync(
            TestCaseDescriptor testCase,
            Action<string> output,
            TestRunOptions? options = null,
            CancellationToken cancellationToken = default)
        {
            if (testCase == null) throw new ArgumentNullException(nameof(testCase));
            if (output == null) throw new ArgumentNullException(nameof(output));

            TestResult result = await TestExecutor.ExecuteCaseResultAsync(testCase, null, options, cancellationToken).ConfigureAwait(false);
            string diagnostics = Format(result);
            if (diagnostics.Length > 0) output(diagnostics);
            TestRunAssert.ThrowIfNotPassed(result, cancellationToken);
        }
    }
}
