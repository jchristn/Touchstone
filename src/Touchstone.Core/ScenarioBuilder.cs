namespace Touchstone.Core
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// Builds a behavior scenario from typed Given, When, Then, and And steps. <see cref="Build"/> returns an ordinary
    /// <see cref="TestCaseDescriptor"/>; each execution of it creates a fresh context, runs the steps in order, and then
    /// cleans up and disposes the context, including after failure and cancellation.
    /// Thread safety: builders are not thread-safe. Built descriptors are immutable and safe to execute concurrently.
    /// </summary>
    /// <typeparam name="TContext">Per-execution context type. A context that implements <see cref="IAsyncDisposable"/>
    /// or <see cref="IDisposable"/> is disposed after each execution.</typeparam>
    public sealed class ScenarioBuilder<TContext>
    {
        private readonly string _SuiteId;
        private readonly string _CaseId;
        private readonly string _DisplayName;
        private readonly Func<CancellationToken, Task<TContext>> _CreateContextAsync;
        private readonly List<ScenarioStepDescriptor<TContext>> _Steps = new List<ScenarioStepDescriptor<TContext>>();
        private TimeSpan _CleanupTimeout = TimeSpan.FromSeconds(30);
        private Func<TContext, CancellationToken, Task>? _CleanupAsync = null;
        private ScenarioMetadata _Metadata = new ScenarioMetadata();
        private IReadOnlyList<string> _Tags = Array.Empty<string>();
        private string? _SkipReason = null;

        /// <summary>
        /// Create a builder.
        /// </summary>
        /// <param name="suiteId">Identifier of the parent suite. Same rules as <see cref="TestCaseDescriptor.SuiteId"/>.</param>
        /// <param name="caseId">Identifier of the scenario within the suite. Same rules as <see cref="TestCaseDescriptor.CaseId"/>.</param>
        /// <param name="displayName">Human-readable scenario name. Cannot be blank.</param>
        /// <param name="createContextAsync">Factory called once per execution. Ownership of each context transfers to the
        /// scenario. Returning null fails the execution.</param>
        /// <exception cref="ArgumentNullException">An argument is null.</exception>
        /// <exception cref="ArgumentException">An identity or the display name is invalid.</exception>
        public ScenarioBuilder(string suiteId, string caseId, string displayName, Func<CancellationToken, Task<TContext>> createContextAsync)
        {
            _SuiteId = IdentifierRules.SuiteId(suiteId, nameof(suiteId));
            _CaseId = IdentifierRules.CaseId(caseId, nameof(caseId));
            _DisplayName = IdentifierRules.DisplayName(displayName, nameof(displayName));
            _CreateContextAsync = createContextAsync ?? throw new ArgumentNullException(nameof(createContextAsync));
        }

        /// <summary>
        /// Time cleanup and disposal may take before the cleanup token is canceled. Default 30 seconds. Must be greater
        /// than zero and at most one day. Cancellation is cooperative and cannot stop a delegate that ignores it.
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException">The value is zero, negative, or more than one day.</exception>
        public TimeSpan CleanupTimeout
        {
            get { return _CleanupTimeout; }
            set
            {
                if (value <= TimeSpan.Zero || value > TimeSpan.FromDays(1))
                    throw new ArgumentOutOfRangeException(nameof(value), value, "Cleanup timeout must be greater than zero and at most one day.");
                _CleanupTimeout = value;
            }
        }

        /// <summary>
        /// Add a reusable step.
        /// </summary>
        /// <param name="step">Step to add. Its identity must be unique within the scenario.</param>
        /// <returns>This builder.</returns>
        /// <exception cref="ArgumentNullException">The step is null.</exception>
        /// <exception cref="ArgumentException">The step identity is already used.</exception>
        public ScenarioBuilder<TContext> Step(ScenarioStepDescriptor<TContext> step)
        {
            if (step == null) throw new ArgumentNullException(nameof(step));
            if (_Steps.Exists(s => s.StepId == step.StepId))
                throw new ArgumentException("Duplicate step identity: " + step.StepId, nameof(step));

            _Steps.Add(step);
            return this;
        }

        /// <summary>
        /// Add a step that establishes a precondition.
        /// </summary>
        /// <param name="id">Step identity, unique within the scenario.</param>
        /// <param name="name">Human-readable behavior.</param>
        /// <param name="executeAsync">Step body. Receives the execution's context and the caller's token. Throw to fail.</param>
        /// <returns>This builder.</returns>
        /// <exception cref="ArgumentNullException">The body is null.</exception>
        /// <exception cref="ArgumentException">The identity or name is blank, or the identity is already used.</exception>
        public ScenarioBuilder<TContext> Given(string id, string name, Func<TContext, CancellationToken, Task> executeAsync)
        {
            return Step(new ScenarioStepDescriptor<TContext>(id, name, ScenarioStepKind.Given, executeAsync));
        }

        /// <summary>
        /// Add a step that performs the action under test.
        /// </summary>
        /// <param name="id">Step identity, unique within the scenario.</param>
        /// <param name="name">Human-readable behavior.</param>
        /// <param name="executeAsync">Step body. Receives the execution's context and the caller's token. Throw to fail.</param>
        /// <returns>This builder.</returns>
        /// <exception cref="ArgumentNullException">The body is null.</exception>
        /// <exception cref="ArgumentException">The identity or name is blank, or the identity is already used.</exception>
        public ScenarioBuilder<TContext> When(string id, string name, Func<TContext, CancellationToken, Task> executeAsync)
        {
            return Step(new ScenarioStepDescriptor<TContext>(id, name, ScenarioStepKind.When, executeAsync));
        }

        /// <summary>
        /// Add a step that verifies observable behavior.
        /// </summary>
        /// <param name="id">Step identity, unique within the scenario.</param>
        /// <param name="name">Human-readable behavior.</param>
        /// <param name="executeAsync">Step body. Receives the execution's context and the caller's token. Throw to fail.</param>
        /// <returns>This builder.</returns>
        /// <exception cref="ArgumentNullException">The body is null.</exception>
        /// <exception cref="ArgumentException">The identity or name is blank, or the identity is already used.</exception>
        public ScenarioBuilder<TContext> Then(string id, string name, Func<TContext, CancellationToken, Task> executeAsync)
        {
            return Step(new ScenarioStepDescriptor<TContext>(id, name, ScenarioStepKind.Then, executeAsync));
        }

        /// <summary>
        /// Add a step with the same kind as the step before it.
        /// </summary>
        /// <param name="id">Step identity, unique within the scenario.</param>
        /// <param name="name">Human-readable behavior.</param>
        /// <param name="executeAsync">Step body. Receives the execution's context and the caller's token. Throw to fail.</param>
        /// <returns>This builder.</returns>
        /// <exception cref="InvalidOperationException">No step has been added yet.</exception>
        /// <exception cref="ArgumentNullException">The body is null.</exception>
        /// <exception cref="ArgumentException">The identity or name is blank, or the identity is already used.</exception>
        public ScenarioBuilder<TContext> And(string id, string name, Func<TContext, CancellationToken, Task> executeAsync)
        {
            if (_Steps.Count == 0) throw new InvalidOperationException("And requires a preceding step.");
            return Step(new ScenarioStepDescriptor<TContext>(id, name, _Steps[_Steps.Count - 1].Kind, executeAsync));
        }

        /// <summary>
        /// Set feature and requirement metadata. Replaces any earlier metadata.
        /// </summary>
        /// <param name="metadata">Metadata to attach.</param>
        /// <returns>This builder.</returns>
        /// <exception cref="ArgumentNullException">The metadata is null.</exception>
        public ScenarioBuilder<TContext> WithMetadata(ScenarioMetadata metadata)
        {
            _Metadata = metadata ?? throw new ArgumentNullException(nameof(metadata));
            return this;
        }

        /// <summary>
        /// Set tags for filtering. Replaces any earlier tags.
        /// </summary>
        /// <param name="tags">Tags. Same rules as <see cref="TestCaseDescriptor.Tags"/>. Null means no tags.</param>
        /// <returns>This builder.</returns>
        /// <exception cref="ArgumentException">A tag is invalid.</exception>
        public ScenarioBuilder<TContext> WithTags(IReadOnlyList<string>? tags)
        {
            _Tags = IdentifierRules.Tags(tags, nameof(tags));
            return this;
        }

        /// <summary>
        /// Mark the scenario as skipped. A skipped scenario neither creates nor disposes a context.
        /// </summary>
        /// <param name="reason">Reason the scenario is skipped. Cannot be null or blank.</param>
        /// <returns>This builder.</returns>
        /// <exception cref="ArgumentNullException">The reason is null.</exception>
        /// <exception cref="ArgumentException">The reason is blank.</exception>
        public ScenarioBuilder<TContext> WithSkip(string reason)
        {
            if (reason == null) throw new ArgumentNullException(nameof(reason));
            _SkipReason = IdentifierRules.SkipReason(reason, nameof(reason));
            return this;
        }

        /// <summary>
        /// Set cleanup that runs before the context is disposed, including after failure and cancellation.
        /// </summary>
        /// <param name="cleanupAsync">Cleanup body. Receives the context and a token canceled after <see cref="CleanupTimeout"/>.</param>
        /// <returns>This builder.</returns>
        /// <exception cref="ArgumentNullException">The cleanup body is null.</exception>
        public ScenarioBuilder<TContext> WithCleanup(Func<TContext, CancellationToken, Task> cleanupAsync)
        {
            _CleanupAsync = cleanupAsync ?? throw new ArgumentNullException(nameof(cleanupAsync));
            return this;
        }

        /// <summary>
        /// Build an immutable descriptor from the current steps and settings. Later builder changes do not affect it.
        /// </summary>
        /// <returns>A descriptor that runs this scenario.</returns>
        /// <exception cref="InvalidOperationException">No step has been added.</exception>
        public TestCaseDescriptor Build()
        {
            if (_Steps.Count == 0) throw new InvalidOperationException("A scenario must contain at least one step.");

            ScenarioDefinition<TContext> definition = new ScenarioDefinition<TContext>(
                _CreateContextAsync, _Steps.ToArray(), _Metadata, _CleanupAsync, _CleanupTimeout);
            return new TestCaseDescriptor(_SuiteId, _CaseId, _DisplayName, definition, _Tags, _SkipReason);
        }
    }
}
