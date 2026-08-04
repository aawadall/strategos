# Map invariants

The 2D sheet and the 3D drape. Same failure mode throughout: the output
comes out drawable but wrong. **Read before touching `Core/Maps`.**

[CLAUDE.md](../CLAUDE.md) is the index.

---

## Map rendering invariants

Same failure mode: the sheet comes out drawable but wrong.

- **Stroke widths are authored at 3 px per cell** (`MapViewport.ReferencePixelsPerCell`)
  and `StrokeScale` is *not* floored at 1. It was, and an overview drowned: a stream held
  at its authored 2 px is 50 m of ground width at 1 px per cell, so the whole drainage
  network rendered as ribbons and the map read as flooded. Floor the **final pixel width**
  at 1 instead, so features thin to hairlines rather than vanish.
- **Graphic control measures are not `MapData` (#161–#163).** Checkpoints, phase lines and
  boundaries live on `Scenario.ControlMeasures` and paint via `ControlMeasureDrawer` after
  `RenderPixels` (PLAY's `MapSheetCard.Render` `afterPixels` hook). Do not add them as
  `MapLineKind` / `MapPoiKind` — those enums are generator terrain.
- **Point marks generalise by dropping, not by shrinking** (`DetailZoom`, a **private**
  const at `MapRasterizer.cs:641` — promote it if a new view needs the same rule).
  A line can thin; a ford's circle has a minimum legible size, so below the threshold
  fords, bridges and spot heights are simply not drawn. Settlements always are.
- **The viewport starts at cell −0.5, not 0.** A cell coordinate names a sample point, not
  a square. Anchor the window at 0 and a 512-cell map renders 511 px and every feature
  sits half a cell off.
- **`MapLabelPlacer` is first-come, so call order is priority order.** Place cities before
  villages, and everything before the grid. An edge label's inset must clear
  `Padding + EdgeMargin` or it is silently rejected as overhanging — grid designators
  vanished entirely at an inset of 3.
- **Landcover pattern spacing is in pixels, not cells.** A stipple is a property of the
  printed surface and should look the same at every zoom; lock it to cells and it
  coarsens as you zoom in.
- **The kilometre grid wins wherever it fits** (`MapGridOverlay.AutoSquareSpacing`).
  Choosing the finest legible interval gives a 500 m grid at working zoom, where the two
  principal digits step by 5 and stop reading as the km figure a report would quote.
- **Hydrology keeps two flood surfaces and they are not interchangeable.**
  `FillDepressions` returns *routing* (epsilon-raised, for D8) and *standing* (epsilon-free,
  the true water surface). Lake depth must come from `standing`: epsilon accumulates along
  gently-sloped paths, so at 512 cells and up a valley floor rises past the 0.75 m lake
  threshold and the whole drainage network is classified as lake. The standing surface also
  seeds the border at −∞, because a map is a window cut from a landscape and its edges are
  not a rim — seeded at their own height, any interior ground below the lowest edge cell
  fills to the edge.

---

---

## 3D drape invariants

The drape is a heightfield mesh textured with a rendered 2D sheet — the map draped over
its own relief. Verify with `Strategos → Probe Map Mesh` (see below) *before* looking at
the picture: a half-texel UV error, a missing last column and a flipped elevation axis all
produce a plausible-looking hill.

- **UVs are derived, not guessed: `u = (cx + 0.5) / w`.** The drape is rendered with
  `MapViewport.ForWholeMap`, whose window starts at cell −0.5, so the half-cell offset is
  required and the pixels-per-cell cancels out (making the UVs resolution-independent).
  `MapData` is row-major from the south edge and `v = 0` is the texture's bottom row, so
  there is **no vertical flip**. Getting this wrong shows up only as the grid floating off
  the drape's edge in a finished render.
- **Decimate on a grid, never by stride.** `cx = i * (w - 1) / nx` makes `i == nx` land on
  `w - 1` exactly. A `for (x = 0; x < w; x += stride)` loop misses the last column whenever
  `(w - 1) % stride != 0`, and the drape then stops short of the map's edge.
- **512 cells at one vertex per cell is 262 144 vertices**, past what a 16-bit index buffer
  addresses. `IndexFormat.UInt32` is set only above 65 000 so the default (192 a side,
  ~38 000 verts) stays 16-bit and WebGL-safe — WebGL is a build target.
- **The drape shader is `Cull Off` on purpose.** A skirted heightfield is viewed from
  outside, culling costs nothing at this triangle count, and it removes the whole class of
  bug where a winding mistake makes the drape invisible from above. Do not tidy it to
  `Cull Back` without checking the mesh winding first.
- **The drape needs its own texture because `MapRasterizer.Render` has no mip-maps**
  (`mipChain: false` at `MapRasterizer.cs:116`). That is right for a flat sheet and wrong
  in perspective, where the far half of the map minifies hard and shimmers.
  `MapDrapeTexture` goes through the public `RenderPixels` and builds a mipmapped,
  trilinear, anisotropic texture instead.
- **Load the shader with `Resources.Load`, never `Shader.Find`.** `Find` only resolves
  shaders used by a scene or listed in `m_AlwaysIncludedShaders`; neither is true here, so
  it works in the editor and returns null in a player, where the symptom is a magenta
  drape.
- **The drape lives on layer 8 (`MapDrape`) and both cameras are masked.** The drape camera
  renders only that layer; the scene camera is masked out of it by `SceneBootstrapper` and
  again by `AppShell` at runtime, so a hand-edited scene cannot reintroduce a second
  terrain render that nothing can see. The drape camera also has **no `AudioListener`** — a
  second one warns every frame.
- **A fresh `RenderTexture` holds uninitialised garbage**, so render once immediately after
  allocating. Release before reallocating, and quantise the size (16 px) or a window drag
  reallocates every frame.
- **The 2D and 3D preview images cannot be the same `RawImage`.** The 2D sheet must be
  aspect-cropped via `uvRect`; the 3D one must not be, because its target is allocated at
  the frame's exact aspect. Two siblings keep each invariant structural.

```powershell
# Menu: Strategos > Probe Map Mesh  — works under -nographics
& "C:\Program Files\Unity\Hub\Editor\6000.0.75f1\Editor\Unity.exe" `
    -batchmode -quit -nographics -projectPath . `
    -executeMethod Strategos.Editor.MapMeshProbe.Run -logFile probe.log
```

It asserts vertex and triangle counts, index-format promotion, that the extent lands
exactly on `(w-1, h-1)` cells, that the skirt floor is 20 m under the map minimum, that
the UV corners are half a texel in, and that no vertex is NaN.

---
