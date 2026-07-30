// SymbolFactory.cs
// Factory Method for APP-6D base framed symbols (Table 3-1 Step 1).
// Decorators add icon / sector modifiers / amplifiers via NatoSymbolComposer.
// Legacy GetSymbolSprite bakes Compose → single Sprite for demo/editor.

using System.Collections.Generic;
using UnityEngine;

namespace Strategos.NatoSymbols
{
    /// <summary>
    /// Abstract factory for APP-6D base symbols (frame + fill).
    /// </summary>
    public abstract class SymbolFactory
    {
        /// <summary>
        /// Returns the most appropriate factory for the current context.
        /// Database-backed factory is reserved for when sprites are populated.
        /// </summary>
        public static SymbolFactory Create(NatoSymbolDatabase database = null)
        {
            // TODO: return new DatabaseSymbolFactory(database) when database is populated
            return new ProceduralSymbolFactory();
        }

        /// <summary>Creates the framed + filled base symbol (no icon/modifiers yet).</summary>
        public abstract INatoSymbol CreateBase(SIDCCode code);

        /// <summary>
        /// Legacy convenience: Compose full symbol and bake to a single Sprite.
        /// </summary>
        public virtual Sprite GetSymbolSprite(SIDCCode code, int size = 256)
        {
            var symbol = NatoSymbolComposer.Compose(code, this);
            return NatoSymbolBaker.Bake(symbol, size);
        }

        public virtual void ClearCache() { }
    }

    /// <summary>
    /// Procedural Land Unit frame factory (APP-6D Table 1-1 / 1-2 / 1-8).
    /// </summary>
    public sealed class ProceduralSymbolFactory : SymbolFactory
    {
        private readonly Dictionary<string, Sprite> _bakeCache = new();

        public override INatoSymbol CreateBase(SIDCCode code)
        {
            Color32 fill = (Color32)AffiliationColour.ForAffiliation(code.Affiliation);
            Color32 bdr  = new Color32(0, 0, 0, 255);
            FrameLineStyle style = ResolveLineStyle(code);

            SymbolLayerDraw frame = SymbolLayerDraw.FromProcedural(
                SymbolLayer.Frame,
                "Frame",
                (buf, sz, sc) => DrawLandFrame(buf, sz, sc, code.IdentityGroup, fill, bdr, style),
                sortOrder: 0);

            return new BaseNatoSymbol(code, frame);
        }

        public override Sprite GetSymbolSprite(SIDCCode code, int size = 256)
        {
            // Every field that affects the bake must be in the key. StrengthLabel is
            // included because the strength percentage now draws a combat-power bar.
            string key = $"{code.Raw}|{code.Designation}|{code.HigherFormation}|" +
                         $"{code.StrengthLabel}|{(int)code.StrengthModifier}|{size}";
            if (_bakeCache.TryGetValue(key, out var hit) && hit != null)
                return hit;

            var sprite = base.GetSymbolSprite(code, size);
            _bakeCache[key] = sprite;
            return sprite;
        }

        public override void ClearCache()
        {
            foreach (var s in _bakeCache.Values)
                if (s != null) Object.Destroy(s.texture);
            _bakeCache.Clear();
        }

        internal static FrameLineStyle ResolveLineStyle(SIDCCode code)
        {
            if (code.HasUncertainIdentity)
                return FrameLineStyle.Dotted;
            if (code.IsPlanned)
                return FrameLineStyle.Dashed;
            return FrameLineStyle.Solid;
        }

        internal static void DrawLandFrame(Color32[] buf, int sz, float sc,
            IdentityGroup group, Color32 fill, Color32 bdr, FrameLineStyle style)
        {
            int l  = SymbolLayout.Scale(SymbolLayout.FrameLeft,   sc);
            int r  = SymbolLayout.Scale(SymbolLayout.FrameRight,  sc);
            int b  = SymbolLayout.Scale(SymbolLayout.FrameBottom, sc);
            int t  = SymbolLayout.Scale(SymbolLayout.FrameTop,    sc);
            int cx = SymbolLayout.Scale(SymbolLayout.FrameCX,     sc);
            int cy = SymbolLayout.Scale(SymbolLayout.FrameCY,     sc);
            int hw = SymbolLayout.Scale(SymbolLayout.FrameHalfW,  sc);
            int hh = SymbolLayout.Scale(SymbolLayout.FrameHalfH,  sc);
            int bw = Mathf.Max(2, SymbolLayout.Scale(SymbolLayout.BorderWidth, sc));

            switch (group)
            {
                case IdentityGroup.Hostile:
                    ProceduralDrawUtil.FillDiamond(buf, sz, cx, cy, hw, hh, fill, bdr, bw, style);
                    break;
                case IdentityGroup.Neutral:
                    ProceduralDrawUtil.FillRect(buf, sz, cx - hh, b, cx + hh, t, fill, bdr, bw, style);
                    break;
                case IdentityGroup.Unknown:
                    ProceduralDrawUtil.FillEllipse(buf, sz, cx, cy, hw, hh, fill, bdr, bw, style);
                    break;
                default: // Friend
                    ProceduralDrawUtil.FillRect(buf, sz, l, b, r, t, fill, bdr, bw, style);
                    break;
            }
        }
    }
}
