@echo off
setlocal
rem Builds dist\HeadsetKeepAlive.exe using the C# compiler that ships with
rem Windows. No SDK, no downloads.

set "CSC=%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
if not exist "%CSC%" set "CSC=%WINDIR%\Microsoft.NET\Framework\v4.0.30319\csc.exe"
if not exist "%CSC%" (
    echo ERROR: could not find the C# compiler that ships with Windows.
    exit /b 1
)

if not exist "%~dp0dist" mkdir "%~dp0dist"

"%CSC%" /nologo /target:winexe /optimize+ /platform:anycpu /warnaserror+ ^
    /out:"%~dp0dist\HeadsetKeepAlive.exe" "%~dp0src\HeadsetKeepAlive.cs"

if errorlevel 1 (
    echo.
    echo Build FAILED.
    exit /b 1
)

echo Build OK  --^>  dist\HeadsetKeepAlive.exe
