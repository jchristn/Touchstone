namespace Test.Shared.Xunit2
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;

    using Test.Shared;
    using Touchstone.Core;
    using Touchstone.Xunit2Adapter;
    using Xunit;
    using Xunit.Sdk;

    /// <summary>
    /// Adapter probe for Touchstone.Xunit2Adapter.
    /// </summary>
    public sealed class Xunit2Probe : IAdapterProbe
    {
        /// <inheritdoc />
        public string Code
        {
            get { return "X2"; }
        }

        /// <inheritdoc />
        public bool NativeRowSkip
        {
            get { return false; }
        }

        /// <inheritdoc />
        public bool RowTraits
        {
            get { return false; }
        }

        /// <inheritdoc />
        public Version FrameworkVersion
        {
            get { return typeof(TheoryData<>).Assembly.GetName().Version!; }
        }

        /// <inheritdoc />
        public Version MinimumVersion
        {
            get { return new Version(2, 9, 3); }
        }

        /// <inheritdoc />
        public IReadOnlyList<AdapterRow> Rows(AdapterFixtureSet set, TestRunOptions? options)
        {
            TheoryData<TouchstoneXunit2Case> data;
            switch (set)
            {
                case AdapterFixtureSet.Standard: data = TouchstoneXunit2Cases.From<StandardProvider>(options); break;
                case AdapterFixtureSet.Empty: data = TouchstoneXunit2Cases.From<EmptyProvider>(options); break;
                case AdapterFixtureSet.EmptySuites: data = TouchstoneXunit2Cases.From<EmptySuitesProvider>(options); break;
                case AdapterFixtureSet.Duplicate: data = TouchstoneXunit2Cases.From<DuplicateProvider>(options); break;
                default: data = TouchstoneXunit2Cases.From<CancelProvider>(options); break;
            }

            List<AdapterRow> rows = new List<AdapterRow>();
            foreach (TouchstoneXunit2Case key in data)
            {
                rows.Add(new AdapterRow(key.TestId, key.ToString(), null, Array.Empty<string>(), key));
            }

            return rows;
        }

        /// <inheritdoc />
        public void ExpectNullSourceRejected()
        {
            TestAssert.Throws<InvalidOperationException>(() => TouchstoneXunit2Cases.From<NullProvider>(), "null suites");
        }

        /// <inheritdoc />
        public AdapterRow RoundTrip(AdapterRow row)
        {
            string serialized = SerializationHelper.Serialize(row.Native);
            TouchstoneXunit2Case key = SerializationHelper.Deserialize<TouchstoneXunit2Case>(serialized);
            return new AdapterRow(key.TestId, key.DisplayName, null, Array.Empty<string>(), key);
        }

        /// <inheritdoc />
        public AdapterRow? Unknown()
        {
            FakeSerializationInfo info = new FakeSerializationInfo();
            ((TouchstoneXunit2Case)Rows(AdapterFixtureSet.Standard, null)[0].Native).Serialize(info);
            info.AddValue("testId", "AdpA.Gone");
            TouchstoneXunit2Case key = new TouchstoneXunit2Case();
            key.Deserialize(info);
            return new AdapterRow(key.TestId, key.DisplayName, null, Array.Empty<string>(), key);
        }

        /// <inheritdoc />
        public async Task<string> ExecuteAsync(AdapterRow row, TestRunOptions? options, CancellationToken cancellationToken)
        {
            CapturingOutput output = new CapturingOutput();
            await TouchstoneXunit2.ExecuteAsync((TouchstoneXunit2Case)row.Native, output, cancellationToken).ConfigureAwait(false);
            return output.Text;
        }

        /// <inheritdoc />
        public string? NativeSkipReason(Exception exception)
        {
            return null;
        }

        /// <inheritdoc />
        public Task RunAllAsync(IReadOnlyList<TestSuiteDescriptor> suites, TestRunOptions? options, CancellationToken cancellationToken)
        {
            return new FactHarness(suites, options).InvokeAsync(cancellationToken);
        }
    }
}
