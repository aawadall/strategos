// SideObservationEncoder.cs
// #101: pack a side's knowledge into SideObservation without reading hostile ground truth.
//
// Own-unit and objective channels may use live friendly / victory state. Contact channels
// are rebuilt from SituationReport traffic the same way ReactionController rebuilds its
// per-unit Picture — Contact / ContactLost / Destroyed, then unioned across friendly
// observers. Engaged records an attacker id without a position; if that subject is not
// already a contact, it is omitted from the position slots (no Cell to believe).
//
// Never pass a Simulation in: callers hand the lists. That keeps the encoder usable from
// probes and from a future env Step without reaching into the world for hostiles.

using System.Collections.Generic;
using UnityEngine;
using Strategos.Objectives;
using Strategos.Reports;
using Strategos.Units;

namespace Strategos.Observation
{
    public static class SideObservationEncoder
    {
        /// <summary>Tick is divided by this for the header slot (one hour at 1 s/tick).</summary>
        public const float TickScale = 3600f;

        /// <summary>
        /// Encode <paramref name="side"/>'s observation at <paramref name="tick"/>.
        /// <paramref name="reports"/> must be in publication order (ReportLog.Entries).
        /// </summary>
        public static SideObservation Encode(
            SideId side,
            int tick,
            int mapWidth,
            int mapHeight,
            IReadOnlyList<UnitInstance> units,
            IEnumerable<SituationReport> reports,
            VictoryEvaluator victory = null)
        {
            var buf = new float[SideObservation.Length];
            float invW = 1f / Mathf.Max(1, mapWidth);
            float invH = 1f / Mathf.Max(1, mapHeight);

            buf[0] = tick / TickScale;
            buf[1] = side.Value;

            // Own units: scenario unit order, capped and padded.
            int ownSlot = 0;
            if (units != null)
            {
                for (int i = 0; i < units.Count && ownSlot < SideObservation.MaxOwnUnits; i++)
                {
                    var u = units[i];
                    if (u == null || u.Side != side) continue;
                    int o = SideObservation.OwnUnitsOffset + ownSlot * SideObservation.OwnUnitFloats;
                    buf[o] = u.IsDestroyed ? 0f : 1f;
                    buf[o + 1] = u.Cell.x * invW;
                    buf[o + 2] = u.Cell.y * invH;
                    buf[o + 3] = Mathf.Clamp01(u.Strength / 100f);
                    buf[o + 4] = (int)u.Posture;
                    ownSlot++;
                }
            }

            // Hostile belief: per-observer pictures, then side union by freshest LastSeen.
            var contacts = BuildSideContacts(side, units, reports);
            int contactSlot = 0;
            for (int i = 0; i < contacts.Count && contactSlot < SideObservation.MaxContacts; i++)
            {
                int o = SideObservation.ContactsOffset + contactSlot * SideObservation.ContactFloats;
                buf[o] = 1f;
                buf[o + 1] = contacts[i].LastSeen.x * invW;
                buf[o + 2] = contacts[i].LastSeen.y * invH;
                contactSlot++;
            }

            // Objectives: ground-truth ownership (public scenario fact today).
            if (victory != null)
            {
                var objs = victory.Objectives;
                int n = objs != null ? objs.Count : 0;
                for (int i = 0; i < n && i < SideObservation.MaxObjectives; i++)
                {
                    var obj = objs[i];
                    int o = SideObservation.ObjectivesOffset + i * SideObservation.ObjectiveFloats;
                    buf[o] = victory.OwnerOfIndex(i).Value;
                    buf[o + 1] = obj.Cell.x * invW;
                    buf[o + 2] = obj.Cell.y * invH;
                }
            }

            return new SideObservation(buf);
        }

        // ─── Belief rebuild ───────────────────────────────────────────────────

        private struct ContactBelief
        {
            public UnitId Subject;
            public Vector2 LastSeen;
            public int ObservedTick;
        }

        private sealed class ObserverPicture
        {
            public readonly List<UnitId> Contacts = new();
            public readonly List<Vector2> LastSeen = new();
            public readonly List<int> ObservedTick = new();

