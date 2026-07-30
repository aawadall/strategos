// MapMeshBuilder.cs
// Turns a MapData heightfield into a mesh.
//
// Lives beside Rendering2D because it reads MapData and nothing else, which is what keeps
// the 2D sheet and the 3D drape from drifting: both derive their geometry from the same
// elevation grid and the same metres-per-cell. The scene plumbing — camera, material,
// render texture — is presentation and lives in Strategos.UI.
//
// The mesh is textured with a rendered 2D sheet (see MapDrapeTexture), so the drape is
// literally the map draped over its own relief. Hillshade is already baked into those
// pixels, which is why no lighting is involved anywhere in this path.

using UnityEngine;
using UnityEngine.Rendering;
using Strategos.Maps;

namespace Strategos.Maps
{
    public struct MapMeshOptions
    {
        /// <summary>
        /// Vertices along the longest side, minus one. 0 means one vertex per cell.
        ///
        /// Decimation matters: a 512-cell map at one vertex per cell is 262 144 vertices,
        /// well past the 65 535 a 16-bit index buffer can address. 192 gives 37 249
        /// vertices and 73 728 triangles, which stays 16-bit and stays inside WebGL1's
        /// limits — and WebGL is one of the build targets.
        /// </summary>
        public int MaxVerticesPerSide;

        /// <summary>
        /// Normals. Off by default: the drape is unlit, so normals are 12 bytes a vertex
        /// that nothing reads. Kept as a flag because a lit variant would need them.
        /// </summary>
        public bool IncludeNormals;

        /// <summary>
        /// Skirt the edges down below the lowest ground.
        ///
        /// With Cull Off in the drape shader you would otherwise see the underside of the
        /// sheet at grazing angles. The skirt copies each edge vertex's UV, so the edge
        /// pixel stretches downward and reads as a cut section through the ground.
        /// </summary>
        public bool SkirtSides;

        public static MapMeshOptions Default => new()
        {
            MaxVerticesPerSide = 192,
            IncludeNormals = false,
            SkirtSides = true,
        };
    }

    public static class MapMeshBuilder
    {
        /// <summary>Vertex count above which a 16-bit index buffer cannot address the mesh.</summary>
        private const int UInt16Limit = 65000;

        /// <summary>Metres the skirt drops below the map's lowest point.</summary>
        private const float SkirtDropMetres = 20f;

        public static Mesh Build(MapData map, MapMeshOptions options)
        {
            if (map == null) return null;

            int w = map.Width, h = map.Height;
            if (w < 2 || h < 2) return null;

            // Segment counts. Note the grid formulation: the cell coordinate is
            //     cx = i * (w - 1) / nx
            // so i == nx lands on w - 1 exactly. A naive `for (x = 0; x < w; x += stride)`
            // loop misses the last column whenever (w - 1) % stride != 0, and the drape then
            // stops short of the map's edge — the same class of off-by-half-a-cell error the
            // 2D viewport warns about.
            int nx = options.MaxVerticesPerSide > 0
                ? Mathf.Min(w - 1, Mathf.Max(1, options.MaxVerticesPerSide))
                : w - 1;
            int ny = options.MaxVerticesPerSide > 0
                ? Mathf.Min(h - 1, Mathf.Max(1, options.MaxVerticesPerSide))
                : h - 1;

            int gw = nx + 1, gh = ny + 1;
            int topCount = gw * gh;

            int skirtCount = options.SkirtSides ? 2 * (gw + gh) : 0;
            int total = topCount + skirtCount;

            var verts = new Vector3[total];
            var uvs = new Vector2[total];

            float minY = map.Header.MinElevation - SkirtDropMetres;

            // --- Top surface ---
            for (int j = 0; j < gh; j++)
            {
                float cy = j * (h - 1) / (float)ny;
                for (int i = 0; i < gw; i++)
                {
                    float cx = i * (w - 1) / (float)nx;
                    int v = j * gw + i;

                    // CellToWorld is (cx * metresPerCell, SampleElevation(cx, cy),
                    // cy * metresPerCell) with the map's SW corner at the world origin and
                    // 1 Unity unit to 1 metre. Reuse it rather than re-deriving.
                    verts[v] = map.CellToWorld(cx, cy);
                    uvs[v] = CellToUv(cx, cy, w, h);
                }
            }

            var tris = new System.Collections.Generic.List<int>(nx * ny * 6 + skirtCount * 3);

            for (int j = 0; j < ny; j++)
            for (int i = 0; i < nx; i++)
            {
                int i0 = j * gw + i;
                int i1 = i0 + 1;
                int i2 = i0 + gw;
                int i3 = i2 + 1;

                // Winding is not defended here: the drape shader is Cull Off. A heightfield
                // with a skirt is viewed from outside, the cost is nil at this triangle
                // count, and it removes the whole "drape invisible from above" bug class.
                tris.Add(i0); tris.Add(i2); tris.Add(i1);
                tris.Add(i1); tris.Add(i2); tris.Add(i3);
            }

            if (options.SkirtSides)
            {
                int next = topCount;

                // South (j = 0) and north (j = gh - 1) run along i; west and east along j.
                next = AddSkirt(verts, uvs, tris, next, gw, gh, minY, Edge.South);
                next = AddSkirt(verts, uvs, tris, next, gw, gh, minY, Edge.North);
                next = AddSkirt(verts, uvs, tris, next, gw, gh, minY, Edge.West);
                _    = AddSkirt(verts, uvs, tris, next, gw, gh, minY, Edge.East);
            }

            var mesh = new Mesh { name = $"MapDrape_{w}x{h}_{nx}" };
            if (total > UInt16Limit) mesh.indexFormat = IndexFormat.UInt32;

            mesh.vertices = verts;
            mesh.uv = uvs;
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateBounds();
            if (options.IncludeNormals) mesh.RecalculateNormals();

            return mesh;
        }

