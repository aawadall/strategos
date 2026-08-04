# Research/

Local and (selectively) committed research material for Strategos.

## Layout

| Path | Committed? | Contents |
|---|---|---|
| `Research/*.pdf` (and other third-party binaries) | **No** — gitignored | Copyrighted or bulky sources (e.g. APP-6D PDF) |
| `Research/historical/*.md` | **Yes** — notes only | Authored engagement digests for Phase 6.2 (#332) |
| `Research/historical/SHORTLIST.md` | **Yes** | Candidate engagements across echelons |
| `Research/audio/*.md` | **Yes** — scripts/prompts only | VO scripts (#42) and Suno prompts (#43) — no generated audio committed here |

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

Conventions detail: [`docs/historical-research.md`](../docs/historical-research.md).
