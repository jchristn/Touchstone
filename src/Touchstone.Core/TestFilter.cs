namespace Touchstone.Core
{
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// Selects which cases a run executes. An empty filter selects every case.
    /// A case is selected when all of these hold: it carries at least one <see cref="IncludeTags"/> tag (or the list is
    /// empty), it carries no <see cref="ExcludeTags"/> tag, its suite is in <see cref="SuiteIds"/> (or the list is empty),
    /// and its test identity is in <see cref="TestIds"/> (or the list is empty). Exclusion wins over inclusion.
    /// Tags compare case-insensitively; suite and test identities compare ordinally.
    /// Thread safety: not thread-safe while being configured. Runs take a snapshot when they start, so changes made
    /// during a run do not affect it.
    /// </summary>
    public sealed class TestFilter
    {
        private readonly List<string> _IncludeTags;
        private readonly List<string> _ExcludeTags;
        private readonly List<string> _SuiteIds;
        private readonly List<string> _TestIds;

        /// <summary>
        /// Initialize an empty filter that selects every case.
        /// </summary>
        public TestFilter()
        {
            _IncludeTags = new List<string>();
            _ExcludeTags = new List<string>();
            _SuiteIds = new List<string>();
            _TestIds = new List<string>();
        }

        /// <summary>
        /// Tags a case must carry at least one of. Empty means no tag requirement. Never null.
        /// </summary>
        public IList<string> IncludeTags
        {
            get { return _IncludeTags; }
        }

        /// <summary>
        /// Tags that exclude a case. Empty means no exclusion. Never null.
        /// </summary>
        public IList<string> ExcludeTags
        {
            get { return _ExcludeTags; }
        }

        /// <summary>
        /// Suite identities to run. Empty means every suite. Never null.
        /// </summary>
        public IList<string> SuiteIds
        {
            get { return _SuiteIds; }
        }

        /// <summary>
        /// Test identities (<c>SuiteId.CaseId</c>) to run. Empty means every case. Never null.
        /// </summary>
        public IList<string> TestIds
        {
            get { return _TestIds; }
        }

        /// <summary>
        /// True when the filter has no criteria and selects every case.
        /// </summary>
        public bool IsEmpty
        {
            get { return _IncludeTags.Count == 0 && _ExcludeTags.Count == 0 && _SuiteIds.Count == 0 && _TestIds.Count == 0; }
        }

        /// <summary>
        /// Decide whether a case is selected.
        /// </summary>
        /// <param name="testCase">Case to test.</param>
        /// <returns>True when the case is selected.</returns>
        /// <exception cref="ArgumentNullException">The case is null.</exception>
        public bool Matches(TestCaseDescriptor testCase)
        {
            if (testCase == null) throw new ArgumentNullException(nameof(testCase));

            if (_SuiteIds.Count > 0 && !_SuiteIds.Contains(testCase.SuiteId)) return false;
            if (_TestIds.Count > 0 && !_TestIds.Contains(testCase.TestId)) return false;

            foreach (string tag in testCase.Tags)
            {
                if (ContainsIgnoreCase(_ExcludeTags, tag)) return false;
            }

            if (_IncludeTags.Count == 0) return true;

            foreach (string tag in testCase.Tags)
            {
                if (ContainsIgnoreCase(_IncludeTags, tag)) return true;
            }

            return false;
        }

        /// <summary>
        /// Check every configured value. Runs perform the same check when they start.
        /// </summary>
        /// <exception cref="ArgumentException">A tag, suite identity, or test identity is null, blank, or contains a
        /// character its grammar does not allow. The message names the value.</exception>
        public void Validate()
        {
            Snapshot();
        }

        internal TestFilter Snapshot()
        {
            TestFilter copy = new TestFilter();
            foreach (string tag in _IncludeTags) copy._IncludeTags.Add(IdentifierRules.Tag(tag, nameof(IncludeTags)));
            foreach (string tag in _ExcludeTags) copy._ExcludeTags.Add(IdentifierRules.Tag(tag, nameof(ExcludeTags)));
            foreach (string suiteId in _SuiteIds) copy._SuiteIds.Add(IdentifierRules.SuiteId(suiteId, nameof(SuiteIds)));
            foreach (string testId in _TestIds)
            {
                if (string.IsNullOrWhiteSpace(testId))
                    throw new ArgumentException("Test identities in a filter cannot be null or blank.", nameof(TestIds));
                copy._TestIds.Add(testId);
            }

            return copy;
        }

        private static bool ContainsIgnoreCase(List<string> values, string value)
        {
            foreach (string candidate in values)
            {
                if (string.Equals(candidate, value, StringComparison.OrdinalIgnoreCase)) return true;
            }

            return false;
        }
    }
}
