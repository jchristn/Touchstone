@echo off

if "%~1"=="" (
    echo Usage: NugetPublish.bat YOUR_NUGET_API_KEY
    exit /b 1
)

set APIKEY=%~1

echo ============================================================================
echo  Building Release
echo ============================================================================
echo.
dotnet pack -c Release
if errorlevel 1 (
    echo Build failed.
    exit /b 1
)

echo.
echo ============================================================================
echo  Publishing Touchstone.Core
echo ============================================================================
echo.
dotnet nuget push src\Touchstone.Core\bin\Release\Touchstone.Core.0.1.0.nupkg -s nuget.org -k %APIKEY% --skip-duplicate

echo.
echo ============================================================================
echo  Publishing Touchstone.Cli
echo ============================================================================
echo.
dotnet nuget push src\Touchstone.Cli\bin\Release\Touchstone.Cli.0.1.0.nupkg -s nuget.org -k %APIKEY% --skip-duplicate

echo.
echo ============================================================================
echo  Publishing Touchstone.XunitAdapter
echo ============================================================================
echo.
dotnet nuget push src\Touchstone.XunitAdapter\bin\Release\Touchstone.XunitAdapter.0.1.0.nupkg -s nuget.org -k %APIKEY% --skip-duplicate

echo.
echo ============================================================================
echo  Publishing Touchstone (metapackage)
echo ============================================================================
echo.
dotnet nuget push src\Touchstone\bin\Release\Touchstone.0.1.0.nupkg -s nuget.org -k %APIKEY% --skip-duplicate

echo.
echo Done.
