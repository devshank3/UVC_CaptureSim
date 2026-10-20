namespace UvcCaptureSim.Core.Frames;

/// <summary>Finds ffmpeg.exe used to decode video playlist items.</summary>
public static class FfmpegLocator
{
    /// <summary>
    /// Looks at the configured path, then <c>ffmpeg\ffmpeg.exe</c> and <c>ffmpeg.exe</c> beside the app, then PATH.
    /// Returns null when ffmpeg is not installed.
    /// </summary>
    public static string? Resolve(string? configuredPath)
    {
        if (!string.IsNullOrWhiteSpace(configuredPath) && File.Exists(configuredPath))
            return configuredPath;

        foreach (var candidate in new[]
                 {
                     Path.Combine(AppContext.BaseDirectory, "ffmpeg", "ffmpeg.exe"),
                     Path.Combine(AppContext.BaseDirectory, "ffmpeg.exe"),
                 })
        {
            if (File.Exists(candidate))
                return candidate;
        }

        var pathVar = Environment.GetEnvironmentVariable("PATH") ?? string.Empty;
        foreach (var dir in pathVar.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
        {
            try
            {
                var candidate = Path.Combine(dir.Trim(), "ffmpeg.exe");
                if (File.Exists(candidate))
                    return candidate;
            }
            catch
            {
                // Ignore a malformed PATH entry.
            }
        }

        return null;
    }
}
