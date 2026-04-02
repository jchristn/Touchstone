using Touchstone.Core;

namespace Touchstone.Cli;

public sealed class ConsoleResultSink : ITestResultSink
{
    private readonly TextWriter _writer;
    private readonly bool _useColor;
    private readonly int _descriptionWidth;
    private readonly List<TestResult> _allResults = [];

    public ConsoleResultSink(TextWriter? writer = null, bool? useColor = null)
    {
        _writer = writer ?? System.Console.Out;
        _useColor = useColor ?? !System.Console.IsOutputRedirected;
        _descriptionWidth = GetMaximumDescriptionWidth();
    }

    public IReadOnlyList<TestResult> AllResults => _allResults;

    public void WriteHeader()
    {
        _writer.WriteLine(
            "Test".PadRight(_descriptionWidth)
            + "  "
            + "Result".PadRight(6)
            + "  "
            + "Runtime".PadLeft(8));
        _writer.WriteLine(
            new string('-', _descriptionWidth)
            + "  "
            + new string('-', 6)
            + "  "
            + new string('-', 8));
    }

    public ValueTask OnSuiteStartedAsync(TestSuiteDescriptor suite, CancellationToken cancellationToken)
    {
        return ValueTask.CompletedTask;
    }

    public ValueTask OnTestCompletedAsync(TestResult result, CancellationToken cancellationToken)
    {
        _allResults.Add(result);
        WriteResultLine(result);
        return ValueTask.CompletedTask;
    }

    public ValueTask OnSuiteCompletedAsync(TestSuiteDescriptor suite, TestRunSummary summary, CancellationToken cancellationToken)
    {
        return ValueTask.CompletedTask;
    }

    public void WriteOverall(TimeSpan totalRuntime, int totalCount, int passedCount, int failedCount, int skippedCount)
    {
        _writer.WriteLine();

        bool passed = failedCount == 0;
        string runtime = FormatRuntime(totalRuntime);

        _writer.Write("OVERALL".PadRight(_descriptionWidth));
        _writer.Write("  ");
        WriteStatus(passed);
        _writer.Write("  ");
        _writer.WriteLine(runtime.PadLeft(8));

        _writer.WriteLine(
            "Total: " + totalCount
            + "  Passed: " + passedCount
            + "  Failed: " + failedCount
            + "  Skipped: " + skippedCount);

        if (failedCount > 0)
        {
            _writer.WriteLine("Failed Tests:");
            foreach (var result in _allResults)
            {
                if (!result.Success && !result.Skipped)
                {
                    _writer.WriteLine("  " + result.DisplayName);
                    if (result.Message != null)
                        _writer.WriteLine("    " + result.Message);
                }
            }
        }
    }

    private void WriteResultLine(TestResult result)
    {
        string description = FormatDescription(result.DisplayName, _descriptionWidth);
        _writer.Write(description);
        _writer.Write("  ");

        if (result.Skipped)
            WriteStatus("SKIP", ConsoleColor.Yellow);
        else
            WriteStatus(result.Success);

        _writer.Write("  ");
        _writer.WriteLine(FormatRuntime(result.Duration).PadLeft(8));
    }

    private void WriteStatus(bool passed)
    {
        WriteStatus(passed ? "PASS" : "FAIL", passed ? ConsoleColor.Green : ConsoleColor.Red);
    }

    private void WriteStatus(string label, ConsoleColor color)
    {
        if (!_useColor)
        {
            _writer.Write(label.PadRight(6));
            return;
        }

        var previous = System.Console.ForegroundColor;
        System.Console.ForegroundColor = color;
        _writer.Write(label.PadRight(6));
        System.Console.ForegroundColor = previous;
    }

    private static string FormatRuntime(TimeSpan runtime)
    {
        long ms = (long)Math.Round(runtime.TotalMilliseconds, MidpointRounding.AwayFromZero);
        return ms + "ms";
    }

    private static string FormatDescription(string description, int descriptionWidth)
    {
        if (string.IsNullOrEmpty(description))
            return string.Empty.PadRight(descriptionWidth);

        if (description.Length <= descriptionWidth)
            return description.PadRight(descriptionWidth);

        if (descriptionWidth <= 3)
            return description[..descriptionWidth];

        return description[..(descriptionWidth - 3)] + "...";
    }

    private static int GetMaximumDescriptionWidth()
    {
        const int resultColumnWidth = 6;
        const int runtimeColumnWidth = 8;
        const int separatorWidth = 4;
        const int minimumDescriptionWidth = 40;
        const int defaultDescriptionWidth = 100;

        try
        {
            if (System.Console.IsOutputRedirected)
                return defaultDescriptionWidth;

            int available = System.Console.WindowWidth - resultColumnWidth - runtimeColumnWidth - separatorWidth;
            return available < minimumDescriptionWidth ? minimumDescriptionWidth : available;
        }
        catch (IOException) { return defaultDescriptionWidth; }
        catch (ArgumentOutOfRangeException) { return defaultDescriptionWidth; }
        catch (PlatformNotSupportedException) { return defaultDescriptionWidth; }
    }
}
