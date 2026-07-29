# Strategos — Phase Definitions

Detailed task breakdown for each development phase. See [ROADMAP.md](../ROADMAP.md) for the high-level overview and versioning strategy.

---

## Phase 0 — Foundation & Project Setup
**Goal:** Establish a buildable Unity 6 project with CI and a walking-skeleton scene.

- [ ] Unity 6 (6000.x LTS) project initialisation with Universal Render Pipeline (URP)
- [ ] Git repository structure, `.gitignore` (Unity), and branch strategy (`main` protected)
- [ ] GitHub Actions: automated build check on every PR
- [ ] Folder architecture inside `Assets/`:
  - `Scripts/Core`, `Scripts/Units`, `Scripts/AI`, `Scripts/UI`, `Scripts/Networking`, `Scripts/Terrain`
  - `Data/Units`, `Data/Scenarios`, `Data/Doctrines`
  - `Art/NatoSymbols`, `Art/Terrain`, `Art/UI`
  - `Scenes/Main`, `Scenes/Editor`, `Scenes/Tests`
- [ ] Basic main-menu scene with placeholder UI
- [ ] In-game developer console (log, commands, unit spawning)
- [ ] Unit test framework (Unity Test Framework) wired up

**Milestone M0:** Clean build, blank terrain scene loads, tests pass in CI.

---

## Phase 1 — Topographic Map System
**Goal:** Render realistic topographic terrain with interactable overlay.

- [ ] Heightmap ingestion pipeline (SRTM/GeoTIFF → Unity Terrain)
- [ ] Procedural contour-line overlay generation from heightmap data
- [ ] Terrain classification layer (open ground, forest, urban, mountain, water, swamp, desert)
- [ ] Map rendering modes: satellite, topographic, hybrid, schematic (op-map style)
- [ ] Smooth zoom from fireteam scale (~100 m) to theater scale (~5,000 km)
- [ ] Grid system: configurable MGRS, UTM, or lat/long grid overlay
- [ ] Line-of-sight (LOS) and line-of-communication (LOC) raycasting
- [ ] Fog of war: per-unit observation radius, terrain masking, time decay
- [ ] Weather system: clear, rain, snow, sandstorm — affects LOS and movement
- [ ] Day/night cycle with illumination effects on visibility

**Milestone M1:** Zoomable topographic map, LOS working, fog of war visible.

---

## Phase 2 — NATO APP-6D Symbol Library
**Goal:** Implement a complete, composable NATO APP-6D military symbol system.

### 2.1 Symbol Composition Engine
- [ ] APP-6D frame shapes by dimension (land, sea, air, space, cyber, SOF)
- [ ] Affiliation colours: Friend (blue), Hostile (red), Neutral (green), Unknown (yellow)
- [ ] Symbol icons for all major unit types: infantry, armour, artillery, aviation, engineer, signals, logistics, medical, HQ, etc.
- [ ] Echelon designator renderer (all 12 echelon marks)
- [ ] Modifier overlays: task-force indicator, reinforced/reduced, HQ, dummy, feint
- [ ] Dynamic text fields: unit designation (top), higher formation (bottom-left), strength (bottom-right), DTG/activity modifiers

### 2.2 Symbol Rendering
- [ ] SVG sprite atlas for all APP-6D icons, procedurally composed at runtime
- [ ] LOD: detailed symbol at close zoom, simplified icon at distance, dot at theater scale
- [ ] Symbol anchoring to terrain (map position → world position)
- [ ] Selection highlight, hover tooltip with unit summary
- [ ] Animated states: moving (arrow), engaged (flash), disrupted, destroyed

### 2.3 Symbol Editor (in-game)
- [ ] Drag-and-drop symbol placement in scenario editor
- [ ] Right-click context menu for symbol properties

**Milestone M2:** Any APP-6D symbol can be created, placed on terrain, and rendered at any zoom.

---

## Phase 3 — Unit & Echelon System
**Goal:** Full data model for every echelon with parent-child hierarchy.

### 3.1 Unit Data Model
- [ ] `UnitDefinition` ScriptableObject: echelon, type, nationality, equipment, manpower, firepower rating, mobility rating, sustainment
- [ ] `UnitInstance`: runtime state — position, strength, readiness, supply level, morale, commander
- [ ] Subordinate unit tree: each unit owns a list of direct subordinates (unlimited depth)
- [ ] Equipment loadout system: weapons, vehicles, support assets per echelon type
- [ ] National doctrine profiles (US, NATO, Russia, China, generic — affects default TTPs)

