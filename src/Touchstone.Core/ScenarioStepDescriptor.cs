namespace Touchstone.Core
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// An immutable, reusable scenario step. Add it to a scenario with <see cref="ScenarioBuilder{TContext}.Step"/>.
    /// Thread safety: the descriptor is immutable; a step body that touches state outside its context must synchronize.
    /// </summary>
    /// <typeparam name="TContext">Per-execution context type.</typeparam>
    public sealed class ScenarioStepDescriptor<TContext>
    {
        private readonly string _StepId;
        private readonly string _DisplayName;
        private readonly ScenarioStepKind _Kind;
        private readonly Func<TContext, CancellationToken, Task> _ExecuteAsync;

        /// <summary>
        /// Create a step.
        /// </summary>
        /// <param name="stepId">Stable identity within a scenario. Cannot be null or blank.</param>
        /// <param name="displayName">Human-readable behavior. Cannot be null or blank.</param>
        /// <param name="kind">Behavior intent.</param>
        /// <param name="executeAsync">Step body. Receives the execution's context and the caller's token. Throw to fail.</param>
        /// <exception cref="ArgumentException">The identity or name is blank, or the kind is undefined.</exception>
        /// <exception cref="ArgumentNullException">The body is null.</exception>
        public ScenarioStepDescriptor(string stepId, string displayName, ScenarioStepKind kind, Func<TContext, CancellationToken, Task> executeAsync)
        {
            if (string.IsNullOrWhiteSpace(stepId)) throw new ArgumentException("Step identity cannot be blank.", nameof(stepId));
            if (string.IsNullOrWhiteSpace(displayName)) throw new ArgumentException("Step name cannot be blank.", nameof(displayName));
            if (!Enum.IsDefined(kind)) throw new ArgumentException("Unknown step kind.", nameof(kind));
            _StepId = stepId;
            _DisplayName = displayName;
            _Kind = kind;
            _ExecuteAsync = executeAsync ?? throw new ArgumentNullException(nameof(executeAsync));
        }

        /// <summary>
        /// Stable identity within a scenario. Never null or blank.
        /// </summary>
        public string StepId
        {
            get { return _StepId; }
        }

        /// <summary>
        /// Human-readable behavior. Never null or blank. May contain consumer text.
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
        /// Step body. Receives the execution's context and the caller's token.
        /// </summary>
        public Func<TContext, CancellationToken, Task> ExecuteAsync
        {
            get { return _ExecuteAsync; }
        }
    }
}
