using SkiaSharp;

namespace UvcCaptureSim.Core.Sinks;

/// <summary>A destination for composed BGRA frames.</summary>
public interface IVirtualCameraSink : IDisposable
{
    string BackendName { get; }

    /// <summary>Whether a capture app currently has the virtual camera open. Always false for preview-only.</summary>
    bool IsConnected { get; }

    void Start(int width, int height, double fps);

    void SendFrame(SKBitmap frame);
}
