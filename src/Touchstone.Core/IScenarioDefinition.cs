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

        Task<TestResult> ExecuteAsync(TestCaseDescriptor descriptor, ITestResultSink? sink, CancellationToken cancellationToken);
    }
}
