namespace Touchstone.Core
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// Forwards every callback to an inner sink one at a time, so sinks written for sequential runs stay correct when
    /// cases run in parallel. Callbacks wait for their turn without observing cancellation, so no result is dropped.
    /// Thread safety: all members are safe to call concurrently.
    /// </summary>
    internal sealed class SerializedSink : ITestResultSink, IDisposable
    {
        private readonly ITestResultSink _Inner;
        private readonly SemaphoreSlim _Gate = new SemaphoreSlim(1, 1);
        private bool _Disposed = false;

        internal SerializedSink(ITestResultSink inner)
        {
            _Inner = inner ?? throw new ArgumentNullException(nameof(inner));
        }

        public ValueTask OnRunStartedAsync(IReadOnlyList<TestSuiteDescriptor> suites, CancellationToken cancellationToken)
        {
            return InvokeAsync(() => _Inner.OnRunStartedAsync(suites, cancellationToken));
        }

        public ValueTask OnSuiteStartedAsync(TestSuiteDescriptor suite, CancellationToken cancellationToken)
        {
            return InvokeAsync(() => _Inner.OnSuiteStartedAsync(suite, cancellationToken));
        }

        public ValueTask OnCaseStartedAsync(TestCaseDescriptor testCase, CancellationToken cancellationToken)
        {
            return InvokeAsync(() => _Inner.OnCaseStartedAsync(testCase, cancellationToken));
        }

        public ValueTask OnStepStartedAsync(string testId, ScenarioStepResult step, CancellationToken cancellationToken)
        {
            return InvokeAsync(() => _Inner.OnStepStartedAsync(testId, step, cancellationToken));
        }

        public ValueTask OnStepCompletedAsync(string testId, ScenarioStepResult step, CancellationToken cancellationToken)
        {
            return InvokeAsync(() => _Inner.OnStepCompletedAsync(testId, step, cancellationToken));
        }

        public ValueTask OnCaseCompletedAsync(TestResult result, CancellationToken cancellationToken)
        {
            return InvokeAsync(() => _Inner.OnCaseCompletedAsync(result, cancellationToken));
        }

        public ValueTask OnSuiteCompletedAsync(TestSuiteDescriptor suite, TestRunSummary summary, CancellationToken cancellationToken)
        {
            return InvokeAsync(() => _Inner.OnSuiteCompletedAsync(suite, summary, cancellationToken));
        }

        public ValueTask OnRunCompletedAsync(TestRunSummary summary, CancellationToken cancellationToken)
        {
            return InvokeAsync(() => _Inner.OnRunCompletedAsync(summary, cancellationToken));
        }

        public void Dispose()
        {
            Dispose(true);
        }

        private void Dispose(bool disposing)
        {
            if (_Disposed) return;
            _Disposed = true;
            if (disposing) _Gate.Dispose();
        }

        private async ValueTask InvokeAsync(Func<ValueTask> callback)
        {
            await _Gate.WaitAsync().ConfigureAwait(false);
            try
            {
                await callback().ConfigureAwait(false);
            }
            finally
            {
                _Gate.Release();
            }
        }
    }
}
