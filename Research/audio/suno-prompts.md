# Suno prompts — menu loop and PLAY ambient beds

Draft prompts for #43 (soundtrack epic) and its children — #253 (menu loop slot), #254
(PLAY ambient bed), #255 (provenance path doc). Nothing here is generated or committed as
audio; these are the prompts to run, and the provenance/licence discipline to follow once
something comes back from them.

Tooling per `ATTRIBUTIONS.md`/#41: **Suno**, generated stems. Confirm the account tier's
commercial/redistribution terms before generating for real (#41) — record what produced
each track and under what terms in `ATTRIBUTIONS.md`'s Music table the moment a real clip
is chosen, not after a folder of takes has piled up with no memory of which prompt made
which file.

**Loop-safety is non-negotiable for both slots below.** Both are beds a player can sit
inside for minutes at a time (a menu, or the calm stretch of a scenario); an audible seam
or a swell that doesn't return to its start level reads as a bug the moment it repeats.
Ask for an explicit loop point and verify it before shipping.

---

## Menu loop (#253)

Plays under whatever front-door menu #371 eventually builds — calm, waiting-room register,
not a title-screen fanfare. This project's whole visual identity is the aged-paper /
operations-map look (`UiTheme`, `PaperTexture`) — the music should sit under that, not
compete with it.

> **Prompt:** Instrumental military ambient, slow tempo (60–70 BPM), sparse arrangement —
> single sustained low strings or a soft brass pad, a distant, irregular field-radio
> static texture underneath, occasional muted low piano or vibraphone note. Restrained and
> procedural, like a quiet command post before an operation starts, not tense or dramatic.
> No percussion, no melody hook, no vocals. Seamless loop, no fade in/out, 90–120 second
> loop length.

**Why sparse, specifically:** a menu loop is heard the longest and the most repeatedly of
any track in the game — anything with a hook becomes fatiguing fastest. Restraint here
buys more replay tolerance than a "better" piece of music with a memorable melody would.

---

## PLAY ambient bed (#254)

Under a live scenario during its calm stretches — before contact, or once a firefight has
resolved. Should read as continuous with the menu loop's world (same instrumental
palette) but slightly more present/awake, since the player is actively commanding, not
waiting.

> **Prompt:** Instrumental military ambient, low tension, 70–85 BPM, minimal percussion —
> a soft, irregular low tom or brushed snare texture at low volume, distant wind or
> open-terrain ambience layered under a sparse string or synth pad, occasional single
> notes from a muted brass or woodwind, no clear melodic phrase. Alert but not urgent —
> the mood of a unit in the field, watching, not yet in contact. Seamless loop, no fade
> in/out, 120–180 second loop length.

---

## Combat-intensity variant (forward-looking; folded into #43, not its own child yet)

`docs/phases.md`'s audio section carries *"dynamic music system: tension ramps with
combat intensity (#43)"* — tagged to the soundtrack epic, but not yet broken out as one of
its ~5-minute children (#253–255 are menu loop, PLAY bed, and provenance doc only). No
engineering work is proposed here for it. This is only the prompt, provided now since it
was asked for, so it exists when someone does break it into its own child rather than
being drafted from nothing at that point.

> **Prompt:** Instrumental military tension cue, 100–120 BPM, driving but restrained low
> percussion (low toms, muted taiko-style hits), a rising low brass or string ostinato,
> occasional dissonant sting on a report of contact — no triumphant melody, this is
> tension, not victory. Should crossfade convincingly from the PLAY ambient bed above
> (same key/tempo family) rather than cut hard into it. Seamless loop, 60–90 second loop
> length, with a clear low-intensity entry point and a clear high-intensity peak section
> that could be layered or cut to independently.

**Time-compression note, same concern #40 already raises for SFX:** a tension cue that
reacts to every `Engaged` report at ×300 compression will thrash between beds many times a
second. Whoever builds this needs a debounce/hysteresis window on the intensity signal,
not a direct per-event trigger — flagging it here so it isn't rediscovered as a bug later.

---

## Out of scope here

Actually running these prompts, choosing a take, `AudioMixer` bus wiring (#40), and the
tension-detection code for the combat variant — this file is prompts and reasoning only.
