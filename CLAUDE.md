# CLAUDE.md — Strategos

Working notes for this repository: commands, invariants, and traps that are not visible
from reading the code.

**Where facts belong:** this file is the authority for *commands, invariants and gotchas*.
`docs/nato-symbol-generator.md` is the authority for *APP-6D reference detail* (SIDC
tables, frame shapes, layer model). If you change symbol behaviour, update both.

---

## Project

Unity 6 (`6000.0.75f1`) / URP tactical command simulation built on NATO APP-6D symbology.

**Phase 2 (the symbol system) is complete; Phase 1's map system is partly built.** The
map has a data model, a generation pipeline and a 2D topographic renderer, and the demo
panel draws its symbol on a real generated sheet with live relief-profile and render-mode
controls. There is still no terrain mesh, no map camera and no panning or zooming — the
map is a texture in a UI card, not a world. Everything else in `ROADMAP.md` — units,
combat, C2, scenarios, AI, networking — is unbuilt; there is no game loop yet.

| Path | Contents |
|---|---|
| `Assets/Scripts/Core/NatoSymbols/` | The symbol system (all of it) |
| `Assets/Scripts/Core/Maps/Model/` | `MapData` and friends — data only |
| `Assets/Scripts/Core/Maps/Generation/` | The generation pipeline, one file per stage |
| `Assets/Scripts/Core/Maps/Rendering2D/` | CPU topographic renderer |
| `Assets/Scripts/Demo/` | Demo scene behaviours, incl. `SymbolBuilderPanel` |
| `Assets/Editor/` | Build pipeline, symbol editor window, TMP importer, contact sheets |
| `docs/` | Reference docs; `phases.md` is the task breakdown |

---

## Build & verify

```powershell
.\scripts\build.ps1 -Target Windows64      # Windows64 | Linux64 | macOS | WebGL | All
.\scripts\capture.ps1                      # launch player, screenshot, close
```

**A batch build can silently ship the previous revision.** `BuildPlayer` will package
whatever is already in `Library/ScriptAssemblies`, so a build started right after an edit
can succeed, report success, and run your last change but one — twice in a row, which is
long enough to send you hunting for a bug in code that is not in the player. `GameBuild.Run`
now calls `AssetDatabase.Refresh()` first and refuses to build while
`EditorApplication.isCompiling`. If you are still unsure which revision you are looking
at, put something visible in the frame and confirm it: the map card's marginalia strip
carries seed and extent, which is exactly what it is for.

Bake a grid of symbol permutations — **prefer this over clicking the GUI** when checking
rendering changes:

```powershell
# Menu: Strategos > Bake Symbol Contact Sheet
& "C:\Program Files\Unity\Hub\Editor\6000.0.75f1\Editor\Unity.exe" `
    -batchmode -quit -nographics -projectPath . `
    -executeMethod Strategos.Editor.SymbolContactSheet.Bake -logFile sheet.log
# -> Artifacts/symbol-contact-sheet.png
```

Same idea for maps, and the same advice — a generator's output is a picture, so read the
picture. `MapContactSheet` bakes every relief profile against every render mode at 1 px
per cell, plus one map at 3 px per cell where labels and cased roads are checkable:

```powershell
# Menu: Strategos > Bake Map Contact Sheet
& "C:\Program Files\Unity\Hub\Editor\6000.0.75f1\Editor\Unity.exe" `
    -batchmode -quit -nographics -projectPath . `
    -executeMethod Strategos.Editor.MapContactSheet.Bake -logFile map-sheet.log
# -> Artifacts/map-contact-sheet.png, Artifacts/map-detail.png
```

It logs per-profile elevation range, feature counts and a landcover breakdown. Check
those numbers before reading the image: a landcover percentage that has moved says the
generator changed, where the image alone cannot tell you whether generation or the
palette moved.

Player log (**always check after a UI change**, see UI gotchas below):

```
%USERPROFILE%\AppData\LocalLow\DefaultCompany\strategos\Player.log
```

Editor menu: `Strategos → Build/…`, `NATO Symbol Generator`, `Open Demo Scene` (F5),
`Recreate Demo Scene`, `Bake Symbol Contact Sheet`, `Bake Map Contact Sheet`,
`Import TMP Essential Resources`.

---

## Architecture

Factory creates the framed base; decorators append layers. `NatoSymbolComposer.Compose`
is the entry point and fixes the order:

