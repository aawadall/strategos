# Strategos — NATO APP-6D Symbol Generator

Custom in-house tool for generating, compositing, and rendering NATO APP-6D military symbols in Unity 6. No external runtime dependencies.

---

## Architecture Overview

The generator is split into three layers:

```
┌───────────────────────────────────────────────────────┐
│  NatoSymbolView  (MonoBehaviour — in-scene display)   │
│  Layered SpriteRenderers, no texture baking required  │
└───────────────────────────┬───────────────────────────┘
                            │ requests
┌───────────────────────────▼───────────────────────────┐
│  NatoSymbolGenerator  (runtime compositor)            │
│  Accepts SIDCCode → composites layers → returns Sprite│
└──────────┬─────────────────────┬──────────────────────┘
           │ parses              │ looks up
┌──────────▼──────────┐ ┌────────▼──────────────────────┐
│  SIDCParser         │ │  NatoSymbolDatabase            │
│  string → SIDCCode  │ │  ScriptableObject              │
└─────────────────────┘ │  frames / icons / echelons     │
                        └───────────────────────────────┘
```

Additionally, a **Unity Editor Window** (`NatoSymbolEditorWindow`) allows:
- Live SIDC preview
- Batch export of sprite atlases
- Symbol catalogue browser

---

## SIDC Format (APP-6D)

A **Symbol Identification Coding (SIDC)** string is 20 characters, structured as follows:

```
Pos  1–2   : Version (10 = APP-6D)
Pos  3     : Standard Identity / Affiliation (0–Z)
Pos  4–5   : Symbol Set (land=10, sea=30, air=01, space=05, subsurface=35, cyberspace=60)
Pos  6     : Status (0=present, 1=anticipated/planned)
Pos  7     : HQ/Task Force/Dummy (0=none, 1=HQ, 2=TF, 3=HQ+TF, 4=Feint/Dummy, ...)
Pos  8–9   : Echelon/Mobility (00=none, 11=team, 12=squad, 13=section, 14=platoon,
             15=company, 16=battalion, 17=regiment, 18=brigade, 21=division,
             22=corps, 23=army, 24=army group, 25=theater/command)
Pos 10–11  : Entity (main unit type code)
Pos 12–13  : Entity Type modifier
Pos 14–15  : Entity Subtype modifier
Pos 16–17  : Sector 1 modifier
Pos 18–19  : Sector 2 modifier
Pos 20     : Reserved
```

Example:
```
10031500001211000000
│ │ │ │ │  │ │
│ │ │ │ │  │ └── Modifiers (0000)
│ │ │ │ │  └──── Entity: Infantry (1211 → 12=infantry, 11=foot)
│ │ │ │ └─────── HQ/TF/Dummy: 0 (none)
│ │ │ └───────── Status: 0 (present)
│ │ └─────────── Symbol Set: 10 (land)
│ └───────────── Affiliation: 3 (friend)
└─────────────── Version: 10 (APP-6D)

Echelon at pos 8–9: 15 = Company
```

---

## Symbol Layers

Every rendered symbol is composed of five ordered layers:

| Layer | Z-Order | Content |
|---|---|---|
| 1. Frame | Bottom | Affiliation shape (rectangle, diamond, circle, etc.) |
| 2. Icon | Above frame | Unit function/type icon |
| 3. Echelon | Above frame | Echelon mark (dots, Roman numerals, Xs) |
| 4. Modifiers | Above frame | HQ line, TF bracket, feint dashes |
| 5. Text Labels | Top | Designation, higher formation, strength |

### Frame Shapes by Affiliation + Dimension

| Affiliation | Land Frame | Air Frame | Sea Frame |
|---|---|---|---|
| Friend | Rectangle | Rounded rect | Diamond |
| Hostile | Diamond | Diamond | Diamond |
| Neutral | Square (rotated 45°) | Square | Square |
| Unknown | Circle | Circle | Circle |
| Pending | Question-mark frame | | |

Fill colours:
- Friend: `#80E0FF` (light blue)
- Hostile: `#FF8080` (light red)
- Neutral: `#AAFFAA` (light green)
- Unknown: `#FFFF80` (light yellow)
- Pending: `#FFFFFF`

### Echelon Marks

| Code | Mark | Rendering |
|---|---|---|
| 11 | Team/Crew | ○ (small circle, or no mark) |
| 12 | Squad | • (one filled dot) |
| 13 | Section | •• (two dots) |
| 14 | Platoon | ••• (three dots) |
| 15 | Company | ••• (three dots, alternate glyph) |
| 16 | Battalion | I |
| 17 | Regiment | II |
| 18 | Brigade | X |
| 21 | Division | XX |
| 22 | Corps | XXX |
| 23 | Army | XXXX |
| 24 | Army Group | XXXXX |
| 25 | Theater | XXXXXX |

Echelon marks are rendered as TextMeshPro text objects centred above the frame.

---

## Rendering Modes

### Mode A — Layered (runtime, default)

`NatoSymbolView` uses stacked `SpriteRenderer` child objects for each layer. No RenderTexture baking; Unity's sprite sorting handles Z-order. Best for in-game unit markers.

