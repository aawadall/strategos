# Strategos — NATO APP-6D Symbol Generator

Custom in-house tool for generating, compositing, and rendering NATO APP-6D military symbols in Unity 6. No external runtime dependencies.

Construction follows APP-6(D) Chapter 1 / Table 3-1: **Factory** creates the framed base; **Decorators** add icon, sector modifiers, and amplifiers.

---

## Architecture Overview

```
SIDC string
    │
    ▼
SIDCParser  ──► SIDCCode
    │
    ▼
NatoSymbolComposer
    │  1. SymbolFactory.CreateBase()     → Frame + Fill
    │  2. IconDecorator                  → Field A icon
    │  3. SectorModifierDecorator        → Sector 1 / 2
    │  4. AmplifierDecorator             → Fields B/D/S + text T/M/F
    ▼
INatoSymbol  (ordered SymbolLayerDraw list + SymbolTextAmplifiers)
    │
    ├── NatoSymbolView      (Mode A — layered SpriteRenderers / baked procedural)
    ├── NatoSymbolBaker     (Mode B — flatten to Sprite)
    └── NatoSymbolGenerator (Mode B — GPU blit when database sprites exist)
```

Editor Window (`NatoSymbolEditorWindow`, menu **Strategos → NATO Symbol Generator**): live SIDC preview, PNG export, batch from catalogue JSON. Works with procedural composition when no database is assigned.

---

## SIDC Format (APP-6D Annex A)

A SIDC is **two sets of ten digits** (20 required). An optional third ten (30 total) may carry originator extensions and is accepted but ignored.

```
Digits 1–2   : Version (10 = APP-6D)
Digits 3–4   : Context (0=Reality) + Standard Identity (Table A-2)
Digits 5–6   : Symbol Set (10=Land Unit — Table A-4)
Digit  7     : Status (Table A-6)
Digit  8     : HQ / Task Force / Dummy (Table A-7)
Digits 9–10  : Amplifier — echelon / mobility (Table A-8)
Digits 11–12 : Entity
Digits 13–14 : Entity Type
Digits 15–16 : Entity Subtype
Digits 17–18 : Sector 1 Modifier
Digits 19–20 : Sector 2 Modifier
```

### Standard Identity (digit 4 — Table A-2)

| Code | Identity |
|---|---|
| 0 | Pending |
| 1 | Unknown |
| 2 | Assumed Friend |
| 3 | Friend |
| 4 | Neutral |
| 5 | Suspect / Joker |
| 6 | Hostile / Faker |

Identity groups (Table A-3) drive frame shape: Unknown, Friend, Neutral, Hostile.

### HQ / Task Force / Dummy (digit 8 — Table A-7)

| Code | Meaning |
|---|---|
| 0 | None |
| 1 | Feint/Dummy |
| 2 | Headquarters |
| 3 | Feint + HQ |
| 4 | Task Force |
| 5 | Feint + TF |
| 6 | TF + HQ |
| 7 | Feint + TF + HQ |

### Canonical example

Friend Land Unit Infantry Company:

```
10031000151211000000
│││││ ││ │ │ │
│││││ ││ │ │ └── Sector mods 0000
│││││ ││ │ └──── Entity subtype 00
│││││ ││ └────── Entity type 11
│││││ └───────── Entity 12 (infantry)
││││└─────────── Echelon 15 (company)
│││└──────────── HQ/TF 0, Status 0
││└───────────── Symbol Set 10 (Land Unit)
│└────────────── Context 0 + Identity 3 (Friend)
└─────────────── Version 10
```

---

## Symbol Layers (APP-6D §1.2)

Every icon-based symbol is composed of:

| Layer | APP-6(D) term | Location |
|---|---|---|
| 1. Frame + Fill | Frame / Fill | Shape by identity group × dimension; colour Table 1-8 |
| 2. Icon | Field A | Bounding octagon main / full-frame / full-octagon |
| 3. Modifiers | Sector 1 / 2 | Octagon sectors (.3L / .4L / .3L) — max one each |
| 4. Amplifiers | Fields B, D, S, … | Outside frame (echelon, HQ staff, TF bracket, feint) |
| 5. Text | Fields T, M, F, … | Designation, higher formation, strength |

### Frame shapes (Land Unit — Table 1-1)

| Identity group | Land frame |
|---|---|
| Friend | Rectangle |
| Hostile | Diamond |
| Neutral | Square |
| Unknown / Pending | Ellipse |

