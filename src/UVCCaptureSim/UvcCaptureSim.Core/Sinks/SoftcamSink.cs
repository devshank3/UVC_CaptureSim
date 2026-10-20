using SkiaSharp;
using UvcCaptureSim.Core.Rendering;

namespace UvcCaptureSim.Core.Sinks;

/// <summary>
/// Pushes top-down BGR24 frames into softcam.dll. The registered filter is enumerated as
/// "DirectShow Softcam". Only one sender can exist on the machine at a time.
/// </summary>
public sealed class SoftcamSink : IVirtualCameraSink
{
    private IntPtr _camera;
    private byte[] _buffer = [];
    private int _width;
    private int _height;

    public string BackendName => "DirectShow Softcam";

    public bool IsConnected => _camera != IntPtr.Zero && SafeIsConnected();

    public void Start(int width, int height, double fps)
    {
        _width = width;
        _height = height;
        _buffer = new byte[PixelConverter.Bgr24Size(width, height)];

        try
        {
            _camera = SoftcamInterop.scCreateCamera(width, height, (float)fps);
        }
        catch (DllNotFoundException ex)
        {
            throw new VirtualCameraException(
                "softcam.dll was not found beside the app. Run tools/Install-Softcam.ps1, then rebuild so the DLL is copied next to the executable.",
                ex);
        }
        catch (BadImageFormatException ex)
        {
            throw new VirtualCameraException(
                "softcam.dll is the wrong architecture. This app needs the x64 build from tools/Install-Softcam.ps1.",
                ex);
        }

        if (_camera == IntPtr.Zero)
        {
            throw new VirtualCameraException(
                "Could not create the softcam virtual camera. Another sender may already own it. softcam allows only one instance at a time.");
        }
    }

    public unsafe void SendFrame(SKBitmap frame)
    {
        if (_camera == IntPtr.Zero || frame.Width != _width || frame.Height != _height)
            return;

        PixelConverter.Bgra8888ToBgr24(frame, _buffer);
        fixed (byte* pixels = _buffer)
            SoftcamInterop.scSendFrame(_camera, (IntPtr)pixels);
    }

    public void Dispose()
    {
        if (_camera == IntPtr.Zero)
            return;

        try { SoftcamInterop.scDeleteCamera(_camera); } catch { /* ignore */ }
        _camera = IntPtr.Zero;
    }

    private bool SafeIsConnected()
    {
        try { return SoftcamInterop.scIsConnected(_camera); }
        catch { return false; }
    }
}
