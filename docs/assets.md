# Strategos — Asset Strategy

This document defines where every asset type comes from, how it is licensed, how it gets into Unity 6, and where it lives in the repository.

---

## Summary

| Category | Source | License | Pipeline |
|---|---|---|---|
| NATO APP-6D Symbols | milsymbol / JMSML | MIT / Apache 2.0 | SVG → Unity SVG Importer → Sprite Atlas |
| Heightmap Terrain | NASA SRTM | Public Domain | GeoTIFF → GDAL → Unity Terrain |
| Road & Settlement Data | OpenStreetMap | ODbL | OSM XML → GeoJSON → runtime overlay |
| Procedural Maps | Custom generator | N/A (owned) | Unity Terrain API |
| Music | Suno (user-generated) | Commercial use (Suno Pro) | WAV/MP3 → OGG → Unity AudioClip |
| Sound Effects | procedural stubs → freesound / Sonniss | CC0 / royalty-free | WAV → OGG → Unity AudioClip |
| Voice / narration | ElevenLabs (planned) | **Blocked** until commercial tier confirmed — [audio-licence.md](audio-licence.md) | Script → API → OGG → Resources |
| Morse / radio FX | Procedural + staging takes | Staging audits in [audio-licence.md](audio-licence.md) | Code / DSP; rare Suno textures |
| Historical Scenarios | Public domain / custom | CC0 / owned | JSON → scenario loader |
| UI Fonts | Google Fonts | SIL OFL | TTF → Unity TextMeshPro |
| Unit Emblems & Flags | Wikipedia Commons / custom | CC0 / public domain | PNG → Sprite |
| UI Art & Icons | Kenney.nl / custom | CC0 | PNG → Sprite Atlas |

---

## NATO APP-6D Symbol System

### Approach — Custom In-House Generator

Strategos builds its own NATO APP-6D symbol compositor. No external runtime dependencies.
See [docs/nato-symbol-generator.md](nato-symbol-generator.md) for the full design spec.

Key source files:

| File | Role |
|---|---|
| `Assets/Scripts/Core/NatoSymbols/NatoSymbolTypes.cs` | Enums, structs, SIDCCode (Annex A) |
| `Assets/Scripts/Core/NatoSymbols/SIDCParser.cs` | SIDC string → SIDCCode |
| `Assets/Scripts/Core/NatoSymbols/INatoSymbol.cs` | Layer model + SymbolLayout |
| `Assets/Scripts/Core/NatoSymbols/NatoSymbolDecorator.cs` | Base symbol + decorator base |
| `Assets/Scripts/Core/NatoSymbols/SymbolFactory.cs` | Factory Method (frame + fill) |
| `Assets/Scripts/Core/NatoSymbols/IconDecorator.cs` | Icon layer decorator |
| `Assets/Scripts/Core/NatoSymbols/SectorModifierDecorator.cs` | Sector 1/2 modifiers |
| `Assets/Scripts/Core/NatoSymbols/AmplifierDecorator.cs` | Echelon / HQ / TF / feint |
| `Assets/Scripts/Core/NatoSymbols/ConditionDecorator.cs` | Condition + combat-power bars |
| `Assets/Scripts/Core/NatoSymbols/TextAmplifierDecorator.cs` | Text amplifiers T / M / F |
| `Assets/Scripts/Core/NatoSymbols/ProceduralDrawUtil.cs` | Pixel primitives + 5×7 bitmap font |
| `Assets/Scripts/Core/NatoSymbols/NatoSymbolComposer.cs` | Table 3-1 composition |
| `Assets/Scripts/Core/NatoSymbols/NatoSymbolBaker.cs` | Compose → Sprite |
| `Assets/Scripts/Core/NatoSymbols/NatoSymbolDatabase.cs` | ScriptableObject sprite registry |
| `Assets/Scripts/Core/NatoSymbols/NatoSymbolGenerator.cs` | GPU bake compositor (RenderTexture) |
| `Assets/Scripts/Core/NatoSymbols/NatoSymbolView.cs` | In-scene layered SpriteRenderer display |
| `Assets/Editor/NatoSymbolEditorWindow.cs` | Editor Window: preview, export, batch |

The generator composites five ordered layers per symbol:
1. Frame (affiliation shape + colour tint)
2. Main icon (unit function/type)
3. Echelon mark (dots, Roman numerals, Xs)
4. Structural modifiers (HQ line, TF bracket, feint)
5. Text labels (designation, higher formation, strength)

