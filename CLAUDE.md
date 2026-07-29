# CLAUDE.md — Strategos

Working notes for this repository: commands, invariants, and traps that are not visible
from reading the code.

**Where facts belong:** this file is the authority for *commands, invariants and gotchas*.
`docs/nato-symbol-generator.md` is the authority for *APP-6D reference detail* (SIDC
tables, frame shapes, layer model). If you change symbol behaviour, update both.

---

## Project

Unity 6 (`6000.0.75f1`) / URP tactical command simulation built on NATO APP-6D symbology.

**Only Phase 2 (the symbol system) is implemented.** Everything else in `ROADMAP.md` —
terrain, units, combat, C2, scenarios, AI, networking — is unbuilt. The runtime is a demo
scene containing a symbol builder panel; there is no game loop yet.

| Path | Contents |
|---|---|
| `Assets/Scripts/Core/NatoSymbols/` | The symbol system (all of it) |
| `Assets/Scripts/Demo/` | Demo scene behaviours, incl. `SymbolBuilderPanel` |
| `Assets/Editor/` | Build pipeline, symbol editor window, TMP importer, contact sheet |
| `docs/` | Reference docs; `phases.md` is the task breakdown |

---

## Build & verify

```powershell
.\scripts\build.ps1 -Target Windows64      # Windows64 | Linux64 | macOS | WebGL | All
.\scripts\capture.ps1                      # launch player, screenshot, close
```

Bake a grid of symbol permutations — **prefer this over clicking the GUI** when checking
rendering changes:

```powershell
# Menu: Strategos > Bake Symbol Contact Sheet
& "C:\Program Files\Unity\Hub\Editor\6000.0.75f1\Editor\Unity.exe" `
    -batchmode -quit -nographics -projectPath . `
    -executeMethod Strategos.Editor.SymbolContactSheet.Bake -logFile sheet.log
# -> Artifacts/symbol-contact-sheet.png
```

Player log (**always check after a UI change**, see UI gotchas below):

```
%USERPROFILE%\AppData\LocalLow\DefaultCompany\strategos\Player.log
```

Editor menu: `Strategos → Build/…`, `NATO Symbol Generator`, `Open Demo Scene` (F5),
`Recreate Demo Scene`, `Bake Symbol Contact Sheet`, `Import TMP Essential Resources`.

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

---

## Glyph coverage

The bundled LiberationSans SDF atlas has **no geometric-shape glyphs**. These render as
tofu boxes: `▾` U+25BE, `○` U+25CB, `•` U+2022 (risky), `−` U+2212.

Latin-1 is safe: `·` `±` `+` `-`. For shapes, draw procedurally instead — see
`ArrowSprite` in `SymbolBuilderPanel`, which generates the dropdown arrow as a texture.

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
