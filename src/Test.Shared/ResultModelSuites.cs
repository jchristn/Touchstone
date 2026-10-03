namespace Test.Shared
{
    using System;
    using System.Collections.Generic;
    using Touchstone.Cli;
    using Touchstone.Core;

    /// <summary>
    /// Suites covering the result model types: <see cref="TestResult"/>, <see cref="TestRunSummary"/>, and <see cref="JsonResultEntry"/>.
    /// </summary>
    public static class ResultModelSuites
    {
        /// <summary>
        /// Suite covering <see cref="TestResult"/>.
        /// </summary>
        /// <returns>Suite descriptor.</returns>
        public static TestSuiteDescriptor TestResultSuite()
        {
            const string S = "TestResult";

            return new TestSuiteDescriptor(S, "TestResult", new List<TestCaseDescriptor>
            {
                CaseBuilder.Sync(S, "Defaults", "TestResult defaults to empty identity, not successful, zero duration", () =>
                {
                    TestResult r = new TestResult();
                    TestAssert.Equal(string.Empty, r.TestId, "TestId");
                    TestAssert.Equal(string.Empty, r.SuiteId, "SuiteId");
                    TestAssert.Equal(string.Empty, r.CaseId, "CaseId");
                    TestAssert.Equal(string.Empty, r.DisplayName, "DisplayName");
                    TestAssert.False(r.Success, "Success");
                    TestAssert.False(r.Skipped, "Skipped");
                    TestAssert.Equal(TimeSpan.Zero, r.Duration, "Duration");
                    TestAssert.Null(r.Exception, "Exception");
                    TestAssert.Null(r.Message, "Message");
                }),

                CaseBuilder.Sync(S, "StoresValues", "TestResult stores assigned values", () =>
                {
                    InvalidOperationException ex = new InvalidOperationException("boom");
                    TestResult r = new TestResult
                    {
                        TestId = "S.C",
                        SuiteId = "S",
                        CaseId = "C",
                        DisplayName = "Display",
                        Success = true,
                        Skipped = true,
                        Duration = TimeSpan.FromMilliseconds(42),
                        Exception = ex,
                        Message = "msg",
                    };
                    TestAssert.Equal("S.C", r.TestId, "TestId");
                    TestAssert.Equal("S", r.SuiteId, "SuiteId");
                    TestAssert.Equal("C", r.CaseId, "CaseId");
                    TestAssert.Equal("Display", r.DisplayName, "DisplayName");
                    TestAssert.True(r.Success, "Success");
                    TestAssert.True(r.Skipped, "Skipped");
                    TestAssert.Equal(TimeSpan.FromMilliseconds(42), r.Duration, "Duration");
                    TestAssert.Same(ex, r.Exception, "Exception");
                    TestAssert.Equal("msg", r.Message, "Message");
                }),

                CaseBuilder.Sync(S, "SettersRejectNull", "TestResult identity setters reject null", () =>
                {
                    TestResult r = new TestResult();
                    TestAssert.Throws<ArgumentNullException>(() => r.TestId = null!, "TestId setter");
                    TestAssert.Throws<ArgumentNullException>(() => r.SuiteId = null!, "SuiteId setter");
                    TestAssert.Throws<ArgumentNullException>(() => r.CaseId = null!, "CaseId setter");
                    TestAssert.Throws<ArgumentNullException>(() => r.DisplayName = null!, "DisplayName setter");
                }),

                CaseBuilder.Sync(S, "OptionalFieldsAcceptNull", "TestResult Exception and Message accept null", () =>
                {
                    TestResult r = new TestResult { Exception = new InvalidOperationException(), Message = "m" };
                    r.Exception = null;
                    r.Message = null;
                    TestAssert.Null(r.Exception, "Exception");
                    TestAssert.Null(r.Message, "Message");
                }),

                CaseBuilder.Sync(S, "NegativeDurationClamped", "TestResult negative duration is clamped to zero", () =>
                {
                    TestResult r = new TestResult { Duration = TimeSpan.FromMilliseconds(-50) };
                    TestAssert.Equal(TimeSpan.Zero, r.Duration, "Duration");
                    r.Duration = TimeSpan.MinValue;
                    TestAssert.Equal(TimeSpan.Zero, r.Duration, "Duration at MinValue");
                }),
            });
        }

        /// <summary>
        /// Suite covering <see cref="TestRunSummary"/>.
        /// </summary>
        /// <returns>Suite descriptor.</returns>
        public static TestSuiteDescriptor TestRunSummarySuite()
        {
            const string S = "TestRunSummary";

            return new TestSuiteDescriptor(S, "TestRunSummary", new List<TestCaseDescriptor>
            {
                CaseBuilder.Sync(S, "DefaultsAreZeroAndSuccessful", "TestRunSummary defaults to zero counts and IsSuccess", () =>
                {
                    TestRunSummary s = new TestRunSummary();
                    TestAssert.Equal(0, s.Total, "Total");
                    TestAssert.Equal(0, s.Passed, "Passed");
                    TestAssert.Equal(0, s.Failed, "Failed");
                    TestAssert.Equal(0, s.Skipped, "Skipped");
                    TestAssert.Equal(TimeSpan.Zero, s.Duration, "Duration");
                    TestAssert.True(s.IsSuccess, "IsSuccess");
                }),

                CaseBuilder.Sync(S, "StoresPositiveValues", "TestRunSummary stores positive counts and duration", () =>
                {
                    TestRunSummary s = new TestRunSummary
                    {
                        Total = 10,
                        Passed = 7,
                        Failed = 2,
                        Skipped = 1,
                        Duration = TimeSpan.FromSeconds(3),
                    };
                    TestAssert.Equal(10, s.Total, "Total");
                    TestAssert.Equal(7, s.Passed, "Passed");
                    TestAssert.Equal(2, s.Failed, "Failed");
                    TestAssert.Equal(1, s.Skipped, "Skipped");
                    TestAssert.Equal(TimeSpan.FromSeconds(3), s.Duration, "Duration");
                }),

                CaseBuilder.Sync(S, "NegativeValuesClamped", "TestRunSummary negative counts and duration are clamped to zero", () =>
                {
                    TestRunSummary s = new TestRunSummary
                    {
                        Total = -5,
                        Passed = -1,
                        Failed = -1,
                        Skipped = -1,
                        Duration = TimeSpan.FromMilliseconds(-100),
                    };
                    TestAssert.Equal(0, s.Total, "Total");
                    TestAssert.Equal(0, s.Passed, "Passed");
                    TestAssert.Equal(0, s.Failed, "Failed");
                    TestAssert.Equal(0, s.Skipped, "Skipped");
                    TestAssert.Equal(TimeSpan.Zero, s.Duration, "Duration");
                }),

                CaseBuilder.Sync(S, "IsSuccessFalseWhenFailed", "TestRunSummary IsSuccess is false when any test failed", () =>
                {
                    TestRunSummary s = new TestRunSummary { Total = 3, Passed = 2, Failed = 1 };
                    TestAssert.False(s.IsSuccess, "IsSuccess");
                }),

                CaseBuilder.Sync(S, "IsSuccessTrueWhenOnlySkipped", "TestRunSummary IsSuccess is true when tests were only skipped", () =>
                {
                    TestRunSummary s = new TestRunSummary { Total = 2, Skipped = 2 };
                    TestAssert.True(s.IsSuccess, "IsSuccess");
                }),
            });
        }

        /// <summary>
        /// Suite covering <see cref="JsonResultEntry"/>.
        /// </summary>
        /// <returns>Suite descriptor.</returns>
        public static TestSuiteDescriptor JsonResultEntrySuite()
        {
            const string S = "JsonResultEntry";

            return new TestSuiteDescriptor(S, "JsonResultEntry", new List<TestCaseDescriptor>
            {
                CaseBuilder.Sync(S, "Defaults", "JsonResultEntry defaults to empty identity and null message", () =>
                {
                    JsonResultEntry e = new JsonResultEntry();
                    TestAssert.Equal(string.Empty, e.TestId, "TestId");
                    TestAssert.Equal(string.Empty, e.SuiteId, "SuiteId");
                    TestAssert.Equal(string.Empty, e.CaseId, "CaseId");
                    TestAssert.Equal(string.Empty, e.DisplayName, "DisplayName");
                    TestAssert.False(e.Success, "Success");
                    TestAssert.False(e.Skipped, "Skipped");
                    TestAssert.Equal(0L, e.DurationMs, "DurationMs");
                    TestAssert.Null(e.Message, "Message");
                }),
            });
        }
    }
}
