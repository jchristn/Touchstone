@echo off

echo ============================================================================
echo  Touchstone.SampleApp.Tests.Xunit
echo ============================================================================
echo.
dotnet test tests\Touchstone.SampleApp.Tests.Xunit

echo.
echo.
echo ============================================================================
echo  Touchstone.SampleApp.Tests.Nunit
echo ============================================================================
echo.
dotnet test tests\Touchstone.SampleApp.Tests.Nunit

echo.
echo.
echo ============================================================================
echo  Touchstone.SampleApp.Tests.Mstest
echo ============================================================================
echo.
dotnet test tests\Touchstone.SampleApp.Tests.Mstest

echo.
echo.
echo ============================================================================
echo  Touchstone.SampleApp.Tests.Console
echo ============================================================================
echo.
dotnet run --project tests\Touchstone.SampleApp.Tests.Console
