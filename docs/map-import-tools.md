# Map import tools (#493 / #487)

External (non-Unity) toolchain: **hunt → convert → import** real DEM and OSM
data into Strategos game-data JSON. Unity only consumes the intermediate JSON via
the `MapGenerator` **authoredRelief** / **authoredFeatures** hooks — it does not
call GDAL or Overpass at runtime.

Cadence: weekly releases **W18–W21** (`docs/value-roadmap-52w.md`). Sources and
licences: [`assets.md`](assets.md). Georeferencing: `MapCoordinates` / `GeoOrigin`.

---

## Layout

| Path | Role | Committed? |
|---|---|---|
| `tools/maps/` | Hunt / convert / validate scripts | Yes |
| `Research/maps/schemas/` | `elevation.v1.json` / `features.v1.json` shapes | Yes |
| `Research/maps/fixtures/<id>/` | Converted game-data + `PROVENANCE.md` | Yes (small) |
| `Research/maps/raw/` | GeoTIFF, HGT, PBF downloads | **No** — gitignored |
| `Assets/Resources/...` | Shipped scenario JSON only (after probe) | Yes |

Never commit raw SRTM/OSM binaries. Summarise provenance in each fixture’s
`PROVENANCE.md` and in root [`ATTRIBUTIONS.md`](../ATTRIBUTIONS.md).

---

## Pipeline

```
bbox + metresPerCell + width/height
        │
        ▼
  hunt-bbox.ps1 ──► URLs + provenance stub under Research/maps/raw/<id>/
        │                 (operator downloads tiles manually or with curl)
        ▼
  convert-dem.ps1 ──► fixtures/<id>/elevation.v1.json
  convert-osm.ps1 ──► fixtures/<id>/features.v1.json   (optional, W21)
        │
        ▼
  validate-fixture.ps1 ──► schema + size checks
        │
        ▼
  Unity (W19+) ──► ElevationGridRelief stage + FeaturesStamp stage
                   → MapData → scenario GenerateMap / ShippedMapProbe
```

### 1. Hunt

```powershell
./tools/maps/hunt-bbox.ps1 -Id lrt-1863 -West -77.26 -South 39.78 -East -77.22 -North 39.81
```

Writes `Research/maps/raw/<id>/HUNT.md` with:

- OpenTopography / EarthExplorer search hints for the bbox
- Overpass turbo query template (highways, place nodes)
- Suggested `GeoOrigin` (UTM zone from bbox centre) and cell grid size

Operator places downloaded `.tif` / `.hgt` / `.osm` / `.pbf` beside that file.

### 2. Convert

Requires **GDAL** on `PATH` (`gdalwarp`, `gdal_translate`, `gdalinfo`).

```powershell
./tools/maps/convert-dem.ps1 `
  -Id lrt-1863 `
  -Input Research/maps/raw/lrt-1863/dem.tif `
  -Width 128 -Height 128 -MetresPerCell 25 `
  -Zone 18 -Band S -Easting 340000 -Northing 4405000

./tools/maps/convert-osm.ps1 `
  -Id lrt-1863 `
  -Input Research/maps/raw/lrt-1863/export.osm
```

`convert-dem` warps to the UTM patch matching `GeoOrigin`, resamples to
`Width × Height`, and emits row-major elevation metres (y = south → north),
matching `MapData` indexing.

`convert-osm` keeps only roads / tracks / place nodes inside the sheet, writes
polylines and POIs in **cell coordinates**.

### 3. Import (validate + stage)

```powershell
./tools/maps/validate-fixture.ps1 -Id lrt-1863
```

Checks schema version, `width*height == elevation.Length`, finite metres, and
that feature positions sit in-bounds. Unity loaders (W19 / #489) read the same
paths under `Research/maps/fixtures/` or a promoted copy under Resources.

---

## Intermediate formats

### `elevation.v1.json`

```json
{
  "schema": "strategos.elevation.v1",
  "name": "lrt-1863",
  "width": 128,
  "height": 128,
  "metresPerCell": 25,
  "origin": { "zone": 18, "band": "S", "easting": 340000, "northing": 4405000 },
  "elevationMetres": [ /* length width*height, row-major, y north */ ],
  "source": { "dataset": "SRTMGL1", "licence": "Public Domain (US Gov)" }
}
```

### `features.v1.json`

```json
{
  "schema": "strategos.features.v1",
  "name": "lrt-1863",
  "width": 128,
  "height": 128,
  "lines": [ { "kind": "Road", "points": [ { "x": 10.5, "y": 20.0 } ] } ],
  "pois":  [ { "kind": "Town", "x": 64, "y": 64, "name": "Example" } ],
  "source": { "dataset": "OpenStreetMap", "licence": "ODbL 1.0" }
}
```

Line/POI `kind` strings map to `MapFeatureKind` / landcover stamps when the Unity
importer lands (#489 / #491). Until then, fixtures are content for probes and
contact sheets.

---

## External dependencies (operator machine)

| Tool | Role | Install hint |
|---|---|---|
| GDAL | Warp / resample DEM | OSGeo4W, `conda install gdal`, or chocolatey `gdal` |
| curl / browser | Download SRTM / OSM extracts | Windows 10+ ships curl |
| (optional) QGIS | Inspect tiles before convert | qgis.org |
| (optional) osmium / osmtogeojson | Prefetch smaller OSM extracts | — |

Scripts fail fast with an install hint when `gdalwarp` is missing.

---

## Weekly release addons

| Week | Tracker | Tooling deliverable | Player-facing addon |
|---|---|---|---|
| W18 | #488 | hunt + convert-dem scaffold + one Research fixture attempt | glossary DEM/SRTM + assets note |
| W19 | #489 | Unity reads `elevation.v1.json` as authoredRelief | HELP real vs procedural |
| W20 | #490 | Promote one scenario onto fixture | ATTRIBUTIONS |
| W21 | #491 | convert-osm + features stamp + ODbL credit | in-game credits |

---

## Out of scope

- Runtime download of DEM/OSM inside the player
- Multi-zone UTM stitch / satellite imagery mode
- Replacing procedural generation for training / climb maps
