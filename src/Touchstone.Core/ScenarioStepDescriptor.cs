#nullable enable
namespace Touchstone.Core
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    /// <summary>Immutable typed step definition. Delegates must synchronize any externally shared state.</summary>
    public sealed class ScenarioStepDescriptor<TContext>
    {
        /// <summary>Stable identity within the scenario.</summary>
        public string StepId { get; }
        /// <summary>Human-readable behavior.</summary>
        public string DisplayName { get; }
        /// <summary>Behavior intent.</summary>
        public ScenarioStepKind Kind { get; }
        /// <summary>Step body receiving per-execution context and caller cancellation.</summary>
        public Func<TContext, CancellationToken, Task> ExecuteAsync { get; }
        /// <summary>Create a step.</summary>
        /// <exception cref="ArgumentException">Identity, name, or kind is invalid.</exception>
        /// <exception cref="ArgumentNullException">The delegate is null.</exception>
        public ScenarioStepDescriptor(string stepId, string displayName, ScenarioStepKind kind, Func<TContext, CancellationToken, Task> executeAsync)
        {
            if (string.IsNullOrWhiteSpace(stepId)) throw new ArgumentException("Step identity cannot be blank.", nameof(stepId));
            if (string.IsNullOrWhiteSpace(displayName)) throw new ArgumentException("Step name cannot be blank.", nameof(displayName));
            if (!Enum.IsDefined(kind)) throw new ArgumentException("Unknown step kind.", nameof(kind));
            StepId = stepId;
            DisplayName = displayName;
            Kind = kind;
            ExecuteAsync = executeAsync ?? throw new ArgumentNullException(nameof(executeAsync));
        }
    }
}
