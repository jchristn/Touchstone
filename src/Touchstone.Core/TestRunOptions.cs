namespace Touchstone.Core
{
    using System;

    /// <summary>
    /// Settings for a run. Pass null to any executor entry point to use the defaults.
    /// Thread safety: not thread-safe while being configured. Runs take a snapshot when they start, so one instance
    /// can drive several runs, and changes made during a run do not affect it.
    /// </summary>
    public sealed class TestRunOptions
    {
        private TestFilter _Filter = new TestFilter();
        private SkipBehavior _SkipBehavior = SkipBehavior.Report;
        private TimeSpan _CleanupTimeout = TimeSpan.FromSeconds(30);

        /// <summary>
        /// Initialize options with the defaults.
        /// </summary>
        public TestRunOptions()
        {
        }

        /// <summary>
        /// Selects which cases run. Default: an empty filter that selects every case. Cannot be null.
        /// </summary>
        /// <exception cref="ArgumentNullException">The value is null.</exception>
        public TestFilter Filter
        {
            get { return _Filter; }
            set { _Filter = value ?? throw new ArgumentNullException(nameof(value)); }
        }

        /// <summary>
        /// How skipped cases are treated. Default: <see cref="SkipBehavior.Report"/>.
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException">The value is undefined.</exception>
        public SkipBehavior SkipBehavior
        {
            get { return _SkipBehavior; }
            set
            {
                if (!Enum.IsDefined(value)) throw new ArgumentOutOfRangeException(nameof(value), value, "Unknown skip behavior.");
                _SkipBehavior = value;
            }
        }

        /// <summary>
        /// When true, the first failed case stops the run: later cases are reported as <see cref="TestOutcome.NotRun"/>,
        /// later suites do not start, and the current suite's teardown hook still runs. Default: false.
        /// </summary>
        public bool StopOnFirstFailure { get; set; }

        /// <summary>
        /// Time a suite teardown hook may keep running after the caller cancels. The hook's token is canceled when this
        /// budget expires. Default 30 seconds. Must be greater than zero and at most one day.
        /// Cancellation is cooperative and cannot stop a hook that ignores its token.
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException">The value is zero, negative, or more than one day.</exception>
        public TimeSpan CleanupTimeout
        {
            get { return _CleanupTimeout; }
            set
            {
                if (value <= TimeSpan.Zero || value > TimeSpan.FromDays(1))
                    throw new ArgumentOutOfRangeException(nameof(value), value, "Cleanup timeout must be greater than zero and at most one day.");
                _CleanupTimeout = value;
            }
        }

        internal TestRunOptions Snapshot()
        {
            TestRunOptions copy = new TestRunOptions();
            copy._Filter = _Filter.Snapshot();
            copy._SkipBehavior = _SkipBehavior;
            copy.StopOnFirstFailure = StopOnFirstFailure;
            copy._CleanupTimeout = _CleanupTimeout;
            return copy;
        }
    }
}
