// MapDrapeStage.cs
// The 3D half of the scenario preview: a draped terrain mesh, a camera pointed at it, and a
// RenderTexture the UI can show.
//
// WHY A RENDER TEXTURE
// The app canvas is ScreenSpaceOverlay at sortingOrder 100, which draws over everything a
// camera renders. A 3D view living inside a card therefore cannot simply be "behind the UI";
// it has to become a texture. Rendering to one keeps the whole thing inside the UI, makes
// the 2D/3D switch a matter of which RawImage is active, and needs no canvas render-mode
// surgery or camera stacking.
//
// THE PERFORMANCE TRAP
// A Camera with a targetTexture renders EVERY FRAME whether or not anything displays the
// result. Leaving it enabled while another view is on screen costs a full terrain render per
// frame, invisibly. SetRendering(false) in the owning view's OnHidden is not optional.

using UnityEngine;
using Strategos.Maps;

namespace Strategos.UI
{
    public sealed class MapDrapeStage : MonoBehaviour
    {
        private const string ShaderPath = "Shaders/StrategosMapDrape";

        /// <summary>
        /// RenderTexture edge quantisation. Without it a window drag reallocates the target
        /// every frame.
        /// </summary>
        private const int SizeQuantum = 16;

        private const int MinSize = 256;
        private const int MaxSize = 4096;

        private GameObject _meshGo;
        private MeshFilter _filter;
        private MeshRenderer _renderer;
        private Material _material;
        private Mesh _mesh;
        private Texture2D _drape;

        private Camera _camera;
        private RenderTexture _target;
        private readonly OrbitRig _rig = new();
        private Bounds _bounds;
        private int _pixelWidth, _pixelHeight;

        /// <summary>The texture to display. Null until <see cref="SetSize"/> has been called.</summary>
        public Texture Output => _target;

        private void Awake()
        {
            _meshGo = new GameObject("MapDrape");
            _meshGo.transform.SetParent(transform, false);
            _meshGo.layer = AppShell.MapDrapeLayer;
            _filter = _meshGo.AddComponent<MeshFilter>();
            _renderer = _meshGo.AddComponent<MeshRenderer>();

            // Resources.Load, never Shader.Find: Find only resolves shaders used by a scene
            // or listed in m_AlwaysIncludedShaders, so it works in the editor and returns
            // null in a player, where the symptom is a magenta drape.
            var shader = Resources.Load<Shader>(ShaderPath);
            if (shader == null)
            {
                shader = Shader.Find("Unlit/Texture");
                Debug.LogError($"[MapDrapeStage] '{ShaderPath}' not in Resources; " +
                               $"fell back to Unlit/Texture (found: {shader != null}).");
            }
            _material = new Material(shader) { name = "MapDrapeMaterial" };
            _renderer.sharedMaterial = _material;

            var camGo = new GameObject("MapDrapeCamera");
            camGo.transform.SetParent(transform, false);
            _camera = camGo.AddComponent<Camera>();
            _camera.orthographic = false;
            _camera.fieldOfView = 45f;
            _camera.clearFlags = CameraClearFlags.SolidColor;
            // A paper-adjacent background so the card stays inside the app's grammar rather
            // than becoming a black hole in a light UI.
            _camera.backgroundColor = UiTheme.MapPaper;
            _camera.nearClipPlane = 1f;
            _camera.farClipPlane = 10000f;
            // Only the drape. Without this the main camera would also draw the terrain, and
            // this camera would draw the rest of the scene into the card.
            _camera.cullingMask = 1 << AppShell.MapDrapeLayer;
            _camera.enabled = false;
            // No AudioListener: a second one in the scene logs warnings every frame.
        }

