namespace Test.Shared
{
    using System.Collections.Generic;
    using System.Diagnostics;

    /// <summary>
    /// One measurement captured by <see cref="TelemetryCapture"/>.
    /// </summary>
    public sealed class CapturedMeasurement
    {
        /// <summary>
        /// Initialize a captured measurement.
        /// </summary>
        /// <param name="instrument">Instrument name.</param>
        /// <param name="unit">Instrument unit.</param>
        /// <param name="value">Recorded value.</param>
        /// <param name="tags">Measurement tags.</param>
        /// <param name="traceId">Trace of <see cref="Activity.Current"/> when recorded, or default when none.</param>
        public CapturedMeasurement(string instrument, string? unit, double value, Dictionary<string, object?> tags, ActivityTraceId traceId)
        {
            Instrument = instrument;
            Unit = unit;
            Value = value;
            Tags = tags;
            TraceId = traceId;
        }

        /// <summary>
        /// Instrument name.
        /// </summary>
        public string Instrument { get; }

        /// <summary>
        /// Instrument unit.
        /// </summary>
        public string? Unit { get; }

        /// <summary>
        /// Recorded value.
        /// </summary>
        public double Value { get; }

        /// <summary>
        /// Measurement tags.
        /// </summary>
        public Dictionary<string, object?> Tags { get; }

        /// <summary>
        /// Trace of the activity that was current when the measurement was recorded.
        /// </summary>
        public ActivityTraceId TraceId { get; }

        /// <summary>
        /// Return a tag value as a string, or null when absent.
        /// </summary>
        /// <param name="key">Tag key.</param>
        /// <returns>Tag value or null.</returns>
        public string? Tag(string key)
        {
            return Tags.TryGetValue(key, out object? value) ? value?.ToString() : null;
        }
    }
}
