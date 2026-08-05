# Audio tooling & licence notes (#260 / #41)

Provenance rules for generated and staged audio. Catalogue: [audio-inventory.md](audio-inventory.md).
Runtime: [audio.md](audio.md). Credits: [ATTRIBUTIONS.md](../ATTRIBUTIONS.md).

Folder / `.meta` conventions stay **#261**. API / ffmpeg scripts stay **#400** / **#401**.

---

## Tools in use

| Tool | Used for | Account / tier | Commercial redistrib. in a game build |
|---|---|---|---|
| **Suno** | Music beds, mayday-style stings | User **Pro** (confirmed by owner 2026-08) | **Yes** under Suno Pro/Premier terms — record each shipped track in ATTRIBUTIONS |
| **ElevenLabs** | VO (briefings, ack, chatter) | **Not started** | **Do not bulk-generate** until the paid tier's commercial / game-redistribution terms are read and noted below |
| **FFmpeg** | WAV/MP3 → OGG Vorbis | Local CLI | N/A (tooling only) |
| **ProceduralSfx** | UI / order / combat stubs | In-repo code | Owned — no third-party licence |
| **freesound.org** | Future one-shot SFX | Per-download | **CC0 only** for Strategos (no CC BY until in-game credits exist) |
| **Sonniss GDC** | Future SFX pack | Free annual bundle | Royalty-free, no attribution required — still log year used in ATTRIBUTIONS |

---

## Rules (do this every time)

1. **Staging ≠ licence.** Files under `Research/audio/` are not cleared for shipping.
   Promoting into `Assets/Resources/Audio/` requires an `ATTRIBUTIONS.md` row **in the
   same PR** as the OGG.
2. **One row per shipped clip.** Music table for beds; SFX table for one-shots; add a
   Voice table when the first ElevenLabs (or other) VO ships.
3. **Name the tool + prompt / script source.** Suno → link `Research/audio/suno-prompts.md`
   (or the prompt used). VO → cite `vo-scripts.md` section. Do not leave "AI generated"
   without a tool name.
4. **No mystery staging takes in Resources.** `radio_chatter_*.mp3` and `numberstation.mp3`
   stay staging until provenance is known (see audit below). Prefer regenerating with a
   known tool over guessing.
5. **Field-of-use.** Root `LICENSE` forbids military operational use. Flavour VO stays
   fictional (same register as `ScenarioSamples`) — not real unit designators or ops.

---

## Suno (music / mayday)

- **OK to ship** beds generated on the owner's Pro account into Resources after OGG convert
  (`libvorbis`, q≈4–6 — see [assets.md](assets.md)).
- Shipped today: `menu-loop.ogg`, `play-ambient.ogg` (already attributed).
- Staging: `253.mp3`, `254*.mp3`, `mayday.mp3`, intensity takes — fine in Research; promote
  only with ATTRIBUTIONS + loop-safety check (`suno-prompts.md`).

---

## ElevenLabs (voice) — gate

| Check | Status |
|---|---|
| Account created | No |
| Paid / commercial tier confirmed for game redistribution | **Blocked** |
| Voice id(s) chosen (one HQ, optional NCO) | No — see `vo-scripts.md` profiles |
| `#400` generate script | Not written |

Until the commercial row above is **Yes**, keep VO as scripts only. Placeholder silence /
tone under #258 is fine; do not commit generated speech without this gate.

---

## Staging audit (2026-08-05)

| File | Claimed / likely tool | Ship? | Action |
|---|---|---|---|
| `253.mp3` / `254*.mp3` | Suno (prompts on file) | After OGG | Already source for shipped beds / intensity candidates |
| `mayday.mp3` | Suno (owner) | After OGG + ATTRIBUTIONS | Wire when a mayday cue exists |
| `radio_chatter_1.mp3` / `_2.mp3` | **Unknown** | **No** | Do not promote; replace via ElevenLabs after gate, or delete |
| `numberstation.mp3` | **Unknown** | **No** | Hold for #62; regenerate under known tool or document source |

---

## FFmpeg convert (until #401)

```
ffmpeg -i take.wav -c:a libvorbis -q:a 6 take.ogg
```

Music beds: q≈6. SFX: q≈4–6. Drop into `Resources/Audio/` (or `…/Sfx/`) only with
`.meta` imported as AudioClip (#261) and an ATTRIBUTIONS row.

---

## Suggested next (#41)

1. ~~#259 inventory~~  
2. **#260** — this page  
3. **#261** — Resources conventions  
4. Fill ElevenLabs commercial gate → then **#400** / **#401**
