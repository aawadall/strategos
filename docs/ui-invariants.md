# UI invariants

The view shell, imperative layout, and the glyphs that are not there.
**Read before touching `Assets/Scripts/UI` or `Assets/Scripts/Demo`.** An exception here
truncates the layout silently — you get a coloured window and nothing else.

[CLAUDE.md](../CLAUDE.md) is the index.

---

## View shell

`AppShell` owns **one** Canvas, CanvasScaler, GraphicRaycaster and EventSystem, plus the
tab bar. Views own only their content and are handed a rect to build into. `ViewHost`
switches between them and is used twice — once for the top-level tabs, once for
EXPLORE's `SYMBOLS`/`MAP` sub-tabs.

- **Views are built lazily and hidden, never destroyed.** Lazily, because building all of
  them multiplies exposure to the silent-layout-truncation failure mode and pays every
  view's startup cost whichever tab you wanted. Not destroyed, because rebuilding costs a
  map regeneration or a few hundred symbol bakes.
- **`IAppView.Build(host)` is explicit, not `Awake`/`OnEnable`** — `AddComponent` fires
  those before the host rect can be handed over. `ViewHost` activates the GameObject
  *before* calling `Build`, because a layout group computes nothing for an inactive
  hierarchy.
- **`OnHidden` must disable any camera with a `targetTexture`.** Such a camera renders
  every frame whether or not anything displays the result, so a forgotten
  `SetRendering(false)` costs a full terrain render per frame while you are looking at a
  different view. This is the most expensive mistake available here.
- **`OnHidden` must also close dropdowns** (`UiFactory.HideDropdownsIn`). A `TMP_Dropdown`
  left open re-appears open, floating over whichever view comes next.
- **Do not add a second bootstrap.** `AppShell` installs itself only if no shell exists,
  and is deliberately *not* gated on the scene's name — that gate existed solely to work
  around a stale committed scene and would blank any other scene. Two installers means two
  stacked canvases and two EventSystems.
- **Only `AppSession` holds shared state** — map settings, the current `MapData`, a
  generation counter and the one cached symbol factory. It holds no textures on purpose,
  so disposal stays with whichever view allocated them. Views compare
  `AppSession.Generation` in `OnShown` and re-render only if it moved.
- **Sprites from `AppSession.Symbols` must never be `Destroy`ed** — they are shared cache
  entries, and only `ClearCache()` may free them. The builder bakes its preview *uncached*
  via `NatoSymbolComposer` + `NatoSymbolBaker` precisely so it can dispose it; if it is
  ever switched to the cached factory, `DestroyPreviewAssets` must go in the same change or
  the library's tiles turn blank.
- **Seed controls with `UiFactory.SetSliderValue`, not `Slider.SetValueWithoutNotify`.**
  The numeric label is maintained by a `UiSliderReadout` component, and the bare
  no-notify setter moves the value while leaving the label showing the old number — which
  is how sixteen relief sliders came to display their minimums with their handles at the
  real values.
- **A slider whose range clamps its own authored value is a data bug, not a cosmetic one.**
  Editing any relief slider writes *all* of them back as a `ParameterOverride`, so a
  clamped reading silently rewrites the profile. `TreelineFraction` reaches 1.5 and
  `SnowlineFraction` 2.0 — they are not 0–1 despite the names — and `BaseElevationMetres`
  is −40 on Coastal. `ScenarioSetupView.BuildReliefSliders` records the real spans.

---

---

## UI layout gotchas

Every view builds its UI imperatively from `UiFactory`. There is no prefab and no UXML.

- **An exception truncates the layout silently.** You get a window with a background
  colour and nothing else, and no error on screen. Always check `Player.log`.
- **`childControlHeight = false`** makes a layout group reserve space from
  `LayoutElement.preferredHeight` but never resize the child — children collapse to zero
  height while still occupying the space.
- **`childForceExpandWidth = true`** hands every child a share of the surplus regardless
  of `flexibleWidth`, inflating a fixed-width child past the screen edge. Leave it off
  when one child has a fixed width and another should absorb slack — which is every view
  here, since each has a fixed 440 px rail and a flexible stage.
- **A map sheet is cropped to fit, never stretched** (`MapSheetCard.UpdateCrop`). The
  card's aspect follows the window, so a `Stretch`ed `RawImage` squashes the sheet — and a
  map with a different scale on each axis misreports every distance on it. The crop is a
  `uvRect` recomputed on resize, because regenerating would stall for a few hundred
  milliseconds.
- **Map generation is synchronous on the main thread** (~200 ms at 200 cells, over a second
  at 512 with erosion). It stays behind discrete controls and an explicit button. Do not
  put map generation behind a slider. Mesh detail and vertical exaggeration *are* safe to
  drive live — they rebuild or scale the mesh without regenerating.
