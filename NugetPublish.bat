@echo off

if "%~1"=="" (
    echo Usage: NugetPublish.bat YOUR_NUGET_API_KEY
    exit /b 1
)

set APIKEY=%~1
set VERSION=0.1.12

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
dotnet nuget push src\Touchstone.Core\bin\Release\Touchstone.Core.%VERSION%.nupkg -s nuget.org -k %APIKEY% --skip-duplicate
dotnet nuget push src\Touchstone.Core\bin\Release\Touchstone.Core.%VERSION%.snupkg -s nuget.org -k %APIKEY% --skip-duplicate

echo.
echo ============================================================================
echo  Publishing Touchstone.Cli
echo ============================================================================
echo.
dotnet nuget push src\Touchstone.Cli\bin\Release\Touchstone.Cli.%VERSION%.nupkg -s nuget.org -k %APIKEY% --skip-duplicate
dotnet nuget push src\Touchstone.Cli\bin\Release\Touchstone.Cli.%VERSION%.snupkg -s nuget.org -k %APIKEY% --skip-duplicate

echo.
echo ============================================================================
echo  Publishing Touchstone.XunitAdapter
echo ============================================================================
echo.
dotnet nuget push src\Touchstone.XunitAdapter\bin\Release\Touchstone.XunitAdapter.%VERSION%.nupkg -s nuget.org -k %APIKEY% --skip-duplicate
dotnet nuget push src\Touchstone.XunitAdapter\bin\Release\Touchstone.XunitAdapter.%VERSION%.snupkg -s nuget.org -k %APIKEY% --skip-duplicate

echo.
echo ============================================================================
echo  Publishing Touchstone.NunitAdapter
echo ============================================================================
echo.
dotnet nuget push src\Touchstone.NunitAdapter\bin\Release\Touchstone.NunitAdapter.%VERSION%.nupkg -s nuget.org -k %APIKEY% --skip-duplicate
dotnet nuget push src\Touchstone.NunitAdapter\bin\Release\Touchstone.NunitAdapter.%VERSION%.snupkg -s nuget.org -k %APIKEY% --skip-duplicate

echo.
echo ============================================================================
echo  Publishing Touchstone.MstestAdapter
echo ============================================================================
echo.
dotnet nuget push src\Touchstone.MstestAdapter\bin\Release\Touchstone.MstestAdapter.%VERSION%.nupkg -s nuget.org -k %APIKEY% --skip-duplicate
dotnet nuget push src\Touchstone.MstestAdapter\bin\Release\Touchstone.MstestAdapter.%VERSION%.snupkg -s nuget.org -k %APIKEY% --skip-duplicate

echo.
echo ============================================================================
echo  Publishing Touchstone (metapackage)
echo ============================================================================
echo.
dotnet nuget push src\Touchstone\bin\Release\Touchstone.%VERSION%.nupkg -s nuget.org -k %APIKEY% --skip-duplicate
dotnet nuget push src\Touchstone\bin\Release\Touchstone.%VERSION%.snupkg -s nuget.org -k %APIKEY% --skip-duplicate

echo.
echo Done.
