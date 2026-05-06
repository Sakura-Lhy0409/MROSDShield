@echo off
setlocal
cd /d "%~dp0"

set "VERSION=1.0.4"
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

copy /y "%~dp0MR_OSD_Shield.exe" "%STAGE%\MROSDShield\" >nul
copy /y "%~dp0README.md" "%STAGE%\MROSDShield\" >nul
copy /y "%~dp0LICENSE" "%STAGE%\MROSDShield\" >nul
copy /y "%~dp0compile.bat" "%STAGE%\MROSDShield\" >nul
copy /y "%~dp0build-release.bat" "%STAGE%\MROSDShield\" >nul
copy /y "%~dp0Shield.cs" "%STAGE%\MROSDShield\" >nul

if exist "%PKG%" del /f /q "%PKG%"

powershell -NoProfile -ExecutionPolicy Bypass -Command "Compress-Archive -Path '%STAGE%\MROSDShield' -DestinationPath '%PKG%' -Force"
if errorlevel 1 (
    echo ERROR: package failed.
    exit /b 1
)

rmdir /s /q "%STAGE%"

echo SUCCESS: %PKG%
exit /b 0