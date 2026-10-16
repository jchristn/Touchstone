#nullable enable
namespace Touchstone.Core
{
    using System;
    using System.Diagnostics;
    using System.Diagnostics.Metrics;
    internal static class ScenarioTelemetry
    {
        private static readonly ActivitySource _Source = new ActivitySource(TouchstoneTelemetry.ActivitySourceName, TouchstoneInstrumentation.Version);
        private static readonly Meter _Meter = new Meter(TouchstoneTelemetry.MeterName, TouchstoneInstrumentation.Version);
        private static readonly Counter<long> _Operations = _Meter.CreateCounter<long>(TouchstoneTelemetry.ScenarioOperations, "{operation}");
        private static readonly Histogram<double> _Duration = _Meter.CreateHistogram<double>(TouchstoneTelemetry.ScenarioOperationDuration, "s");

        internal static Activity? Start(string operation, ScenarioStepKind? kind = null)
        {
            try
            {
                Activity? activity = _Source.StartActivity("scenario:" + operation);
                activity?.SetTag("touchstone.step.kind", kind?.ToString());
                return activity;
            }
            catch { return null; }
        }
        internal static void Complete(Activity? activity, string operation, string outcome, TimeSpan duration, ScenarioStepKind? kind = null, Exception? exception = null)
        {
            try
            {
                activity?.SetTag("touchstone.outcome", outcome);
                if (exception != null) activity?.AddEvent(new ActivityEvent("exception", tags: new ActivityTagsCollection
                {
                    { "exception.type", exception.GetType().FullName }
                }));
                activity?.SetStatus(outcome == "Failed" ? ActivityStatusCode.Error : ActivityStatusCode.Unset);
                TagList tags = new TagList { { "operation", operation }, { "outcome", outcome }, { "kind", kind?.ToString() ?? "none" } };
                _Operations.Add(1, tags);
                _Duration.Record(duration.TotalSeconds, tags);
            }
            catch { }
            finally
            {
                try { activity?.Dispose(); } catch { }
            }
        }
    }
}
