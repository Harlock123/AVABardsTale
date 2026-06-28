using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;

namespace BardsTale.UI.Audio;

/// <summary>
/// A thin wrapper over the Windows Multimedia <c>mciSendString</c> API. MCI plays WAV files
/// natively — and supports any number of concurrently-playing aliases for overlapping sound
/// effects — so the Windows desktop heads need no audio NuGet dependency. (Its <c>waveaudio</c>
/// device has no working <c>repeat</c> flag, so music is looped manually; see <see cref="Restart"/>.)
/// Every call is best-effort: failures are swallowed, since audio is non-essential.
///
/// Set the <c>BT_AUDIO_LOG</c> environment variable to write every MCI command, its return
/// code and the decoded error text to <c>%TEMP%\bardstale_audio.log</c> — used to diagnose
/// audio problems on machines we can't run directly.
/// </summary>
internal static class WinMm
{
    [DllImport("winmm.dll", CharSet = CharSet.Unicode)]
    private static extern int mciSendString(string command, StringBuilder? returnValue, int returnLength, nint callback);

    [DllImport("winmm.dll", CharSet = CharSet.Unicode)]
    private static extern bool mciGetErrorString(int error, StringBuilder buffer, int bufferLength);

    private static readonly bool LogEnabled =
        !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("BT_AUDIO_LOG"));
    private static readonly string LogPath =
        Path.Combine(Path.GetTempPath(), "bardstale_audio.log");
    private static readonly object LogGate = new();

    /// <summary>Sends an MCI command; returns true on success (error code 0).</summary>
    internal static bool Send(string command)
    {
        try
        {
            var code = mciSendString(command, null, 0, 0);
            Log(command, code, null);
            return code == 0;
        }
        catch (Exception ex) { Log(command, -1, ex.Message); return false; }
    }

    /// <summary>Sends an MCI query and returns its response string (empty on failure).</summary>
    internal static string Query(string command)
    {
        try
        {
            var sb = new StringBuilder(128);
            var code = mciSendString(command, sb, sb.Capacity, 0);
            Log(command, code, code == 0 ? sb.ToString() : null);
            return code == 0 ? sb.ToString() : "";
        }
        catch (Exception ex) { Log(command, -1, ex.Message); return ""; }
    }

    // Appends one line per MCI call when BT_AUDIO_LOG is set; otherwise does nothing.
    private static void Log(string command, int code, string? note)
    {
        if (!LogEnabled) return;
        try
        {
            string detail;
            if (code == 0) detail = note is null ? "ok" : $"ok: {note}";
            else
            {
                var sb = new StringBuilder(256);
                detail = mciGetErrorString(code, sb, sb.Capacity) ? sb.ToString() : (note ?? "unknown error");
            }
            lock (LogGate)
                File.AppendAllText(LogPath, $"[{code}] {command}  ->  {detail}{Environment.NewLine}");
        }
        catch { /* logging must never throw */ }
    }

    /// <summary>Opens a WAV under an alias. MCI volume is 0–1000.</summary>
    internal static bool Open(string path, string alias) =>
        Send($"open \"{path}\" type waveaudio alias {alias}");

    internal static void SetVolume(string alias, double volume) =>
        Send($"setaudio {alias} volume to {(int)(System.Math.Clamp(volume, 0, 1) * 1000)}");

    /// <summary>Plays the aliased sound once from the start.</summary>
    internal static void Play(string alias) => Send($"play {alias}");

    /// <summary>Restarts the aliased sound from the top — used to loop music manually
    /// (MCI's <c>waveaudio</c> device has no working <c>repeat</c> flag).</summary>
    internal static void Restart(string alias) => Send($"play {alias} from 0");

    /// <summary>
    /// Plays an aliased device from the start. Re-issuing this on an already-open device is the
    /// way music loops on Windows: the <c>waveaudio</c> device ignores <c>play … repeat</c>
    /// (it returns MCIERR_UNSUPPORTED_FUNCTION and plays nothing), so the caller re-arms this
    /// just before each loop ends — re-seeking an open device is gap-free and needs no re-spawn.
    /// </summary>
    internal static void PlayFromStart(string alias) =>
        Send($"play {alias} from 0");

    internal static void Close(string alias)
    {
        Send($"stop {alias}");
        Send($"close {alias}");
    }

    /// <summary>True when the aliased device has finished (or doesn't exist).</summary>
    internal static bool IsStopped(string alias)
    {
        var mode = Query($"status {alias} mode");
        return mode.Length == 0 || mode.StartsWith("stop", System.StringComparison.OrdinalIgnoreCase);
    }
}
