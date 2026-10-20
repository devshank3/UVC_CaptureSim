using System.Diagnostics;
using System.Globalization;
using SkiaSharp;
using UvcCaptureSim.Core.Configuration;
using UvcCaptureSim.Core.Frames;
using UvcCaptureSim.Core.Rendering;
using UvcCaptureSim.Core.Sinks;

namespace UvcCaptureSim.Core.Engine;

/// <summary>
/// Paces playlist frames to the selected sink and raises each composed frame for the in-app preview.
/// Start and stop are safe to call from the UI thread. The loop itself runs in the background.
/// </summary>
public sealed class SimulatorEngine : IDisposable
{
    private readonly object _gate = new();

    private CancellationTokenSource? _cts;
    private Task? _loop;
    private FrameComposer? _composer;
    private PlaylistFrameSource? _playlist;
    private IVirtualCameraSink? _sink;
    private ScalingMode _scaling;
    private bool _showOverlay;

    public bool IsRunning { get; private set; }
    public int Width { get; private set; }
    public int Height { get; private set; }
    public double Fps { get; private set; }
    public string BackendName => _sink?.BackendName ?? "Idle";
    public bool IsConnected => _sink?.IsConnected ?? false;
    public string CurrentItemName => _playlist?.CurrentName ?? "—";

    /// <summary>
    /// Raised on the loop thread. Handlers must copy the pixels before returning.
    /// The bitmap is reused for the next frame.
    /// </summary>
    public event Action<SKBitmap>? FrameRendered;

    public event Action<string>? StatusChanged;

    public void Start(SimulatorConfig config)
    {
        lock (_gate)
        {
            if (IsRunning)
                return;

            Width = Math.Max(4, config.Width & ~3);
            Height = Math.Max(4, config.Height & ~3);
            Fps = config.FramesPerSecond is > 0 and <= 240 ? config.FramesPerSecond : 30;
            _scaling = config.Scaling;
            _showOverlay = config.ShowOverlay;

            _composer = new FrameComposer();
            _composer.Configure(Width, Height);
            _playlist = new PlaylistFrameSource(BuildContents(config), config.LoopPlaylist);
            _playlist.Start(Width, Height, Fps);
            _sink = CreateSink(config);

            try
            {
                _sink.Start(Width, Height, Fps);
                var hint = _sink is SoftcamSink
                    ? " Start this stream before the capture app rescans its cameras."
                    : string.Empty;
                Raise($"Streaming {_sink.BackendName} · {Width}×{Height} @ {Fps.ToString("0.#", CultureInfo.InvariantCulture)} fps.{hint}");
            }
            catch (VirtualCameraException ex)
            {
                try { _sink.Dispose(); } catch { /* ignore */ }
                _sink = new PreviewOnlySink();
                _sink.Start(Width, Height, Fps);
                Raise(ex.Message + " Showing the in-app preview only.");
            }

            _cts = new CancellationTokenSource();
            IsRunning = true;
            _loop = Task.Run(() => RunLoop(_cts.Token));
        }
    }

    public async Task StopAsync()
    {
        Task? loop;
        lock (_gate)
        {
            if (!IsRunning)
                return;
            IsRunning = false;
            try { _cts?.Cancel(); } catch { /* ignore */ }
            loop = _loop;
        }

        if (loop is not null)
        {
            try { await loop.ConfigureAwait(false); } catch { /* loop faults are reported via status */ }
        }

        lock (_gate)
        {
            try { _sink?.Dispose(); } catch { /* ignore */ }
            try { _playlist?.Dispose(); } catch { /* ignore */ }
            _composer?.Dispose();
            _cts?.Dispose();
            _sink = null;
            _playlist = null;
            _composer = null;
            _cts = null;
            _loop = null;
        }

        Raise("Stopped.");
    }

    public void Dispose() => StopAsync().GetAwaiter().GetResult();

    private void RunLoop(CancellationToken cancellationToken)
    {
        var interval = TimeSpan.FromSeconds(1.0 / Fps);
        var clock = Stopwatch.StartNew();
        var next = clock.Elapsed;

        while (!cancellationToken.IsCancellationRequested)
        {
            var source = _playlist!.GetFrame();
            var overlay = _showOverlay
                ? $"{DateTime.Now:HH:mm:ss}   {Width}×{Height} @ {Fps.ToString("0.#", CultureInfo.InvariantCulture)} fps   {_sink!.BackendName}"
                : null;
            var composed = _composer!.Compose(source, _scaling, overlay);

            try { _sink!.SendFrame(composed); } catch { /* one bad frame should not stop the stream */ }

            FrameRendered?.Invoke(composed);

            next += interval;
            var delay = next - clock.Elapsed;
            if (delay > TimeSpan.Zero)
            {
                if (cancellationToken.WaitHandle.WaitOne(delay))
                    break;
            }
            else
            {
                next = clock.Elapsed;
            }
        }
    }

    private static List<IPlaylistContent> BuildContents(SimulatorConfig config)
    {
        var ffmpeg = FfmpegLocator.Resolve(config.FfmpegPath);
        var contents = new List<IPlaylistContent>();

        foreach (var item in config.Playlist)
        {
            var duration = TimeSpan.FromSeconds(item.DurationSeconds ?? config.SecondsPerImage);
            switch (item.Kind)
            {
                case MediaKind.Image:
                    contents.Add(new ImageContent(item.Path, duration));
                    break;
                case MediaKind.Video:
                    contents.Add(new VideoContent(item.Path, ffmpeg));
                    break;
                case MediaKind.TestPattern:
                    contents.Add(new TestPatternContent(duration));
                    break;
            }
        }

        if (contents.Count == 0)
            contents.Add(new TestPatternContent(null));

        return contents;
    }

    private static IVirtualCameraSink CreateSink(SimulatorConfig config) =>
        config.Backend == VirtualCameraBackend.DirectShowSoftcam
            ? new SoftcamSink()
            : new PreviewOnlySink();

    private void Raise(string status) => StatusChanged?.Invoke(status);
}
