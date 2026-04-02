using Touchstone.Core;
using Xunit;

namespace Touchstone.Xunit;

public sealed class TouchstoneTheoryData : TheoryData<TestCaseDescriptor>
{
    public TouchstoneTheoryData(TestSuiteDescriptor suite)
    {
        foreach (var testCase in suite.Cases)
            Add(testCase);
    }

    public TouchstoneTheoryData(IEnumerable<TestSuiteDescriptor> suites)
    {
        foreach (var suite in suites)
            foreach (var testCase in suite.Cases)
                Add(testCase);
    }
}
