namespace Touchstone.Core;

public sealed class TestRunSummary
{
    public int Total { get; init; }
    public int Passed { get; init; }
    public int Failed { get; init; }
    public int Skipped { get; init; }
    public TimeSpan Duration { get; init; }

    public bool IsSuccess => Failed == 0;
}
