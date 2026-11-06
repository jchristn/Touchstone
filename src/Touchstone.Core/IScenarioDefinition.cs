namespace Touchstone.Core
{
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// A built scenario that a descriptor executes in place of a delegate.
    /// </summary>
    internal interface IScenarioDefinition
    {
        ScenarioMetadata Metadata { get; }

        // cancellationToken is the caller's token. When deadline is not null, context creation and steps observe
        // deadline.Token instead, and the scenario stops the deadline before cleanup. An OperationCanceledException
        // counts as cancellation only when the caller's token is canceled.
        Task<TestResult> ExecuteAsync(TestCaseDescriptor descriptor, ITestResultSink? sink, CancellationToken cancellationToken, CaseDeadline? deadline);
    }
}
