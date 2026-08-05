# CLAUDE.md — Strategos

Working notes for this repository. **This file is an index and an orientation, not the whole
story** — the detail lives in `docs/`, split so a page can be read when it is relevant instead
of every session.

**Where facts belong.** This file: the project's shape, the pipelines, and the rules that apply
to *any* change. The pages below: everything specific to one area. `docs/phases.md` is the task
breakdown; `docs/command-architecture.md` is the design reasoning behind the topics.

---

## Read before you change anything

Four rules that are cheap to follow and expensive to discover.

1. **Verify in the player, not by inspection.** Build, then `capture.ps1`, then read the
   screenshot. Several bugs this project has shipped were invisible to code review and obvious
   in one frame — a symbol rendering at 6/255 alpha, an objective one cell inside a lake.
2. **Always check `Player.log` after a UI change.** An exception truncates the layout silently:
   a window with a background colour and nothing else, and no error on screen.
   `%USERPROFILE%\AppData\LocalLow\DefaultCompany\strategos\Player.log`
3. **Run the probes for whatever you touched**, and read their *numbers*, not just their
   pass/fail. The most useful output this project produces is a table — a combat matrix, a
   landcover breakdown — and several real bugs were found by a number looking wrong in a run
   that passed.
4. **A generator's output is a picture, so read the picture.** Prefer baking a contact sheet
   over clicking the GUI when checking rendering.

---

## Project

Unity 6 (`6000.0.75f1`) / URP tactical command simulation built on NATO APP-6D symbology.

