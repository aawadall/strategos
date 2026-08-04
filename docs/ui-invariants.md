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

**#371 hierarchy.** The app boots into `MainMenuView` (`-view menu`), not PLAY. PLAY is a
*session* entered from the menu (or `-view play` for capture). EXPLORE / SCENARIO / DRILLS /
BUILDER are Tools reachable from the menu or the tab strip. The tab strip is hidden on the
main menu; a **MENU** tab returns from Tools/PLAY.

**Settings shell (#306 / #307 / #389).** `SettingsView` (`-view settings`) is a no-tab screen
opened from menu Options. GRAPHICS has a **FULLSCREEN** toggle that calls
`AppShell.ApplyFullscreen` / `ApplyWindowed` and persists `PlayerPreferences.Fullscreen`
(same path as F11). AUDIO / ACCESSIBILITY stay empty; GAMEPLAY has persisted
`ConfirmOrders`. Windowed size presets are #390; boot apply is #391. Tab strip stays
hidden the same way as the menu. Preference round-trip is probed by `PreferenceStoreProbe`
(#307); tutorial Validate is #311.

**Display mode (#387 / #388 / #389 / #385).** F11 calls `AppShell.ToggleFullscreen`, which
shares `ApplyWindowed` / `ApplyFullscreen` with Settings. Fullscreen is borderless
`FullScreenWindow` at the current display size; windowed restores a remembered size
(default 1600×900, or prefs WxH from Settings). `PlayerPreferences` carries `Fullscreen`,
`WindowWidth`, `WindowHeight` (#388); boot apply is #391; size presets UI is #390.
`DisplayModeProbe` checks the AppShell API + GRAPHICS category; `PreferenceStoreProbe`
round-trips the display fields.

**Esc precedence in PLAY (#371 / #129 / #308):** drills quick-ref closes first; then context
help; then the pause overlay resumes; then an armed palette verb clears
(`CommandPalette.ClearShortcut`); else Esc opens pause and stops the clock. Space remains
the clock toggle and must not open pause.

**Context help (#308).** PLAY rail **HELP** opens `ContextHelpOverlay` for the armed verb.
Only **MOVE** has authored copy today — other verbs get a stub pointing at MOVE / #124.
Distinct from the field manual (#124).

**Tutorial first beat (#310).** Loading `tutorial-squad` shows a non-blocking banner:
select a player-commanded unit, then issue MoveTo through the normal `IssueMoveTo` path.
Not a scripted fake order.

**Pause overlay** is built under PlayView's host (one Canvas — no second EventSystem). Save /
Load call the same quicksave path as the rail; Exit returns to the main menu without
destroying the session (Resume / Continue can re-enter).

**In-session drills** from pause are a *quick-reference lookup* (interpretation a of #371),
not optional quests. Full binder remains the DRILLS tab; issuing drills stays on PLAY's rail.

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
  generation counter, the one cached symbol factory, the live `Simulation` reference, and
  (when PLAY is in a multi-op session) `ActiveChain` / `ActiveOperationIndex` (#139). It
  holds no textures on purpose, so disposal stays with whichever view allocated them. Views
  compare `AppSession.Generation` in `OnShown` and re-render only if it moved.
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
- **Control measures paint into sheet pixels after `RenderPixels` (#160–#166).**
  `MapSheetCard.Render(..., afterPixels)` is the hook; PLAY passes `ControlMeasureDrawer`
  over `Scenario.ControlMeasures` with `PlayerSide` as viewer, then `WorldObjectDrawer` for
  live hazards (#34). Not UI markers like
  objectives — baked into the texture. Distinct from `OrderTrackLayer` live-plan arrows.
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
  The drill dropdown has the same shape (`_drillOptionsKey`): each entry is annotated with the
  selected unit's T/P/U from `TtpReadiness.Assess`, and rebuilding every tick would close the
  list under the pointer — so the key is unit identity plus rounded effectiveness, not "always".
- **The player's rank on the top bar is a shoulder board, not a text label (#38).** It is
  derived from the highest echelon on the player's ORBAT and looked up on `Side.RankLadder` —
  never a free-floating rank field. Marks are procedural (`RankInsignia`), same reason as the
  dropdown arrow: the SDF atlas has no stars or chevrons. Prefab PNGs in Resources were
  considered and rejected for store cost — one insignia is on screen at a time.
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
- **Shipped scenarios load from the CAMPAIGN card, not only on Build (#133).** `Build` still
  defaults to skirmish so a cold start has a map; **SKIRMISH ONLY** and **PUSH NORTH** call
  the same `LoadScenario(name)` path so every `Resources/Scenarios` sample is reachable
  without editing code. A full gallery of generated maps is out of scope here.
- **PLAY's command palette arms verbs from a table, not from click-handler branches (#127–#129 / #54).**
  `CommandPalette.Verbs` is the row list the rail iterates; `PaletteVerb.None` is select-only
  and is not a table row. MOVE / ENGAGE armed left-clicks issue via `PaletteVerbDef.Kind`
  through the same `Command` / bus helpers as the right-click shortcut; a miss (Engage on
  empty ground, no selection) issues nothing and leaves the verb armed. WAYPOINTS is a
  separate `PaletteVerb.Id` (same `CommandKind.MoveTo`) that opens a **draft session**: click
  places a point, drag moves a handle, click a handle removes it; Enter / CONFIRM ROUTE
  commits as N ordinary queued `MoveTo`s (Shift appends; otherwise Abort once then queue).
  Esc / SELECT clears the draft without issuing. Pending and draft legs draw through
  `MoveToExecutor.PlanCells` (same Find → Simplify → Smooth as the executor), not straight
  lines. DIG IN (#33) is a fourth table row (`CommandKind.Defend`); a confirming click
  expands via `SpecialAction.TryCreate(DigIn)` into Hold/Defend when `CanDigIn`, same as the
  HOLD button. Keyboard arming reads `Shortcut` / `ClearShortcut` via `TryReadArmingKey` — do not
  hard-code M/E/W/D/Esc in `PlayView.Update`. Space stays the clock; the probe fails if a verb
  steals it. Right-click remains its own engage-or-march path and must not be redefined by
  the table (#53). Plan-card CANCEL uses `QueuedCommand.Ordinal` for CancelFrom (#57).
  Config-loading the table is #130 and must not block the in-code table.
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
