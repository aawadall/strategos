# Audio

Runtime audio for Strategos. **Read before adding a clip or changing volume behaviour.**

[CLAUDE.md](../CLAUDE.md) is the index. Sourcing / licences: [assets.md](assets.md),
[ATTRIBUTIONS.md](../ATTRIBUTIONS.md). Research drafts: `Research/audio/`.

---

## Shape

`Strategos.Audio.AudioService` lives on `AppShell`. It owns two `AudioSource`s (music loop,
SFX one-shots). There is no Unity `AudioMixer` asset yet — the **master bus stub** is
`AudioListener.volume`, set from `PlayerPreferences.MasterVolume`. Music and SFX gains
are `MusicVolume` / `SfxVolume` on the sources.

| Slot | Resource | When |
|---|---|---|
| Menu loop (#253) | `Resources/Audio/menu-loop` | Menu, settings, tools tabs |
| PLAY ambient (#254) | `Resources/Audio/play-ambient` | `Navigate("play")` |

`AppShell.Navigate` switches the bed. Missing clips log a warning and stay silent — never
throw. Batchmode (`-nographics`) skips `Play` / `PlayOneShot` so probes stay green.

---

## Preferences (#264)

Settings → AUDIO: MASTER / MUSIC / SFX (0–100%). Persisted in `PlayerPreferences` via
`JsonPreferenceStore`. Applied on boot and when a slider changes.

---

## Probe

`Strategos > Probe Audio` / `-executeMethod Strategos.Editor.AudioProbe.Run` —
silence-safe missing clip, procedural one-shot, shipped beds load, volume →
`AudioListener.volume`.

---

## Not here yet

Terrain ambients, combat SFX (#44), VO (#42), intensity crossfade, a real mixer with
duck groups. Staging MP3s under `Research/audio/` are not auto-imported.
