using System.Runtime.InteropServices;
using System.Text;

namespace BardsTale.UI.Audio;

/// <summary>
/// A thin wrapper over the Windows Multimedia <c>mciSendString</c> API. MCI plays WAV files
/// natively — including a seamless built-in loop (<c>play … repeat</c>) for music and any
/// number of concurrently-playing aliases for overlapping sound effects — so the Windows
/// desktop heads need no audio NuGet dependency. Every call is best-effort: failures are
/// swallowed, since audio is non-essential.
/// </summary>
internal static class WinMm
{
    [DllImport("winmm.dll", CharSet = CharSet.Unicode)]
    private static extern int mciSendString(string command, StringBuilder? returnValue, int returnLength, nint callback);

    /// <summary>Sends an MCI command; returns true on success (error code 0).</summary>
    internal static bool Send(string command)
    {
        try { return mciSendString(command, null, 0, 0) == 0; }
        catch { return false; }
    }

    /// <summary>Sends an MCI query and returns its response string (empty on failure).</summary>
    internal static string Query(string command)
    {
        try
        {
            var sb = new StringBuilder(128);
            return mciSendString(command, sb, sb.Capacity, 0) == 0 ? sb.ToString() : "";
        }
        catch { return ""; }
    }

    /// <summary>Opens a WAV under an alias. MCI volume is 0–1000.</summary>
    internal static bool Open(string path, string alias) =>
        Send($"open \"{path}\" type waveaudio alias {alias}");

    internal static void SetVolume(string alias, double volume) =>
        Send($"setaudio {alias} volume to {(int)(System.Math.Clamp(volume, 0, 1) * 1000)}");

    internal static void Play(string alias, bool loop) =>
        Send($"play {alias}{(loop ? " repeat" : "")}");

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
