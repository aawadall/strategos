# Known gaps

Recorded so they are not re-investigated. **None are fixed.** Check here before
chasing anything that looks like a bug in generation, detection, combat balance or CI.

[CLAUDE.md](../CLAUDE.md) is the index.

---

## Known gaps

Recorded so they are not re-investigated. None are fixed.

- **Artillery is the best direct-fire weapon in the model, which is wrong.** The matrix has
  it killing infantry in the open in 2.0 minutes against armour's 2.7. Its `Firepower` of 30
  represents an indirect-fire battery, and #12 resolves direct fire only — so the number is
  being spent on something it does not describe. Indirect fire is Phase 4.2 and out of scope
  here. Until it exists, either artillery needs a separate direct-fire figure or it should
  not be given engage orders.
- **A destroyed unit stays on the map — deliberately now, as a wreck.** It is not
  commandable, not detected, and not counted among a side's troops, and the loss is recorded
  in `CasualtyLog` with tick and killer. What is still missing is **reconstitution**: a
  formation brought back to strength is indistinguishable from one that never fought, which
  matters only once a campaign carries an ORBAT between operations (#75).
- **`CancelFrom` does not halt the unit it stops, and `Abort` does.** `Simulation.
  OnCommandDelivered` follows an Abort with a halt report and `ApplyAbortPosture` and follows
  a CancelFrom with neither, so cancelling a *running* move empties the queue while leaving
  `unit.Posture` at `Moving` — a permanent 1.25x to incoming fire (`CombatProbe`'s matrix:
  open 15.96 against moving 19.95) for a unit that is standing still. `PlayView.CancelPlanFrom`
  sends an Abort for index 0 to dodge it, which is why the player does not show it; remove that
  workaround when #56 lands.
- **A queue index is stale by the time it is delivered.** `Command.CancelFrom` addresses an
  entry by position and commands deliver one step after they are issued, so a head that
  completes in between shifts every entry down one and the cancel lands on the wrong order.
  One tick wide, so it is rare at x1 and likely at x60+. Fixing it means addressing the entry
  by `Command.Seq` instead — #57.
- **An order issued while the clock is paused has no visible effect.** `AdvanceSimulation`
  returns early when paused, so `CommandBus.Deliver` never runs and every order — move,
  engage, abort, hold, cancel — sits on the bus until the clock restarts. The command path is
  right; the missing part is any sign to the player that something is in flight. #59.
- **A mutual firefight settles into a suppression equilibrium** at roughly 70 points each,
  where both sides trade about 4.5 damage a minute instead of 16. That is arguably the right
  behaviour — it makes flanking, cover and digging in the way to break a stalemate rather
  than out-shooting it — but it means a head-on exchange between equals takes about twenty
  minutes to decide, which is slow if it turns out not to be what the game wants.
- **Detection ranges are large enough that the skirmish has almost no fog of war.** The
  recon platoon's 4000 m sees 160 cells on a 256-cell map, so `SCT/1-7 IN` reports contact
  on all three OPFOR units at T+0001 and the scenario's own description — *"neither side
  knows the other is there"* — is false from the first tick. The numbers are individually
  defensible (4 km is a reasonable ground-scout range in the open) and the map is simply
  small: 6.4 km square is one company frontage, not a divisional area. Three fixes, none
  taken: terrain LOS, which is the real answer and a Phase 1 / M1 item; a larger map;
  or lower ranges, which would be tuning the model to hide a missing feature. Note the
  *reporting* is correct — it is the visibility model that is trivial.
- **`Side.AreHostile` is "different side, different affiliation", which is a stand-in.**
  It gets coalitions right and gets one case wrong: two mutually hostile factions that both
  draw as Hostile read as allies, because nothing in the data says otherwise. A three-way
  scenario must therefore give its factions distinct affiliations until a real alliance
  graph lands. It is one method on purpose, so replacing it is not a search.
- **`ContactTracker.Sweep` is O(n²) per step.** Thirty-six distance tests a second at
  sandbox scale, and a spatial index would be code with no measurable benefit. It stops
  being acceptable in the low hundreds of units, well before the theatre scale the roadmap
  ends at; a uniform grid bucketed by detection range drops in without changing anything
  that consumes reports.
- **A contact report names the real `UnitId`,** which hands the recipient a perfect
  identification of something it has merely seen at range. When misidentification becomes
  possible, `SituationReport.Subject` should become a *track* id resolvable to a unit only
  by the simulation. Recorded now because every consumer written against a truthful
  `Subject` is one that has to be revisited.
- **Generated terrain has huge closed basins, so maps come out 18–29% lake.** Measured on
  256-cell maps at seed 20260729: Hills 11,676 lake cells with 8,396 over 5 m deep and a
  deepest point of 38.7 m; Mountains deepest 381.7 m; Desert 20% water. These are real
  depressions in the noise, not a classification error — both flood-surface bugs above were
  found and fixed while chasing this and neither moved the numbers. Real landscapes lack
  them because fluvial erosion breaches them; the generator has no equivalent. The fix is a
  breaching pass (carve an outlet from each basin's low point to its spill) or a minimum
  catchment test before a depression is allowed to be a lake — **not** raising
  `HydrologyStage.LakeDepth`, which would only shrink the shorelines of basins that should
  not exist. Note `FillDepressions`' comment deliberately preserves hollows as tactical
  features, so breaching needs a size threshold rather than being applied wholesale.
- **There is still no test assembly anywhere under `Assets/`,** so the EditMode test job
  has nothing to run. `com.unity.test-framework` is in the manifest, so the scaffolding is
  one asmdef away. `build` no longer has `needs: test` — gating releases on an empty test
  run only added a failure mode — so **restore that dependency when real tests land**.
- **CI cannot activate Unity: no `UNITY_LICENSE` / `UNITY_EMAIL` / `UNITY_PASSWORD` secrets
  are set on the repo.** Every hosted build and test therefore skips. The `preflight` job
  probes for those credentials and the Unity jobs are `if`-gated on it, so a run without
  them reports neutral-green with a warning annotation rather than a red X that says
  nothing about the code — but note that **green CI currently means "nothing ran"**. Set
  the secrets under Settings → Secrets → Actions to get real coverage. The `secrets`
  context is unavailable in a job-level `if`, which is why the probe is a job and not a
  condition.
- **`.gitattributes` line-ending rules are overridden.** `git check-attr text filter --
  "Assets/TextMesh Pro/Sprites/EmojiOne.png"` reports `text: auto` despite the `-text`
  flag on `*.png` / `*.ttf`, so a later `* text=auto` rule wins. Harmless while LFS
  carries the content (verified: committed PNG/TTF headers intact), but it would corrupt
  a binary added without an LFS rule.
- **Four land entity codes render as a bare frame.** `IconDecorator.ResolveLandIcon`
  handles 11 of the 14 `LandEntityCode` values; `Unknown`, `SpecialOperations`,
  `MissileBallistic` and `Cyber` fall through to its `default` and draw nothing inside the
  frame. The symbol library lists them anyway, captioned `FRAME ONLY` — a catalogue that
  hides the gaps is worse than one that shows them. `DisplayNames.RendersIcon` is the
  lookup and must be kept in step with `ResolveLandIcon`.
- **Only land symbol sets draw icons at all.** `IconDecorator.Contribute` returns early
  unless the set is `LandUnit` or `LandCivilian`, and `ProceduralSymbolFactory` only draws
  land frames, so the other 19 `SymbolSet` values would render as empty land frames. This
  is why the library offers no symbol-set axis.
- **Airborne and air assault share one chevron glyph.** `SectorModifierDecorator`
  resolves `ModAirborne` and `ModAirAssault` to the same case, so they are
  indistinguishable on screen despite being separate dropdown entries. Same for the Air
  Assault entity-type variant.
- `Packages/packages-lock.json` is gitignored, which undercuts reproducible CI builds.
- **Objectives are authored by coordinate, not by map feature, so a fixed cell can drown or
  drift out of reach whenever the generator changes underneath it.** #95 was exactly this:
  `THE CROSSROADS` at `(119,123)` was `Forest` with erosion off and `Water` with it on — the
  shipped setting — and shipped unreachable to every unit type on both sides.
  `Scenario.Validate` now checks an objective's cell is passable when given a catalogue and
  map, and `ShippedMapProbe` (`docs/build-and-verify.md`) checks it against the real shipped
  map, so the specific failure is caught; the underlying fragility is not. The fix moved the
  objective to `(119,114)`, chosen for defensible, low-slope, well-buffered ground rather than
  the next dry cell over — but it is still a hardcoded coordinate. Worth recording precisely:
  on seed `20260729` at 256×256, `NetworkStage`'s road network (a 5-edge spanning tree over 6
  perimeter settlements, no loops on this seed) never reaches the interior valley where the
  two forces meet — the closest any road gets to the contested ground is 68 cells (1700 m).
  "Place the objective on a real road junction" is not achievable there without abandoning the
  scenario's roughly-equidistant meeting-engagement premise, which is itself evidence for #51
  ("place objectives by map feature, not only by coordinate") over patching individual
  coordinates as they are found broken.
