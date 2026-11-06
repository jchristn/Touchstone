namespace Test.Shared
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;

    using Touchstone.Core;

    /// <summary>
    /// Sink that records every callback, the token it received, and optionally throws on one event.
    /// </summary>
    public sealed class RecordingSink : ITestResultSink
    {
        private readonly List<string> _Events = new List<string>();
        private readonly List<TestResult> _Results = new List<TestResult>();
        private readonly Dictionary<string, CancellationToken> _Tokens = new Dictionary<string, CancellationToken>(StringComparer.Ordinal);
        private readonly string? _ThrowOn;
        private readonly Action<string>? _OnEvent;

        /// <summary>
        /// Initialize a recording sink.
        /// </summary>
        /// <param name="throwOn">Event name (for example <c>case_completed</c>) that throws <see cref="InvalidOperationException"/>, or null.</param>
        /// <param name="onEvent">Callback run for every event name before recording, or null.</param>
        public RecordingSink(string? throwOn = null, Action<string>? onEvent = null)
        {
            _ThrowOn = throwOn;
            _OnEvent = onEvent;
        }

        /// <summary>
        /// Every event, formatted as <c>event:identity</c>, in order.
        /// </summary>
        public IReadOnlyList<string> Events
        {
            get { return _Events; }
        }

        /// <summary>
        /// Every completed case result, in order.
        /// </summary>
        public IReadOnlyList<TestResult> Results
        {
            get { return _Results; }
        }

        /// <summary>
        /// The last token received per event name.
        /// </summary>
        public IReadOnlyDictionary<string, CancellationToken> Tokens
        {
            get { return _Tokens; }
        }

        /// <summary>
        /// The summary received by <c>OnRunCompletedAsync</c>, or null.
        /// </summary>
        public TestRunSummary? RunSummary { get; private set; }

        /// <summary>
        /// The summaries received by <c>OnSuiteCompletedAsync</c>, in order.
        /// </summary>
        public List<TestRunSummary> SuiteSummaries { get; } = new List<TestRunSummary>();

        /// <inheritdoc />
        public ValueTask OnRunStartedAsync(IReadOnlyList<TestSuiteDescriptor> suites, CancellationToken cancellationToken)
        {
            return Record(TouchstoneTelemetry.EventRunStarted, suites.Count.ToString(System.Globalization.CultureInfo.InvariantCulture), cancellationToken);
        }

        /// <inheritdoc />
        public ValueTask OnSuiteStartedAsync(TestSuiteDescriptor suite, CancellationToken cancellationToken)
        {
            return Record(TouchstoneTelemetry.EventSuiteStarted, suite.SuiteId, cancellationToken);
        }

        /// <inheritdoc />
        public ValueTask OnCaseStartedAsync(TestCaseDescriptor testCase, CancellationToken cancellationToken)
        {
            return Record(TouchstoneTelemetry.EventCaseStarted, testCase.TestId, cancellationToken);
        }

        /// <inheritdoc />
        public ValueTask OnStepStartedAsync(string testId, ScenarioStepResult step, CancellationToken cancellationToken)
        {
            return Record("step_started", testId + "/" + step.StepId, cancellationToken);
        }

        /// <inheritdoc />
        public ValueTask OnStepCompletedAsync(string testId, ScenarioStepResult step, CancellationToken cancellationToken)
        {
            return Record("step_completed", testId + "/" + step.StepId + "=" + step.Outcome, cancellationToken);
        }

        /// <inheritdoc />
        public ValueTask OnCaseCompletedAsync(TestResult result, CancellationToken cancellationToken)
        {
            _Results.Add(result);
            return Record(TouchstoneTelemetry.EventCaseCompleted, result.TestId + "=" + result.Outcome, cancellationToken);
        }

        /// <inheritdoc />
        public ValueTask OnSuiteCompletedAsync(TestSuiteDescriptor suite, TestRunSummary summary, CancellationToken cancellationToken)
        {
            SuiteSummaries.Add(summary);
            return Record(TouchstoneTelemetry.EventSuiteCompleted, suite.SuiteId, cancellationToken);
        }

        /// <inheritdoc />
        public ValueTask OnRunCompletedAsync(TestRunSummary summary, CancellationToken cancellationToken)
        {
            RunSummary = summary;
            return Record(TouchstoneTelemetry.EventRunCompleted, "run", cancellationToken);
        }

        private ValueTask Record(string name, string identity, CancellationToken cancellationToken)
        {
            _OnEvent?.Invoke(name);
            _Tokens[name] = cancellationToken;
            _Events.Add(name + ":" + identity);
            if (name == _ThrowOn) throw new InvalidOperationException("sink failure on " + name);
            return default;
        }
    }
}
