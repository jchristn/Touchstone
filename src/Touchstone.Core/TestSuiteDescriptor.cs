namespace Touchstone.Core
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// Describes a suite of related test cases with optional lifecycle hooks. Instances are immutable.
    /// Thread safety: safe to share. Running the same suite concurrently runs its hooks concurrently, so hooks that
    /// touch shared state must synchronize.
    /// </summary>
    public sealed class TestSuiteDescriptor
    {
        private readonly string _SuiteId;
        private readonly string _DisplayName;
        private readonly IReadOnlyList<TestCaseDescriptor> _Cases;
        private readonly Func<CancellationToken, ValueTask>? _BeforeSuiteAsync;
        private readonly Func<CancellationToken, ValueTask>? _AfterSuiteAsync;

        /// <summary>
        /// Initialize a new test suite descriptor.
        /// </summary>
        /// <param name="suiteId">Unique identifier for this suite. Letters, digits, '_' and '-'.</param>
        /// <param name="displayName">Human-readable suite name. Cannot be blank.</param>
        /// <param name="cases">Cases in this suite, in execution order. Copied. May be empty. Every case must have
        /// <paramref name="suiteId"/> as its suite identity, and case identities must be unique (ordinal).</param>
        /// <param name="beforeSuiteAsync">Optional setup hook run once before the suite's cases. Null when unused.</param>
        /// <param name="afterSuiteAsync">Optional teardown hook run once after the suite's cases, including after
        /// failures and cancellation. Null when unused.</param>
        /// <exception cref="ArgumentNullException">The identity, display name, or case list is null.</exception>
        /// <exception cref="ArgumentException">The identity or display name is invalid, the list contains null, or a
        /// case belongs to a different suite.</exception>
        /// <exception cref="DuplicateTestIdException">Two cases share a case identity.</exception>
        public TestSuiteDescriptor(
            string suiteId,
            string displayName,
            IReadOnlyList<TestCaseDescriptor> cases,
            Func<CancellationToken, ValueTask>? beforeSuiteAsync = null,
            Func<CancellationToken, ValueTask>? afterSuiteAsync = null)
        {
            _SuiteId = IdentifierRules.SuiteId(suiteId, nameof(suiteId));
            _DisplayName = IdentifierRules.DisplayName(displayName, nameof(displayName));
            if (cases == null) throw new ArgumentNullException(nameof(cases));

            List<TestCaseDescriptor> copy = new List<TestCaseDescriptor>(cases.Count);
            HashSet<string> caseIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (TestCaseDescriptor? testCase in cases)
            {
                if (testCase == null)
                    throw new ArgumentException("Suite " + suiteId + " contains a null case.", nameof(cases));

                if (!string.Equals(testCase.SuiteId, suiteId, StringComparison.Ordinal))
                    throw new ArgumentException(
                        "Case " + testCase.TestId + " belongs to suite " + testCase.SuiteId + ", not " + suiteId + ".",
                        nameof(cases));

                if (!caseIds.Add(testCase.CaseId))
                    throw new DuplicateTestIdException(
                        testCase.TestId,
                        "Suite " + suiteId + " contains more than one case with identity " + testCase.TestId + ".",
                        nameof(cases));

                copy.Add(testCase);
            }

            _Cases = copy.AsReadOnly();
            _BeforeSuiteAsync = beforeSuiteAsync;
            _AfterSuiteAsync = afterSuiteAsync;
        }

        /// <summary>
        /// Unique identifier for this suite. Never null.
        /// </summary>
        public string SuiteId
        {
            get { return _SuiteId; }
        }

        /// <summary>
        /// Human-readable suite name. Never null or blank.
        /// </summary>
        public string DisplayName
        {
            get { return _DisplayName; }
        }

        /// <summary>
        /// Cases in this suite, in execution order. Never null.
        /// </summary>
        public IReadOnlyList<TestCaseDescriptor> Cases
        {
            get { return _Cases; }
        }

        /// <summary>
        /// Optional setup hook run once before the suite's cases. Null when unused.
        /// </summary>
        public Func<CancellationToken, ValueTask>? BeforeSuiteAsync
        {
            get { return _BeforeSuiteAsync; }
        }

        /// <summary>
        /// Optional teardown hook run once after the suite's cases. Null when unused.
        /// </summary>
        public Func<CancellationToken, ValueTask>? AfterSuiteAsync
        {
            get { return _AfterSuiteAsync; }
        }

        /// <summary>
        /// Returns the display name.
        /// </summary>
        /// <returns>The display name.</returns>
        public override string ToString()
        {
            return _DisplayName;
        }
    }
}
