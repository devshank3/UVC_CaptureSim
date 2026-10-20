<#
.SYNOPSIS
    Builds and registers the DirectShow softcam virtual camera (tshino/softcam, MIT).

.DESCRIPTION
    Clones softcam, builds Release x64 and Win32, copies the x64 DLL next to the app project,
    and with -Register registers both DLLs. Registration also writes the DevicePath value FlashCap
    needs in order to list "DirectShow Softcam".

    Requires git and Visual Studio with the Desktop development with C++ workload.
    -Register / -Unregister self-elevate only the registration step.

.EXAMPLE
    ./tools/Install-Softcam.ps1 -Register

.EXAMPLE
    ./tools/Install-Softcam.ps1 -Unregister
#>
[CmdletBinding()]
param(
    [switch]$Register,
    [switch]$Unregister,
    [switch]$SkipWin32,
    [string]$Ref = "main",
    [string]$RepoUrl = "https://github.com/tshino/softcam.git"
)

$ErrorActionPreference = "Stop"
if ($Register -and $Unregister) {
    throw "Pass either -Register or -Unregister, not both."
}

$root = Split-Path $PSScriptRoot -Parent
$softcamDir = Join-Path $root "native\softcam"
$softcamProject = Join-Path $softcamDir "src\softcam\softcam.vcxproj"
$softcamSrcDir = Split-Path $softcamProject -Parent
$appNative = Join-Path $root "UvcCaptureSim.App\native"
$destX64 = Join-Path $appNative "softcam.dll"
$videoInputKey = "HKLM\SOFTWARE\Classes\CLSID\{860BB310-5D01-11D0-BD3B-00A0C911CE86}\Instance\DirectShow Softcam"
$videoInputKey32 = "HKLM\SOFTWARE\Classes\WOW6432Node\CLSID\{860BB310-5D01-11D0-BD3B-00A0C911CE86}\Instance\DirectShow Softcam"

function Find-MSBuild {
    $vswhere = Join-Path ${env:ProgramFiles(x86)} "Microsoft Visual Studio\Installer\vswhere.exe"
    if (-not (Test-Path $vswhere)) {
        throw "vswhere.exe was not found. Install Visual Studio with the Desktop development with C++ workload."
    }

    $msbuild = & $vswhere -latest -products * -requires Microsoft.Component.MSBuild -find "MSBuild\**\Bin\MSBuild.exe" | Select-Object -First 1
    if (-not $msbuild) {
        throw "MSBuild was not found. Install the Desktop development with C++ workload."
    }

    return $msbuild
}

function Resolve-FirstExisting {
    param([string[]]$Candidates)
    $Candidates | Where-Object { $_ -and (Test-Path $_) } | Select-Object -First 1
}

function Get-X64Candidates {
    @(
        (Join-Path $softcamSrcDir "dist\bin\x64\softcam.dll"),
        (Join-Path $softcamSrcDir "x64\Release\softcam.dll")
    )
}

function Get-Win32Candidates {
    @(
        (Join-Path $softcamSrcDir "dist\bin\Win32\softcam.dll"),
        (Join-Path $softcamSrcDir "Win32\Release\softcam.dll")
    )
}

function Register-Dlls {
    param(
        [string[]]$Dlls,
        [switch]$Remove
    )

    $flag = if ($Remove) { "/u" } else { "" }
    $commands = foreach ($dll in $Dlls) {
        $regsvr = if ($dll -match '\\Win32\\') { "$env:WINDIR\SysWOW64\regsvr32.exe" } else { "$env:WINDIR\System32\regsvr32.exe" }
        "& '$regsvr' /s $flag '$dll'"
    }

    if (-not $Remove) {
        $commands += "& reg.exe add `"$videoInputKey`" /v DevicePath /t REG_SZ /d softcam-uvcsim /f"
        $commands += "& reg.exe add `"$videoInputKey32`" /v DevicePath /t REG_SZ /d softcam-uvcsim /f"
    }

    $script = $commands -join "; "
    Write-Host "Elevating to $(if ($Remove) { 'unregister' } else { 'register' }) softcam ..."
    $process = Start-Process powershell.exe -Verb RunAs -Wait -PassThru -ArgumentList "-NoProfile", "-Command", $script
    if ($process.ExitCode -ne 0) {
        throw "The elevated registration step exited with code $($process.ExitCode)."
    }
}

if ($Unregister) {
    $dlls = @()
    if (Test-Path $destX64) { $dlls += $destX64 }
    $builtWin32 = Resolve-FirstExisting (Get-Win32Candidates)
    if ($builtWin32) { $dlls += $builtWin32 }
    if ($dlls.Count -eq 0) {
        Write-Warning "No built softcam.dll was found to unregister."
        return
    }

    Register-Dlls -Dlls $dlls -Remove
    Write-Host "Unregistered DirectShow Softcam."
    return
}

if (-not (Test-Path (Join-Path $softcamDir "softcam.sln"))) {
    Write-Host "Cloning softcam into $softcamDir ..."
    New-Item -ItemType Directory -Force -Path (Split-Path $softcamDir -Parent) | Out-Null
    git clone --depth 1 --branch $Ref $RepoUrl $softcamDir
    if ($LASTEXITCODE -ne 0) { throw "git clone failed." }
}
else {
    Write-Host "Using the existing clone at $softcamDir"
}

$msbuild = Find-MSBuild
Write-Host "Building softcam x64 Release ..."
& $msbuild $softcamProject /nologo /m /p:Configuration=Release /p:Platform=x64
if ($LASTEXITCODE -ne 0) { throw "The x64 softcam build failed." }

if (-not $SkipWin32) {
    Write-Host "Building softcam Win32 Release ..."
    & $msbuild $softcamProject /nologo /m /p:Configuration=Release /p:Platform=Win32
    if ($LASTEXITCODE -ne 0) { throw "The Win32 softcam build failed." }
}

$builtX64 = Resolve-FirstExisting (Get-X64Candidates)
if (-not $builtX64) {
    throw "softcam.dll was not found after the x64 build. Looked in: $((Get-X64Candidates) -join ', ')"
}

New-Item -ItemType Directory -Force -Path $appNative | Out-Null
Copy-Item $builtX64 $destX64 -Force
Write-Host "Copied x64 softcam.dll to $destX64"

if ($Register) {
    $dlls = @($destX64)
    $builtWin32 = Resolve-FirstExisting (Get-Win32Candidates)
    if (-not $SkipWin32 -and $builtWin32) { $dlls += $builtWin32 }
    Register-Dlls -Dlls $dlls
    Write-Host "Registered 'DirectShow Softcam' as a DirectShow video input device."
    Write-Host "Rebuild the app so softcam.dll is copied beside the executable, start streaming, then rescan in the capture app."
}
else {
    Write-Host "Build finished. Run this script again with -Register to make other apps see the camera."
}
