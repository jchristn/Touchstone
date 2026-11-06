@echo off

for %%P in (Xunit3 Xunit2 Nunit Mstest) do (
    echo ============================================================================
    echo  Touchstone.SampleApp.Tests.%%P
    echo ============================================================================
    echo.
    dotnet test test\Touchstone.SampleApp.Tests.%%P
    echo.
)

echo ============================================================================
echo  Touchstone.SampleApp.Tests.Console
echo ============================================================================
echo.
dotnet run --project test\Touchstone.SampleApp.Tests.Console
