// WorldLayer.cs
// #34: spawn/despawn, movement block query, lifetime, signature, restore.
//
// Distinct from ControlMeasures (authored plan graphics) and MapPoi/MapLine (generator
// terrain). Hazards are runtime state on the Simulation, not Scenario JSON.

using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace Strategos.World
{
    public sealed class WorldLayer
    {
        private readonly List<WorldObject> _objects = new();
        private int _nextId = 1;

        /// <summary>Live objects in spawn order. Never mutate the list from outside.</summary>
        public IReadOnlyList<WorldObject> Objects => _objects;

        public int Count => _objects.Count;

        /// <summary>
        /// Spawns an object. Returns its id. Cell is clamped by the caller; this does not
        /// know the map extent.
        /// </summary>
        public int Spawn(WorldObjectKind kind, Vector2Int cell, int lifetimeTicks = -1)
        {
            if (kind == WorldObjectKind.None) kind = WorldObjectKind.HazardBlocking;

            int id = _nextId++;
            _objects.Add(new WorldObject
            {
                Id = id,
                Kind = kind,
                Cell = cell,
                LifetimeTicks = lifetimeTicks,
            });
            return id;
        }

        /// <summary>Removes by id. Returns false when missing.</summary>
        public bool Despawn(int id)
        {
            for (int i = 0; i < _objects.Count; i++)
            {
                if (_objects[i].Id != id) continue;
                _objects.RemoveAt(i);
                return true;
            }
            return false;
        }

        /// <summary>True when a HazardBlocking object occupies this cell.</summary>
        public bool BlocksMovement(int x, int y)
        {
            for (int i = 0; i < _objects.Count; i++)
            {
                var o = _objects[i];
                if (o.Kind != WorldObjectKind.HazardBlocking) continue;
                if (o.Cell.x == x && o.Cell.y == y) return true;
            }
            return false;
        }

        /// <summary>Decrement lifetimes; remove expired. Call once per Simulation.Step.</summary>
        public void TickLifetimes()
        {
            for (int i = _objects.Count - 1; i >= 0; i--)
            {
                var o = _objects[i];
                if (o.LifetimeTicks < 0) continue; // until Despawn
                o.LifetimeTicks--;
                if (o.LifetimeTicks <= 0) _objects.RemoveAt(i);
            }
        }

        public void AppendSignature(StringBuilder sb)
        {
            sb.Append('W').Append('[');
            for (int i = 0; i < _objects.Count; i++)
            {
                var o = _objects[i];
                sb.Append(o.Id).Append(':')
                  .Append((int)o.Kind).Append(':')
                  .Append(o.Cell.x).Append(',')
                  .Append(o.Cell.y).Append(':')
                  .Append(o.LifetimeTicks).Append(';');
            }
            sb.Append(']');
        }

        /// <summary>Replace contents from a snapshot — restore-only.</summary>
        public void Restore(IReadOnlyList<WorldObject> objects, int nextId)
        {
            _objects.Clear();
            if (objects != null)
            {
                for (int i = 0; i < objects.Count; i++)
                    if (objects[i] != null) _objects.Add(objects[i].Clone());
            }

            _nextId = nextId > 0 ? nextId : 1;
            for (int i = 0; i < _objects.Count; i++)
                if (_objects[i].Id >= _nextId) _nextId = _objects[i].Id + 1;
        }

        public int PeekNextId() => _nextId;
    }
}
