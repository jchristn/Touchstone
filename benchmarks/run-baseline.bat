@echo off
cd /d "%~dp0.."
dotnet build Touchstone.slnx -c Release
if errorlevel 1 exit /b 1
dotnet run --project src/Test.Benchmark -c Release -f net10.0 --no-build -- run benchmarks/results/candidate.json
exit /b %errorlevel%
