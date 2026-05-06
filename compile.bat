@echo off
cd /d "%~dp0"
echo Building MR OSD Shield...

if not exist "MROSDShield.csproj" (
    echo ERROR: MROSDShield.csproj not found.
    exit /b 1
)

set "DOTNET_EXE=dotnet"
if exist "%~dp0..\.dotnet\dotnet.exe" set "DOTNET_EXE=%~dp0..\.dotnet\dotnet.exe"
if exist "%~dp0\.dotnet\dotnet.exe" set "DOTNET_EXE=%~dp0\.dotnet\dotnet.exe"

"%DOTNET_EXE%" build MROSDShield.csproj -c Release -p:CopyBuildOutputToOutputDirectory=true -p:CopyOutputSymbolsToOutputDirectory=false
set "BUILD_ERROR=%ERRORLEVEL%"

if not "%BUILD_ERROR%"=="0" (
    echo FAILED
    if /i "%1" neq "--no-pause" pause
    exit /b %BUILD_ERROR%
)

set "OUT=%~dp0bin\Release\net8.0-windows"
if exist "%OUT%\MR_OSD_Shield.exe" (
    echo Syncing runnable files to project root...
    xcopy /e /i /y "%OUT%\*" "%~dp0" >nul
    if errorlevel 1 (
        echo ERROR: sync runnable files failed.
        if /i "%1" neq "--no-pause" pause
        exit /b 1
    )
)

echo SUCCESS: build completed.

if /i "%1" neq "--no-pause" pause
exit /b 0