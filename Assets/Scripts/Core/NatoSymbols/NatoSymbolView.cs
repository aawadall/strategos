// NatoSymbolView.cs
// MonoBehaviour that displays a NATO APP-6D symbol in-scene using layered SpriteRenderers.
// Prefer NatoSymbolDatabase sprites when assigned; otherwise Compose → bake procedural layers.

using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace Strategos.NatoSymbols
{
    [DisallowMultipleComponent]
    public class NatoSymbolView : MonoBehaviour
    {
        [Header("Dependencies")]
        [SerializeField] private NatoSymbolDatabase _database;

        [Header("Layer Renderers (auto-created if null)")]
        [SerializeField] private SpriteRenderer _frameRenderer;
        [SerializeField] private SpriteRenderer _iconRenderer;
        [SerializeField] private SpriteRenderer _echelonRenderer;
        [SerializeField] private SpriteRenderer _hqLineRenderer;
        [SerializeField] private SpriteRenderer _tfBracketRenderer;
        [SerializeField] private SpriteRenderer _feintRenderer;
        [SerializeField] private SpriteRenderer _strengthModRenderer;

        [Header("Text Labels (auto-created if null)")]
        [SerializeField] private TMP_Text _designationLabel;
        [SerializeField] private TMP_Text _higherFormationLabel;
        [SerializeField] private TMP_Text _strengthLabel;

        [Header("Scale")]
        [Tooltip("World-space size of the symbol in units.")]
        [SerializeField] private float _symbolSize = 1f;

        [Header("Preview (Inspector)")]
        [SerializeField] private string _previewSIDC = "10031000151211000000";
        [SerializeField] private string _previewDesignation;
        [SerializeField] private string _previewFormation;

        private SIDCCode _currentCode;
        private readonly List<Texture2D> _ownedTextures = new();

        private void Awake()
        {
            EnsureLayers();
        }

        private void OnDestroy()
        {
            ClearOwnedTextures();
        }

        private void OnValidate()
        {
            if (Application.isPlaying) return;
            if (string.IsNullOrEmpty(_previewSIDC)) return;
            if (SIDCParser.TryParse(_previewSIDC, out var code))
            {
                code.Designation     = _previewDesignation;
                code.HigherFormation = _previewFormation;
                SetSymbol(code);
            }
        }

        /// <summary>Updates the displayed symbol to match the given SIDCCode.</summary>
        public void SetSymbol(SIDCCode code)
        {
            _currentCode = code;
            EnsureLayers();
            ClearOwnedTextures();

            if (_database != null)
            {
                ApplyDatabaseLayers(code);
            }
            else
            {
                ApplyComposedLayers(code);
            }

            UpdateTextLabels(code);
            transform.localScale = Vector3.one * _symbolSize;
        }

        public void SetSymbol(string sidc, string designation = "", string higherFormation = "")
        {
            if (!SIDCParser.TryParse(sidc, out var code)) return;
            code.Designation     = designation;
            code.HigherFormation = higherFormation;
            SetSymbol(code);
        }

        public SIDCCode CurrentCode => _currentCode;

        private void ApplyDatabaseLayers(SIDCCode code)
        {
            var sprites = _database.Resolve(code);
            ApplyLayer(_frameRenderer,     sprites.Frame,      sprites.FrameTint);
            ApplyLayer(_iconRenderer,      sprites.Icon,       Color.white);
            ApplyLayer(_echelonRenderer,   sprites.Echelon,    Color.black);
            ApplyLayer(_hqLineRenderer,    sprites.HQLine,     Color.black);
            ApplyLayer(_tfBracketRenderer, sprites.TFBracket,  Color.black);
            ApplyLayer(_feintRenderer,     sprites.Feint,      Color.black);
            ApplyLayer(_strengthModRenderer,
                code.StrengthModifier == StrengthModifier.Reinforced ? sprites.Reinforced
              : code.StrengthModifier == StrengthModifier.Reduced    ? sprites.Reduced
              : null, Color.black);
        }

        private void ApplyComposedLayers(SIDCCode code)
        {
            // Procedural path: bake full composition onto the frame renderer.
            var symbol = NatoSymbolComposer.Compose(code, (NatoSymbolDatabase)null);
            var baked  = NatoSymbolBaker.Bake(symbol, 256);
            if (baked != null)
                _ownedTextures.Add(baked.texture);

            ApplyLayer(_frameRenderer, baked, Color.white);
            ApplyLayer(_iconRenderer, null, Color.white);
            ApplyLayer(_echelonRenderer, null, Color.white);
            ApplyLayer(_hqLineRenderer, null, Color.white);
            ApplyLayer(_tfBracketRenderer, null, Color.white);
            ApplyLayer(_feintRenderer, null, Color.white);
            ApplyLayer(_strengthModRenderer, null, Color.white);
        }

        private static void ApplyLayer(SpriteRenderer renderer, Sprite sprite, Color tint)
        {
            if (renderer == null) return;
            renderer.sprite  = sprite;
            renderer.color   = sprite != null ? tint : Color.clear;
            renderer.enabled = sprite != null;
        }

        private void UpdateTextLabels(SIDCCode code)
        {
            var text = SymbolTextAmplifiers.FromCode(code);
            SetLabel(_designationLabel,     text.Designation);
            SetLabel(_higherFormationLabel, text.HigherFormation);
            SetLabel(_strengthLabel,        text.StrengthDisplay);
        }

        private static void SetLabel(TMP_Text label, string text)
        {
            if (label == null) return;
            label.text    = text ?? string.Empty;
            label.enabled = !string.IsNullOrEmpty(text);
        }

        private void ClearOwnedTextures()
        {
            foreach (var t in _ownedTextures)
                if (t != null) Destroy(t);
            _ownedTextures.Clear();
        }

        private void EnsureLayers()
        {
            _frameRenderer       = EnsureRenderer(_frameRenderer,       "Layer_Frame",      sortOrder: 0,  zOffset: 0.00f);
            _iconRenderer        = EnsureRenderer(_iconRenderer,        "Layer_Icon",       sortOrder: 1,  zOffset: -0.01f);
            _echelonRenderer     = EnsureRenderer(_echelonRenderer,     "Layer_Echelon",    sortOrder: 2,  zOffset: -0.02f);
            _hqLineRenderer      = EnsureRenderer(_hqLineRenderer,      "Layer_HQLine",     sortOrder: 3,  zOffset: -0.03f);
            _tfBracketRenderer   = EnsureRenderer(_tfBracketRenderer,   "Layer_TFBracket",  sortOrder: 3,  zOffset: -0.03f);
            _feintRenderer       = EnsureRenderer(_feintRenderer,       "Layer_Feint",      sortOrder: 3,  zOffset: -0.03f);
            _strengthModRenderer = EnsureRenderer(_strengthModRenderer, "Layer_Strength",   sortOrder: 3,  zOffset: -0.03f);
        }

        private SpriteRenderer EnsureRenderer(SpriteRenderer existing, string childName, int sortOrder, float zOffset)
        {
            if (existing != null) return existing;

            var go = new GameObject(childName);
            go.transform.SetParent(transform, false);
            go.transform.localPosition = new Vector3(0, 0, zOffset);

            var sr = go.AddComponent<SpriteRenderer>();
            sr.sortingOrder = sortOrder;
            return sr;
        }
    }
}
