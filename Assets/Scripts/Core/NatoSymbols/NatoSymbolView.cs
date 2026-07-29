// NatoSymbolView.cs
// MonoBehaviour that displays a NATO APP-6D symbol in-scene using layered SpriteRenderers.
// No texture baking — each layer is a separate child SpriteRenderer sorted by order.
// Attach to any unit marker GameObject. Call SetSymbol() to update.

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
        [SerializeField] private string _previewSIDC = "10031500001211000000";
        [SerializeField] private string _previewDesignation;
        [SerializeField] private string _previewFormation;

        private SIDCCode _currentCode;

        // -------------------------------------------------------------------------
        // Lifecycle
        // -------------------------------------------------------------------------

        private void Awake()
        {
            EnsureLayers();
        }

        private void OnValidate()
        {
            // Live preview in the Editor Inspector.
            if (Application.isPlaying) return;
            if (!string.IsNullOrEmpty(_previewSIDC) && _database != null)
            {
                if (SIDCParser.TryParse(_previewSIDC, out var code))
                {
                    code.Designation    = _previewDesignation;
                    code.HigherFormation = _previewFormation;
                    SetSymbol(code);
                }
            }
        }

        // -------------------------------------------------------------------------
        // Public API
        // -------------------------------------------------------------------------

        /// <summary>Updates the displayed symbol to match the given SIDCCode.</summary>
        public void SetSymbol(SIDCCode code)
        {
            if (_database == null)
            {
                Debug.LogError("[NatoSymbolView] NatoSymbolDatabase is not assigned.", this);
                return;
            }

            _currentCode = code;
            EnsureLayers();

            var sprites = _database.Resolve(code);
            ApplyLayer(_frameRenderer,    sprites.Frame,      sprites.FrameTint);
            ApplyLayer(_iconRenderer,     sprites.Icon,       Color.white);
            ApplyLayer(_echelonRenderer,  sprites.Echelon,    Color.black);
            ApplyLayer(_hqLineRenderer,   sprites.HQLine,     Color.black);
            ApplyLayer(_tfBracketRenderer, sprites.TFBracket, Color.black);
            ApplyLayer(_feintRenderer,    sprites.Feint,      Color.black);
            ApplyLayer(_strengthModRenderer,
                code.StrengthModifier == StrengthModifier.Reinforced ? sprites.Reinforced
              : code.StrengthModifier == StrengthModifier.Reduced    ? sprites.Reduced
              : null, Color.black);

            UpdateTextLabels(code);

            // Scale the root transform so the symbol occupies _symbolSize world units.
            transform.localScale = Vector3.one * _symbolSize;
        }

        /// <summary>Convenience overload accepting a raw SIDC string.</summary>
        public void SetSymbol(string sidc, string designation = "", string higherFormation = "")
        {
            if (!SIDCParser.TryParse(sidc, out var code)) return;
            code.Designation     = designation;
            code.HigherFormation = higherFormation;
            SetSymbol(code);
        }

        /// <summary>Returns the SIDCCode currently displayed.</summary>
        public SIDCCode CurrentCode => _currentCode;

        // -------------------------------------------------------------------------
        // Private helpers
        // -------------------------------------------------------------------------

        private static void ApplyLayer(SpriteRenderer renderer, Sprite sprite, Color tint)
        {
            if (renderer == null) return;
            renderer.sprite  = sprite;
            renderer.color   = sprite != null ? tint : Color.clear;
            renderer.enabled = sprite != null;
        }

        private void UpdateTextLabels(SIDCCode code)
        {
            SetLabel(_designationLabel,     code.Designation);
            SetLabel(_higherFormationLabel, code.HigherFormation);

            // Strength label: number + optional +/- modifier
            var strength = code.StrengthLabel ?? string.Empty;
            if (code.StrengthModifier == StrengthModifier.Reinforced) strength += "+";
            if (code.StrengthModifier == StrengthModifier.Reduced)    strength += "-";
            SetLabel(_strengthLabel, strength);
        }

        private static void SetLabel(TMP_Text label, string text)
        {
            if (label == null) return;
            label.text    = text ?? string.Empty;
            label.enabled = !string.IsNullOrEmpty(text);
        }

        /// <summary>Creates any missing child renderers at their canonical Z-offsets.</summary>
        private void EnsureLayers()
        {
            _frameRenderer      = EnsureRenderer(_frameRenderer,      "Layer_Frame",      sortOrder: 0,  zOffset: 0.00f);
            _iconRenderer       = EnsureRenderer(_iconRenderer,       "Layer_Icon",       sortOrder: 1,  zOffset: -0.01f);
            _echelonRenderer    = EnsureRenderer(_echelonRenderer,    "Layer_Echelon",    sortOrder: 2,  zOffset: -0.02f);
            _hqLineRenderer     = EnsureRenderer(_hqLineRenderer,     "Layer_HQLine",     sortOrder: 3,  zOffset: -0.03f);
            _tfBracketRenderer  = EnsureRenderer(_tfBracketRenderer,  "Layer_TFBracket",  sortOrder: 3,  zOffset: -0.03f);
            _feintRenderer      = EnsureRenderer(_feintRenderer,      "Layer_Feint",      sortOrder: 3,  zOffset: -0.03f);
            _strengthModRenderer = EnsureRenderer(_strengthModRenderer, "Layer_Strength", sortOrder: 3,  zOffset: -0.03f);
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
