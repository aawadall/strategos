# Build & verify

How to build, run and prove a change in Strategos.
**Read before running anything**; the traps here have each cost real time.
[CLAUDE.md](../CLAUDE.md) is the index.

---

## Build & verify

```powershell
.\scripts\build.ps1 -Target Windows64      # Windows64 | Linux64 | macOS | WebGL | All
.\scripts\capture.ps1 -View scenario       # launch player on one view, screenshot, close
```

**`build.ps1` waits for the editor; do not "simplify" it back to the call operator.**
`Unity.exe` is a GUI-subsystem binary and PowerShell does not wait for those, so
`& $UnityExe …` returns in about 0.1 s with the build still running — measured 0.1 s
versus 19 s after the fix. Anything sequenced after it then races the build, and
`capture.ps1` screenshots the *previous* player, so a UI change looks like it did
nothing. `-Wait` is also wrong: it waits on the whole process tree and Unity leaves
helper children alive, which hung a 19 s build for ten minutes. Use `-PassThru` plus
`WaitForExit()` on the returned object.

**`capture.ps1` verifies it actually captured the player.** `SetForegroundWindow` fails
silently when the caller is not itself the foreground process, and `CopyFromScreen` then
saves whatever window occupies those coordinates — it once saved an unrelated
application, which is indistinguishable from a catastrophically broken layout. It now
retries focus and errors out rather than saving a lie. If it reports focus failure,
something is stealing focus; it is not a UI bug. **Kill stray `Strategos` processes
before capturing** — a lingering window at the same coordinates will be photographed
instead.

`-View <key>` selects a view without driving the UI: `explore`, `symbols`, `map`,
`scenario`, `ttp`, `builder`. Add `-view3d` (passed straight to the player) to open the
scenario preview in 3D. `AppShell` logs `[AppShell] n view(s), showing 'key'` on start,
which is the cheap check that the shell came up at all.

**A batch build can silently ship the previous revision.** `BuildPlayer` will package
whatever is already in `Library/ScriptAssemblies`, so a build started right after an edit
can succeed, report success, and run your last change but one — twice in a row, which is
long enough to send you hunting for a bug in code that is not in the player. `GameBuild.Run`
now calls `AssetDatabase.Refresh()` first and refuses to build while
`EditorApplication.isCompiling`. If you are still unsure which revision you are looking
at, put something visible in the frame and confirm it: the map card's marginalia strip
carries seed and extent, which is exactly what it is for.

**Check the build's error count, not the absence of error messages.** Grepping for `error CS`
and finding nothing is an absence — it cannot distinguish "the build was clean" from "the
check was broken, pointed at the wrong file, or never ran." The build log ends with a
summary line asserting the result positively: `Errors: 0 | Warnings: 0`. Assert on that
line instead. A positive assertion can fail; an absence cannot. The check must fail in two
ways: if the summary line does not exist (the build crashed or was truncated before finishing),
and if the error count is nonzero. Use `Select-String` to find the line, since it is the
gateway check before any other verification runs:

```powershell
$m = Select-String -Pattern 'Errors: (\d+)' -Path .\Artifacts\build-log-Windows.txt
if (-not $m) { throw 'No build summary line - the build did not finish.' }
if ([int]$m[-1].Matches[0].Groups[1].Value -ne 0) { throw "Build failed: $($m[-1].Line)" }
```

This check fails the script (and thus any test or deploy automation) if either the summary
line is missing or the error count is not zero. A script that checks an absence will always
succeed, even when the build did not run.

Bake a grid of symbol permutations — **prefer this over clicking the GUI** when checking
rendering changes:

```powershell
# Menu: Strategos > Bake Symbol Contact Sheet
& "C:\Program Files\Unity\Hub\Editor\6000.0.75f1\Editor\Unity.exe" `
    -batchmode -quit -nographics -projectPath . `
    -executeMethod Strategos.Editor.SymbolContactSheet.Bake -logFile sheet.log
# -> Artifacts/symbol-contact-sheet.png
```

Same idea for maps, and the same advice — a generator's output is a picture, so read the
picture. `MapContactSheet` bakes every relief profile against every render mode at 1 px
per cell, plus one map at 3 px per cell where labels and cased roads are checkable:

```powershell
# Menu: Strategos > Bake Map Contact Sheet
& "C:\Program Files\Unity\Hub\Editor\6000.0.75f1\Editor\Unity.exe" `
    -batchmode -quit -nographics -projectPath . `
    -executeMethod Strategos.Editor.MapContactSheet.Bake -logFile map-sheet.log
# -> Artifacts/map-contact-sheet.png, Artifacts/map-detail.png
```