### 3.2 ORBAT (Order of Battle) System
- [ ] Hierarchical ORBAT tree view in UI
- [ ] ORBAT serialisation to JSON for save/load and scenario files
- [ ] Attach/detach (OPCON/TACON) units between formations
- [ ] Headquarters (HQ) units with C2 radius and morale bonus

### 3.3 Commander Assignment
- [ ] `Commander` entity: name, rank, skill stats (leadership, tactics, logistics, intel)
- [ ] Commander bonuses applied to subordinate units within command radius
- [ ] Commander career progression (earned by player or AI through scenario performance)
- [ ] Historical commander database (optional DLC/data pack)

**Milestone M3:** Multi-echelon ORBAT displayed in scene; units selectable, stats shown in inspector panel.

---

## Phase 4 — Movement & Combat Engine
**Goal:** Physically grounded movement and deterministic combat resolution.

### 4.1 Movement
- [ ] Terrain movement cost table per unit type (wheeled, tracked, foot, air, naval)
- [ ] Pathfinding: A* on terrain grid with elevation penalties
- [ ] Road/trail network bonus movement
- [ ] Operational movement vs. tactical movement speed distinction
- [ ] Formation movement: line, column, wedge, echelon, bounding overwatch
- [ ] Forced march (speed boost with readiness penalty)
- [ ] River crossing: bridge vs. ford vs. wet gap crossing

### 4.2 Combat
- [ ] Engagement range by weapon system (direct fire, indirect fire, air defence)
- [ ] Combat resolution: modified dice model with firepower, protection, terrain, morale, supply
- [ ] Suppression mechanic: units under fire become suppressed, reducing effectiveness
- [ ] Indirect fire: artillery, mortars, MLRS — target area, scatter, time-on-target
- [ ] Air support: CAS, interdiction, ISR (scheduled or on-call)
- [ ] Naval gunfire support (coastal scenarios)
- [ ] Counter-battery radar and anti-artillery fire
- [ ] NBC (nuclear/biological/chemical) contamination zones (optional scenario element)

### 4.3 Logistics & Sustainment
- [ ] Supply class system (ammunition, fuel, rations, water, spare parts)
- [ ] Supply line tracing: unit → higher HQ → supply depot → supply point
- [ ] Consumption rates per unit type and activity level
- [ ] Out-of-supply penalties: readiness degradation, movement restriction
- [ ] Engineer: bridging, minefield emplacement/breaching, fortification construction

### 4.4 Attrition & Recovery
- [ ] Casualty model: killed, wounded, missing
- [ ] Equipment loss tracking
- [ ] Medical evacuation chain
- [ ] Reconstitution (refit and replacement) at rear echelon

**Milestone M4:** Two opposing forces can manoeuvre, engage, and resolve combat; logistic failure degrades units.

---

## Phase 5 — Command & Control (C2) System
**Goal:** Model realistic military C2 with orders, communications, and doctrine.

### 5.1 Orders System
- [ ] Mission type orders: Attack, Defend, Delay, Withdraw, Screen, Cover, Guard, Reconnaissance, Exploit, Pursue
- [ ] Graphic control measures: axes of advance, battle positions, phase lines, checkpoints, boundaries, engagement areas, kill zones
- [ ] FRAGO (Fragmentary Order) system: modify an existing plan without full reorder
- [ ] Order propagation delay (time for order to reach subordinate)
- [ ] Commander's intent field: free-text or template-based

### 5.2 Communications
- [ ] Communication range by echelon (line-of-sight radio, satellite, landline)
- [ ] Comms degradation in mountains/urban terrain (multipath)
- [ ] EW (Electronic Warfare): jamming, SIGINT detection
- [ ] PACE plan (Primary, Alternate, Contingency, Emergency comms)

### 5.3 Intelligence (INT)
- [ ] Intelligence cycle: collection, processing, analysis, dissemination
- [ ] Reconnaissance units: scouts, UAVs, satellite passes
- [ ] HUMINT, SIGINT, IMINT intelligence types
- [ ] OPFOR position confidence levels (confirmed, suspected, possible)
- [ ] Intel fusion board: correlate multiple sources
- [ ] Deception operations: feints, decoys, radio silence

### 5.4 Doctrine Templates
- [ ] Pre-built TTP libraries per nation/doctrine
- [ ] AI and human players can load and apply doctrine templates
- [ ] Player can create and save custom doctrine templates

