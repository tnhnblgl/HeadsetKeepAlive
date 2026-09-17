// HeadsetKeepAlive - stops a wireless headset from falling asleep.
//
// Many wireless headsets power themselves down after a few minutes without
// audio. This program keeps the default Windows output device continuously fed
// with a 10 Hz tone, so from the headset's point of view audio is always
// playing and its sleep timer never starts.
//
// 10 Hz is infrasound - below the ~20 Hz floor of human hearing - so the tone
// is inaudible however loud it is made. That matters: broadband noise would
// have to be kept near-silent to stay unheard, and near-silent signals get
// rounded away to digital silence when the Windows volume is low. A tone the
// ear cannot reach can be sent at a level that always survives the mixer.
//
// Build with Build.cmd - it uses the C# compiler that already ships with
// Windows, so there is nothing to install.

using System;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading;

// AssemblyTitle becomes the file description, which is the name Task Manager
// shows for a windowless background process.
[assembly: AssemblyTitle("Headset Keep-Alive")]
[assembly: AssemblyDescription("Prevents a wireless headset from entering battery-save sleep")]
[assembly: AssemblyProduct("Headset Keep-Alive")]
[assembly: AssemblyVersion("1.0.0.0")]
[assembly: AssemblyFileVersion("1.0.0.0")]

internal static class Program
{
    // The stream is torn down and reopened this often so that it re-binds to
    // whatever the default output device is right now - the headset switched on
    // after boot, the dongle re-plugged, and so on. The gap is a few
    // milliseconds, far below any headset sleep timer.
    private const int RebindSeconds = 60;

    private static int Main(string[] args)
    {
        bool firstInstance;
        using (new Mutex(true, @"Local\HeadsetKeepAlive.SingleInstance", out firstInstance))
        {
            if (!firstInstance) return 0;   // already running, nothing to do

            int amplitude = ParseAmplitude(args);
            Log.Write("started (amplitude=" + amplitude + " LSB)");

            while (true)
            {
                KeepAliveStream stream;
                try
                {
                    stream = KeepAliveStream.Open(amplitude);
                }
                catch (Exception ex)
                {
                    // No usable output device yet - keep retrying quietly.
                    Log.Throttled("cannot open output device: " + ex.Message);
                    Thread.Sleep(5000);
                    continue;
                }

                try
                {
                    DateTime rebindAt = DateTime.UtcNow.AddSeconds(RebindSeconds);
                    while (DateTime.UtcNow < rebindAt)
                    {
                        stream.Pump();
                        Thread.Sleep(25);
                    }
                }
                catch (Exception ex)
                {
                    // Device removed or driver reset - fall through and reopen.
                    Log.Throttled("stream lost: " + ex.Message);
                    Thread.Sleep(2000);
                }
                finally
                {
                    stream.Dispose();
                }
            }
        }
    }

