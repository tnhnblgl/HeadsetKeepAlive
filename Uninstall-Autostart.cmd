@echo off
setlocal
rem Removes the Startup folder shortcut. Does not stop a running copy - use
rem Stop.cmd for that.

set "LINK=%APPDATA%\Microsoft\Windows\Start Menu\Programs\Startup\Headset Keep-Alive.lnk"

if not exist "%LINK%" (
    echo Autostart was not installed.
    ping -n 4 127.0.0.1 >nul 2>&1
    exit /b 0
)

del "%LINK%"
echo Autostart removed. Headset Keep-Alive will no longer start with Windows.
echo Run Stop.cmd if it is still running right now.
ping -n 6 127.0.0.1 >nul 2>&1
