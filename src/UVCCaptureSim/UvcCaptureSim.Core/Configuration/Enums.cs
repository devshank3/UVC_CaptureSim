namespace UvcCaptureSim.Core.Configuration;

/// <summary>Where composed frames are sent.</summary>
public enum VirtualCameraBackend
{
    /// <summary>In-app preview only. No DirectShow device is created.</summary>
    PreviewOnly,

    /// <summary>DirectShow virtual camera through softcam.dll ("DirectShow Softcam").</summary>
    DirectShowSoftcam,
}

/// <summary>How a source frame is fitted into the camera resolution.</summary>
public enum ScalingMode
{
    /// <summary>Keep the aspect ratio and pad with black.</summary>
    Letterbox,

    /// <summary>Keep the aspect ratio, fill the frame, and crop the overflow.</summary>
    Crop,

    /// <summary>Fill the frame and ignore the aspect ratio.</summary>
    Stretch,
}

/// <summary>What a playlist entry plays.</summary>
public enum MediaKind
{
    Image,
    Video,
    TestPattern,
}
