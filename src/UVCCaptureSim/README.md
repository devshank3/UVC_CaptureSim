# UVC Capture-Card Simulator

A Windows desktop app that streams images, videos, or a test pattern into a **DirectShow** virtual camera. Capture apps enumerate it as **DirectShow Softcam**.

The camera is [softcam](https://github.com/tshino/softcam) (MIT). This project does not use a Media Foundation virtual camera.

## Layout

```
src/UVCCaptureSim/
  UvcCaptureSim.Core/     Frame playlist, composer, BGR24 conversion, softcam sender
  UvcCaptureSim.App/      Avalonia UI (stock Fluent Dark)
  tools/Install-Softcam.ps1
  tools/smoke.cs
```

## Prerequisites

- .NET 10 SDK
- Visual Studio with the **Desktop development with C++** workload, to build softcam
- ffmpeg on PATH only if you want video files. Images and the test pattern do not need it.

## Run

```powershell
cd src/UVCCaptureSim
dotnet build UVCCaptureSim.slnx
dotnet run --project UvcCaptureSim.App
```

Settings are written to `uvcsim.config.json` next to the executable when streaming starts.

## Virtual camera

```powershell
./tools/Install-Softcam.ps1 -Register
dotnet build UVCCaptureSim.slnx
```

`-Register` builds x64 and Win32, registers both, and writes the `DevicePath` value FlashCap needs so the device is not dropped. softcam advertises a format only while this app is streaming, and only one sender can exist at a time. Start streaming here, then rescan cameras in the capture app.

Remove it later with `./tools/Install-Softcam.ps1 -Unregister`.

## Check the frame pipeline

```powershell
dotnet run tools/smoke.cs
```

That runs without softcam.dll. It checks pacing, the test pattern, resolution rounding, and BGR24 byte order.
