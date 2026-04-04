namespace Touchstone.SampleApp.Tests.Nunit
{
    using System.Collections.Generic;
    using System.Threading.Tasks;

    using NUnit.Framework;

    using Touchstone.Core;
    using Touchstone.NunitAdapter;
    using Touchstone.SampleApp.Tests.Shared;

    /// <summary>
    /// NUnit fact-style test host. All descriptors run in a single [Test].
    /// </summary>
    [TestFixture]
    public sealed class SampleAppNunitFactTests : TouchstoneNunitBase
    {
        /// <inheritdoc />
        protected override IReadOnlyList<TestSuiteDescriptor> Suites
        {
            get { return SampleAppSuites.All; }
        }

        [Test]
        public async Task RunAll()
        {
            await RunAllAsync();
        }
    }
}
