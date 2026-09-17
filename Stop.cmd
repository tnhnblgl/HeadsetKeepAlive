@echo off
rem Stops the keep-alive.

taskkill /IM HeadsetKeepAlive.exe /F >nul 2>&1
if errorlevel 1 (
    echo Headset Keep-Alive was not running.
) else (
    echo Headset Keep-Alive stopped. Your headset can sleep again.
)

ping -n 4 127.0.0.1 >nul 2>&1
