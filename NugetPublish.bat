@echo off

if "%~1"=="" (
    echo Usage: NugetPublish.bat YOUR_NUGET_API_KEY
    exit /b 1
)

set APIKEY=%~1
for /f "tokens=3 delims=<>" %%V in ('findstr /r "<Version>" Directory.Build.props') do set VERSION=%%V

echo ============================================================================
echo  Building Release
echo ============================================================================
echo.
dotnet pack Touchstone.slnx -c Release
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
echo  Publishing Touchstone.Xunit2Adapter
echo ============================================================================
echo.
dotnet nuget push src\Touchstone.Xunit2Adapter\bin\Release\Touchstone.Xunit2Adapter.%VERSION%.nupkg -s nuget.org -k %APIKEY% --skip-duplicate
dotnet nuget push src\Touchstone.Xunit2Adapter\bin\Release\Touchstone.Xunit2Adapter.%VERSION%.snupkg -s nuget.org -k %APIKEY% --skip-duplicate

echo.
echo ============================================================================
echo  Publishing Touchstone.Xunit3Adapter
echo ============================================================================
echo.
dotnet nuget push src\Touchstone.Xunit3Adapter\bin\Release\Touchstone.Xunit3Adapter.%VERSION%.nupkg -s nuget.org -k %APIKEY% --skip-duplicate
dotnet nuget push src\Touchstone.Xunit3Adapter\bin\Release\Touchstone.Xunit3Adapter.%VERSION%.snupkg -s nuget.org -k %APIKEY% --skip-duplicate

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
