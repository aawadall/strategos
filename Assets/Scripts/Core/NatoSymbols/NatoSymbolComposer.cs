// NatoSymbolComposer.cs
// Orchestrates Factory (base frame) + Decorator chain per APP-6D Table 3-1.

namespace Strategos.NatoSymbols
{
    /// <summary>
    /// Builds a fully composed <see cref="INatoSymbol"/> from a SIDC.
    /// Order: Frame+Fill → Icon → Sector modifiers → Amplifiers.
    /// </summary>
    public static class NatoSymbolComposer
    {
        public static INatoSymbol Compose(string sidc, NatoSymbolDatabase database = null)
        {
            if (!SIDCParser.TryParse(sidc, out var code))
                return null;
            return Compose(code, database);
        }

        public static INatoSymbol Compose(SIDCCode code, NatoSymbolDatabase database = null)
        {
            var factory = SymbolFactory.Create(database);
            return Compose(code, factory);
        }

        public static INatoSymbol Compose(SIDCCode code, SymbolFactory factory)
        {
            if (factory == null)
                factory = SymbolFactory.Create();

            INatoSymbol symbol = factory.CreateBase(code);
            symbol = new IconDecorator(symbol);
            symbol = new SectorModifierDecorator(symbol);
            symbol = new AmplifierDecorator(symbol);
            return symbol;
        }
    }
}