**Pros:** Zero allocation after spawn, dynamic (echelon/status change updates instantly).
**Cons:** Multiple draw calls per symbol (mitigated with GPU instancing + sprite atlases).

### Mode B — Baked (editor, persistence)

`NatoSymbolGenerator.Bake()` composites all layers onto a `RenderTexture` and calls `ReadPixels()` to produce a persistent `Texture2D`/`Sprite`. Used by the Editor Window to produce atlas assets.

**Pros:** Single draw call, portable (can be shown anywhere including UI).
**Cons:** Allocates memory; not suitable for very large numbers of unique symbols at runtime.

### Mode C — SVG Export (CI pipeline)

The Editor Window can export any symbol as a standalone SVG file using Unity's **Vector Graphics** package (`com.unity.vectorgraphics`). Used for documentation, the Steam store page, and scenario thumbnails.

---

## Component Sprite Organisation

All component sprites live in `Assets/Art/NatoSymbols/` and are packed into Sprite Atlases per category:

```
Assets/Art/NatoSymbols/
├── Frames/
│   ├── Land_Friend.png
│   ├── Land_Hostile.png
│   ├── Land_Neutral.png
│   ├── Land_Unknown.png
│   ├── Air_Friend.png
│   └── ... (one per Dimension × Affiliation)
├── Icons/
│   ├── Land/
│   │   ├── Infantry.png
│   │   ├── Armor.png
│   │   ├── Artillery.png
│   │   ├── Aviation.png
│   │   ├── Engineer.png
│   │   ├── Signals.png
│   │   ├── Logistics.png
│   │   ├── Medical.png
│   │   ├── Headquarters.png
│   │   ├── AirDefense.png
│   │   ├── Reconnaissance.png
│   │   └── ... (all APP-6D land entities)
│   ├── Air/
│   └── Sea/
├── Echelons/
│   ├── Team.png
│   ├── Squad.png
│   ├── Platoon.png
│   ├── Company.png
│   ├── Battalion.png
│   ├── Regiment.png
│   ├── Brigade.png
│   ├── Division.png
│   ├── Corps.png
│   ├── Army.png
│   ├── ArmyGroup.png
│   └── Theater.png
├── Modifiers/
│   ├── HQ_Line.png
│   ├── TaskForce_Bracket.png
│   ├── Feint_Indicator.png
│   ├── Reinforced.png
│   └── Reduced.png
└── Atlas/
    ├── Frames.spriteatlas
    ├── Icons_Land.spriteatlas
    ├── Icons_Air.spriteatlas
    ├── Icons_Sea.spriteatlas
    ├── Echelons.spriteatlas
    └── Modifiers.spriteatlas
```

All component sprites are **128×128 px** source art, exported as greyscale + alpha (tinted at runtime).

---

## Editor Tool

`NatoSymbolEditorWindow` (menu: **Strategos → NATO Symbol Generator**) provides:

1. **Preview Panel** — Enter any SIDC string, see live composed symbol at 128 px and 256 px
2. **Component Inspector** — Click any layer to highlight which sprite is used
3. **Text Modifier Fields** — Set designation, higher formation, strength labels
4. **Export Button** — Saves the current symbol as a `Sprite` asset into `Assets/Art/NatoSymbols/Exported/`
5. **Batch Generate** — Reads a JSON catalogue file and generates all listed symbols into an atlas
6. **Symbol Catalogue Browser** — Grid view of all symbols in `NatoSymbolDatabase`

---

## Batch Generation (CI)

A `NatoSymbolBatchGenerator` Editor script can be invoked from the command line:

```bash
Unity.exe -batchmode -quit \
  -executeMethod Strategos.Editor.NatoSymbolBatchGenerator.Run \
  -cataloguePath Assets/Data/NatoSymbols/catalogue.json \
  -outputPath Assets/Art/NatoSymbols/Exported/
```

`catalogue.json` is a JSON array of SIDC strings + metadata:

```json
[
  { "sidc": "10031500001211000000", "designation": "1-7 IN", "formation": "3 ID" },
  { "sidc": "10061500003100000000", "designation": "1-34 AR", "formation": "1 AD" }
]
```

---

## Relevant Source Files

| File | Purpose |
|---|---|
| `Assets/Scripts/Core/NatoSymbols/NatoSymbolTypes.cs` | Enums, structs, SIDC data model |
| `Assets/Scripts/Core/NatoSymbols/SIDCParser.cs` | SIDC string → SIDCCode |
| `Assets/Scripts/Core/NatoSymbols/NatoSymbolDatabase.cs` | ScriptableObject component registry |
| `Assets/Scripts/Core/NatoSymbols/NatoSymbolGenerator.cs` | Compositor — layers → Sprite |
| `Assets/Scripts/Core/NatoSymbols/NatoSymbolView.cs` | MonoBehaviour in-scene display |
| `Assets/Editor/NatoSymbolEditorWindow.cs` | Unity Editor preview + export tool |

---

*Last updated: 2026-07-29 | Co-Authored-By: Oz <oz-agent@warp.dev>*
