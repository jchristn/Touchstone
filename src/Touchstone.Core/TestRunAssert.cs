namespace Touchstone.Core
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Runtime.ExceptionServices;
    using System.Text;
    using System.Threading;

    /// <summary>
    /// Turns a completed run into a single host-level pass or failure. Framework adapters use this for their
    /// run-everything-in-one-test base classes.
    /// Thread safety: all members are safe to call concurrently.
    /// </summary>
    public static class TestRunAssert
    {
        /// <summary>
        /// Throw if a run was canceled or did not succeed.
        /// </summary>
        /// <param name="summary">The run summary.</param>
        /// <param name="results">Every case result from the run.</param>
        /// <param name="cancellationToken">The run's cancellation token, attached to the cancellation exception.</param>
        /// <exception cref="ArgumentNullException">The summary or result list is null.</exception>
        /// <exception cref="OperationCanceledException">The run was canceled.</exception>
        /// <exception cref="AggregateException">A case failed or was canceled, a case was not run, or a suite hook failed.
        /// The message lists every problem with its test identity and scenario diagnostics; the inner exceptions are
        /// the original exceptions, one per failed case and failed hook.</exception>
        public static void ThrowIfUnsuccessful(TestRunSummary summary, IReadOnlyList<TestResult> results, CancellationToken cancellationToken)
        {
            if (summary == null) throw new ArgumentNullException(nameof(summary));
            if (results == null) throw new ArgumentNullException(nameof(results));

            if (summary.WasCanceled) throw new OperationCanceledException("The Touchstone run was canceled.", cancellationToken);
            if (summary.IsSuccess) return;

            AggregateException? failure = CreateFailure(summary, results);
            if (failure != null) throw failure;
        }

        /// <summary>
        /// Throw if a single case did not pass. Returns normally for <see cref="TestOutcome.Passed"/>,
        /// <see cref="TestOutcome.Skipped"/>, and <see cref="TestOutcome.NotRun"/>.
        /// </summary>
        /// <param name="result">The case result.</param>
        /// <param name="cancellationToken">The run's cancellation token, attached to a cancellation exception that has to be created.</param>
        /// <exception cref="ArgumentNullException">The result is null.</exception>
        /// <exception cref="OperationCanceledException">The case was canceled.</exception>
        /// <exception cref="InvalidOperationException">The case failed without an exception (only possible for results built by callers).</exception>
        /// <remarks>For a failed case, the original exception is rethrown with its original stack trace.</remarks>
        public static void ThrowIfNotPassed(TestResult result, CancellationToken cancellationToken)
        {
            if (result == null) throw new ArgumentNullException(nameof(result));

            if (result.Outcome == TestOutcome.Failed)
            {
                if (result.Exception != null) ExceptionDispatchInfo.Capture(result.Exception).Throw();
                throw new InvalidOperationException(result.Message ?? (result.TestId + " failed."));
            }

            if (result.Outcome == TestOutcome.Canceled)
            {
                if (result.Exception is OperationCanceledException) ExceptionDispatchInfo.Capture(result.Exception).Throw();
                throw new OperationCanceledException(result.TestId + " was canceled.", result.Exception, cancellationToken);
            }
        }

        /// <summary>
        /// Build the exception <see cref="ThrowIfUnsuccessful"/> would throw for an unsuccessful, uncanceled run.
        /// </summary>
        /// <param name="summary">The run summary.</param>
        /// <param name="results">Every case result from the run.</param>
        /// <returns>The exception, or null when the run succeeded.</returns>
        /// <exception cref="ArgumentNullException">The summary or result list is null.</exception>
        public static AggregateException? CreateFailure(TestRunSummary summary, IReadOnlyList<TestResult> results)
        {
            if (summary == null) throw new ArgumentNullException(nameof(summary));
            if (results == null) throw new ArgumentNullException(nameof(results));
            if (summary.IsSuccess) return null;

            List<Exception> inner = new List<Exception>();
            StringBuilder message = new StringBuilder();
            int problems = 0;

            foreach (TestResult result in results)
            {
                if (result.Outcome != TestOutcome.Failed && result.Outcome != TestOutcome.Canceled && result.Outcome != TestOutcome.NotRun)
                    continue;

                problems++;
                message.AppendLine().Append(result.TestId).Append(" (").Append(result.Outcome).Append("): ").Append(result.Message);
                string diagnostics = ScenarioDiagnostics.Format(result);
                if (diagnostics.Length > 0) message.AppendLine().Append(diagnostics.TrimEnd());
                if (result.Exception != null) inner.Add(result.Exception);
            }

            foreach (SuiteLifecycleError error in summary.LifecycleErrors)
            {
                problems++;
                message.AppendLine().Append(error.SuiteId).Append(' ').Append(error.Stage).Append(": ").Append(error.Exception.Message);
                if (!inner.Contains(error.Exception)) inner.Add(error.Exception);
            }

            string header = problems.ToString(CultureInfo.InvariantCulture) + " Touchstone problem(s):";
            return new AggregateException(header + message.ToString(), inner);
        }
    }
}
