#nullable enable
namespace Touchstone.Core
{
    using System.Threading;
    using System.Threading.Tasks;
    internal interface IScenarioDefinition
    {
        ScenarioMetadata Metadata { get; }
        Task<TestResult> ExecuteAsync(TestCaseDescriptor descriptor, IScenarioStepSink? sink, CancellationToken cancellationToken);
    }
}