- **A profile's `FeatureScaleCells` is tuned for the 512-cell default map.** On the
  200-cell builder underlay one landform is wider than the whole sheet and it renders as a
  single dome with concentric rings, which reads as a bug in the noise. `RefreshMap` scales
  it down through `ParameterOverride`; note this also multiplies the number of closed
  basins, so the lake problem in Known gaps shows up more strongly there than on a
  full-size map.
- **A grid of tiles needs two-axis scrolling and a top-left-anchored content rect.** A
  matrix enumerating two symbol fields is ~1900 px wide; with the content stretched to the
  viewport width there is nothing to scroll horizontally and the right-hand columns are
  simply unreachable. `UiScroll.CreateGridColumn` anchors content to the corner alone so
  the `ContentSizeFitter` drives both dimensions.
- **Text on aged paper must be reserved, and the presets say which.** `PaperTexture` takes
  a `keepClear` list of the rects text will occupy and suppresses stains inside them, for
  the same reason `MapLabelPlacer.Reserve` exists — placement can only avoid collisions it
  can see. Without it, `PaperOptions.Used` drops to about 4.8:1 against `UiTheme.Ink` inside
  a ring and `Worn` to about 4.0:1, against the 7:1 AAA floor every other pair in the palette
  holds. With it, the reserved rects measure above 9:1. `RequiresReservedText` carries that
  contract on the preset itself so `PaperContactSheet` asserts it rather than trusting prose.
  Grain and mottling are deliberately *not* suppressed inside a reserve: they cannot move a
  contrast ratio, and suppressing them would leave a visibly clean rectangle behind every
  block of text.
- **A `PaperTexture` is owned by whoever asked for it and must be `Destroy`ed** — the
  opposite of the `AppSession.Symbols` rule above. Sheets are per-size and per-seed, so
  caching them centrally would be a leak with no clear owner.
- **A list of buttons rebuilt from live state needs a guard on what it was built from.** The
  PLAY rail's plan card lists `CommandQueue.Entries` with a cancel on each row, and its
  refresh runs from `RefreshSelection`, which runs every tick. Rebuilding unconditionally
  destroys and recreates every button once a second — the row under the pointer disappears
  between press and release, so roughly one click in ten does nothing. `PlayView._planKey` is
  the fix: a string of everything a row draws, compared before rebuilding. It deliberately
  omits `QueuedCommand.TicksExecuting`, which changes every tick and appears nowhere on a row.
- **PLAY's zoom is bounded by the echelon the player commands, and that is a mechanic.**
  `EchelonSpans` gives each echelon a contiguous band of ground widths; PLAY clamps the
  card's `uvRect` to it. A squad leader may not widen to a theatre picture and a corps
  commander may not drop to a single platoon — because the natural thing to do with a platoon
  on screen is to order it directly, which is the command problem `ROADMAP.md:47` says height
  is meant to remove. The bands are **configurable JSON** in `Resources/Config`, because they
  are balance numbers.
  **Clamp the ceiling and the floor together.** Clamping only the top to a small map left a
  battalion with 1.1x of zoom on the shipped 6.4 km sheet — the feature was dead in the only
  scenario that ships, and the probe passed because a collapsed range is still a range.
  `ClampedTo` preserves the band's *ratio*; `EchelonProbe` now fails under 2x.
- **A drag must not also select.** Unity delivers `OnPointerClick` on release whether or not
  the pointer moved, so panning would select whatever the cursor stopped over.
  `PlayView.ClickSlop` is the threshold; right-click ordering resets it so a stale drag cannot
  swallow an order.
- **Baked symbol sprites are not frame-centred.** `FrameRight = 160` of `BASE = 256`
  reserves a right-hand amplifier column, so the symbol sits left of centre in its texture.
  The library's tiles are 4:3 rather than square for this reason — in a square tile it reads
  as a layout bug. Do not "centre" it.
- **A standing rail section that can be entirely absent needs one container to hide, not
  several.** `PlayView.BuildDirectiveCard` (the `DIRECTIVE` card, #73) wraps its section
  header, card and button row in one `VerticalLayoutGroup` + `ContentSizeFitter` container
  (the same shape `_orbatRoot` already used) so "no directive" is a single
  `gameObject.SetActive(false)` on the container rather than three calls that can drift out
  of sync. There is no empty-state card — the rule is "the section is absent, not empty."
  It is built once in `BuildRail` (before the scenario is loaded, so it starts hidden) and
  its content is refreshed from a bus subscription exactly as `BuildFeedCard`/`OnReport`
  does, never from reading scenario state directly — see this file's own note on that
  discipline above.

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
