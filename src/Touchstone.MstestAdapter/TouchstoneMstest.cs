namespace Touchstone.MstestAdapter
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;

    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Touchstone.Adapters;
    using Touchstone.Core;

    /// <summary>
    /// Executes Touchstone cases inside MSTest tests. Scenario diagnostics go to the test context you pass.
    /// Thread safety: all members are safe to call concurrently.
    /// </summary>
    public static class TouchstoneMstest
    {
        /// <summary>
        /// Execute the case behind a data row.
        /// </summary>
        /// <param name="testCase">Row key from <see cref="TouchstoneMstestCases.From{TProvider}"/>.</param>
        /// <param name="testContext">The running test's <see cref="TestContext"/>, which receives scenario diagnostics. Null writes nothing.</param>
        /// <param name="cancellationToken">Caller cancellation. Pass <c>TestContext.CancellationToken</c>.</param>
        /// <returns>A task that completes when the case passes.</returns>
        /// <exception cref="ArgumentNullException">The key is null.</exception>
        /// <exception cref="InvalidOperationException">The key cannot be resolved to a case.</exception>
        /// <exception cref="OperationCanceledException">The case observed caller cancellation.</exception>
        /// <exception cref="SkippedCaseNotAllowedException">The case is skipped and the row was created with <see cref="SkipBehavior.Fail"/>.</exception>
        /// <remarks>For a failed case, the case's original exception is rethrown with its original stack trace. A skipped
        /// case reached at run time is reported to MSTest as inconclusive.</remarks>
        public static async Task ExecuteAsync(
            TouchstoneMstestCase testCase,
            TestContext? testContext = null,
            CancellationToken cancellationToken = default)
        {
            if (testCase == null) throw new ArgumentNullException(nameof(testCase));

            TestCaseDescriptor descriptor = CaseIndex.Resolve(CaseIndex.ProviderType(testCase.ProviderType), testCase.TestId);
            TestRunOptions options = new TestRunOptions { SkipBehavior = testCase.SkipBehavior };
            Touchstone.Core.TestResult result = await TestExecutor.ExecuteCaseResultAsync(descriptor, null, options, cancellationToken).ConfigureAwait(false);

            string diagnostics = ScenarioDiagnostics.Format(result);
            if (diagnostics.Length > 0) testContext?.WriteLine(diagnostics);

            if (result.Outcome == TestOutcome.Skipped) Assert.Inconclusive(result.Message ?? "Skipped");
            TestRunAssert.ThrowIfNotPassed(result, cancellationToken);
        }
    }
}
