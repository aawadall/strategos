// NatoSymbolDecorator.cs
// Abstract decorator and concrete base framed symbol for APP-6D composition.

using System.Collections.Generic;
using UnityEngine;

namespace Strategos.NatoSymbols
{
    /// <summary>
    /// Framed + filled base symbol produced by the factory (Table 3-1 Step 1).
    /// </summary>
    public sealed class BaseNatoSymbol : INatoSymbol
    {
        private readonly List<SymbolLayerDraw> _layers;

        public BaseNatoSymbol(SIDCCode code, SymbolLayerDraw frameLayer)
        {
            Code   = code;
            Text   = SymbolTextAmplifiers.FromCode(code);
            _layers = new List<SymbolLayerDraw> { frameLayer };
        }

        public SIDCCode Code { get; }
        public SymbolTextAmplifiers Text { get; }
        public IReadOnlyList<SymbolLayerDraw> Layers => _layers;
    }

    /// <summary>
    /// Decorator base: wraps an inner INatoSymbol and appends own layers.
    /// </summary>
    public abstract class NatoSymbolDecorator : INatoSymbol
    {
        protected readonly INatoSymbol Inner;
        private List<SymbolLayerDraw> _merged;
        private SymbolTextAmplifiers _text;

        protected NatoSymbolDecorator(INatoSymbol inner)
        {
            Inner = inner ?? throw new System.ArgumentNullException(nameof(inner));
            _text = inner.Text;
        }

        public SIDCCode Code => Inner.Code;

        public SymbolTextAmplifiers Text
        {
            get
            {
                EnsureMerged();
                return _text;
            }
        }

        public IReadOnlyList<SymbolLayerDraw> Layers
        {
            get
            {
                EnsureMerged();
                return _merged;
            }
        }

        /// <summary>Override to append graphic layers and/or replace text amplifiers.</summary>
        protected abstract void Contribute(List<SymbolLayerDraw> layers, ref SymbolTextAmplifiers text);

        private void EnsureMerged()
        {
            if (_merged != null) return;
            _merged = new List<SymbolLayerDraw>(Inner.Layers.Count + 4);
            _merged.AddRange(Inner.Layers);
            var text = Inner.Text;
            Contribute(_merged, ref text);
            _text = text;
        }
    }
}
