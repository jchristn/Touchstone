namespace Test.Shared
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using Touchstone.Core;

    /// <summary>
    /// Result sink that records every event it receives, in order, for later inspection.
    /// </summary>
    public sealed class RecordingSink : ITestResultSink
    {
        private readonly List<string> _Events = new List<string>();
        private readonly List<TestResult> _Results = new List<TestResult>();
        private readonly List<CancellationToken> _Tokens = new List<CancellationToken>();
        private TestSuiteDescriptor? _StartedSuite = null;
        private TestSuiteDescriptor? _CompletedSuite = null;
        private TestRunSummary? _CompletedSummary = null;
        private Action<TestResult>? _OnTestCompleted = null;

        /// <summary>
        /// Initialize a new recording sink.
        /// </summary>
        /// <param name="onTestCompleted">Optional callback invoked for each completed test. May throw to simulate a faulty sink.</param>
        public RecordingSink(Action<TestResult>? onTestCompleted = null)
        {
            _OnTestCompleted = onTestCompleted;
        }

        /// <summary>
        /// Ordered event log. Entries are "start:SuiteId", "test:TestId", and "complete:SuiteId".
        /// </summary>
        public IReadOnlyList<string> Events
        {
            get { return _Events; }
        }

        /// <summary>
        /// Results received through <see cref="OnTestCompletedAsync"/>, in order.
        /// </summary>
        public IReadOnlyList<TestResult> Results
        {
            get { return _Results; }
        }

        /// <summary>
        /// Cancellation tokens received across all callbacks, in order.
        /// </summary>
        public IReadOnlyList<CancellationToken> Tokens
        {
            get { return _Tokens; }
        }

        /// <summary>
        /// Suite passed to the most recent <see cref="OnSuiteStartedAsync"/> call. Null when never called.
        /// </summary>
        public TestSuiteDescriptor? StartedSuite
        {
            get { return _StartedSuite; }
        }

        /// <summary>
        /// Suite passed to the most recent <see cref="OnSuiteCompletedAsync"/> call. Null when never called.
        /// </summary>
        public TestSuiteDescriptor? CompletedSuite
        {
            get { return _CompletedSuite; }
        }

        /// <summary>
        /// Summary passed to the most recent <see cref="OnSuiteCompletedAsync"/> call. Null when never called.
        /// </summary>
        public TestRunSummary? CompletedSummary
        {
            get { return _CompletedSummary; }
        }

        /// <inheritdoc />
        public ValueTask OnSuiteStartedAsync(TestSuiteDescriptor suite, CancellationToken cancellationToken)
        {
            _StartedSuite = suite;
            _Tokens.Add(cancellationToken);
            _Events.Add("start:" + suite.SuiteId);
            return ValueTask.CompletedTask;
        }

        /// <inheritdoc />
        public ValueTask OnTestCompletedAsync(TestResult result, CancellationToken cancellationToken)
        {
            _Results.Add(result);
            _Tokens.Add(cancellationToken);
            _Events.Add("test:" + result.TestId);
            _OnTestCompleted?.Invoke(result);
            return ValueTask.CompletedTask;
        }

        /// <inheritdoc />
        public ValueTask OnSuiteCompletedAsync(TestSuiteDescriptor suite, TestRunSummary summary, CancellationToken cancellationToken)
        {
            _CompletedSuite = suite;
            _CompletedSummary = summary;
            _Tokens.Add(cancellationToken);
            _Events.Add("complete:" + suite.SuiteId);
            return ValueTask.CompletedTask;
        }
    }
}
