using System.Buffers;
using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Threading;
using SkiaSharp;

namespace UvcCaptureSim.App.Views;

/// <summary>
/// Copies each composed frame off the engine thread, then blits the latest one into the preview image.
/// </summary>
internal sealed class PreviewPresenter : IDisposable
{
    private readonly Image _image;
    private readonly object _gate = new();
    private WriteableBitmap? _bitmap;
    private byte[]? _pending;
    private int _width;
    private int _height;
    private int _stride;
    private int _length;
    private int _queued;
    private int _closed;

    public PreviewPresenter(Image image) => _image = image;

    public void OnFrameRendered(SKBitmap frame)
    {
        if (Volatile.Read(ref _closed) == 1)
            return;

        var stride = frame.RowBytes;
        var height = frame.Height;
        var length = stride * height;
        var source = frame.GetPixels();
        if (source == IntPtr.Zero || length <= 0)
            return;

        var buffer = ArrayPool<byte>.Shared.Rent(length);
        Marshal.Copy(source, buffer, 0, length);

        byte[]? discarded;
        lock (_gate)
        {
            discarded = _pending;
            _pending = buffer;
            _width = frame.Width;
            _height = height;
            _stride = stride;
            _length = length;
        }

        if (discarded is not null)
            ArrayPool<byte>.Shared.Return(discarded);

        if (Interlocked.CompareExchange(ref _queued, 1, 0) == 0)
            Dispatcher.UIThread.Post(Present, DispatcherPriority.Render);
    }

    public void Dispose()
    {
        Interlocked.Exchange(ref _closed, 1);
        byte[]? pending;
        lock (_gate)
        {
            pending = _pending;
            _pending = null;
        }

        if (pending is not null)
            ArrayPool<byte>.Shared.Return(pending);
    }

    private void Present()
    {
        if (Volatile.Read(ref _closed) == 1)
            return;

        byte[]? buffer;
        int width;
        int height;
        int stride;
        int length;
        lock (_gate)
        {
            buffer = _pending;
            _pending = null;
            width = _width;
            height = _height;
            stride = _stride;
            length = _length;
        }

        if (buffer is not null)
        {
            try
            {
                Blit(buffer, width, height, stride, length);
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(buffer);
            }
        }

        Interlocked.Exchange(ref _queued, 0);
        var more = false;
        lock (_gate)
            more = _pending is not null;

        if (more && Interlocked.CompareExchange(ref _queued, 1, 0) == 0)
            Dispatcher.UIThread.Post(Present, DispatcherPriority.Render);
    }

    private void Blit(byte[] pixels, int width, int height, int stride, int length)
    {
        if (width <= 0 || height <= 0 || length < stride * height)
            return;

        if (_bitmap is null || _bitmap.PixelSize.Width != width || _bitmap.PixelSize.Height != height)
        {
            _bitmap?.Dispose();
            _bitmap = new WriteableBitmap(
                new PixelSize(width, height),
                new Vector(96, 96),
                PixelFormat.Bgra8888,
                AlphaFormat.Opaque);
            _image.Source = _bitmap;
        }

        using var locked = _bitmap.Lock();
        var destinationStride = locked.RowBytes;
        var rowBytes = Math.Min(stride, destinationStride);
        if (rowBytes <= 0)
            return;

        unsafe
        {
            fixed (byte* source = pixels)
            {
                var destination = (byte*)locked.Address;
                for (var y = 0; y < height; y++)
                {
                    Buffer.MemoryCopy(
                        source + (y * stride),
                        destination + (y * destinationStride),
                        destinationStride,
                        rowBytes);
                }
            }
        }

        _image.InvalidateVisual();
    }
}
