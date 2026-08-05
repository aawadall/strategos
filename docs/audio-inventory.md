# Audio inventory (#259 / #41)

Master catalogue of audio **by category** — owners, tools, and where each clip lives.
Extends the one-shot SFX list from [#44](sfx-inventory.md). Licence / provenance prose
is **#260**; Resources folder `.meta` rules are **#261**. Conversion scripts are
**#400** (ElevenLabs) and **#401** (OGG).

Runtime wiring: [audio.md](audio.md). Attribution for anything shipped:
[ATTRIBUTIONS.md](../ATTRIBUTIONS.md).

**Tooling (as of 2026-08-05):** music and mayday-style beds → **Suno** (user Pro).
Voiceover → **ElevenLabs** (not started — scripts only). One-shots → procedural stubs
first, then freesound CC0 / Sonniss.

---

## Status legend

| Status | Meaning |
|---|---|
| **shipped** | Under `Assets/Resources/Audio/` and loadable in a player build |
| **staging** | Under `Research/audio/` — not packaged; convert before promote |
| **procedural** | Synthesised at runtime (`ProceduralSfx`) — no file yet |
| **planned** | Named in scripts/prompts; no take yet |
| **deferred** | Epic exists; do not source until that epic asks for a clip |

---

## Music (Suno)

| Id / role | Tool | Status | Path | Notes |
|---|---|---|---|---|
| Menu loop | Suno | **shipped** | `Resources/Audio/menu-loop.ogg` | From `Research/audio/253.mp3`; prompt in `suno-prompts.md` |
| PLAY ambient | Suno | **shipped** | `Resources/Audio/play-ambient.ogg` | From `Research/audio/254.mp3` |
| PLAY high intensity A | Suno | staging | `Research/audio/254_high_intesity_1.mp3` | Typo in filename kept; combat-intensity bed (#43 leftover) |
| PLAY high intensity B | Suno | staging | `Research/audio/254_high_intensity_2.mp3` | Same family |
| Contact / victory / defeat / night | Suno | planned | — | Target list in [assets.md](assets.md) Music table |

Owner: soundtrack epic leftovers / `#43`. Do not commit new Suno takes without an
`ATTRIBUTIONS.md` Music row (#260).

---

## Sound effects (one-shots)

Cue checklist and priorities: [sfx-inventory.md](sfx-inventory.md). Summary:

| Id | Tool / owner | Status | Call site |
|---|---|---|---|
| `ui-click` / `ui-select` | Procedural → later OGG | **procedural** (#250) | `AudioService.PlayUiClick` / `PlayUiSelect` |
| `order-issued` / `order-rejected` | Procedural → later OGG | **procedural** (#251) | `PlayOrderIssued` / `PlayOrderRejected` |
| `combat-fire` | Procedural → later OGG | **procedural** (#252) | `PlayCombatFire` on `Engaged` |
| `combat-hit` / `unit-destroyed` | freesound / Sonniss | planned | Ids reserved; no call site yet |
| Alert / contact sting | freesound | planned | P2 in sfx-inventory |
| Objective taken | freesound / Suno sting | planned | P2 |

Epic **#44** closed on procedural stubs. Drop-in OGGs keep the same resource ids under
`Resources/Audio/Sfx/` (#261).

---

## Voice / narration (ElevenLabs)

Scripts: `Research/audio/vo-scripts.md` (#42). **No ElevenLabs generation yet.**

| Role | Tool | Status | Path / source | Epic |
|---|---|---|---|---|
| Directive briefing (HQ) | ElevenLabs | planned | Scripts only — Meeting Engagement / Highland / template | #256–#258, #395 |
| Command ack (Wilco) | ElevenLabs | planned | `CommandKind` lines in vo-scripts | #397 |
| Radio chatter (`ReportKind`) | ElevenLabs | planned | Contact / Engaged / … variants | #396 |
| Radio chatter take 1 | *(unknown — listen before attributing)* | staging | `Research/audio/radio_chatter_1.mp3` | Audit under #260 |
| Radio chatter take 2 | *(unknown)* | staging | `Research/audio/radio_chatter_2.mp3` | Audit under #260 |
| Mayday / distress sting | Suno | staging | `Research/audio/mayday.mp3` | User: Suno; not wired |

One voice per BLUFOR role (see vo-scripts). Confirm ElevenLabs commercial terms before
bulk generate (#260 / #400).

---

## Morse / number stations / radio texture

| Role | Tool | Status | Path | Epic |
|---|---|---|---|---|
| Number-station flavour take | *(staging)* | staging | `Research/audio/numberstation.mp3` | #62 / #46 adjacent |
| Morse encode → schedule | Procedural | planned | Code only | #244–#246 |
| Comms degradation FX | Procedural / DSP | planned | No clip — effect on radio bus | #45 / #247–#248 |
| Radio static bed | Suno / freesound | planned | Search terms in assets.md SFX | Texture under music already |

Do not treat `numberstation.mp3` / chatter takes as licensed until #260 records provenance.

---

## Folder map (today)

```
Research/audio/                 # staging — not in player builds
  suno-prompts.md
  vo-scripts.md
  253.mp3 / 254*.mp3            # Suno music takes
  mayday.mp3
  numberstation.mp3
  radio_chatter_1.mp3 / _2.mp3

Assets/Resources/Audio/         # shipped beds
  menu-loop.ogg
  play-ambient.ogg
  Sfx/                          # reserved; procedural stubs skip files for now
```

---

## Suggested work order (#41)

1. **#259** — this inventory (shipped)  
2. **#260** — licence / provenance notes (Suno Pro, ElevenLabs TBD, staging audits)  
3. **#261** — Resources conventions + `.meta` expectations  
4. **#400** / **#401** — ElevenLabs script + OGG conversion once Voice terms are decided  

Cross-links: [assets.md](assets.md) · [sfx-inventory.md](sfx-inventory.md) ·
[audio.md](audio.md) · `Research/audio/`.
