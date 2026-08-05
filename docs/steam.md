# Strategos — Steam Publishing Guide

This document covers everything needed to ship Strategos on Steam as an indie title: Steamworks SDK integration, Early Access strategy, feature alignment, and launch checklist.

---

## App ID gate (#300 / #288)

**Nothing Steamworks talks to a real client without a registered Steam App ID.** That is a
partner/business step, not a code step: register at [partner.steamgames.com](https://partner.steamgames.com)
($100 one-time fee, recouped after $1,000 in sales), create a **Game** app, note the App ID.

Until that exists, the repo ships a **stub seam** so CI and local builds stay green:

| Piece | Where |
|---|---|
| Chosen package | **Steamworks.NET** (below) — not linked until App ID |
| Asmdef + interface | `Assets/Scripts/Steam/` (`Strategos.Steam`, `ISteamClient`) |
| Default impl | `NullSteamClient` — `Init` returns false; Overlay / Achievement / Cloud no-op |
| Boot lifecycle | `SteamClientHost.Bootstrap` / `Shutdown` from `AppShell` (#302) |
| Dev App ID file | `steam_appid.txt.example` → copy to gitignored `steam_appid.txt` |
| Overlay smoke | Settings → GAMEPLAY → **STEAM OVERLAY** (#303) |
| Probe | `Strategos > Probe Steam` / `SteamProbe.Run` (#305) |

### Partner checklist (do before linking the native package)

- [ ] Steamworks partner account created
- [ ] App created (type **Game**); App ID noted
- [ ] `steam_appid.txt` at project root with that App ID (from the example)
- [ ] Steam client installed and logged in on the machine that will smoke-test Overlay
- [ ] Steamworks.NET added via UPM (Git URL below) and a real `ISteamClient` wired behind `SteamClientHost.SetClient`
- [ ] Overlay verified in a development player build (Settings → STEAM OVERLAY)
- [ ] One test Achievement defined in the Steamworks portal; Cloud quota noted for save sync (#304)

Cross-link: Phase 0 bullets in [phases.md](phases.md); epic #288.

---

## Steam Account & App Setup

1. Register a Steamworks partner account at [partner.steamgames.com](https://partner.steamgames.com) ($100 one-time fee, recouped after $1,000 in sales)
2. Create a new App — select **Game**, not Software or DLC
3. Note your **Steam App ID** — copy `steam_appid.txt.example` to `steam_appid.txt` at the Unity project root and replace the placeholder
4. Set up your Steamworks SDK configuration: supported platforms (Windows, macOS, Linux), depots, and build branches (`default`, `beta`, `dev`)

---

## Steamworks SDK Integration (Unity 6)

Tracked as #288. Stub seam: #301–#305 (shipped as `Strategos.Steam`).

### Chosen library (#301)

**Steamworks.NET** (open source, MIT licensed, well-maintained Unity wrapper):

- Package: `https://github.com/rlabrecque/Steamworks.NET`
- Add via Unity Package Manager as a Git URL dependency when an App ID exists
- Wrap `SteamAPI.Init` / `Shutdown` / Overlay / UserStats / RemoteStorage behind `ISteamClient`

Alternative (not chosen): **Facepunch.Steamworks** — more ergonomic C# API, also MIT
(`https://github.com/Facepunch/Facepunch.Steamworks`). Prefer one package; do not dual-link.

### Required Steamworks Features by Phase

| Feature | Steamworks API | Phase | Repo status |
|---|---|---|---|
| Steam Overlay | `SteamFriends.ActivateGameOverlay` | Phase 0 | Stub + Settings control (#303) |
| Steam Remote Play Together | Automatic (enabled per-app in Steamworks portal) | Phase 7 | Portal only |
| Steam Lobbies | `SteamMatchmaking` | Phase 7 | Not started |
| Steam Friends invite | `SteamFriends.InviteUserToGame` | Phase 7 | Not started |
| Steam Workshop | `SteamUGC` | Phase 6 | Not started |
| Steam Leaderboards | `SteamUserStats.FindLeaderboard` | Phase 9 | Not started |
| Steam Achievements | `SteamUserStats.SetAchievement` | Phase 10 | Stub method (#304) |
| Steam Cloud | `SteamRemoteStorage` | Phase 10 | Stub write/read (#304) |
| Steam Rich Presence | `SteamFriends.SetRichPresence` | Phase 9 | Not started |

---

## Early Access Strategy

### Why Early Access?

Strategos is well-suited to Early Access because:
- The wargaming community expects iterative development and actively participates in balancing
- Scenario editor and AI system benefit enormously from community feedback
- Revenue during development funds ongoing work
- Steam's EA framework is standard for the genre (see: WARNO, Regiments)

### Recommended EA Entry Point: v0.5 Beta

This corresponds to Phases 0–7 complete:
- Topographic map + NATO APP-6D symbols
- Full unit/echelon system
- Movement and combat engine
- C2 and orders
- Scenario editor (without Workshop at launch, added shortly after)
- All 5 game modes functional

The AI pipeline (Phase 8) and full online services (Phase 9) are the main EA development pillars communicated to buyers upfront.

### EA Roadmap Communication

Publish a public-facing version of [ROADMAP.md](../ROADMAP.md) on the Steam store page. Update it each major patch. Wargamers respect transparency.

### Suggested EA Duration

12–18 months, targeting 1.0 when:
- Phase 8 AI system ships (RL agent functional at battalion+ scale)
- Phase 9 online services stable
- Phase 10 polish complete

### Pricing

| Stage | Suggested Price | Notes |
|---|---|---|
| Early Access | $24.99 | Standard for genre |
| 1.0 Launch | $29.99 | Price increase on full release is expected and accepted |
| Post-launch DLC | $4.99–9.99 | Historical scenario packs, doctrine packs |

**Product shape:** Free / Base / Scenario Pass — what stays free, what the base game
includes, and what a purchase pass unlocks — is designed in
[tiered-releases.md](tiered-releases.md) (#465). Workshop (below) remains a separate
*community* channel and is never gated by the Pass.

---

## Steam Workshop Integration

The scenario editor (Phase 6) maps directly to Steam Workshop:

- Each scenario `.json` file becomes a Workshop item
- Players subscribe and items sync automatically to their local scenario folder
- Workshop item metadata: title, description, tags (era, echelon scale, region, nation), screenshots, version
- In-game scenario browser queries `SteamUGC.BrowseWorkshop` for subscribed and featured items
- ORBAT packs and doctrine packs can also be published as Workshop items post-1.0

### Workshop Item Structure

```
workshop_item/
├── scenario.json          # Scenario data
├── preview.png            # Workshop thumbnail (512×512 min)
└── metadata.json          # Tags, description, version
```

---

## Steam Remote Play Together

Enables the hotseat game mode to work over the internet without any custom networking code:

- Enable **Remote Play Together** in the Steamworks App Admin portal (one checkbox)
- Host streams their screen; guest inputs are forwarded transparently
- Zero additional code required — Unity's input system works normally
- Supports up to 4 players (adequate for hotseat at any echelon)
- Steam handles NAT traversal, latency compensation, and controller mapping

This effectively gives Strategos free cross-internet hotseat from day one of Early Access.

---

## Steam Achievements

In-game **bar medals** (local ribbon rack, categories + procedural renderer) are designed
separately in [bar-medals.md](bar-medals.md) (#467). Catalogue entries may reserve a
`SteamAchievementId` for later sync; local awards must work without an App ID.

Aim for 20–30 achievements. Suggested categories:

### Progression (echelon milestones)
- *First Blood* — Complete first scenario as a fireteam commander
- *Company Commander* — Win a scenario at company echelon
- *Iron General* — Win a scenario at division echelon or above
- *Theater of War* — Win a scenario at XXXXXX (Theater/Combatant Command) echelon

### Mastery
- *No Casualties* — Win a scenario with zero friendly casualties
- *Encirclement* — Destroy an enemy unit by surrounding it on all sides
- *Blitzkrieg* — Achieve objectives 50% faster than the scenario time limit
- *Logistician* — Win a scenario where no unit ran out of supply

### Game Modes
- *Hot Seat, Hot War* — Win a hotseat game
- *Ghost in the Machine* — Win a match against the hardest AI difficulty
- *Spectator Sport* — Watch an AI vs AI battle to completion
- *Time Traveller* — Watch a full replay of a historical scenario

### Community
- *Scenario Architect* — Publish a scenario to Steam Workshop
- *Popular Commander* — Have a Workshop scenario reach 100 subscribers
- *Living History* — Download and play 5 community scenarios

---

## Steam Cloud

Use `SteamRemoteStorage` to sync:

| Data | Sync Priority |
|---|---|
| Player settings and keybindings | High |
| Campaign save files | High |
| Custom doctrine templates | Medium |
| AI model preferences | Medium |
| Local replay files | Low (large; opt-in) |

Set per-file quotas conservatively — Steam Cloud has a 1 GB default per-user per-game limit.

---

## Steam Deck

Strategos targets Linux (Steam Deck compatible) in Phase 10. Requirements for **Verified** status:

- All UI navigable with controller or touch (no mouse-required interactions)
- Default text size readable at 1280×800
- No system calls that fail on Linux (verify with Proton if Windows-only APIs used)
- No mandatory third-party launchers or overlays
- Full gamepad input mapping via Steam Input

For a wargame with complex UI, **Playable** rating (controller works but not fully optimised) is acceptable at 1.0; **Verified** can be a post-1.0 goal.

---

## Steam Store Page Checklist

Tracked as #293. Required before any public page or Early Access launch:

- [ ] Capsule art: 460×215 (small), 231×87 (tiny), 616×353 (header)
- [ ] Library capsule: 600×900
- [ ] Hero graphic: 3840×1240 (optional but recommended)
- [ ] Screenshots: minimum 5, showing map, symbols, combat, UI, and AI mode
- [ ] Trailer: minimum 30 seconds; show gameplay, not just cinematics
- [ ] Short description (≤ 300 chars): hook line for search results
- [ ] Long description: feature list, echelon progression, game modes, AI section
- [ ] System requirements: Unity 6 URP minimum/recommended specs
- [ ] Tags: Strategy, Wargame, Turn-Based Strategy, Military, Simulation, Multiplayer, Singleplayer
- [ ] Content warnings: violence (military combat), none expected beyond T/12+

---

## Key Milestone Summary

| Event | Timing | Prerequisite |
|---|---|---|
| Steamworks account + App ID | Phase 0 | — |
| Stub seam + probe (no App ID) | Phase 0 | #301–#305 |
| Remote Play Together enabled | Phase 7 | App ID live |
| Steam Workshop live | Phase 6 completion | App ID + UGC configured |
| Early Access launch (v0.5) | Phases 0–7 complete | Store page approved |
| Steam Achievements live | Phase 10 | EA user feedback on achievement design |
| Steam Cloud live | Phase 10 | Save format stable |
| 1.0 Full Release | Phase 10 complete | All EA feedback addressed |

---

*Last updated: 2026-08-04*
