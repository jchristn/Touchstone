namespace Test.Shared
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;

    using Touchstone.Core;

    /// <summary>Records live events and can fail observation for lifecycle tests.</summary>
    public sealed class ScenarioObserver : IScenarioStepSink
    {
        /// <summary>Observed event order.</summary>
        public List<string> Events { get; } = new List<string>();
        /// <summary>Throw on start when true.</summary>
        public bool FailStart { get; set; }
        /// <summary>Throw on completion when true.</summary>
        public bool FailCompletion { get; set; }
        /// <inheritdoc />
        public ValueTask OnStepStartedAsync(string testId, ScenarioStepResult step, CancellationToken cancellationToken)
        {
            Events.Add("start:" + step.StepId);
            if (FailStart) throw new InvalidOperationException("observer start failure");
            return ValueTask.CompletedTask;
        }
        /// <inheritdoc />
        public ValueTask OnStepCompletedAsync(string testId, ScenarioStepResult step, CancellationToken cancellationToken)
        {
            TestAssert.False(cancellationToken.IsCancellationRequested, "completion token allows observation");
            Events.Add("complete:" + step.StepId);
            if (FailCompletion) throw new InvalidOperationException("observer completion failure");
            return ValueTask.CompletedTask;
        }
    }
}
