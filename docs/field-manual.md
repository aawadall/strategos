# Field manual / glossary (#124)

In-game and web reference for command vocabulary and doctrine terms — distinct from
PLAY context help (#308+) and the main-menu alpha HELP overlay.

## Shipped so far

| Issue | Status |
|---|---|
| #205 glossary JSON + one pack | `GlossaryPack` / `GlossaryTerm` + `Resources/FieldManual/alpha-glossary.json` |
| #206 in-app read-only browser | Pause → **FIELD MANUAL** → `FieldManualBrowserPanel` (list + detail) |
| #207 cross-link one drill | **DRILLS** binder + pause quick-ref show glossary terms for `T1`; manual detail lists `DrillRefs` |
| #208 Pages stub | open |
| #209 phases pointer | this page + phases §10 |

## In-app browser (#206)

From PLAY, Esc (pause) → **FIELD MANUAL**. Nested overlay loads
`GlossaryIO.Load("alpha-glossary")`, lists terms, shows Title/Body. Esc closes the
manual before pause. Not the DRILLS binder and not `AlphaHelpOverlay`.

Probe: **Strategos → Probe Glossary** also Build/Open/Close the browser panel.

## Drill cross-links (#207)

`GlossaryTerm.DrillRefs` cites doctrine codes (alpha pack: **T1** on MoveTo and TTP).

- **Binder → term:** `TtpView` and pause **DRILLS** quick-ref append a Field Manual line
  for the selected / listed drill via `GlossaryIO.TermsForDrill`.
- **Term → drills:** field-manual detail appends `Related drills: T1` and points at the
  DRILLS tab. `OpenOnDrill(code)` selects the first citing term (probe uses T1).

## JSON shape (#205)

```
GlossaryPack
  Name, Source
  Terms[]:
    Id        stable kebab / short code
    Title     display heading
    Body      plain-language copy
    DrillRefs optional string[] of doctrine drill codes (e.g. T1)
```

Load: `GlossaryIO.Load("alpha-glossary")` via `ResourcesGlossaryPackSource`
(`Resources/FieldManual/`). Same fields-only Newtonsoft contract as scenarios / doctrine.

The alpha pack ships six terms (MoveTo, Engage, Hold/Dig in, Abort, ORBAT, TTP) with
`T1` cited on MoveTo and TTP.
