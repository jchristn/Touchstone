namespace Touchstone.Core
{
    using System;

    /// <summary>
    /// Outcome of a single test case execution.
    /// </summary>
    public sealed class TestResult
    {
        private string _TestId = string.Empty;
        private string _SuiteId = string.Empty;
        private string _CaseId = string.Empty;
        private string _DisplayName = string.Empty;
        private bool _Success;
        private bool _Skipped;
        private TimeSpan _Duration;
        private Exception _Exception = null;
        private string _Message = null;

        /// <summary>
        /// Initialize a new test result.
        /// </summary>
        public TestResult()
        {
        }

        /// <summary>
        /// Composite test identity.
        /// </summary>
        public string TestId
        {
            get { return _TestId; }
            set { _TestId = value ?? throw new ArgumentNullException(nameof(TestId)); }
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
        /// Identifier of the test case within the suite.
        /// </summary>
        public string CaseId
        {
            get { return _CaseId; }
            set { _CaseId = value ?? throw new ArgumentNullException(nameof(CaseId)); }
        }

        /// <summary>
        /// Human-readable test name.
        /// </summary>
        public string DisplayName
        {
            get { return _DisplayName; }
            set { _DisplayName = value ?? throw new ArgumentNullException(nameof(DisplayName)); }
        }

        /// <summary>
        /// True when the test passed or was skipped.
        /// </summary>
        public bool Success
        {
            get { return _Success; }
            set { _Success = value; }
        }

        /// <summary>
        /// True when the test was skipped.
        /// </summary>
        public bool Skipped
        {
            get { return _Skipped; }
            set { _Skipped = value; }
        }

        /// <summary>
        /// Wall-clock duration of the test execution.
        /// </summary>
        public TimeSpan Duration
        {
            get { return _Duration; }
            set { _Duration = value < TimeSpan.Zero ? TimeSpan.Zero : value; }
        }

        /// <summary>
        /// Exception captured during a failed test. Null on success.
        /// </summary>
        public Exception Exception
        {
            get { return _Exception; }
            set { _Exception = value; }
        }

        /// <summary>
        /// Human-readable failure or skip message. Null on success.
        /// </summary>
        public string Message
        {
            get { return _Message; }
            set { _Message = value; }
        }
    }
}
