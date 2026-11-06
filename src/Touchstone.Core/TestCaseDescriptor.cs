namespace Touchstone.Core
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// Describes a single test case within a suite. Instances are immutable; use the <c>With</c> methods to derive
    /// changed copies. Execute cases through <see cref="TestExecutor"/> or a framework adapter.
    /// Thread safety: safe to share and execute concurrently. Concurrency inside the case body is the author's
    /// responsibility.
    /// </summary>
    public sealed class TestCaseDescriptor
    {
        private readonly string _SuiteId;
        private readonly string _CaseId;
        private readonly string _DisplayName;
        private readonly IReadOnlyList<string> _Tags;
        private readonly string? _SkipReason;
        private readonly Func<CancellationToken, Task>? _Body;
        private readonly IScenarioDefinition? _Scenario;

        /// <summary>
        /// Initialize a new test case descriptor.
        /// </summary>
        /// <param name="suiteId">Identifier of the parent suite. Letters, digits, '_' and '-'; '.' is not allowed.</param>
        /// <param name="caseId">Identifier of this case within the suite. Letters, digits, '_', '-' and '.'.</param>
        /// <param name="displayName">Human-readable name shown in test output. Cannot be blank.</param>
        /// <param name="executeAsync">Async delegate that performs the test. Throw to fail the case.</param>
        /// <param name="tags">Optional tags for filtering. Letters, digits, '_', '-', '.' and ':'. Null means no tags.
        /// Tags are copied, and duplicates that differ only by case are collapsed to the first spelling.</param>
        /// <param name="skipReason">Reason to skip the case. Null (the default) means the case is not skipped.</param>
        /// <exception cref="ArgumentNullException">An identity, the display name, or the delegate is null.</exception>
        /// <exception cref="ArgumentException">An identity, the display name, a tag, or the skip reason is invalid.</exception>
        public TestCaseDescriptor(
            string suiteId,
            string caseId,
            string displayName,
            Func<CancellationToken, Task> executeAsync,
            IReadOnlyList<string>? tags = null,
            string? skipReason = null)
        {
            _SuiteId = IdentifierRules.SuiteId(suiteId, nameof(suiteId));
            _CaseId = IdentifierRules.CaseId(caseId, nameof(caseId));
            _DisplayName = IdentifierRules.DisplayName(displayName, nameof(displayName));
            _Body = executeAsync ?? throw new ArgumentNullException(nameof(executeAsync));
            _Tags = IdentifierRules.Tags(tags, nameof(tags));
            _SkipReason = IdentifierRules.SkipReason(skipReason, nameof(skipReason));
        }

        internal TestCaseDescriptor(
            string suiteId,
            string caseId,
            string displayName,
            IScenarioDefinition scenario,
            IReadOnlyList<string>? tags,
            string? skipReason)
        {
            _SuiteId = IdentifierRules.SuiteId(suiteId, nameof(suiteId));
            _CaseId = IdentifierRules.CaseId(caseId, nameof(caseId));
            _DisplayName = IdentifierRules.DisplayName(displayName, nameof(displayName));
            _Scenario = scenario ?? throw new ArgumentNullException(nameof(scenario));
            _Tags = IdentifierRules.Tags(tags, nameof(tags));
            _SkipReason = IdentifierRules.SkipReason(skipReason, nameof(skipReason));
        }

        private TestCaseDescriptor(TestCaseDescriptor source, string displayName, IReadOnlyList<string> tags, string? skipReason)
        {
            _SuiteId = source._SuiteId;
            _CaseId = source._CaseId;
            _DisplayName = displayName;
            _Body = source._Body;
            _Scenario = source._Scenario;
            _Tags = tags;
            _SkipReason = skipReason;
        }

        /// <summary>
        /// Identifier of the parent suite. Never null.
        /// </summary>
        public string SuiteId
        {
            get { return _SuiteId; }
        }

        /// <summary>
        /// Identifier of this case within the suite. Never null.
        /// </summary>
        public string CaseId
        {
            get { return _CaseId; }
        }

        /// <summary>
        /// Composite identity, <c>SuiteId + "." + CaseId</c>. Unique for valid identities because suite identities
        /// cannot contain '.'.
        /// </summary>
        public string TestId
        {
            get { return _SuiteId + "." + _CaseId; }
        }

        /// <summary>
        /// Human-readable name shown in test output. Never null or blank.
        /// </summary>
        public string DisplayName
        {
            get { return _DisplayName; }
        }

        /// <summary>
        /// Tags used for filtering. Never null; empty when the case has no tags.
        /// </summary>
        public IReadOnlyList<string> Tags
        {
            get { return _Tags; }
        }

        /// <summary>
        /// Reason the case is skipped. Null when the case is not skipped.
        /// </summary>
        public string? SkipReason
        {
            get { return _SkipReason; }
        }

        /// <summary>
        /// True when the case carries a skip reason.
        /// </summary>
        public bool IsSkipped
        {
            get { return _SkipReason != null; }
        }

        /// <summary>
        /// Feature and example metadata for a structured scenario. Null for ordinary cases.
        /// </summary>
        public ScenarioMetadata? Metadata
        {
            get { return _Scenario?.Metadata; }
        }

        internal Func<CancellationToken, Task>? Body
        {
            get { return _Body; }
        }

        internal IScenarioDefinition? Scenario
        {
            get { return _Scenario; }
        }

        /// <summary>
        /// Create a copy with different tags.
        /// </summary>
        /// <param name="tags">Replacement tags. Null means no tags. Validated like the constructor's tags.</param>
        /// <returns>A new descriptor. This instance is unchanged.</returns>
        /// <exception cref="ArgumentException">A tag is invalid.</exception>
        public TestCaseDescriptor WithTags(IReadOnlyList<string>? tags)
        {
            return new TestCaseDescriptor(this, _DisplayName, IdentifierRules.Tags(tags, nameof(tags)), _SkipReason);
        }

        /// <summary>
        /// Create a skipped copy.
        /// </summary>
        /// <param name="reason">Reason the case is skipped. Cannot be null or blank.</param>
        /// <returns>A new descriptor. This instance is unchanged.</returns>
        /// <exception cref="ArgumentNullException">The reason is null.</exception>
        /// <exception cref="ArgumentException">The reason is blank.</exception>
        public TestCaseDescriptor WithSkip(string reason)
        {
            if (reason == null) throw new ArgumentNullException(nameof(reason));
            return new TestCaseDescriptor(this, _DisplayName, _Tags, IdentifierRules.SkipReason(reason, nameof(reason)));
        }

        /// <summary>
        /// Create a copy that is not skipped.
        /// </summary>
        /// <returns>A new descriptor. This instance is unchanged.</returns>
        public TestCaseDescriptor WithoutSkip()
        {
            return new TestCaseDescriptor(this, _DisplayName, _Tags, null);
        }

        /// <summary>
        /// Create a copy with a different display name.
        /// </summary>
        /// <param name="displayName">Replacement display name. Cannot be null or blank.</param>
        /// <returns>A new descriptor. This instance is unchanged.</returns>
        /// <exception cref="ArgumentNullException">The display name is null.</exception>
        /// <exception cref="ArgumentException">The display name is blank.</exception>
        public TestCaseDescriptor WithDisplayName(string displayName)
        {
            return new TestCaseDescriptor(this, IdentifierRules.DisplayName(displayName, nameof(displayName)), _Tags, _SkipReason);
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
