# Drill range (#475 W04)

Design note for the first "practice range" scenario — a fixed, repeatable tactical problem
built around one field drill, so the DRILLS binder becomes something the player *uses*
instead of only reads.

Parent: [ttp-epics.md](ttp-epics.md) / [#510](https://github.com/aawadall/strategos/issues/510)
· rung epic [#475](https://github.com/aawadall/strategos/issues/475) ("Train-on-drills:
practice range / guided TTP scenarios") · cadence:
[value-roadmap-52w.md](value-roadmap-52w.md) W04.

## What ships this week (design + skeleton, not yet playable)

- This note.
- `ScenarioSamples.DrillRangeT1()` / `Resources/Scenarios/drill-range-t1.json` — a real,
  `ScenarioProbe`-checked scenario fixture, built and validated the same way every other
  sample is.
- **Not** wired into `MainMenuView` — no menu button yet. That is deliberate: a range only
  earns a place on the menu once it teaches something the raw scenario cannot (W05, see
  below), and an unfinished feature with a menu entry is a worse first impression than one
  that does not exist yet. It stays reachable from `ScenarioIO.Load("drill-range-t1")` for
  probes and manual testing.

## Why T1 first

`T1` — Fire and Movement — is the one drill the field manual already cites: `MoveTo` and
`TTP` both carry it in `DrillRefs` (#207, see [field-manual.md](field-manual.md)). A player
who has already followed that cross-link from the glossary into the DRILLS binder is the
exact player a range should catch next — the teaching path this scenario extends already
exists, rather than being invented alongside it.

`T1` is also mechanically legible without new engine work: `Steps` alternates
`Engage`/`MoveTo` bound `AtThreat`/`TowardThreat` (`Resources/Doctrine/field-drills.json`),
and a unit can already be ordered into a drill it can run (#97, closed) — the drill decomposes
into orders for that one unit; it does not require a second player-controlled element to
alternate with.

## The fixture

`DrillRangeT1()` deliberately mirrors `Tutorial()`'s shape (squad echelon, small flat map,
erosion/culture off — see `ScenarioSamples.Tutorial`) rather than inventing a new one, but
changes what the ground is *for*:

- One BLUFOR squad, player-controlled, echelon Squad.
- One static OPFOR squad, `Halted`/`ReturnFire`, dug into the only covered approach —
  fixed and repeatable, which is what makes it a *range* rather than a skirmish. Full
  strength and moderate training: it answers fire like a real position, not a scarecrow.
- One objective on the OPFOR position. Victory is BLUFOR holding it — there is no draw
  condition tuned for "the player never engaged," because a range does not need one.
- `Description` names the DRILLS binder and `T1` explicitly, the same way a scenario
  briefing names a directive. No `HistoricalNote` — that field is authored historical
  commentary (#462/#421) and this scenario has none to give.

Crossing the open approach without alternating fire and movement costs suppression the
same way it would anywhere else in the sim — nothing about combat resolution is special-
cased for a range. What is special-cased is the ground itself: unlike a generated skirmish,
the same approach, the same OPFOR posture, and the same result are there every time, which
is what "practice" requires and a procedurally seeded meeting engagement does not promise
on its own (two different seeds can hand the same drill very different terrain).

## What "playable" (W05) still needs

None of this is graded yet. Ordering `T1` and ordering a bare `MoveTo` both currently just
run the sim — there is no feedback that names the difference. W05's gap, concretely:

- A context help card (`ContextHelp` / `AlphaHelpOverlay`, matching the project's other
  addon pattern) that appears on this scenario and names the drill by code.
- Some signal, even a thin one, that the player used the drill rather than a raw order —
  `CommandLog` already carries `CommandKind`, so this is a read over existing data (same
  shape as the AAR critique epic, #421), not new instrumentation.
- Only then does a menu entry (or a dedicated "RANGE" section) make sense.

## Open questions (not blocking this week)

- Multiple drills mean multiple ranges; whether they share one scenario family or get a
  dedicated `Resources/Scenarios/Ranges/` folder is a W05+ call once there is a second one.
- Whether a range should be replayable to a clean state without reloading the whole scenario
  (a soft reset) is a real player-facing gap once this becomes playable, but the shipped
  scenario model has no such concept today and none is added here.
