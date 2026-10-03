namespace Touchstone.Core
{
    /// <summary>
    /// Public telemetry contract for Touchstone: the meter and activity source names, every metric and span name,
    /// and the label and attribute keys. Dashboards and alerts depend on these values, so treat them as public API.
    /// Touchstone emits through <see cref="System.Diagnostics.Metrics.Meter"/> and
    /// <see cref="System.Diagnostics.ActivitySource"/> only. Nothing is exported unless the host subscribes a
    /// collector to <see cref="MeterName"/> and <see cref="ActivitySourceName"/>, and emission with no listener is
    /// effectively free. Thread safety: all members are safe to read from any thread.
    /// </summary>
    public static class TouchstoneTelemetry
    {
        private static volatile bool _SuiteLabelEnabled = true;

        /// <summary>
        /// Name of the meter that carries every Touchstone metric.
        /// </summary>
        public const string MeterName = "Touchstone";

        /// <summary>
        /// Name of the activity source that carries every Touchstone span.
        /// </summary>
        public const string ActivitySourceName = "Touchstone";

        /// <summary>
        /// Histogram (seconds) of multi-suite run durations, labeled by outcome.
        /// </summary>
        public const string RunDuration = "touchstone.run.duration";

        /// <summary>
        /// Counter of multi-suite runs, labeled by outcome.
        /// </summary>
        public const string Runs = "touchstone.runs";

        /// <summary>
        /// Observable gauge (Unix seconds) of the last multi-suite run in which no test failed.
        /// </summary>
        public const string RunLastSuccessTime = "touchstone.run.last_success_time";

        /// <summary>
        /// Histogram (seconds) of suite durations, labeled by suite and outcome.
        /// </summary>
        public const string SuiteDuration = "touchstone.suite.duration";

        /// <summary>
        /// Counter of suite executions, labeled by suite and outcome.
        /// </summary>
        public const string Suites = "touchstone.suites";

        /// <summary>
        /// Observable gauge (Unix seconds) of the last time each suite completed with no failed test.
        /// </summary>
        public const string SuiteLastSuccessTime = "touchstone.suite.last_success_time";

        /// <summary>
        /// Histogram (seconds) of suite lifecycle stage durations (before_suite, after_suite), labeled by suite, stage, and outcome.
        /// </summary>
        public const string StageDuration = "touchstone.stage.duration";

        /// <summary>
        /// Counter of suite lifecycle stage executions, labeled by suite, stage, and outcome.
        /// </summary>
        public const string Stages = "touchstone.stages";

        /// <summary>
        /// Histogram (seconds) of test case durations, labeled by suite and outcome.
        /// </summary>
        public const string CaseDuration = "touchstone.case.duration";

        /// <summary>
        /// Counter of test case results, labeled by suite and outcome.
        /// </summary>
        public const string Cases = "touchstone.cases";

        /// <summary>
        /// Up-down counter of test cases currently executing, labeled by suite.
        /// </summary>
        public const string CasesActive = "touchstone.cases.active";

        /// <summary>
        /// Histogram (seconds) of result sink callback durations, labeled by event and outcome.
        /// </summary>
        public const string SinkDuration = "touchstone.sink.duration";

        /// <summary>
        /// Counter of result sink callbacks, labeled by event and outcome.
        /// </summary>
        public const string SinkEvents = "touchstone.sink.events";

        /// <summary>
        /// Counter of failures, labeled by stage, error.type, and suite.
        /// </summary>
        public const string Errors = "touchstone.errors";

        /// <summary>
        /// Observable gauge that is always 1, labeled with the Touchstone version.
        /// </summary>
        public const string BuildInfo = "touchstone.build.info";

        /// <summary>
        /// Histogram (seconds) of result export durations (Touchstone.Cli), labeled by format and outcome.
        /// </summary>
        public const string ExportDuration = "touchstone.export.duration";

        /// <summary>
        /// Counter of result exports (Touchstone.Cli), labeled by format, outcome, and error.type.
        /// </summary>
        public const string Exports = "touchstone.exports";

        /// <summary>
        /// Span name of a multi-suite run.
        /// </summary>
        public const string RunSpanName = "touchstone.run";

        /// <summary>
        /// Span name prefix of a suite. The full name is the prefix followed by the suite identifier.
        /// </summary>
        public const string SuiteSpanPrefix = "suite:";

        /// <summary>
        /// Span name prefix of a suite lifecycle stage. The full name is the prefix followed by the stage name.
        /// </summary>
        public const string StageSpanPrefix = "stage:";

        /// <summary>
        /// Span name prefix of a test case. The full name is the prefix followed by the test identifier.
        /// </summary>
        public const string CaseSpanPrefix = "case:";

        /// <summary>
        /// Span name prefix of a result sink callback. The full name is the prefix followed by the event name.
        /// </summary>
        public const string SinkSpanPrefix = "sink ";

        /// <summary>
        /// Span name prefix of a result export. The full name is the prefix followed by the format.
        /// </summary>
        public const string ExportSpanPrefix = "export ";

        /// <summary>
        /// Metric label: suite identifier.
        /// </summary>
        public const string LabelSuite = "suite";

        /// <summary>
        /// Metric label: outcome of the measured operation.
        /// </summary>
        public const string LabelOutcome = "outcome";

        /// <summary>
        /// Metric label: lifecycle stage or failing stage.
        /// </summary>
        public const string LabelStage = "stage";

        /// <summary>
        /// Metric label: result sink event.
        /// </summary>
        public const string LabelEvent = "event";

        /// <summary>
        /// Metric label and span attribute: fully qualified exception type name (OpenTelemetry error.type).
        /// </summary>
        public const string LabelErrorType = "error.type";

        /// <summary>
        /// Metric label: export format.
        /// </summary>
        public const string LabelFormat = "format";

        /// <summary>
        /// Metric label: Touchstone version.
        /// </summary>
        public const string LabelVersion = "version";

        /// <summary>
        /// Span attribute: suite identifier.
        /// </summary>
        public const string AttributeSuiteId = "touchstone.suite.id";

        /// <summary>
        /// Span attribute: suite display name.
        /// </summary>
        public const string AttributeSuiteDisplayName = "touchstone.suite.display_name";

        /// <summary>
        /// Span attribute: case identifier within its suite.
        /// </summary>
        public const string AttributeCaseId = "touchstone.case.id";

        /// <summary>
        /// Span attribute: composite test identifier (suite.case).
        /// </summary>
        public const string AttributeTestId = "touchstone.test.id";

        /// <summary>
        /// Span attribute: case display name.
        /// </summary>
        public const string AttributeCaseDisplayName = "touchstone.case.display_name";

        /// <summary>
        /// Span attribute: case tags.
        /// </summary>
        public const string AttributeCaseTags = "touchstone.case.tags";

        /// <summary>
        /// Span attribute: case skip reason.
        /// </summary>
        public const string AttributeSkipReason = "touchstone.case.skip_reason";

        /// <summary>
        /// Span attribute: outcome of the span's operation.
        /// </summary>
        public const string AttributeOutcome = "touchstone.outcome";

        /// <summary>
        /// Span attribute: number of suites in a run.
        /// </summary>
        public const string AttributeSuiteCount = "touchstone.suite.count";

        /// <summary>
        /// Span attribute: number of cases in a suite or run.
        /// </summary>
        public const string AttributeCaseCount = "touchstone.case.count";

        /// <summary>
        /// Span attribute: number of passed cases in a suite or run.
        /// </summary>
        public const string AttributePassedCount = "touchstone.case.passed";

        /// <summary>
        /// Span attribute: number of failed cases in a suite or run.
        /// </summary>
        public const string AttributeFailedCount = "touchstone.case.failed";

        /// <summary>
        /// Span attribute: number of skipped cases in a suite or run.
        /// </summary>
        public const string AttributeSkippedCount = "touchstone.case.skipped";

        /// <summary>
        /// Span attribute: result count written by an export.
        /// </summary>
        public const string AttributeResultCount = "touchstone.export.result_count";

        /// <summary>
        /// Outcome: the case passed, or the suite or run completed with no failed case.
        /// </summary>
        public const string OutcomePassed = "passed";

        /// <summary>
        /// Outcome: the case failed, or the suite or run completed with at least one failed case.
        /// </summary>
        public const string OutcomeFailed = "failed";

        /// <summary>
        /// Outcome: the case was skipped.
        /// </summary>
        public const string OutcomeSkipped = "skipped";

        /// <summary>
        /// Outcome: an exception escaped the suite or run (hook, sink, or argument failure).
        /// </summary>
        public const string OutcomeError = "error";

        /// <summary>
        /// Outcome: an OperationCanceledException escaped the suite or run.
        /// </summary>
        public const string OutcomeCanceled = "canceled";

        /// <summary>
        /// Outcome: a lifecycle stage, sink callback, or export completed without throwing.
        /// </summary>
        public const string OutcomeOk = "ok";

        /// <summary>
        /// Stage: the suite's BeforeSuiteAsync hook.
        /// </summary>
        public const string StageBeforeSuite = "before_suite";

        /// <summary>
        /// Stage: the suite's AfterSuiteAsync hook.
        /// </summary>
        public const string StageAfterSuite = "after_suite";

        /// <summary>
        /// Stage: a multi-suite run as a whole (used on touchstone.errors when an exception escapes the run).
        /// </summary>
        public const string StageRun = "run";

        /// <summary>
        /// Stage: a test case body (used on touchstone.errors).
        /// </summary>
        public const string StageCase = "case";

        /// <summary>
        /// Stage: a result sink callback (used on touchstone.errors).
        /// </summary>
        public const string StageSink = "sink";

        /// <summary>
        /// Sink event: OnSuiteStartedAsync.
        /// </summary>
        public const string EventSuiteStarted = "suite_started";

        /// <summary>
        /// Sink event: OnTestCompletedAsync.
        /// </summary>
        public const string EventTestCompleted = "test_completed";

        /// <summary>
        /// Sink event: OnSuiteCompletedAsync.
        /// </summary>
        public const string EventSuiteCompleted = "suite_completed";

        /// <summary>
        /// Whether metrics carry the <see cref="LabelSuite"/> label. Default true.
        /// Suite identifiers are normally a small, fixed set defined in code, so the label is bounded.
        /// Set to false when suites are generated dynamically (for example one suite per tenant or per input file)
        /// so the suite label cannot grow without bound. Spans always carry the suite identifier.
        /// </summary>
        public static bool SuiteLabelEnabled
        {
            get { return _SuiteLabelEnabled; }
            set { _SuiteLabelEnabled = value; }
        }
    }
}
