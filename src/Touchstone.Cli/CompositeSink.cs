namespace Touchstone.Cli
{
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;

    using Touchstone.Core;

    /// <summary>
    /// Forwards every event to several sinks in order.
    /// </summary>
    internal sealed class CompositeSink : ITestResultSink
    {
        private readonly IReadOnlyList<ITestResultSink> _Sinks;

        internal CompositeSink(IReadOnlyList<ITestResultSink> sinks)
        {
            _Sinks = sinks;
        }

        public async ValueTask OnRunStartedAsync(IReadOnlyList<TestSuiteDescriptor> suites, CancellationToken cancellationToken)
        {
            foreach (ITestResultSink sink in _Sinks) await sink.OnRunStartedAsync(suites, cancellationToken).ConfigureAwait(false);
        }

        public async ValueTask OnSuiteStartedAsync(TestSuiteDescriptor suite, CancellationToken cancellationToken)
        {
            foreach (ITestResultSink sink in _Sinks) await sink.OnSuiteStartedAsync(suite, cancellationToken).ConfigureAwait(false);
        }

        public async ValueTask OnCaseStartedAsync(TestCaseDescriptor testCase, CancellationToken cancellationToken)
        {
            foreach (ITestResultSink sink in _Sinks) await sink.OnCaseStartedAsync(testCase, cancellationToken).ConfigureAwait(false);
        }

        public async ValueTask OnStepStartedAsync(string testId, ScenarioStepResult step, CancellationToken cancellationToken)
        {
            foreach (ITestResultSink sink in _Sinks) await sink.OnStepStartedAsync(testId, step, cancellationToken).ConfigureAwait(false);
        }

        public async ValueTask OnStepCompletedAsync(string testId, ScenarioStepResult step, CancellationToken cancellationToken)
        {
            foreach (ITestResultSink sink in _Sinks) await sink.OnStepCompletedAsync(testId, step, cancellationToken).ConfigureAwait(false);
        }

        public async ValueTask OnCaseCompletedAsync(TestResult result, CancellationToken cancellationToken)
        {
            foreach (ITestResultSink sink in _Sinks) await sink.OnCaseCompletedAsync(result, cancellationToken).ConfigureAwait(false);
        }

        public async ValueTask OnSuiteCompletedAsync(TestSuiteDescriptor suite, TestRunSummary summary, CancellationToken cancellationToken)
        {
            foreach (ITestResultSink sink in _Sinks) await sink.OnSuiteCompletedAsync(suite, summary, cancellationToken).ConfigureAwait(false);
        }

        public async ValueTask OnRunCompletedAsync(TestRunSummary summary, CancellationToken cancellationToken)
        {
            foreach (ITestResultSink sink in _Sinks) await sink.OnRunCompletedAsync(summary, cancellationToken).ConfigureAwait(false);
        }
    }
}
