@echo off
cd /d "%~dp0"
echo Compiling MR OSD Shield...
C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe /target:winexe /out:MR_OSD_Shield.exe /platform:anycpu /optimize+ /nologo /reference:System.ServiceProcess.dll /reference:System.Drawing.dll /reference:System.Windows.Forms.dll Shield.cs
if exist "%~dp0MR_OSD_Shield.exe" (
    echo SUCCESS: %~dp0MR_OSD_Shield.exe
) else (
    echo FAILED
)
pause