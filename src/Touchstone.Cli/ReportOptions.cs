namespace Touchstone.Cli
{
    using System;

    /// <summary>
    /// Settings shared by the JSON and HTML reports.
    /// Thread safety: not thread-safe while being configured. Do not change an instance while a report is being written.
    /// </summary>
    public sealed class ReportOptions
    {
        private string _RunId = "touchstone";
        private Func<string, string> _Redact = text => text;

        /// <summary>
        /// Initialize options with the defaults.
        /// </summary>
        public ReportOptions()
        {
        }

        /// <summary>
        /// Name of the run shown in the HTML report. Default <c>touchstone</c>. Cannot be null or blank.
        /// </summary>
        /// <exception cref="ArgumentNullException">The value is null.</exception>
        /// <exception cref="ArgumentException">The value is blank.</exception>
        public string RunId
        {
            get { return _RunId; }
            set
            {
                if (value == null) throw new ArgumentNullException(nameof(value));
                if (string.IsNullOrWhiteSpace(value)) throw new ArgumentException("Run identity cannot be blank.", nameof(value));
                _RunId = value;
            }
        }

        /// <summary>
        /// When the run started, in UTC. Null means the time the report is written.
        /// </summary>
        public DateTimeOffset? StartedUtc { get; set; }

        /// <summary>
        /// Transforms every consumer-supplied string (names, messages, tags, identities) before it is written.
        /// Default: identity. Cannot be null.
        /// </summary>
        /// <exception cref="ArgumentNullException">The value is null.</exception>
        public Func<string, string> Redact
        {
            get { return _Redact; }
            set { _Redact = value ?? throw new ArgumentNullException(nameof(value)); }
        }
    }
}
