using SkiaSharp;

namespace UvcCaptureSim.Core.Frames;

/// <summary>
/// One playlist item. <see cref="Start"/> is called each time the playlist lands on it,
/// then <see cref="GetFrame"/> is polled once per tick.
/// </summary>
public interface IPlaylistContent : IDisposable
{
    string Name { get; }

    /// <summary>
    /// On-screen time for images and patterns. Null means the item runs until
    /// <see cref="GetFrame"/> reports that it is finished (videos).
    /// </summary>
    TimeSpan? FixedDuration { get; }

    void Start(int targetWidth, int targetHeight, double fps);

    /// <summary>
    /// Borrowed BGRA frame, valid until the next call, or null when a frame is not ready yet.
    /// </summary>
    SKBitmap? GetFrame(out bool finished);
}
