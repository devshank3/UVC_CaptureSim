namespace UvcCaptureSim.Core.Configuration;

/// <summary>One playlist entry: an image, a video file, or the generated test pattern.</summary>
public sealed class MediaItem
{
    public MediaKind Kind { get; set; } = MediaKind.Image;

    /// <summary>Absolute path for an image or video. Unused for <see cref="MediaKind.TestPattern"/>.</summary>
    public string Path { get; set; } = string.Empty;

    /// <summary>
    /// How long to show this item. Images and test patterns use this, or
    /// <see cref="SimulatorConfig.SecondsPerImage"/> when it is null. Videos play to the end.
    /// </summary>
    public double? DurationSeconds { get; set; }

    public override string ToString() => Kind switch
    {
        MediaKind.TestPattern => "Test pattern",
        MediaKind.Image => "Image  ·  " + System.IO.Path.GetFileName(Path),
        MediaKind.Video => "Video  ·  " + System.IO.Path.GetFileName(Path),
        _ => System.IO.Path.GetFileName(Path),
    };
}

/// <summary>Settings persisted as <c>uvcsim.config.json</c> next to the executable.</summary>
public sealed class SimulatorConfig
{
    /// <summary>Output width. The engine rounds it down to a multiple of 4 (softcam requirement).</summary>
    public int Width { get; set; } = 1280;

    /// <summary>Output height. The engine rounds it down to a multiple of 4.</summary>
    public int Height { get; set; } = 720;

    public double FramesPerSecond { get; set; } = 30;

    public VirtualCameraBackend Backend { get; set; } = VirtualCameraBackend.DirectShowSoftcam;

    public ScalingMode Scaling { get; set; } = ScalingMode.Letterbox;

    /// <summary>Display time for images and test patterns that do not set their own duration.</summary>
    public double SecondsPerImage { get; set; } = 5;

    public bool LoopPlaylist { get; set; } = true;

    /// <summary>Burn a clock, resolution, and frame rate into the corner of each frame.</summary>
    public bool ShowOverlay { get; set; } = true;

    /// <summary>
    /// Explicit ffmpeg.exe path. When null, the app looks beside itself and on PATH.
    /// Video items are skipped when ffmpeg cannot be found.
    /// </summary>
    public string? FfmpegPath { get; set; }

    public List<MediaItem> Playlist { get; set; } = [];
}
