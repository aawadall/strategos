# Strategos

> *From Greek: στρατηγός — a general, a commander of armies.*

**Strategos** is a Unity 6 tactical command simulation built on topographic maps and
NATO APP-6D symbology. The long arc is command from fireteam to theatre; today it is a
**playable free alpha** (solo / hotseat / spectator / replay, campaigns, combat).

- **Play:** [v0.3.0-alpha.1 Windows zip](https://github.com/aawadall/strategos/releases/tag/v0.3.0-alpha.1)
  · itch: follow [docs/itch-publish.md](docs/itch-publish.md) then add the live URL here
- **Site:** [aawadall.github.io/strategos](https://aawadall.github.io/strategos/)
- **Status:** [ROADMAP.md](ROADMAP.md) · [CHANGELOG.md](CHANGELOG.md)

---

## How to play

1. Unzip the Windows release and run `Strategos.exe` (or open the Unity project and Play).
2. From the menu, start **SQUAD TUTORIAL** or **SKIRMISH ONLY**.
3. **Left-click** a friendly (blue) unit to select it.
4. Arm **MOVE** (button or `M`), then left-click empty ground — or **right-click** ground to march.
5. Arm **ENGAGE** (button or `E`), then left-click an enemy — or right-click a contact to fire.
6. **Space** pauses / resumes. **Esc** opens pause (Save / Load / return to menu).

In the player, **HELP** on the main menu repeats this and lists alpha limits.

---

## Alpha limits (not bugs)

Honest gaps strangers hit first — full engineering list in [docs/known-gaps.md](docs/known-gaps.md):

- **Little fog of war** on the small shipped maps (detection ranges are long).
- **Artillery fights as direct fire**; true indirect fire is not built yet.
- **No zone of control, facing, or unit collision** — units pass through each other.

---

## What's playable today

- NATO APP-6D symbol composer + topographic map generation / 2D sheet / 3D drape preview
- PLAY: orders, A\*, direct fire, ROE reflexes, victory, save/load, campaign chains
- Modes: solo, hotseat, spectator, replay · Climb / Valley / Highland campaigns
- Front door: splash, fit-height menu, Options / Audio / Exit, version in the top bar

Not yet: planning AI, online multiplayer, scenario editor, Steam store build, WebGL demo.

---

## Echelon scale

```
○        Fireteam / Crew          (2–4 personnel)
•        Squad / Section          (~9–13 personnel)
••       Platoon / Detachment     (~30–50 personnel)
•••      Company / Battery        (~80–250 personnel)
I        Battalion / Squadron     (~300–1,000 personnel)
II       Regiment / Group         (~1,000–3,000 personnel)
X        Brigade                  (~3,000–5,000 personnel)
XX       Division                 (~10,000–20,000 personnel)
XXX      Corps                    (~20,000–80,000 personnel)
XXXX     Army                     (~100,000+ personnel)
XXXXX    Army Group               (multiple Armies)
XXXXXX   Theater / Combatant Cmd  (multiple Army Groups)
```

---

## Building from source

### Prerequisites

- Unity 6 (`6000.0.75f1`) with URP
- Git (LFS for some ProjectSettings)

### Setup

```bash
git clone https://github.com/aawadall/strategos.git
cd strategos
# Open the project root in Unity Hub, or:
#   .\scripts\build.ps1 -Target Windows64 -Version 0.3.0-alpha.1
```

See [docs/build-and-verify.md](docs/build-and-verify.md) for capture, probes, and contact sheets.

---

## Contributing

Read [CONTRIBUTING.md](CONTRIBUTING.md) and open a PR against `master`. Small known-gap
fixes and size:5m issues are the best first contributions.

---

## License

MIT terms with a **non-military use restriction** — see [LICENSE](LICENSE) for the
full text.

Strategos is a game. It is licensed for personal, educational, research and
non-military entertainment use. It may not be deployed, integrated or executed by
any military organisation, armed force, defence contractor, intelligence agency or
state security service for operational training, war-gaming or combat simulation.

Because that is a restriction on the field of use, this is **not** an OSI-approved
open-source licence, and GitHub will report the repository as "Other" rather than
MIT. The source remains public and freely readable, modifiable and redistributable
within the terms above.
