// NatoSymbolDatabase.cs
// ScriptableObject registry for all NATO APP-6D component sprites.
// Create via: Assets → Create → Strategos → NATO Symbol Database
// Assign all component sprites in the Inspector, then reference from NatoSymbolGenerator.

using System;
using System.Collections.Generic;
using UnityEngine;

namespace Strategos.NatoSymbols
{
    [CreateAssetMenu(menuName = "Strategos/NATO Symbol Database", fileName = "NatoSymbolDatabase")]
    public class NatoSymbolDatabase : ScriptableObject
    {
        // -------------------------------------------------------------------------
        // Frame sprites — one per Affiliation × Dimension combination
        // -------------------------------------------------------------------------
        [Serializable]
        public struct FrameEntry
        {
            public Affiliation      Affiliation;
            public SymbolDimension  Dimension;
            public Sprite           Sprite;
            [Tooltip("Dashed variant used for AnticipatedPlanned status.")]
            public Sprite           PlannedSprite;
        }

        [Header("Frames")]
        [SerializeField] private List<FrameEntry> _frames = new();

        // -------------------------------------------------------------------------
        // Icon sprites — keyed by (Dimension, EntityCode)
        // -------------------------------------------------------------------------
        [Serializable]
        public struct IconEntry
        {
            public SymbolDimension  Dimension;
            [Tooltip("Two-digit entity code from SIDC positions 10–11.")]
            public int              EntityCode;
            public Sprite           Sprite;
        }

        [Header("Unit Icons")]
        [SerializeField] private List<IconEntry> _icons = new();

        // -------------------------------------------------------------------------
        // Echelon sprites — one per Echelon value
        // -------------------------------------------------------------------------
        [Serializable]
        public struct EchelonEntry
        {
            public Echelon  Echelon;
            public Sprite   Sprite;
        }

        [Header("Echelon Marks")]
        [SerializeField] private List<EchelonEntry> _echelons = new();

        // -------------------------------------------------------------------------
        // Modifier sprites
        // -------------------------------------------------------------------------
        [Header("Modifiers")]
        public Sprite HeadquartersLine;
        public Sprite TaskForceBracket;
        public Sprite FeintIndicator;
        public Sprite ReinforcedMark;
        public Sprite ReducedMark;

        // -------------------------------------------------------------------------
        // Fallback / placeholder sprites
        // -------------------------------------------------------------------------
        [Header("Fallbacks")]
        [Tooltip("Shown when a frame sprite is missing for a given affiliation/dimension.")]
        public Sprite FallbackFrame;
        [Tooltip("Shown when an icon sprite is missing for a given entity code.")]
        public Sprite FallbackIcon;

        // -------------------------------------------------------------------------
        // Lookup API
        // -------------------------------------------------------------------------

        /// <summary>Returns the frame sprite for the given affiliation and dimension.</summary>
        public Sprite GetFrame(Affiliation affiliation, SymbolDimension dimension, bool planned = false)
        {
            foreach (var entry in _frames)
            {
                if (entry.Affiliation == affiliation && entry.Dimension == dimension)
                    return (planned && entry.PlannedSprite != null) ? entry.PlannedSprite : entry.Sprite;
            }

            // Fall back: try Land dimension if the exact dimension is missing.
            if (dimension != SymbolDimension.Land)
                return GetFrame(affiliation, SymbolDimension.Land, planned);

            Debug.LogWarning($"[NatoSymbolDatabase] No frame for Affiliation={affiliation}, Dimension={dimension}. Using fallback.");
            return FallbackFrame;
        }

        /// <summary>Returns the icon sprite for the given dimension and entity code.</summary>
        public Sprite GetIcon(SymbolDimension dimension, int entityCode)
        {
            foreach (var entry in _icons)
            {
                if (entry.Dimension == dimension && entry.EntityCode == entityCode)
                    return entry.Sprite;
            }

            // Try entity code 0 (unknown) as fallback within the same dimension.
            if (entityCode != 0)
                return GetIcon(dimension, 0);

            Debug.LogWarning($"[NatoSymbolDatabase] No icon for Dimension={dimension}, EntityCode={entityCode}. Using fallback.");
            return FallbackIcon;
        }

        /// <summary>Returns the echelon mark sprite for the given echelon level.</summary>
        public Sprite GetEchelon(Echelon echelon)
        {
            if (echelon == Echelon.None) return null;

            foreach (var entry in _echelons)
            {
                if (entry.Echelon == echelon)
                    return entry.Sprite;
            }

            Debug.LogWarning($"[NatoSymbolDatabase] No echelon sprite for {echelon}.");
            return null;
        }

        /// <summary>Resolves all sprites for a given SIDCCode in one call.</summary>
        public SymbolSpriteSet Resolve(SIDCCode sidc)
        {
            return new SymbolSpriteSet
            {
                Frame       = GetFrame(sidc.Affiliation, sidc.Dimension, sidc.IsPlanned),
                Icon        = GetIcon(sidc.Dimension, sidc.EntityCode),
                Echelon     = GetEchelon(sidc.Echelon),
                HQLine      = sidc.IsHeadquarters ? HeadquartersLine : null,
                TFBracket   = sidc.IsTaskForce    ? TaskForceBracket : null,
                Feint       = sidc.IsFeintDummy   ? FeintIndicator   : null,
                Reinforced  = sidc.StrengthModifier == StrengthModifier.Reinforced ? ReinforcedMark : null,
                Reduced     = sidc.StrengthModifier == StrengthModifier.Reduced    ? ReducedMark    : null,
                FrameTint   = AffiliationColour.ForAffiliation(sidc.Affiliation),
            };
        }

#if UNITY_EDITOR
        /// <summary>Editor-only: returns all frame entries (for the Editor Window catalogue browser).</summary>
        public IReadOnlyList<FrameEntry> AllFrames   => _frames;
        public IReadOnlyList<IconEntry>  AllIcons    => _icons;
        public IReadOnlyList<EchelonEntry> AllEchelons => _echelons;
#endif
    }

    // -------------------------------------------------------------------------
    // SymbolSpriteSet — resolved sprite bundle for one symbol
    // -------------------------------------------------------------------------
    public struct SymbolSpriteSet
    {
        public Sprite   Frame;
        public Sprite   Icon;
        public Sprite   Echelon;
        public Sprite   HQLine;
        public Sprite   TFBracket;
        public Sprite   Feint;
        public Sprite   Reinforced;
        public Sprite   Reduced;
        public Color    FrameTint;
    }
}
