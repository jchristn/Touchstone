namespace Test.Shared
{
    using System;
    using System.Collections.Generic;
    using System.Linq;

    using Touchstone.Core;

    /// <summary>
    /// Opt-in suites that let scripts/verify-hosts.py check host exit codes. Empty unless the
    /// <c>TOUCHSTONE_EXIT_PROBE</c> environment variable is set to <c>fail</c>, which adds one failing case.
    /// </summary>
    public static class ExitProbe
    {
        /// <summary>
        /// Name of the environment variable that enables the probe.
        /// </summary>
        public const string Variable = "TOUCHSTONE_EXIT_PROBE";

        /// <summary>
        /// The probe suites for the current environment.
        /// </summary>
        /// <returns>Zero or one suite.</returns>
        public static IEnumerable<TestSuiteDescriptor> Suites()
        {
            if (!string.Equals(Environment.GetEnvironmentVariable(Variable), "fail", StringComparison.OrdinalIgnoreCase))
                return Enumerable.Empty<TestSuiteDescriptor>();

            return new[] { Fixtures.Suite("ExitProbe", Fixtures.Failing("ExitProbe", "Fails", "exit code probe")) };
        }
    }
}
