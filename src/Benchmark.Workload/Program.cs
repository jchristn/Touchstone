namespace Touchstone.Benchmark.Workload
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Diagnostics.Metrics;
    using System.IO;
    using System.Linq;
    using System.Text.Json;
    using System.Threading;
    using System.Threading.Tasks;

    using Touchstone.Cli;
    using Touchstone.Core;

    internal static class Program
    {
        private static async Task<int> Main(string[] args)
        {
            if (args.Length != 3 || !int.TryParse(args[1], out int count) || count < 1) return 2;
            string arm = args[0];
            bool listen = args[2] == "on";
            if (arm != "floor" && arm != "delegate" && arm != "one" && arm != "many") return 2;
            using (ActivityListener traces = new ActivityListener
            {
                ShouldListenTo = source => listen && source.Name == "Touchstone",
                Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllData
            })
            using (MeterListener metrics = new MeterListener())
            {
                ActivitySource.AddActivityListener(traces);
                metrics.InstrumentPublished = (instrument, listener) => { if (listen && instrument.Meter.Name == "Touchstone") listener.EnableMeasurementEvents(instrument); };
                metrics.SetMeasurementEventCallback<long>((_, _, _, _) => { });
                metrics.SetMeasurementEventCallback<double>((_, _, _, _) => { });
                metrics.Start();
                TestCaseDescriptor descriptor = new TestCaseDescriptor("Benchmark", "case", "no work", _ => Task.CompletedTask);
                if (arm == "one" || arm == "many")
                {
                    ScenarioBuilder<int> builder = new ScenarioBuilder<int>("Benchmark", "case", "no work", _ => Task.FromResult(0));
                    for (int i = 0; i < (arm == "one" ? 1 : 5); i++) builder.Then(i.ToString(), "a completed operation", (_, _) => Task.CompletedTask);
                    descriptor = builder.Build();
                }
                List<TestCaseDescriptor> cases = new List<TestCaseDescriptor>();
                if (arm != "floor") for (int i = 0; i < count; i++) cases.Add(descriptor);
                long before = GC.GetTotalAllocatedBytes(true);
                Stopwatch timer = Stopwatch.StartNew();
                ConsoleResultSink sink = new ConsoleResultSink(TextWriter.Null, false);
                int code = await ConsoleRunner.RunAsync(new[] { new TestSuiteDescriptor("Benchmark", "benchmark", cases) }, sink, cancellationToken: CancellationToken.None).ConfigureAwait(false);
                timer.Stop();
                WorkloadResult result = new WorkloadResult
                {
                    Arm = arm, Listener = listen, Cases = sink.AllResults.Count,
                    Steps = sink.AllResults.Sum(item => item.Steps.Count),
                    Failed = code != 0 || sink.AllResults.Any(item => item.Steps.Any(step => step.Outcome != ScenarioStepOutcome.Passed)), ElapsedMs = timer.Elapsed.TotalMilliseconds,
                    AllocatedBytes = GC.GetTotalAllocatedBytes(true) - before
                };
                result.PeakWorkingSetBytes = ProcessMemory.PeakBytes();
                Console.WriteLine(JsonSerializer.Serialize(result));
                return code;
            }
        }
    }
}
