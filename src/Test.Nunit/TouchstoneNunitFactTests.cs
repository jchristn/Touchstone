namespace Test.Nunit
{
    using System.Collections.Generic;
    using System.Threading.Tasks;

    using NUnit.Framework;
    using Touchstone.Core;
    using Touchstone.NunitAdapter;

    /// <summary>
    /// Runs every case inside one NUnit test.
    /// </summary>
    public sealed class TouchstoneNunitFactTests : TouchstoneNunitFactBase
    {
        /// <inheritdoc />
        protected override IReadOnlyList<TestSuiteDescriptor> Suites
        {
            get { return TouchstoneNunitTests.HostSuites(); }
        }

        /// <summary>
        /// Run every suite.
        /// </summary>
        /// <returns>A task that completes when every case passed.</returns>
        [Test]
        public Task RunAll()
        {
            return RunAllAsync(TestContext.CurrentContext.CancellationToken);
        }
    }
}