**Milestone M5:** Player can issue orders, units execute with lag, intel system reveals/conceals OPFOR.

---

## Phase 6 — Scenario & Campaign System
**Goal:** Replayable standalone scenarios and linked campaign chains.

### 6.1 Scenario Editor
- [ ] Map selection and terrain editing
- [ ] ORBAT builder (both sides)
- [ ] Objectives placement: area, point, task (seize, secure, clear, destroy, etc.)
- [ ] Victory conditions editor: timed, kill-ratio, objective control, phase line
- [ ] Weather and time-of-day presets
- [ ] Trigger/event scripting (simple): reinforce at time X, event fires when Y captured
- [ ] Scenario metadata: name, description, date/era, classification level, author
- [ ] Export to JSON; share as file or upload to community hub

### 6.2 Historical Scenarios
- [ ] Initial pack: 5–10 historical engagements at varied echelon scales
- [ ] Data format allows community-built historical packs
- [ ] Historical AI behaviour seeding (see Phase 8)

### 6.3 Campaign Mode
- [ ] Linked scenario chain with persistent ORBAT and casualty carry-over
- [ ] Strategic map layer: campaign moves between operations
- [ ] Operational-level resupply and reinforcement between scenarios
- [ ] Dynamic campaign generator (procedural)

**Milestone M6:** Editor ships, at least five built-in scenarios playable, campaign chain of three linked operations works.

---

## Phase 7 — Game Modes
**Goal:** All five game modes functional and stable.

### 7.1 Single Player vs AI
- [ ] Player selects side, echelon scale, and scenario
- [ ] AI controls opposing force
- [ ] Difficulty: Recruit → Regular → Veteran → Elite → Legendary
- [ ] Post-battle AAR (After-Action Review): map replay with analytics

### 7.2 Hotseat Multiplayer
- [ ] Simultaneous orders input, then resolution phase (WEGO)
- [ ] Hidden information: each player sees only their own intel
- [ ] Screen-swap prompts between player turns
- [ ] Local AI fill-in if player disconnects

### 7.3 Online Multiplayer
- [ ] Unity Netcode for GameObjects (primary) / Mirror (fallback)
- [ ] Lobby system: create/join game, password, invite via link
- [ ] Turn timer with async option (email-style, submit orders when ready)
- [ ] Spectator slot
- [ ] Reconnect and resume after disconnect
- [ ] Ranked and unranked match types

### 7.4 AI vs AI (Watch Mode)
- [ ] Player sets up two AI commanders (faction, difficulty, doctrine, model)
- [ ] Speed control: 1×, 4×, 16×, instant
- [ ] Commentary overlay (optional, generated from game events)
- [ ] Export battle as replay file

### 7.5 Replay System
- [ ] All game actions recorded to compressed replay file (`.stgreplay`)
- [ ] Replay playback with scrub, pause, rewind, speed control
- [ ] Replay sharing: upload to server, download community replays
- [ ] AI training ingestion: replay files can be fed into the RL pipeline

**Milestone M7:** All five modes launchable and playable end-to-end without crashes.

---

## Phase 8 — AI System
**Goal:** An AI that can be trained, evolved, transferred, and shared.

### 8.1 Rule-Based AI (baseline)
- [ ] Doctrine-driven behaviour trees per mission type
- [ ] Used for lowest difficulty levels and as RL environment baseline
- [ ] Exposes same action API as ML agent for compatibility

