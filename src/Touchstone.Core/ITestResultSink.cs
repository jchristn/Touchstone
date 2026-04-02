namespace Touchstone.Core
{
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// Receives test execution events as they occur.
    /// </summary>
    public interface ITestResultSink
    {
        /// <summary>
        /// Called when a suite begins execution.
        /// </summary>
        /// <param name="suite">Suite that is starting.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>ValueTask.</returns>
        ValueTask OnSuiteStartedAsync(TestSuiteDescriptor suite, CancellationToken cancellationToken);

        /// <summary>
        /// Called after each individual test case completes.
        /// </summary>
        /// <param name="result">Result of the completed test.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>ValueTask.</returns>
        ValueTask OnTestCompletedAsync(TestResult result, CancellationToken cancellationToken);

        /// <summary>
        /// Called when a suite has finished all of its test cases.
        /// </summary>
        /// <param name="suite">Suite that completed.</param>
        /// <param name="summary">Aggregated results for the suite.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>ValueTask.</returns>
        ValueTask OnSuiteCompletedAsync(TestSuiteDescriptor suite, TestRunSummary summary, CancellationToken cancellationToken);
    }
}
