namespace Touchstone.Core
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// Describes a single test case within a suite.
    /// </summary>
    public sealed class TestCaseDescriptor
    {
        private string _SuiteId = string.Empty;
        private string _CaseId = string.Empty;
        private string _DisplayName = string.Empty;
        private Func<CancellationToken, Task> _ExecuteAsync = _ => Task.CompletedTask;
        private IReadOnlyList<string> _Tags = Array.Empty<string>();
        private bool _Skip;
        private string _SkipReason = null;

        /// <summary>
        /// Initialize a new test case descriptor.
        /// </summary>
        /// <param name="suiteId">Identifier of the parent suite.</param>
        /// <param name="caseId">Identifier of this test case within the suite.</param>
        /// <param name="displayName">Human-readable name shown in test output.</param>
        /// <param name="executeAsync">Async delegate that performs the test.</param>
        /// <param name="tags">Optional tags for filtering.</param>
        /// <param name="skip">True to skip this test at runtime.</param>
        /// <param name="skipReason">Reason the test is skipped.</param>
        public TestCaseDescriptor(
            string suiteId,
            string caseId,
            string displayName,
            Func<CancellationToken, Task> executeAsync,
            IReadOnlyList<string> tags = null,
            bool skip = false,
            string skipReason = null)
        {
            SuiteId = suiteId ?? throw new ArgumentNullException(nameof(suiteId));
            CaseId = caseId ?? throw new ArgumentNullException(nameof(caseId));
            DisplayName = displayName ?? throw new ArgumentNullException(nameof(displayName));
            ExecuteAsync = executeAsync ?? throw new ArgumentNullException(nameof(executeAsync));
            Tags = tags ?? Array.Empty<string>();
            Skip = skip;
            SkipReason = skipReason;
        }

        /// <summary>
        /// Identifier of the parent suite.
        /// </summary>
        public string SuiteId
        {
            get { return _SuiteId; }
            set { _SuiteId = value ?? throw new ArgumentNullException(nameof(SuiteId)); }
        }

        /// <summary>
        /// Identifier of this test case within the suite.
        /// </summary>
        public string CaseId
        {
            get { return _CaseId; }
            set { _CaseId = value ?? throw new ArgumentNullException(nameof(CaseId)); }
        }

        /// <summary>
        /// Human-readable name shown in test output.
        /// </summary>
        public string DisplayName
        {
            get { return _DisplayName; }
            set { _DisplayName = value ?? throw new ArgumentNullException(nameof(DisplayName)); }
        }

        /// <summary>
        /// Async delegate that performs the test.
        /// </summary>
        public Func<CancellationToken, Task> ExecuteAsync
        {
            get { return _ExecuteAsync; }
            set { _ExecuteAsync = value ?? throw new ArgumentNullException(nameof(ExecuteAsync)); }
        }

        /// <summary>
        /// Optional tags for filtering.
        /// </summary>
        public IReadOnlyList<string> Tags
        {
            get { return _Tags; }
            set { _Tags = value ?? Array.Empty<string>(); }
        }

        /// <summary>
        /// True to skip this test at runtime.
        /// </summary>
        public bool Skip
        {
            get { return _Skip; }
            set { _Skip = value; }
        }

        /// <summary>
        /// Reason the test is skipped. Null when not skipped.
        /// </summary>
        public string SkipReason
        {
            get { return _SkipReason; }
            set { _SkipReason = value; }
        }

        /// <summary>
        /// Composite identity formed from SuiteId and CaseId.
        /// </summary>
        public string TestId
        {
            get { return _SuiteId + "." + _CaseId; }
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
