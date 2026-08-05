<#
.SYNOPSIS
    Push a Strategos Windows build to itch.io via butler (#221).

.DESCRIPTION
    Thin wrapper around `butler push`. Does not create the itch project for you —
    see docs/itch-publish.md. Resolves butler from PATH or the itch desktop app broth
    cache. Target defaults to STRATEGOS_ITCH_TARGET (e.g. aawadall/strategos).

.PARAMETER Source
    Folder containing Strategos.exe, or a .zip of that build / Release asset.

.PARAMETER Channel
    itch channel suffix. Default: windows-alpha

.PARAMETER Target
    user/game pair. Default: $env:STRATEGOS_ITCH_TARGET

.PARAMETER DryRun
    Pass --dry-run to butler (list files; no upload).

.EXAMPLE
    $env:STRATEGOS_ITCH_TARGET = 'aawadall/strategos'
    .\scripts\itch-push.ps1 -Source .\Artifacts\Windows
    .\scripts\itch-push.ps1 -Source .\Strategos-0.3.0-alpha.1-windows.zip -Channel windows
#>

#Requires -Version 5.1
[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$Source,

    [string]$Channel = 'windows-alpha',

    [string]$Target = $(if ($env:STRATEGOS_ITCH_TARGET) { $env:STRATEGOS_ITCH_TARGET } else { 'aawadall/strategos' }),

    [switch]$DryRun
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function Resolve-Butler {
    $cmd = Get-Command butler -ErrorAction SilentlyContinue
    if ($cmd) { return $cmd.Source }

    $brothRoot = Join-Path $env:APPDATA 'itch\broth\butler\versions'
    if (Test-Path $brothRoot) {
        $exe = Get-ChildItem -Path $brothRoot -Filter butler.exe -Recurse -ErrorAction SilentlyContinue |
            Sort-Object FullName -Descending |
            Select-Object -First 1
        if ($exe) { return $exe.FullName }
    }

    return $null
}

if (-not (Test-Path -LiteralPath $Source)) {
    Write-Error "Source not found: $Source"
}

$butler = Resolve-Butler
if (-not $butler) {
    Write-Error @"
butler not found on PATH or under %APPDATA%\itch\broth\butler.
Install butler, or use the itch app Upload tab / browser upload — docs/itch-publish.md.
"@
}

$dest = "${Target}:${Channel}"
Write-Host "butler: $butler"
Write-Host "push:   $Source -> $dest"

$args = @('push', $Source, $dest)
if ($DryRun) { $args += '--dry-run' }

& $butler @args
exit $LASTEXITCODE
