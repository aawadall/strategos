// MapMeshProbe.cs
// Verifies MapMeshBuilder numerically, in batch mode, with no graphics device.
//
// A rendered check cannot run under -nographics, and a picture of a terrain mesh is a poor
// test anyway: a half-texel UV error, a missing last column or a flipped elevation axis all
// produce a plausible-looking hill. So assert the numbers first, then look at the drape.
//
// Menu:  Strategos > Probe Map Mesh
// Batch: -executeMethod Strategos.Editor.MapMeshProbe.Run

#if UNITY_EDITOR
using System.Text;
using UnityEditor;
using UnityEngine;
using Strategos.Maps;

namespace Strategos.Editor
{
    public static class MapMeshProbe
    {
        private const int Seed = 20260729;

        private static readonly int[] Sizes = { 128, 256, 512 };
        private static readonly int[] Detail = { 96, 192, 384, 0 };   // 0 = one vertex per cell

        [MenuItem("Strategos/Probe Map Mesh")]
        public static void Run()
        {
            var log = new StringBuilder();
            int failures = 0;

            foreach (int cells in Sizes)
            {
                var settings = new MapGenerationSettings
                {
                    Name = $"PROBE{cells}",
                    Seed = Seed,
                    Width = cells,
                    Height = cells,
                    MetresPerCell = 25f,
                    Profile = ReliefProfile.Hills,
                    // Erosion dominates generation cost and none of the assertions below
                    // depend on it; the probe is about mesh geometry, not terrain realism.
                    EnableErosion = false,
                };

                var map = MapGenerator.Generate(settings);
                var header = map.Header;

                log.AppendLine($"--- {cells}x{cells}  elevation {header.MinElevation:0.0} .. " +
                               $"{header.MaxElevation:0.0} m  ({header.MetresPerCell} m/cell)");

                foreach (int detail in Detail)
                {
                    var opts = MapMeshOptions.Default;
                    opts.MaxVerticesPerSide = detail;

                    var mesh = MapMeshBuilder.Build(map, opts);
                    if (mesh == null)
                    {
                        log.AppendLine($"    detail {detail,4}: NULL MESH");
                        failures++;
                        continue;
                    }

                    failures += Check(map, mesh, opts, detail, log);
                    Object.DestroyImmediate(mesh);
                }
            }

            log.AppendLine(failures == 0
                ? "PROBE PASSED"
                : $"PROBE FAILED with {failures} problem(s)");

            if (failures == 0) Debug.Log("[MapMeshProbe]\n" + log);
            else Debug.LogError("[MapMeshProbe]\n" + log);
        }

