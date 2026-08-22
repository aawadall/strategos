# TTP / doctrine — epic of epics (#510)

Meta-tracking for **tactics, techniques, and procedures**: the binder players thumb,
the drills they issue, the glossary that defines them, the teaching/reward loop that
makes fluency pay off, and **sourcing the doctrine text itself** from Distro A pubs.

GitHub: [#510](https://github.com/aawadall/strategos/issues/510).  
Product rung that *charges* for this arc: [`$15` Drill school](price-ladder.md) (#475)
under price-ladder meta [#471](https://github.com/aawadall/strategos/issues/471).

| Arc | Epic | Status |
|---|---|---|
| Binder (read + execute) | [#61](https://github.com/aawadall/strategos/issues/61) · [#97](https://github.com/aawadall/strategos/issues/97) | **done** |
| Campaign arc (drills in climb) | [#78](https://github.com/aawadall/strategos/issues/78) | **done** |
| **Source TTP content (Distro A)** | [#513](https://github.com/aawadall/strategos/issues/513) | **open** (#514–#517) |
| Field manual / glossary | [#124](https://github.com/aawadall/strategos/issues/124) | **done** (#205–#209) |
| Teach in PLAY (AAR / commentary) | [#421](https://github.com/aawadall/strategos/issues/421) | **open** |
| Bar medals | [#467](https://github.com/aawadall/strategos/issues/467) | **partial** — post-battle panel + Merit kills/objective; career rack open |
| **$15 Drill school** | [#475](https://github.com/aawadall/strategos/issues/475) | **open — next chargeable** |
| Echelon-gated authoring | [#65](https://github.com/aawadall/strategos/issues/65) | **open** (#228–#231) |

## Sourcing doctrine (#513)

Shipped `field-drills.json` is **FM 7-8 lineage, abridged** — not a full-text import.
Expand packs from **Distro A** pubs (e.g. [armypubs.army.mil](https://armypubs.army.mil)
ATP 3-21.8) into `Resources/Doctrine/` with Research digests and
[ATTRIBUTIONS.md](../ATTRIBUTIONS.md). Never commit copyrighted PDFs under Research
(same rule as historical notes).

| Child | Task |
|---|---|
| [#514](https://github.com/aawadall/strategos/issues/514) | Provenance inventory of shipped packs |
| [#515](https://github.com/aawadall/strategos/issues/515) | `Research/doctrine/` digest template |
| [#516](https://github.com/aawadall/strategos/issues/516) | +1–2 drills from one Distro A ATP (W11 shape) |
| [#517](https://github.com/aawadall/strategos/issues/517) | ATTRIBUTIONS row for doctrine pubs |

## What already ships

- `Resources/Doctrine` JSON packs + DRILLS binder (`TtpView`)
- Drill execution, group addressing, T/P/U readiness on PLAY
- Pause **DRILLS** quick-ref
- Field manual (#124 done): pause browser, Pages stub, **T1 ↔ glossary** (#207) — see
  [field-manual.md](field-manual.md)
- Drill range design + T1 skeleton (#475 W04) — `drill-range-t1` fixture, not yet menu-wired
  or graded — see [drill-range.md](drill-range.md)

## Cadence

Weekly plan ([value-roadmap-52w.md](value-roadmap-52w.md) / #483): Q1 (`$15`) is the
primary TTP teaching quarter (drill range, medals, AAR, historical fights, glossary).
**W11** is the first explicit ATP doctrine expand week (#516 / #475). Authoring (#65)
and national doctrine profiles stay later unless a week claims them.

## Rule

Close a child when *its* gates are clear. `#475` can close while `#65` stays open —
players can learn shipped drills before they author new ones. `#513` can continue after
`$15` clears.

Update this table in the same change when a child epic flips.
