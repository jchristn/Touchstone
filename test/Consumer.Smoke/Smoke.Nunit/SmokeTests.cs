namespace Smoke.Nunit
{
    using System.Collections.Generic;
    using System.Threading.Tasks;

    using NUnit.Framework;
    using Smoke.Shared;
    using Touchstone.Core;
    using Touchstone.NunitAdapter;

    public sealed class SmokeTests
    {
        public static IEnumerable<TestCaseData> Cases()
        {
            return TouchstoneNunitCases.From(new SmokeSuites().Suites);
        }

        [TestCaseSource(nameof(Cases))]
        public Task Run(TestCaseDescriptor testCase)
        {
            return TouchstoneNunit.ExecuteAsync(testCase);
        }
    }
}
