namespace Touchstone.Cli
{
    using Touchstone.Core;

    /// <summary>
    /// Lowercase outcome and kind names used by every report. They match the Touchstone telemetry outcome values.
    /// </summary>
    internal static class OutcomeNames
    {
        internal static string Of(TestOutcome outcome)
        {
            switch (outcome)
            {
                case TestOutcome.Passed: return TouchstoneTelemetry.OutcomePassed;
                case TestOutcome.Failed: return TouchstoneTelemetry.OutcomeFailed;
                case TestOutcome.Skipped: return TouchstoneTelemetry.OutcomeSkipped;
                case TestOutcome.Canceled: return TouchstoneTelemetry.OutcomeCanceled;
                default: return TouchstoneTelemetry.OutcomeNotRun;
            }
        }

        internal static string Of(ScenarioStepOutcome outcome)
        {
            switch (outcome)
            {
                case ScenarioStepOutcome.Passed: return TouchstoneTelemetry.OutcomePassed;
                case ScenarioStepOutcome.Failed: return TouchstoneTelemetry.OutcomeFailed;
                case ScenarioStepOutcome.Skipped: return TouchstoneTelemetry.OutcomeSkipped;
                case ScenarioStepOutcome.Canceled: return TouchstoneTelemetry.OutcomeCanceled;
                default: return TouchstoneTelemetry.OutcomeNotRun;
            }
        }

        internal static string Of(ScenarioStepKind kind)
        {
            switch (kind)
            {
                case ScenarioStepKind.Given: return "given";
                case ScenarioStepKind.When: return "when";
                default: return "then";
            }
        }

        internal static string Label(TestOutcome outcome)
        {
            switch (outcome)
            {
                case TestOutcome.Passed: return "PASS";
                case TestOutcome.Failed: return "FAIL";
                case TestOutcome.Skipped: return "SKIP";
                case TestOutcome.Canceled: return "CANCELED";
                default: return "NOT RUN";
            }
        }
    }
}
