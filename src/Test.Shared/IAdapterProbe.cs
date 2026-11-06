namespace Test.Shared
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;

    using Touchstone.Core;

    /// <summary>
    /// Drives one framework adapter for the shared adapter contract. Each Test.Shared.{Adapter} project implements it.
    /// </summary>
    public interface IAdapterProbe
    {
        /// <summary>
        /// Short code used as the case identity suffix: X2, X3, NU, or MS.
        /// </summary>
        string Code { get; }

        /// <summary>
        /// True when the framework supports a native row-level skip.
        /// </summary>
        bool NativeRowSkip { get; }

        /// <summary>
        /// True when rows carry tags as native traits or categories.
        /// </summary>
        bool RowTraits { get; }

        /// <summary>
        /// Resolved framework version.
        /// </summary>
        Version FrameworkVersion { get; }

        /// <summary>
        /// Documented minimum framework version.
        /// </summary>
        Version MinimumVersion { get; }

        /// <summary>
        /// Create native rows for a fixture set.
        /// </summary>
        /// <param name="set">Fixture set.</param>
        /// <param name="options">Options passed to the data source.</param>
        /// <returns>Row views, in order.</returns>
        IReadOnlyList<AdapterRow> Rows(AdapterFixtureSet set, TestRunOptions? options);

        /// <summary>
        /// Assert that a null or missing suite source is rejected.
        /// </summary>
        void ExpectNullSourceRejected();

        /// <summary>
        /// Serialize and deserialize a row the way the framework does between discovery and execution.
        /// </summary>
        /// <param name="row">Row to round-trip.</param>
        /// <returns>The deserialized row.</returns>
        AdapterRow RoundTrip(AdapterRow row);

        /// <summary>
        /// A row whose test identity is not supplied by any suite, or null when rows carry the case itself.
        /// </summary>
        /// <returns>The row, or null.</returns>
        AdapterRow? Unknown();

        /// <summary>
        /// Execute a row through the adapter's execute helper.
        /// </summary>
        /// <param name="row">Row to execute.</param>
        /// <param name="options">Options the rows were created with.</param>
        /// <param name="cancellationToken">Caller cancellation.</param>
        /// <returns>Diagnostics the adapter wrote, or an empty string.</returns>
        Task<string> ExecuteAsync(AdapterRow row, TestRunOptions? options, CancellationToken cancellationToken);

        /// <summary>
        /// Decide whether an exception is the framework's native skip signal.
        /// </summary>
        /// <param name="exception">Exception thrown by the execute helper.</param>
        /// <returns>The skip reason, or null when the exception is not a skip.</returns>
        string? NativeSkipReason(Exception exception);

        /// <summary>
        /// Run suites through the adapter's run-all base class.
        /// </summary>
        /// <param name="suites">Suites.</param>
        /// <param name="options">Options.</param>
        /// <param name="cancellationToken">Caller cancellation.</param>
        /// <returns>A task that completes when the base class returns.</returns>
        Task RunAllAsync(IReadOnlyList<TestSuiteDescriptor> suites, TestRunOptions? options, CancellationToken cancellationToken);
    }
}
