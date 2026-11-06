namespace Touchstone.Core
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Runtime.ExceptionServices;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// Immutable scenario: a context factory, ordered steps, optional cleanup, and metadata.
    /// Each execution owns a fresh context.
    /// </summary>
    /// <typeparam name="TContext">Per-execution context type.</typeparam>
    internal sealed class ScenarioDefinition<TContext> : IScenarioDefinition
    {
        private const string MessagePrecedingFailed = "A preceding operation did not complete successfully.";

        private readonly Func<CancellationToken, Task<TContext>> _Factory;
        private readonly ScenarioStepDescriptor<TContext>[] _Steps;
        private readonly Func<TContext, CancellationToken, Task>? _Cleanup;
        private readonly TimeSpan _CleanupTimeout;
        private readonly ScenarioMetadata _Metadata;

        internal ScenarioDefinition(
            Func<CancellationToken, Task<TContext>> factory,
            ScenarioStepDescriptor<TContext>[] steps,
            ScenarioMetadata metadata,
            Func<TContext, CancellationToken, Task>? cleanup,
            TimeSpan cleanupTimeout)
        {
            _Factory = factory;
            _Steps = steps;
            _Metadata = metadata;
            _Cleanup = cleanup;
            _CleanupTimeout = cleanupTimeout;
        }

        public ScenarioMetadata Metadata
        {
            get { return _Metadata; }
        }

        public async Task<TestResult> ExecuteAsync(TestCaseDescriptor descriptor, ITestResultSink? sink, CancellationToken cancellationToken, CaseDeadline? deadline)
        {
            Execution execution = new Execution(descriptor, cancellationToken);
            CancellationToken caseToken = deadline != null ? deadline.Token : cancellationToken;
            Stopwatch total = Stopwatch.StartNew();

            if (descriptor.IsSkipped)
            {
                foreach (ScenarioStepDescriptor<TContext> step in _Steps)
                {
                    execution.Steps.Add(new ScenarioStepResult(step.StepId, step.DisplayName, step.Kind,
                        ScenarioStepOutcome.Skipped, TimeSpan.Zero, null, descriptor.SkipReason));
                }

                return execution.Build(TestOutcome.Skipped, TimeSpan.Zero, _Metadata, descriptor.SkipReason);
            }

            TContext context = default!;
            bool created = await CreateContextAsync(execution, caseToken).ConfigureAwait(false);
            if (created) context = execution.Context!;

            try
            {
                foreach (ScenarioStepDescriptor<TContext> step in _Steps)
                {
                    if (execution.Error != null || execution.ObserverError != null)
                    {
                        execution.Steps.Add(new ScenarioStepResult(step.StepId, step.DisplayName, step.Kind,
                            ScenarioStepOutcome.NotRun, TimeSpan.Zero, null, MessagePrecedingFailed));
                        continue;
                    }

                    await RunStepAsync(execution, step, context, sink, caseToken).ConfigureAwait(false);
                }
            }
            finally
            {
                // The case timeout covers context creation and steps, not cleanup.
                deadline?.Stop();
                if (created) await CleanupAsync(execution, context).ConfigureAwait(false);
            }

            total.Stop();
            if (execution.ObserverError != null) ExceptionDispatchInfo.Capture(execution.ObserverError).Throw();

            TestOutcome outcome = execution.Error == null ? TestOutcome.Passed : execution.Canceled ? TestOutcome.Canceled : TestOutcome.Failed;
            Exception? primary = execution.Error;
            string? message = execution.Message;

            if (execution.CleanupErrors.Count > 0)
            {
                if (outcome == TestOutcome.Passed) outcome = TestOutcome.Failed;
                if (primary == null)
                {
                    primary = execution.CleanupErrors[0];
                    message = "Context cleanup: " + primary.Message;
                }
            }

            return execution.Build(outcome, total.Elapsed, _Metadata, message, primary);
        }

        private async Task<bool> CreateContextAsync(Execution execution, CancellationToken cancellationToken)
        {
            Activity? activity = ScenarioTelemetry.Start(TouchstoneTelemetry.ScenarioOperationContext);
            Stopwatch timer = Stopwatch.StartNew();

            try
            {
                cancellationToken.ThrowIfCancellationRequested();
                TContext context = await _Factory(cancellationToken).ConfigureAwait(false);
                if (context is null)
                    throw new InvalidOperationException("Scenario context factory returned null for " + execution.Descriptor.TestId + ".");
                execution.Context = context;
                return true;
            }
            catch (Exception ex)
            {
                execution.SetFailure(ex, "Context creation: ");
                return false;
            }
            finally
            {
                ScenarioTelemetry.Complete(activity, TouchstoneTelemetry.ScenarioOperationContext, execution.OperationOutcome(), timer.Elapsed, null, execution.Error);
            }
        }

        private async Task RunStepAsync(
            Execution execution,
            ScenarioStepDescriptor<TContext> step,
            TContext context,
            ITestResultSink? sink,
            CancellationToken cancellationToken)
        {
            ScenarioStepOutcome outcome = ScenarioStepOutcome.Passed;
            Exception? error = null;
            Stopwatch timer = Stopwatch.StartNew();
            Activity? activity = ScenarioTelemetry.Start(TouchstoneTelemetry.ScenarioOperationStep, step.Kind);

            try
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (sink != null)
                {
                    try
                    {
                        ScenarioStepResult started = new ScenarioStepResult(step.StepId, step.DisplayName, step.Kind, ScenarioStepOutcome.NotRun, TimeSpan.Zero);
                        await sink.OnStepStartedAsync(execution.Descriptor.TestId, started, cancellationToken).ConfigureAwait(false);
                    }
                    catch (Exception ex)
                    {
                        execution.ObserverError = ex;
                        throw;
                    }
                }

                Task? task = step.ExecuteAsync(context, cancellationToken);
                if (task == null) throw new InvalidOperationException("Step " + step.StepId + " returned a null task.");
                await task.ConfigureAwait(false);
                cancellationToken.ThrowIfCancellationRequested();
            }
            catch (Exception ex)
            {
                error = ex;
                if (execution.ObserverError == null) execution.SetFailure(ex, string.Empty);
                outcome = execution.Canceled ? ScenarioStepOutcome.Canceled : ScenarioStepOutcome.Failed;
            }
            finally
            {
                ScenarioTelemetry.Complete(activity, TouchstoneTelemetry.ScenarioOperationStep, StepOutcomeLabel(outcome), timer.Elapsed, step.Kind, error);
            }

            ScenarioStepResult completed = new ScenarioStepResult(step.StepId, step.DisplayName, step.Kind, outcome, timer.Elapsed, error);
            execution.Steps.Add(completed);

            if (sink != null && execution.ObserverError == null)
            {
                try
                {
                    await sink.OnStepCompletedAsync(execution.Descriptor.TestId, completed, CancellationToken.None).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    execution.ObserverError = ex;
                }
            }
        }

        private async Task CleanupAsync(Execution execution, TContext context)
        {
            Activity? activity = ScenarioTelemetry.Start(TouchstoneTelemetry.ScenarioOperationCleanup);
            Stopwatch timer = Stopwatch.StartNew();

            using (CancellationTokenSource budget = new CancellationTokenSource(_CleanupTimeout))
            {
                if (_Cleanup != null)
                {
                    try
                    {
                        Task? task = _Cleanup(context, budget.Token);
                        if (task != null) await task.ConfigureAwait(false);
                    }
                    catch (Exception ex)
                    {
                        execution.CleanupErrors.Add(ex);
                    }
                }

                try
                {
                    if (context is IAsyncDisposable asyncDisposable)
                        await asyncDisposable.DisposeAsync().ConfigureAwait(false);
                    else if (context is IDisposable disposable)
                        disposable.Dispose();
                }
                catch (Exception ex)
                {
                    execution.CleanupErrors.Add(ex);
                }
            }

            string outcome = execution.CleanupErrors.Count == 0 ? TouchstoneTelemetry.OutcomePassed : TouchstoneTelemetry.OutcomeFailed;
            Exception? first = execution.CleanupErrors.Count == 0 ? null : execution.CleanupErrors[0];
            ScenarioTelemetry.Complete(activity, TouchstoneTelemetry.ScenarioOperationCleanup, outcome, timer.Elapsed, null, first);
        }

        private static string StepOutcomeLabel(ScenarioStepOutcome outcome)
        {
            switch (outcome)
            {
                case ScenarioStepOutcome.Passed: return TouchstoneTelemetry.OutcomePassed;
                case ScenarioStepOutcome.Canceled: return TouchstoneTelemetry.OutcomeCanceled;
                default: return TouchstoneTelemetry.OutcomeFailed;
            }
        }

        private sealed class Execution
        {
            internal Execution(TestCaseDescriptor descriptor, CancellationToken callerToken)
            {
                Descriptor = descriptor;
                CallerToken = callerToken;
            }

            internal CancellationToken CallerToken { get; }

            internal TestCaseDescriptor Descriptor { get; }

            internal TContext? Context { get; set; }

            internal List<ScenarioStepResult> Steps { get; } = new List<ScenarioStepResult>();

            internal List<Exception> CleanupErrors { get; } = new List<Exception>();

            internal Exception? Error { get; private set; }

            internal string? Message { get; private set; }

            internal bool Canceled { get; private set; }

            internal Exception? ObserverError { get; set; }

            internal void SetFailure(Exception error, string prefix)
            {
                Error = error;
                Message = prefix + error.Message;
                Canceled = TestExecutor.IsCallerCancellation(error, CallerToken);
            }

            internal string OperationOutcome()
            {
                if (Error == null) return TouchstoneTelemetry.OutcomePassed;
                return Canceled ? TouchstoneTelemetry.OutcomeCanceled : TouchstoneTelemetry.OutcomeFailed;
            }

            internal TestResult Build(TestOutcome outcome, TimeSpan duration, ScenarioMetadata metadata, string? message, Exception? exception = null)
            {
                return new TestResult(Descriptor.SuiteId, Descriptor.CaseId, Descriptor.DisplayName, outcome, duration,
                    message, exception, Descriptor.Tags, metadata, Steps, CleanupErrors);
            }
        }
    }
}
