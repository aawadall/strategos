<#
.SYNOPSIS
    Builds Strategos using Unity 6 in batch mode.

.DESCRIPTION
    Reads the Unity version from ProjectSettings/ProjectVersion.txt,
    locates the Unity executable, optionally runs tests, then builds
    for the requested platform.

.PARAMETER Target
    Platform to build for. Default: Windows64
    Options: Windows64 | Linux64 | macOS | WebGL | All

.PARAMETER OutputDir
    Output directory relative to the project root. Default: Artifacts

.PARAMETER Test
    Run Unity EditMode tests before building.

.PARAMETER Development
    Build with development flags enabled (profiler attach, script debugging).

.PARAMETER Clean
    Delete the Library/ cache before building (forces a full reimport).

.EXAMPLE
    .\scripts\build.ps1
    .\scripts\build.ps1 -Target Linux64 -Test
    .\scripts\build.ps1 -Target Windows64 -OutputDir Builds\release -Development
    .\scripts\build.ps1 -Target All -Clean
#>

#Requires -Version 5.1
[CmdletBinding()]
param (
    [ValidateSet("Windows64", "Linux64", "macOS", "WebGL", "All")]
    [string]$Target = "Windows64",

    [string]$OutputDir = "Artifacts",

    [switch]$Test,
    [switch]$Development,
    [switch]$Clean
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

# ─── Paths ───────────────────────────────────────────────────────────────────
$ProjectRoot  = Split-Path -Parent $PSScriptRoot
$VersionFile  = Join-Path $ProjectRoot "ProjectSettings\ProjectVersion.txt"
$ArtifactsDir = Join-Path $ProjectRoot $OutputDir

# ─── Resolve Unity version ───────────────────────────────────────────────────
if (-not (Test-Path $VersionFile)) {
    Write-Error "ProjectVersion.txt not found at $VersionFile"
    exit 1
}

$VersionLine  = Get-Content $VersionFile | Where-Object { $_ -match "^m_EditorVersion:" }
$UnityVersion = ($VersionLine -replace "m_EditorVersion:\s*", "").Trim()

Write-Host "── Strategos Build Script ────────────────────────────────────"
Write-Host "   Project : $ProjectRoot"
Write-Host "   Unity   : $UnityVersion"
Write-Host "   Target  : $Target"
Write-Host "   Output  : $ArtifactsDir"
Write-Host "──────────────────────────────────────────────────────────────"

# ─── Locate Unity executable ─────────────────────────────────────────────────
$Candidates = @(
    "C:\Program Files\Unity\Hub\Editor\$UnityVersion\Editor\Unity.exe",
    "C:\Program Files (x86)\Unity\Hub\Editor\$UnityVersion\Editor\Unity.exe"
)

# Allow CI override via environment variable
if ($env:UNITY_EXECUTABLE) { $Candidates = @($env:UNITY_EXECUTABLE) + $Candidates }

$UnityExe = $Candidates | Where-Object { Test-Path $_ } | Select-Object -First 1

if (-not $UnityExe) {
    Write-Error @"
Unity $UnityVersion not found. Either:
  1. Install it in Unity Hub, or
  2. Set the UNITY_EXECUTABLE environment variable to the full path of Unity.exe

Searched:
$($Candidates -join "`n")
"@
    exit 1
}

Write-Host "   Unity   : $UnityExe"
Write-Host ""

# ─── Optional clean ──────────────────────────────────────────────────────────
if ($Clean) {
    $LibPath = Join-Path $ProjectRoot "Library"
    if (Test-Path $LibPath) {
        Write-Host "Cleaning Library/ ..."
        Remove-Item $LibPath -Recurse -Force
        Write-Host "Done."
    }
}

# ─── Ensure output directory ─────────────────────────────────────────────────
New-Item -ItemType Directory -Path $ArtifactsDir -Force | Out-Null

# ─── Helper: invoke Unity and check exit code ─────────────────────────────────
function Invoke-Unity {
    param([string[]]$Arguments, [string]$LogFile)

    Write-Host "Running Unity:"
    Write-Host "  $UnityExe $($Arguments -join ' ')"
    Write-Host "  Log: $LogFile"
    Write-Host ""

    & $UnityExe @Arguments

    if ($LASTEXITCODE -ne 0) {
        Write-Host ""
        Write-Error "Unity exited with code $LASTEXITCODE. Full log: $LogFile"
        exit $LASTEXITCODE
    }
}

# ─── Run tests ───────────────────────────────────────────────────────────────
if ($Test) {
    Write-Host "=== Running EditMode Tests ==="
    $TestResults = Join-Path $ArtifactsDir "test-results.xml"
    $TestLog     = Join-Path $ArtifactsDir "test-log.txt"

    Invoke-Unity -Arguments @(
        "-batchmode", "-quit", "-nographics",
        "-projectPath", $ProjectRoot,
        "-runTests",
        "-testPlatform", "EditMode",
        "-testResults", $TestResults,
        "-logFile",     $TestLog
    ) -LogFile $TestLog

    Write-Host "Tests passed. Results: $TestResults"
    Write-Host ""
}

# ─── Build function ──────────────────────────────────────────────────────────
function Build-Target {
    param([string]$PlatformName, [string]$BuildMethod)

    Write-Host "=== Building $PlatformName ==="

    $BuildOutput = Join-Path $ArtifactsDir $PlatformName
    $BuildLog    = Join-Path $ArtifactsDir "build-log-$PlatformName.txt"

    $Args = @(
        "-batchmode", "-quit", "-nographics",
        "-projectPath",    $ProjectRoot,
        "-executeMethod",  $BuildMethod,
        "-customBuildPath", $BuildOutput,
        "-logFile",        $BuildLog
    )

    if ($Development) { $Args += "-development" }

    Invoke-Unity -Arguments $Args -LogFile $BuildLog

    Write-Host "Build complete: $BuildOutput"
    Write-Host ""
}

# ─── Dispatch ─────────────────────────────────────────────────────────────────
switch ($Target) {
    "Windows64" { Build-Target "Windows"  "Strategos.Editor.GameBuild.BuildWindows" }
    "Linux64"   { Build-Target "Linux"    "Strategos.Editor.GameBuild.BuildLinux"   }
    "macOS"     { Build-Target "macOS"    "Strategos.Editor.GameBuild.BuildMacOS"   }
    "WebGL"     { Build-Target "WebGL"    "Strategos.Editor.GameBuild.BuildWebGL"   }
    "All"       {
        Build-Target "Windows"  "Strategos.Editor.GameBuild.BuildWindows"
        Build-Target "Linux"    "Strategos.Editor.GameBuild.BuildLinux"
        Build-Target "macOS"    "Strategos.Editor.GameBuild.BuildMacOS"
    }
}

Write-Host "── All done ──────────────────────────────────────────────────"
Write-Host "   Artifacts: $ArtifactsDir"
Write-Host "──────────────────────────────────────────────────────────────"
