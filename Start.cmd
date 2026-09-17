@echo off
rem Starts the keep-alive. It runs in the background with no window.

tasklist /FI "IMAGENAME eq HeadsetKeepAlive.exe" | find /I "HeadsetKeepAlive.exe" >nul
if not errorlevel 1 (
    echo Headset Keep-Alive is already running.
    ping -n 4 127.0.0.1 >nul 2>&1
    exit /b 0
)

start "" "%~dp0HeadsetKeepAlive.exe"
echo Headset Keep-Alive started.
echo It has no window on purpose - it sits in Task Manager as "Headset Keep-Alive".
ping -n 5 127.0.0.1 >nul 2>&1