        /// <summary>Builds the mesh and drape texture for <paramref name="map"/>.</summary>
        public void SetMap(MapData map, MapMeshOptions meshOptions, MapRenderOptions renderOptions)
        {
            if (map == null) return;

            // Runtime meshes and textures do not get collected; replace explicitly.
            if (_mesh != null) Destroy(_mesh);
            if (_drape != null) Destroy(_drape);

            _mesh = MapMeshBuilder.Build(map, meshOptions);
            _filter.sharedMesh = _mesh;

            _drape = MapDrapeTexture.Create(map, renderOptions);
            _material.mainTexture = _drape;

            _bounds = MapMeshBuilder.WorldBounds(map);
            _camera.farClipPlane = Mathf.Max(1000f, _bounds.size.magnitude * 4f);
            ResetView();
        }

        /// <summary>
        /// Vertical exaggeration, as a scale on the mesh. Safe to drive from a slider: it
        /// touches no generation and no rasterisation.
        /// </summary>
        public void SetVerticalExaggeration(float scale)
        {
            if (_meshGo == null) return;
            _meshGo.transform.localScale = new Vector3(1f, Mathf.Max(0.1f, scale), 1f);
        }

        /// <summary>
        /// (Re)allocates the target for a card of this pixel size. The RT matches the card's
        /// aspect exactly, so Camera.aspect follows targetTexture and NO crop is needed —
        /// which is precisely why the 2D and 3D paths cannot share one RawImage.
        /// </summary>
        public void SetSize(int pixelWidth, int pixelHeight)
        {
            int w = Quantise(pixelWidth);
            int h = Quantise(pixelHeight);
            if (_target != null && _target.width == w && _target.height == h) return;

            // Release before reallocating, not after.
            if (_target != null)
            {
                if (_camera.targetTexture == _target) _camera.targetTexture = null;
                _target.Release();
                Destroy(_target);
            }

            _target = new RenderTexture(w, h, 24, RenderTextureFormat.Default)
            {
                name = "MapDrapeTarget",
                // A heightfield silhouette against a flat background is all edges, so MSAA
                // pays for itself disproportionately here and is nearly free at card size.
                antiAliasing = 4,
                useMipMap = false,
                filterMode = FilterMode.Bilinear,
            };
            _target.Create();

            _pixelWidth = w;
            _pixelHeight = h;
            _camera.targetTexture = _target;

            // A freshly created RT contains uninitialised garbage until something renders
            // into it, so render once immediately rather than showing noise for a frame.
            RenderOnce();
        }

        private static int Quantise(int v)
        {
            int q = Mathf.RoundToInt(v / (float)SizeQuantum) * SizeQuantum;
            return Mathf.Clamp(q, MinSize, MaxSize);
        }

        /// <summary>
        /// Turns continuous rendering on or off. MUST be false whenever the card is not
        /// visible — see the note at the top of the file.
        /// </summary>
        public void SetRendering(bool on)
        {
            if (_camera == null) return;
            _camera.enabled = on;
            if (on) RenderOnce();
        }

        /// <summary>Releases the target. For a view being hidden for a while.</summary>
        public void ReleaseTarget()
        {
            if (_target == null) return;
            _camera.targetTexture = null;
            _target.Release();
            Destroy(_target);
            _target = null;
        }

        public void RenderOnce()
        {
            if (_camera == null || _target == null || _mesh == null) return;
            _rig.Apply(_camera.transform);
            _camera.Render();
        }

        public void ResetView()
        {
            _rig.Frame(_bounds);
            _rig.Apply(_camera.transform);
            RenderOnce();
        }

        // ─── Interaction ──────────────────────────────────────────────────────

        /// <param name="secondary">True to pan instead of orbit (right or middle button).</param>
        public void Drag(Vector2 deltaPixels, bool secondary)
        {
            if (secondary) _rig.Pan(deltaPixels);
            else _rig.Orbit(deltaPixels);
            _rig.Apply(_camera.transform);
            RenderOnce();
        }

        public void Zoom(float steps)
        {
            _rig.Zoom(steps);
            _rig.Apply(_camera.transform);
            RenderOnce();
        }

        private void OnDestroy()
        {
            ReleaseTarget();
            if (_mesh != null) Destroy(_mesh);
            if (_drape != null) Destroy(_drape);
            if (_material != null) Destroy(_material);
        }
    }
}