The **Editor Window** (menu: **Strategos → NATO Symbol Generator**) enables live SIDC preview, single export, and CI batch generation from a JSON catalogue.

### SIDC Format (APP-6D Annex A)

Each symbol is identified by a 20-digit Symbol Identification Code (optional third ten ignored). Key fields:

```
1–2 Version (10) | 3–4 Context+Identity | 5–6 Symbol Set | 7 Status
8 HQ/TF/Dummy | 9–10 Amplifier | 11–16 Entity… | 17–20 Sector modifiers
```

Example (friend land infantry company): `10031000151211000000`

Construction uses Factory (frame) + Decorators (icon → modifiers → amplifiers). See [docs/nato-symbol-generator.md](nato-symbol-generator.md).

Store SIDC codes in `UnitDefinition` ScriptableObjects and resolve to sprite at runtime.

### Asset Location
```
Assets/Art/NatoSymbols/
├── Frames/            # Affiliation frame sprites (friend, hostile, neutral, unknown)
├── Icons/             # Unit type icons by symbol set (land, sea, air, etc.)
├── Echelons/          # Echelon designator sprites (• •• ••• I II X XX XXX XXXX XXXXX XXXXXX)
├── Modifiers/         # Task force, HQ, reinforced/reduced, feint overlays
└── Atlas/             # Packed Sprite Atlases (one per symbol set)
```

---

## Terrain & Maps

### Real-World Heightmaps — NASA SRTM

