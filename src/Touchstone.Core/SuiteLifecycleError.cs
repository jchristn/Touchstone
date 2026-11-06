namespace Touchstone.Core
{
    using System;

    /// <summary>
    /// Immutable record of a failed suite lifecycle hook.
    /// Thread safety: safe to read concurrently.
    /// </summary>
    public sealed class SuiteLifecycleError
    {
        private readonly string _SuiteId;
        private readonly string _Stage;
        private readonly Exception _Exception;

        /// <summary>
        /// Initialize a new lifecycle error.
        /// </summary>
        /// <param name="suiteId">Identifier of the suite whose hook failed.</param>
        /// <param name="stage"><see cref="TouchstoneTelemetry.StageBeforeSuite"/> or <see cref="TouchstoneTelemetry.StageAfterSuite"/>.</param>
        /// <param name="exception">The exception the hook threw.</param>
        /// <exception cref="ArgumentNullException">An argument is null.</exception>
        /// <exception cref="ArgumentException">The stage is not a lifecycle stage.</exception>
        public SuiteLifecycleError(string suiteId, string stage, Exception exception)
        {
            _SuiteId = suiteId ?? throw new ArgumentNullException(nameof(suiteId));
            if (stage == null) throw new ArgumentNullException(nameof(stage));
            if (stage != TouchstoneTelemetry.StageBeforeSuite && stage != TouchstoneTelemetry.StageAfterSuite)
                throw new ArgumentException("Stage must be before_suite or after_suite.", nameof(stage));
            _Stage = stage;
            _Exception = exception ?? throw new ArgumentNullException(nameof(exception));
        }

        /// <summary>
        /// Identifier of the suite whose hook failed. Never null.
        /// </summary>
        public string SuiteId
        {
            get { return _SuiteId; }
        }

        /// <summary>
        /// <c>before_suite</c> or <c>after_suite</c>.
        /// </summary>
        public string Stage
        {
            get { return _Stage; }
        }

        /// <summary>
        /// The exception the hook threw. Never null.
        /// </summary>
        public Exception Exception
        {
            get { return _Exception; }
        }

        /// <summary>
        /// Returns the suite, stage, and message.
        /// </summary>
        /// <returns>A short description of the failure.</returns>
        public override string ToString()
        {
            return _SuiteId + " " + _Stage + ": " + _Exception.Message;
        }
    }
}
