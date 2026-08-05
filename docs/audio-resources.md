# Audio Resources conventions (#261 / #41)

Where generated / converted audio is **committed**, how Unity `.meta` sidecars must look,
and what must **never** land under `Resources/`. Catalogue: [audio-inventory.md](audio-inventory.md).
Licence gates: [audio-licence.md](audio-licence.md). Runtime loaders: [audio.md](audio.md).

---

## Two trees

| Tree | Ships in player? | Purpose |
|---|---|---|
| `Research/audio/` | **No** | Staging takes (Suno MP3s, VO drafts, mystery clips). Gitignored patterns may apply to bulky takes; committed drafts are ok when small and attributed in comments / inventory. |
| `Assets/Resources/Audio/` | **Yes — every build** | Only clips `AudioService` (or a future VO player) loads **by name** via `Resources.Load<AudioClip>(…)`. |

`unity-gotchas.md`: everything under `Assets/Resources/` is packed unconditionally. Do not
dump a Suno session folder here.

---

## Layout (committed)

```
Assets/Resources/Audio/
├── menu-loop.ogg              # bed — AudioService.MenuLoopResource = "Audio/menu-loop"
├── menu-loop.ogg.meta
├── play-ambient.ogg           # bed — AudioService.PlayAmbientResource = "Audio/play-ambient"
├── play-ambient.ogg.meta
└── Sfx/                       # one-shots — load path "Audio/Sfx/<id>" (no extension)
    ├── ui-click.ogg           # optional once procedural stubs are replaced
    ├── ui-click.ogg.meta
    └── …
```

Load paths **omit** `Assets/Resources/` and the file extension. Match the constants /
sfx-inventory ids exactly (`ui-click`, not `UI_Click`).

Intensity beds, victory/defeat, VO, Morse: add folders only when a call site exists
(`Audio/Vo/`, `Audio/Combat/` …). Do not pre-create empty trees.

---

## Format

| Kind | Container | Encoder | Notes |
|---|---|---|---|
| Music beds | `.ogg` | `libvorbis` q≈6 | Loop-safe; verify seam before promote |
| SFX one-shots | `.ogg` | `libvorbis` q≈4–6 | Short; mono ok |
| Staging only | `.mp3` / `.wav` | whatever the tool emits | Stay in `Research/audio/` |

Do not commit `.wav` under Resources — convert first (`audio-licence.md` FFmpeg snippet;
scripted convert is #401).

---

## `.meta` expectations

1. **Always commit the `.meta` with the clip** in the same PR. A fresh clone without it
   regenerates a new GUID; nothing breaks for `Resources.Load` by path, but Git diffs and
   any future asset references will thrash.
2. Unity creates `AudioImporter` metas on first import. Prefer letting the editor write them
   (open project once after adding the OGG) rather than hand-authoring.
3. Acceptable defaults for beds/SFX today (as on shipped `menu-loop.ogg.meta`):
   - `loadType: 0` (Decompress On Load) — fine for short loops; revisit Streaming for long
     tracks later
   - `compressionFormat: 1` (Vorbis) with `quality: 1` (editor default for OGG import)
   - `forceToMono: 0`, `3D: 1` (Unity default — 2D UI/SFX still play; change only if a
     probe shows a problem)
4. **Do not** set `Resources` audio as Addressables or move them out of Resources without
   changing `AudioService` load paths in the same change.

---

## Promote checklist (Research → Resources)

1. Licence row ready — [audio-licence.md](audio-licence.md) + `ATTRIBUTIONS.md` in the **same PR**.
2. Convert to OGG; name matches the inventory id / `AudioService` constant.
3. Drop under `Assets/Resources/Audio/` (or `…/Sfx/`).
4. Open Unity once (or batch import) so `.meta` exists; commit both files.
5. Run `AudioProbe` — beds load; missing SFX stay silence-safe.
6. Leave the Research MP3 in place as provenance, or note the mapping in ATTRIBUTIONS.

---

## Anti-patterns

- Committing `Research/audio/*.mp3` into `Resources/Audio/` “temporarily”
- Renaming a resource id without updating `AudioService` / sfx-inventory call sites
- Empty `Sfx/` folders with no `.meta` placeholders (Unity ignores empty dirs in Git)
- Hand-editing GUIDs in `.meta` to “fix” a conflict — re-import instead

---

## Next (#41)

~~#259 inventory~~ · ~~#260 licence~~ · **#261** (this page) · then **#400** / **#401**
after the ElevenLabs commercial gate in audio-licence.md.
