<#
.SYNOPSIS
    Launches a Strategos build windowed, screenshots it, and closes it.

.DESCRIPTION
    Used to confirm a UI change in the real player rather than only in the editor.
    Starts the build with a fixed screen size, waits for it to render, captures the
    window, then terminates the process.

    IMPORTANT: this calls SetProcessDPIAware() before capturing. Without it the capture
    runs in a DPI-virtualised coordinate space and silently crops the window on a scaled
    display, and the right-hand control rail then looks broken when it is not.

.PARAMETER Exe
    Player executable. Default: Artifacts\Windows\Strategos.exe

.PARAMETER Out
    Destination PNG. Default: Artifacts\screenshot.png

.PARAMETER Width / Height
    Requested screen size. Use a size your display can show at 100% or the window is
    clamped and the capture will not match the requested resolution.

.PARAMETER WaitSeconds
    Seconds to wait for the first frame before capturing.

.EXAMPLE
    .\scripts\capture.ps1
    .\scripts\capture.ps1 -Width 1920 -Height 1080 -Out shot.png
#>

#Requires -Version 5.1
[CmdletBinding()]
param (
    [string]$Exe,
    [string]$Out,
    [int]$Width = 1600,
    [int]$Height = 900,
    [int]$WaitSeconds = 12,
    [string]$View,
    [switch]$DemoPostBattle,
    [switch]$DemoCareer,
    [switch]$FreezeSplash
)

$ErrorActionPreference = "Stop"

# --- Paths -------------------------------------------------------------------
$ProjectRoot = Split-Path -Parent $PSScriptRoot
if (-not $Exe) { $Exe = Join-Path $ProjectRoot "Artifacts\Windows\Strategos.exe" }
if (-not $Out) { $Out = Join-Path $ProjectRoot "Artifacts\screenshot.png" }

if (-not (Test-Path $Exe)) {
    Write-Error "Player not found: $Exe`nRun scripts\build.ps1 first."
    exit 1
}

Add-Type -AssemblyName System.Drawing
Add-Type @"
using System;
using System.Runtime.InteropServices;
public class StrategosCapture {
    [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr h, out RECT r);
    [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr h);
    [DllImport("user32.dll")] public static extern bool SetProcessDPIAware();
    [DllImport("user32.dll")] public static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")] public static extern bool ShowWindow(IntPtr h, int cmd);
    [DllImport("user32.dll")] public static extern bool BringWindowToTop(IntPtr h);
    [StructLayout(LayoutKind.Sequential)]
    public struct RECT { public int Left, Top, Right, Bottom; }
}
"@

# Must precede any screen coordinate work.
[void][StrategosCapture]::SetProcessDPIAware()

Write-Host "-- Strategos Capture ----------------------------------------"
Write-Host "   Player : $Exe"
Write-Host "   Size   : ${Width}x${Height}"
Write-Host "   Output : $Out"
Write-Host "--------------------------------------------------------------"

$unityArgs = @(
    "-screen-fullscreen", "0",
    "-screen-width", "$Width",
    "-screen-height", "$Height",
    "-popupwindow"
)
if ($View) { $unityArgs += @("-view", $View) }
if ($DemoPostBattle) { $unityArgs += "-demo-postbattle" }
if ($DemoCareer) { $unityArgs += "-demo-career" }
if ($FreezeSplash) { $unityArgs += "-freeze-splash" }

$proc = Start-Process -FilePath $Exe -ArgumentList $unityArgs -PassThru

try {
    Start-Sleep -Seconds $WaitSeconds
    $proc.Refresh()

    $handle = $proc.MainWindowHandle
    if ($handle -eq [IntPtr]::Zero) {
        Write-Error "Player has no main window after $WaitSeconds seconds."
        exit 1
    }

    # SetForegroundWindow FAILS SILENTLY when the calling process is not itself the
    # foreground process, and CopyFromScreen below then saves whatever window happens to
    # occupy those coordinates. That has happened, and the screenshot of an unrelated
    # application is indistinguishable from a broken layout. So verify, retry, and refuse
    # to save rather than save a lie.
    $focused = $false
    for ($attempt = 1; $attempt -le 5; $attempt++) {
        [void][StrategosCapture]::ShowWindow($handle, 5)       # SW_SHOW
        [void][StrategosCapture]::BringWindowToTop($handle)
        [void][StrategosCapture]::SetForegroundWindow($handle)
        Start-Sleep -Milliseconds 600
        if ([StrategosCapture]::GetForegroundWindow() -eq $handle) { $focused = $true; break }
        Write-Host "   Focus : attempt $attempt did not take, retrying"
    }
    if (-not $focused) {
        Write-Error ("Player window never reached the foreground, so the capture would " +
                     "have been of another window. Close whatever is stealing focus " +
                     "(or use an idle desktop) and retry.")
        exit 1
    }
    Start-Sleep -Seconds 1

    $rect = New-Object StrategosCapture+RECT
    [void][StrategosCapture]::GetWindowRect($handle, [ref]$rect)
    $w = $rect.Right - $rect.Left
    $h = $rect.Bottom - $rect.Top
    Write-Host "   Window : ${w}x${h} at $($rect.Left),$($rect.Top)"

    $outDir = Split-Path -Parent $Out
    if ($outDir -and -not (Test-Path $outDir)) {
        New-Item -ItemType Directory -Force $outDir | Out-Null
    }

    $bmp = New-Object System.Drawing.Bitmap($w, $h)
    $gfx = [System.Drawing.Graphics]::FromImage($bmp)
    $gfx.CopyFromScreen($rect.Left, $rect.Top, 0, 0, $bmp.Size)
    $bmp.Save($Out, [System.Drawing.Imaging.ImageFormat]::Png)
    $gfx.Dispose()
    $bmp.Dispose()

    Write-Host ""
    Write-Host "Saved: $Out"
}
finally {
    if (-not $proc.HasExited) {
        $proc.Kill()
        $proc.WaitForExit(5000)
    }
}