        private static int Check(MapData map, Mesh mesh, MapMeshOptions opts, int detail,
            StringBuilder log)
        {
            int bad = 0;
            int w = map.Width, h = map.Height;
            float mpc = map.Header.MetresPerCell;

            int nx = detail > 0 ? Mathf.Min(w - 1, Mathf.Max(1, detail)) : w - 1;
            int ny = detail > 0 ? Mathf.Min(h - 1, Mathf.Max(1, detail)) : h - 1;
            int expectedTop = (nx + 1) * (ny + 1);
            int expectedSkirt = opts.SkirtSides ? 2 * ((nx + 1) + (ny + 1)) : 0;
            int expectedVerts = expectedTop + expectedSkirt;

            var verts = mesh.vertices;
            var uvs = mesh.uv;

            log.AppendLine($"    detail {detail,4}: {mesh.vertexCount,7} verts  " +
                           $"{mesh.triangles.Length / 3,7} tris  index {mesh.indexFormat}");

            if (mesh.vertexCount != expectedVerts)
            {
                log.AppendLine($"      FAIL vertex count: expected {expectedVerts}");
                bad++;
            }

            // A 16-bit buffer cannot address past 65535, so anything larger must have been
            // promoted. This is the check that catches a decimation default being raised
            // without the index format following.
            if (mesh.vertexCount > 65535 &&
                mesh.indexFormat != UnityEngine.Rendering.IndexFormat.UInt32)
            {
                log.AppendLine("      FAIL index format: >65535 verts needs UInt32");
                bad++;
            }

            // --- Extent. Catches the last-row/column decimation bug: the grid formulation
            // must land its final vertex exactly on cell w-1, not short of it. ---
            var b = mesh.bounds;
            float expectMaxX = (w - 1) * mpc;
            float expectMaxZ = (h - 1) * mpc;

            if (!Near(b.min.x, 0f, 0.01f) || !Near(b.min.z, 0f, 0.01f))
            {
                log.AppendLine($"      FAIL origin: min.xz = ({b.min.x:0.###}, {b.min.z:0.###}), expected (0, 0)");
                bad++;
            }
            if (!Near(b.max.x, expectMaxX, 0.01f) || !Near(b.max.z, expectMaxZ, 0.01f))
            {
                log.AppendLine($"      FAIL extent: max.xz = ({b.max.x:0.#}, {b.max.z:0.#}), " +
                               $"expected ({expectMaxX:0.#}, {expectMaxZ:0.#})");
                bad++;
            }

            // --- Elevation axis. max.y must reach the map's own maximum when undecimated;
            // decimation samples bilinearly so peaks flatten slightly, hence the tolerance. ---
            float tolY = detail == 0 ? 0.01f : (map.Header.MaxElevation - map.Header.MinElevation) * 0.25f + 1f;
            if (b.max.y > map.Header.MaxElevation + 0.01f)
            {
                log.AppendLine($"      FAIL max.y {b.max.y:0.##} above map max {map.Header.MaxElevation:0.##}");
                bad++;
            }
            if (map.Header.MaxElevation - b.max.y > tolY)
            {
                log.AppendLine($"      FAIL max.y {b.max.y:0.##} far below map max {map.Header.MaxElevation:0.##}");
                bad++;
            }

            // The skirt is the only thing that should sit below the map's minimum, and by a
            // fixed 20 m. This checks the skirt exists and did not invert.
            if (opts.SkirtSides)
            {
                float expectMinY = map.Header.MinElevation - 20f;
                if (!Near(b.min.y, expectMinY, 0.01f))
                {
                    log.AppendLine($"      FAIL skirt floor {b.min.y:0.##}, expected {expectMinY:0.##}");
                    bad++;
                }
            }

            // --- UV corners. The derivation is u = (cx + 0.5) / w, so the first vertex sits
            // half a texel in and the last half a texel short of 1. Getting this wrong is
            // invisible except as the grid floating off the drape's edge in a finished
            // picture. ---
            var uv0 = uvs[0];
            var uvLast = uvs[expectedTop - 1];
            if (!Near(uv0.x, 0.5f / w, 1e-5f) || !Near(uv0.y, 0.5f / h, 1e-5f))
            {
                log.AppendLine($"      FAIL uv[0] = {uv0}, expected ({0.5f / w:0.######}, {0.5f / h:0.######})");
                bad++;
            }
            if (!Near(uvLast.x, (w - 0.5f) / w, 1e-5f) || !Near(uvLast.y, (h - 0.5f) / h, 1e-5f))
            {
                log.AppendLine($"      FAIL uv[last] = {uvLast}, expected " +
                               $"({(w - 0.5f) / w:0.######}, {(h - 0.5f) / h:0.######})");
                bad++;
            }

            // --- No NaN. A NaN vertex makes the whole mesh vanish with no error. ---
            for (int i = 0; i < verts.Length; i++)
            {
                var v = verts[i];
                if (float.IsNaN(v.x) || float.IsNaN(v.y) || float.IsNaN(v.z))
                {
                    log.AppendLine($"      FAIL NaN vertex at {i}");
                    bad++;
                    break;
                }
            }

            // Normals are off by default; assert that, so turning them on is a deliberate act.
            if (!opts.IncludeNormals && mesh.normals.Length != 0)
            {
                log.AppendLine("      FAIL normals present but IncludeNormals is false");
                bad++;
            }

            return bad;
        }

        private static bool Near(float a, float b, float tol) => Mathf.Abs(a - b) <= tol;
    }
}
#endif
