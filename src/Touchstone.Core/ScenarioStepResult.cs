#nullable enable
namespace Touchstone.Core
{
    using System;
    /// <summary>Immutable step execution snapshot. Safe to read concurrently after execution.</summary>
    public sealed class ScenarioStepResult
    {
        /// <summary>Stable step identity.</summary>
        public string StepId { get; }
        /// <summary>Human-readable step name. May contain consumer-supplied data.</summary>
        public string DisplayName { get; }
        /// <summary>Behavior intent.</summary>
        public ScenarioStepKind Kind { get; }
        /// <summary>Execution outcome.</summary>
        public ScenarioStepOutcome Outcome { get; }
        /// <summary>Elapsed execution time.</summary>
        public TimeSpan Duration { get; }
        /// <summary>Original failure, or null. Not serialized by reporters.</summary>
        public Exception? Exception { get; }
        /// <summary>Human-readable outcome reason, or null.</summary>
        public string? Message { get; }

        internal ScenarioStepResult(string stepId, string displayName, ScenarioStepKind kind,
            ScenarioStepOutcome outcome, TimeSpan duration, Exception? exception = null, string? message = null)
        {
            StepId = stepId;
            DisplayName = displayName;
            Kind = kind;
            Outcome = outcome;
            Duration = duration;
            Exception = exception;
            Message = message ?? exception?.Message;
        }
    }
}
