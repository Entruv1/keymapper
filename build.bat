@echo off
rem ============================================
rem  按键映射助手 KeyMapper - 一键编译脚本
rem  双击本文件即可编译生成 KeyMapper.exe
rem  需要系统已安装 .NET Framework（Win7+ 自带）
rem ============================================
setlocal
set CSC=%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe
if not exist "%CSC%" set CSC=%WINDIR%\Microsoft.NET\Framework\v4.0.30319\csc.exe

"%CSC%" /nologo /target:winexe /out:KeyMapper.exe /win32icon:keyboard.ico /r:System.dll /r:System.Windows.Forms.dll /r:System.Drawing.dll /r:System.Xml.dll KeyMapper.cs

if errorlevel 1 (
    echo.
    echo 编译失败，请检查上方错误信息。
) else (
    echo.
    echo 编译成功：KeyMapper.exe
)
pause
