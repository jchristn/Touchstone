namespace Touchstone.NunitAdapter
{
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;

    using Touchstone.Core;

    /// <summary>
    /// Base class for running every case in one NUnit test. Derive, supply <see cref="Suites"/>, and call
    /// <see cref="RunAllAsync"/> from a <c>[Test]</c>.
    /// Thread safety: each test class instance runs its own suites; instances are not shared.
    /// </summary>
    public abstract class TouchstoneNunitFactBase
    {
        /// <summary>
        /// Initialize the base class.
        /// </summary>
        protected TouchstoneNunitFactBase()
        {
        }

        /// <summary>
        /// Suites to run. Never null.
        /// </summary>
        protected abstract IReadOnlyList<TestSuiteDescriptor> Suites { get; }

        /// <summary>
        /// Run settings. Default: null, which uses the defaults.
        /// </summary>
        protected virtual TestRunOptions? Options
        {
            get { return null; }
        }

        /// <summary>
        /// Run every selected case, including suite hooks, and throw if the run did not succeed.
        /// Skipped cases do not fail the fact unless <see cref="SkipBehavior.Fail"/> is set.
        /// </summary>
        /// <param name="cancellationToken">Caller cancellation.</param>
        /// <returns>A task that completes when the run succeeds.</returns>
        /// <exception cref="System.OperationCanceledException">The run was canceled.</exception>
        /// <exception cref="System.AggregateException">A case failed or a suite hook failed. Inner exceptions are the originals.</exception>
        protected async Task RunAllAsync(CancellationToken cancellationToken = default)
        {
            TestResultCollector collector = new TestResultCollector();
            TestRunSummary summary = await TestExecutor.RunAsync(Suites, collector, Options, cancellationToken).ConfigureAwait(false);
            TestRunAssert.ThrowIfUnsuccessful(summary, collector.Results, cancellationToken);
        }
    }
}
