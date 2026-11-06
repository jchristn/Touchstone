namespace Touchstone.SampleApp.Tests.Nunit
{
    using System.Collections.Generic;
    using System.Threading.Tasks;

    using NUnit.Framework;
    using Touchstone.Core;
    using Touchstone.NunitAdapter;
    using Touchstone.SampleApp.Tests.Shared;

    /// <summary>
    /// Runs every sample case, with suite hooks, inside one NUnit test.
    /// </summary>
    [TestFixture]
    public sealed class SampleAppNunitFactTests : TouchstoneNunitFactBase
    {
        /// <inheritdoc />
        protected override IReadOnlyList<TestSuiteDescriptor> Suites
        {
            get { return SampleAppSuites.All; }
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
