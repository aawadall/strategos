#Requires -Version 5.1
<#
.SYNOPSIS
  Validate Research/maps/fixtures/<Id>/elevation.v1.json (+ optional features.v1.json).
#>
param(
    [Parameter(Mandatory = $true)][string]$Id
)

$ErrorActionPreference = "Stop"
$repoRoot = Resolve-Path (Join-Path $PSScriptRoot "..\..")
$dir = Join-Path $repoRoot "Research\maps\fixtures\$Id"
$elevPath = Join-Path $dir "elevation.v1.json"
if (-not (Test-Path $elevPath)) { throw "Missing $elevPath" }

$elev = Get-Content $elevPath -Raw | ConvertFrom-Json
$errors = New-Object System.Collections.Generic.List[string]

if ($elev.schema -ne "strategos.elevation.v1") {
    $errors.Add("elevation.schema must be strategos.elevation.v1")
}
if ($elev.width -lt 8 -or $elev.height -lt 8) {
    $errors.Add("width/height must be >= 8")
}
$expected = [int]$elev.width * [int]$elev.height
if ($null -eq $elev.elevationMetres -or $elev.elevationMetres.Count -ne $expected) {
    $errors.Add("elevationMetres length $($elev.elevationMetres.Count) != width*height $expected")
} else {
    foreach ($v in $elev.elevationMetres) {
        if ($null -eq $v -or [double]::IsNaN([double]$v) -or [double]::IsInfinity([double]$v)) {
            $errors.Add("non-finite elevation value"); break
        }
    }
}
if ($null -eq $elev.origin) {
    $errors.Add("origin missing")
} else {
    if ($elev.origin.zone -lt 1 -or $elev.origin.zone -gt 60) { $errors.Add("origin.zone out of range") }
}

$featPath = Join-Path $dir "features.v1.json"
if (Test-Path $featPath) {
    $feat = Get-Content $featPath -Raw | ConvertFrom-Json
    if ($feat.schema -ne "strategos.features.v1") {
        $errors.Add("features.schema must be strategos.features.v1")
    }
    if ($feat.width -ne $elev.width -or $feat.height -ne $elev.height) {
        $errors.Add("features width/height must match elevation grid")
    }
    foreach ($line in @($feat.lines)) {
        if ($null -eq $line.points -or $line.points.Count -lt 2) {
            $errors.Add("line needs >= 2 points"); break
        }
    }
    foreach ($poi in @($feat.pois)) {
        if ($poi.x -lt 0 -or $poi.y -lt 0 -or $poi.x -ge $elev.width -or $poi.y -ge $elev.height) {
            $errors.Add("POI out of bounds: $($poi.name)")
        }
    }
}

if ($errors.Count -gt 0) {
    Write-Host "FAIL $Id"
    $errors | ForEach-Object { Write-Host "  - $_" }
    exit 1
}

$min = ($elev.elevationMetres | Measure-Object -Minimum).Minimum
$max = ($elev.elevationMetres | Measure-Object -Maximum).Maximum
Write-Host "OK $Id  $($elev.width)x$($elev.height)  elev $min .. $max m"
if (Test-Path $featPath) {
    $feat = Get-Content $featPath -Raw | ConvertFrom-Json
    Write-Host "   features: $($feat.lines.Count) lines, $($feat.pois.Count) pois"
}
exit 0
