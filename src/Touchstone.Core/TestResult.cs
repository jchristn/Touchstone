namespace Touchstone.Core;

public sealed class TestResult
{
    public required string TestId { get; init; }
    public required string SuiteId { get; init; }
    public required string CaseId { get; init; }
    public required string DisplayName { get; init; }
    public required bool Success { get; init; }
    public bool Skipped { get; init; }
    public TimeSpan Duration { get; init; }
    public Exception? Exception { get; init; }
    public string? Message { get; init; }
}
