@echo off
rem ============================================
rem   KeyMapper one-click build script
rem   Double-click to build KeyMapper.exe
rem   Requires .NET Framework (built into Win7+)
rem ============================================
setlocal
set CSC=%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe
if not exist "%CSC%" set CSC=%WINDIR%\Microsoft.NET\Framework\v4.0.30319\csc.exe

"%CSC%" /nologo /target:winexe /out:KeyMapper.exe /win32icon:keyboard.ico /r:System.dll /r:System.Windows.Forms.dll /r:System.Drawing.dll /r:System.Xml.dll KeyMapper.cs

if errorlevel 1 (
    echo.
    echo [ERROR] Build failed, see messages above.
) else (
    echo.
    echo [OK] KeyMapper.exe generated.
)
pause
