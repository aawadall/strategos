# Research/maps/

Staging area for real-world DEM / OSM → Strategos game-data (#493 / #487).

| Path | Committed? | Contents |
|---|---|---|
| `schemas/` | Yes | JSON Schema for elevation / features v1 |
| `fixtures/<id>/` | Yes (small JSON + PROVENANCE) | Converted game-data ready for Unity |
| `raw/<id>/` | **No** | Downloaded GeoTIFF / HGT / OSM / PBF |

Pipeline docs: [`docs/map-import-tools.md`](../../docs/map-import-tools.md).
Scripts: [`tools/maps/`](../../tools/maps/).

Do not commit raw tiles. Record licence + URL in each fixture `PROVENANCE.md`
and in root [`ATTRIBUTIONS.md`](../../ATTRIBUTIONS.md).
