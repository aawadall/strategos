# Tiered releases — purchase pass unlocks scenarios (#465)

Product shape for **what ships free vs what a purchase unlocks**. Not the
scenario *host* ([#464](https://github.com/aawadall/strategos/issues/464)) and
not live multiplayer (§7.3). Builds on the #355 seams
([local-api-seam.md](local-api-seam.md)): content is still `IContentSource<T>`;
identity / store are where entitlements attach.

[CLAUDE.md](../CLAUDE.md) is the index. Steam pricing context lives in
[steam.md](steam.md). Authored fight inventory lives in
[Research/historical/SHORTLIST.md](../Research/historical/SHORTLIST.md).

---

## Decisions (binding for #465 children)

### Three product tiers

| Tier | What the player gets | How they get it |
|---|---|---|
| **Free** | Onboarding + one evening of play | itch PWYW / free Steam demo SKU / always-on in base build |
| **Base** | Full sandbox modes + shipped campaigns + the free historical set | Paid base game (Steam EA / itch paid) |
| **Pass** | Additional authored scenario packs as they ship | One **Scenario Pass** SKU (Steam DLC or itch unlock); optional later pack SKUs |

**Do not invent a fourth “season pass that expires.”** The first pass is a
permanent unlock of the *current* paid catalog plus whatever packs are marked
`IncludedInPass` when they ship. Time-limited battle-passes are out of scope.

### What stays Free forever

These must load with **no** entitlement check (offline, anonymous identity OK):

| Content | Why |
|---|---|
| `tutorial-squad` | Teach the loop (#289) |
| Climb campaign (`climb-squad` → company → battalion) | Echelon ladder is the product thesis |
| One sandbox (`skirmish`) | Impulse “just fight” |
| One historical demo (`little-round-top-20th-maine`) | Proof that history ships; teaching hook for #421 |

Everything else on the main menu may be **Base** or **Pass** once gating is on.

### Base vs Pass split (first cut)

| Catalog | Tier | Notes |
|---|---|---|
| `push-north`, Valley / Highland campaigns | **Base** | Core product, not DLC |
| Modes already shipped under #287 | **Base** | Solo / hotseat / spectator / replay |
| Belleau Wood, Remagen (and later shortlist converts) | **Pass** | Historical packs are the monetisation surface called out in [steam.md](steam.md) |
| Future doctrine / ORBAT packs | **Pass** (or separate DLC) | Same entitlement seam; separate SKU only if marketing needs it |

**Alpha exception:** the public itch build may keep **all** menu fights unlocked
until Base/Pass SKUs exist. Gating is a *release* switch, not a mid-alpha
surprise. Document the switch in release notes when it flips.

### One Scenario Pass SKU first

- **Steam:** one DLC App (or additional depot) owned → `HasScenarioPass == true`.
- **itch:** one unlock key / paid download that sets the same flag in the local
  entitlement store (no Steam required).
- **Later packs** (e.g. “Pacific historical pack”) are additional DLC AppIds that
  OR into the same check: `ownedPass || ownedPack(id)`.

Do **not** put per-scenario microtransactions in v1. Unlock is by **pack /
pass**, not by individual fight.

### Client model — entitlement over content filter

```
IPlayerIdentity  →  who is playing (local / Steam)
IEntitlementStore →  which tiers/packs they own (local file + Steam ownership query)
IContentSource    →  still returns Scenario bytes; menu/loader filters by catalog tier
```

Binding rules:

1. **Catalog metadata** lives next to the scenario (JSON field or sidecar
   `ContentCatalog` entry): `Id`, `Tier` (`Free` | `Base` | `Pass`), optional
   `PackId`, `IncludedInPass`.
2. **Load path never crashes on deny.** Locked rows show on the menu with a
   clear “requires Scenario Pass / base game” affordance; selecting them opens
   store / itch URL or a short explanation — they do not throw.
3. **Resources may still contain Pass JSON** in early builds (easy to strip with
   a build define later). Entitlement is the gate; obfuscation is not the design.
4. **Offline:** cached entitlements from last successful Steam/itch check win;
   Free tier always works. Do not require a server round-trip to start Free
   content (#464 is optional hosting, not a DRM phone-home).

### Menu / UX

- Main menu SCENARIO column lists Free and owned content as today.
- Locked Pass rows remain visible (discovery) with a lock mark and one-line
  reason — same density as LITTLE ROUND TOP / BELLEAU WOOD buttons.
- Pause / FIELD MANUAL / Historical Notes stay available for **loaded**
  scenarios; do not tease Pass-only commentary behind a second paywall.

### Teaching and #421

Historical commentary (`HistoricalNotes`) ships **with** the scenario it
describes. Unlocking Belleau unlocks its briefing note. Do not sell commentary
separately from the fight.

---

## Distinct from

| Issue / page | Question |
|---|---|
| **#464** scenario server | Where remote bytes live |
| **#355** local/API seam | Interfaces only — no product tiers |
| **Steam Workshop** ([steam.md](steam.md)) | Community subscribe — free content channel |
| **§7.3 online multiplayer** | Match host, not content unlock |
| **Rank / career gates** (#76) | Command *seat* authority — orthogonal to purchase |

A Workshop scenario is never blocked by the Scenario Pass. Paid packs are
first-party (or licensed) catalog entries.

---

## Out of scope (this design)

- Implementing Steam DLC Apps, depots, or itch key redemption UI.
- Server-side license validation / anti-tamper.
- Per-scenario prices, cosmetics, or battle-pass seasons.
- Changing PWYW alpha catalog before Base/Pass SKUs exist.
- Linux/macOS SKUs (platform builds are Phase 10; entitlements are store-agnostic).

---

## Suggested children (file when leaving backlog)

About five minutes each; do not open until Base/Pass is on the critical path:

1. Docs: phases / steam cross-links + alpha “gating off” note (this page).
2. `ContentTier` enum + catalog entry shape on Scenario or sidecar.
3. `IEntitlementStore` + always-owns-Free local stub.
4. Menu filter: hide-or-lock Pass rows from catalog + entitlement.
5. Probe: Free loads anonymous; Pass denied without entitlement; owned Pass loads.
6. Steam ownership adapter stub (behind #288) — returns false until AppId exists.
7. Release checklist: when to flip the alpha exception.

---

## Pricing (guidance only — not binding)

Aligns with [steam.md](steam.md) post-launch DLC band:

| SKU | Ballpark | Notes |
|---|---|---|
| Base (EA) | ~$25 | Full sandbox + campaigns + free historical |
| Scenario Pass | $5–10 | Unlocks Pass-tier historical packs as they ship |
| Later named pack | $5–10 | Only if Pass catalog grows too large for one SKU |

itch stays PWYW for the Free/demo slice; paid itch build maps to Base + optional
Pass key.

---

*Design for #465. Update this page when a binding decision above changes.*
