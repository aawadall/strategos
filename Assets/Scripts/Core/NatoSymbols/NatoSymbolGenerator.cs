// NatoSymbolGenerator.cs
// Composites NATO APP-6D symbol layers onto a RenderTexture and returns a baked Sprite.
// Use NatoSymbolView for efficient in-scene real-time display (no baking).
// Use this class in the Editor Window and CI batch generator.

using UnityEngine;
using UnityEngine.Rendering;

namespace Strategos.NatoSymbols
{
    /// <summary>
    /// Bakes a fully composed NATO APP-6D symbol sprite from a SIDCCode.
    /// Rendering is GPU-side via CommandBuffer + Graphics.Blit; safe to call from Editor code.
    /// </summary>
    public class NatoSymbolGenerator : MonoBehaviour
    {
        [Header("Dependencies")]
        [SerializeField] private NatoSymbolDatabase _database;

        [Header("Bake Settings")]
        [SerializeField] private int _defaultSize = 128;
        [SerializeField] private Material _blitMaterial; // Optional: tint/composite shader

        // Singleton accessor (optional — Editor Window can also instantiate directly).
        private static NatoSymbolGenerator _instance;
        public  static NatoSymbolGenerator Instance => _instance;

        private void Awake()
        {
            if (_instance != null && _instance != this) { Destroy(gameObject); return; }
            _instance = this;
            DontDestroyOnLoad(gameObject);
        }

        // -------------------------------------------------------------------------
        // Public API
        // -------------------------------------------------------------------------

        /// <summary>
        /// Returns a baked Sprite for the given SIDC string.
        /// Parses the SIDC, resolves sprites, and composites them at <paramref name="size"/> px.
        /// </summary>
        public Sprite Generate(string sidc, int size = 0)
        {
            if (!SIDCParser.TryParse(sidc, out SIDCCode code))
                return null;
            return Generate(code, size);
        }

        /// <summary>
        /// Returns a baked Sprite for the given SIDCCode.
        /// </summary>
        public Sprite Generate(SIDCCode code, int size = 0)
        {
            int px = size > 0 ? size : _defaultSize;

            if (_database != null)
            {
                var sprites = _database.Resolve(code);
                return Bake(sprites, code, px);
            }

            // Procedural fallback via Factory + Decorator composer.
            var symbol = NatoSymbolComposer.Compose(code, (NatoSymbolDatabase)null);
            return NatoSymbolBaker.Bake(symbol, px);
        }

        // -------------------------------------------------------------------------
        // Baking pipeline
        // -------------------------------------------------------------------------

        private Sprite Bake(SymbolSpriteSet sprites, SIDCCode code, int size)
        {
            var rt = new RenderTexture(size, size, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB)
            {
                filterMode = FilterMode.Bilinear,
                antiAliasing = 1,
            };

            var cb = new CommandBuffer { name = $"NatoSymbol_Bake_{code.Raw}" };

            cb.SetRenderTarget(rt);
            cb.ClearRenderTarget(true, true, Color.clear);

            // Layer 1: Frame (tinted by affiliation colour)
            BlitSprite(cb, sprites.Frame, sprites.FrameTint, size);

            // Layer 2: Main icon (neutral white — inherits no tint)
            BlitSprite(cb, sprites.Icon, Color.white, size);

            // Layer 3: Echelon mark
            BlitSprite(cb, sprites.Echelon, Color.black, size);

            // Layer 4: Structural modifiers
            BlitSprite(cb, sprites.HQLine,    Color.black, size);
            BlitSprite(cb, sprites.TFBracket, Color.black, size);
            BlitSprite(cb, sprites.Feint,     Color.black, size);
            BlitSprite(cb, sprites.Reinforced, Color.black, size);
            BlitSprite(cb, sprites.Reduced,    Color.black, size);

            Graphics.ExecuteCommandBuffer(cb);
            cb.Release();

            // Read back pixels from GPU → CPU
            RenderTexture.active = rt;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false, false)
            {
                name        = $"NatoSymbol_{code.Raw}",
                filterMode  = FilterMode.Bilinear,
                wrapMode    = TextureWrapMode.Clamp,
            };
            tex.ReadPixels(new Rect(0, 0, size, size), 0, 0, false);
            tex.Apply(false, false);

            RenderTexture.active = null;
            rt.Release();
            Object.Destroy(rt);

            var sprite = Sprite.Create(
                tex,
                new Rect(0, 0, size, size),
                new Vector2(0.5f, 0.5f),
                pixelsPerUnit: size);

            sprite.name = $"NatoSymbol_{code.Raw}_{code.Designation}";
            return sprite;
        }

        private void BlitSprite(CommandBuffer cb, Sprite sprite, Color tint, int size)
        {
            if (sprite == null) return;

            // Create a temporary material to apply the tint via shader property.
            var mat = _blitMaterial != null
                ? new Material(_blitMaterial)
                : new Material(Shader.Find("Sprites/Default"));

            mat.color = tint;
            cb.DrawMesh(
                BuildQuadMesh(sprite, size),
                Matrix4x4.identity,
                mat,
                submeshIndex: 0);

            Object.Destroy(mat);
        }

        /// <summary>Builds a unit quad mesh matching the sprite's UV rect.</summary>
        private static Mesh BuildQuadMesh(Sprite sprite, int targetSize)
        {
            // Normalise sprite UV to 0–1 within the atlas texture.
            var uvRect = new Rect(
                sprite.rect.x      / sprite.texture.width,
                sprite.rect.y      / sprite.texture.height,
                sprite.rect.width  / sprite.texture.width,
                sprite.rect.height / sprite.texture.height);

            var mesh = new Mesh { name = "SymbolQuad" };
            mesh.SetVertices(new[]
            {
                new Vector3(-0.5f, -0.5f, 0),
                new Vector3( 0.5f, -0.5f, 0),
                new Vector3(-0.5f,  0.5f, 0),
                new Vector3( 0.5f,  0.5f, 0),
            });
            mesh.SetUVs(0, new[]
            {
                new Vector2(uvRect.xMin, uvRect.yMin),
                new Vector2(uvRect.xMax, uvRect.yMin),
                new Vector2(uvRect.xMin, uvRect.yMax),
                new Vector2(uvRect.xMax, uvRect.yMax),
            });
            mesh.SetTriangles(new[] { 0, 2, 1, 2, 3, 1 }, 0);
            mesh.UploadMeshData(true);
            return mesh;
        }
    }
}
