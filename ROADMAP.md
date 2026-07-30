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
| ○ | Fireteam / Crew | 2–4 |
| • | Squad | 9–13 |
| •• | Section | 8–13 |
| ••• | Platoon / Detachment | 30–50 |
| I | Company / Battery / Troop | 80–250 |
| II | Battalion / Squadron | 300–1,000 |
| III | Regiment / Group | 1,000–3,000 |
| X | Brigade / Combat Team | 3,000–5,000 |
| XX | Division | 10,000–20,000 |
| XXX | Corps | 20,000–80,000 |
| XXXX | Army | 100,000+ |
| XXXXX | Army Group / Front | multiple Armies |
| XXXXXX | Theater / Combatant Command | multiple Army Groups |

> Squad and section are near-synonyms and vary by army, but APP-6D and the code treat
> them as distinct echelons, so they are listed separately here. The marks above match
> `AmplifierDecorator.DrawEchelon` — company is **one** bar, battalion two, regiment three.
> An earlier version of this table had them shifted by one; see the echelon note in
> [CLAUDE.md](CLAUDE.md) before "correcting" it back.

### Echelon is the difficulty curve

The echelon the player commands is not just a matter of scale. **It determines how much of
the command problem is present at all**, and that is the intended progression:

| Commanding | What command feels like |
|---|---|
| Fireteam / squad | No C2 problem — you *are* the unit. You see what it sees; orders are instant. |
| Platoon / company | Subordinates exist but are close. Orders are near-immediate. |
| Battalion | Subordinates out of sight. Reports mediate what you know. Delay begins to bite. |
| Brigade / division | You command through subordinate headquarters. You cannot see the front. |
| Corps / army | You work from reports already stale. You issue intent, not instructions. |
| Theater | Coalition. Some formations you cannot order at all, only ask. |

This is why the later phases exist. Communications degradation, order propagation delay,
intelligence fusion, and the divergence between what a commander believes and what is true
are not features bolted onto a finished game — **they are what makes each echelon feel
different rather than merely bigger.** Introduced all at once at squad level they would be
noise; unlocked as the player climbs, they are the learning curve.

The design consequence: these effects should be **parameterised by echelon, not toggled**.
Order delay is a function of echelon, distance and terrain that returns approximately zero
at squad level and grows from there — one code path from tutorial to theatre, with the
degenerate case serving as the tutorial. See
[docs/command-architecture.md](docs/command-architecture.md).

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
See [docs/command-architecture.md](docs/command-architecture.md) for the command/situation topic design that Phases 3–5 build on.
See [docs/steam.md](docs/steam.md) for the Steam publishing guide, Early Access strategy, and Steamworks integration details.

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
