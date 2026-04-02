namespace Touchstone.Core
{
    using System;

    /// <summary>
    /// Aggregated results for a suite or an entire test run.
    /// </summary>
    public sealed class TestRunSummary
    {
        private int _Total;
        private int _Passed;
        private int _Failed;
        private int _Skipped;
        private TimeSpan _Duration;

        /// <summary>
        /// Initialize a new test run summary.
        /// </summary>
        public TestRunSummary()
        {
        }

        /// <summary>
        /// Total number of test cases.
        /// </summary>
        public int Total
        {
            get { return _Total; }
            set { _Total = value < 0 ? 0 : value; }
        }

        /// <summary>
        /// Number of test cases that passed.
        /// </summary>
        public int Passed
        {
            get { return _Passed; }
            set { _Passed = value < 0 ? 0 : value; }
        }

        /// <summary>
        /// Number of test cases that failed.
        /// </summary>
        public int Failed
        {
            get { return _Failed; }
            set { _Failed = value < 0 ? 0 : value; }
        }

        /// <summary>
        /// Number of test cases that were skipped.
        /// </summary>
        public int Skipped
        {
            get { return _Skipped; }
            set { _Skipped = value < 0 ? 0 : value; }
        }

        /// <summary>
        /// Wall-clock duration of the suite or run.
        /// </summary>
        public TimeSpan Duration
        {
            get { return _Duration; }
            set { _Duration = value < TimeSpan.Zero ? TimeSpan.Zero : value; }
        }

        /// <summary>
        /// True when no tests failed.
        /// </summary>
        public bool IsSuccess
        {
            get { return _Failed == 0; }
        }
    }
}