        /// <summary>
        /// Cell coordinate to drape UV.
        ///
        /// The drape texture is rendered with MapViewport.ForWholeMap, whose window is
        /// Rect(-0.5, -0.5, w, h) — a cell coordinate names a sample point, not a square, so
        /// the window starts half a cell before cell 0. For a cell coordinate cx:
        ///
        ///     u = view.CellToPixel(cx, 0).x / view.Width
        ///       = ((cx - (-0.5)) * ppc) / (w * ppc)
        ///       = (cx + 0.5) / w
        ///
        /// The pixels-per-cell cancels, so these UVs are independent of the texture's
        /// resolution. MapData is row-major from the south edge and v = 0 is the texture's
        /// bottom row, so there is NO vertical flip. Getting this half-texel wrong shows up
        /// only in a finished picture, as the grid floating off the drape's edge.
        /// </summary>
        private static Vector2 CellToUv(float cx, float cy, int w, int h) =>
            new((cx + 0.5f) / w, (cy + 0.5f) / h);

        private enum Edge { South, North, West, East }

        /// <summary>
        /// Emits one edge's skirt: a strip of new vertices at <paramref name="minY"/> below
        /// the existing edge vertices, each copying its top neighbour's UV. Returns the next
        /// free vertex index.
        /// </summary>
        private static int AddSkirt(Vector3[] verts, Vector2[] uvs,
            System.Collections.Generic.List<int> tris, int next,
            int gw, int gh, float minY, Edge edge)
        {
            int count = (edge == Edge.South || edge == Edge.North) ? gw : gh;
            int first = next;

            for (int k = 0; k < count; k++)
            {
                int top = edge switch
                {
                    Edge.South => k,
                    Edge.North => (gh - 1) * gw + k,
                    Edge.West  => k * gw,
                    _          => k * gw + (gw - 1),
                };

                var p = verts[top];
                verts[next] = new Vector3(p.x, minY, p.z);
                uvs[next] = uvs[top];
                next++;

                if (k == 0) continue;

                int topPrev = edge switch
                {
                    Edge.South => k - 1,
                    Edge.North => (gh - 1) * gw + (k - 1),
                    Edge.West  => (k - 1) * gw,
                    _          => (k - 1) * gw + (gw - 1),
                };
                int botPrev = first + k - 1;
                int botCur = next - 1;

                tris.Add(topPrev); tris.Add(botPrev); tris.Add(top);
                tris.Add(top);     tris.Add(botPrev); tris.Add(botCur);
            }

            return next;
        }

        /// <summary>
        /// World-space extent of the whole map, for framing a camera. y spans the map's
        /// elevation range; x and z span (w-1) and (h-1) cells, because the last vertex sits
        /// on the last sample point rather than half a cell beyond it.
        /// </summary>
        public static Bounds WorldBounds(MapData map)
        {
            if (map == null) return new Bounds(Vector3.zero, Vector3.one);

            float mpc = map.Header.MetresPerCell;
            float sizeX = (map.Width - 1) * mpc;
            float sizeZ = (map.Height - 1) * mpc;
            float minY = map.Header.MinElevation;
            float maxY = map.Header.MaxElevation;

            var centre = new Vector3(sizeX * 0.5f, (minY + maxY) * 0.5f, sizeZ * 0.5f);
            var size = new Vector3(sizeX, Mathf.Max(1f, maxY - minY), sizeZ);
            return new Bounds(centre, size);
        }
    }
}
