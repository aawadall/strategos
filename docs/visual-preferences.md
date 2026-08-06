# Visual preferences (#519)

Player options for **unit marks**, **map style**, and **map layers** — persisted in
`PlayerPreferences`, edited from Settings (and optionally PLAY), applied when baking
the topographic sheet.

GitHub: [#519](https://github.com/aawadall/strategos/issues/519).

| Concern | Epic / issue | Notes |
|---|---|---|
| Window / fullscreen | #385 (done) | GRAPHICS size — not this page |
| Dark UI chrome | #132 | Shell colours — not map sheet |
| Fog of war physics | #476 | Sensor model — not layer toggles |
| **Visual prefs** | **#519** | Units + map style + layers |

## Already in the engine

- `MapRenderMode` (Schematic / Topographic / Hybrid / Terrain / NatoTopo) — dropdowns on
  PLAY / EXPLORE / SCENARIO today are **session-only** (reset on restart).
- `MapRenderOptions.DrawHillshade` / `DrawContours` / `DrawAreas` / `DrawLines` /
  `DrawPois` / `DrawLabels` / `DrawGrid` — rasterizer flags; not yet prefs.
- PLAY `afterPixels` overlays: GCMs, world objects, order tracks — always on when present.

## Children (#520–#525)

1. Prefs fields + round-trip  
2. Settings **VISUAL** section  
3. Views honor prefs on bake  
4. Unit symbol scale + wreck visibility  
5. Overlay toggles (GCM / world / tracks)  
6. Probe + invariants note  

## Rule

Defaults must match today’s look (`MapRenderOptions.Default`, Topographic, overlays on)
so a fresh install does not surprise. Changing prefs must not mutate `MapData`.

Update this page when a child ships.
