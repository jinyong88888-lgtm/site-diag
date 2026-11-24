@echo off
rem Build site-diag.exe with the C# compiler that ships with .NET Framework.
rem No SDK, no NuGet, no Visual Studio required.
setlocal
set "ROOT=%~dp0"

set "CSC=%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
if not exist "%CSC%" set "CSC=%WINDIR%\Microsoft.NET\Framework\v4.0.30319\csc.exe"
if not exist "%CSC%" (
    echo [ERROR] csc.exe not found. Install .NET Framework 4.5 or later.
    exit /b 1
)

echo Building...
"%CSC%" /nologo /target:exe /out:"%ROOT%site-diag.exe" /win32icon:"%ROOT%assets\site-diag.ico" /codepage:65001 /optimize+ "%ROOT%src\Net.cs" "%ROOT%src\Program.cs"
if errorlevel 1 (
    echo [ERROR] Build failed.
    exit /b 1
)

echo Built: %ROOT%site-diag.exe
exit /b 0