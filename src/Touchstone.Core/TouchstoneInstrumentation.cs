namespace Touchstone.Core
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Diagnostics.Metrics;
    using System.Reflection;

    /// <summary>
    /// Owns the Touchstone meter, activity source, and instruments, and records every measurement.
    /// Every method is best-effort: a failure inside telemetry is swallowed so it can never change test outcomes.
    /// </summary>
    internal static class TouchstoneInstrumentation
    {
        private const int MaxExceptionMessageLength = 4096;

        private static readonly string _Version = ResolveVersion();
        private static readonly ActivitySource _Source = new ActivitySource(TouchstoneTelemetry.ActivitySourceName, _Version);
        private static readonly Meter _Meter = new Meter(TouchstoneTelemetry.MeterName, _Version);

        private static readonly ConcurrentDictionary<string, double> _SuiteLastSuccess = new ConcurrentDictionary<string, double>(StringComparer.Ordinal);
        private static double _RunLastSuccess = double.NaN;

        private static readonly Histogram<double> _RunDuration = _Meter.CreateHistogram<double>(
            TouchstoneTelemetry.RunDuration, "s", "Duration of a multi-suite run.");
        private static readonly Counter<long> _Runs = _Meter.CreateCounter<long>(
            TouchstoneTelemetry.Runs, "{run}", "Multi-suite runs by outcome.");
        private static readonly Histogram<double> _SuiteDuration = _Meter.CreateHistogram<double>(
            TouchstoneTelemetry.SuiteDuration, "s", "Duration of a suite, including hooks and sink callbacks.");
        private static readonly Counter<long> _Suites = _Meter.CreateCounter<long>(
            TouchstoneTelemetry.Suites, "{suite}", "Suite executions by suite and outcome.");
        private static readonly Histogram<double> _StageDuration = _Meter.CreateHistogram<double>(
            TouchstoneTelemetry.StageDuration, "s", "Duration of a suite lifecycle stage.");
        private static readonly Counter<long> _Stages = _Meter.CreateCounter<long>(
            TouchstoneTelemetry.Stages, "{stage}", "Suite lifecycle stage executions by stage and outcome.");
        private static readonly Histogram<double> _CaseDuration = _Meter.CreateHistogram<double>(
            TouchstoneTelemetry.CaseDuration, "s", "Duration of a test case.");
        private static readonly Counter<long> _Cases = _Meter.CreateCounter<long>(
            TouchstoneTelemetry.Cases, "{case}", "Test case results by suite and outcome.");
        private static readonly UpDownCounter<long> _CasesActive = _Meter.CreateUpDownCounter<long>(
            TouchstoneTelemetry.CasesActive, "{case}", "Test cases currently executing.");
        private static readonly Histogram<double> _SinkDuration = _Meter.CreateHistogram<double>(
            TouchstoneTelemetry.SinkDuration, "s", "Duration of a result sink callback.");
        private static readonly Counter<long> _SinkEvents = _Meter.CreateCounter<long>(
            TouchstoneTelemetry.SinkEvents, "{event}", "Result sink callbacks by event and outcome.");
        private static readonly Counter<long> _Errors = _Meter.CreateCounter<long>(
            TouchstoneTelemetry.Errors, "{error}", "Failures by stage and error type.");

        static TouchstoneInstrumentation()
        {
            _Meter.CreateObservableGauge(
                TouchstoneTelemetry.SuiteLastSuccessTime, ObserveSuiteLastSuccess, "s",
                "Unix time of the last suite completion with no failed case.");
            _Meter.CreateObservableGauge(
                TouchstoneTelemetry.RunLastSuccessTime, ObserveRunLastSuccess, "s",
                "Unix time of the last multi-suite run with no failed case.");
            _Meter.CreateObservableGauge(
                TouchstoneTelemetry.BuildInfo, ObserveBuildInfo, "{info}",
                "Always 1, labeled with the Touchstone version.");
        }

        internal static string Version
        {
            get { return _Version; }
        }

        internal static Activity StartRun(int suiteCount)
        {
            try
            {
                Activity activity = _Source.StartActivity(TouchstoneTelemetry.RunSpanName, ActivityKind.Internal);
                activity?.SetTag(TouchstoneTelemetry.AttributeSuiteCount, suiteCount);
                return activity;
            }
            catch
            {
                return null;
            }
        }

        internal static void CompleteRun(Activity activity, TestRunSummary summary, TimeSpan elapsed, Exception exception)
        {
            try
            {
                string outcome = SummaryOutcome(summary, exception);
                TagList tags = new TagList { { TouchstoneTelemetry.LabelOutcome, outcome } };
                _RunDuration.Record(elapsed.TotalSeconds, tags);
                _Runs.Add(1, tags);

                if (outcome == TouchstoneTelemetry.OutcomePassed)
                    _RunLastSuccess = UnixNow();

                if (exception != null)
                    RecordError(TouchstoneTelemetry.StageRun, null, exception);
            }
            catch
            {
            }

            FinishSpan(activity, SummaryOutcome(summary, exception), exception, summary);
        }

        internal static Activity StartSuite(TestSuiteDescriptor suite)
        {
            try
            {
                Activity activity = _Source.StartActivity(TouchstoneTelemetry.SuiteSpanPrefix + suite.SuiteId, ActivityKind.Internal);
                if (activity != null)
                {
                    activity.SetTag(TouchstoneTelemetry.AttributeSuiteId, suite.SuiteId);
                    activity.SetTag(TouchstoneTelemetry.AttributeSuiteDisplayName, suite.DisplayName);
                    activity.SetTag(TouchstoneTelemetry.AttributeCaseCount, suite.Cases.Count);
                }

                return activity;
            }
            catch
            {
                return null;
            }
        }

        internal static void CompleteSuite(Activity activity, string suiteId, TestRunSummary summary, TimeSpan elapsed, Exception exception)
        {
            try
            {
                string outcome = SummaryOutcome(summary, exception);
                TagList tags = SuiteTags(suiteId);
                tags.Add(TouchstoneTelemetry.LabelOutcome, outcome);
                _SuiteDuration.Record(elapsed.TotalSeconds, tags);
                _Suites.Add(1, tags);

                if (outcome == TouchstoneTelemetry.OutcomePassed)
                    _SuiteLastSuccess[SuiteKey(suiteId)] = UnixNow();
            }
            catch
            {
            }

            FinishSpan(activity, SummaryOutcome(summary, exception), exception, summary);
        }

        internal static Activity StartStage(string suiteId, string stage)
        {
            try
            {
                Activity activity = _Source.StartActivity(TouchstoneTelemetry.StageSpanPrefix + stage, ActivityKind.Internal);
                activity?.SetTag(TouchstoneTelemetry.AttributeSuiteId, suiteId);
                return activity;
            }
            catch
            {
                return null;
            }
        }

        internal static void CompleteStage(Activity activity, string suiteId, string stage, TimeSpan elapsed, Exception exception)
        {
            try
            {
                string outcome = exception == null ? TouchstoneTelemetry.OutcomeOk : TouchstoneTelemetry.OutcomeFailed;
                TagList tags = SuiteTags(suiteId);
                tags.Add(TouchstoneTelemetry.LabelStage, stage);
                tags.Add(TouchstoneTelemetry.LabelOutcome, outcome);
                _StageDuration.Record(elapsed.TotalSeconds, tags);
                _Stages.Add(1, tags);

                if (exception != null)
                    RecordError(stage, suiteId, exception);
            }
            catch
            {
            }

            FinishSpan(activity, exception == null ? TouchstoneTelemetry.OutcomeOk : TouchstoneTelemetry.OutcomeFailed, exception, null);
        }

        internal static Activity StartCase(TestCaseDescriptor testCase)
        {
            try
            {
                Activity activity = _Source.StartActivity(TouchstoneTelemetry.CaseSpanPrefix + testCase.TestId, ActivityKind.Internal);
                if (activity != null)
                {
                    activity.SetTag(TouchstoneTelemetry.AttributeSuiteId, testCase.SuiteId);
                    activity.SetTag(TouchstoneTelemetry.AttributeCaseId, testCase.CaseId);
                    activity.SetTag(TouchstoneTelemetry.AttributeTestId, testCase.TestId);
                    activity.SetTag(TouchstoneTelemetry.AttributeCaseDisplayName, testCase.DisplayName);

                    if (testCase.Tags.Count > 0)
                    {
                        string[] caseTags = new string[testCase.Tags.Count];
                        for (int i = 0; i < caseTags.Length; i++)
                            caseTags[i] = testCase.Tags[i];
                        activity.SetTag(TouchstoneTelemetry.AttributeCaseTags, caseTags);
                    }

                    if (testCase.Skip && testCase.SkipReason != null)
                        activity.SetTag(TouchstoneTelemetry.AttributeSkipReason, testCase.SkipReason);
                }

                return activity;
            }
            catch
            {
                return null;
            }
        }

        internal static void CaseStarted(string suiteId)
        {
            try
            {
                _CasesActive.Add(1, SuiteTags(suiteId));
            }
            catch
            {
            }
        }

        internal static void CompleteCase(Activity activity, string suiteId, string outcome, TimeSpan elapsed, Exception exception, bool wasStarted)
        {
            try
            {
                if (wasStarted)
                    _CasesActive.Add(-1, SuiteTags(suiteId));

                TagList tags = SuiteTags(suiteId);
                tags.Add(TouchstoneTelemetry.LabelOutcome, outcome);
                _CaseDuration.Record(elapsed.TotalSeconds, tags);
                _Cases.Add(1, tags);

                if (exception != null)
                    RecordError(TouchstoneTelemetry.StageCase, suiteId, exception);
            }
            catch
            {
            }

            FinishSpan(activity, outcome, exception, null);
        }

        internal static Activity StartSink(string suiteId, string sinkEvent)
        {
            try
            {
                Activity activity = _Source.StartActivity(TouchstoneTelemetry.SinkSpanPrefix + sinkEvent, ActivityKind.Internal);
                activity?.SetTag(TouchstoneTelemetry.AttributeSuiteId, suiteId);
                return activity;
            }
            catch
            {
                return null;
            }
        }

        internal static void CompleteSink(Activity activity, string suiteId, string sinkEvent, TimeSpan elapsed, Exception exception)
        {
            try
            {
                string outcome = exception == null ? TouchstoneTelemetry.OutcomeOk : TouchstoneTelemetry.OutcomeFailed;
                TagList tags = new TagList
                {
                    { TouchstoneTelemetry.LabelEvent, sinkEvent },
                    { TouchstoneTelemetry.LabelOutcome, outcome },
                };
                _SinkDuration.Record(elapsed.TotalSeconds, tags);
                _SinkEvents.Add(1, tags);

                if (exception != null)
                    RecordError(TouchstoneTelemetry.StageSink, suiteId, exception);
            }
            catch
            {
            }

            FinishSpan(activity, exception == null ? TouchstoneTelemetry.OutcomeOk : TouchstoneTelemetry.OutcomeFailed, exception, null);
        }

        internal static string ErrorType(Exception exception)
        {
            if (exception == null) return null;
            return exception.GetType().FullName ?? "_OTHER";
        }

        internal static void AddExceptionEvent(Activity activity, Exception exception)
        {
            ActivityTagsCollection tags = new ActivityTagsCollection
            {
                { "exception.type", ErrorType(exception) },
                { "exception.message", Truncate(exception.Message) },
                { "exception.stacktrace", exception.ToString() },
            };
            activity.AddEvent(new ActivityEvent("exception", DateTimeOffset.UtcNow, tags));
        }

        internal static string Truncate(string value)
        {
            if (value == null || value.Length <= MaxExceptionMessageLength) return value;
            return value.Substring(0, MaxExceptionMessageLength);
        }

        private static void FinishSpan(Activity activity, string outcome, Exception exception, TestRunSummary summary)
        {
            if (activity == null) return;

            try
            {
                if (summary != null)
                {
                    activity.SetTag(TouchstoneTelemetry.AttributeCaseCount, summary.Total);
                    activity.SetTag(TouchstoneTelemetry.AttributePassedCount, summary.Passed);
                    activity.SetTag(TouchstoneTelemetry.AttributeFailedCount, summary.Failed);
                    activity.SetTag(TouchstoneTelemetry.AttributeSkippedCount, summary.Skipped);
                }

                activity.SetTag(TouchstoneTelemetry.AttributeOutcome, outcome);

                if (exception != null)
                {
                    activity.SetTag(TouchstoneTelemetry.LabelErrorType, ErrorType(exception));
                    AddExceptionEvent(activity, exception);
                    activity.SetStatus(ActivityStatusCode.Error, Truncate(exception.Message));
                }
                else if (outcome == TouchstoneTelemetry.OutcomeFailed && summary != null)
                {
                    activity.SetStatus(ActivityStatusCode.Error, summary.Failed.ToString() + " test(s) failed");
                }
                else
                {
                    activity.SetStatus(ActivityStatusCode.Ok);
                }
            }
            catch
            {
            }

            try
            {
                activity.Dispose();
            }
            catch
            {
                // A listener threw while the span stopped. Restore the ambient span so later spans keep the right parent.
                if (Activity.Current == activity)
                    Activity.Current = activity.Parent;
            }
        }

        private static void RecordError(string stage, string suiteId, Exception exception)
        {
            TagList tags = suiteId == null ? new TagList() : SuiteTags(suiteId);
            tags.Add(TouchstoneTelemetry.LabelStage, stage);
            tags.Add(TouchstoneTelemetry.LabelErrorType, ErrorType(exception));
            _Errors.Add(1, tags);
        }

        private static string SummaryOutcome(TestRunSummary summary, Exception exception)
        {
            if (exception is OperationCanceledException) return TouchstoneTelemetry.OutcomeCanceled;
            if (exception != null) return TouchstoneTelemetry.OutcomeError;
            return summary.IsSuccess ? TouchstoneTelemetry.OutcomePassed : TouchstoneTelemetry.OutcomeFailed;
        }

        private static TagList SuiteTags(string suiteId)
        {
            TagList tags = new TagList();
            if (TouchstoneTelemetry.SuiteLabelEnabled)
                tags.Add(TouchstoneTelemetry.LabelSuite, suiteId);
            return tags;
        }

        private static string SuiteKey(string suiteId)
        {
            return TouchstoneTelemetry.SuiteLabelEnabled ? suiteId : string.Empty;
        }

        private static double UnixNow()
        {
            return DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() / 1000.0;
        }

        private static IEnumerable<Measurement<double>> ObserveSuiteLastSuccess()
        {
            List<Measurement<double>> measurements = new List<Measurement<double>>();
            foreach (KeyValuePair<string, double> entry in _SuiteLastSuccess)
            {
                if (entry.Key.Length == 0)
                    measurements.Add(new Measurement<double>(entry.Value));
                else
                    measurements.Add(new Measurement<double>(entry.Value, new KeyValuePair<string, object>(TouchstoneTelemetry.LabelSuite, entry.Key)));
            }

            return measurements;
        }

        private static IEnumerable<Measurement<double>> ObserveRunLastSuccess()
        {
            double value = _RunLastSuccess;
            if (double.IsNaN(value)) return Array.Empty<Measurement<double>>();
            return new[] { new Measurement<double>(value) };
        }

        private static Measurement<long> ObserveBuildInfo()
        {
            return new Measurement<long>(1, new KeyValuePair<string, object>(TouchstoneTelemetry.LabelVersion, _Version));
        }

        private static string ResolveVersion()
        {
            try
            {
                Assembly assembly = typeof(TouchstoneInstrumentation).Assembly;
                AssemblyInformationalVersionAttribute info = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>();
                string version = info?.InformationalVersion ?? assembly.GetName().Version?.ToString() ?? "unknown";
                int plus = version.IndexOf('+');
                return plus > 0 ? version.Substring(0, plus) : version;
            }
            catch
            {
                return "unknown";
            }
        }
    }
}
