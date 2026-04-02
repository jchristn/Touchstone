namespace Touchstone.Core
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// Describes a suite of related test cases with optional lifecycle hooks.
    /// </summary>
    public sealed class TestSuiteDescriptor
    {
        private string _SuiteId = string.Empty;
        private string _DisplayName = string.Empty;
        private IReadOnlyList<TestCaseDescriptor> _Cases = Array.Empty<TestCaseDescriptor>();
        private Func<CancellationToken, ValueTask> _BeforeSuiteAsync = null;
        private Func<CancellationToken, ValueTask> _AfterSuiteAsync = null;

        /// <summary>
        /// Initialize a new test suite descriptor.
        /// </summary>
        /// <param name="suiteId">Unique identifier for this suite.</param>
        /// <param name="displayName">Human-readable suite name.</param>
        /// <param name="cases">Test cases contained in this suite.</param>
        /// <param name="beforeSuiteAsync">Optional setup hook run before any test in the suite.</param>
        /// <param name="afterSuiteAsync">Optional teardown hook run after all tests in the suite.</param>
        public TestSuiteDescriptor(
            string suiteId,
            string displayName,
            IReadOnlyList<TestCaseDescriptor> cases,
            Func<CancellationToken, ValueTask> beforeSuiteAsync = null,
            Func<CancellationToken, ValueTask> afterSuiteAsync = null)
        {
            SuiteId = suiteId ?? throw new ArgumentNullException(nameof(suiteId));
            DisplayName = displayName ?? throw new ArgumentNullException(nameof(displayName));
            Cases = cases ?? throw new ArgumentNullException(nameof(cases));
            BeforeSuiteAsync = beforeSuiteAsync;
            AfterSuiteAsync = afterSuiteAsync;
        }

        /// <summary>
        /// Unique identifier for this suite.
        /// </summary>
        public string SuiteId
        {
            get { return _SuiteId; }
            set { _SuiteId = value ?? throw new ArgumentNullException(nameof(SuiteId)); }
        }

        /// <summary>
        /// Human-readable suite name.
        /// </summary>
        public string DisplayName
        {
            get { return _DisplayName; }
            set { _DisplayName = value ?? throw new ArgumentNullException(nameof(DisplayName)); }
        }

        /// <summary>
        /// Test cases contained in this suite.
        /// </summary>
        public IReadOnlyList<TestCaseDescriptor> Cases
        {
            get { return _Cases; }
            set { _Cases = value ?? throw new ArgumentNullException(nameof(Cases)); }
        }

        /// <summary>
        /// Optional setup hook run before any test in the suite. Null when unused.
        /// </summary>
        public Func<CancellationToken, ValueTask> BeforeSuiteAsync
        {
            get { return _BeforeSuiteAsync; }
            set { _BeforeSuiteAsync = value; }
        }

        /// <summary>
        /// Optional teardown hook run after all tests in the suite. Null when unused.
        /// </summary>
        public Func<CancellationToken, ValueTask> AfterSuiteAsync
        {
            get { return _AfterSuiteAsync; }
            set { _AfterSuiteAsync = value; }
        }

        /// <summary>
        /// Returns the display name.
        /// </summary>
        /// <returns>Display name string.</returns>
        public override string ToString()
        {
            return _DisplayName;
        }
    }
}
