namespace Touchstone.NunitAdapter
{
    using System;
    using System.IO;
    using System.Threading;
    using System.Threading.Tasks;

    using NUnit.Framework;
    using Touchstone.Core;

    /// <summary>
    /// Executes Touchstone cases inside NUnit tests. Scenario diagnostics go to <see cref="TestContext.Out"/> unless another writer is supplied.
    /// Thread safety: all members are safe to call concurrently.
    /// </summary>
    public static class TouchstoneNunit
    {
        /// <summary>
        /// Execute one case.
        /// </summary>
        /// <param name="testCase">Case from <see cref="TouchstoneNunitCases.From"/>.</param>
        /// <param name="options">The options the case source was created with. Null uses the defaults.</param>
        /// <param name="output">Receives scenario diagnostics. Null (the default) means <see cref="TestContext.Out"/>.</param>
        /// <param name="cancellationToken">Caller cancellation.</param>
        /// <returns>A task that completes when the case passes.</returns>
        /// <exception cref="ArgumentNullException">The case is null.</exception>
        /// <exception cref="OperationCanceledException">The case observed caller cancellation.</exception>
        /// <exception cref="SkippedCaseNotAllowedException">The case is skipped and the skip behavior is <see cref="SkipBehavior.Fail"/>.</exception>
        /// <remarks>For a failed case, the case's original exception is rethrown with its original stack trace. A skipped
        /// case reached at run time is reported to NUnit as ignored.</remarks>
        public static async Task ExecuteAsync(
            TestCaseDescriptor testCase,
            TestRunOptions? options = null,
            TextWriter? output = null,
            CancellationToken cancellationToken = default)
        {
            if (testCase == null) throw new ArgumentNullException(nameof(testCase));

            TestResult result = await TestExecutor.ExecuteCaseResultAsync(testCase, null, options, cancellationToken).ConfigureAwait(false);

            string diagnostics = ScenarioDiagnostics.Format(result);
            if (diagnostics.Length > 0) (output ?? TestContext.Out).WriteLine(diagnostics);

            if (result.Outcome == TestOutcome.Skipped) Assert.Ignore(result.Message ?? "Skipped");
            TestRunAssert.ThrowIfNotPassed(result, cancellationToken);
        }
    }
}
