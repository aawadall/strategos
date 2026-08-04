# Strategos — Third-Party Attributions

This file tracks all third-party assets, libraries, and data used in Strategos, along with their licenses and required attribution text.

Items marked **[IN-GAME]** must appear in the in-game credits screen.
Items marked **[DOCS ONLY]** require no in-game credit but are tracked here for legal audit.

---

## Code & Libraries

| Library | Version | License | URL | Notes |
|---|---|---|---|---|
| Steamworks.NET | latest | MIT | https://github.com/rlabrecque/Steamworks.NET | Steam platform integration |
| JMSML | latest | Apache 2.0 | https://github.com/Esri/joint-military-symbology-xml | NATO APP-6D symbol data |
| milsymbol | latest | MIT | https://github.com/spatialillusions/milsymbol | NATO APP-6D SVG generation |

---

## Map & Geodata

| Asset | License | Source | Attribution Text | Required |
|---|---|---|---|---|
| SRTM Heightmap Data | Public Domain | NASA / USGS EarthExplorer | — | [DOCS ONLY] |
| OpenStreetMap Data | ODbL 1.0 | openstreetmap.org | © OpenStreetMap contributors | **[IN-GAME]** |

**OpenStreetMap in-game credit (required verbatim):**
> Map data © [OpenStreetMap](https://www.openstreetmap.org/copyright) contributors, licensed under [ODbL](https://opendatacommons.org/licenses/odbl/)

---

## Audio

### Music
| Track | Source | License | Attribution |
|---|---|---|---|
| `menu-loop` (Resources/Audio/menu-loop.ogg) | Suno (from `Research/audio/253.mp3`) | Suno Pro commercial licence | Instrumental military ambient menu bed — see `Research/audio/suno-prompts.md` |
| `play-ambient` (Resources/Audio/play-ambient.ogg) | Suno (from `Research/audio/254.mp3`) | Suno Pro commercial licence | PLAY ambient bed — same prompt family |

### Sound Effects
| Asset | Author | Source | License | Attribution |
|---|---|---|---|---|
| *(Add SFX here as sourced)* | | freesound.org | CC0 | — |

**Sonniss GDC Audio Bundle** (royalty-free, no attribution required — tracked for audit):
- Year(s) used: *(fill in)*

---

## Art & Visual Assets

### Fonts
| Font | License | Source | Attribution |
|---|---|---|---|
| Oswald | SIL OFL 1.1 | Google Fonts | [DOCS ONLY] |
| IBM Plex Mono | SIL OFL 1.1 | Google Fonts | [DOCS ONLY] |
| Roboto Condensed | Apache 2.0 | Google Fonts | [DOCS ONLY] |
| Share Tech Mono | SIL OFL 1.1 | Google Fonts | [DOCS ONLY] |

### UI Icons
| Pack | License | Source | Attribution |
|---|---|---|---|
| Kenney UI Pack | CC0 1.0 | kenney.nl | [DOCS ONLY] |
| Kenney Game Icons | CC0 1.0 | kenney.nl | [DOCS ONLY] |
| Kenney Input Prompts | CC0 1.0 | kenney.nl | [DOCS ONLY] |

### Unit Emblems & Flags
| Asset | Source | License | Attribution |
|---|---|---|---|
| *(Add as sourced from Wikipedia Commons or similar)* | | | |

---

## Historical Data & Scenarios

| Source | License | Usage |
|---|---|---|
| US Army Center of Military History (history.army.mil) | Public Domain | Scenario research, ORBAT data |
| Combined Arms Research Library (CARL) | Public Domain | After-Action Reports, tactical studies |
| Project Gutenberg military texts | Public Domain | Historical reference |

**Research hook (#332 / #335):** Authored engagement digests live under
`Research/historical/` (committed markdown). Third-party PDFs stay in `Research/` and
remain gitignored. Conventions:
[`docs/historical-research.md`](docs/historical-research.md). Shortlist:
[`Research/historical/SHORTLIST.md`](Research/historical/SHORTLIST.md).

When a digest cites a source already in the table above, the engagement note's Source
block is enough. When it introduces a **new** publisher or licence class, add a row here
in the same change (steps below).

**Digests gathered under #332** (licence: CMH / CARL public-domain U.S. gov works):

| Note | Engagement | Echelon band |
|---|---|---|
| `Research/historical/little-round-top-20th-maine.md` | 20th Maine, Little Round Top (1863) | Regiment / company–battalion |
| `Research/historical/belleau-wood-1st-marine-brigade.md` | Belleau Wood (1918) | Brigade / battalion |
| `Research/historical/normandy-v-corps-breakout-context.md` | Normandy V Corps / First Army context (1944) | Corps / army |

**Shipped historical scenarios (#333 / #345)** — player-visible provenance:

| Resources scenario | Drawn from | Licence |
|---|---|---|
| `little-round-top-20th-maine` | `Research/historical/little-round-top-20th-maine.md` + CMH Gettysburg pubs | U.S. gov public domain |

Map terrain is a procedural approximation (see each scenario `Description`); not real
ground.

---

## How to Add a New Attribution

1. Add a row to the appropriate table above
2. Note the exact licence type and version
3. Mark **[IN-GAME]** if credit is required in the game's credits screen
4. If the asset requires in-game verbatim credit, paste the exact credit string
5. Commit the update with message `docs: add attribution for <asset name>`

---

*Last updated: 2026-07-29 | Co-Authored-By: Oz <oz-agent@warp.dev>*
