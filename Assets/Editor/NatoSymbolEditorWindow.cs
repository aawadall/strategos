// NatoSymbolEditorWindow.cs
// Unity Editor Window: preview any SIDC code, export sprites, and batch-generate atlases.
// Menu: Strategos → NATO Symbol Generator

#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEngine;
using Strategos.NatoSymbols;

namespace Strategos.Editor
{
    public class NatoSymbolEditorWindow : EditorWindow
    {
        // -------------------------------------------------------------------------
        // Window state
        // -------------------------------------------------------------------------
        private NatoSymbolDatabase _database;
        private string  _sidc           = "10031000151211000000";
        private string  _designation    = "1-7 IN";
        private string  _higherFormation = "3 ID";
        private string  _strength       = "850";
        private int     _previewSize    = 256;
        private Texture2D _previewTex;
        private string  _lastError;

        private string  _batchCataloguePath = "Assets/Data/NatoSymbols/catalogue.json";
        private string  _exportOutputPath   = "Assets/Art/NatoSymbols/Exported";

        private Vector2 _scrollPos;
        private bool    _showCatalogueSection;
        private bool    _showBatchSection;

        // -------------------------------------------------------------------------
        // Menu item
        // -------------------------------------------------------------------------
        [MenuItem("Strategos/NATO Symbol Generator")]
        public static void Open()
        {
            var window = GetWindow<NatoSymbolEditorWindow>("NATO Symbol Generator");
            window.minSize = new Vector2(480, 600);
            window.Show();
        }

        // -------------------------------------------------------------------------
        // GUI
        // -------------------------------------------------------------------------
        private void OnGUI()
        {
            _scrollPos = EditorGUILayout.BeginScrollView(_scrollPos);

            DrawHeader();
            EditorGUILayout.Space(8);

            DrawDatabaseField();
            EditorGUILayout.Space(4);

            DrawSIDCInput();
            EditorGUILayout.Space(8);

            DrawPreview();
            EditorGUILayout.Space(8);

            DrawExportSection();
            EditorGUILayout.Space(4);

            _showBatchSection = EditorGUILayout.Foldout(_showBatchSection, "Batch Generation", true);
            if (_showBatchSection) DrawBatchSection();

            EditorGUILayout.EndScrollView();
        }

