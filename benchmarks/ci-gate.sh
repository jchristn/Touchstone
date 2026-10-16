#!/usr/bin/env bash
set -euo pipefail
cd "$(cd "$(dirname "$0")/.." && pwd)"
bash benchmarks/run-baseline.sh
dotnet run --project src/Test.Benchmark -c Release -f net10.0 --no-build -- compare "${1:?Pass a matching baseline path}" benchmarks/results/candidate.json benchmarks/results/comparison.json
