#Requires -Version 5.1
<#
.SYNOPSIS
  Warp/resample a DEM GeoTIFF into Research/maps/fixtures/<Id>/elevation.v1.json
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
    [Parameter(Mandatory = $true)][double]$Northing,
    [string]$Dataset = "SRTM",
    [string]$Licence = "Public Domain (US Gov)"
)

$ErrorActionPreference = "Stop"
$repoRoot = Resolve-Path (Join-Path $PSScriptRoot "..\..")
$inputPath = if ([IO.Path]::IsPathRooted($Input)) { $Input } else { Join-Path $repoRoot $Input }
if (-not (Test-Path $inputPath)) { throw "Input not found: $inputPath" }

foreach ($cmd in @("gdalwarp", "gdal_translate")) {
    if (-not (Get-Command $cmd -ErrorAction SilentlyContinue)) {
        throw "$cmd not on PATH. Install GDAL (OSGeo4W / conda / chocolatey). See docs/map-import-tools.md"
    }
}

if ($Width -lt 8 -or $Height -lt 8) { throw "Width/Height must be >= 8" }
if ($MetresPerCell -le 0) { throw "MetresPerCell must be > 0" }
if ($Zone -lt 1 -or $Zone -gt 60) { throw "Zone must be 1..60" }
$Band = $Band.Substring(0, 1).ToUpperInvariant()

$epsg = if ([int][char]$Band -ge [int][char]'N') { 32600 + $Zone } else { 32700 + $Zone }
$teXmax = $Easting + ($Width * $MetresPerCell)
$teYmax = $Northing + ($Height * $MetresPerCell)

$fixDir = Join-Path $repoRoot "Research\maps\fixtures\$Id"
$workDir = Join-Path $repoRoot "Research\maps\raw\$Id\_work"
New-Item -ItemType Directory -Force -Path $fixDir, $workDir | Out-Null

$warped = Join-Path $workDir "warped.tif"
$asc = Join-Path $workDir "grid.asc"

Write-Host "gdalwarp → EPSG:$epsg  ${Width}x${Height}  te=$Easting,$Northing,$teXmax,$teYmax"
& gdalwarp -overwrite -t_srs "EPSG:$epsg" `
    -te $Easting $Northing $teXmax $teYmax `
    -ts $Width $Height -r bilinear `
    -dstnodata -9999 `
    $inputPath $warped
if ($LASTEXITCODE -ne 0) { throw "gdalwarp failed ($LASTEXITCODE)" }

& gdal_translate -of AAIGrid $warped $asc
if ($LASTEXITCODE -ne 0) { throw "gdal_translate failed ($LASTEXITCODE)" }

# AAIGrid: header then row-major north→south. MapData is south→north — reverse rows.
$lines = Get-Content $asc
$hdr = @{}
$i = 0
while ($i -lt $lines.Count -and $lines[$i] -match '^[A-Za-z]') {
    $parts = $lines[$i] -split '\s+', 2
    $hdr[$parts[0].ToLowerInvariant()] = $parts[1].Trim()
    $i++
}
$ncols = [int]$hdr["ncols"]
$nrows = [int]$hdr["nrows"]
$nodata = if ($hdr.ContainsKey("nodata_value")) { [double]$hdr["nodata_value"] } else { -9999 }
if ($ncols -ne $Width -or $nrows -ne $Height) {
    throw "ASC size ${ncols}x${nrows} != requested ${Width}x${Height}"
}

$northFirst = New-Object 'double[]' ($Width * $Height)
$row = 0
while ($i -lt $lines.Count -and $row -lt $Height) {
    $vals = ($lines[$i] -split '\s+' | Where-Object { $_ -ne '' })
    if ($vals.Count -eq 0) { $i++; continue }
    if ($vals.Count -ne $Width) { throw "ASC row $row has $($vals.Count) cols, expected $Width" }
    for ($c = 0; $c -lt $Width; $c++) {
        $v = [double]$vals[$c]
        if ($v -eq $nodata) { $v = 0 }
        $northFirst[($row * $Width) + $c] = $v
    }
    $row++; $i++
}
if ($row -ne $Height) { throw "ASC only read $row rows, expected $Height" }

$elevation = New-Object 'object[]' ($Width * $Height)
for ($y = 0; $y -lt $Height; $y++) {
    $srcRow = $Height - 1 - $y
    for ($x = 0; $x -lt $Width; $x++) {
        $elevation[($y * $Width) + $x] = [math]::Round($northFirst[($srcRow * $Width) + $x], 3)
    }
}

$payload = [ordered]@{
    schema           = "strategos.elevation.v1"
    name             = $Id
    width            = $Width
    height           = $Height
    metresPerCell    = $MetresPerCell
    origin           = [ordered]@{
        zone     = $Zone
        band     = $Band
        easting  = $Easting
        northing = $Northing
    }
    elevationMetres  = $elevation
    source           = [ordered]@{
        dataset = $Dataset
        licence = $Licence
        input   = [IO.Path]::GetFileName($inputPath)
    }
}

$outJson = Join-Path $fixDir "elevation.v1.json"
($payload | ConvertTo-Json -Depth 6 -Compress:$false) | Set-Content -Path $outJson -Encoding utf8

$prov = Join-Path $fixDir "PROVENANCE.md"
if (-not (Test-Path $prov)) {
    @"
# Provenance — $Id

- Dataset: $Dataset
- Licence: $Licence
- Input file: ``$([IO.Path]::GetFileName($inputPath))``
- Grid: ${Width}x${Height} @ ${MetresPerCell} m; GeoOrigin zone $Zone$Band E$Easting N$Northing
- Converted: $(Get-Date -Format o) via ``tools/maps/convert-dem.ps1``

Add URL and exact tile id before promoting to a shipped scenario. Mirror the
licence row in root ATTRIBUTIONS.md.
"@ | Set-Content -Path $prov -Encoding utf8
}

Write-Host "Wrote $outJson"
Write-Host "Next: ./tools/maps/validate-fixture.ps1 -Id $Id"
