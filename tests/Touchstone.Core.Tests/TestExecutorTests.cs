namespace Touchstone.Core.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;

    using Touchstone.Core;
    using global::Xunit;

    public sealed class TestExecutorTests
    {
        [Fact]
        public async Task RunSuiteAsync_PassingTest_ReturnsSuccess()
        {
            TestSuiteDescriptor suite = new TestSuiteDescriptor(
                suiteId: "S1",
                displayName: "Suite",
                cases: new List<TestCaseDescriptor>
                {
                    new TestCaseDescriptor(
                        suiteId: "S1",
                        caseId: "C1",
                        displayName: "Passing",
                        executeAsync: _ => Task.CompletedTask)
                });

            TestRunSummary summary = await TestExecutor.RunSuiteAsync(suite);

            Assert.True(summary.IsSuccess);
            Assert.Equal(1, summary.Total);
            Assert.Equal(1, summary.Passed);
            Assert.Equal(0, summary.Failed);
            Assert.Equal(0, summary.Skipped);
        }

        [Fact]
        public async Task RunSuiteAsync_FailingTest_CapturesException()
        {
            TestSuiteDescriptor suite = new TestSuiteDescriptor(
                suiteId: "S1",
                displayName: "Suite",
                cases: new List<TestCaseDescriptor>
                {
                    new TestCaseDescriptor(
                        suiteId: "S1",
                        caseId: "C1",
                        displayName: "Failing",
                        executeAsync: _ => throw new InvalidOperationException("boom"))
                });

            TestRunSummary summary = await TestExecutor.RunSuiteAsync(suite);

            Assert.False(summary.IsSuccess);
            Assert.Equal(1, summary.Failed);
        }

        [Fact]
        public async Task RunSuiteAsync_SkippedTest_CountsAsSkipped()
        {
            TestSuiteDescriptor suite = new TestSuiteDescriptor(
                suiteId: "S1",
                displayName: "Suite",
                cases: new List<TestCaseDescriptor>
                {
                    new TestCaseDescriptor(
                        suiteId: "S1",
                        caseId: "C1",
                        displayName: "Skipped",
                        skip: true,
                        skipReason: "Not ready",
                        executeAsync: _ => Task.CompletedTask)
                });

            TestRunSummary summary = await TestExecutor.RunSuiteAsync(suite);

            Assert.True(summary.IsSuccess);
            Assert.Equal(1, summary.Skipped);
            Assert.Equal(0, summary.Passed);
        }

        [Fact]
        public async Task RunSuiteAsync_WithSink_InvokesSinkMethods()
        {
            bool suiteStarted = false;
            bool testCompleted = false;
            bool suiteCompleted = false;

            RecordingSink sink = new RecordingSink(
                onSuiteStarted: () => suiteStarted = true,
                onTestCompleted: () => testCompleted = true,
                onSuiteCompleted: () => suiteCompleted = true);

            TestSuiteDescriptor suite = new TestSuiteDescriptor(
                suiteId: "S1",
                displayName: "Suite",
                cases: new List<TestCaseDescriptor>
                {
                    new TestCaseDescriptor(
                        suiteId: "S1",
                        caseId: "C1",
                        displayName: "Test",
                        executeAsync: _ => Task.CompletedTask)
                });

            await TestExecutor.RunSuiteAsync(suite, sink);

            Assert.True(suiteStarted);
            Assert.True(testCompleted);
            Assert.True(suiteCompleted);
        }

        [Fact]
        public async Task RunSuiteAsync_WithNoSink_Succeeds()
        {
            TestSuiteDescriptor suite = new TestSuiteDescriptor(
                suiteId: "S1",
                displayName: "Suite",
                cases: new List<TestCaseDescriptor>
                {
                    new TestCaseDescriptor(
                        suiteId: "S1",
                        caseId: "C1",
                        displayName: "Test",
                        executeAsync: _ => Task.CompletedTask)
                });

            TestRunSummary summary = await TestExecutor.RunSuiteAsync(suite);

            Assert.Equal(1, summary.Passed);
        }

        [Fact]
        public async Task RunSuiteAsync_BeforeAndAfterHooks_AreCalled()
        {
            bool beforeCalled = false;
            bool afterCalled = false;

            TestSuiteDescriptor suite = new TestSuiteDescriptor(
                suiteId: "S1",
                displayName: "Suite",
                cases: new List<TestCaseDescriptor>
                {
                    new TestCaseDescriptor(
                        suiteId: "S1",
                        caseId: "C1",
                        displayName: "Test",
                        executeAsync: _ => Task.CompletedTask)
                },
                beforeSuiteAsync: _ => { beforeCalled = true; return ValueTask.CompletedTask; },
                afterSuiteAsync: _ => { afterCalled = true; return ValueTask.CompletedTask; });

            await TestExecutor.RunSuiteAsync(suite);

            Assert.True(beforeCalled);
            Assert.True(afterCalled);
        }

        [Fact]
        public async Task RunSuiteAsync_FailingTest_CapturesDurationAndMessage()
        {
            RecordingSink sink = new RecordingSink();

            TestSuiteDescriptor suite = new TestSuiteDescriptor(
                suiteId: "S1",
                displayName: "Suite",
                cases: new List<TestCaseDescriptor>
                {
                    new TestCaseDescriptor(
                        suiteId: "S1",
                        caseId: "C1",
                        displayName: "Failing",
                        executeAsync: _ => throw new InvalidOperationException("detailed error"))
                });

            await TestExecutor.RunSuiteAsync(suite, sink);

            Assert.Single(sink.Results);

            TestResult result = sink.Results[0];
            Assert.False(result.Success);
            Assert.Equal("detailed error", result.Message);
            Assert.IsType<InvalidOperationException>(result.Exception);
            Assert.True(result.Duration >= TimeSpan.Zero);
        }

        [Fact]
        public void TestCaseDescriptor_TestId_IsComposite()
        {
            TestCaseDescriptor descriptor = new TestCaseDescriptor(
                suiteId: "MySuite",
                caseId: "MyCase",
                displayName: "Test",
                executeAsync: _ => Task.CompletedTask);

            Assert.Equal("MySuite.MyCase", descriptor.TestId);
        }

        [Fact]
        public void TestCaseDescriptor_ToString_ReturnsDisplayName()
        {
            TestCaseDescriptor descriptor = new TestCaseDescriptor(
                suiteId: "S",
                caseId: "C",
                displayName: "My Display Name",
                executeAsync: _ => Task.CompletedTask);

            Assert.Equal("My Display Name", descriptor.ToString());
        }

        [Fact]
        public void TestRunSummary_NegativeValues_ClampedToZero()
        {
            TestRunSummary summary = new TestRunSummary
            {
                Total = -5,
                Passed = -1,
                Failed = -1,
                Skipped = -1,
                Duration = TimeSpan.FromMilliseconds(-100),
            };

            Assert.Equal(0, summary.Total);
            Assert.Equal(0, summary.Passed);
            Assert.Equal(0, summary.Failed);
            Assert.Equal(0, summary.Skipped);
            Assert.Equal(TimeSpan.Zero, summary.Duration);
        }

        [Fact]
        public void TestResult_NegativeDuration_ClampedToZero()
        {
            TestResult result = new TestResult
            {
                TestId = "S.C",
                SuiteId = "S",
                CaseId = "C",
                DisplayName = "Test",
                Success = true,
                Duration = TimeSpan.FromMilliseconds(-50),
            };

            Assert.Equal(TimeSpan.Zero, result.Duration);
        }

        private sealed class RecordingSink : ITestResultSink
        {
            private readonly Action _OnSuiteStarted;
            private readonly Action _OnTestCompleted;
            private readonly Action _OnSuiteCompleted;

            public List<TestResult> Results { get; } = new List<TestResult>();

            public RecordingSink(
                Action onSuiteStarted = null,
                Action onTestCompleted = null,
                Action onSuiteCompleted = null)
            {
                _OnSuiteStarted = onSuiteStarted;
                _OnTestCompleted = onTestCompleted;
                _OnSuiteCompleted = onSuiteCompleted;
            }

            public ValueTask OnSuiteStartedAsync(TestSuiteDescriptor suite, CancellationToken cancellationToken)
            {
                _OnSuiteStarted?.Invoke();
                return ValueTask.CompletedTask;
            }

            public ValueTask OnTestCompletedAsync(TestResult result, CancellationToken cancellationToken)
            {
                Results.Add(result);
                _OnTestCompleted?.Invoke();
                return ValueTask.CompletedTask;
            }

            public ValueTask OnSuiteCompletedAsync(TestSuiteDescriptor suite, TestRunSummary summary, CancellationToken cancellationToken)
            {
                _OnSuiteCompleted?.Invoke();
                return ValueTask.CompletedTask;
            }
        }
    }
}
