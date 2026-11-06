namespace Touchstone.Core
{
    using System;

    /// <summary>
    /// Immutable snapshot of one scenario step's execution.
    /// Thread safety: safe to read concurrently.
    /// </summary>
    public sealed class ScenarioStepResult
    {
        private readonly string _StepId;
        private readonly string _DisplayName;
        private readonly ScenarioStepKind _Kind;
        private readonly ScenarioStepOutcome _Outcome;
        private readonly TimeSpan _Duration;
        private readonly Exception? _Exception;
        private readonly string? _Message;

        /// <summary>
        /// Initialize a new step result. The engine creates step results; this constructor exists so custom sinks and
        /// reporters can be tested.
        /// </summary>
        /// <param name="stepId">Stable step identity. Cannot be null.</param>
        /// <param name="displayName">Human-readable step name. Cannot be null.</param>
        /// <param name="kind">Behavior intent.</param>
        /// <param name="outcome">Execution outcome.</param>
        /// <param name="duration">Elapsed time. Cannot be negative.</param>
        /// <param name="exception">Original failure. Null when the step did not fail.</param>
        /// <param name="message">Outcome reason. Null means the exception message, or no message.</param>
        /// <exception cref="ArgumentNullException">The identity or name is null.</exception>
        /// <exception cref="ArgumentOutOfRangeException">The kind or outcome is undefined, or the duration is negative.</exception>
        public ScenarioStepResult(
            string stepId,
            string displayName,
            ScenarioStepKind kind,
            ScenarioStepOutcome outcome,
            TimeSpan duration,
            Exception? exception = null,
            string? message = null)
        {
            _StepId = stepId ?? throw new ArgumentNullException(nameof(stepId));
            _DisplayName = displayName ?? throw new ArgumentNullException(nameof(displayName));
            if (!Enum.IsDefined(kind)) throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unknown step kind.");
            if (!Enum.IsDefined(outcome)) throw new ArgumentOutOfRangeException(nameof(outcome), outcome, "Unknown step outcome.");
            if (duration < TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(duration), duration, "Duration cannot be negative.");
            _Kind = kind;
            _Outcome = outcome;
            _Duration = duration;
            _Exception = exception;
            _Message = message ?? exception?.Message;
        }

        /// <summary>
        /// Stable step identity. Never null.
        /// </summary>
        public string StepId
        {
            get { return _StepId; }
        }

        /// <summary>
        /// Human-readable step name. Never null. May contain consumer text.
        /// </summary>
        public string DisplayName
        {
            get { return _DisplayName; }
        }

        /// <summary>
        /// Behavior intent.
        /// </summary>
        public ScenarioStepKind Kind
        {
            get { return _Kind; }
        }

        /// <summary>
        /// Execution outcome.
        /// </summary>
        public ScenarioStepOutcome Outcome
        {
            get { return _Outcome; }
        }

        /// <summary>
        /// Elapsed execution time. Zero for steps that did not run.
        /// </summary>
        public TimeSpan Duration
        {
            get { return _Duration; }
        }

        /// <summary>
        /// Original failure. Null when the step did not fail. Reports serialize only its message.
        /// </summary>
        public Exception? Exception
        {
            get { return _Exception; }
        }

        /// <summary>
        /// Outcome reason. Null when there is nothing to report. May contain consumer text.
        /// </summary>
        public string? Message
        {
            get { return _Message; }
        }
    }
}
