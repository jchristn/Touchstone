namespace Touchstone.Core;

public sealed class TestCaseDescriptor
{
    public TestCaseDescriptor(
        string suiteId,
        string caseId,
        string displayName,
        Func<CancellationToken, Task> executeAsync,
        IReadOnlyList<string>? tags = null,
        bool skip = false,
        string? skipReason = null)
    {
        SuiteId = suiteId;
        CaseId = caseId;
        DisplayName = displayName;
        ExecuteAsync = executeAsync;
        Tags = tags ?? [];
        Skip = skip;
        SkipReason = skipReason;
    }

    public string SuiteId { get; }
    public string CaseId { get; }
    public string DisplayName { get; }
    public IReadOnlyList<string> Tags { get; }
    public Func<CancellationToken, Task> ExecuteAsync { get; }
    public bool Skip { get; }
    public string? SkipReason { get; }

    public string TestId => $"{SuiteId}.{CaseId}";

    public override string ToString() => DisplayName;
}
