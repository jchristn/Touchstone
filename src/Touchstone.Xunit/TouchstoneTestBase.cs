using Touchstone.Core;
using Xunit;

namespace Touchstone.Xunit;

public abstract class TouchstoneTestBase
{
    protected abstract IReadOnlyList<TestSuiteDescriptor> Suites { get; }

    public TheoryData<TestCaseDescriptor> TestCases => new TouchstoneTheoryData(Suites);
}
