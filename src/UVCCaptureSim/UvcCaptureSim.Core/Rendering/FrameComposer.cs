using SkiaSharp;
using UvcCaptureSim.Core.Configuration;

namespace UvcCaptureSim.Core.Rendering;

/// <summary>
/// Scales a source frame into a fixed BGRA canvas (letterbox, crop, or stretch) and optionally
/// paints a status line. The returned bitmap is owned by the composer and reused on the next call.
/// </summary>
public sealed class FrameComposer : IDisposable
{
    private static readonly SKSamplingOptions Sampling = new(SKFilterMode.Linear, SKMipmapMode.Linear);

    private readonly SKFont _overlayFont = new(SKTypeface.Default, 18);
    private readonly SKPaint _overlayText = new() { Color = SKColors.White, IsAntialias = true };
    private readonly SKPaint _overlayBack = new() { Color = new SKColor(0, 0, 0, 150) };

    private SKBitmap? _target;
    private SKCanvas? _canvas;

    public int Width { get; private set; }
    public int Height { get; private set; }

    public void Configure(int width, int height)
    {
        if (_target is not null && Width == width && Height == height)
            return;

        _canvas?.Dispose();
        _target?.Dispose();
        Width = width;
        Height = height;
        _target = new SKBitmap(new SKImageInfo(width, height, SKColorType.Bgra8888, SKAlphaType.Opaque));
        _canvas = new SKCanvas(_target);
    }

    public SKBitmap Compose(SKBitmap? source, ScalingMode mode, string? overlay)
    {
        if (_canvas is null || _target is null)
            throw new InvalidOperationException("Configure the composer before composing a frame.");

        _canvas.Clear(SKColors.Black);

        if (source is { Width: > 0, Height: > 0 })
        {
            var destination = ComputeDestination(source.Width, source.Height, Width, Height, mode);
            using var image = SKImage.FromBitmap(source);
            _canvas.DrawImage(image, destination, Sampling, paint: null);
        }

        if (!string.IsNullOrEmpty(overlay))
            DrawOverlay(overlay);

        _canvas.Flush();
        return _target;
    }

    public void Dispose()
    {
        _canvas?.Dispose();
        _target?.Dispose();
        _overlayFont.Dispose();
        _overlayText.Dispose();
        _overlayBack.Dispose();
    }

    private static SKRect ComputeDestination(int sourceWidth, int sourceHeight, int targetWidth, int targetHeight, ScalingMode mode)
    {
        if (mode == ScalingMode.Stretch)
            return new SKRect(0, 0, targetWidth, targetHeight);

        var scale = mode == ScalingMode.Crop
            ? Math.Max((float)targetWidth / sourceWidth, (float)targetHeight / sourceHeight)
            : Math.Min((float)targetWidth / sourceWidth, (float)targetHeight / sourceHeight);

        var width = sourceWidth * scale;
        var height = sourceHeight * scale;
        var x = (targetWidth - width) / 2f;
        var y = (targetHeight - height) / 2f;
        return new SKRect(x, y, x + width, y + height);
    }

    private void DrawOverlay(string text)
    {
        const float pad = 8f;
        var metrics = _overlayFont.Metrics;
        var lineHeight = metrics.Descent - metrics.Ascent;
        var boxWidth = _overlayFont.MeasureText(text) + pad * 2;
        var boxHeight = lineHeight + pad * 2;
        var top = Height - boxHeight - 12;
        _canvas!.DrawRect(12, top, boxWidth, boxHeight, _overlayBack);
        _canvas.DrawText(text, 12 + pad, top + pad - metrics.Ascent, SKTextAlign.Left, _overlayFont, _overlayText);
    }
}
