# Research/

Local and (selectively) committed research material for Strategos.

## Layout

| Path | Committed? | Contents |
|---|---|---|
| `Research/*.pdf` (and other third-party binaries) | **No** — gitignored | Copyrighted or bulky sources (e.g. APP-6D PDF) |
| `Research/historical/*.md` | **Yes** — notes only | Authored engagement digests for Phase 6.2 (#332) |
| `Research/historical/SHORTLIST.md` | **Yes** | Candidate engagements across echelons |
| `Research/audio/*.md` | **Yes** | VO scripts (#42) and Suno prompts (#43) |
| `Research/audio/*.mp3` | **Yes** — draft takes | Candidate ElevenLabs/Suno generations from the scripts/prompts above, staged here pending #401 (OGG Vorbis conversion) and provenance logging in `ATTRIBUTIONS.md`; not the final shipped format or location (`Assets/Audio/`, per `docs/assets.md`) |
| `Research/maps/schemas/`, `fixtures/` | **Yes** — small JSON | Converted elevation/features for real-map import (#493) |
| `Research/maps/raw/` | **No** — gitignored | Downloaded GeoTIFF / HGT / OSM / PBF |

Do **not** commit source PDFs, scraped HTML dumps, or copyrighted reproductions.
Summarise into markdown; cite the public-domain (or otherwise cleared) source and licence
in the note and in root [`ATTRIBUTIONS.md`](../ATTRIBUTIONS.md).

## Historical engagement notes (#332)

Each engagement folder or file under `historical/` should record, to the fidelity the
source supports:

- Approximate strengths / organisation for both sides (at the fight's echelon)
- Terrain character (open / forest / urban / river / relief)
- Each side's objective
- Outcome and approximate duration
- Source URL or bibliographic cite + licence
- Explicit **"invented vs sourced"** callouts where the source's granularity runs out

Conversion to `Scenario` JSON is a separate epic ([#333](https://github.com/aawadall/strategos/issues/333)).

## Real maps (#493 / #487)

Hunt DEM/OSM outside Unity, convert to `elevation.v1.json` / `features.v1.json`,
validate, then Unity loads via authoredRelief / authoredFeatures (W19+).

- Docs: [`docs/map-import-tools.md`](../docs/map-import-tools.md)
- Scripts: [`tools/maps/`](../tools/maps/)
- Staging: [`Research/maps/`](maps/)

Conventions detail: [`docs/historical-research.md`](../docs/historical-research.md).
