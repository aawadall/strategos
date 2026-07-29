# Strategos — Development Roadmap

> **Engine:** Unity 6 (6000.x LTS) | **Status:** Pre-production

This roadmap describes the high-level development path for Strategos. Detailed implementation tasks for each phase are maintained in [docs/phases.md](docs/phases.md).

---

## Vision

Strategos is a tactical command simulation game built around topographic maps, NATO APP-6D symbology, multi-echelon command, and adaptive AI. The player can grow from commanding a fireteam to controlling a theater-level combatant command.

The game supports:

- Solo play against AI
- Hotseat play
- Online multiplayer
- AI vs AI spectator mode
- Replay of saved or historical battles

---

## Echelon Progression

| Symbol | Echelon | Typical Strength |
|---|---|---|
| ○ (none) | Fireteam / Crew | 2–4 |
| • | Squad / Section | 9–13 |
| •• | Platoon / Detachment | 30–50 |
| ••• | Company / Battery / Troop | 80–250 |
| I | Battalion / Squadron | 300–1,000 |
| II | Regiment / Group | 1,000–3,000 |
| X | Brigade / Combat Team | 3,000–5,000 |
| XX | Division | 10,000–20,000 |
| XXX | Corps | 20,000–80,000 |
| XXXX | Army | 100,000+ |
| XXXXX | Army Group / Front | multiple Armies |
| XXXXXX | Theater / Combatant Command | multiple Army Groups |

---

## Phase Overview

### Phase 0 — Foundation & Project Setup
Establish a buildable Unity 6 project, repository structure, CI, base scenes, and test framework.

### Phase 1 — Topographic Map System
Implement zoomable heightmap terrain, contour overlays, terrain classification, LOS, fog of war, weather, and grid systems.

### Phase 2 — NATO APP-6D Symbol Library
Build the composable military symbol system, including APP-6D frames, affiliation, unit types, echelon indicators, modifiers, and map rendering.

### Phase 3 — Unit & Echelon System
Create the hierarchical unit model, ORBAT system, commander assignment, equipment data, doctrine profiles, and runtime unit state.

### Phase 4 — Movement & Combat Engine
Implement terrain-aware movement, pathfinding, combat resolution, suppression, indirect fire, logistics, attrition, and recovery.

### Phase 5 — Command & Control (C2) System
Model orders, mission types, command delays, communication degradation, intelligence, reconnaissance, deception, and doctrine templates.

### Phase 6 — Scenario & Campaign System
Build the scenario editor, historical scenario support, objective system, victory conditions, triggers, and linked campaigns.

### Phase 7 — Game Modes
Deliver solo vs AI, hotseat, online multiplayer, AI vs AI watch mode, and replay playback.

### Phase 8 — AI System
Develop rule-based AI, reinforcement learning, transfer learning from historical battles/replays, genetic strategy evolution, AI personalities, and model sharing.

### Phase 9 — Online Services & Community
Add accounts, matchmaking, leaderboards, scenario workshop, replay library, AI model hub, notifications, and mod support.

### Phase 10 — Polish, Accessibility & Release
Complete audio, UI/UX, tutorials, accessibility, performance optimization, platform builds, legal review, and 1.0 launch assets.

See [docs/phases.md](docs/phases.md) for the full task breakdown and milestones.

---

## Versioning Strategy

| Release | Focus |
|---|---|
| 0.1 Alpha | Phases 0–3: Map + Symbols + Units |
| 0.3 Alpha | Phases 4–5: Movement + Combat + C2 |
| 0.5 Beta | Phases 6–7: Scenarios + All Game Modes |
| 0.8 Beta | Phase 8: Full AI pipeline |
| 0.9 RC | Phase 9: Online services |
| 1.0 | Phase 10: Polish + Release |

---

## Post-1.0 Direction

Post-release development may expand into naval, air, space, and cyber dimensions; coalition multiplayer; large-scale AI tournaments; VR spectator tools; and a public modding SDK.

---

*Last updated: 2026-07-29 | Co-Authored-By: Oz <oz-agent@warp.dev>*
