using System.Diagnostics;
using System.Globalization;
using System.Runtime.InteropServices;
using SkiaSharp;

namespace UvcCaptureSim.Core.Frames;

/// <summary>
/// Decodes a video to raw BGRA with ffmpeg. Playback is paced with <c>-re</c>.
/// The item finishes when the stream ends. If ffmpeg or the file is missing, it finishes immediately.
/// </summary>
public sealed class VideoContent : IPlaylistContent
{
    private readonly string _path;
    private readonly string? _ffmpeg;
    private readonly object _gate = new();

    private Process? _process;
    private Stream? _stdout;
    private Thread? _reader;
    private CancellationTokenSource? _cts;

    private byte[] _reading = [];
    private byte[] _ready = [];
    private SKBitmap? _display;
    private int _frameBytes;
    private bool _hasFrame;
    private bool _displayValid;
    private bool _finished;

    public VideoContent(string path, string? ffmpegPath)
    {
        _path = path;
        _ffmpeg = ffmpegPath;
    }

    public string Name => Path.GetFileName(_path);

    public TimeSpan? FixedDuration => null;

    public void Start(int targetWidth, int targetHeight, double fps)
    {
        Stop();

        _frameBytes = targetWidth * targetHeight * 4;
        _reading = new byte[_frameBytes];
        _ready = new byte[_frameBytes];
        _display = new SKBitmap(new SKImageInfo(targetWidth, targetHeight, SKColorType.Bgra8888, SKAlphaType.Opaque));
        _hasFrame = false;
        _displayValid = false;
        _finished = false;

        if (string.IsNullOrWhiteSpace(_ffmpeg) || !File.Exists(_path))
        {
            _finished = true;
            return;
        }

        var startInfo = new ProcessStartInfo
        {
            FileName = _ffmpeg,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        foreach (var arg in new[]
                 {
                     "-hide_banner", "-loglevel", "error",
                     "-re", "-i", _path,
                     "-f", "rawvideo", "-pix_fmt", "bgra",
                     "-s", $"{targetWidth}x{targetHeight}",
                     "-r", fps.ToString("0.###", CultureInfo.InvariantCulture),
                     "-an", "-",
                 })
        {
            startInfo.ArgumentList.Add(arg);
        }

        try
        {
            _process = Process.Start(startInfo);
        }
        catch
        {
            _finished = true;
            return;
        }

        if (_process is null)
        {
            _finished = true;
            return;
        }

        _stdout = _process.StandardOutput.BaseStream;
        _cts = new CancellationTokenSource();
        _reader = new Thread(ReadLoop) { IsBackground = true, Name = "uvcsim-ffmpeg" };
        _reader.Start();
        _ = Task.Run(() =>
        {
            try { _ = _process.StandardError.ReadToEnd(); } catch { /* keep ffmpeg from blocking */ }
        });
    }

    public SKBitmap? GetFrame(out bool finished)
    {
        lock (_gate)
        {
            finished = _finished;
            if (_hasFrame && _display is not null)
            {
                Marshal.Copy(_ready, 0, _display.GetPixels(), _frameBytes);
                _hasFrame = false;
                _displayValid = true;
            }

            return _displayValid ? _display : null;
        }
    }

    public void Dispose() => Stop();

    private void ReadLoop()
    {
        var stream = _stdout;
        if (stream is null)
            return;

        try
        {
            while (_cts is { IsCancellationRequested: false })
            {
                if (!ReadExact(stream, _reading, _frameBytes))
                    break;

                lock (_gate)
                {
                    (_ready, _reading) = (_reading, _ready);
                    _hasFrame = true;
                }
            }
        }
        catch
        {
            // Treated as end of stream.
        }
        finally
        {
            lock (_gate)
                _finished = true;
        }
    }

    private static bool ReadExact(Stream stream, byte[] buffer, int count)
    {
        var offset = 0;
        while (offset < count)
        {
            var read = stream.Read(buffer, offset, count - offset);
            if (read <= 0)
                return false;
            offset += read;
        }

        return true;
    }

    private void Stop()
    {
        try { _cts?.Cancel(); } catch { /* shutting down */ }

        var process = _process;
        _process = null;
        if (process is not null)
        {
            try
            {
                if (!process.HasExited)
                    process.Kill(entireProcessTree: true);
            }
            catch { /* already gone */ }

            try { process.Dispose(); } catch { /* ignore */ }
        }

        try { _reader?.Join(500); } catch { /* ignore */ }
        _reader = null;

        try { _cts?.Dispose(); } catch { /* ignore */ }
        _cts = null;
        _stdout = null;
        _display?.Dispose();
        _display = null;
    }
}
