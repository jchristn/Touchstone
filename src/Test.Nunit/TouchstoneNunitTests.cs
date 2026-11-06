namespace Test.Nunit
{
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;

    using NUnit.Framework;
    using Test.Shared;
    using Test.Shared.Nunit;
    using Test.Shared.Reflection;
    using Touchstone.Core;
    using Touchstone.NunitAdapter;

    /// <summary>
    /// Runs each case as its own NUnit test, with native names, categories, and ignores.
    /// </summary>
    public sealed class TouchstoneNunitTests
    {
        /// <summary>
        /// Every suite this host runs.
        /// </summary>
        /// <returns>The suites.</returns>
        public static IReadOnlyList<TestSuiteDescriptor> HostSuites()
        {
            return TouchstoneSuites.All.Concat(ReflectionSuites.All).Concat(NunitSuites.All).ToList();
        }

        /// <summary>
        /// One entry per case.
        /// </summary>
        /// <returns>Test case data.</returns>
        public static IEnumerable<TestCaseData> Cases()
        {
            return TouchstoneNunitCases.From(HostSuites());
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
