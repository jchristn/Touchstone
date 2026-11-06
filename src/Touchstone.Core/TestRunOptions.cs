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
        private const int MaxParallelismLimit = 1024;

        private TimeSpan _CleanupTimeout = TimeSpan.FromSeconds(30);
        private TimeSpan? _CaseTimeout = null;
        private int _MaxDegreeOfParallelism = 1;

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

        /// <summary>
        /// Longest time one case may run. Default: null, which means no timeout. When set, must be greater than zero
        /// and at most one day.
        /// The timer covers the case body, or a scenario's context creation and steps. It does not cover scenario
        /// cleanup (which has its own budget), suite hooks, or sink callbacks. When the timeout elapses, the case's
        /// cancellation token is canceled. A case that is still running at that point, or that then throws, is reported
        /// as <see cref="TestOutcome.Failed"/> with a <see cref="CaseTimeoutException"/>. Caller cancellation still
        /// reports <see cref="TestOutcome.Canceled"/>.
        /// Cancellation is cooperative: the engine waits for the case to return, so a case that ignores its token
        /// finishes before the next case starts and is then reported as timed out.
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException">The value is zero, negative, or more than one day.</exception>
        public TimeSpan? CaseTimeout
        {
            get { return _CaseTimeout; }
            set
            {
                if (value.HasValue && (value.Value <= TimeSpan.Zero || value.Value > TimeSpan.FromDays(1)))
                    throw new ArgumentOutOfRangeException(nameof(value), value, "Case timeout must be null, or greater than zero and at most one day.");
                _CaseTimeout = value;
            }
        }

        /// <summary>
        /// Most cases of one suite that run at the same time. Default 1, which runs cases one at a time in declaration
        /// order. Minimum 1, maximum 1024.
        /// Suites still run one after another, and each suite's setup hook runs before its first case and its teardown
        /// hook after its last. Above 1, cases start in declaration order and results reach the sink in completion order.
        /// Sink callbacks are never concurrent. With <see cref="StopOnFirstFailure"/> or caller cancellation, cases that
        /// have not started are reported as <see cref="TestOutcome.NotRun"/> and cases already running finish.
        /// Cases that share state must be safe to run concurrently before you raise this value.
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException">The value is less than 1 or more than 1024.</exception>
        public int MaxDegreeOfParallelism
        {
            get { return _MaxDegreeOfParallelism; }
            set
            {
                if (value < 1 || value > MaxParallelismLimit)
                    throw new ArgumentOutOfRangeException(nameof(value), value, "Max degree of parallelism must be from 1 to 1024.");
                _MaxDegreeOfParallelism = value;
            }
        }

        internal TestRunOptions Snapshot()
        {
            TestRunOptions copy = new TestRunOptions();
            copy._Filter = _Filter.Snapshot();
            copy._SkipBehavior = _SkipBehavior;
            copy.StopOnFirstFailure = StopOnFirstFailure;
            copy._CleanupTimeout = _CleanupTimeout;
            copy._CaseTimeout = _CaseTimeout;
            copy._MaxDegreeOfParallelism = _MaxDegreeOfParallelism;
            return copy;
        }
    }
}
