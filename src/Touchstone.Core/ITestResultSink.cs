namespace Touchstone.Core
{
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// Receives run, suite, case, and step events as they happen. Every member has a no-op default, so implement only
    /// the events you need. New members added in later 1.x releases will also have defaults.
    /// A callback that throws stops the run: the current suite's teardown hook runs, and the exception propagates to
    /// the caller. Start callbacks receive the caller's token; completion callbacks receive a token that is not
    /// canceled, so results are always delivered.
    /// Thread safety: the engine calls a sink from one logical flow per run. A sink shared by concurrent runs must
    /// synchronize.
    /// </summary>
    public interface ITestResultSink
    {
        /// <summary>
        /// Called once when a multi-suite run starts, before any suite. Not called by single-suite or single-case entry points.
        /// </summary>
        /// <param name="suites">Suites in the run, in order.</param>
        /// <param name="cancellationToken">The caller's cancellation token.</param>
        /// <returns>A task that completes when the sink has handled the event.</returns>
        ValueTask OnRunStartedAsync(IReadOnlyList<TestSuiteDescriptor> suites, CancellationToken cancellationToken)
        {
            return default;
        }

        /// <summary>
        /// Called when a suite with at least one selected case starts, before its setup hook.
        /// </summary>
        /// <param name="suite">Suite that is starting.</param>
        /// <param name="cancellationToken">The caller's cancellation token.</param>
        /// <returns>A task that completes when the sink has handled the event.</returns>
        ValueTask OnSuiteStartedAsync(TestSuiteDescriptor suite, CancellationToken cancellationToken)
        {
            return default;
        }

        /// <summary>
        /// Called before a case body runs. Not called for skipped, filtered, or not-run cases.
        /// </summary>
        /// <param name="testCase">Case that is starting.</param>
        /// <param name="cancellationToken">The caller's cancellation token.</param>
        /// <returns>A task that completes when the sink has handled the event.</returns>
        ValueTask OnCaseStartedAsync(TestCaseDescriptor testCase, CancellationToken cancellationToken)
        {
            return default;
        }

        /// <summary>
        /// Called before a scenario step body runs. Cancellation has already been checked.
        /// </summary>
        /// <param name="testId">Identity of the scenario's case.</param>
        /// <param name="step">The step, with outcome <see cref="ScenarioStepOutcome.NotRun"/>.</param>
        /// <param name="cancellationToken">The caller's cancellation token.</param>
        /// <returns>A task that completes when the sink has handled the event.</returns>
        ValueTask OnStepStartedAsync(string testId, ScenarioStepResult step, CancellationToken cancellationToken)
        {
            return default;
        }

        /// <summary>
        /// Called after a scenario step completes.
        /// </summary>
        /// <param name="testId">Identity of the scenario's case.</param>
        /// <param name="step">The completed step.</param>
        /// <param name="cancellationToken">A token that is not canceled.</param>
        /// <returns>A task that completes when the sink has handled the event.</returns>
        ValueTask OnStepCompletedAsync(string testId, ScenarioStepResult step, CancellationToken cancellationToken)
        {
            return default;
        }

        /// <summary>
        /// Called once for every selected case with its final result, including skipped, canceled, and not-run cases.
        /// </summary>
        /// <param name="result">The case result.</param>
        /// <param name="cancellationToken">A token that is not canceled.</param>
        /// <returns>A task that completes when the sink has handled the event.</returns>
        ValueTask OnCaseCompletedAsync(TestResult result, CancellationToken cancellationToken)
        {
            return default;
        }

        /// <summary>
        /// Called when a suite finishes, after its teardown hook.
        /// </summary>
        /// <param name="suite">Suite that completed.</param>
        /// <param name="summary">The suite's summary.</param>
        /// <param name="cancellationToken">A token that is not canceled.</param>
        /// <returns>A task that completes when the sink has handled the event.</returns>
        ValueTask OnSuiteCompletedAsync(TestSuiteDescriptor suite, TestRunSummary summary, CancellationToken cancellationToken)
        {
            return default;
        }

        /// <summary>
        /// Called once when a multi-suite run finishes, with the summary the run returns.
        /// </summary>
        /// <param name="summary">The run summary.</param>
        /// <param name="cancellationToken">A token that is not canceled.</param>
        /// <returns>A task that completes when the sink has handled the event.</returns>
        ValueTask OnRunCompletedAsync(TestRunSummary summary, CancellationToken cancellationToken)
        {
            return default;
        }
    }
}
