// WorldObject.cs
// #34 / #273: runtime objects that are neither MapData nor units — hazards, later bridges,
// wrecks-as-props, etc. Data only; behaviour lives on WorldLayer.

using System;
using UnityEngine;

namespace Strategos.World
{
    /// <summary>Kinds of dynamic world object. Room for Bridge/Clear later (#33 stubs).</summary>
    public enum WorldObjectKind
    {
        None = 0,
        /// <summary>Occupies a cell and blocks movement through it (#275).</summary>
        HazardBlocking = 1,
    }

    /// <summary>One spawned world object. Identity + placement + remaining lifetime.</summary>
    [Serializable]
    public sealed class WorldObject
    {
        public int Id;
        public WorldObjectKind Kind = WorldObjectKind.HazardBlocking;
        /// <summary>Cell centre, integer grid coordinates.</summary>
        public Vector2Int Cell;
        /// <summary>
        /// Remaining ticks before despawn. Use <c>-1</c> for until <see cref="WorldLayer.Despawn"/>.
        /// </summary>
        public int LifetimeTicks = -1;

        public WorldObject Clone() => new()
        {
            Id = Id,
            Kind = Kind,
            Cell = Cell,
            LifetimeTicks = LifetimeTicks,
        };

        public override string ToString() =>
            $"[{Id}] {Kind} @ ({Cell.x},{Cell.y}) t{LifetimeTicks}";
    }
}
