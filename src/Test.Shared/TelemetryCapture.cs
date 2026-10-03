namespace Test.Shared
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Diagnostics.Metrics;
    using System.Linq;
    using Touchstone.Core;

    /// <summary>
    /// In-memory listener for the Touchstone meter and activity source. On construction it clears
    /// <see cref="Activity.Current"/> for the calling flow and starts a private root activity, so everything Touchstone emits
    /// underneath shares one trace id. Queries filter on that trace id, which keeps the capture isolated from other test hosts
    /// running Touchstone concurrently in the same process.
    /// </summary>
    public sealed class TelemetryCapture : IDisposable
    {
        /// <summary>
        /// Name of the activity source that owns the capture's root activity.
        /// </summary>
        public const string RootSourceName = "Test.Shared.TelemetryCapture";

        private static readonly ActivitySource _RootSource = new ActivitySource(RootSourceName);

        private readonly object _Lock = new object();
        private readonly List<CapturedMeasurement> _Measurements = new List<CapturedMeasurement>();
        private readonly List<Activity> _Stopped = new List<Activity>();
        private readonly MeterListener _MeterListener;
        private readonly ActivityListener _ActivityListener;
        private readonly Activity _Root;

        /// <summary>
        /// Start listening and open the capture's root activity.
        /// </summary>
        public TelemetryCapture()
        {
            _ActivityListener = new ActivityListener
            {
                ShouldListenTo = s => s.Name == TouchstoneTelemetry.ActivitySourceName || s.Name == RootSourceName,
                Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded,
                ActivityStopped = a =>
                {
                    lock (_Lock) _Stopped.Add(a);
                },
            };
            ActivitySource.AddActivityListener(_ActivityListener);

            _MeterListener = new MeterListener
            {
                InstrumentPublished = (instrument, listener) =>
                {
                    if (instrument.Meter.Name == TouchstoneTelemetry.MeterName)
                        listener.EnableMeasurementEvents(instrument);
                },
            };
            _MeterListener.SetMeasurementEventCallback<long>((i, v, t, _) => Record(i, v, t));
            _MeterListener.SetMeasurementEventCallback<double>((i, v, t, _) => Record(i, v, t));
            _MeterListener.Start();

            Activity.Current = null;
            _Root = _RootSource.StartActivity("telemetry-capture")
                ?? throw new InvalidOperationException("Capture root activity was not created");
        }

        /// <summary>
        /// Trace id shared by everything emitted under this capture.
        /// </summary>
        public ActivityTraceId TraceId
        {
            get { return _Root.TraceId; }
        }

        /// <summary>
        /// The capture's root activity.
        /// </summary>
        public Activity Root
        {
            get { return _Root; }
        }

        /// <summary>
        /// Measurements recorded inside this capture's trace for the named instrument.
        /// </summary>
        /// <param name="instrument">Instrument name.</param>
        /// <returns>Matching measurements.</returns>
        public List<CapturedMeasurement> Measurements(string instrument)
        {
            lock (_Lock)
                return _Measurements.Where(m => m.Instrument == instrument && m.TraceId == TraceId).ToList();
        }

        /// <summary>
        /// Collect observable instruments now and return the named instrument's values (observable values carry no trace).
        /// </summary>
        /// <param name="instrument">Instrument name.</param>
        /// <returns>Matching measurements.</returns>
        public List<CapturedMeasurement> Observe(string instrument)
        {
            Activity? saved = Activity.Current;
            Activity.Current = null;
            try
            {
                _MeterListener.RecordObservableInstruments();
            }
            finally
            {
                Activity.Current = saved;
            }

            lock (_Lock)
                return _Measurements.Where(m => m.Instrument == instrument && m.TraceId == default).ToList();
        }

        /// <summary>
        /// Touchstone spans stopped inside this capture's trace.
        /// </summary>
        /// <returns>Stopped spans, in stop order.</returns>
        public List<Activity> Spans()
        {
            lock (_Lock)
                return _Stopped.Where(a => a.Source.Name == TouchstoneTelemetry.ActivitySourceName && a.TraceId == TraceId).ToList();
        }

        /// <summary>
        /// The single Touchstone span with the given name inside this capture's trace.
        /// </summary>
        /// <param name="name">Span display name.</param>
        /// <returns>The span.</returns>
        public Activity Span(string name)
        {
            List<Activity> matches = Spans().Where(a => a.DisplayName == name).ToList();
            TestAssert.Equal(1, matches.Count, "span count for " + name);
            return matches[0];
        }

        /// <summary>
        /// Stop listening and close the root activity.
        /// </summary>
        public void Dispose()
        {
            _Root.Dispose();
            _MeterListener.Dispose();
            _ActivityListener.Dispose();
        }

        private void Record<T>(Instrument instrument, T value, ReadOnlySpan<KeyValuePair<string, object?>> tags)
        {
            Dictionary<string, object?> copy = new Dictionary<string, object?>(StringComparer.Ordinal);
            foreach (KeyValuePair<string, object?> tag in tags)
                copy[tag.Key] = tag.Value;

            ActivityTraceId traceId = Activity.Current?.TraceId ?? default;
            CapturedMeasurement measurement = new CapturedMeasurement(
                instrument.Name, instrument.Unit, Convert.ToDouble(value), copy, traceId);

            lock (_Lock) _Measurements.Add(measurement);
        }
    }
}
