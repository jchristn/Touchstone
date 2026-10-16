#nullable enable
namespace Touchstone.Core
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    /// <summary>Code-first scenario authoring. Builders are not thread-safe; built descriptors isolate execution state.</summary>
    public sealed class ScenarioBuilder<TContext>
    {
        private readonly string _SuiteId;
        private readonly string _CaseId;
        private readonly string _DisplayName;
        private readonly Func<CancellationToken, Task<TContext>> _CreateContextAsync;
        private readonly List<ScenarioStepDescriptor<TContext>> _Steps = new List<ScenarioStepDescriptor<TContext>>();
        private TimeSpan _CleanupTimeout = TimeSpan.FromSeconds(30);
        private Func<TContext, CancellationToken, Task>? _CleanupAsync;
        private ScenarioMetadata _Metadata = new ScenarioMetadata();

        /// <summary>Create a builder. The factory transfers ownership of each context to the scenario.</summary>
        /// <exception cref="ArgumentException">An identity or name is blank.</exception>
        /// <exception cref="ArgumentNullException">The context factory is null.</exception>
        public ScenarioBuilder(string suiteId, string caseId, string displayName, Func<CancellationToken, Task<TContext>> createContextAsync)
        {
            if (string.IsNullOrWhiteSpace(suiteId)) throw new ArgumentException("Suite identity cannot be blank.", nameof(suiteId));
            if (string.IsNullOrWhiteSpace(caseId)) throw new ArgumentException("Case identity cannot be blank.", nameof(caseId));
            if (string.IsNullOrWhiteSpace(displayName)) throw new ArgumentException("Scenario name cannot be blank.", nameof(displayName));
            _SuiteId = suiteId;
            _CaseId = caseId;
            _DisplayName = displayName;
            _CreateContextAsync = createContextAsync ?? throw new ArgumentNullException(nameof(createContextAsync));
        }
        /// <summary>Cleanup cancellation budget. Default 30 seconds, must be positive and at most one day.
        /// Cancellation is cooperative and cannot terminate a delegate that ignores it.</summary>
        /// <exception cref="ArgumentOutOfRangeException">The budget is outside its supported range.</exception>
        public TimeSpan CleanupTimeout
        {
            get { return _CleanupTimeout; }
            set
            {
                if (value <= TimeSpan.Zero || value > TimeSpan.FromDays(1)) throw new ArgumentOutOfRangeException(nameof(value));
                _CleanupTimeout = value;
            }
        }
        /// <summary>Add a typed reusable step.</summary>
        /// <exception cref="ArgumentNullException">Step is null.</exception>
        /// <exception cref="ArgumentException">Step identity is duplicated.</exception>
        public ScenarioBuilder<TContext> Step(ScenarioStepDescriptor<TContext> step)
        {
            ArgumentNullException.ThrowIfNull(step);
            if (_Steps.Exists(s => s.StepId == step.StepId)) throw new ArgumentException("Duplicate step identity: " + step.StepId, nameof(step));
            _Steps.Add(step);
            return this;
        }
        /// <summary>Add a precondition.</summary>
        public ScenarioBuilder<TContext> Given(string id, string name, Func<TContext, CancellationToken, Task> executeAsync)
        {
            return Step(new ScenarioStepDescriptor<TContext>(id, name, ScenarioStepKind.Given, executeAsync));
        }
        /// <summary>Add an action.</summary>
        public ScenarioBuilder<TContext> When(string id, string name, Func<TContext, CancellationToken, Task> executeAsync)
        {
            return Step(new ScenarioStepDescriptor<TContext>(id, name, ScenarioStepKind.When, executeAsync));
        }
        /// <summary>Add an observable expectation.</summary>
        public ScenarioBuilder<TContext> Then(string id, string name, Func<TContext, CancellationToken, Task> executeAsync)
        {
            return Step(new ScenarioStepDescriptor<TContext>(id, name, ScenarioStepKind.Then, executeAsync));
        }
        /// <summary>Add a step inheriting the previous step's intent.</summary>
        /// <exception cref="InvalidOperationException">No preceding step exists.</exception>
        public ScenarioBuilder<TContext> And(string id, string name, Func<TContext, CancellationToken, Task> executeAsync)
        {
            if (_Steps.Count == 0) throw new InvalidOperationException("And requires a preceding step.");
            return Step(new ScenarioStepDescriptor<TContext>(id, name, _Steps[_Steps.Count - 1].Kind, executeAsync));
        }
        /// <summary>Set feature and requirement metadata.</summary>
        /// <exception cref="ArgumentNullException">Metadata is null.</exception>
        public ScenarioBuilder<TContext> WithMetadata(ScenarioMetadata metadata)
        {
            _Metadata = metadata ?? throw new ArgumentNullException(nameof(metadata));
            return this;
        }
        /// <summary>Set optional cleanup, called before context disposal even after failure or cancellation.</summary>
        /// <exception cref="ArgumentNullException">Cleanup is null.</exception>
        public ScenarioBuilder<TContext> WithCleanup(Func<TContext, CancellationToken, Task> cleanupAsync)
        {
            _CleanupAsync = cleanupAsync ?? throw new ArgumentNullException(nameof(cleanupAsync));
            return this;
        }
        /// <summary>Build an independent definition snapshot. Each execution owns a fresh context.</summary>
        /// <exception cref="InvalidOperationException">There are no steps.</exception>
        public TestCaseDescriptor Build()
        {
            if (_Steps.Count == 0) throw new InvalidOperationException("A scenario must contain at least one step.");
            ScenarioDefinition<TContext> definition = new ScenarioDefinition<TContext>(_CreateContextAsync,
                _Steps.ToArray(), _Metadata, _CleanupAsync, _CleanupTimeout);
            TestCaseDescriptor descriptor = new TestCaseDescriptor(_SuiteId, _CaseId, _DisplayName, _ => Task.CompletedTask);
            descriptor.AttachScenario(definition);
            return descriptor;
        }
    }
}
