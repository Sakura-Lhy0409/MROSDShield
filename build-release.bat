@echo off
setlocal
cd /d "%~dp0"

set "VERSION=1.0.6"
set "ROOT=%~dp0.."
set "PKG=%ROOT%\MROSDShield-v%VERSION%.zip"
set "STAGE=%TEMP%\MROSDShield-release-%VERSION%"

echo Building MR OSD Shield v%VERSION%...
call "%~dp0compile.bat" --no-pause
if errorlevel 1 (
    echo ERROR: build failed.
    exit /b 1
)

if exist "%STAGE%" rmdir /s /q "%STAGE%"
mkdir "%STAGE%\MROSDShield"

set "OUT=%~dp0bin\Release\net8.0-windows"

if not exist "%OUT%\MR_OSD_Shield.exe" (
    echo ERROR: build output not found: %OUT%\MR_OSD_Shield.exe
    exit /b 1
)

xcopy /e /i /y "%OUT%" "%STAGE%\MROSDShield" >nul
copy /y "%~dp0README.md" "%STAGE%\MROSDShield\" >nul
copy /y "%~dp0LICENSE" "%STAGE%\MROSDShield\" >nul
copy /y "%~dp0MROSDShield.csproj" "%STAGE%\MROSDShield\" >nul
copy /y "%~dp0app.manifest" "%STAGE%\MROSDShield\" >nul
copy /y "%~dp0compile.bat" "%STAGE%\MROSDShield\" >nul
copy /y "%~dp0build-release.bat" "%STAGE%\MROSDShield\" >nul
xcopy /e /i /y "%~dp0src" "%STAGE%\MROSDShield\src" >nul
xcopy /e /i /y "%~dp0frontend" "%STAGE%\MROSDShield\frontend" >nul
xcopy /e /i /y "%~dp0tools" "%STAGE%\MROSDShield\tools" >nul

if exist "%PKG%" del /f /q "%PKG%"

powershell -NoProfile -ExecutionPolicy Bypass -Command "Compress-Archive -Path '%STAGE%\MROSDShield' -DestinationPath '%PKG%' -Force"
if errorlevel 1 (
    echo ERROR: package failed.
    exit /b 1
)

rmdir /s /q "%STAGE%"

echo SUCCESS: %PKG%
exit /b 0