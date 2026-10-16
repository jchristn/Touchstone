namespace Touchstone.Benchmark
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Globalization;
    using System.IO;
    using System.Linq;
    using System.Runtime.InteropServices;
    using System.Security.Cryptography;
    using System.Text;
    using System.Text.Json;
    using System.Threading;
    using System.Threading.Tasks;

    internal static class Program
    {
        private static readonly JsonSerializerOptions _Json = new JsonSerializerOptions { WriteIndented = true };

        private static async Task<int> Main(string[] args)
        {
            using (CancellationTokenSource stop = new CancellationTokenSource(TimeSpan.FromMinutes(10)))
            {
                Console.CancelKeyPress += (_, e) => { e.Cancel = true; stop.Cancel(); };
                try
                {
                    string root = FindRoot();
                    if (args.Length == 2 && args[0] == "run") return await RunAsync(root, args[1], stop.Token).ConfigureAwait(false);
                    if (args.Length == 4 && args[0] == "compare") return Compare(root, args[1], args[2], args[3]);
                    Console.Error.WriteLine("Usage: run <report.json> | compare <baseline.json> <candidate.json> <comparison.json>");
                    return 2;
                }
                catch (Exception error) { Console.Error.WriteLine(error.Message); return 2; }
            }
        }
        private static string FindRoot()
        {
            DirectoryInfo? current = new DirectoryInfo(AppContext.BaseDirectory);
            while (current != null)
            {
                if (Directory.Exists(Path.Combine(current.FullName, ".git")) && Directory.Exists(Path.Combine(current.FullName, "benchmarks"))) return current.FullName;
                current = current.Parent;
            }
            throw new DirectoryNotFoundException("Cannot find repository root.");
        }
        private static string Hash(string text) { return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text))); }
        private static string SourceHash(string root)
        {
            IEnumerable<string> files = Directory.GetFiles(Path.Combine(root, "src"), "*", SearchOption.AllDirectories)
                .Where(path => (path.EndsWith(".cs", StringComparison.Ordinal) || path.EndsWith(".csproj", StringComparison.Ordinal))
                    && !path.Contains(Path.DirectorySeparatorChar + "obj" + Path.DirectorySeparatorChar, StringComparison.Ordinal)
                    && !path.Contains(Path.DirectorySeparatorChar + "bin" + Path.DirectorySeparatorChar, StringComparison.Ordinal)).OrderBy(path => path, StringComparer.Ordinal);
            return Hash(string.Join("\n", files.Select(path => Path.GetRelativePath(root, path) + ":" + Hash(File.ReadAllText(path)))) + File.ReadAllText(Path.Combine(root, "Directory.Build.props")));
        }
        private static async Task<string> CommandAsync(string root, string command, IReadOnlyList<string> arguments, CancellationToken cancellationToken)
        {
            ProcessStartInfo info = new ProcessStartInfo(command) { WorkingDirectory = root, RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false };
            foreach (string argument in arguments) info.ArgumentList.Add(argument);
            using (Process process = Process.Start(info) ?? throw new InvalidOperationException("Cannot start " + command))
            {
                Task<string> output = process.StandardOutput.ReadToEndAsync(cancellationToken);
                Task<string> errors = process.StandardError.ReadToEndAsync(cancellationToken);
                try { await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false); }
                catch { if (!process.HasExited) process.Kill(true); throw; }
                string text = await output.ConfigureAwait(false);
                string diagnostic = await errors.ConfigureAwait(false);
                if (process.ExitCode != 0) throw new InvalidOperationException(command + " failed: " + diagnostic);
                return text.Trim();
            }
        }
        private static async Task<int> RunAsync(string root, string output, CancellationToken cancellationToken)
        {
            const int Count = 10000;
            const int Repetitions = 12;
            const int Warmups = 2;
            string dll = Path.Combine(root, "src", "Benchmark.Workload", "bin", "Release", "net10.0", "Touchstone.Benchmark.Workload.dll");
            string dataset = File.ReadAllText(Path.Combine(root, "benchmarks", "datasets.json"));
            BenchmarkDataset definition = JsonSerializer.Deserialize<BenchmarkDataset>(dataset, new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
                ?? throw new InvalidOperationException("Missing dataset definition.");
            if (definition.Schema != 1 || definition.Seed != 1729 || definition.Cases != Count
                || !definition.Arms.SequenceEqual(new[] { "floor", "delegate", "one", "many" })
                || !definition.Listeners.SequenceEqual(new[] { false, true })
                || definition.Steps.Count != 4 || definition.Steps["floor"] != 0 || definition.Steps["delegate"] != 0
                || definition.Steps["one"] != 1 || definition.Steps["many"] != 5)
                throw new InvalidOperationException("Dataset does not match the versioned workload contract.");
            BenchmarkReport report = new BenchmarkReport
            {
                Commit = await CommandAsync(root, "git", new[] { "rev-parse", "HEAD" }, cancellationToken).ConfigureAwait(false),
                DirtyState = await CommandAsync(root, "git", new[] { "status", "--porcelain" }, cancellationToken).ConfigureAwait(false),
                Sdk = await CommandAsync(root, "dotnet", new[] { "--version" }, cancellationToken).ConfigureAwait(false),
                SourceHash = SourceHash(root), DatasetHash = Hash(dataset), ConfigurationHash = Hash("count=10000;repetitions=12;warmups=2;seed=1729;net10.0;Release"),
                Machine = Environment.MachineName, OS = RuntimeInformation.OSDescription, Runtime = RuntimeInformation.FrameworkDescription,
                CpuCount = Environment.ProcessorCount, CpuModel = RuntimeInformation.ProcessArchitecture.ToString(), Count = Count,
                Repetitions = Repetitions, Warmups = Warmups, CollectedAt = DateTimeOffset.UtcNow
            };
            if (OperatingSystem.IsMacOS()) report.CpuModel = await CommandAsync(root, "sysctl", new[] { "-n", "machdep.cpu.brand_string" }, cancellationToken).ConfigureAwait(false);
            string[] arms = { "floor", "delegate", "one", "many" };
            try
            {
                for (int repetition = -Warmups; repetition < Repetitions; repetition++)
                {
                    foreach (bool listener in new[] { false, true })
                    {
                        // Rotate order deterministically to reduce systematic thermal/order bias.
                        for (int index = 0; index < arms.Length; index++)
                        {
                            string arm = arms[(index + repetition + Warmups) % arms.Length];
                            ProcessStartInfo info = new ProcessStartInfo("dotnet") { WorkingDirectory = root, RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false };
                            foreach (string argument in new[] { dll, arm, Count.ToString(CultureInfo.InvariantCulture), listener ? "on" : "off" }) info.ArgumentList.Add(argument);
                            Stopwatch timer = Stopwatch.StartNew();
                            using (Process process = Process.Start(info) ?? throw new InvalidOperationException("Workload did not start."))
                            {
                                Task<string> resultText = process.StandardOutput.ReadToEndAsync(cancellationToken);
                                Task<string> errorText = process.StandardError.ReadToEndAsync(cancellationToken);
                                try { await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false); }
                                catch { if (!process.HasExited) process.Kill(true); throw; }
                                string text = await resultText.ConfigureAwait(false);
                                string errors = await errorText.ConfigureAwait(false);
                                if (process.ExitCode != 0) throw new InvalidOperationException("Workload failed: " + errors);
                                BenchmarkSample sample = JsonSerializer.Deserialize<BenchmarkSample>(text) ?? throw new InvalidOperationException("Missing workload result.");
                                if (sample.Failed || sample.Arm != arm || sample.Listener != listener || sample.Cases != (arm == "floor" ? 0 : Count)
                                    || sample.Steps != (arm == "one" ? Count : arm == "many" ? Count * 5 : 0)) throw new InvalidOperationException("Incomplete or incorrect workload.");
                                sample.ProcessMs = timer.Elapsed.TotalMilliseconds;
                                if (sample.PeakWorkingSetBytes <= 0) throw new InvalidOperationException("Missing memory measurement.");
                                sample.Repetition = repetition;
                                if (repetition >= 0) report.Samples.Add(sample);
                            }
                        }
                    }
                }
                report.Complete = report.Samples.Count == Repetitions * 8;
                return report.Complete ? 0 : 1;
            }
            finally
            {
                string path = Path.GetFullPath(output, root);
                Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                await File.WriteAllTextAsync(path, JsonSerializer.Serialize(report, _Json), CancellationToken.None).ConfigureAwait(false);
                Console.WriteLine("Report: " + path + "; complete=" + report.Complete);
            }
        }
        private static BenchmarkReport Read(string root, string path)
        {
            BenchmarkReport report = JsonSerializer.Deserialize<BenchmarkReport>(File.ReadAllText(Path.GetFullPath(path, root))) ?? throw new InvalidOperationException("Missing report.");
            if (!report.Complete || report.Repetitions < 2 || report.Count < 1 || report.Samples.Count != report.Repetitions * 8
                || report.Samples.Any(sample => sample.Failed || !double.IsFinite(sample.ElapsedMs) || sample.ElapsedMs < 0
                    || !double.IsFinite(sample.ProcessMs) || sample.ProcessMs <= 0 || sample.AllocatedBytes < 0 || sample.PeakWorkingSetBytes <= 0
                    || sample.Repetition < 0 || sample.Repetition >= report.Repetitions
                    || (sample.Arm != "floor" && sample.Arm != "delegate" && sample.Arm != "one" && sample.Arm != "many")
                    || sample.Cases != (sample.Arm == "floor" ? 0 : report.Count)
                    || sample.Steps != (sample.Arm == "one" ? report.Count : sample.Arm == "many" ? report.Count * 5 : 0)))
                throw new InvalidOperationException("Cannot compare incomplete or incorrect report.");
            return report;
        }
        private static int Compare(string root, string baseline, string candidate, string output)
        {
            BenchmarkReport a = Read(root, baseline);
            BenchmarkReport b = Read(root, candidate);
            if (a.Sdk != b.Sdk || a.ConfigurationHash != b.ConfigurationHash || a.DatasetHash != b.DatasetHash || a.Machine != b.Machine || a.OS != b.OS || a.Runtime != b.Runtime || a.CpuModel != b.CpuModel)
                throw new InvalidOperationException("Configuration, data, machine, or runtime differs; collect a matched baseline.");
            Dictionary<string, BenchmarkSample> old = a.Samples.ToDictionary(sample => sample.ProbeId, StringComparer.Ordinal);
            if (!old.Keys.OrderBy(key => key).SequenceEqual(b.Samples.Select(sample => sample.ProbeId).OrderBy(key => key))) throw new InvalidOperationException("Mismatched probe sets.");
            BenchmarkComparison comparison = new BenchmarkComparison { Tolerance = 0.20, BaselineCommit = a.Commit, CandidateCommit = b.Commit };
            Random random = new Random(1729);
            foreach (IGrouping<string, BenchmarkSample> group in b.Samples.GroupBy(sample => sample.Arm + "/" + sample.Listener))
            {
                BenchmarkSample[] samples = group.ToArray();
                foreach (string metric in new[] { "elapsed", "allocated", "process", "peak" })
                {
                    double Value(BenchmarkSample sample) { return metric == "elapsed" ? sample.ElapsedMs : metric == "allocated" ? sample.AllocatedBytes : metric == "process" ? sample.ProcessMs : sample.PeakWorkingSetBytes; }
                    double[] differences = samples.Select(sample => Value(sample) - Value(old[sample.ProbeId])).ToArray();
                    double meanOld = samples.Average(sample => Value(old[sample.ProbeId]));
                    double delta = differences.Average();
                    double[] bootstrap = new double[2000];
                    for (int i = 0; i < bootstrap.Length; i++)
                    {
                        double sum = 0;
                        for (int j = 0; j < differences.Length; j++) sum += differences[random.Next(differences.Length)];
                        bootstrap[i] = sum / differences.Length;
                    }
                    Array.Sort(bootstrap);
                    double[] before = samples.Select(sample => Value(old[sample.ProbeId])).OrderBy(value => value).ToArray();
                    double[] after = samples.Select(Value).OrderBy(value => value).ToArray();
                    int p95 = (int)Math.Ceiling(samples.Length * .95) - 1;
                    bool regression = delta > meanOld * comparison.Tolerance && bootstrap[50] > 0
                        && (metric == "allocated" || after[p95] > before[p95] * (1 + comparison.Tolerance));
                    comparison.Regression |= regression;
                    comparison.Details.Add(group.Key + " " + metric + ": delta=" + delta.ToString("F2", CultureInfo.InvariantCulture)
                        + "; paired bootstrap 95% CI=[" + bootstrap[50].ToString("F2", CultureInfo.InvariantCulture) + "," + bootstrap[1949].ToString("F2", CultureInfo.InvariantCulture) + "]; regression=" + regression);
                }
            }
            string path = Path.GetFullPath(output, root);
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, JsonSerializer.Serialize(comparison, _Json));
            File.WriteAllText(Path.ChangeExtension(path, ".md"), string.Join(Environment.NewLine, comparison.Details));
            Console.WriteLine("RESULT: " + (comparison.Regression ? "regression" : "no regression"));
            return comparison.Regression ? 1 : 0;
        }
    }
}
