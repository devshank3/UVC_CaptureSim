using SkiaSharp;

namespace UvcCaptureSim.Core.Frames;

/// <summary>A still image, decoded once and held for a fixed duration.</summary>
public sealed class ImageContent : IPlaylistContent
{
    private readonly string _path;
    private SKBitmap? _bitmap;
    private bool _decodeAttempted;

    public ImageContent(string path, TimeSpan duration)
    {
        _path = path;
        FixedDuration = duration;
    }

    public string Name => Path.GetFileName(_path);

    public TimeSpan? FixedDuration { get; }

    public void Start(int targetWidth, int targetHeight, double fps)
    {
        if (_decodeAttempted)
            return;

        _decodeAttempted = true;
        try
        {
            if (File.Exists(_path))
                _bitmap = SKBitmap.Decode(_path);
        }
        catch
        {
            _bitmap = null;
        }
    }

    public SKBitmap? GetFrame(out bool finished)
    {
        finished = _bitmap is null;
        return _bitmap;
    }

    public void Dispose() => _bitmap?.Dispose();
}
