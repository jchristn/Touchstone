namespace Parity.Nunit
{
    using System.Collections.Generic;
    using System.Threading.Tasks;

    using NUnit.Framework;
    using Parity.Shared;
    using Touchstone.Core;
    using Touchstone.NunitAdapter;

    /// <summary>
    /// Parity fixture for NUnit.
    /// </summary>
    public sealed class ParityTests
    {
        /// <summary>
        /// Entries.
        /// </summary>
        /// <returns>Test case data.</returns>
        public static IEnumerable<TestCaseData> Cases()
        {
            return TouchstoneNunitCases.From(new ParitySuites().Suites, ParitySuites.Options());
        }

        /// <summary>
        /// Run one entry.
        /// </summary>
        /// <param name="testCase">The case.</param>
        /// <returns>The run.</returns>
        [TestCaseSource(nameof(Cases))]
        public Task Run(TestCaseDescriptor testCase)
        {
            return TouchstoneNunit.ExecuteAsync(testCase, ParitySuites.Options(), null, ParitySuites.TokenFor(testCase.TestId, TestContext.CurrentContext.CancellationToken));
        }
    }
}
