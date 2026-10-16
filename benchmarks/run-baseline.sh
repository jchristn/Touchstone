#!/usr/bin/env bash
set -euo pipefail
cd "$(cd "$(dirname "$0")/.." && pwd)"
dotnet build Touchstone.slnx -c Release
dotnet run --project src/Test.Benchmark -c Release -f net10.0 --no-build -- run benchmarks/results/candidate.json