```
SIDCParser → SIDCCode
    └─ SymbolFactory.CreateBase      frame + fill
       └─ IconDecorator              entity icon + entity-type variant mark
          └─ SectorModifierDecorator sector 1 (upper) / sector 2 (lower)
             └─ AmplifierDecorator   echelon, HQ staff, TF bracket, feint
                └─ ConditionDecorator    condition bar + combat-power bar
                   └─ TextAmplifierDecorator  fields T, M, F
                      → NatoSymbolBaker.Bake → one Sprite
```

`INatoSymbol` is data only — an ordered `SymbolLayerDraw` list plus
`SymbolTextAmplifiers`. Nothing rasterises until `Bake`. `ConditionDecorator` and
`TextAmplifierDecorator` run last because they read the amplifiers that
`AmplifierDecorator` populates.

The map system mirrors that shape: an ordered pipeline over a data-only container, and
nothing rasterises until the renderer runs. `MapGenerator.Generate` hard-codes the stage
order for the same reason `Compose` does — later stages read what earlier ones wrote:

```
MapGenerationSettings → ReliefParameters
    └─ TectonicStage      base landform, sea level
       └─ (authored relief hook)
          └─ ErosionStage      weathers the surface
             └─ HydrologyStage  fill, D8 routing, lakes, rivers, moisture
                └─ LandcoverStage  classification from slope + moisture
                   └─ SettlementStage  towns, stamped into landcover
                      └─ NetworkStage  roads between them, bridges and fords
                         └─ (authored features hook)
```

`MapData` is data only: an elevation grid, a landcover grid, and vector features.
`MapRasterizer.RenderPixels` is the only thing that draws, and its layer order is fixed
too — ground, contours, areas, lines, point marks, labels, grid. Labels come after every
mark because placement can only avoid collisions it can see, and the grid comes last
because a grid line that gives way to a road has stopped being a coordinate reference.

The two systems share `ProceduralDrawUtil`. Symbols bake into a square buffer, maps into
a rectangular one, so every primitive has a `(w, h)` overload with the square one
forwarding to it. Add new primitives to the rectangular overload.

---

## SIDC field layout

20 digits (an optional third ten is parsed and ignored). Source of truth:
`SIDCParser.TryParse`.

| Digits | Field |
|---|---|
| 1–2 | Version (`10` = APP-6D) |
| 3 | Context (0 = reality) |
| 4 | Standard identity |
| 5–6 | Symbol set (`10` = land unit) |
| 7 | Status / operational condition |
| 8 | HQ / task force / dummy |
| 9–10 | Echelon / mobility |
| 11–12 | Entity |
| 13–14 | Entity type |
| 15–16 | Entity subtype |
| 17–18 | Sector 1 modifier |
| 19–20 | Sector 2 modifier |

Canonical friend infantry company: `10031000151211000000`.

---

## Symbology conventions

**Echelon marks** (`AmplifierDecorator.DrawEchelon`). These were off by one before commit
`2039fe0`; do not "correct" them back:

| Code | Echelon | Mark |
|---|---|---|
| 11–14 | Team / Squad / Section / Platoon | `○` `•` `••` `•••` |
| 15 | Company | `I` — one bar |
| 16 | Battalion | `II` — two bars |
| 17 | Regiment | `III` — three bars |
| 18–26 | Brigade … Command | `X` `XX` `XXX` `XXXX` `XXXXX` `XXXXXX` |

**Frame shape** by identity group: Friend rectangle, Hostile diamond, Neutral square,
Unknown ellipse. Line style: solid present, dashed planned, dotted uncertain identity.

**Infantry is a pair of crossed diagonals**, not a single slash.

---

## Rendering invariants

Break these and the symbol silently degrades rather than erroring.

- **The frame is deliberately left of centre.** `FrameRight = 160` of `BASE = 256`
  reserves a right-hand column for text amplifiers, which APP-6D places outside the
  frame. A composed symbol is therefore *not* centred in its texture — that is correct,
  not a layout bug. A full-width frame leaves ~24px of margin, far too little for a
  designation like `1-7 IN`.
- **Icons fit the frame's inscribed rectangle, not its bounding box.** Diamond and
  ellipse frames taper. A diamond requires `fx + fy <= 1`, an ellipse `fx² + fy² <= 1`.
  Margins are per identity group because a diamond is already at its geometric limit and
  has none to spare — applying a uniform margin shrinks the hostile icon to a bowtie.
- **Full-frame icons reduce to the main sector** when `Modifier1`, `Modifier2` or a
  non-standard `EntityType` needs the space (`IconDecorator.NeedsMainSectorOnly`).
  Otherwise sector glyphs are drawn straight through the infantry/recon X. Clamp the
  height only — the icon fills the main sector horizontally.