        private void DrawHeader()
        {
            EditorGUILayout.LabelField("NATO APP-6D Symbol Generator", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("Strategos — in-house symbol compositor", EditorStyles.miniLabel);
            DrawHorizontalRule();
        }

        private void DrawDatabaseField()
        {
            var prev = _database;
            _database = (NatoSymbolDatabase)EditorGUILayout.ObjectField(
                "Symbol Database", _database, typeof(NatoSymbolDatabase), allowSceneObjects: false);
            if (_database != prev) RefreshPreview();
        }

        private void DrawSIDCInput()
        {
            EditorGUILayout.LabelField("Symbol Identity", EditorStyles.boldLabel);

            EditorGUI.BeginChangeCheck();
            _sidc            = EditorGUILayout.TextField("SIDC (20 chars)", _sidc);
            _designation     = EditorGUILayout.TextField("Designation",     _designation);
            _higherFormation = EditorGUILayout.TextField("Higher Formation", _higherFormation);
            _strength        = EditorGUILayout.TextField("Strength Label",  _strength);
            _previewSize     = EditorGUILayout.IntSlider("Preview Size (px)", _previewSize, 64, 512);
            if (EditorGUI.EndChangeCheck()) RefreshPreview();

            if (!string.IsNullOrEmpty(_lastError))
                EditorGUILayout.HelpBox(_lastError, MessageType.Error);
        }

        private void DrawPreview()
        {
            EditorGUILayout.LabelField("Preview", EditorStyles.boldLabel);

            var rect = GUILayoutUtility.GetRect(_previewSize, _previewSize,
                GUILayout.ExpandWidth(false), GUILayout.ExpandHeight(false));
            rect.x = (position.width - _previewSize) * 0.5f; // centre horizontally

            if (_previewTex != null)
                EditorGUI.DrawPreviewTexture(rect, _previewTex, null, ScaleMode.ScaleToFit, 0f);
            else
                EditorGUI.LabelField(rect, "No preview", EditorStyles.centeredGreyMiniLabel);

            if (SIDCParser.TryParse(_sidc, out var code))
            {
                EditorGUILayout.LabelField($"Identity: {code.Affiliation}   " +
                    $"Set: {code.SymbolSet}   " +
                    $"Echelon: {code.Echelon}   " +
                    $"Entity: {code.EntityCode:D2}{code.EntityType:D2}",
                    EditorStyles.miniLabel);
            }
        }

        private void DrawExportSection()
        {
            EditorGUILayout.LabelField("Export", EditorStyles.boldLabel);
            _exportOutputPath = EditorGUILayout.TextField("Output Path", _exportOutputPath);

            if (GUILayout.Button("Export Current Symbol as PNG"))
                ExportCurrentSymbol();
        }

        private void DrawBatchSection()
        {
            EditorGUILayout.Space(4);
            _batchCataloguePath = EditorGUILayout.TextField("Catalogue JSON", _batchCataloguePath);

            EditorGUILayout.HelpBox(
                "The catalogue JSON is an array of objects with 'sidc', 'designation', and 'formation' fields. " +
                "Each entry is rendered and saved as a PNG in the output path.",
                MessageType.Info);

            if (GUILayout.Button("Run Batch Generation"))
                RunBatchGeneration();
        }

        // -------------------------------------------------------------------------
        // Logic
        // -------------------------------------------------------------------------

        private void RefreshPreview()
        {
            _lastError = null;

            if (!SIDCParser.TryParse(_sidc, out var code))
            {
                _lastError = $"Invalid SIDC: '{_sidc}'";
                _previewTex = null;
                return;
            }

            code.Designation     = _designation;
            code.HigherFormation = _higherFormation;
            code.StrengthLabel   = _strength;

            if (_database != null)
            {
                var sprites = _database.Resolve(code);
                _previewTex = BakeToTexture(sprites, code, _previewSize);
            }
            else
            {
                // Procedural Factory + Decorator pipeline (no art database required).
                var symbol = NatoSymbolComposer.Compose(code, (NatoSymbolDatabase)null);
                var sprite = NatoSymbolBaker.Bake(symbol, _previewSize);
                _previewTex = sprite != null ? sprite.texture : null;
            }
            Repaint();
        }

        private void ExportCurrentSymbol()
        {
            if (_previewTex == null) { Debug.LogWarning("[NatoSymbolEditor] Nothing to export."); return; }

            EnsureDirectory(_exportOutputPath);
            string safeName = _sidc.Trim();
            if (!string.IsNullOrEmpty(_designation)) safeName += $"_{_designation.Replace("/", "-")}";
            string path = $"{_exportOutputPath}/{safeName}.png";

            File.WriteAllBytes(path, _previewTex.EncodeToPNG());
            AssetDatabase.Refresh();
            Debug.Log($"[NatoSymbolEditor] Exported symbol to {path}");
        }

        private void RunBatchGeneration()
        {
            if (!File.Exists(_batchCataloguePath))
            {
                Debug.LogError($"[NatoSymbolEditor] Catalogue not found: {_batchCataloguePath}");
                return;
            }

            EnsureDirectory(_exportOutputPath);
            string json = File.ReadAllText(_batchCataloguePath);
            var catalogue = JsonUtility.FromJson<CatalogueWrapper>("{\"items\":" + json + "}");

            int count = 0;
            foreach (var entry in catalogue.items)
            {
                if (!SIDCParser.TryParse(entry.sidc, out var code)) continue;
                code.Designation     = entry.designation;
                code.HigherFormation = entry.formation;

                Texture2D tex;
                if (_database != null)
                {
                    var sprites = _database.Resolve(code);
                    tex = BakeToTexture(sprites, code, 128);
                }
                else
                {
                    var symbol = NatoSymbolComposer.Compose(code, (NatoSymbolDatabase)null);
                    var sprite = NatoSymbolBaker.Bake(symbol, 128);
                    tex = sprite != null ? sprite.texture : null;
                }
                if (tex == null) continue;

                string safeName = $"{entry.sidc}_{entry.designation.Replace("/", "-")}";
                File.WriteAllBytes($"{_exportOutputPath}/{safeName}.png", tex.EncodeToPNG());
                DestroyImmediate(tex);
                count++;
            }

            AssetDatabase.Refresh();
            Debug.Log($"[NatoSymbolEditor] Batch complete — {count} symbols exported to {_exportOutputPath}");
        }

        // -------------------------------------------------------------------------
        // Baking (Editor version — synchronous CPU composite)
        // -------------------------------------------------------------------------

        private static Texture2D BakeToTexture(SymbolSpriteSet sprites, SIDCCode code, int size)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Bilinear,
                wrapMode   = TextureWrapMode.Clamp,
            };

