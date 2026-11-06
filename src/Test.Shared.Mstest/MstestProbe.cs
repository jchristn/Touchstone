namespace Test.Shared.Mstest
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Runtime.Serialization.Json;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;

    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Test.Shared;
    using Touchstone.Core;
    using Touchstone.MstestAdapter;

    /// <summary>
    /// Adapter probe for Touchstone.MstestAdapter.
    /// </summary>
    public sealed class MstestProbe : IAdapterProbe
    {
        private readonly TestContext? _TestContext;

        /// <summary>
        /// Initialize the probe.
        /// </summary>
        /// <param name="testContext">The running test's context, which receives diagnostics, or null.</param>
        public MstestProbe(TestContext? testContext)
        {
            _TestContext = testContext;
        }

        /// <inheritdoc />
        public string Code
        {
            get { return "MS"; }
        }

        /// <inheritdoc />
        public bool NativeRowSkip
        {
            get { return true; }
        }

        /// <inheritdoc />
        public bool RowTraits
        {
            get { return true; }
        }

        /// <inheritdoc />
        public Version FrameworkVersion
        {
            get { return typeof(TestDataRow<>).Assembly.GetName().Version!; }
        }

        /// <inheritdoc />
        public Version MinimumVersion
        {
            get { return new Version(4, 4, 1); }
        }

        /// <inheritdoc />
        public IReadOnlyList<AdapterRow> Rows(AdapterFixtureSet set, TestRunOptions? options)
        {
            IReadOnlyList<TestDataRow<TouchstoneMstestCase>> rows;
            switch (set)
            {
                case AdapterFixtureSet.Standard: rows = TouchstoneMstestCases.From<StandardProvider>(options); break;
                case AdapterFixtureSet.Empty: rows = TouchstoneMstestCases.From<EmptyProvider>(options); break;
                case AdapterFixtureSet.EmptySuites: rows = TouchstoneMstestCases.From<EmptySuitesProvider>(options); break;
                case AdapterFixtureSet.Duplicate: rows = TouchstoneMstestCases.From<DuplicateProvider>(options); break;
                default: rows = TouchstoneMstestCases.From<CancelProvider>(options); break;
            }

            return rows.Select(row => new AdapterRow(row.Value.TestId, row.DisplayName ?? string.Empty, row.IgnoreMessage,
                row.TestCategories?.ToList() ?? new List<string>(), row.Value)).ToList();
        }

        /// <inheritdoc />
        public void ExpectNullSourceRejected()
        {
            TestAssert.Throws<InvalidOperationException>(() => TouchstoneMstestCases.From<NullProvider>(), "null suites");
        }

        /// <inheritdoc />
        public AdapterRow RoundTrip(AdapterRow row)
        {
            TouchstoneMstestCase key = Deserialize(Serialize((TouchstoneMstestCase)row.Native));
            return new AdapterRow(key.TestId, key.DisplayName, null, Array.Empty<string>(), key);
        }

        /// <inheritdoc />
        public AdapterRow? Unknown()
        {
            string json = Serialize((TouchstoneMstestCase)Rows(AdapterFixtureSet.Standard, null)[0].Native).Replace("AdpA.Pass", "AdpA.Gone", StringComparison.Ordinal);
            TouchstoneMstestCase key = Deserialize(json);
            return new AdapterRow(key.TestId, key.DisplayName, null, Array.Empty<string>(), key);
        }

        /// <inheritdoc />
        public async Task<string> ExecuteAsync(AdapterRow row, TestRunOptions? options, CancellationToken cancellationToken)
        {
            CapturingTestContext capture = new CapturingTestContext(_TestContext);
            await TouchstoneMstest.ExecuteAsync((TouchstoneMstestCase)row.Native, capture, cancellationToken).ConfigureAwait(false);
            return capture.Text;
        }

        /// <inheritdoc />
        public string? NativeSkipReason(Exception exception)
        {
            return exception is AssertInconclusiveException ? exception.Message : null;
        }

        private static string Serialize(TouchstoneMstestCase key)
        {
            DataContractJsonSerializer serializer = new DataContractJsonSerializer(typeof(TouchstoneMstestCase));
            using (MemoryStream stream = new MemoryStream())
            {
                serializer.WriteObject(stream, key);
                return Encoding.UTF8.GetString(stream.ToArray());
            }
        }

        private static TouchstoneMstestCase Deserialize(string json)
        {
            DataContractJsonSerializer serializer = new DataContractJsonSerializer(typeof(TouchstoneMstestCase));
            using (MemoryStream stream = new MemoryStream(Encoding.UTF8.GetBytes(json)))
            {
                return (TouchstoneMstestCase)serializer.ReadObject(stream)!;
            }
        }

        /// <inheritdoc />
        public Task RunAllAsync(IReadOnlyList<TestSuiteDescriptor> suites, TestRunOptions? options, CancellationToken cancellationToken)
        {
            return new FactHarness(suites, options).InvokeAsync(cancellationToken);
        }
    }
}