- **Every field affecting the bake must be in the cache key**
  (`ProceduralSymbolFactory.GetSymbolSprite`). `StrengthLabel` was once missing, so two
  symbols differing only in strength shared a sprite.

---

## Map rendering invariants

Same failure mode: the sheet comes out drawable but wrong.

- **Stroke widths are authored at 3 px per cell** (`MapViewport.ReferencePixelsPerCell`)
  and `StrokeScale` is *not* floored at 1. It was, and an overview drowned: a stream held
  at its authored 2 px is 50 m of ground width at 1 px per cell, so the whole drainage
  network rendered as ribbons and the map read as flooded. Floor the **final pixel width**
  at 1 instead, so features thin to hairlines rather than vanish.
- **Point marks generalise by dropping, not by shrinking** (`MapRasterizer.DetailZoom`).
  A line can thin; a ford's circle has a minimum legible size, so below the threshold
  fords, bridges and spot heights are simply not drawn. Settlements always are.
- **The viewport starts at cell −0.5, not 0.** A cell coordinate names a sample point, not
  a square. Anchor the window at 0 and a 512-cell map renders 511 px and every feature
  sits half a cell off.
- **`MapLabelPlacer` is first-come, so call order is priority order.** Place cities before
  villages, and everything before the grid. An edge label's inset must clear
  `Padding + EdgeMargin` or it is silently rejected as overhanging — grid designators
  vanished entirely at an inset of 3.
- **Landcover pattern spacing is in pixels, not cells.** A stipple is a property of the
  printed surface and should look the same at every zoom; lock it to cells and it
  coarsens as you zoom in.
- **The kilometre grid wins wherever it fits** (`MapGridOverlay.AutoSquareSpacing`).
  Choosing the finest legible interval gives a 500 m grid at working zoom, where the two
  principal digits step by 5 and stop reading as the km figure a report would quote.
- **Hydrology keeps two flood surfaces and they are not interchangeable.**
  `FillDepressions` returns *routing* (epsilon-raised, for D8) and *standing* (epsilon-free,
  the true water surface). Lake depth must come from `standing`: epsilon accumulates along
  gently-sloped paths, so at 512 cells and up a valley floor rises past the 0.75 m lake
  threshold and the whole drainage network is classified as lake. The standing surface also
  seeds the border at −∞, because a map is a window cut from a landscape and its edges are
  not a rim — seeded at their own height, any interior ground below the lowest edge cell
  fills to the edge.

---

## Unity / VCS gotchas

- **`.meta` files and `ProjectSettings/` must stay tracked.** Unity stores asset GUIDs in
  `.meta` sidecars; without them a fresh clone regenerates GUIDs and every scene, prefab
  and asmdef reference breaks. Both were untracked before commit `5e20475`, so CI built a
  different project than local (default settings, no URP pipeline asset).
- **`Assets/TextMesh Pro/` is committed on purpose — do not re-ignore it.** See below.
- Binary assets go through Git LFS (`.gitattributes`). Verify with
  `git check-attr text filter -- <path>`.

---

## TextMeshPro gotchas

TMP ships its runtime assets in a `.unitypackage` that only a human clicking
*Window → TextMeshPro → Import TMP Essential Resources* unpacks. Without them:

- `TMP_Settings.instance` is `null`, and **`TMP_Settings.defaultFontAsset` throws rather
  than returning null** — guard it, or it takes down whatever is building the UI.
- `TextMeshProUGUI.Awake()` throws too, so the component renders nothing.

The resources are committed to avoid this. To regenerate: `Strategos → Import TMP
Essential Resources`. **`AssetDatabase.ImportPackage` is asynchronous** — a batch import
must *not* use `-quit` or the editor exits before the import runs; exit from the
`importPackageCompleted` callback instead (`TmpResources.ImportBatch`).

---

## UI layout gotchas

`SymbolBuilderPanel` constructs its entire UI in `Start()`.

- **An exception truncates the layout silently.** You get a window with a background
  colour and nothing else, and no error on screen. Always check `Player.log`.
- **`childControlHeight = false`** makes a layout group reserve space from
  `LayoutElement.preferredHeight` but never resize the child — children collapse to zero
  height while still occupying the space.
- **`childForceExpandWidth = true`** hands every child a share of the surplus regardless
  of `flexibleWidth`, inflating a fixed-width child past the screen edge. Leave it off
  when one child has a fixed width and another should absorb slack.
