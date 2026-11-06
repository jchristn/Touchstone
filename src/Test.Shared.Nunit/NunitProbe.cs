namespace Test.Shared.Nunit
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;

    using NUnit.Framework;
    using NUnit.Framework.Interfaces;
    using NUnit.Framework.Internal;
    using Test.Shared;
    using Touchstone.Core;
    using Touchstone.NunitAdapter;

    /// <summary>
    /// Adapter probe for Touchstone.NunitAdapter.
    /// </summary>
    public sealed class NunitProbe : IAdapterProbe
    {
        /// <inheritdoc />
        public string Code
        {
            get { return "NU"; }
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
            get { return typeof(TestCaseData).Assembly.GetName().Version!; }
        }

        /// <inheritdoc />
        public Version MinimumVersion
        {
            get { return new Version(5, 0, 0); }
        }

        /// <inheritdoc />
        public IReadOnlyList<AdapterRow> Rows(AdapterFixtureSet set, TestRunOptions? options)
        {
            List<AdapterRow> rows = new List<AdapterRow>();
            foreach (TestCaseData entry in TouchstoneNunitCases.From(AdapterFixtures.Suites(set), options))
            {
                TestCaseDescriptor testCase = (TestCaseDescriptor)entry.Arguments[0]!;
                string? skip = entry.RunState == RunState.Ignored ? entry.Properties.Get(PropertyNames.SkipReason) as string : null;
                List<string> categories = entry.Properties[PropertyNames.Category].Cast<string>().ToList();
                rows.Add(new AdapterRow(testCase.TestId, entry.TestName ?? string.Empty, skip, categories, testCase));
            }

            return rows;
        }

        /// <inheritdoc />
        public void ExpectNullSourceRejected()
        {
            TestAssert.Throws<ArgumentNullException>(() => TouchstoneNunitCases.From(null!), "null suites");
        }

        /// <inheritdoc />
        public AdapterRow RoundTrip(AdapterRow row)
        {
            return row;
        }

        /// <inheritdoc />
        public AdapterRow? Unknown()
        {
            return null;
        }

        /// <inheritdoc />
        public async Task<string> ExecuteAsync(AdapterRow row, TestRunOptions? options, CancellationToken cancellationToken)
        {
            using (StringWriter output = new StringWriter())
            {
                await TouchstoneNunit.ExecuteAsync((TestCaseDescriptor)row.Native, options, output, cancellationToken).ConfigureAwait(false);
                return output.ToString();
            }
        }

        /// <inheritdoc />
        public string? NativeSkipReason(Exception exception)
        {
            return exception is IgnoreException ? exception.Message : null;
        }

        /// <inheritdoc />
        public Task RunAllAsync(IReadOnlyList<TestSuiteDescriptor> suites, TestRunOptions? options, CancellationToken cancellationToken)
        {
            return new FactHarness(suites, options).InvokeAsync(cancellationToken);
        }
    }
}
