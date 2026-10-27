@echo off
setlocal
set CSC="C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
if not exist %CSC% (
    set CSC="C:\Windows\Microsoft.NET\Framework\v4.0.30319\csc.exe"
)

echo Compiling AlwaysOnTop.exe...
%CSC% /nologo /target:winexe /optimize+ /out:AlwaysOnTop.exe /win32icon:app.ico /r:System.dll /r:System.Core.dll /r:System.Drawing.dll /r:System.Windows.Forms.dll /r:System.Web.Extensions.dll AlwaysOnTop.cs

if %ERRORLEVEL% EQU 0 (
    echo Build succeeded: AlwaysOnTop.exe
) else (
    echo Build failed with error code %ERRORLEVEL%
)
