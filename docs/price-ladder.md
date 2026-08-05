# Price ladder — milestone epics (#471)

Product thermometer from **$0 → $30** in playable rungs. Canvas:
`ten-dollar-game` (Cursor). Meta-epic: [#471](https://github.com/aawadall/strategos/issues/471).

| Price | Name | Epic | Status |
|---|---|---|---|
| $0 | Sandbox | [#472](https://github.com/aawadall/strategos/issues/472) | done |
| Free | Public alpha | [#473](https://github.com/aawadall/strategos/issues/473) | done |
| $10 | Impulse buy | [#474](https://github.com/aawadall/strategos/issues/474) | done |
| $15 | Drill school | [#475](https://github.com/aawadall/strategos/issues/475) | **open — next** |
| $20 | Honest fight | [#476](https://github.com/aawadall/strategos/issues/476) | open |
| $25 | Steam EA | [#477](https://github.com/aawadall/strategos/issues/477) | open |
| $30 | 1.0 | [#478](https://github.com/aawadall/strategos/issues/478) | open |

## Themes

| Rung | Player gets |
|---|---|
| $15 | Teaching + reward chrome (drills, AAR, medals, history) |
| $20 | Sim depth (LOS fog, indirect, ZoC, formations, supply) |
| $25 | Steam + authoring + unlocks (editor, Pass, Deck, Linux) |
| $30 | AI, online, Workshop/#464, weather, Achievements, macOS |

FoW / LOS lands in **$20** (#476) — sensor physics that feeds the C2 COP, not a new
order type. C2 *foundations* already ship under $0–$10.

## Related design notes

- [tiered-releases.md](tiered-releases.md) (#465) — Pass SKUs → gate on #477
- [bar-medals.md](bar-medals.md) (#467) — medals → gate on #475
- Scenario server (#464) → gate on #478

Close a child epic when its canvas gate tasks are `done`. Update this table in the
same change.
