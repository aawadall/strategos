# Field manual / glossary (#124)

In-game and web reference for command vocabulary and doctrine terms — distinct from
PLAY context help (#308+) and the main-menu alpha HELP overlay.

## Shipped so far

| Issue | Status |
|---|---|
| #205 glossary JSON + one pack | `GlossaryPack` / `GlossaryTerm` + `Resources/FieldManual/alpha-glossary.json` |
| #206 in-app read-only browser | open |
| #207 cross-link one drill | open (terms already carry optional `DrillRefs`) |
| #208 Pages stub | open |
| #209 phases pointer | this page + phases §10 |

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

Probe: **Strategos → Probe Glossary** (`GlossaryProbe`).

The alpha pack ships six terms (MoveTo, Engage, Hold/Dig in, Abort, ORBAT, TTP) with
`T1` cited on MoveTo and TTP.
