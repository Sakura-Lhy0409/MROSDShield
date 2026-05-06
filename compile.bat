@echo off
cd /d "%~dp0"
echo Compiling MR OSD Shield...

set "CSC=%SystemRoot%\Microsoft.NET\Framework64\v4.0.30319\csc.exe"

if not exist "%CSC%" (
    echo ERROR: csc.exe not found: %CSC%
    exit /b 1
)

set "SRC_LIST=%TEMP%\mrosdshield_sources_%RANDOM%.txt"
if exist "%SRC_LIST%" del /f /q "%SRC_LIST%"

for /r "%~dp0src" %%F in (*.cs) do echo "%%F" >> "%SRC_LIST%"

"%CSC%" /target:winexe /out:MR_OSD_Shield.exe /platform:anycpu /optimize+ /nologo /reference:System.ServiceProcess.dll /reference:System.Drawing.dll /reference:System.Windows.Forms.dll @"%SRC_LIST%"
set "BUILD_ERROR=%ERRORLEVEL%"
if exist "%SRC_LIST%" del /f /q "%SRC_LIST%"

if not "%BUILD_ERROR%"=="0" (
    echo FAILED
    if /i "%1" neq "--no-pause" pause
    exit /b %BUILD_ERROR%
)

echo SUCCESS: %~dp0MR_OSD_Shield.exe

if /i "%1" neq "--no-pause" pause
exit /b 0