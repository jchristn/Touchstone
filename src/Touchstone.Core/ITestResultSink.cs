namespace Touchstone.Core;

public interface ITestResultSink
{
    ValueTask OnSuiteStartedAsync(TestSuiteDescriptor suite, CancellationToken cancellationToken);
    ValueTask OnTestCompletedAsync(TestResult result, CancellationToken cancellationToken);
    ValueTask OnSuiteCompletedAsync(TestSuiteDescriptor suite, TestRunSummary summary, CancellationToken cancellationToken);
}
