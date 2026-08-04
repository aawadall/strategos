# Strategos

> *From Greek: στρατηγός — a general, a commander of armies.*

**Strategos** is an open-source tactical command simulation game built with **Unity 6**, spanning every echelon of military command — from a four-soldier fireteam all the way to a multi-theater combatant command.

---

## Features

- **NATO APP-6D Symbology** — Full implementation of NATO standardised military symbols for land, sea, air, space, and cyber dimensions
- **Topographic Maps** — Height-map-driven terrain with contour overlays, terrain types, LOS calculations, and weather effects
- **Full Echelon Progression** — Command units from Fireteam (○) through Squad, Platoon, Company, Battalion, Brigade, Division, Corps, Army, Army Group, all the way to Theater/Combatant Command (XXXXXX)
- **Multiple Game Modes** — Single player vs AI, Hotseat, Online Multiplayer, AI vs AI (spectator), and Battle Replay
- **Adaptive AI** — Agents that learn through Reinforcement Learning, improve via Transfer Learning from historical battles, and evolve strategies with Genetic Algorithms
- **Scenario Editor** — Build custom operations with full ORBAT (Order of Battle) design
- **Cross-Platform** — Windows, macOS, Linux (WebGL stretch goal)

---

## Technology Stack

| Layer | Technology |
|---|---|
| Engine | Unity 6 (6000.x LTS) |
| Rendering | Universal Render Pipeline (URP) |
| Networking | Unity Netcode for GameObjects / Mirror |
| AI / ML | Unity ML-Agents + custom RL pipeline |
| Map Data | Heightmap terrain + procedural topographic overlay |
| Symbols | NATO APP-6D SVG sprite library |
| Data | ScriptableObjects + JSON scenario files |
| Version Control | Git / GitHub |

---

## Game Modes

| Mode | Description |
|---|---|
| Solo vs AI | Player commands one side; AI commands the other |
| Hotseat | Two players share the same machine, alternating turns |
| Online | Networked multiplayer across any echelon size |
| AI vs AI | Watch two AI commanders battle; no player input required |
| Replay | Load and play back any previously recorded battle |

---

## Echelon Scale

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

## Getting Started

> ⚠️ **Project is under active development.** See [ROADMAP.md](ROADMAP.md) for current status.

### Prerequisites

- Unity 6 (6000.x LTS) with Universal Render Pipeline
- .NET 8 SDK (for tooling)
- Git

### Setup

```bash
git clone https://github.com/aawadall/strategos.git
cd strategos
# Open the Unity project from the root folder in Unity Hub
```

---

## Contributing

Contributions are welcome — whether it's new NATO symbols, historical scenarios, AI improvements, or bug fixes. Please read [CONTRIBUTING.md](CONTRIBUTING.md) and open a PR against `master`.

---

## Roadmap

See [ROADMAP.md](ROADMAP.md) for the full phased development plan.

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

---

*Co-Authored-By: Oz <oz-agent@warp.dev>*
