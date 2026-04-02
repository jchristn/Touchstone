namespace Touchstone.Xunit
{
    using System.Collections.Generic;

    using Touchstone.Core;
    using global::Xunit;

    /// <summary>
    /// Base class for theory-driven Touchstone test classes.
    /// Subclasses provide suites; this base exposes them as TheoryData.
    /// </summary>
    public abstract class TouchstoneTestBase
    {
        /// <summary>
        /// Suites whose test cases are exercised by this test class.
        /// </summary>
        protected abstract IReadOnlyList<TestSuiteDescriptor> Suites { get; }

        /// <summary>
        /// TheoryData source for MemberData attributes.
        /// </summary>
        public TheoryData<TestCaseDescriptor> TestCases
        {
            get { return new TouchstoneTheoryData(Suites); }
        }
    }
}