### 8.2 Reinforcement Learning (RL)
- [ ] **Environment API**: state space (unit positions, supply, Intel, terrain), action space (move, attack, defend, reinforce, retreat, order artillery), reward function (objectives secured, enemy destroyed, friendly preserved, time to victory)
- [ ] Unity ML-Agents integration (C# environment, Python trainer)
- [ ] Self-play training loop: two instances of same model play against each other
- [ ] Curriculum learning: start with small scenarios (company level), progress to corps
- [ ] Training dashboard: reward curves, win rate, episode length
- [ ] Model checkpointing every N episodes
- [ ] Serialise trained model to `.onnx` for runtime inference

### 8.3 Transfer Learning
- [ ] Historical battle imitation learning: convert replay files to expert demonstrations
- [ ] Behavioural cloning pre-training from historical data before RL fine-tuning
- [ ] Doctrine transfer: model trained on one national doctrine bootstraps another
- [ ] Cloud model repository: download pre-trained `.onnx` models by echelon/doctrine

### 8.4 Genetic Algorithm (GA) Strategy Evolution
- [ ] Strategy chromosome: encodes tactical preferences (aggression, axis choice, reserve usage, deception propensity)
- [ ] Population of N strategy chromosomes per faction
- [ ] Fitness function: performance across M randomised scenarios
- [ ] Selection, crossover, mutation operators
- [ ] GA runs offline or as background process; best chromosome applied at runtime
- [ ] GA + RL hybrid: GA evolves high-level strategy; RL handles tactical execution

### 8.5 AI Difficulty & Personality
- [ ] AI profiles: Aggressive, Defensive, Balanced, Feint-heavy, Attrition, Manoeuvre
- [ ] Personality parameters tune RL policy at inference time
- [ ] Named commanders (personalities) with persistent stats across sessions

### 8.6 AI Model Hub
- [ ] REST API server: upload, download, version, rate AI models
- [ ] Client-side: browse community models by echelon, doctrine, win rate
- [ ] Automated tournament: server runs AI-vs-AI matches, ranks models on ELO ladder
- [ ] Provenance: model lineage tree (which models trained which)

**Milestone M8:** RL agent beats rule-based AI at battalion level. Transfer-learning from at least one historical dataset improves training speed by ≥ 20%. GA evolves a strategy that outperforms fixed doctrine on 3 out of 5 test scenarios.

---

## Phase 9 — Online Services & Community
**Goal:** Backend services supporting persistent accounts, matchmaking, and content sharing.

- [ ] User accounts: registration, login, OAuth (Google/Discord)
- [ ] Player profile: rank, win/loss, favourite doctrine, commander history
- [ ] ELO-based matchmaking for online games
- [ ] Global and faction leaderboards
- [ ] Scenario workshop: upload, tag, rate, subscribe to community scenarios
- [ ] Replay library: upload/download `.stgreplay` files
- [ ] AI model hub (see Phase 8.6)
- [ ] Notification system: game invites, turn alerts, tournament results
- [ ] Mod support API: custom units, terrain packs, doctrine packs

**Milestone M9:** Backend deployed; account creation, matchmaking, and scenario upload/download working in production.

---

## Phase 10 — Polish, Accessibility & Release
**Goal:** Shippable 1.0 build.

### Audio
- [ ] Ambient terrain soundscapes (forest, urban, desert, arctic)
- [ ] Unit movement and combat SFX
- [ ] Radio chatter / commander voice lines (localisation-ready)
- [ ] Dynamic music system: tension ramps with combat intensity

### UI / UX
- [ ] Full UI skin — military map aesthetic
- [ ] Colour-blind accessible symbol and map palettes
- [ ] Keyboard shortcut remapping
- [ ] Controller support (console stretch goal)
- [ ] Comprehensive settings: graphics, audio, gameplay, accessibility

### Tutorial & Onboarding
- [ ] Interactive tutorial campaign: start at squad, walk through all systems
- [ ] Context-sensitive in-game help overlay
- [ ] Doctrinal reference wiki (in-game and web)

### Performance & Stability
- [ ] Unit stress tests: 10,000 simultaneous units at theater scale
- [ ] Memory profiling and GC pressure reduction
- [ ] Addressables for async asset loading (no load-screen hitches)
- [ ] Unity DOTS/ECS migration path for massive-scale scenarios (post-1.0 spike)

### Platform Builds
- [ ] Windows (primary)
- [ ] macOS
- [ ] Linux (Steam Deck compatible)
- [ ] WebGL (stretch — limited echelon scale)

### Release Checklist
- [ ] Legal review: NATO symbol usage, any trademarked unit names
- [ ] ESRB/PEGI rating (expected: T/12+)
- [ ] Steam page, trailer, press kit
- [ ] 1.0 launch blog post with AI architecture writeup

**Milestone M10: 1.0 Release.**

---

## Post-1.0 Stretch Goals

- **Naval dimension** — sea-control operations, amphibious landings (APP-6D sea symbols)
- **Air dimension** — air superiority, strategic bombing, airborne operations
- **Space layer** — satellite assets (ISR, GPS denial, anti-satellite)
- **Cyber domain** — network attack/defence as a game mechanic
- **Multiplayer coalitions** — joint commands shared between multiple human players
- **MTTR scoring** — measure AI commander Mean Time to Recovery after setbacks
- **VR spectator mode** — stand inside the operations room
- **Modding SDK** — full SDK + documentation for community content

---

*Last updated: 2026-07-29 | Co-Authored-By: Oz <oz-agent@warp.dev>*
