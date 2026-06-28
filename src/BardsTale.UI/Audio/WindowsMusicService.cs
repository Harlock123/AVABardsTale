using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace BardsTale.UI.Audio;

/// <summary>
/// Windows desktop music: plays each synthesized track through the Multimedia (MCI) API and
/// loops it manually. MCI's <c>waveaudio</c> device ignores the <c>play … repeat</c> flag —
/// it returns MCIERR_UNSUPPORTED_FUNCTION and plays nothing — so instead we keep one device
/// open and re-arm <c>play … from 0</c> a hair before each loop ends. Re-seeking an already-open
/// device is near-instant, so the loop stays gap-free with no per-loop process re-spawn.
/// waveaudio also can't re-volume a playing stream (<c>setaudio … volume</c> is unsupported), so
/// the chosen level is baked into the WAV instead: a volume change re-renders the file and takes
/// effect on the next loop (the same "applies on next loop" semantics as the macOS/Linux backends),
/// so the music slider works without restarting the track mid-loop. No live-volume support, so
/// scene changes get a clean cut rather than a crossfade. A no-op on non-Windows desktops.
/// </summary>
public sealed class WindowsMusicService : IMusicService
{
    private readonly object _gate = new();

    /// <summary>
    /// How far before a loop ends we re-arm the next play, covering MCI/timer jitter without
    /// falling silent. Small enough to land in the loop's faded boundary so it never doubles
    /// the audible tune (clamped for short clips).
    /// </summary>
    private const int OverlapMs = 60;

    private CancellationTokenSource? _cts;
    private GameMusic? _current;
    private string? _alias;     // the open MCI device for the active loop, if any
    private int _gen;           // unique alias suffix per Play, so old/new loops never collide
    private double _volume = 0.45;

    public WindowsMusicService()
    {
        AppDomain.CurrentDomain.ProcessExit += (_, _) => Stop();
    }

    public void Play(GameMusic track)
    {
        if (!OperatingSystem.IsWindows()) return;
        lock (_gate)
        {
            if (_current == track && _cts is { IsCancellationRequested: false }) return;
            StopLocked();
            var durationMs = MusicSynth.BuildPcm(track).Length * 1000L / MusicSynth.SampleRate;
            var alias = $"btmusic{_gen++}";
            var cts = new CancellationTokenSource();
            _alias = alias;
            _cts = cts;
            _current = track;
            Task.Run(() => Loop(alias, track, durationMs, cts.Token));
        }
    }

    public void Stop()
    {
        lock (_gate) StopLocked();
    }

    public void SetVolume(double volume) => Volatile.Write(ref _volume, Math.Clamp(volume, 0, 1));

    // waveaudio can't re-volume a playing stream, so there's no live volume to crossfade.
    public bool SupportsLiveVolume => false;

    private void StopLocked()
    {
        _current = null;
        _cts?.Cancel();
        _cts = null;
        if (_alias is not null) WinMm.Close(_alias); // stop playback now; the loop closes it too (idempotent)
        _alias = null;
    }

    private void Loop(string alias, GameMusic track, long durationMs, CancellationToken ct)
    {
        // Re-arm a touch before the clip ends so re-seeking to 0 bridges the loop without a gap.
        var overlapMs = (int)Math.Min(OverlapMs, durationMs / 8);
        var waitMs = (int)Math.Max(1, durationMs - overlapMs);
        var bakedPct = -1; // the volume% currently rendered into the open device; -1 = none yet
        try
        {
            while (!ct.IsCancellationRequested)
            {
                // At each loop boundary, re-open with a freshly leveled WAV if the volume changed.
                var pct = (int)Math.Round(Volatile.Read(ref _volume) * 100);
                if (pct != bakedPct)
                {
                    WinMm.Close(alias); // no-op on the first pass; the boundary is silent, so gap-free
                    if (!WinMm.Open(FileFor(track, pct), alias)) return;
                    bakedPct = pct;
                }
                WinMm.PlayFromStart(alias);
                if (ct.WaitHandle.WaitOne(waitMs)) break; // woken early when music is stopped
            }
        }
        finally
        {
            WinMm.Close(alias);
        }
    }

    private static string FileFor(GameMusic track, int volumePct)
    {
        var path = Path.Combine(Path.GetTempPath(), $"bardstale_music_{track}_v{volumePct}.wav");
        if (!File.Exists(path))
            File.WriteAllBytes(path, MusicSynth.BuildWav(track, volumePct / 100.0));
        return path;
    }
}
