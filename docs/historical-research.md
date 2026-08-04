# Historical research conventions

Phase 6.2 research half — epic
[#332](https://github.com/aawadall/strategos/issues/332) (children #335–#340).
Conversion to playable scenarios is
[#333](https://github.com/aawadall/strategos/issues/333).

## Where material lives

- **Third-party sources** (PDFs, downloads): `Research/` root — **gitignored**. Same
  convention as the APP-6D reference PDF.
- **Authored digests** (our notes): `Research/historical/*.md` — **committed**. Enough
  for a later author to build a `Scenario` without re-reading the primary source cold.
- **Licence / provenance audit**: root [`ATTRIBUTIONS.md`](../ATTRIBUTIONS.md)
  ("Historical Data & Scenarios"), plus a Source block in every engagement note.

## Attribution process (per engagement)

1. Prefer sources already listed under Historical Data & Scenarios in `ATTRIBUTIONS.md`
   (CMH, CARL, Project Gutenberg — typically public domain U.S. government works).
2. In the engagement note, record: title, publisher/URL, access date, licence.
3. If a new source class is used, add a row to `ATTRIBUTIONS.md` in the same change
   (see "How to Add a New Attribution" there).
4. Mark gaps: write `SOURCED:` / `INVENTED:` (or `UNKNOWN — do not invent`) rather than
   filling ORBAT detail the source does not support.

## Shortlist and digests

| Artifact | Role |
|---|---|
| [`Research/historical/SHORTLIST.md`](../Research/historical/SHORTLIST.md) | 5–10 candidates across echelons |
| `Research/historical/<slug>.md` | One engagement digest |

Echelon spread follows [`ROADMAP.md`](../ROADMAP.md) (fireteam through theatre): the pack
must not be all company-vs-company.
