namespace Test.Shared.Mstest
{
    using System.Collections.Generic;

    using Test.Shared;
    using Touchstone.Core;

    /// <summary>
    /// Suite provider for <see cref="AdapterFixtureSet.Standard"/>.
    /// </summary>
    public sealed class StandardProvider : ITestSuiteProvider
    {
        /// <inheritdoc />
        public IReadOnlyList<TestSuiteDescriptor> Suites
        {
            get { return AdapterFixtures.Suites(AdapterFixtureSet.Standard); }
        }
    }

    /// <summary>
    /// Suite provider for <see cref="AdapterFixtureSet.Empty"/>.
    /// </summary>
    public sealed class EmptyProvider : ITestSuiteProvider
    {
        /// <inheritdoc />
        public IReadOnlyList<TestSuiteDescriptor> Suites
        {
            get { return AdapterFixtures.Suites(AdapterFixtureSet.Empty); }
        }
    }

    /// <summary>
    /// Suite provider for <see cref="AdapterFixtureSet.EmptySuites"/>.
    /// </summary>
    public sealed class EmptySuitesProvider : ITestSuiteProvider
    {
        /// <inheritdoc />
        public IReadOnlyList<TestSuiteDescriptor> Suites
        {
            get { return AdapterFixtures.Suites(AdapterFixtureSet.EmptySuites); }
        }
    }

    /// <summary>
    /// Suite provider for <see cref="AdapterFixtureSet.Duplicate"/>.
    /// </summary>
    public sealed class DuplicateProvider : ITestSuiteProvider
    {
        /// <inheritdoc />
        public IReadOnlyList<TestSuiteDescriptor> Suites
        {
            get { return AdapterFixtures.Suites(AdapterFixtureSet.Duplicate); }
        }
    }

    /// <summary>
    /// Suite provider for <see cref="AdapterFixtureSet.Cancel"/>.
    /// </summary>
    public sealed class CancelProvider : ITestSuiteProvider
    {
        /// <inheritdoc />
        public IReadOnlyList<TestSuiteDescriptor> Suites
        {
            get { return AdapterFixtures.Suites(AdapterFixtureSet.Cancel); }
        }
    }

    /// <summary>
    /// Suite provider whose suites are null.
    /// </summary>
    public sealed class NullProvider : ITestSuiteProvider
    {
        /// <inheritdoc />
        public IReadOnlyList<TestSuiteDescriptor> Suites
        {
            get { return null!; }
        }
    }
}