It logs per-profile elevation range, feature counts and a landcover breakdown. Check
those numbers before reading the image: a landcover percentage that has moved says the
generator changed, where the image alone cannot tell you whether generation or the
palette moved.

The procedurally aged paper stock (`PaperTexture`) has its own sheet, and the same rule
applies twice over — it produces both a picture and a contrast ratio, and neither catches
what the other does:

```powershell
# Menu: Strategos > Bake Paper Contact Sheet
& "C:\Program Files\Unity\Hub\Editor\6000.0.75f1\Editor\Unity.exe" `
    -batchmode -quit -nographics -projectPath . `
    -executeMethod Strategos.Editor.PaperContactSheet.Bake -logFile paper-sheet.log
# -> Artifacts/paper-contact-sheet.png, Artifacts/paper-detail.png
```

It prints, per cell, the darkest pixel produced and the WCAG contrast of `UiTheme.Ink`
against it — measured from the baked texture, not predicted from the options. **A stain
that costs contrast is invisible as a bug**, because it reads as styling rather than as a
failure, so the number is the only thing that catches it. The detail page reports two
figures and *the second is the one that matters*: whole-sheet contrast includes the inside
of a coffee ring, where no text is ever placed, while the in-reserve figure is what text
will actually be read against.

`PROBE PASSED` here asserts only what each preset claims — `PaperOptions.RequiresReservedText`
decides whether a preset is held to 7:1 across its whole surface or only inside its reserved
rects. A preset that says it is safe unreserved and is not will fail the bake.

The simulation has no picture to read, so it has probes instead. All four run under
`-batchmode -quit -nographics` and print a summary followed by `PROBE PASSED`/`FAILED`:

| `-executeMethod` | Asserts |
|---|---|
| `Strategos.Editor.MapMeshProbe.Run` | Drape mesh counts, extent, UVs, skirt, no NaN |
| `Strategos.Editor.ScenarioProbe.Run` | Round trip, and that it regenerates *the same ground* |
| `Strategos.Editor.CommunityScenarioLoadProbe.Run` | Arbitrary community JSON via FromJson/LoadFromFile; no shipped-name allowlist |
| `Strategos.Editor.CommandProbe.Run` | The four delivery rules, queues, A\*, replay divergence |
| `Strategos.Editor.ReportProbe.Run` | Detection edges, report timing, replay of reports |
| `Strategos.Editor.CombatProbe.Run` | The engagement matrix, terrain, simultaneity, replay |
| `Strategos.Editor.ReactionProbe.Run` | Each ROE, reflex preemption, break contact, fairness |
| `Strategos.Editor.VictoryProbe.Run` | Objective control, hold duration, draws, precedence |
| `Strategos.Editor.DirectorProbe.Run` | An unattended scenario reaches a decision |
| `Strategos.Editor.DoctrineProbe.Run` | Drill pack round trip, and the T/P/U matrix |
| `Strategos.Editor.TrainingProbe.Run` | The hesitation curve, and that training only costs time |
| `Strategos.Editor.FatigueProbe.Run` | Fatigue and recovery curves, the floor, and that idling is free |
| `Strategos.Editor.HierarchyProbe.Run` | The ORBAT tree, rollup, decomposition, and that formations never fight |
| `Strategos.Editor.CasualtyProbe.Run` | A wreck is not a contact; losses recorded and in the signature |
| `Strategos.Editor.EchelonProbe.Run` | Zoom bands contiguous, round-tripping, and usable on a small map |
| `Strategos.Editor.DefendProbe.Run` | Defend never ends, digging in costs time and pays, Hold is not Abort |
| `Strategos.Editor.SpecialActionsProbe.Run` | DigIn → Hold/Defend clock + fire cut; artillery CanDigIn refuse; DIG IN palette |
| `Strategos.Editor.WorldLayerProbe.Run` | Spawn changes Signature; hazard blocks Passable; despawn; drawer pixels |
| `Strategos.Editor.ScenarioGeneratorProbe.Run` | Generated scenario validates; force ratio; SideEnv Reset/Step smoke |
| `Strategos.Editor.ReplanProbe.Run` | Mid-march hazard: detour or fail; never walk through blocked cell |
| `Strategos.Editor.ObjectivePlacementProbe.Run` | PlaceNear resolve; missing ref fails; generator fallback |
| `Strategos.Editor.ScreenProbe.Run` | Screen never ends, does not dig in, detection reaches further than Halted |
| `Strategos.Editor.GuardProbe.Run` | Guard never ends, digs in like Defend, modest detection once prepared |
| `Strategos.Editor.CoverProbe.Run` | Cover digs in, no watch bonus, does not break contact while covering |
| `Strategos.Editor.WithdrawProbe.Run` | Withdraw expands; unit pulls away from the threat |
| `Strategos.Editor.DelayProbe.Run` | Delay holds until pressed, then converts to Withdraw |
| `Strategos.Editor.AttackProbe.Run` | Attack closes when far, Engage-only when already close |
| `Strategos.Editor.ReconProbe.Run` | Recon closes to standoff then Screens with detection stretch |
| `Strategos.Editor.ExploitProbe.Run` | Exploit drives past the threat and Engages |
| `Strategos.Editor.PursueProbe.Run` | Pursue closes tighter than Attack then Engages |
| `Strategos.Editor.ObservationProbe.Run` | Fog-leak: belief identical for unseen hostile move; naive GT differs; in-range differs |
| `Strategos.Editor.ActionSpaceProbe.Run` | Drill+ADVANCE mask gates vs ExpandDrill / readiness / busy / unheld objectives |
| `Strategos.Editor.RewardProbe.Run` | Terminal ±1/0; objective/force shaping; Step has no contact/report input |
| `Strategos.Editor.EnvProbe.Run` | SideEnv Reset signature-stable; Step Issue-path parity; mask skip; done on win |
| `Strategos.Editor.ThroughputProbe.Run` | Map gen + 3600-tick step timings; maps/hour and episodes/hour |
| `Strategos.Editor.TrajectoryProbe.Run` | Trajectory JSON round-trip; belief-only obs from ReportLog; fog-leak twin |
| `Strategos.Editor.MapPaletteProbe.Run` | NatoTopo in dropdown; colours distinct from Topographic; draw differs |
| `Strategos.Editor.RankGateProbe.Run` | Rank authority table; under-rank refuse; BN allow; promote |
| `Strategos.Editor.CommandScopeProbe.Run` | PlayerEchelon derive/validate; Company seat refuses BN Issue; rank prefers authored |
| `Strategos.Editor.CareerAcrossCampaignsProbe.Run` | CareerProfile stamp/round-trip; highland Regiment≠valley; directive From continues |
| `Strategos.Editor.GameModesProbe.Run` | ModeKind; spectator both directors decide; hotseat side flip |
| `Strategos.Editor.AiDifficultyProbe.Run` | Easy/Hard ladder; personality packs; Hard issues more orders than Easy |
| `Strategos.Editor.DrillProbe.Run` | Drills become orders, bind directionally, reach a formation's troops |
| `Strategos.Editor.ShippedMapProbe.Run` | Every shipped scenario, generated with erosion exactly as authored: objective and unit cells are passable, and every objective is reachable per side by a real `PathFinder.Find` |
| `Strategos.Editor.SaveLoadProbe.Run` | Round-trip and step-after-restore `Signature()` comparisons, one dedicated assertion per state-audit row `Signature()` does not cover, the file store round trip, and version refusal |
| `Strategos.Editor.GameStoreSeamProbe.Run` | `IGameStore` async + `StoreResult`; `IContentSource`; anonymous `IPlayerIdentity` |
| `Strategos.Editor.TutorialFirstBeatProbe.Run` | Tutorial select→MoveTo phase machine (#310) |
| `Strategos.Editor.ContextHelpProbe.Run` | MOVE context help text; ContextHelpOverlay Open/Close |
| `Strategos.Editor.PreferenceStoreProbe.Run` | `JsonPreferenceStore` ConfirmOrders + display fields write/read; legacy JSON defaults |
| `Strategos.Editor.DisplayModeProbe.Run` | AppShell display-mode API (remembered windowed size; ToggleFullscreen callable) |
| `Strategos.Editor.DisplayPrefsProbe.Run` | Display prefs round-trip + Settings/F11 share AppShell Apply* (#392) |
| `Strategos.Editor.UiShellProbe.Run` | MainMenuView / SettingsView categories / PauseOverlay keys and Build/Open/Close |
| `Strategos.Editor.SteamProbe.Run` | `SteamClientHost` Init no-ops without Steamworks; Overlay / Achievement / Cloud stubs guarded (#305) |
| `Strategos.Editor.CampaignChainProbe.Run` | `CampaignChain` round trip: every field, including the outcome enum and carried-over ORBAT state — #75 chunk 1, data shape only |
| `Strategos.Editor.CampaignCarryOverProbe.Run` | `CampaignCarryOver.CarryOver` — wreck exclusion (red-then-green), exact readiness-recovery arithmetic unclamped and at the clamp, outcome mapping for Won/Lost/Drew, both undecided-simulation caller-error paths — #75 chunk 2, carry-over logic only |
| `Strategos.Editor.CampaignChainDriverProbe.Run` | `CampaignChainDriver.MergeCarriedOver`/`.StartNext` — a real two-operation round trip (skirmish -> push-north) played unattended to a decision on both ends, merged units carry Strength/Readiness/Training/Roe/Supply while every unit (matched or reinforcement) keeps the next scenario's own Cell/ParentId, the rest-hours-by-outcome ("defeat cost") pattern, an unmatched carried-over unit throws (red-then-green) — #75 chunk 3; and a real three-operation chain (skirmish -> push-north -> skirmish) played unattended to a decision on all three ends, one persistent unit's Strength asserted continuous (exact carried value at the next operation's start) and monotonically, cumulatively falling across all three — #75 chunk 4 |

**Fifteen of the probes above run with `scenario.Map.EnableErosion = false`** —
`CampaignCarryOverProbe`, `CasualtyProbe`, `CombatProbe`, `CommandProbe`, `DefendProbe`,
`ScreenProbe`, `GuardProbe`, `CoverProbe`, `WithdrawProbe`, `DelayProbe`, `AttackProbe`,
`ReconProbe`, `ExploitProbe`, `PursueProbe`, `ObservationProbe`, `ActionSpaceProbe`, `RewardProbe`, `EnvProbe`, `ThroughputProbe`, `TrajectoryProbe`, `ControlMeasureProbe`, `MapPaletteProbe`, `RankGateProbe`, `CommandScopeProbe`, `CareerAcrossCampaignsProbe`, `GameModesProbe`,
`DirectiveProbe`, `DirectorProbe`, `DrillProbe`, `HierarchyProbe`, `MapMeshProbe`,
`ReactionProbe`, `ReportProbe`, `ScenarioProbe`, `UnitModelProbe`, `VictoryProbe` — because
erosion is the dominant generation cost and none of them are *reasoning about terrain*: they
need a unit to
stand on, not a faithful landscape. That used to be a silent gap rather than a deliberate
trade-off: `skirmish.json` ships `EnableErosion: true`, erosion runs before hydrology (see
CLAUDE.md's pipeline diagram), so disabling it changes where water pools and what gets
classified where — at `(119,123)` that was the difference between `Forest` and `Water`, and
`THE CROSSROADS` shipped sitting in the lake that only the real, erosion-on map has (#95),
invisible to eighteen green probes that never generated it (#96). **`ShippedMapProbe` is what
makes running the other fourteen erosion-off safe rather than merely fast**: it is the one
probe that reads `EnableErosion` from the scenario instead of overriding it, so it is the one
thing in the suite that ever looks at the map the player actually loads. Do not turn erosion on
in any of the fourteen to "fix" this — that was considered and rejected (see #96): it would
make the whole suite slow for a property this one cheap probe already covers.

**Run `CommandProbe`, `ReportProbe` and `CombatProbe` after touching anything under
`Core/Commands`, `Core/Reports`, `Core/Combat`, `Core/Movement` or `Core/Messaging`.**
Their divergence tests are the only thing standing between a determinism bug and finding
out months later that a replay does not reproduce — nothing about that failure is visible
at the time it is introduced.

**A change to unit state moves every divergence baseline, so "the probes still pass" is not
evidence it is right — they pass *differently*.** Training was the first such change:
hesitation is part of `CommandQueue`'s signature and delayed reports change the report log.
What `TrainingProbe` asserts instead is the property that survives any retuning — **a unit at
`Training = 100` behaves exactly as it did before the feature existed**, so anything that
moved in another probe's numbers is a real regression rather than the new feature showing up.
**Fatigue could not keep that property** and did not try: it is a world rule rather than an
opt-in attribute, so every scenario's numbers move by design. What `FatigueProbe` asserts
instead is the *shape* — costs only fall, recovery only rises, neither passes its bound, a
destroyed unit is inert, and **a unit that does nothing is not worn down by time alone**.
That last one is a design decision held in place by a test: a slow drift for "time without
rest" is realistic and would make a long scenario unwinnable regardless of what the player
does, so it is deliberately absent and the probe fails if it comes back.

The movement it caused was small and attributable: the reference firefight went from 139 to
140 ticks because engaged units now tire, and `CombatProbe`'s terrain matrix is unchanged
because it is sampled from full readiness — which is now stated in the matrix header, since
readiness stopped being a constant.

**`CombatProbe`'s table is the point of it, not its pass/fail.** Balance the combat model by
reading the printed damage-per-minute matrix; the assertions only catch the model breaking,
not the model being wrong. It stamps landcover onto one fixed pair of cells rather than
hunting the map for a forest cell — searching varies elevation and distance along with the
cover, so "forest halves incoming fire" gets measured against a different slope on a
different hill, and the number cannot be attributed to anything.

Player log (**always check after a UI change**, see UI gotchas below):

```
%USERPROFILE%\AppData\LocalLow\DefaultCompany\strategos\Player.log
```

Editor menu: `Strategos → Build/…`, `NATO Symbol Generator`, `Open Demo Scene` (F5),
`Recreate Demo Scene`, `Bake Symbol Contact Sheet`, `Bake Map Contact Sheet`,
`Probe Map Mesh`, `Probe Scenario`, `Probe Commands`, `Probe Reports`,
`Write Sample Scenarios`, `Write Sample Drills`, `Write Sample Config`, `Probe Training`,
`Probe Fatigue`, `Probe Campaign Chain`, `Probe Campaign Carry Over`,
`Probe Campaign Chain Driver`, `Probe Campaign Chain Validate`,
`Probe Campaign Save Load`, `Probe Save Load`,
`Import TMP Essential Resources`.

**A new field on a serialised type does nothing until the samples are rewritten.**
`Assets/Resources/Scenarios/*.json` is what the game loads, not `ScenarioSamples`, so a field
added to `UnitInstance` deserialises to its default in every shipped scenario until
`Strategos > Write Sample Scenarios` runs. `TrainingProbe` caught exactly this — it measured a
scout that was supposed to be green, found it fully trained, and passed anyway because its
guard skipped when the data was uninteresting. It now fails if no unit in the sample scenario
is below 100. **A guard that skips when the fixture is stale is a guard that cannot fail.**

**Renaming a `CommandKind` is a content migration.** Enums serialise *by name*, which survives
reordering and does not survive renaming: `Hold` becoming `Defend` made every shipped drill
that referenced it fail to deserialise, and `DoctrineProbe` threw on load. That is the right
behaviour — loud, with the file and line — but it means `Strategos > Write Sample Drills` has
to run in the same change. **The same now applies to saves, #74 having landed**: a `CommandQueue`
entry's `Command.Kind` and everything else `SimulationSnapshot` carries serialises the same way,
through the same `FieldsOnlyResolver`/`StringEnumConverter` pair `ScenarioIO` uses (see
`FileGameStore`) — renaming a kind a save references fails the same way a drill does. Unlike a
drill, there is no `Strategos > Write Sample Saves` to re-run: a save is player state, not
shipped content, so an incompatible rename is exactly what `SaveRecord.FormatVersion` exists to
have refused already, loudly, at load — see `docs/simulation-invariants.md`'s note on what
`Signature()` does and does not cover.

**Never index into `Scenario.Units` by position.** `ReportProbe` took `Units[0]` and `Units[3]`
as "the mover and an enemy"; adding battalion formations to the sample ORBAT shifted every
index by two, and the probe spent 900 ticks testing detection between two units on the *same
side* — five failures, none of which pointed at the cause. `DirectorProbe` had the same bug
and was worse: it set a formation's stored strength, which the rollup ignores and the director
never sees, so its assertion passed while being unable to fail. Select by designation, or from
`Simulation.Units`, which is fighting units only.

**Drills are content, not code.** `DoctrineSamples` holds the shipped set in C# and
`Strategos > Write Sample Drills` serialises it to `Assets/Resources/Doctrine/`; the app
reads the JSON. Editing a drill therefore needs no recompile — but **changing
`DoctrineSamples` without rewriting the pack changes nothing in the player**, which is the
one trap in this arrangement. `DoctrineProbe` catches the reverse case: it asserts the pack
loads from Resources rather than from the in-code fallback, because the fallback is the same
drills and is invisible on screen.

`DoctrineProbe`'s readiness table is the point of it, as `CombatProbe`'s matrix is. It prints
two: the sample force, which is all fresh companies and platoons and therefore all `T`, and a
constructed matrix over echelon and condition which is the one that can actually fail — a
matrix that never produces one of the three ratings is not exercising the thresholds, whatever
it prints.

---
