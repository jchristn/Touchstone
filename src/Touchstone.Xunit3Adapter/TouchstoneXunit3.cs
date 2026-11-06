namespace Touchstone.Xunit3Adapter
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;

    using Touchstone.Adapters;
    using Touchstone.Core;
    using Xunit;

    /// <summary>
    /// Executes Touchstone cases inside xUnit v3 tests. Scenario diagnostics go to the current test's output.
    /// Thread safety: all members are safe to call concurrently.
    /// </summary>
    public static class TouchstoneXunit3
    {
        /// <summary>
        /// Execute the case behind a theory row.
        /// </summary>
        /// <param name="testCase">Row key from <see cref="TouchstoneXunit3Cases.From{TProvider}"/>.</param>
        /// <param name="cancellationToken">Caller cancellation. Pass <c>TestContext.Current.CancellationToken</c>.</param>
        /// <returns>A task that completes when the case passes.</returns>
        /// <exception cref="ArgumentNullException">The key is null.</exception>
        /// <exception cref="InvalidOperationException">The key cannot be resolved to a case.</exception>
        /// <exception cref="OperationCanceledException">The case observed caller cancellation.</exception>
        /// <exception cref="SkippedCaseNotAllowedException">The case is skipped and the row was created with <see cref="SkipBehavior.Fail"/>.</exception>
        /// <remarks>For a failed case, the case's original exception is rethrown with its original stack trace. A skipped
        /// case reached at run time is reported to xUnit as a dynamic skip.</remarks>
        public static async Task ExecuteAsync(TouchstoneXunit3Case testCase, CancellationToken cancellationToken = default)
        {
            if (testCase == null) throw new ArgumentNullException(nameof(testCase));

            TestCaseDescriptor descriptor = CaseIndex.Resolve(CaseIndex.ProviderType(testCase.ProviderType), testCase.TestId);
            TestRunOptions options = new TestRunOptions { SkipBehavior = testCase.SkipBehavior };
            Touchstone.Core.TestResult result = await TestExecutor.ExecuteCaseResultAsync(descriptor, null, options, cancellationToken).ConfigureAwait(false);

            string diagnostics = ScenarioDiagnostics.Format(result);
            if (diagnostics.Length > 0) TestContext.Current.TestOutputHelper?.WriteLine(diagnostics);

            if (result.Outcome == TestOutcome.Skipped) throw new TouchstoneXunit3SkipException(result.Message ?? "Skipped");
            TestRunAssert.ThrowIfNotPassed(result, cancellationToken);
        }
    }
}