- **Source:** [earthexplorer.usgs.gov](https://earthexplorer.usgs.gov) (SRTM 1 Arc-Second = ~30 m resolution)
- **License:** Public Domain (US Government work)
- **Format:** GeoTIFF or HGT
- **Coverage:** Global land surface

**Processing pipeline:**
1. Download SRTM tiles for target region from EarthExplorer or [OpenTopography](https://opentopography.org)
2. Convert and crop with **GDAL** (`gdal_translate`, `gdalwarp`):
   ```
   gdalwarp -t_srs EPSG:4326 -r bilinear input.hgt output.tif
   gdal_translate -of PNG -scale output.tif heightmap.png
   ```
3. Import 16-bit greyscale PNG into Unity as a Raw Heightmap
4. Apply to `UnityEngine.TerrainData.SetHeightsFromTexture`

**Useful pre-built tools:**
- **Terrain.party** ([terrain.party](https://terrain.party)) — download Unity-ready heightmaps by drawing a bounding box on a web map (free, no account required)
- **WorldCreator** — commercial terrain tool with SRTM import

### Road & Settlement Overlay — OpenStreetMap

- **Source:** [openstreetmap.org](https://openstreetmap.org) / [download.geofabrik.de](https://download.geofabrik.de)
- **License:** Open Database License (ODbL) — attribution required in-game
- **Data includes:** roads, railways, rivers, towns, airfields, bridges, borders
- **Format:** OSM XML or PBF → convert to GeoJSON with [osmtogeojson](https://github.com/tyrasd/osmtogeojson) or **osm2json**
- **Unity integration:** parse GeoJSON at runtime and draw road/river overlays on the terrain

**Attribution requirement (ODbL):**
Add "© OpenStreetMap contributors" to the in-game map credits screen and docs.

### Procedural Map Generator

For fictional/training scenarios:
- Unity **Terrain Tools** package for sculpting
- Noise-based generation (Perlin, Simplex) for height variation
- Voronoi/river networks for realistic drainage patterns
- Biome classification table → terrain type assignment

### Asset Location
```
Assets/Data/Maps/
├── Heightmaps/        # Raw .raw or PNG heightmap files (tracked by Git LFS)
├── GeoJSON/           # Road, river, settlement overlays
└── Procedural/        # Saved procedural map seeds and parameters
```

---

## Music — Suno (User-Generated)

Music is generated with **Suno** (user's own subscription). Suno Pro/Premier licences allow commercial release.

### Track List (Target)

| Track | Mood | Suggested Suno Style Tags | Usage |
|---|---|---|---|
| Main Theme | Epic, military, orchestral | orchestral, military march, brass, snare drum, cinematic | Main menu |
| Tactical Operations | Tension, ambient | ambient, low tension, military, electronic, slow build | Gameplay (no contact) |
| Contact! | Urgent, intense | fast tempo, military, cinematic action, percussion heavy | Combat active |
| Encirclement | Dark, relentless | dark orchestral, war drums, ominous, brass stabs | Large battle |
| Victory March | Triumphant | military march, brass fanfare, uplifting, snare | Scenario won |
| Defeat | Sombre, reflective | slow, melancholic, solo strings, military bugle | Scenario lost |
| Strategic Map | Contemplative | ambient, strategic, piano, subtle tension | Campaign map |
| Night Operations | Stealthy, eerie | ambient, night, electronic, quiet, subtle | Night scenarios |

### Delivery Format
Export from Suno as WAV → convert to **OGG Vorbis** (q=6) for Unity:
```
ffmpeg -i track.wav -c:a libvorbis -q:a 6 track.ogg
```
Target file sizes: ambient tracks ~3–5 MB, intense tracks ~2–3 MB.

### Asset Location
Shipped beds load by name via `Resources.Load` (`AudioService`):

```
Assets/Resources/Audio/
├── menu-loop.ogg      # #253 — main menu / tools / settings
└── play-ambient.ogg   # #254 — PLAY session bed
```

Staging takes (Suno MP3s, VO drafts) stay under `Research/audio/` and are **not**
shipped. Convert with ffmpeg (`libvorbis`, q≈4) before promoting into `Resources/Audio/`.
Broader catalogue targets (combat intensity, victory/defeat) remain future content under
`#43` / `#398`–`#399` leftovers.
---

## Sound Effects

### Primary Source — freesound.org (CC0)

Filter by **CC0 (Public Domain)** licence only. Key searches:

| Category | Search Terms |
|---|---|
| Small arms | `rifle shot`, `machine gun`, `suppressed gunfire` |
| Artillery | `artillery fire`, `howitzer`, `mortar explosion` |
| Armour | `tank engine`, `tank track`, `tank cannon` |
| Aviation | `helicopter rotor`, `jet flyby`, `prop aircraft` |
| Radio | `radio static`, `military radio`, `squelch click` |
| Explosions | `distant explosion`, `grenade`, `bomb impact` |
| UI | `map click`, `order confirm`, `alert`, `notification` |
| Ambience | `wind`, `rain`, `urban background`, `forest ambience` |

### Secondary Source — Sonniss GDC Audio Bundle

Sonniss releases a free professional SFX bundle every year at GDC:
- [sonniss.com/gameaudiogdc](https://sonniss.com/gameaudiogdc)
- Royalty-free, commercial use, no attribution required
- Previous bundles total 30+ GB of military-relevant SFX

### Format
Encode all SFX to **OGG Vorbis** (q=4 for ambient, q=6 for combat SFX).

### Asset Location

One-shots that must load by name ship under Resources (same rule as music beds):

```
Assets/Resources/Audio/Sfx/
├── ui-click.ogg
├── ui-select.ogg
├── order-issued.ogg
├── order-rejected.ogg
├── combat-fire.ogg
├── combat-hit.ogg
└── unit-destroyed.ogg
```

Full cue checklist and priority: [sfx-inventory.md](sfx-inventory.md) (#249 / #44).
**Full audio catalogue (music / SFX / VO / Morse):** [audio-inventory.md](audio-inventory.md) (#259 / #41).
Procedural stubs (#250–#252) may skip files and synthesise in memory first.

Broader category folders (weapons / vehicles / ambience) stay a sourcing guide only
until a scenario event needs them — do not pre-create empty trees under Resources.

---

## Historical Scenario Data

### Sources

| Source | Content | License |
|---|---|---|
| US Army Center of Military History ([history.army.mil](https://history.army.mil)) | Unit histories, ORBATs, campaign studies | Public Domain (US Gov) |
| Combined Arms Research Library ([cgsc.contentdm.oclc.org](https://cgsc.contentdm.oclc.org)) | After-Action Reports, tactical studies | Public Domain |
| Project Gutenberg ([gutenberg.org](https://gutenberg.org)) | Historical military texts | Public Domain |
| Wikipedia (as reference only) | Order of battle outlines | CC BY-SA (not used verbatim in game data) |
| Avalon Hill / SPI wargame maps | Inspiration only | Not licensable — recreate independently |

### Initial Multi-Era Scenario Pack (Target for v0.5 EA)

| # | Scenario | Era | Echelon | Source |
|---|---|---|---|---|
| 1 | Stalingrad: 6th Army Encirclement | WW2 (1942) | Army / Corps | CMH |
| 2 | Normandy: D+1 Beach Consolidation | WW2 (1944) | Division | CMH |
| 3 | Chosin Reservoir Breakout | Korean War (1950) | Division | CMH |
| 4 | Ia Drang Valley | Vietnam (1965) | Battalion | CMH |
| 5 | Fulda Gap War Game | Cold War (1980s) | Corps | Fictional/doctrine |
| 6 | Gulf War: 100-Hour Ground Offensive | Gulf War (1991) | Corps / Army | CMH |
| 7 | Mogadishu: TF Ranger | Modern (1993) | Company | CMH |
| 8 | Fallujah: Operation Phantom Fury | Modern (2004) | Division | CMH |

Each scenario stored as a JSON file in `Assets/Data/Scenarios/Historical/`.

### Scenario JSON Schema (abbreviated)

```json
{
  "id": "ww2-stalingrad-encirclement",
  "name": "Stalingrad: The Kessel",
  "era": "WW2",
  "date": "1942-11-23",
  "echelon": "Corps",
  "sides": ["German 6th Army", "Soviet Don Front"],
  "map_region": "stalingrad_volga_bend",
  "objectives": [...],
  "orbat": {...},
  "conditions": {...},
  "historical_notes": "..."
}
```

---

## UI Art & Fonts

### Fonts — Google Fonts (SIL OFL)

| Usage | Font | Reason |
|---|---|---|
| UI headings | **Oswald** or **Barlow Condensed** | Military/stencil feel |
| Body / data labels | **IBM Plex Mono** | Monospace, military HUD aesthetic |
| Map labels | **Roboto Condensed** | Legible at small sizes |
| Designation callouts | **Share Tech Mono** | Radio/terminal feel |

All available at [fonts.google.com](https://fonts.google.com) under SIL Open Font Licence.

### UI Icons — Kenney.nl

- [kenney.nl/assets](https://kenney.nl/assets) — free CC0 game UI icons
- Relevant packs: **Game Icons**, **UI Pack**, **Input Prompts**
- Supplement with custom SVG icons for military-specific UI elements

### Asset Location
```
Assets/Art/UI/
├── Icons/
├── Fonts/
└── Panels/
```

---

## Unit Emblems & National Flags

### Sources

- **Wikipedia Commons** — large collection of military unit patches and national flags, most CC0 or public domain
- **US military insignia** — all public domain as US Government works
- **The Noun Project** — some free military icons (CC BY or paid royalty-free)

### Scope for v1.0

Emblems for: US, UK, Germany, France, Russia, China, generic NATO, generic OPFOR.

### Asset Location
```
Assets/Art/Units/
├── Emblems/           # Unit patch/insignia sprites
└── Flags/             # National flags (ISO 3166 named)
```

---

## Git LFS Configuration

Binary assets must be tracked by **Git LFS** to keep the repository manageable. See `.gitattributes` in the project root.

### Tracked by LFS

| Pattern | Type |
|---|---|
| `*.png`, `*.tga`, `*.psd` | Textures |
| `*.ogg`, `*.wav`, `*.mp3` | Audio |
| `*.raw`, `*.tif`, `*.hgt` | Heightmaps |
| `*.fbx`, `*.obj`, `*.blend` | 3D models (if used) |
| `*.ttf`, `*.otf` | Fonts |
| `Assets/Data/Maps/Heightmaps/**` | Terrain data |

---

## Asset Pipeline Tools

| Tool | Purpose | Cost |
|---|---|---|
| **GDAL** | Heightmap processing, CRS conversion | Free / Open Source |
| **terrain.party** | Download Unity heightmaps by map area | Free |
| **QGIS** | Visual OSM/GIS data inspection and export | Free / Open Source |
| **Inkscape** | SVG symbol editing | Free / Open Source |
| **FFmpeg** | Audio format conversion (WAV → OGG) | Free / Open Source |
| **TexturePacker** | Sprite atlas packing | Free tier / paid |
| **Unity SVG Importer** | Import SVG as sprites | Free (Unity package) |
| **Unity Terrain Tools** | Terrain sculpting and blending | Free (Unity package) |
| **Suno** | Music generation | User subscription |

---

## Attribution Tracking

All third-party assets with attribution requirements are tracked in [`ATTRIBUTIONS.md`](../ATTRIBUTIONS.md) at the repo root.

**Assets requiring in-game attribution:**
- OpenStreetMap data → "© OpenStreetMap contributors" in map credits
- CC BY licensed assets → credit in game credits screen
- CC0 / public domain → no attribution required, tracked for audit only

---

*Last updated: 2026-08-05*
