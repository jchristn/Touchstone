namespace Touchstone.Cli
{
    using System;
    using System.Diagnostics;
    using System.Diagnostics.Metrics;

    using Touchstone.Core;

    /// <summary>
    /// Export instruments for the console runner. Emits on the same meter and activity source names as
    /// Touchstone.Core, so a host that subscribes to <see cref="TouchstoneTelemetry.MeterName"/> receives both.
    /// Every method is best-effort and never throws.
    /// </summary>
    internal static class CliInstrumentation
    {
        internal const string FormatJson = "json";

        private const int MaxExceptionMessageLength = 4096;

        private static readonly string _Version = typeof(CliInstrumentation).Assembly.GetName().Version?.ToString() ?? "unknown";
        private static readonly ActivitySource _Source = new ActivitySource(TouchstoneTelemetry.ActivitySourceName, _Version);
        private static readonly Meter _Meter = new Meter(TouchstoneTelemetry.MeterName, _Version);

        private static readonly Histogram<double> _ExportDuration = _Meter.CreateHistogram<double>(
            TouchstoneTelemetry.ExportDuration, "s", "Duration of a result export.");
        private static readonly Counter<long> _Exports = _Meter.CreateCounter<long>(
            TouchstoneTelemetry.Exports, "{export}", "Result exports by format and outcome.");

        internal static Activity StartExport(string format, int resultCount)
        {
            try
            {
                Activity activity = _Source.StartActivity(TouchstoneTelemetry.ExportSpanPrefix + format, ActivityKind.Internal);
                activity?.SetTag(TouchstoneTelemetry.AttributeResultCount, resultCount);
                return activity;
            }
            catch
            {
                return null;
            }
        }

        internal static void CompleteExport(Activity activity, string format, TimeSpan elapsed, Exception exception)
        {
            try
            {
                string outcome = exception == null ? TouchstoneTelemetry.OutcomeOk : TouchstoneTelemetry.OutcomeFailed;
                TagList durationTags = new TagList
                {
                    { TouchstoneTelemetry.LabelFormat, format },
                    { TouchstoneTelemetry.LabelOutcome, outcome },
                };
                _ExportDuration.Record(elapsed.TotalSeconds, durationTags);

                TagList countTags = durationTags;
                if (exception != null)
                    countTags.Add(TouchstoneTelemetry.LabelErrorType, exception.GetType().FullName);
                _Exports.Add(1, countTags);

                if (activity == null) return;

                activity.SetTag(TouchstoneTelemetry.AttributeOutcome, outcome);
                if (exception != null)
                {
                    string message = exception.Message;
                    if (message != null && message.Length > MaxExceptionMessageLength)
                        message = message.Substring(0, MaxExceptionMessageLength);

                    activity.SetTag(TouchstoneTelemetry.LabelErrorType, exception.GetType().FullName);
                    activity.AddEvent(new ActivityEvent("exception", DateTimeOffset.UtcNow, new ActivityTagsCollection
                    {
                        { "exception.type", exception.GetType().FullName },
                        { "exception.message", message },
                        { "exception.stacktrace", exception.ToString() },
                    }));
                    activity.SetStatus(ActivityStatusCode.Error, message);
                }
                else
                {
                    activity.SetStatus(ActivityStatusCode.Ok);
                }

                activity.Dispose();
            }
            catch
            {
            }
        }
    }
}
