namespace Touchstone.Core
{
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// Immutable aggregate of a suite run or a multi-suite run.
    /// Thread safety: safe to read concurrently.
    /// </summary>
    public sealed class TestRunSummary
    {
        private readonly int _Passed;
        private readonly int _Failed;
        private readonly int _Skipped;
        private readonly int _Canceled;
        private readonly int _NotRun;
        private readonly int _Filtered;
        private readonly TimeSpan _Duration;
        private readonly bool _WasCanceled;
        private readonly IReadOnlyList<SuiteLifecycleError> _LifecycleErrors;

        /// <summary>
        /// Initialize a new summary.
        /// </summary>
        /// <param name="passed">Cases that passed. Minimum 0.</param>
        /// <param name="failed">Cases that failed. Minimum 0.</param>
        /// <param name="skipped">Cases reported as skipped. Minimum 0.</param>
        /// <param name="canceled">Cases that observed cancellation. Minimum 0.</param>
        /// <param name="notRun">Selected cases that never started. Minimum 0.</param>
        /// <param name="filtered">Cases excluded by the filter or by <see cref="SkipBehavior.Omit"/>. Minimum 0.</param>
        /// <param name="duration">Wall-clock duration. Cannot be negative.</param>
        /// <param name="wasCanceled">True when caller cancellation stopped the run.</param>
        /// <param name="lifecycleErrors">Failed suite hooks. Null means none. Copied.</param>
        /// <exception cref="ArgumentOutOfRangeException">A count or the duration is negative.</exception>
        /// <exception cref="ArgumentNullException">The lifecycle error list contains null.</exception>
        public TestRunSummary(
            int passed,
            int failed,
            int skipped,
            int canceled,
            int notRun,
            int filtered,
            TimeSpan duration,
            bool wasCanceled = false,
            IReadOnlyList<SuiteLifecycleError>? lifecycleErrors = null)
        {
            _Passed = NonNegative(passed, nameof(passed));
            _Failed = NonNegative(failed, nameof(failed));
            _Skipped = NonNegative(skipped, nameof(skipped));
            _Canceled = NonNegative(canceled, nameof(canceled));
            _NotRun = NonNegative(notRun, nameof(notRun));
            _Filtered = NonNegative(filtered, nameof(filtered));
            if (duration < TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(duration), duration, "Duration cannot be negative.");
            _Duration = duration;
            _WasCanceled = wasCanceled;

            if (lifecycleErrors == null || lifecycleErrors.Count == 0)
            {
                _LifecycleErrors = Array.Empty<SuiteLifecycleError>();
            }
            else
            {
                SuiteLifecycleError[] copy = new SuiteLifecycleError[lifecycleErrors.Count];
                for (int i = 0; i < copy.Length; i++)
                    copy[i] = lifecycleErrors[i] ?? throw new ArgumentNullException(nameof(lifecycleErrors), "Collection contains null.");
                _LifecycleErrors = Array.AsReadOnly(copy);
            }
        }

        /// <summary>
        /// Selected cases: the sum of passed, failed, skipped, canceled, and not-run cases. Excludes <see cref="Filtered"/>.
        /// </summary>
        public int Total
        {
            get { return _Passed + _Failed + _Skipped + _Canceled + _NotRun; }
        }

        /// <summary>
        /// Cases that passed.
        /// </summary>
        public int Passed
        {
            get { return _Passed; }
        }

        /// <summary>
        /// Cases that failed.
        /// </summary>
        public int Failed
        {
            get { return _Failed; }
        }

        /// <summary>
        /// Cases reported as skipped.
        /// </summary>
        public int Skipped
        {
            get { return _Skipped; }
        }

        /// <summary>
        /// Cases that observed caller cancellation.
        /// </summary>
        public int Canceled
        {
            get { return _Canceled; }
        }

        /// <summary>
        /// Selected cases that never started because the run was canceled or stopped.
        /// </summary>
        public int NotRun
        {
            get { return _NotRun; }
        }

        /// <summary>
        /// Cases excluded by the filter or by <see cref="SkipBehavior.Omit"/>. Not part of <see cref="Total"/>.
        /// </summary>
        public int Filtered
        {
            get { return _Filtered; }
        }

        /// <summary>
        /// Wall-clock duration, including hooks and sink callbacks.
        /// </summary>
        public TimeSpan Duration
        {
            get { return _Duration; }
        }

        /// <summary>
        /// True when caller cancellation stopped the run.
        /// </summary>
        public bool WasCanceled
        {
            get { return _WasCanceled; }
        }

        /// <summary>
        /// Failed suite hooks, in the order they happened. Never null.
        /// </summary>
        public IReadOnlyList<SuiteLifecycleError> LifecycleErrors
        {
            get { return _LifecycleErrors; }
        }

        /// <summary>
        /// True when no case failed, was canceled, or was left unrun, and no suite hook failed.
        /// A run with only skipped cases, or with no cases, is successful.
        /// </summary>
        public bool IsSuccess
        {
            get { return _Failed == 0 && _Canceled == 0 && _NotRun == 0 && _LifecycleErrors.Count == 0 && !_WasCanceled; }
        }

        private static int NonNegative(int value, string paramName)
        {
            if (value < 0) throw new ArgumentOutOfRangeException(paramName, value, "Count cannot be negative.");
            return value;
        }
    }
}
