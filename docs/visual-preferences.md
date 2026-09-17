# Visual preferences (#519)

Player options for **unit marks**, **map style**, **map layers**, and **chrome theme
(Light / Dark / CRT)** — persisted in `PlayerPreferences`, edited from Settings (and
optionally PLAY).

GitHub: [#519](https://github.com/aawadall/strategos/issues/519).

| Concern | Epic / issue | Notes |
|---|---|---|
| Window / fullscreen | #385 (done) | GRAPHICS size — not this page |
| Fog of war physics | #476 | Sensor model — not layer toggles |
| itch page theme | #498 | Storefront — not in-game |
| **Visual prefs** | **#519** | Units + map + layers + **Light/Dark/CRT** |
| Dark slice (legacy) | #132 | Children #193–#197; owned by #519 section D |

## Themes (Light / Dark / CRT)

| Kind | Intent |
|---|---|
| **Light** | Current paper `UiTheme` (default) |
| **Dark** | Operations-map / low-light shell (#194) |
| **CRT** | Phosphor green/amber on near-black + mild scanline/vignette (#529) |

Selector lives in Settings VISUAL / APPEARANCE (#530). Prefs: `UiThemeKind` (#528).
Requires palette indirection (#193) so views stop hard-coding `UiTheme` statics.

Theme changes **shell chrome** first. Map bake palettes (`MapRenderMode` / NatoTopo)
stay independent unless a later child explicitly ties CRT phosphor to the sheet.

## Already in the engine

- Light `UiTheme` paper palette
- `MapRenderMode` dropdowns — **session-only** today
- `MapRenderOptions.Draw*` layer flags — not yet wired to a view (prefs now hold them, #520)
- PLAY `afterPixels` overlays — always on when present

## Children

| # | Task |
|---|---|
| #520 | Prefs: map mode + layers — **done**. `PlayerPreferences.MapRenderMode` + `Draw*` bools, defaults matching `MapRenderOptions.Default`, `FormatVersion` bumped to 2. Nothing reads them yet — that's #522. |
| #521 | Settings VISUAL (map) |
| #522 | Views honor map prefs |
| #523 | Unit scale + wrecks |
| #524 | Overlay toggles |
| #525 | Probe + invariants |
| #528 | Prefs: Light / Dark / Crt |
| #529 | CRT colours + scanline |
| #530 | Settings theme selector |
| #193–#197 | Dark theme plumbing (under #132) |

## Rule

Default **Light** + today’s map look (`MapRenderOptions.Default`). Changing prefs must
not mutate `MapData`. CRT must remain readable (contrast), not a novelty-only filter.

Update this page when a child ships.
