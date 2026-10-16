#nullable enable
namespace Touchstone.Core
{
    using System;
    using System.Globalization;
    using System.Runtime.ExceptionServices;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    /// <summary>Formats scenario diagnostics without depending on a test host.</summary>
    public static class ScenarioDiagnostics
    {
        /// <summary>Format steps and cleanup failures. Input may contain sensitive consumer text.</summary>
        /// <exception cref="ArgumentNullException">Result is null.</exception>
        public static string Format(TestResult result)
        {
            ArgumentNullException.ThrowIfNull(result);
            StringBuilder text = new StringBuilder();
            foreach (ScenarioStepResult step in result.Steps)
                text.AppendLine(step.Kind.ToString().ToUpperInvariant() + " " + step.DisplayName + " : " + step.Outcome
                    + " (" + step.Duration.TotalMilliseconds.ToString("F1", CultureInfo.InvariantCulture) + "ms)"
                    + (step.Message == null ? "" : " - " + step.Message));
            foreach (Exception error in result.CleanupExceptions) text.AppendLine("Cleanup: " + error.Message);
            return text.ToString();
        }
        /// <summary>Execute a single case, send completed diagnostics to the host, and rethrow original failures.</summary>
        /// <exception cref="ArgumentNullException">Case or output is null.</exception>
        /// <exception cref="Exception">An execution failure or output callback error propagates.</exception>
        public static async Task ExecuteAsync(TestCaseDescriptor testCase, Action<string> output, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(output);
            TestResult result = await TestExecutor.ExecuteCaseResultAsync(testCase, cancellationToken).ConfigureAwait(false);
            if (result.Steps.Count > 0) output(Format(result));
            if (result.Exception != null) ExceptionDispatchInfo.Capture(result.Exception).Throw();
        }
    }
}
