namespace Touchstone.Core
{
    using System;
    using System.Diagnostics;
    using System.Diagnostics.Metrics;

    /// <summary>
    /// Records scenario context, step, and cleanup telemetry on the shared Touchstone meter and activity source.
    /// Every method is best-effort: a failure inside telemetry never changes test outcomes.
    /// </summary>
    internal static class ScenarioTelemetry
    {
        private const string KindNone = "none";

        private static readonly Counter<long> _Operations = TouchstoneInstrumentation.Meter.CreateCounter<long>(
            TouchstoneTelemetry.ScenarioOperations, "{operation}", "Scenario context, step, and cleanup operations.");
        private static readonly Histogram<double> _Duration = TouchstoneInstrumentation.Meter.CreateHistogram<double>(
            TouchstoneTelemetry.ScenarioOperationDuration, "s", "Duration of scenario operations.");

        internal static Activity? Start(string operation, ScenarioStepKind? kind = null)
        {
            try
            {
                Activity? activity = TouchstoneInstrumentation.Source.StartActivity(SpanName(operation), ActivityKind.Internal);
                if (activity != null && kind.HasValue) activity.SetTag(TouchstoneTelemetry.AttributeStepKind, KindName(kind.Value));
                return activity;
            }
            catch
            {
                // Telemetry must never change test outcomes.
                return null;
            }
        }

        internal static void Complete(Activity? activity, string operation, string outcome, TimeSpan duration, ScenarioStepKind? kind = null, Exception? exception = null)
        {
            try
            {
                TagList tags = new TagList
                {
                    { TouchstoneTelemetry.LabelOperation, operation },
                    { TouchstoneTelemetry.LabelOutcome, outcome },
                    { TouchstoneTelemetry.LabelKind, kind.HasValue ? KindName(kind.Value) : KindNone },
                };
                _Operations.Add(1, tags);
                _Duration.Record(duration.TotalSeconds, tags);

                if (activity != null)
                {
                    activity.SetTag(TouchstoneTelemetry.AttributeOutcome, outcome);
                    if (exception != null)
                    {
                        activity.SetTag(TouchstoneTelemetry.LabelErrorType, TouchstoneInstrumentation.ErrorType(exception));
                        TouchstoneInstrumentation.AddExceptionEvent(activity, exception);
                    }

                    activity.SetStatus(outcome == TouchstoneTelemetry.OutcomeFailed ? ActivityStatusCode.Error : ActivityStatusCode.Ok);
                }
            }
            catch
            {
                // Telemetry must never change test outcomes.
            }
            finally
            {
                try
                {
                    activity?.Dispose();
                }
                catch
                {
                    // A listener threw while the span stopped. Restore the ambient span so later spans keep the right parent.
                    if (activity != null && Activity.Current == activity) Activity.Current = activity.Parent;
                }
            }
        }

        private static string SpanName(string operation)
        {
            switch (operation)
            {
                case TouchstoneTelemetry.ScenarioOperationContext: return TouchstoneTelemetry.ScenarioContextSpanName;
                case TouchstoneTelemetry.ScenarioOperationStep: return TouchstoneTelemetry.ScenarioStepSpanName;
                default: return TouchstoneTelemetry.ScenarioCleanupSpanName;
            }
        }

        private static string KindName(ScenarioStepKind kind)
        {
            switch (kind)
            {
                case ScenarioStepKind.Given: return "given";
                case ScenarioStepKind.When: return "when";
                default: return "then";
            }
        }
    }
}
