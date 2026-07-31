# Symbol invariants

SIDC layout, symbology conventions and the rendering rules that make a
symbol silently wrong rather than erroring. **Read before touching `Core/NatoSymbols`.**

[docs/nato-symbol-generator.md](nato-symbol-generator.md) is the APP-6D reference detail;
this is the working rules. Note that amplifier text baked into symbols does **not** use TMP —
it uses the 5x7 bitmap font in `ProceduralDrawUtil` — so the glyph-coverage rules in
[ui-invariants.md](ui-invariants.md) apply to the UI, not to symbols.
[CLAUDE.md](../CLAUDE.md) is the index.

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
