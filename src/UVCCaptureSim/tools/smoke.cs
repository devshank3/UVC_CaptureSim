#:project ../UvcCaptureSim.Core/UvcCaptureSim.Core.csproj
#:property TargetFramework=net10.0-windows
// Headless check for the frame pipeline. From src/UVCCaptureSim:
//   dotnet run tools/smoke.cs
using System.Threading;
using SkiaSharp;
using UvcCaptureSim.Core.Configuration;
using UvcCaptureSim.Core.Engine;
using UvcCaptureSim.Core.Rendering;

var config = new SimulatorConfig
{
    Width = 322,
    Height = 240,
    FramesPerSecond = 30,
    Backend = VirtualCameraBackend.PreviewOnly,
    ShowOverlay = true,
};
config.Playlist.Add(new MediaItem { Kind = MediaKind.TestPattern, DurationSeconds = 5 });

var frames = 0;
var nonBlack = 0;
var width = 0;
var height = 0;

using var engine = new SimulatorEngine();
engine.StatusChanged += status => Console.WriteLine("STATUS: " + status);
engine.FrameRendered += bitmap =>
{
    Interlocked.Increment(ref frames);
    width = bitmap.Width;
    height = bitmap.Height;
    var color = bitmap.GetPixel(bitmap.Width / 2, bitmap.Height / 4);
    if (color.Red + color.Green + color.Blue > 0)
        Interlocked.Increment(ref nonBlack);
};

engine.Start(config);
Thread.Sleep(700);
engine.StopAsync().GetAwaiter().GetResult();

Console.WriteLine($"frames={frames} nonBlack={nonBlack} size={width}x{height} (expected 320x240)");

using var probe = new SKBitmap(new SKImageInfo(320, 240, SKColorType.Bgra8888, SKAlphaType.Opaque));
using (var canvas = new SKCanvas(probe))
    canvas.Clear(new SKColor(10, 20, 30));

var bgr = new byte[PixelConverter.Bgr24Size(320, 240)];
PixelConverter.Bgra8888ToBgr24(probe, bgr);
Console.WriteLine($"bgr24 first pixel B,G,R = {bgr[0]},{bgr[1]},{bgr[2]} (expected 30,20,10)");

var ok = frames > 5
         && nonBlack > 0
         && width == 320
         && height == 240
         && bgr[0] == 30
         && bgr[1] == 20
         && bgr[2] == 10;

Console.WriteLine(ok ? "SMOKE OK" : "SMOKE FAILED");
Environment.Exit(ok ? 0 : 1);
