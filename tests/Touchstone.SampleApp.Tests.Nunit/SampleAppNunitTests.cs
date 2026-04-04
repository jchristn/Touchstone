namespace Touchstone.SampleApp.Tests.Nunit
{
    using System.Collections;
    using System.Threading;
    using System.Threading.Tasks;

    using NUnit.Framework;

    using Touchstone.Core;
    using Touchstone.NunitAdapter;
    using Touchstone.SampleApp.Tests.Shared;

    /// <summary>
    /// NUnit test host using TestCaseSource for data-driven execution.
    /// </summary>
    [TestFixture]
    public sealed class SampleAppNunitTests
    {
        private static IEnumerable TestCases()
        {
            return new TouchstoneTestCaseSource(SampleAppSuites.All);
        }

        [Test]
        [TestCaseSource(nameof(TestCases))]
        public async Task RunTest(TestCaseDescriptor testCase)
        {
            await testCase.ExecuteAsync(CancellationToken.None);
        }
    }
}
