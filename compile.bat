@echo off
cd /d "%~dp0"
echo Compiling MR OSD Shield...

set "CSC=%SystemRoot%\Microsoft.NET\Framework64\v4.0.30319\csc.exe"

if not exist "%CSC%" (
    echo ERROR: csc.exe not found: %CSC%
    exit /b 1
)

"%CSC%" /target:winexe /out:MR_OSD_Shield.exe /platform:anycpu /optimize+ /nologo /reference:System.ServiceProcess.dll /reference:System.Drawing.dll /reference:System.Windows.Forms.dll Shield.cs

if errorlevel 1 (
    echo FAILED
    if /i "%1" neq "--no-pause" pause
    exit /b 1
)

echo SUCCESS: %~dp0MR_OSD_Shield.exe

if /i "%1" neq "--no-pause" pause
exit /b 0