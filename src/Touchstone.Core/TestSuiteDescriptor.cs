namespace Touchstone.Core;

public sealed class TestSuiteDescriptor
{
    public TestSuiteDescriptor(
        string suiteId,
        string displayName,
        IReadOnlyList<TestCaseDescriptor> cases,
        Func<CancellationToken, ValueTask>? beforeSuiteAsync = null,
        Func<CancellationToken, ValueTask>? afterSuiteAsync = null)
    {
        SuiteId = suiteId;
        DisplayName = displayName;
        Cases = cases;
        BeforeSuiteAsync = beforeSuiteAsync;
        AfterSuiteAsync = afterSuiteAsync;
    }

    public string SuiteId { get; }
    public string DisplayName { get; }
    public IReadOnlyList<TestCaseDescriptor> Cases { get; }
    public Func<CancellationToken, ValueTask>? BeforeSuiteAsync { get; }
    public Func<CancellationToken, ValueTask>? AfterSuiteAsync { get; }

    public override string ToString() => DisplayName;
}