- **The map card's underlay is cropped to fit, never stretched** (`UpdateUnderlayCrop`).
  The card's aspect follows the window, so a `Stretch`ed `RawImage` squashes the sheet —
  and a map with a different scale on each axis misreports every distance on it. The crop
  is a `uvRect` recomputed when the card resizes, because regenerating would stall for a
  few hundred milliseconds.
- **The underlay is generated at `Start` on the main thread** (~200 ms for a 200-cell map),
  which is why profile and mode are dropdowns and reseeding is a button. Do not put map
  generation behind a slider.
- **A profile's `FeatureScaleCells` is tuned for the 512-cell default map.** On the
  200-cell underlay one landform is wider than the whole sheet and it renders as a single
  dome with concentric rings, which reads as a bug in the noise. `RefreshMap` scales it
  down through `ParameterOverride`; note this also multiplies the number of closed basins,
  so the lake problem in Known gaps shows up more strongly here than on a full-size map.

---

## Glyph coverage

The bundled LiberationSans SDF atlas has **no geometric-shape glyphs**. These render as
tofu boxes: `▾` U+25BE, `○` U+25CB, `•` U+2022 (risky), `−` U+2212.

**`–` U+2013 EN DASH renders as nothing at all** — no tofu box, just a gap, which is
worse because it looks like a formatting bug rather than a missing glyph. An elevation
range came out as `202 280 M`. Use a plain hyphen.

Latin-1 is safe: `·` `±` `+` `-`. For shapes, draw procedurally instead — see
`ArrowSprite` and `HaloSprite` in `SymbolBuilderPanel`, which generate the dropdown arrow
and the symbol's soft paper halo as textures.

Amplifier text baked into symbols does **not** use TMP at all; it uses the 5×7 bitmap
font in `ProceduralDrawUtil` (`DrawText` / `MeasureText`), so symbols render identically
in headless bakes where no canvas exists.

---

## Project stubs — not APP-6D

Invented values, kept because they make the builder's controls meaningful. Do not cite
them as standard:

- **Entity-type variant codes 11–19** (`IconDecorator.VarStandard` …) and the
  **`SectorModifierDecorator` mod codes** are project inventions pending transcription of
  the Annex A tables. In real APP-6D, mobility belongs in the sector modifier, not the
  entity type.
- **The combat-power bar** (strength %) is a game amplifier with no APP-6D equivalent.
  Only the `+ / - / ±` Field F marker is standard.
- **Heavy / Light render as the letters `H` / `L`** because no conventional glyph exists.

The reference PDF is `Research/APP-6D…pdf` (gitignored — copyright restricted).

---

## Known gaps

Recorded so they are not re-investigated. None are fixed.

- **Generated terrain has huge closed basins, so maps come out 18–29% lake.** Measured on
  256-cell maps at seed 20260729: Hills 11,676 lake cells with 8,396 over 5 m deep and a
  deepest point of 38.7 m; Mountains deepest 381.7 m; Desert 20% water. These are real
  depressions in the noise, not a classification error — both flood-surface bugs above were
  found and fixed while chasing this and neither moved the numbers. Real landscapes lack
  them because fluvial erosion breaches them; the generator has no equivalent. The fix is a
  breaching pass (carve an outlet from each basin's low point to its spill) or a minimum
  catchment test before a depression is allowed to be a lake — **not** raising
  `HydrologyStage.LakeDepth`, which would only shrink the shorelines of basins that should
  not exist. Note `FillDepressions`' comment deliberately preserves hollows as tactical
  features, so breaching needs a size threshold rather than being applied wholesale.
- **CI gates every build on a test job with no tests.** `.github/workflows/build.yml` runs
  `game-ci/unity-test-runner` in EditMode and `build` has `needs: test`, but there is no
  test assembly anywhere under `Assets/`. `com.unity.test-framework` is in the manifest,
  so the scaffolding is one asmdef away.
- **`.gitattributes` line-ending rules are overridden.** `git check-attr text filter --
  "Assets/TextMesh Pro/Sprites/EmojiOne.png"` reports `text: auto` despite the `-text`
  flag on `*.png` / `*.ttf`, so a later `* text=auto` rule wins. Harmless while LFS
  carries the content (verified: committed PNG/TTF headers intact), but it would corrupt
  a binary added without an LFS rule.
- **Airborne and air assault share one chevron glyph.** `SectorModifierDecorator`
  resolves `ModAirborne` and `ModAirAssault` to the same case, so they are
  indistinguishable on screen despite being separate dropdown entries. Same for the Air
  Assault entity-type variant.
- `Packages/packages-lock.json` is gitignored, which undercuts reproducible CI builds.
