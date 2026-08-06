#Requires -Version 5.1
<#
.SYNOPSIS
  Convert a small OSM XML extract into Research/maps/fixtures/<Id>/features.v1.json

  Expects GeoOrigin matching the elevation fixture. Node lon/lat → UTM → cell via
  a planar offset (same flat-patch assumption as MapCoordinates).
#>
param(
    [Parameter(Mandatory = $true)][string]$Id,
    [Parameter(Mandatory = $true)][string]$Input,
    [Parameter(Mandatory = $true)][int]$Width,
    [Parameter(Mandatory = $true)][int]$Height,
    [Parameter(Mandatory = $true)][double]$MetresPerCell,
    [Parameter(Mandatory = $true)][int]$Zone,
    [Parameter(Mandatory = $true)][string]$Band,
    [Parameter(Mandatory = $true)][double]$Easting,
    [Parameter(Mandatory = $true)][double]$Northing
)

$ErrorActionPreference = "Stop"
$repoRoot = Resolve-Path (Join-Path $PSScriptRoot "..\..")
$inputPath = if ([IO.Path]::IsPathRooted($Input)) { $Input } else { Join-Path $repoRoot $Input }
if (-not (Test-Path $inputPath)) { throw "Input not found: $inputPath" }

$Band = $Band.Substring(0, 1).ToUpperInvariant()
$fixDir = Join-Path $repoRoot "Research\maps\fixtures\$Id"
New-Item -ItemType Directory -Force -Path $fixDir | Out-Null

# Minimal WGS84 → UTM (Snyder series truncated). Good enough for sheet-scale stamps.
function Convert-LonLatToUtm {
    param([double]$Lon, [double]$Lat, [int]$ZoneNum)
    $a = 6378137.0
    $f = 1.0 / 298.257223563
    $e2 = $f * (2.0 - $f)
    $k0 = 0.9996
    $lon0 = [math]::PI / 180.0 * (([double]$ZoneNum - 1) * 6 - 180 + 3)
    $phi = $Lat * [math]::PI / 180.0
    $lam = $Lon * [math]::PI / 180.0 - $lon0
    $N = $a / [math]::Sqrt(1.0 - $e2 * [math]::Sin($phi) * [math]::Sin($phi))
    $T = [math]::Tan($phi) * [math]::Tan($phi)
    $C = $e2 / (1.0 - $e2) * [math]::Cos($phi) * [math]::Cos($phi)
    $A = [math]::Cos($phi) * $lam
    $M = $a * ((1 - $e2 / 4 - 3 * $e2 * $e2 / 64) * $phi `
        - (3 * $e2 / 8 + 3 * $e2 * $e2 / 32) * [math]::Sin(2 * $phi) `
        + (15 * $e2 * $e2 / 256) * [math]::Sin(4 * $phi))
    $easting = $k0 * $N * ($A + (1 - $T + $C) * $A * $A * $A / 6) + 500000.0
    $northing = $k0 * ($M + $N * [math]::Tan($phi) * ($A * $A / 2 + (5 - $T + 9 * $C + 4 * $C * $C) * $A * $A * $A * $A / 24))
    if ($Lat -lt 0) { $northing += 10000000.0 }
    return @{ E = $easting; N = $northing }
}

function Convert-UtmToCell {
    param([double]$E, [double]$N)
    $x = ($E - $Easting) / $MetresPerCell
    $y = ($N - $Northing) / $MetresPerCell
    return @{ X = $x; Y = $y }
}

[xml]$osm = Get-Content -Path $inputPath -Raw
$nodes = @{}
foreach ($n in $osm.osm.node) {
    $nodes[$n.id] = @{ Lon = [double]$n.lon; Lat = [double]$n.lat }
}

$lines = New-Object System.Collections.Generic.List[object]
$pois = New-Object System.Collections.Generic.List[object]

foreach ($way in $osm.osm.way) {
    $tags = @{}
    foreach ($t in $way.tag) { $tags[$t.k] = $t.v }
    if (-not $tags.ContainsKey("highway")) { continue }
    $pts = New-Object System.Collections.Generic.List[object]
    foreach ($nd in $way.nd) {
        $node = $nodes[$nd.ref]
        if ($null -eq $node) { continue }
        $utm = Convert-LonLatToUtm -Lon $node.Lon -Lat $node.Lat -ZoneNum $Zone
        $cell = Convert-UtmToCell -E $utm.E -N $utm.N
        if ($cell.X -lt -1 -or $cell.Y -lt -1 -or $cell.X -gt $Width + 1 -or $cell.Y -gt $Height + 1) {
            continue
        }
        $pts.Add([ordered]@{ x = [math]::Round($cell.X, 3); y = [math]::Round($cell.Y, 3) })
    }
    if ($pts.Count -ge 2) {
        $kind = if ($tags["highway"] -match "motorway|trunk|primary|secondary") { "Road" } else { "Track" }
        $name = if ($tags.ContainsKey("name")) { $tags["name"] } else { $null }
        $line = [ordered]@{ kind = $kind; points = $pts }
        if ($name) { $line["name"] = $name }
        $lines.Add($line)
    }
}

foreach ($n in $osm.osm.node) {
    $tags = @{}
    foreach ($t in $n.tag) { $tags[$t.k] = $t.v }
    if (-not $tags.ContainsKey("place")) { continue }
    if ($tags["place"] -notmatch "city|town|village|hamlet") { continue }
    $utm = Convert-LonLatToUtm -Lon ([double]$n.lon) -Lat ([double]$n.lat) -ZoneNum $Zone
    $cell = Convert-UtmToCell -E $utm.E -N $utm.N
    if ($cell.X -lt 0 -or $cell.Y -lt 0 -or $cell.X -ge $Width -or $cell.Y -ge $Height) { continue }
    $poi = [ordered]@{
        kind = "Town"
        x    = [math]::Round($cell.X, 3)
        y    = [math]::Round($cell.Y, 3)
    }
    if ($tags.ContainsKey("name")) { $poi["name"] = $tags["name"] }
    $pois.Add($poi)
}

$payload = [ordered]@{
    schema = "strategos.features.v1"
    name   = $Id
    width  = $Width
    height = $Height
    lines  = $lines
    pois   = $pois
    source = [ordered]@{
        dataset = "OpenStreetMap"
        licence = "ODbL 1.0"
        input   = [IO.Path]::GetFileName($inputPath)
    }
}

$outJson = Join-Path $fixDir "features.v1.json"
($payload | ConvertTo-Json -Depth 8 -Compress:$false) | Set-Content -Path $outJson -Encoding utf8
Write-Host "Wrote $outJson ($($lines.Count) lines, $($pois.Count) pois)"
Write-Host "Remember ODbL credit — ATTRIBUTIONS.md + in-game map credits (#491)."
