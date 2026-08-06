# Map tools — hunt / convert / validate (#493)

PowerShell scripts for the external map pipeline. Full design:
[`docs/map-import-tools.md`](../../docs/map-import-tools.md).

## Prerequisites

- PowerShell 5+ (Windows) or PowerShell 7
- **GDAL** on `PATH` for DEM convert (`gdalwarp`, `gdal_translate`, `gdalinfo`)
- Optional: curl for downloads listed in `HUNT.md`

```powershell
gdalinfo --version   # must print a version
```

## Commands

```powershell
# 1. Hunt — write download checklist + provenance stub
./tools/maps/hunt-bbox.ps1 -Id demo-patch `
  -West -77.26 -South 39.78 -East -77.22 -North 39.81

# 2. Convert DEM (after placing dem.tif under Research/maps/raw/<id>/)
./tools/maps/convert-dem.ps1 -Id demo-patch `
  -Input Research/maps/raw/demo-patch/dem.tif `
  -Width 64 -Height 64 -MetresPerCell 25 `
  -Zone 18 -Band S -Easting 340000 -Northing 4405000

# 3. Convert OSM (optional)
./tools/maps/convert-osm.ps1 -Id demo-patch `
  -Input Research/maps/raw/demo-patch/export.osm `
  -Width 64 -Height 64 -MetresPerCell 25 `
  -Zone 18 -Band S -Easting 340000 -Northing 4405000

# 4. Validate fixture JSON
./tools/maps/validate-fixture.ps1 -Id demo-patch
```

Run from the **repo root**.
