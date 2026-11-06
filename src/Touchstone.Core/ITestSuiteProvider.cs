namespace Touchstone.Core
{
    using System.Collections.Generic;

    /// <summary>
    /// Supplies suites to hosts that must recreate them by type, such as the xUnit adapters, which rebuild cases in the
    /// process that executes them. Implementations need a public parameterless constructor.
    /// Thread safety: <see cref="Suites"/> may be read from several threads and should return equivalent suites each time.
    /// </summary>
    public interface ITestSuiteProvider
    {
        /// <summary>
        /// Every suite this provider supplies. Never null. Suite identities must be unique.
        /// </summary>
        IReadOnlyList<TestSuiteDescriptor> Suites { get; }
    }
}
