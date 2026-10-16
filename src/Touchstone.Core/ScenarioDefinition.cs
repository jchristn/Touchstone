#nullable enable
namespace Touchstone.Core
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Runtime.ExceptionServices;
    using System.Threading;
    using System.Threading.Tasks;
    internal sealed class ScenarioDefinition<TContext> : IScenarioDefinition
    {
        private readonly Func<CancellationToken, Task<TContext>> _Factory;
        private readonly ScenarioStepDescriptor<TContext>[] _Steps;
        private readonly Func<TContext, CancellationToken, Task>? _Cleanup;
        private readonly TimeSpan _CleanupTimeout;
        public ScenarioMetadata Metadata { get; }

        internal ScenarioDefinition(Func<CancellationToken, Task<TContext>> factory, ScenarioStepDescriptor<TContext>[] steps,
            ScenarioMetadata metadata, Func<TContext, CancellationToken, Task>? cleanup, TimeSpan cleanupTimeout)
        {
            _Factory = factory;
            _Steps = steps;
            Metadata = metadata;
            _Cleanup = cleanup;
            _CleanupTimeout = cleanupTimeout;
        }

        public async Task<TestResult> ExecuteAsync(TestCaseDescriptor descriptor, IScenarioStepSink? sink, CancellationToken cancellationToken)
        {
            TestResult result = new TestResult
            {
                TestId = descriptor.TestId, SuiteId = descriptor.SuiteId, CaseId = descriptor.CaseId,
                DisplayName = descriptor.DisplayName, Success = true, Skipped = descriptor.Skip,
                Message = descriptor.Skip ? descriptor.SkipReason ?? "Skipped" : null, Metadata = Metadata
            };
            List<ScenarioStepResult> results = new List<ScenarioStepResult>();
            List<Exception> cleanupErrors = new List<Exception>();
            Stopwatch total = Stopwatch.StartNew();
            TContext context = default!;
            bool created = false;
            Exception? observerError = null;
            if (!descriptor.Skip)
            {
                Activity? creation = ScenarioTelemetry.Start("context");
                Stopwatch timer = Stopwatch.StartNew();
                try
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    context = await _Factory(cancellationToken).ConfigureAwait(false);
                    if (context is null) throw new InvalidOperationException("Scenario context factory returned null for " + descriptor.TestId);
                    created = true;
                }
                catch (Exception ex) { SetFailure(result, ex, cancellationToken, "Context creation: "); }
                finally { ScenarioTelemetry.Complete(creation, "context", result.Exception == null ? "Passed" : result.Canceled ? "Canceled" : "Failed", timer.Elapsed, exception: result.Exception); }
            }
            try
            {
                foreach (ScenarioStepDescriptor<TContext> step in _Steps)
                {
                    if (descriptor.Skip || result.Exception != null || observerError != null)
                    {
                        results.Add(new ScenarioStepResult(step.StepId, step.DisplayName, step.Kind,
                            descriptor.Skip ? ScenarioStepOutcome.Skipped : ScenarioStepOutcome.NotRun, TimeSpan.Zero,
                            message: descriptor.Skip ? result.Message : "A preceding operation did not complete successfully."));
                        continue;
                    }
                    ScenarioStepOutcome outcome = ScenarioStepOutcome.Passed;
                    Exception? error = null;
                    Stopwatch timer = Stopwatch.StartNew();
                    Activity? activity = ScenarioTelemetry.Start("step", step.Kind);
                    try
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        if (sink != null)
                        {
                            try
                            {
                                await sink.OnStepStartedAsync(descriptor.TestId, new ScenarioStepResult(step.StepId,
                                    step.DisplayName, step.Kind, ScenarioStepOutcome.NotRun, TimeSpan.Zero), cancellationToken).ConfigureAwait(false);
                            }
                            catch (Exception ex) { observerError = ex; throw; }
                        }
                        await step.ExecuteAsync(context, cancellationToken).ConfigureAwait(false);
                        cancellationToken.ThrowIfCancellationRequested();
                    }
                    catch (Exception ex)
                    {
                        error = ex;
                        if (observerError == null) SetFailure(result, ex, cancellationToken, "");
                        outcome = result.Canceled ? ScenarioStepOutcome.Canceled : ScenarioStepOutcome.Failed;
                    }
                    finally { ScenarioTelemetry.Complete(activity, "step", outcome.ToString(), timer.Elapsed, step.Kind, error); }
                    ScenarioStepResult completed = new ScenarioStepResult(step.StepId, step.DisplayName, step.Kind, outcome, timer.Elapsed, error);
                    results.Add(completed);
                    if (sink != null && observerError == null)
                    {
                        try { await sink.OnStepCompletedAsync(descriptor.TestId, completed, CancellationToken.None).ConfigureAwait(false); }
                        catch (Exception ex) { observerError = ex; }
                    }
                }
            }
            finally
            {
                if (created)
                {
                    Activity? cleanupActivity = ScenarioTelemetry.Start("cleanup");
                    Stopwatch timer = Stopwatch.StartNew();
                    using (CancellationTokenSource cleanupToken = new CancellationTokenSource(_CleanupTimeout))
                    {
                        if (_Cleanup != null)
                        {
                            try { await _Cleanup(context, cleanupToken.Token).ConfigureAwait(false); }
                            catch (Exception ex) { cleanupErrors.Add(ex); }
                        }
                        try
                        {
                            if (context is IAsyncDisposable asyncDisposable) await asyncDisposable.DisposeAsync().ConfigureAwait(false);
                            else if (context is IDisposable disposable) disposable.Dispose();
                        }
                        catch (Exception ex) { cleanupErrors.Add(ex); }
                    }
                    ScenarioTelemetry.Complete(cleanupActivity, "cleanup", cleanupErrors.Count == 0 ? "Passed" : "Failed", timer.Elapsed, exception: cleanupErrors.Count == 0 ? null : cleanupErrors[0]);
                }
            }
            result.Steps = results.AsReadOnly();
            result.CleanupExceptions = cleanupErrors.AsReadOnly();
            if (cleanupErrors.Count > 0)
            {
                result.Success = false;
                if (result.Exception == null)
                {
                    result.Exception = cleanupErrors[0];
                    result.Message = "Context cleanup: " + cleanupErrors[0].Message;
                }
            }
            result.Duration = descriptor.Skip ? TimeSpan.Zero : total.Elapsed;
            if (observerError != null) ExceptionDispatchInfo.Capture(observerError).Throw();
            return result;
        }
        private static void SetFailure(TestResult result, Exception error, CancellationToken token, string prefix)
        {
            result.Success = false;
            result.Exception = error;
            result.Message = prefix + error.Message;
            result.Canceled = error is OperationCanceledException canceled && token.IsCancellationRequested && canceled.CancellationToken == token;
        }
    }
}