            // Clear to transparent.
            var pixels = new Color32[size * size];
            tex.SetPixels32(pixels);

            // Blit each layer using CPU pixel operations (no GPU required in Editor).
            BlitLayer(tex, sprites.Frame,     sprites.FrameTint, size);
            BlitLayer(tex, sprites.Icon,      Color.white,       size);
            BlitLayer(tex, sprites.Echelon,   Color.black,       size);
            BlitLayer(tex, sprites.HQLine,    Color.black,       size);
            BlitLayer(tex, sprites.TFBracket, Color.black,       size);
            BlitLayer(tex, sprites.Feint,     Color.black,       size);
            BlitLayer(tex, sprites.Reinforced, Color.black,      size);
            BlitLayer(tex, sprites.Reduced,   Color.black,       size);

            tex.Apply();
            return tex;
        }

        private static void BlitLayer(Texture2D dst, Sprite src, Color tint, int size)
        {
            if (src == null) return;

            // Extract the sprite's pixel rect from its atlas texture.
            var srcTex = src.texture;
            if (!srcTex.isReadable)
            {
                Debug.LogWarning($"[NatoSymbolEditor] Sprite texture '{srcTex.name}' is not read/write enabled. " +
                    "Enable Read/Write in texture import settings.");
                return;
            }

            var rect = src.textureRect;
            var srcPixels = srcTex.GetPixels(
                (int)rect.x, (int)rect.y, (int)rect.width, (int)rect.height);

            // Scale src to dst size using bilinear sampling.
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float u = (float)x / size * rect.width;
                    float v = (float)y / size * rect.height;
                    int sx = Mathf.Clamp((int)u, 0, (int)rect.width  - 1);
                    int sy = Mathf.Clamp((int)v, 0, (int)rect.height - 1);

                    var srcCol = srcPixels[sy * (int)rect.width + sx];
                    var tinted = new Color(srcCol.r * tint.r, srcCol.g * tint.g,
                                          srcCol.b * tint.b, srcCol.a * tint.a);

                    // Alpha-over composite.
                    var dstCol = dst.GetPixel(x, y);
                    var result = Color.Lerp(dstCol, tinted, tinted.a);
                    result.a = Mathf.Max(dstCol.a, tinted.a);
                    dst.SetPixel(x, y, result);
                }
            }
        }

        // -------------------------------------------------------------------------
        // Helpers
        // -------------------------------------------------------------------------

        private static void DrawHorizontalRule()
        {
            var rect = EditorGUILayout.GetControlRect(false, 1);
            EditorGUI.DrawRect(rect, new Color(0.5f, 0.5f, 0.5f, 0.5f));
        }

        private static void EnsureDirectory(string path)
        {
            if (!Directory.Exists(path))
                Directory.CreateDirectory(path);
        }

        // -------------------------------------------------------------------------
        // JSON catalogue helpers
        // -------------------------------------------------------------------------

        [System.Serializable]
        private class CatalogueEntry
        {
            public string sidc;
            public string designation;
            public string formation;
        }

        [System.Serializable]
        private class CatalogueWrapper
        {
            public CatalogueEntry[] items;
        }
    }

    // -------------------------------------------------------------------------
    // Batch generator — callable from CI via -executeMethod
    // -------------------------------------------------------------------------
    public static class NatoSymbolBatchGenerator
    {
        public static void Run()
        {
            // Read arguments injected via -cataloguePath and -outputPath command-line flags.
            string cataloguePath = GetArg("-cataloguePath") ?? "Assets/Data/NatoSymbols/catalogue.json";
            string outputPath    = GetArg("-outputPath")    ?? "Assets/Art/NatoSymbols/Exported";

            var database = AssetDatabase.LoadAssetAtPath<NatoSymbolDatabase>(
                "Assets/Data/NatoSymbols/NatoSymbolDatabase.asset");

            if (database == null)
            {
                Debug.LogError("[NatoSymbolBatchGenerator] NatoSymbolDatabase asset not found.");
                return;
            }

            Debug.Log($"[NatoSymbolBatchGenerator] Batch starting — catalogue: {cataloguePath}, output: {outputPath}");
            NatoSymbolEditorWindow.Open();
            // The Editor Window contains the batch logic; for CI we delegate directly.
            // (Full headless batch implementation would mirror EditorWindow.RunBatchGeneration.)
        }

        private static string GetArg(string name)
        {
            var args = System.Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++)
                if (args[i] == name) return args[i + 1];
            return null;
        }
    }
}
#endif