    // /a:N sets the tone level in LSB. The default of 128 is about -48 dBFS.
    // Because the tone is infrasonic none of this is audible; the level only
    // decides how much headroom there is before a low Windows volume setting
    // scales the signal down into digital silence. Raise it if a headset still
    // sleeps, lower it if a headphone driver audibly flutters.
    private static int ParseAmplitude(string[] args)
    {
        foreach (string arg in args)
        {
            if (!arg.StartsWith("/a:", StringComparison.OrdinalIgnoreCase) &&
                !arg.StartsWith("-a:", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            int value;
            if (int.TryParse(arg.Substring(3), out value))
            {
                return Math.Max(1, Math.Min(1024, value));
            }
        }

        return 128;
    }
}

/// <summary>
/// A never-ending stream of an inaudible infrasonic tone sent to the default
/// output device. Several buffers are kept queued at all times so the device
/// never sees a gap.
/// </summary>
internal sealed class KeepAliveStream : IDisposable
{
    private const int SampleRate = 44100;
    private const int Channels = 2;
    private const int BytesPerSample = 2;
    private const int BufferCount = 4;

    // Each buffer holds exactly one whole cycle of the tone, so the same
    // buffers can be replayed forever with no discontinuity at the join. A
    // mid-cycle jump would be a step change in the waveform, and a step is
    // broadband - it would be heard as a click 10 times a second.
    // 100 ms per cycle gives a 10 Hz tone, comfortably below hearing.
    private const int BufferMilliseconds = 100;
    private const int BufferBytes = SampleRate * Channels * BytesPerSample * BufferMilliseconds / 1000;

    private static readonly int HeaderSize = Marshal.SizeOf(typeof(WaveHeader));
    private static readonly int FlagsOffset = Marshal.OffsetOf(typeof(WaveHeader), "dwFlags").ToInt32();
    private static bool openLogged;

    private readonly IntPtr[] headers = new IntPtr[BufferCount];
    private readonly IntPtr[] buffers = new IntPtr[BufferCount];
    private IntPtr device;
    private bool disposed;

    private KeepAliveStream()
    {
    }

    public static KeepAliveStream Open(int amplitude)
    {
        var stream = new KeepAliveStream();
        try
        {
            stream.Start(amplitude);
        }
        catch
        {
            stream.Dispose();
            throw;
        }

        if (!openLogged)
        {
            openLogged = true;
            Log.Write("output stream opened - headset will be kept awake");
        }

        return stream;
    }

    /// <summary>
    /// Re-queues every buffer the device has finished playing. Called often
    /// enough that the queue never drains.
    /// </summary>
    public void Pump()
    {
        for (int i = 0; i < BufferCount; i++)
        {
            int flags = Marshal.ReadInt32(headers[i], FlagsOffset);
            if ((flags & WHDR_DONE) == 0) continue;

            // Clear only the done bit - WHDR_PREPARED must survive so the
            // buffer can be re-written without preparing it again.
            Marshal.WriteInt32(headers[i], FlagsOffset, flags & ~WHDR_DONE);
            Check(waveOutWrite(device, headers[i], HeaderSize), "waveOutWrite");
        }
    }

    public void Dispose()
    {
        if (disposed) return;
        disposed = true;

        if (device != IntPtr.Zero)
        {
            waveOutReset(device);   // marks every queued buffer as done
            for (int i = 0; i < BufferCount; i++)
            {
                if (headers[i] != IntPtr.Zero) waveOutUnprepareHeader(device, headers[i], HeaderSize);
            }

            waveOutClose(device);
            device = IntPtr.Zero;
        }

        for (int i = 0; i < BufferCount; i++)
        {
            if (headers[i] != IntPtr.Zero)
            {
                Marshal.FreeHGlobal(headers[i]);
                headers[i] = IntPtr.Zero;
            }

            if (buffers[i] != IntPtr.Zero)
            {
                Marshal.FreeHGlobal(buffers[i]);
                buffers[i] = IntPtr.Zero;
            }
        }
    }

    private void Start(int amplitude)
    {
        var format = new WaveFormat
        {
            wFormatTag = WAVE_FORMAT_PCM,
            nChannels = Channels,
            nSamplesPerSec = SampleRate,
            nAvgBytesPerSec = SampleRate * Channels * BytesPerSample,
            nBlockAlign = Channels * BytesPerSample,
            wBitsPerSample = BytesPerSample * 8,
            cbSize = 0
        };

        // WAVE_MAPPER resolves to whichever device is the default right now.
        Check(waveOutOpen(out device, WAVE_MAPPER, ref format, IntPtr.Zero, IntPtr.Zero, CALLBACK_NULL), "waveOutOpen");

        for (int i = 0; i < BufferCount; i++)
        {
            buffers[i] = Marshal.AllocHGlobal(BufferBytes);
            Fill(buffers[i], amplitude);

            headers[i] = Marshal.AllocHGlobal(HeaderSize);
            Marshal.StructureToPtr(
                new WaveHeader { lpData = buffers[i], dwBufferLength = BufferBytes },
                headers[i],
                false);

            Check(waveOutPrepareHeader(device, headers[i], HeaderSize), "waveOutPrepareHeader");
            Check(waveOutWrite(device, headers[i], HeaderSize), "waveOutWrite");
        }
    }

    /// <summary>
    /// Fills a buffer with exactly one cycle of a sine wave, peaking at
    /// +/- amplitude least-significant bits, identical on both channels.
    /// </summary>
    private static void Fill(IntPtr buffer, int amplitude)
    {
        int frames = BufferBytes / (Channels * BytesPerSample);
        var samples = new short[BufferBytes / BytesPerSample];

        for (int frame = 0; frame < frames; frame++)
        {
            var value = (short)Math.Round(amplitude * Math.Sin(2.0 * Math.PI * frame / frames));
            for (int channel = 0; channel < Channels; channel++)
            {
                samples[(frame * Channels) + channel] = value;
            }
        }

        Marshal.Copy(samples, 0, buffer, samples.Length);
    }

    private static void Check(int result, string call)
    {
        if (result != MMSYSERR_NOERROR)
        {
            throw new InvalidOperationException(call + " failed, MMSYSERR " + result);
        }
    }

    // --- winmm interop ---

    private const int WAVE_MAPPER = -1;
    private const short WAVE_FORMAT_PCM = 1;
    private const int CALLBACK_NULL = 0x0;
    private const int WHDR_DONE = 0x1;
    private const int MMSYSERR_NOERROR = 0;

    [StructLayout(LayoutKind.Sequential)]
    private struct WaveFormat
    {
        public short wFormatTag;
        public short nChannels;
        public int nSamplesPerSec;
        public int nAvgBytesPerSec;
        public short nBlockAlign;
        public short wBitsPerSample;
        public short cbSize;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct WaveHeader
    {
        public IntPtr lpData;
        public int dwBufferLength;
        public int dwBytesRecorded;
        public IntPtr dwUser;
        public int dwFlags;
        public int dwLoops;
        public IntPtr lpNext;
        public IntPtr reserved;
    }

    [DllImport("winmm.dll")]
    private static extern int waveOutOpen(out IntPtr handle, int deviceId, ref WaveFormat format,
        IntPtr callback, IntPtr instance, int flags);

    [DllImport("winmm.dll")]
    private static extern int waveOutPrepareHeader(IntPtr handle, IntPtr header, int headerSize);

    [DllImport("winmm.dll")]
    private static extern int waveOutUnprepareHeader(IntPtr handle, IntPtr header, int headerSize);

    [DllImport("winmm.dll")]
    private static extern int waveOutWrite(IntPtr handle, IntPtr header, int headerSize);

    [DllImport("winmm.dll")]
    private static extern int waveOutReset(IntPtr handle);

    [DllImport("winmm.dll")]
    private static extern int waveOutClose(IntPtr handle);
}

/// <summary>
/// Minimal log file. The program has no window, so this is the only way to see
/// what it is doing.
/// </summary>
internal static class Log
{
    private const long MaxBytes = 64 * 1024;

    private static readonly string LogFile = Path.Combine(
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "HeadsetKeepAlive"),
        "keepalive.log");

    private static string lastThrottled;
    private static DateTime lastThrottledAt;

    public static void Write(string message)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(LogFile));

            var file = new FileInfo(LogFile);
            if (file.Exists && file.Length > MaxBytes) file.Delete();

            File.AppendAllText(LogFile, DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + "  " + message + Environment.NewLine);
        }
        catch
        {
            // Logging must never take the program down.
        }
    }

    /// <summary>
    /// Writes a message at most once every ten minutes, so a device that stays
    /// unavailable does not fill the log.
    /// </summary>
    public static void Throttled(string message)
    {
        if (message == lastThrottled && DateTime.UtcNow - lastThrottledAt < TimeSpan.FromMinutes(10)) return;

        lastThrottled = message;
        lastThrottledAt = DateTime.UtcNow;
        Write(message);
    }
}
