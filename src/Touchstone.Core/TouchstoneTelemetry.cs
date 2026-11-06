namespace Touchstone.Core
{
    /// <summary>
    /// Public telemetry contract: the meter and activity source names, every metric and span name, and every label,
    /// attribute, and value. Dashboards and alerts depend on these values, so they are part of the public API and do
    /// not change within a major version.
    /// Touchstone emits through <see cref="System.Diagnostics.Metrics.Meter"/> and
    /// <see cref="System.Diagnostics.ActivitySource"/> only. Nothing is exported unless the host subscribes a collector
    /// to <see cref="MeterName"/> and <see cref="ActivitySourceName"/>, and emission with no listener is effectively free.
    /// Thread safety: all members are safe to read from any thread.
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
        /// Observable gauge (Unix seconds) of the last successful multi-suite run.
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
        /// Observable gauge (Unix seconds) of the last successful completion of each suite.
        /// </summary>
        public const string SuiteLastSuccessTime = "touchstone.suite.last_success_time";

        /// <summary>
        /// Histogram (seconds) of suite lifecycle stage durations, labeled by suite, stage, and outcome.
        /// </summary>
        public const string StageDuration = "touchstone.stage.duration";

        /// <summary>
        /// Counter of suite lifecycle stage executions, labeled by suite, stage, and outcome.
        /// </summary>
        public const string Stages = "touchstone.stages";

        /// <summary>
        /// Histogram (seconds) of case durations, labeled by suite and outcome.
        /// </summary>
        public const string CaseDuration = "touchstone.case.duration";

        /// <summary>
        /// Counter of case results, labeled by suite and outcome (including not_run).
        /// </summary>
        public const string Cases = "touchstone.cases";

        /// <summary>
        /// Up-down counter of cases currently executing, labeled by suite.
        /// </summary>
        public const string CasesActive = "touchstone.cases.active";

        /// <summary>
        /// Counter of cases excluded by the filter or by SkipBehavior.Omit, labeled by suite.
        /// </summary>
        public const string CasesFiltered = "touchstone.cases.filtered";

        /// <summary>
        /// Histogram (seconds) of result sink callback durations, labeled by event and outcome.
        /// </summary>
        public const string SinkDuration = "touchstone.sink.duration";

        /// <summary>
        /// Counter of result sink callbacks, labeled by event and outcome.
        /// </summary>
        public const string SinkEvents = "touchstone.sink.events";

        /// <summary>
        /// Counter of cases that exceeded <see cref="TestRunOptions.CaseTimeout"/>, labeled by suite (when enabled).
        /// Each is also counted in <see cref="Cases"/> with outcome failed.
        /// </summary>
        public const string CaseTimeouts = "touchstone.case.timeouts";

        /// <summary>
        /// Counter of failures, labeled by stage, error.type, and (when enabled) suite.
        /// </summary>
        public const string Errors = "touchstone.errors";

        /// <summary>
        /// Observable gauge that is always 1, labeled with the Touchstone version.
        /// </summary>
        public const string BuildInfo = "touchstone.build.info";

        /// <summary>
        /// Histogram (seconds) of report export durations (Touchstone.Cli), labeled by format and outcome.
        /// </summary>
        public const string ExportDuration = "touchstone.export.duration";

        /// <summary>
        /// Counter of report exports (Touchstone.Cli), labeled by format, outcome, and error.type.
        /// </summary>
        public const string Exports = "touchstone.exports";

        /// <summary>
        /// Counter of scenario context, step, and cleanup operations, labeled by operation, outcome, and kind.
        /// </summary>
        public const string ScenarioOperations = "touchstone.scenario.operations";

        /// <summary>
        /// Histogram (seconds) of scenario operation durations, labeled by operation, outcome, and kind.
        /// </summary>
        public const string ScenarioOperationDuration = "touchstone.scenario.operation.duration";

        /// <summary>
        /// Span name of a multi-suite run.
        /// </summary>
        public const string RunSpanName = "touchstone.run";

        /// <summary>
        /// Span name of a suite.
        /// </summary>
        public const string SuiteSpanName = "touchstone.suite";

        /// <summary>
        /// Span name of a suite lifecycle stage. The stage is in the stage attribute.
        /// </summary>
        public const string StageSpanName = "touchstone.stage";

        /// <summary>
        /// Span name of a case.
        /// </summary>
        public const string CaseSpanName = "touchstone.case";

        /// <summary>
        /// Span name of a result sink callback. The event is in the event attribute.
        /// </summary>
        public const string SinkSpanName = "touchstone.sink";

        /// <summary>
        /// Span name of a report export (Touchstone.Cli). The format is in the format attribute.
        /// </summary>
        public const string ExportSpanName = "touchstone.export";

        /// <summary>
        /// Span name of creating a scenario context.
        /// </summary>
        public const string ScenarioContextSpanName = "touchstone.scenario.context";

        /// <summary>
        /// Span name of running a scenario step.
        /// </summary>
        public const string ScenarioStepSpanName = "touchstone.scenario.step";

        /// <summary>
        /// Span name of scenario cleanup and context disposal.
        /// </summary>
        public const string ScenarioCleanupSpanName = "touchstone.scenario.cleanup";

        /// <summary>
        /// Metric label: suite identifier. Omitted when SuiteLabelEnabled is false.
        /// </summary>
        public const string LabelSuite = "suite";

        /// <summary>
        /// Metric label: outcome of the measured operation.
        /// </summary>
        public const string LabelOutcome = "outcome";

        /// <summary>
        /// Metric label and span attribute: lifecycle stage or failing stage.
        /// </summary>
        public const string LabelStage = "stage";

        /// <summary>
        /// Metric label and span attribute: result sink event.
        /// </summary>
        public const string LabelEvent = "event";

        /// <summary>
        /// Metric label and span attribute: fully qualified exception type name (OpenTelemetry error.type).
        /// </summary>
        public const string LabelErrorType = "error.type";

        /// <summary>
        /// Metric label and span attribute: export format.
        /// </summary>
        public const string LabelFormat = "format";

        /// <summary>
        /// Metric label: Touchstone version.
        /// </summary>
        public const string LabelVersion = "version";

        /// <summary>
        /// Metric label: scenario operation (context, step, or cleanup).
        /// </summary>
        public const string LabelOperation = "operation";

        /// <summary>
        /// Metric label: scenario step kind (given, when, then), or none for context and cleanup.
        /// </summary>
        public const string LabelKind = "kind";

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
        /// Span attribute: test identity, SuiteId.CaseId.
        /// </summary>
        public const string AttributeTestId = "touchstone.test.id";

        /// <summary>
        /// Span attribute: case display name.
        /// </summary>
        public const string AttributeCaseDisplayName = "touchstone.case.display_name";

        /// <summary>
        /// Span attribute: case tags, as a string array.
        /// </summary>
        public const string AttributeCaseTags = "touchstone.case.tags";

        /// <summary>
        /// Span attribute: skip reason of a skipped case.
        /// </summary>
        public const string AttributeSkipReason = "touchstone.case.skip_reason";

        /// <summary>
        /// Case span attribute set to true when the case exceeded <see cref="TestRunOptions.CaseTimeout"/>.
        /// </summary>
        public const string AttributeCaseTimedOut = "touchstone.case.timed_out";

        /// <summary>
        /// Run and suite span attribute: the <see cref="TestRunOptions.MaxDegreeOfParallelism"/> the run used.
        /// </summary>
        public const string AttributeMaxDegreeOfParallelism = "touchstone.max_degree_of_parallelism";

        /// <summary>
        /// Span attribute: scenario step kind (given, when, then).
        /// </summary>
        public const string AttributeStepKind = "touchstone.step.kind";

        /// <summary>
        /// Span attribute: outcome of the span's operation.
        /// </summary>
        public const string AttributeOutcome = "touchstone.outcome";

        /// <summary>
        /// Span attribute: number of suites in a run.
        /// </summary>
        public const string AttributeSuiteCount = "touchstone.suite.count";

        /// <summary>
        /// Span attribute: number of cases in a suite, or selected cases in a completed suite or run.
        /// </summary>
        public const string AttributeCaseCount = "touchstone.case.count";

        /// <summary>
        /// Span attribute: number of passed cases in a suite or run.
        /// </summary>
        public const string AttributePassedCount = "touchstone.passed_count";

        /// <summary>
        /// Span attribute: number of failed cases in a suite or run.
        /// </summary>
        public const string AttributeFailedCount = "touchstone.failed_count";

        /// <summary>
        /// Span attribute: number of skipped cases in a suite or run.
        /// </summary>
        public const string AttributeSkippedCount = "touchstone.skipped_count";

        /// <summary>
        /// Span attribute: number of canceled cases in a suite or run.
        /// </summary>
        public const string AttributeCanceledCount = "touchstone.canceled_count";

        /// <summary>
        /// Span attribute: number of selected cases that never started in a suite or run.
        /// </summary>
        public const string AttributeNotRunCount = "touchstone.not_run_count";

        /// <summary>
        /// Span attribute: number of cases excluded by the filter in a suite or run.
        /// </summary>
        public const string AttributeFilteredCount = "touchstone.filtered_count";

        /// <summary>
        /// Span attribute: number of results in a report export.
        /// </summary>
        public const string AttributeResultCount = "touchstone.export.result_count";

        /// <summary>
        /// Outcome: the case or operation passed, or the suite or run succeeded.
        /// </summary>
        public const string OutcomePassed = "passed";

        /// <summary>
        /// Outcome: the case or operation failed, or the suite or run had a failed case.
        /// </summary>
        public const string OutcomeFailed = "failed";

        /// <summary>
        /// Outcome: the case was skipped.
        /// </summary>
        public const string OutcomeSkipped = "skipped";

        /// <summary>
        /// Outcome: the case observed caller cancellation, or the suite or run was canceled.
        /// </summary>
        public const string OutcomeCanceled = "canceled";

        /// <summary>
        /// Outcome: the case was selected but never started.
        /// </summary>
        public const string OutcomeNotRun = "not_run";

        /// <summary>
        /// Outcome: a suite hook failed, or an exception escaped the suite or run (for example from a sink).
        /// </summary>
        public const string OutcomeError = "error";

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
        /// Stage: a multi-suite run as a whole, on touchstone.errors.
        /// </summary>
        public const string StageRun = "run";

        /// <summary>
        /// Stage: a case body, on touchstone.errors.
        /// </summary>
        public const string StageCase = "case";

        /// <summary>
        /// Stage: a result sink callback, on touchstone.errors.
        /// </summary>
        public const string StageSink = "sink";

        /// <summary>
        /// Sink event: OnRunStartedAsync.
        /// </summary>
        public const string EventRunStarted = "run_started";

        /// <summary>
        /// Sink event: OnSuiteStartedAsync.
        /// </summary>
        public const string EventSuiteStarted = "suite_started";

        /// <summary>
        /// Sink event: OnCaseStartedAsync.
        /// </summary>
        public const string EventCaseStarted = "case_started";

        /// <summary>
        /// Sink event: OnCaseCompletedAsync.
        /// </summary>
        public const string EventCaseCompleted = "case_completed";

        /// <summary>
        /// Sink event: OnSuiteCompletedAsync.
        /// </summary>
        public const string EventSuiteCompleted = "suite_completed";

        /// <summary>
        /// Sink event: OnRunCompletedAsync.
        /// </summary>
        public const string EventRunCompleted = "run_completed";

        /// <summary>
        /// Scenario operation: creating the per-execution context.
        /// </summary>
        public const string ScenarioOperationContext = "context";

        /// <summary>
        /// Scenario operation: running one step.
        /// </summary>
        public const string ScenarioOperationStep = "step";

        /// <summary>
        /// Scenario operation: cleanup and disposal of the context.
        /// </summary>
        public const string ScenarioOperationCleanup = "cleanup";

        /// <summary>
        /// Whether metrics carry the <see cref="LabelSuite"/> label. Default true.
        /// Suite identifiers are normally a small, fixed set defined in code, so the label is bounded. Set to false when
        /// suites are generated dynamically (for example one suite per tenant or per input file) so the label cannot grow
        /// without bound. Spans always carry the suite identifier.
        /// </summary>
        public static bool SuiteLabelEnabled
        {
            get { return _SuiteLabelEnabled; }
            set { _SuiteLabelEnabled = value; }
        }
    }
}
