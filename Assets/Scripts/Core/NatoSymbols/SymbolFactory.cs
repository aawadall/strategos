// SymbolFactory.cs
// Abstract factory for NATO APP-6D symbol sprites.
// Use SymbolFactory.Create() to get the appropriate concrete factory.
//
// Factory method pattern:
//   SymbolFactory            — abstract base (this file)
//   ProceduralSymbolFactory  — runtime pixel-art generator, no art assets required
//   DatabaseSymbolFactory    — (future) uses NatoSymbolDatabase sprite sheets

using UnityEngine;

namespace Strategos.NatoSymbols
{
    /// <summary>
    /// Abstract base for all NATO APP-6D symbol sprite factories.
    /// Concrete subclasses decide how to render the symbol (procedural pixel-art,
    /// sprite database, vector SVG, etc.).
    /// </summary>
    public abstract class SymbolFactory
    {
        // -------------------------------------------------------------------------
        // Factory method
        // -------------------------------------------------------------------------

        /// <summary>
        /// Returns the most appropriate factory for the current context.
        /// <list type="bullet">
        ///   <item>If <paramref name="database"/> is assigned and fully populated,
        ///   returns a database-backed factory (higher fidelity).</item>
        ///   <item>Otherwise returns <see cref="ProceduralSymbolFactory"/>,
        ///   which generates correct APP-6D shapes at runtime with no art assets.</item>
        /// </list>
        /// </summary>
        public static SymbolFactory Create(NatoSymbolDatabase database = null)
        {
            // When the database has real sprites wired up, swap to a high-fidelity
            // factory.  For now, always use the procedural generator.
            // TODO: return new DatabaseSymbolFactory(database) when database != null
            return new ProceduralSymbolFactory();
        }

        // -------------------------------------------------------------------------
        // Abstract contract
        // -------------------------------------------------------------------------

        /// <summary>
        /// Returns a cached or newly generated Sprite for the given SIDC code.
        /// </summary>
        /// <param name="code">Fully parsed APP-6D symbol identity.</param>
        /// <param name="size">Texture resolution in pixels (square). Default 256.</param>
        public abstract Sprite GetSymbolSprite(SIDCCode code, int size = 256);

        /// <summary>
        /// Releases all cached textures and sprites.
        /// Call when the scene that owns the factory is being destroyed.
        /// </summary>
        public virtual void ClearCache() { }
    }
}
