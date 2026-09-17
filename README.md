# Headset Keep-Alive

Stops a wireless headset from switching itself off to save battery.

Many wireless headsets power down after a few quiet minutes. They are waiting for audio, and silence is exactly what tells them to sleep. This keeps the sound device fed with a **10 Hz tone** so the headset always sees audio arriving and its sleep timer never starts.

**You cannot hear it.** Human hearing starts at roughly 20 Hz, and this tone sits below that, so it is inaudible no matter how high the volume goes.

It costs about 0.4% of one CPU core and 20 MB of RAM.

## Install

1. **[Download the latest release](../../releases/latest)** and unzip it anywhere.
2. Double-click **`Start.cmd`**.

Nothing appears on screen. That is intentional — it runs in the background with no window and no tray icon.

Direct download link: **[HeadsetKeepAlive.zip](../../releases/latest/download/HeadsetKeepAlive.zip)**

### Run it automatically at startup

Double-click **`Install-Autostart.cmd`**. It puts a shortcut in your Startup folder, so it runs every time you log in.

Keep the folder where it is afterwards — moving or deleting it breaks the shortcut.

To undo, double-click `Uninstall-Autostart.cmd`.

## Stop it

Double-click **`Stop.cmd`**.

Or by hand: <kbd>Ctrl</kbd>+<kbd>Shift</kbd>+<kbd>Esc</kbd> to open Task Manager, find **Headset Keep-Alive**, right-click, End task.

## If your headset still falls asleep

Windows scales all audio by your volume slider. If the volume is very low, the tone can be scaled down so far that it reaches the headset as pure silence.

1. Double-click `Stop.cmd`
2. Double-click `Start-Stronger.cmd`

That is 8x stronger and still completely inaudible, because what makes this tone unhearable is its pitch, not its quietness.

To autostart the stronger version, open a Command Prompt in the folder and run `Install-Autostart.cmd 1024`.

If it still sleeps, check whether the headset has its own sleep timer in its companion software or on a button combination. No amount of audio overrides a timer built into the hardware.

## How it works

A 10 Hz sine wave is rendered continuously to the default output device through the WinMM `waveOut` API, with four buffers kept permanently queued so the device never sees a gap.

A few details that matter:

- **Why a tone and not silence.** Some headsets count zero-samples in their DSP, so digital silence is detected as "no audio" and they sleep anyway. A tone is unambiguously audio.
- **Why not quiet noise.** Broadband noise has to be kept near-silent to stay unheard, and near-silent signals get rounded away to digital silence when Windows volume is low — the two requirements fight each other. An infrasonic tone is inaudible at any level, so it can be sent strongly enough to always survive the mixer.
- **Seamless looping.** Each buffer holds exactly one whole cycle of the wave, so replaying the same buffers forever produces no discontinuity at the join. A mid-cycle jump would be a step change, and a step is broadband — it would be heard as a click 10 times a second.
- **Follows your default device.** The stream is reopened every 60 seconds, so if you switch the headset on after boot or move the dongle to another port, it moves across on its own within a minute.
- **Single instance.** A named mutex means starting it twice does nothing.

Verified with the Windows audio endpoint peak meter: `0.003910` measured while running against `0.003906` predicted for the default level, and exactly `0.000000` when stopped.

## Requirements

Windows 10 or 11. Nothing to install — it uses the .NET Framework that is already part of Windows.

## Building from source

The release zip includes the full source in `source/`. Run `source\Build.cmd`, which compiles with the C# compiler that ships with Windows. No SDK, no downloads, no dependencies.

From a clone of this repository, run `Build.cmd` in the root instead; it writes to `dist\`.

## A note on antivirus

A small unsigned executable that runs with no window and adds itself to startup is the shape of something suspicious, even though all this one does is play a tone too low to hear.

Windows SmartScreen may warn on first run — click **More info** then **Run anyway**. Your antivirus may also flag it. The full source is included so anyone can read it and rebuild the exe themselves.

## Log

If you want to see what it has been doing:

```
%LOCALAPPDATA%\HeadsetKeepAlive\keepalive.log
```

Paste that into the Explorer address bar.

## Uninstall

1. `Uninstall-Autostart.cmd`
2. `Stop.cmd`
3. Delete the folder.

Nothing is left behind except the small log file above.
