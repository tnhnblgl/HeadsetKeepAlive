@echo off
setlocal
rem Puts a shortcut in the Windows Startup folder so the keep-alive runs
rem automatically every time you log in.
rem
rem Optional: pass a strength value to match Start-Stronger.cmd, e.g.
rem     Install-Autostart.cmd 16

set "LINK=%APPDATA%\Microsoft\Windows\Start Menu\Programs\Startup\Headset Keep-Alive.lnk"

set "ARGS="
if not "%~1"=="" set "ARGS=/a:%~1"

if not exist "%~dp0HeadsetKeepAlive.exe" (
    echo ERROR: HeadsetKeepAlive.exe is not next to this script.
    ping -n 6 127.0.0.1 >nul 2>&1
    exit /b 1
)

powershell -NoProfile -ExecutionPolicy Bypass -Command ^
    "$s = (New-Object -ComObject WScript.Shell).CreateShortcut('%LINK%');" ^
    "$s.TargetPath = '%~dp0HeadsetKeepAlive.exe';" ^
    "$s.Arguments = '%ARGS%';" ^
    "$s.WorkingDirectory = '%~dp0';" ^
    "$s.Description = 'Keeps the wireless headset awake';" ^
    "$s.Save()"

if not exist "%LINK%" (
    echo ERROR: the shortcut could not be created.
    ping -n 6 127.0.0.1 >nul 2>&1
    exit /b 1
)

echo Autostart installed.
echo Headset Keep-Alive will now start automatically when you log in.
echo.
echo Keep this folder where it is - moving or deleting it breaks the shortcut.
ping -n 7 127.0.0.1 >nul 2>&1
