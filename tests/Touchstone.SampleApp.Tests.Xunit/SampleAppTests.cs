using Touchstone.Core;
using Touchstone.SampleApp.Tests.Shared;
using Xunit;
using Xunit.Abstractions;

namespace Touchstone.SampleApp.Tests.Xunit;

public sealed class SampleAppTests
{
    private readonly ITestOutputHelper _output;

    public SampleAppTests(ITestOutputHelper output) => _output = output;

    public static TheoryData<TestCaseDescriptor> TestCases()
    {
        var data = new TheoryData<TestCaseDescriptor>();
        foreach (var suite in SampleAppSuites.All)
            foreach (var testCase in suite.Cases)
                if (!testCase.Skip)
                    data.Add(testCase);
        return data;
    }

    public static TheoryData<TestCaseDescriptor> SkippedCases()
    {
        var data = new TheoryData<TestCaseDescriptor>();
        foreach (var suite in SampleAppSuites.All)
            foreach (var testCase in suite.Cases)
                if (testCase.Skip)
                    data.Add(testCase);
        return data;
    }

    [Theory]
    [MemberData(nameof(TestCases))]
    public async Task RunTest(TestCaseDescriptor testCase)
    {
        await testCase.ExecuteAsync(CancellationToken.None);
    }

    [Theory(Skip = "Dynamically skipped test cases")]
    [MemberData(nameof(SkippedCases))]
    public Task Skipped(TestCaseDescriptor testCase)
    {
        _output.WriteLine($"Skipped: {testCase.SkipReason}");
        return Task.CompletedTask;
    }
}
