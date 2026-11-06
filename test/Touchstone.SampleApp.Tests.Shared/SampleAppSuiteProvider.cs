namespace Touchstone.SampleApp.Tests.Shared
{
    using System.Collections.Generic;

    using Touchstone.Core;

    /// <summary>
    /// Supplies the sample suites to hosts that rebuild cases by type, such as the xUnit adapters.
    /// </summary>
    public sealed class SampleAppSuiteProvider : ITestSuiteProvider
    {
        /// <inheritdoc />
        public IReadOnlyList<TestSuiteDescriptor> Suites
        {
            get { return SampleAppSuites.All; }
        }
    }
}
