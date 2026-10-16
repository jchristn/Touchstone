@echo off
if "%~1"=="" exit /b 2
cd /d "%~dp0.."
call benchmarks\run-baseline.bat
if errorlevel 1 exit /b 1
dotnet run --project src/Test.Benchmark -c Release -f net10.0 --no-build -- compare "%~1" benchmarks/results/candidate.json benchmarks/results/comparison.json
exit /b %errorlevel%