Line style: solid (present), dashed (planned), dotted (uncertain identity — Assumed Friend / Suspect / Pending).

Fill colours (Table 1-8 computer-generated): Friend `#80E0FF`, Hostile `#FF8080`, Neutral `#AAFFAA`, Unknown `#FFFF80`.

### Bounding octagon (Figure 1-15)

Vertical sectors relative to octagon height `L`: top `.3L` (sector 1), mid `.4L` (main icon), bottom `.3L` (sector 2). Encoded in `SymbolLayout`.

### Echelon marks (Field B / Table A-8)

| Code | Mark |
|---|---|
| 11 | Team ○ |
| 12 | Squad • |
| 13 | Section •• |
| 14 | Platoon ••• |
| 15 | Company ••• |
| 16 | Battalion I |
| 17 | Regiment II |
| 18 | Brigade X |
| 21–26 | Division … Command (XX …) |

---

## Factory + Decorator API

```csharp
// Preferred entry point
INatoSymbol symbol = NatoSymbolComposer.Compose("10031000151211000000");
INatoSymbol symbol = NatoSymbolComposer.Compose(sidcCode, database);

// Legacy single-sprite convenience (Compose → Bake)
Sprite sprite = SymbolFactory.Create().GetSymbolSprite(sidcCode, 256);
```

| Type | Role |
|---|---|
| `SymbolFactory` / `ProceduralSymbolFactory` | Factory Method — base frame + fill |
| `IconDecorator` | Table 3-1 Step 2 — land main / full-frame icons |
| `SectorModifierDecorator` | Step 3 — sector 1/2 modifiers |
| `AmplifierDecorator` | Echelon, HQ, TF, feint + text T/M/F |
| `NatoSymbolBaker` | Flatten `INatoSymbol` → `Sprite` |
| `NatoSymbolView` | In-scene display (database layers or baked procedural) |

---

## Rendering Modes

### Mode A — Layered (runtime)

`NatoSymbolView` uses stacked `SpriteRenderer` children when a `NatoSymbolDatabase` is assigned. Without a database, the composer bakes a procedural sprite onto the frame layer.

### Mode B — Baked

`NatoSymbolBaker.Bake()` (CPU procedural) or `NatoSymbolGenerator.Bake()` (GPU blit of database sprites). Used by the Editor Window and demo grid.

### Mode C — SVG Export (planned)

Vector Graphics package export for documentation / store art — not implemented in this slice.

---

## Component Sprite Organisation (database path)

When art is ready, component sprites live under `Assets/Art/NatoSymbols/` and are registered on `NatoSymbolDatabase`. Same composer order; only the frame factory / resolvers switch to sprites.

```
Assets/Art/NatoSymbols/
├── Frames/
├── Icons/Land/
├── Echelons/
├── Modifiers/
└── Atlas/
```

---

## Editor Tool

`NatoSymbolEditorWindow` (menu: **Strategos → NATO Symbol Generator**):

1. Live SIDC preview (procedural or database)
2. Text amplifier fields (designation, formation, strength)
3. PNG export
4. Batch generate from JSON catalogue

```json
[
  { "sidc": "10031000151211000000", "designation": "1-7 IN", "formation": "3 ID" },
  { "sidc": "10061000151600000000", "designation": "1-34 AR", "formation": "1 AD" }
]
```

---

## Relevant Source Files

| File | Purpose |
|---|---|
| `NatoSymbolTypes.cs` | Enums / SIDCCode (Annex A) |
| `SIDCParser.cs` | 20/30-digit SIDC → SIDCCode |
| `INatoSymbol.cs` | Layer draw model + `SymbolLayout` |
| `NatoSymbolDecorator.cs` | Base symbol + decorator base |
| `SymbolFactory.cs` | Factory Method (procedural frame) |
| `IconDecorator.cs` | Land unit icons |
| `SectorModifierDecorator.cs` | Sector 1/2 modifiers |
| `AmplifierDecorator.cs` | Graphic + text amplifiers |
| `NatoSymbolComposer.cs` | Table 3-1 orchestration |
| `NatoSymbolBaker.cs` | Compose → Sprite |
| `NatoSymbolDatabase.cs` | ScriptableObject sprite registry |
| `NatoSymbolGenerator.cs` | GPU bake (database) / procedural fallback |
| `NatoSymbolView.cs` | In-scene display |
| `NatoSymbolEditorWindow.cs` | Editor preview + export |

---

*Last updated: 2026-07-29*
