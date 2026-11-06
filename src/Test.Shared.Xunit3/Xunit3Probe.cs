namespace Test.Shared.Xunit3
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;

    using Test.Shared;
    using Touchstone.Core;
    using Touchstone.Xunit3Adapter;
    using Xunit;
    using Xunit.Sdk;
    using Xunit.v3;

    /// <summary>
    /// Adapter probe for Touchstone.Xunit3Adapter.
    /// </summary>
    public sealed class Xunit3Probe : IAdapterProbe
    {
        /// <inheritdoc />
        public string Code
        {
            get { return "X3"; }
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
            get { return typeof(TheoryData<>).Assembly.GetName().Version!; }
        }

        /// <inheritdoc />
        public Version MinimumVersion
        {
            get { return new Version(4, 0, 2); }
        }

        /// <inheritdoc />
        public IReadOnlyList<AdapterRow> Rows(AdapterFixtureSet set, TestRunOptions? options)
        {
            TheoryData<TouchstoneXunit3Case> data;
            switch (set)
            {
                case AdapterFixtureSet.Standard: data = TouchstoneXunit3Cases.From<StandardProvider>(options); break;
                case AdapterFixtureSet.Empty: data = TouchstoneXunit3Cases.From<EmptyProvider>(options); break;
                case AdapterFixtureSet.EmptySuites: data = TouchstoneXunit3Cases.From<EmptySuitesProvider>(options); break;
                case AdapterFixtureSet.Duplicate: data = TouchstoneXunit3Cases.From<DuplicateProvider>(options); break;
                default: data = TouchstoneXunit3Cases.From<CancelProvider>(options); break;
            }

            List<AdapterRow> rows = new List<AdapterRow>();
            foreach (ITheoryDataRow row in data)
            {
                TouchstoneXunit3Case key = (TouchstoneXunit3Case)row.GetData()[0]!;
                List<string> categories = row.Traits != null && row.Traits.TryGetValue(TouchstoneXunit3Cases.TagTraitName, out HashSet<string>? values)
                    ? values.ToList()
                    : new List<string>();
                rows.Add(new AdapterRow(key.TestId, row.TestDisplayName ?? key.ToString(), row.Skip, categories, key));
            }

            return rows;
        }

        /// <inheritdoc />
        public void ExpectNullSourceRejected()
        {
            TestAssert.Throws<InvalidOperationException>(() => TouchstoneXunit3Cases.From<NullProvider>(), "null suites");
        }

        /// <inheritdoc />
        public AdapterRow RoundTrip(AdapterRow row)
        {
            string serialized = SerializationHelper.Instance.Serialize(row.Native);
            TouchstoneXunit3Case key = (TouchstoneXunit3Case)SerializationHelper.Instance.Deserialize(serialized)!;
            return new AdapterRow(key.TestId, key.DisplayName, null, Array.Empty<string>(), key);
        }

        /// <inheritdoc />
        public AdapterRow? Unknown()
        {
            FakeSerializationInfo info = new FakeSerializationInfo();
            ((TouchstoneXunit3Case)Rows(AdapterFixtureSet.Standard, null)[0].Native).Serialize(info);
            info.AddValue("testId", "AdpA.Gone");
            TouchstoneXunit3Case key = new TouchstoneXunit3Case();
            key.Deserialize(info);
            return new AdapterRow(key.TestId, key.DisplayName, null, Array.Empty<string>(), key);
        }

        /// <inheritdoc />
        public async Task<string> ExecuteAsync(AdapterRow row, TestRunOptions? options, CancellationToken cancellationToken)
        {
            ITestOutputHelper? output = TestContext.Current.TestOutputHelper;
            string before = output?.Output ?? string.Empty;
            await TouchstoneXunit3.ExecuteAsync((TouchstoneXunit3Case)row.Native, cancellationToken).ConfigureAwait(false);
            string after = output?.Output ?? string.Empty;
            return after.Length >= before.Length ? after.Substring(before.Length) : after;
        }

        /// <inheritdoc />
        public string? NativeSkipReason(Exception exception)
        {
            return exception.Message.StartsWith(DynamicSkipToken.Value, StringComparison.Ordinal)
                ? exception.Message.Substring(DynamicSkipToken.Value.Length)
                : null;
        }

        /// <inheritdoc />
        public Task RunAllAsync(IReadOnlyList<TestSuiteDescriptor> suites, TestRunOptions? options, CancellationToken cancellationToken)
        {
            return new FactHarness(suites, options).InvokeAsync(cancellationToken);
        }
    }
}
