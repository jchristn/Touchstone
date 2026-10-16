# Running the overhead suite

Install the .NET 8 and .NET 10 SDKs. Run `run-baseline.sh` (or `.bat` on Windows) from any working directory. It resolves the repository root, builds the solution, and writes `benchmarks/results/candidate.json`. The harness has no product project references; it invokes the public CLI of the separately built consumer workload.

```sh
bash benchmarks/run-baseline.sh
dotnet run --project src/Test.Benchmark -c Release -f net10.0 --no-build -- compare benchmarks/baselines/macos-arm64.json benchmarks/results/candidate.json benchmarks/results/comparison.json
```

The dataset and expected case/step counts are in `datasets.json`. Probe identities combine arm, listener mode, and repetition. There is no downloaded data. Reports store their dataset/configuration hash and a hash of source files and central build properties. Outputs are resolved relative to the repository root, never the caller's working directory.

`ci-gate.sh` and `.bat` run a fresh candidate and compare it to the baseline passed as their first argument. Use a dedicated machine with matching OS, CPU, host, runtime, SDK, and configuration. A comparison exits 0 for no regression, 1 for regression, and 2 for invalid/incomplete or incomparable input. Comparison JSON and Markdown are saved beside the candidate. A failed workload run still saves an incomplete report.

The floor is an empty suite run in its own process; the reference is ordinary delegate execution. Read operation timing separately from total process timing. Allocated bytes and peak working set are process-wide observations, not live object counts. See `../BENCHMARKING.md` for methodology and `RESULTS.md` for measured results.
