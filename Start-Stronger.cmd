@echo off
rem Use this ONLY if the normal Start.cmd does not stop your headset sleeping.
rem It sends the same inaudible 10 Hz tone 8x stronger, which helps when the
rem Windows volume is set very low. You still cannot hear it - the tone is
rem below the range human ears can pick up at any level.

tasklist /FI "IMAGENAME eq HeadsetKeepAlive.exe" | find /I "HeadsetKeepAlive.exe" >nul
if not errorlevel 1 (
    echo A copy is already running - run Stop.cmd first, then try again.
    ping -n 6 127.0.0.1 >nul 2>&1
    exit /b 0
)

start "" "%~dp0HeadsetKeepAlive.exe" /a:1024
echo Headset Keep-Alive started in stronger mode.
ping -n 5 127.0.0.1 >nul 2>&1
