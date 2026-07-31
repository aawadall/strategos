// AppSession.cs
// State the views share, owned by AppShell.
//
// The split that makes separate tabs feel like one application: the scenario view
// *configures* the map, the explorer *views* it, and neither reaches into the other. Both
// talk to this.
//
// It deliberately holds no textures and no GameObjects. MapData is plain arrays, so a view
// can render it however it likes — 2D sheet, 3D drape, contact sheet — and owns and
// disposes whatever it allocated. Putting a Texture2D here would make disposal everyone's
// problem.

using System;
using Strategos.Maps;
using Strategos.NatoSymbols;
using UnityEngine;

namespace Strategos.UI
{
    public sealed class AppSession
    {
        /// <summary>
        /// Fixed so the app opens on the same ground every run. Matches the seed the
        /// builder's underlay used before there was a session to hold it.
        /// </summary>
        public const int DefaultSeed = 20260729;

        /// <summary>
        /// 256 rather than the generator's 512 default. Generation is synchronous on the
        /// main thread and erosion dominates the cost, so a 512-cell map with erosion and
        /// culture stalls for a noticeable second or more. 256 is responsive enough to
        /// iterate on and the scenario view can raise it deliberately.
        /// </summary>
        public const int DefaultCells = 256;

        private SymbolFactory _symbols;

        public AppSession()
        {
            Settings = new MapGenerationSettings
            {
                Name          = "SCENARIO",
                Seed          = DefaultSeed,
                Width         = DefaultCells,
                Height        = DefaultCells,
                MetresPerCell = 25f,
                Profile       = ReliefProfile.Rolling,
            };
            Mode = MapRenderMode.Topographic;
        }

        /// <summary>The scenario's map settings. Mutated in place by the scenario view.</summary>
        public MapGenerationSettings Settings { get; }

        /// <summary>
        /// The generated map, or null until <see cref="Generate"/> has been called.
        /// Generation is not done in the constructor: it costs hundreds of milliseconds
        /// and the shell builds before any view has asked for a map.
        /// </summary>
        public MapData Map { get; private set; }

        /// <summary>Render mode the 2D views draw with.</summary>
        public MapRenderMode Mode { get; set; }

        /// <summary>
        /// Bumped every time <see cref="Map"/> is replaced. A hidden view records the
        /// value it last drew and re-renders in OnShown only if this has moved, so
        /// regenerating does not cost a render in every view that is not on screen.
        /// </summary>
        public int Generation { get; private set; }

        /// <summary>Raised after <see cref="Map"/> is replaced.</summary>
        public event Action MapChanged;

        /// <summary>
        /// The running scenario, published by PLAY, or null before it has loaded one.
        /// </summary>
        /// <remarks>
        /// A **reference to the live simulation**, deliberately, not a copy of anything out of
        /// it. The drill binder rates units against drills and has to read their real condition
        /// — a unit shot to pieces must not still show as trained. Loading a second copy of the
        /// scenario here would give exactly that: two sets of units, one of them frozen at load
        /// time, and the ORBAT list has already made that mistake once by showing load-time
        /// positions while a unit was under way.
        ///
        /// Null is a normal state, not an error: a player who has never opened PLAY has no
        /// force to rate, and a reader must say so rather than invent one.
        ///
        /// This does not make AppSession own the simulation. PLAY builds it, PLAY steps it, and
        /// nothing else may mutate it — reading unit state is the only use intended here.
        /// </remarks>
        public Commands.Simulation Simulation { get; set; }

        /// <summary>
        /// One cached symbol factory for the whole app.
        ///
        /// IMPORTANT: the sprites this returns are cached and shared. Never Destroy one —
        /// only ClearCache may, and it destroys the backing textures for every holder.
        /// The builder bakes its own preview uncached precisely so it can dispose it.
        /// </summary>
        public SymbolFactory Symbols => _symbols ??= SymbolFactory.Create();

        /// <summary>
        /// Regenerates the map from the current settings. Synchronous and main-thread by
        /// design — see the GENERATE button flow in the scenario view, which yields a
        /// frame first so its progress label actually reaches the screen.
        /// </summary>
        public MapData Generate()
        {
            Map = MapGenerator.Generate(Settings);
            Generation++;
            MapChanged?.Invoke();
            return Map;
        }

        /// <summary>Generates only if there is no map yet. Cheap for a view to call in OnShown.</summary>
        public MapData EnsureMap() => Map ?? Generate();

        /// <summary>Advances the seed and regenerates. Same profile, different ground.</summary>
        public MapData Reseed()
        {
            Settings.Seed = UnityEngine.Random.Range(1, int.MaxValue);
            return Generate();
        }

        public void ClearSymbolCache() => _symbols?.ClearCache();
    }
}
