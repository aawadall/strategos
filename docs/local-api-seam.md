// local-api-seam.md
// Read before touching IGameStore, IContentSource, or IPlayerIdentity (#355).

# Local / API seam (#355)

Interfaces that can later be API-backed. **Not** the choice of which local embedded
database holds player data — that is [#66](https://github.com/aawadall/strategos/issues/66).

| Issue | Question |
|---|---|
| **#66** | Which *local* store for player data (SQLite vs LiteDB vs …) — storage **choice** |
| **#355** | Interfaces that can later be remote — **seam** |

Precedent: [`ISidePolicy`](../Assets/Scripts/Core/Direction/ISidePolicy.cs) (#100) — a seam
only works if implementations never need a concrete `Simulation`. Same bar here.

## What shipped

| Seam | Core contract | First impl |
|---|---|---|
| Saves | `IGameStore` + `StoreResult` (`SaveAsync` / `LoadAsync` / …) | `FileGameStore` |
| Content | `IContentSource<T>` | `ResourcesScenarioSource`, `ResourcesCampaignChainSource`, `ResourcesDoctrinePackSource` |
| Identity | `IPlayerIdentity` | `LocalAnonymousIdentity` |

Static `ScenarioIO.Load` / `CampaignChainIO.Load` / `TtpIO.Load` thin-wrap the Resources
sources. Steam-backed identity belongs to [#288](https://github.com/aawadall/strategos/issues/288),
not this epic.

## Out of scope

Server, Workshop, OAuth UI, Steamworks package, SQLite/#66 choice.

## Probe

`Strategos.Editor.GameStoreSeamProbe.Run` — async store shape, content sources, anonymous identity.
