namespace Touchstone.SampleApp.Tests.Nunit
{
    using System.Collections.Generic;
    using System.Threading.Tasks;

    using NUnit.Framework;
    using Touchstone.Core;
    using Touchstone.NunitAdapter;
    using Touchstone.SampleApp.Tests.Shared;

    /// <summary>
    /// Runs each sample case as its own NUnit test, with native names, categories, and ignores.
    /// </summary>
    [TestFixture]
    public sealed class SampleAppNunitTests
    {
        /// <summary>
        /// One entry per case.
        /// </summary>
        /// <returns>Test case data.</returns>
        public static IEnumerable<TestCaseData> Cases()
        {
            return TouchstoneNunitCases.From(SampleAppSuites.All);
        }

        /// <summary>
        /// Execute one case.
        /// </summary>
        /// <param name="testCase">The case.</param>
        /// <returns>A task that completes when the case passes.</returns>
        [TestCaseSource(nameof(Cases))]
        public Task Run(TestCaseDescriptor testCase)
        {
            return TouchstoneNunit.ExecuteAsync(testCase, null, null, TestContext.CurrentContext.CancellationToken);
        }
    }
}
