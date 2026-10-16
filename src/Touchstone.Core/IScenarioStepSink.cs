#nullable enable
namespace Touchstone.Core
{
    using System.Threading;
    using System.Threading.Tasks;
    /// <summary>Optional live step observer. Callback failures propagate as observer errors.
    /// Implementations are responsible for synchronization when shared across runs.</summary>
    public interface IScenarioStepSink
    {
        /// <summary>Observe a step before its body runs. Cancellation has already been checked.</summary>
        ValueTask OnStepStartedAsync(string testId, ScenarioStepResult step, CancellationToken cancellationToken);
        /// <summary>Observe a completed step. Completion and cleanup observation use a non-canceled token.</summary>
        ValueTask OnStepCompletedAsync(string testId, ScenarioStepResult step, CancellationToken cancellationToken);
    }
}
