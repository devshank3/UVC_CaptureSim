using System.Diagnostics;
using SkiaSharp;

namespace UvcCaptureSim.Core.Frames;

/// <summary>
/// Walks playlist items on one thread. Images and patterns advance after their duration.
/// Videos advance when the decoder finishes. Optionally loops.
/// </summary>
public sealed class PlaylistFrameSource : IDisposable
{
    private readonly IReadOnlyList<IPlaylistContent> _contents;
    private readonly bool _loop;
    private readonly Stopwatch _clock = new();

    private int _width;
    private int _height;
    private double _fps;
    private int _index;
    private TimeSpan _itemStart;
    private bool _advancePending;
    private bool _exhausted;

    public PlaylistFrameSource(IReadOnlyList<IPlaylistContent> contents, bool loop)
    {
        _contents = contents;
        _loop = loop;
    }

    public string CurrentName => _contents.Count == 0 ? "(empty)" : _contents[_index].Name;

    public void Start(int targetWidth, int targetHeight, double fps)
    {
        _width = targetWidth;
        _height = targetHeight;
        _fps = fps;
        _index = 0;
        _advancePending = false;
        _exhausted = _contents.Count == 0;
        _clock.Restart();

        if (_exhausted)
            return;

        _contents[0].Start(targetWidth, targetHeight, fps);
        _itemStart = _clock.Elapsed;
    }

    public SKBitmap? GetFrame()
    {
        if (_exhausted || _contents.Count == 0)
            return null;

        if (_advancePending)
        {
            _advancePending = false;
            if (!MoveNext())
                return null;
        }

        for (var attempt = 0; attempt <= _contents.Count; attempt++)
        {
            var current = _contents[_index];
            var frame = current.GetFrame(out var contentFinished);
            var durationElapsed = current.FixedDuration is { } duration
                                  && _clock.Elapsed - _itemStart >= duration;

            if (frame is null)
            {
                if (contentFinished || durationElapsed)
                {
                    if (!MoveNext())
                        return null;
                    continue;
                }

                return null;
            }

            if (contentFinished || durationElapsed)
                _advancePending = true;

            return frame;
        }

        return null;
    }

    public void Dispose()
    {
        foreach (var content in _contents)
        {
            try { content.Dispose(); } catch { /* ignore */ }
        }
    }

    private bool MoveNext()
    {
        _index++;
        if (_index >= _contents.Count)
        {
            if (!_loop)
            {
                _exhausted = true;
                return false;
            }

            _index = 0;
        }

        _contents[_index].Start(_width, _height, _fps);
        _itemStart = _clock.Elapsed;
        return true;
    }
}
