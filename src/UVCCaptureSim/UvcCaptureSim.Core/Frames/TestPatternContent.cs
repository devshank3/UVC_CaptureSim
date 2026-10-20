using System.Diagnostics;
using SkiaSharp;

namespace UvcCaptureSim.Core.Frames;

/// <summary>Animated colour bars, a grey ramp, and a sweeping bar. No external files required.</summary>
public sealed class TestPatternContent : IPlaylistContent
{
    private static readonly SKColor[] Bars =
    [
        new(192, 192, 192), new(192, 192, 0), new(0, 192, 192), new(0, 192, 0),
        new(192, 0, 192), new(192, 0, 0), new(0, 0, 192),
    ];

    private readonly SKPaint _fill = new() { IsAntialias = false };
    private readonly SKPaint _sweep = new() { Color = new SKColor(255, 255, 255, 90) };
    private readonly SKPaint _textPaint = new() { Color = SKColors.White, IsAntialias = true };
    private readonly SKFont _titleFont = new(SKTypeface.Default, 28);
    private readonly Stopwatch _clock = new();

    private SKBitmap? _bitmap;
    private SKCanvas? _canvas;
    private int _width;
    private int _height;
    private long _frame;

    public TestPatternContent(TimeSpan? duration) => FixedDuration = duration;

    public string Name => "Test pattern";

    public TimeSpan? FixedDuration { get; }

    public void Start(int targetWidth, int targetHeight, double fps)
    {
        if (_bitmap is null || _width != targetWidth || _height != targetHeight)
        {
            _canvas?.Dispose();
            _bitmap?.Dispose();
            _width = targetWidth;
            _height = targetHeight;
            _bitmap = new SKBitmap(new SKImageInfo(targetWidth, targetHeight, SKColorType.Bgra8888, SKAlphaType.Opaque));
            _canvas = new SKCanvas(_bitmap);
        }

        _frame = 0;
        _clock.Restart();
    }

    public SKBitmap? GetFrame(out bool finished)
    {
        finished = false;
        Render();
        return _bitmap;
    }

    private void Render()
    {
        if (_canvas is null)
            return;

        var barWidth = _width / (float)Bars.Length;
        for (var i = 0; i < Bars.Length; i++)
        {
            _fill.Color = Bars[i];
            _canvas.DrawRect(i * barWidth, 0, barWidth + 1, _height * 0.75f, _fill);
        }

        for (var x = 0; x < _width; x += 4)
        {
            var level = (byte)(x * 255 / Math.Max(1, _width));
            _fill.Color = new SKColor(level, level, level);
            _canvas.DrawRect(x, _height * 0.75f, 4, _height * 0.25f, _fill);
        }

        var sweepX = (float)(_clock.Elapsed.TotalSeconds * _width / 3.0 % _width);
        _canvas.DrawRect(sweepX, 0, Math.Max(3f, _width / 160f), _height, _sweep);
        _canvas.DrawText("UVC CAPTURE CARD", 24, 48, SKTextAlign.Left, _titleFont, _textPaint);
        _canvas.DrawText($"frame {_frame++}    {_width}x{_height}", 24, 84, SKTextAlign.Left, _titleFont, _textPaint);
        _canvas.Flush();
    }

    public void Dispose()
    {
        _canvas?.Dispose();
        _bitmap?.Dispose();
        _fill.Dispose();
        _sweep.Dispose();
        _textPaint.Dispose();
        _titleFont.Dispose();
    }
}