**Phase 2 (the symbol system) is complete; Phase 1's map system is largely built.** The
map has a data model, a generation pipeline, a 2D topographic renderer and a 3D drape
(a heightfield mesh textured with the 2D sheet). The app boots to a **main menu**
(#371), then enters **PLAY** as a session (pause overlay + drills quick-ref), opens
**OPTIONS** (`SettingsView`, #306 — category shell; #307 persists ConfirmOrders), or opens Tools tabs:
**EXPLORE** (symbol-library browser and pannable map), **SCENARIO** (map
settings with 2D/3D preview), **DRILLS** (TTP binder), **BUILDER** (digit-by-digit
symbol composer).

**The sandbox is playable and units can fight.** A scenario loads two sides onto generated
terrain; units have capabilities and state; a fixed-step simulation carries orders down a
command topic into per-unit queues, moves units by terrain-cost A\*, resolves direct fire
between them, and carries reports back up a situation topic. Select, order, engage, queue,
abort and time compression all work; the live plan is listed order by order and can be cut at
any entry; and the whole run is deterministic and replayable from the command log.

**Hold is a real order**: a unit told to hold digs in over two minutes and takes half the
incoming fire once it has. **The ORBAT is a tree** — a formation is a `UnitInstance` that owns
subordinates, its state rolls up from them, and an order addressed to it decomposes one echelon
per step. **Units tire and units are green**: training costs time before an order is acted on,
fatigue costs capability and recovers with rest. **A destroyed unit becomes a wreck** that is
neither commandable nor a contact, and the loss is recorded. The map pans and zooms, bounded by
the echelon the player commands.

Units also fight on their own initiative under rules of engagement, answer fire while
marching, and withdraw when they are being destroyed. **Training is a unit attribute and
costs time**: a green unit hesitates before acting on an order — the same delay covering
both marching and returning fire, since a reflex preempts onto the head of the queue — and
is slower to report what it sees, so its commander works from an older picture.

There is still no *world* in the 3D sense: the drape is a preview rendered into a UI card,
not a playable space, and there is no game camera. Units still pass through each other —
there is no collision, no zone of control and no facing. Reconstitution after loss is still
open (Phase 4.4). C2 foundations ship — buses, queues, directives, drills, `ISidePolicy` —
but echelon-scale comms, intel fusion and remaining mission types do not. Everything past
that in `ROADMAP.md` — online modes, trained AI, services — is unbuilt. Reflexes are not
intelligence: nothing plans or manoeuvres, which is Phase 8.

| Path | Contents |
|---|---|
| `Assets/Scripts/Core/NatoSymbols/` | The symbol system (all of it) |
| `Assets/Scripts/Core/Maps/Model/` | `MapData` and friends — data only |
| `Assets/Scripts/Core/Maps/Generation/` | The generation pipeline, one file per stage |
| `Assets/Scripts/Core/Maps/Rendering2D/` | CPU topographic renderer |
| `Assets/Scripts/Core/Maps/Rendering3D/` | Drape mesh + mipmapped drape texture |
| `Assets/Scripts/Core/Units/` | Unit instances, capabilities, catalogue, sides, ORBAT tree, fatigue |
| `Assets/Scripts/Core/Scenarios/` | Scenario model, validation, Newtonsoft IO, samples, `ScenarioGenerator` (#334) |
| `Assets/Scripts/Core/Messaging/` | `MessageBus<T>` — the delivery rules, once |
| `Assets/Scripts/Core/Commands/` | Orders down: bus, log, queues, `Simulation`, executors |
| `Assets/Scripts/Core/Reports/` | Reports up: bus, log, `ContactTracker` |
| `Assets/Scripts/Core/Combat/` | `EngagementResolver` — the direct-fire model |
| `Assets/Scripts/Core/Reactions/` | `ReactionController` — ROE and reflexes |
| `Assets/Scripts/Core/Direction/` | `SideDirector` — side-level intent for an unplayed side |
| `Assets/Scripts/Core/World/` | Dynamic world objects — hazards etc. (#34) |
| `Assets/Scripts/Core/Objectives/` | Objectives, victory conditions, the evaluator |
| `Assets/Scripts/Core/Movement/` | Movement grid and A\* |
| `Assets/Scripts/Core/Doctrine/` | TTPs — coded drills, figures, readiness, pack IO |
| `Assets/Scripts/Core/Campaigns/` | `CampaignChain` — linked scenarios sharing one ORBAT, JSON IO |
| `Assets/Scripts/Core/Observation/` | `SideObservation` / encoder — belief-only side knowledge |
| `Assets/Scripts/Core/Actions/` | `SideActionSpace` / mask — drill + ADVANCE vocabulary |
| `Assets/Scripts/Core/Reward/` | `SideReward` — terminal + potential shaping |
| `Assets/Scripts/Core/SimEnv/` | `SideEnv` — Reset/Step environment lifecycle |
| `Assets/Scripts/Core/Trajectories/` | `Trajectory` / exporter — CommandLog+ReportLog demos |
| `Assets/Scripts/Core/ControlMeasures/` | Authored GCMs — checkpoints, phase lines, boundaries |
| `Assets/Scripts/Core/Preferences/` | `PlayerPreferences` + `IPreferenceStore` (#307) |
| `Assets/Scripts/Core/Audio/` | `AudioService` — music beds + one-shots; volume prefs (#40) |
| `Assets/Scripts/Steam/` | `ISteamClient` / `NullSteamClient` — Steamworks seam (#288); no native package until App ID |
| `Assets/Scripts/Persistence/` | `FileGameStore`, `JsonPreferenceStore` (bytes outside Core) |
| `Assets/Scripts/UI/` | Shell, widget kit, shared cards; `Views/` holds the views |
| `Assets/Scripts/Demo/` | `SymbolBuilderPanel` (the BUILDER view) and `SymbolDemoSpawner` |
| `Assets/Resources/Shaders/` | `StrategosMapDrape.shader` |
| `Assets/Resources/Scenarios/` | Shipped scenario JSON |
| `Assets/Resources/Audio/` | Menu loop + PLAY ambient beds (`AudioService`) |
| `Assets/Resources/Doctrine/` | Shipped doctrine packs — drills as JSON |
| `Assets/Editor/` | Build pipeline, symbol editor window, TMP importer, contact sheets, probes |
| `docs/` | Reference docs; `phases.md` is the task breakdown |

---

---

## Architecture

Factory creates the framed base; decorators append layers. `NatoSymbolComposer.Compose`
is the entry point and fixes the order:

```
SIDCParser → SIDCCode
    └─ SymbolFactory.CreateBase      frame + fill
       └─ IconDecorator              entity icon + entity-type variant mark
          └─ SectorModifierDecorator sector 1 (upper) / sector 2 (lower)
             └─ AmplifierDecorator   echelon, HQ staff, TF bracket, feint
                └─ ConditionDecorator    condition bar + combat-power bar
                   └─ TextAmplifierDecorator  fields T, M, F
                      → NatoSymbolBaker.Bake → one Sprite
```

`INatoSymbol` is data only — an ordered `SymbolLayerDraw` list plus
`SymbolTextAmplifiers`. Nothing rasterises until `Bake`. `ConditionDecorator` and
`TextAmplifierDecorator` run last because they read the amplifiers that
`AmplifierDecorator` populates.

The map system mirrors that shape: an ordered pipeline over a data-only container, and
nothing rasterises until the renderer runs. `MapGenerator.Generate` hard-codes the stage
order for the same reason `Compose` does — later stages read what earlier ones wrote:

```
MapGenerationSettings → ReliefParameters
    └─ TectonicStage      base landform, sea level
       └─ (authored relief hook)
          └─ ErosionStage      weathers the surface
             └─ HydrologyStage  fill, D8 routing, lakes, rivers, moisture
                └─ LandcoverStage  classification from slope + moisture
                   └─ SettlementStage  towns, stamped into landcover
                      └─ NetworkStage  roads between them, bridges and fords
                         └─ (authored features hook)
```

`MapData` is data only: an elevation grid, a landcover grid, and vector features.
`MapRasterizer.RenderPixels` is the only thing that draws, and its layer order is fixed
too — ground, contours, areas, lines, point marks, labels, grid. Labels come after every
mark because placement can only avoid collisions it can see, and the grid comes last
because a grid line that gives way to a road has stopped being a coordinate reference.

The two systems share `ProceduralDrawUtil`. Symbols bake into a square buffer, maps into
a rectangular one, so every primitive has a `(w, h)` overload with the square one
forwarding to it. Add new primitives to the rectangular overload.

---

---

## Where the detail is

| Page | Read it before |
|---|---|
| [docs/build-and-verify.md](docs/build-and-verify.md) | Running anything — builds, captures, probes, contact sheets |
| [docs/ci-unity-licence.md](docs/ci-unity-licence.md) | Hosted CI Unity secrets checklist (#216) |
| [docs/itch-publish.md](docs/itch-publish.md) | itch.io project + butler / app / browser push (#221) |
| [docs/field-manual.md](docs/field-manual.md) | Glossary JSON shape + pack status (#124 / #205) |
| [docs/simulation-invariants.md](docs/simulation-invariants.md) | Touching `Core/Commands`, `Reports`, `Combat`, `Reactions`, `Direction`, `Objectives`, `Movement`, `Messaging` |
| [docs/symbol-invariants.md](docs/symbol-invariants.md) | Touching `Core/NatoSymbols` — SIDC layout, echelon marks, frame rules |
| [docs/map-invariants.md](docs/map-invariants.md) | Touching `Core/Maps` — the 2D sheet and the 3D drape |
| [docs/ui-invariants.md](docs/ui-invariants.md) | Touching `Assets/Scripts/UI` or `Demo` — view shell, layout, glyph coverage |
| [docs/audio.md](docs/audio.md) | Touching `Core/Audio` or soundtrack beds — mixer stub, Resources clips, volume prefs |
| [docs/audio-inventory.md](docs/audio-inventory.md) | Sourcing catalogue (#41 / #259) — music, SFX, VO, Morse; owners and paths |
| [docs/audio-licence.md](docs/audio-licence.md) | Tooling + licence / provenance rules (#260) — Suno Pro, ElevenLabs gate, staging audit |
| [docs/audio-resources.md](docs/audio-resources.md) | Resources/Audio layout + `.meta` conventions (#261) — what ships vs Research/audio |
| [docs/steam.md](docs/steam.md) | Touching `Scripts/Steam` or Steam publishing — App ID gate, package choice, Overlay / Achievements / Cloud |
| [docs/sfx-inventory.md](docs/sfx-inventory.md) | Adding or wiring a one-shot SFX (#44) — cue list and resource ids |
| [docs/unity-gotchas.md](docs/unity-gotchas.md) | Adding an asset, a package, or a serialised type |
| [docs/local-api-seam.md](docs/local-api-seam.md) | Touching `IGameStore`, `IContentSource`, `IPlayerIdentity` (#355 vs #66) |
| [docs/campaign-invariants.md](docs/campaign-invariants.md) | Touching `Core/Campaigns` — the chain shape, carry-over, authored-not-generated |
| [docs/climb-campaign.md](docs/climb-campaign.md) | Multi-echelon climb campaign (#403) — seat ladder and scenario Id rules |
| [docs/game-modes.md](docs/game-modes.md) | Touching PLAY mode-select, spectator, hotseat, replay (#287) |
| [docs/ai-difficulty.md](docs/ai-difficulty.md) | Touching `SideDirector` difficulty / personality (#291) |
| [docs/scenario-generation.md](docs/scenario-generation.md) | Touching `ScenarioGenerator` / procedural scenarios (#334) |
| [docs/historical-research.md](docs/historical-research.md) | Gathering Phase 6.2 notes under `Research/historical/` (#332); before #333 conversion |
| [docs/known-gaps.md](docs/known-gaps.md) | Chasing anything that looks like a bug — it may already be recorded |

Reference material, unchanged:

| Page | What it is |
|---|---|
| [docs/command-architecture.md](docs/command-architecture.md) | Why orders and reports are messages. The reasoning behind the topics |
| [docs/nato-symbol-generator.md](docs/nato-symbol-generator.md) | APP-6D reference detail — SIDC tables, frame shapes, layer model |
| [docs/phases.md](docs/phases.md) | The phase breakdown |
| [ROADMAP.md](ROADMAP.md) | Echelon as the difficulty curve, backlog, and the long arc |
| [CHANGELOG.md](CHANGELOG.md) | What landed recently |

**If you change behaviour covered by one of these pages, update it in the same change.** A note
that is wrong is worse than no note, because it is trusted.
