namespace Test.Shared
{
    using System;
    using System.Collections.Generic;
    using Touchstone.Core;
    using Touchstone.XunitAdapter;

    /// <summary>
    /// Exposes the protected members of <see cref="TouchstoneTestBase"/> so shared suites can exercise them directly.
    /// </summary>
    public sealed class XunitTheoryHarness : TouchstoneTestBase
    {
        private readonly IReadOnlyList<TestSuiteDescriptor> _Suites;

        /// <summary>
        /// Initialize the harness with the suites to expose.
        /// </summary>
        /// <param name="suites">Suites returned by the Suites property.</param>
        public XunitTheoryHarness(IReadOnlyList<TestSuiteDescriptor> suites)
        {
            _Suites = suites ?? throw new ArgumentNullException(nameof(suites));
        }

        /// <inheritdoc />
        protected override IReadOnlyList<TestSuiteDescriptor> Suites
        {
            get { return _Suites; }
        }
    }
}
