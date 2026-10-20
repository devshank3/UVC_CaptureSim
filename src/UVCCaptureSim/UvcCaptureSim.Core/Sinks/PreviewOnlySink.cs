using SkiaSharp;

namespace UvcCaptureSim.Core.Sinks;

/// <summary>Drops frames. Used for preview-only mode and when softcam cannot start.</summary>
public sealed class PreviewOnlySink : IVirtualCameraSink
{
    public string BackendName => "Preview only";

    public bool IsConnected => false;

    public void Start(int width, int height, double fps)
    {
    }

    public void SendFrame(SKBitmap frame)
    {
    }

    public void Dispose()
    {
    }
}
