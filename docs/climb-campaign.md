# Climb campaign — design note (#404 / #403)

A single authored `CampaignChain` that escalates the player's **command seat**
(`Scenario.PlayerEchelon`) across operations. This is the content pack for
`ROADMAP.md`'s "Echelon is the difficulty curve" — not career handoff across
theatres (#109), and not the interactive tutorial (#289).

[CLAUDE.md](../CLAUDE.md) is the index. Carry-over / merge rules live in
[campaign-invariants.md](campaign-invariants.md). PLAY campaign seams live in
[campaign-play-plan.md](campaign-play-plan.md).

---

## Decisions (binding for #405–#408)

### Seat ladder

**Three operations: Squad → Company → Battalion.**

| Op | Seat (`PlayerEchelon`) | Why this step |
|---|---|---|
| 1 | `Squad` | Degenerate C2 — you *are* the unit; orders feel instant (`ROADMAP.md`) |
| 2 | `Company` | Subordinates exist and are close; first real formation address |
| 3 | `Battalion` | Reports mediate; formation decomposition delay bites |

**Stop at Battalion inside this chain.** Regiment stays Highland's job
(`highland-campaign` / #213) so a climb win → career promote to `regiment` →
Highland remains the second-theatre story. Do not duplicate that seat here.

Platoon is skipped as its own op: no shipped platoon-seat scenario, and the
rank table's lowest rung is already `platoon` while the pedagogically important
"no C2 problem" seat is **Squad**.

### Career / rank gates

Default career is `battalion` (`RankAuthorityDefaults.DefaultRankId`).
`RankGate.MayCommand` allows a higher rank to sit in a lower seat, so the
default career may start the climb at Squad and play through Battalion without
mid-campaign promotion.

**Do not change promotion rules for this epic.** Promotion remains
end-of-campaign-on-win (`TryPromoteAfterCampaignWin`). After a climb win the
player becomes `regiment` and Highland unlocks as today.

Optional later (out of scope): promote one rung per won op for shoulder-board
flavour. That needs new code; the first climb must not depend on it.

### Scenario reuse vs new JSON

| Op | Scenario name | Action | Notes |
|---|---|---|---|
| 1 | `climb-squad` | **New** thin variant of `tutorial-squad` | Same small map + two leaves; **different** `Name` so `ScenarioSamples.IsTutorial` / #310 first-beat banner does **not** fire mid-campaign |
| 2 | `climb-company` | **New** | Company HQ over the surviving squad leaves; `PlayerEchelon = Company` |
| 3 | `climb-battalion` | **New** thin cousin of `skirmish` structure | Battalion HQ over the company; `PlayerEchelon = Battalion` |

**Do not reuse by name:**

- `tutorial-squad` — reserved for #289 onboarding (`IsTutorial` / first beat).
- `skirmish` / `push-north` — Valley's ID contract and `PlayerEchelon: None` derivation; overloading them couples climb to Valley.
- `highland-opening` — Regiment seat for #109's second theatre.

Valley and Highland campaigns stay as they are. Climb is a **third** shipped
chain: `Assets/Resources/Campaigns/climb-campaign.json`, sample name
`CampaignSamples.ClimbName = "climb-campaign"`.

### Unit Id stability (hard rule)

`CampaignChainDriver.MergeCarriedOver` matches by `UnitId` and **throws on
unmatched carried units**. Every climb scenario must:

1. Keep the same persistent leaf ids across all three ops for units that can
   survive (start with friendly leaf `1` and hostile leaf `2`, matching
   tutorial-squad's pair).
2. Introduce higher HQs and extra subordinates as **new ids** on later ops
   (reinforcements), the same pattern `push-north` uses for id `9`.
3. Author `ParentId` on the *next* scenario — merge takes Cell/ParentId from the
   next op, combat state from carry-over.

Sketch (friendly side; hostile mirrors with its own ids):

```
Op1 Squad:     1 (squad leaf)
Op2 Company:   11 (company HQ) → 1 (squad), (+ optional new squad leaf 3)
Op3 Battalion: 7 (battalion HQ) → 11 → 1, 3
```

Hostile ids `2` (and later hostile formation ids) follow the same stability rule.
Shipped in #405 as `ScenarioSamples.ClimbSquad` / `ClimbCompany` /
`ClimbBattalion` and `Assets/Resources/Scenarios/climb-*.json` — leaf ids `1`/`2`,
company HQs `11`/`12`, battalion HQs `7`/`8`, optional leaves `3`/`4`.

### Maps

- Ops 1–2: small sheet (tutorial / push-north scale, ~64×64, erosion off) — fast load, readable at squad/company zoom.
- Op 3: skirmish-scale sheet is fine; battalion zoom span already matches shipped play.

Relief profile may differ per op; seed may differ. Id contract matters more than
shared terrain.

### PLAY entry (#407)

Add `START CLIMB CAMPAIGN` on `MainMenuView` / PLAY campaign
row beside Valley and Highland. Wired through `AppShell.StartClimbFromMenu` →
`PlayView.StartClimbCampaign` the same way Valley/Highland already do.
`RankGate.Authorize` on the first op still runs — default battalion career may
command Squad.

### Probe (#408)

Minimum: two-op climb through `CampaignChainDriver` (Squad → Company is enough
for the probe; three-op can be a follow-up assert in the same probe if cheap).
Assert after `StartNext` for op 1 that `scenario.PlayerEchelon == Company` and
command scope accepts the company HQ, not only leaf `1`. Prefer extending
campaign probe style (`CampaignChainDriverProbe`) over a PLAY UI click script.

---

## Explicit non-goals

| Not this epic | Where it lives |
|---|---|
| Tutorial first-beat / HELP overlays | #289 / #308 / #310 |
| Career stamp / second theatre at Regiment | #109 / #213 / `highland-campaign` |
| Mid-op rank promotion each win | Future; not required for Squad→BN |
| Strategic map, inter-op resupply, procedural campaign sequencing | Phase 6.3 open bullets |
| Free-scenario echelon picker UI | phases.md §7.1 note — file separately |

---

## Implementation order (unchanged from #403)

`#404` (this note) → `#405` scenarios → `#406` chain JSON (shipped) → `#407` menu/PLAY (shipped) → `#408` probe → `#409` docs cross-link polish.
