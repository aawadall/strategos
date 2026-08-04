# Voiceover scripts — directive briefings and radio chatter

Draft VO content for #42 (voiceover/narration epic) and its children — #256 (play-on-ACK
hook), #257 (Resources layout), #258 (placeholder briefing, currently silence/tone only).
Scripts here are the "real VO later" #258 explicitly defers; nothing here is implemented,
this is source material for whoever records or generates it.

Tooling per #41: **ElevenLabs**, one consistent voice per role — a commander who changes
voice between missions reads as a bug. Confirm the account tier's commercial/redistribution
terms before generating in bulk (#41), and record provenance in `ATTRIBUTIONS.md` per
asset, the same way `Research/historical/`'s notes cite their sources.

**Field-of-use note:** this project's licence carries a non-military-use restriction (see
root `LICENSE`). The content below is fictional wargame flavour text — the same register
`ScenarioSamples.cs` already ships (fictional units "3 BDE", "2 MRR", fictional place names)
— not real operational material, consistent with everything else authored in this repo.

---

## Voice profiles

One voice per side, matching the US/Soviet split `Side.RankLadder` already established
(#38, #125):

| Side | Role | Register |
|---|---|---|
| BLUFOR (US-pattern) | Higher HQ (directive briefings, "3 BDE") | Clipped, professional, American English |
| BLUFOR | Own-unit acknowledgements (Wilco/contact calls) | Same voice as HQ, or a second consistent NCO-register voice — decide once, keep it |
| OPFOR (Soviet-pattern) | Ambient/observed-only flavour, if ever voiced | Distinct accent/register — OPFOR is rarely "heard" directly since the player only sees what their own side reports |

Do not vary the HQ voice per scenario. A player who hears "3 BDE" sound like a different
person in the next mission has lost the one thing a recorded voice is supposed to buy —
continuity.

---

## Directive briefings (play-on-ACK, #256)

Spoken on the same trigger the `ACKNOWLEDGE` button already fires (`Directive`,
`Assets/Scripts/Core/Directives/Directive.cs`). Real content, not placeholder — pulled
directly from what `ScenarioSamples.cs` already ships, read as a briefing rather than
displayed as a text block.

### Meeting Engagement ("Valley" / `skirmish`)

> *[HQ, 3 BDE]* "Task force, this is 3 Brigade actual. Your mission: seize and hold
> Objective Anvil. [pause] Second Motor Rifle Regiment is advancing through the valley —
> deny them that ground, and hold the open approach clear for our follow-on forces.
> [pause] Constraints: do not become decisively engaged beyond the objective. Preserve
> your combat power — the brigade's main effort still needs it. [pause] Deadline,
> twelve-hundred. Acknowledge."

### Highland Opening (`highland-opening`)

> *[HQ, 3 BDE]* "Task force, this is 3 Brigade actual. Open the highland approach. Hold
> the regiment's assigned ground and deny the enemy the ridge line — our follow-on forces
> need it clear. Acknowledge."

### Placeholder / generic template (for any future authored scenario)

> *[HQ, {From}]* "Task force, this is {From} actual. {Intent}. [pause] {Constraints — if
> present}. [pause] Deadline, {DeadlineTick, spoken as a time}. Acknowledge."

Keep the template's phrasing close to the literal `Directive.Intent`/`Constraints` text
rather than paraphrasing — the spoken and the on-screen directive card should say the same
thing, or a player who reads ahead of the audio (compression, or just faster reading) gets
two versions of the same order.

---

## Radio chatter (per `ReportKind`, `SituationReport.cs`)

Two to three short variants per kind, for repetition relief — a report that fires often
(`Contact`, `Halted`) needs more variety than one that fires once (`Destroyed`). Keep every
line under ~2 seconds spoken; these are barked, not narrated.

| `ReportKind` | Variant lines |
|---|---|
| `Contact` | "Contact, [bearing]." / "Enemy in sight." / "We've got contact." |
| `ContactLost` | "Contact lost." / "They're gone from view." |
| `Arrived` | "In position." / "We're at the objective." / "Arrived, holding." |
| `OrderCompleted` | "Done." / "Task complete." / "Order carried out." |
| `OrderFailed` | "Can't comply." / "Negative, unable." / "Order's no good from here." |
| `Halted` | "Holding up." / "We've stopped." / "Blocked, standing by." |
| `Engaged` | "Taking fire!" / "Engaged, returning fire." / "Contact, weapons free." |
| `Depleted` | "Winchester." / "Out of ammo." / "Dry — need resupply." |
| `Destroyed` | "[unit callsign] is down." / "We're hit — going off net." |

**Vary by `Confidence`** (`Confirmed` / `Suspected` / `Possible`, `SituationReport.cs`) for
`Contact` specifically — a `Possible` contact should sound less certain than a `Confirmed`
one: *"Possible contact, can't confirm"* vs. *"Contact confirmed, [bearing]."* This is the
one place VO can carry information the on-screen report already carries but the player
processes faster by ear under pressure.

**Time-compression note (#40's own concern):** at high compression a busy firefight can
produce this report kind dozens of times a second. Do not queue one clip per event — pick
one voice line per burst (rate-limited or first-of-batch only) and let the visual log
carry the rest, or the audio degrades into an unintelligible pile the moment compression
climbs, which is the exact failure #40 already names.

---

## Command acknowledgements (per `CommandKind`, `Command.cs`)

Short, said once when a unit accepts an order — not per tick, not per queue check.

| `CommandKind` | Lines |
|---|---|
| `MoveTo` | "Moving." / "Wilco, moving out." |
| `Engage` | "Engaging." / "Wilco, opening fire." |
| `Defend` / `Hold` | "Holding this ground." / "Digging in." |
| Drill (coded, e.g. `2`, `36B`) | "Executing [drill name]." — say the drill's plain name, not its code; the code is for the player's binder, not the radio |

---

## Out of scope here

Actual recording/generation, the `Resources` clip layout (#257), and the play-on-ACK
wiring itself (#256, code) — this file is the script only.
