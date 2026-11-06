namespace Touchstone.Xunit2Adapter
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;

    using Touchstone.Adapters;
    using Touchstone.Core;
    using Xunit.Abstractions;

    /// <summary>
    /// Executes Touchstone cases inside xUnit v2 tests.
    /// Thread safety: all members are safe to call concurrently.
    /// </summary>
    public static class TouchstoneXunit2
    {
        /// <summary>
        /// Execute the case behind a theory row. Passes for passed cases and, under <see cref="SkipBehavior.Report"/>,
        /// for skipped cases, writing the skip reason to <paramref name="output"/>.
        /// </summary>
        /// <param name="testCase">Row key from <see cref="TouchstoneXunit2Cases.From{TProvider}"/>.</param>
        /// <param name="output">xUnit output helper for skip reasons and scenario diagnostics. Null writes nothing.</param>
        /// <param name="cancellationToken">Caller cancellation.</param>
        /// <returns>A task that completes when the case passes or is skipped.</returns>
        /// <exception cref="ArgumentNullException">The key is null.</exception>
        /// <exception cref="InvalidOperationException">The key cannot be resolved to a case.</exception>
        /// <exception cref="OperationCanceledException">The case observed caller cancellation.</exception>
        /// <exception cref="SkippedCaseNotAllowedException">The case is skipped and the row was created with <see cref="SkipBehavior.Fail"/>.</exception>
        /// <remarks>For a failed case, the case's original exception is rethrown with its original stack trace.</remarks>
        public static Task ExecuteAsync(TouchstoneXunit2Case testCase, ITestOutputHelper? output = null, CancellationToken cancellationToken = default)
        {
            if (testCase == null) throw new ArgumentNullException(nameof(testCase));

            TestCaseDescriptor descriptor = CaseIndex.Resolve(CaseIndex.ProviderType(testCase.ProviderType), testCase.TestId);
            TestRunOptions options = new TestRunOptions { SkipBehavior = testCase.SkipBehavior, CaseTimeout = testCase.CaseTimeout };
            return RunAsync(descriptor, options, output, cancellationToken);
        }

        private static async Task RunAsync(TestCaseDescriptor descriptor, TestRunOptions options, ITestOutputHelper? output, CancellationToken cancellationToken)
        {
            TestResult result = await TestExecutor.ExecuteCaseResultAsync(descriptor, null, options, cancellationToken).ConfigureAwait(false);

            string diagnostics = ScenarioDiagnostics.Format(result);
            if (output != null && diagnostics.Length > 0) output.WriteLine(diagnostics);
            if (output != null && result.Outcome == TestOutcome.Skipped)
                output.WriteLine("Skipped (xUnit v2 cannot skip at run time): " + result.Message);

            TestRunAssert.ThrowIfNotPassed(result, cancellationToken);
        }
    }
}