            public int IndexOf(UnitId id)
            {
                for (int i = 0; i < Contacts.Count; i++)
                    if (Contacts[i] == id) return i;
                return -1;
            }

            public void Saw(UnitId id, Vector2 cell, int observedTick)
            {
                int i = IndexOf(id);
                if (i >= 0)
                {
                    LastSeen[i] = cell;
                    ObservedTick[i] = observedTick;
                    return;
                }
                Contacts.Add(id);
                LastSeen.Add(cell);
                ObservedTick.Add(observedTick);
            }

            public void Lost(UnitId id)
            {
                int i = IndexOf(id);
                if (i < 0) return;
                Contacts.RemoveAt(i);
                LastSeen.RemoveAt(i);
                ObservedTick.RemoveAt(i);
            }
        }

        /// <summary>
        /// Replay reports into per-friendly-observer pictures, then union by subject id
        /// keeping the freshest ObservedTick. Sorted by subject id for stable slot order.
        /// </summary>
        private static List<ContactBelief> BuildSideContacts(
            SideId side,
            IReadOnlyList<UnitInstance> units,
            IEnumerable<SituationReport> reports)
        {
            var sideOf = new Dictionary<int, SideId>();
            if (units != null)
            {
                for (int i = 0; i < units.Count; i++)
                {
                    var u = units[i];
                    if (u == null) continue;
                    sideOf[u.Id.Value] = u.Side;
                }
            }

            var pictures = new Dictionary<int, ObserverPicture>();

            ObserverPicture PictureOf(UnitId source)
            {
                if (!pictures.TryGetValue(source.Value, out var p))
                    pictures[source.Value] = p = new ObserverPicture();
                return p;
            }

            void ForgetEverywhere(UnitId subject)
            {
                // Walk units (stable), not the dictionary — same rule as ReactionController.
                if (units == null) return;
                for (int i = 0; i < units.Count; i++)
                {
                    var u = units[i];
                    if (u == null || u.Side != side) continue;
                    if (pictures.TryGetValue(u.Id.Value, out var p))
                        p.Lost(subject);
                }
            }

            if (reports != null)
            {
                foreach (var r in reports)
                {
                    switch (r.Kind)
                    {
                        case ReportKind.Contact:
                            if (!sideOf.TryGetValue(r.Source.Value, out var srcSide) || srcSide != side)
                                break;
                            PictureOf(r.Source).Saw(r.Subject, r.Cell, r.ObservedTick);
                            break;

                        case ReportKind.ContactLost:
                            if (!sideOf.TryGetValue(r.Source.Value, out var lostSide) || lostSide != side)
                                break;
                            PictureOf(r.Source).Lost(r.Subject);
                            break;

                        case ReportKind.Destroyed:
                            ForgetEverywhere(r.Subject);
                            break;
                    }
                }
            }

            // Union: subject -> best belief.
            var bySubject = new Dictionary<int, ContactBelief>();
            if (units != null)
            {
                for (int i = 0; i < units.Count; i++)
                {
                    var u = units[i];
                    if (u == null || u.Side != side) continue;
                    if (!pictures.TryGetValue(u.Id.Value, out var p)) continue;
                    for (int c = 0; c < p.Contacts.Count; c++)
                    {
                        int sid = p.Contacts[c].Value;
                        var belief = new ContactBelief
                        {
                            Subject = p.Contacts[c],
                            LastSeen = p.LastSeen[c],
                            ObservedTick = p.ObservedTick[c],
                        };
                        if (!bySubject.TryGetValue(sid, out var existing) ||
                            belief.ObservedTick >= existing.ObservedTick)
                            bySubject[sid] = belief;
                    }
                }
            }

            var list = new List<ContactBelief>(bySubject.Count);
            foreach (var kv in bySubject)
                list.Add(kv.Value);
            list.Sort((a, b) => a.Subject.Value.CompareTo(b.Subject.Value));
            return list;
        }
    }
}
