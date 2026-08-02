// ContactTracker.cs
// Who can see whom, and the reports that follow from it changing.
//
// This is the only place in the project that decides a unit has been detected. Everything
// downstream — the report feed, autonomous reaction in #13, the intelligence layer in Phase
// 5.3 — reads reports and never repeats this test. That rule is the acceptance criterion the
// issue spends most of its words on, and it is worth restating why: a consumer that polls
// world state for hostiles cannot be deceived, delayed or jammed, so every one written that
// way is a consumer that C3 has to go back and rewrite.
//
// EDGES, NOT STATE. A contact is published when a hostile *enters* detection and lost when it
// *leaves*. Publishing the current picture every tick would produce one message per observer
// per hostile per second, which is not a report — it is a poll wearing a message's clothes,
// and it would drown the log it is supposed to make readable.
//
// RANGE, NOT LINE OF SIGHT. Terrain LOS does not exist anywhere in the project; it is a Phase
// 1 / M1 item and substantial on its own. Detection routes through
// UnitCapabilities.DetectionRangeAt, which is documented as the single call to replace, so
// LOS lands there and nothing in this file changes.

using System;
using System.Collections.Generic;
using UnityEngine;
using Strategos.Maps;
using Strategos.Scenarios;
using Strategos.Units;

namespace Strategos.Reports
{
    public sealed class ContactTracker
    {
        /// <summary>
        /// How much further than detection range a contact must go before it counts as lost,
        /// as a multiplier.
        ///
        /// Without hysteresis a unit halted within a metre of the boundary flaps: seen, lost,
        /// seen, lost, one pair of reports every tick for as long as it stands there. That is
        /// not a hypothetical — a unit ordered to a cell near the edge of an observer's arc
        /// finishes its move there and stops. Five percent is enough to cover the float noise
        /// and the sub-cell drift of a moving unit without being large enough to notice.
        /// </summary>
        public const float LossHysteresis = 1.05f;

        private readonly Scenario _scenario;
        private readonly IReadOnlyList<UnitInstance> _units;

        // Detection is a directed relation — the observer's cover sets its range, so A can see
        // B while B cannot see A — hence a full matrix rather than a triangle.
        //
        // A flat bool[] indexed by position in _units, NOT a HashSet of pairs. Membership is
        // all a set would give, and iterating one to find what has been lost is exactly the
        // unordered traversal that makes a replay diverge. Index order is the scenario's unit
        // order, which is stable by construction.
        private readonly bool[] _seen;
        private readonly int _n;

        public ContactTracker(Scenario scenario, IReadOnlyList<UnitInstance> units)
        {
            _scenario = scenario;
            _units = units ?? Array.Empty<UnitInstance>();
            _n = _units.Count;
            _seen = new bool[_n * _n];
        }

        /// <summary>Contacts currently held, across all observers. Diagnostic.</summary>
        public int ActiveContacts
        {
            get
            {
                int n = 0;
                for (int i = 0; i < _seen.Length; i++) if (_seen[i]) n++;
                return n;
            }
        }

        /// <summary>
        /// Tests every observer against every hostile and publishes what changed.
        ///
        /// Called once per step, after units have moved, so a contact reports where the
        /// subject actually is at the end of the tick rather than where it started.
        ///
        /// O(n²) per step, deliberately. At sandbox scale that is thirty-six distance tests a
        /// second and a spatial index would be code with no measurable benefit. It stops being
        /// acceptable somewhere in the low hundreds of units, well before the theatre scale
        /// the roadmap ends at; a uniform grid bucketed by detection range is the fix, and it
        /// drops in here without changing anything that consumes the reports.
        /// </summary>
        public void Sweep(MapData map, UnitCatalogue catalogue, int tick,
            Action<SituationReport> publish)
        {
            if (publish == null) return;

            // Held reports first: one observed three ticks ago must reach the topic before
            // anything seen now, or the feed reads out of order.
            Flush(tick, publish);

            if (map == null || _n == 0) return;

            float metresPerCell = Mathf.Max(0.0001f, map.Header.MetresPerCell);

            for (int i = 0; i < _n; i++)
            {
                var observer = _units[i];
                if (observer == null) continue;

                var observerSide = _scenario?.FindSide(observer.Side);

                // Range depends on where the observer is standing, so it is hoisted out of the
                // inner loop: it is a property of the observer, not of the pair.
                var caps = observer.Capabilities(catalogue);
                float rangeCells = caps.DetectionRangeAt(observer.Landcover(map)) / metresPerCell;
                float lossCells = rangeCells * LossHysteresis;

                for (int j = 0; j < _n; j++)
                {
                    if (i == j) continue;

                    var subject = _units[j];
                    if (subject == null) continue;

                    int slot = i * _n + j;
                    bool held = _seen[slot];

                    if (!Side.AreHostile(observerSide, _scenario?.FindSide(subject.Side)))
                    {
                        // Not an enemy — and if it used to be, the contact is not "lost", it
                        // has stopped being a contact. Clear silently rather than publishing a
                        // ContactLost nobody could act on.
                        _seen[slot] = false;
                        continue;
                    }

                    // A WRECK IS NOT A CONTACT. Until wrecks existed this was never asked,
                    // and the answer was silently wrong: a destroyed company went on being
                    // reported as a live contact for the rest of the scenario, telling its
                    // enemy's commander there was a going concern on ground that had been
                    // cleared. A held contact on something that has just been destroyed is
                    // *lost* — it has stopped being a threat, which is exactly what the
                    // report says.
                    if (subject.IsDestroyed)
                    {
                        if (held)
                        {
                            _seen[slot] = false;
                            Send(SituationReport.ContactLost(observer.Id, subject, tick),
                                observer, tick, publish);
                        }
                        continue;
                    }

                    // An observer that has been destroyed reports nothing further.
                    if (observer.IsDestroyed) { _seen[slot] = false; continue; }

                    float distance = Vector2.Distance(observer.Cell, subject.Cell);

                    if (!held && distance <= rangeCells)
                    {
                        _seen[slot] = true;
                        Send(SituationReport.Contact(observer.Id, subject, tick),
                            observer, tick, publish);
                    }
                    else if (held && distance > lossCells)
                    {
                        _seen[slot] = false;
                        Send(SituationReport.ContactLost(observer.Id, subject, tick),
                            observer, tick, publish);
                    }
                }
            }
        }

        /// <summary>
        /// A report that has been observed and not yet reached anyone.
        /// </summary>
        /// <remarks>
        /// This is the one place training touches what a *commander knows* rather than what a
        /// unit does, and it is the reason the effect is worth having. A green unit does not
        /// see less — it is slower to say so, so the picture its commander is working from is
        /// older. That is the fog #34 and #36 are about, arriving from the command chain
        /// rather than from a vision cone.
        /// </remarks>
        private readonly List<(int Due, SituationReport Report)> _pending = new();

        /// <summary>
        /// Publishes now, or holds the report until the observer has got round to sending it.
        /// </summary>
        /// <remarks>
        /// <see cref="SituationReport.ObservedTick"/> is left at the moment of observation and
        /// only the publication tick moves — which is exactly the split Simulation.Report
        /// documents and refuses to overwrite, so a delayed contact arrives already carrying
        /// how stale it is. Nothing downstream needed changing to understand that.
        /// </remarks>
        private void Send(SituationReport report, UnitInstance observer, int tick,
            Action<SituationReport> publish)
        {
            int delay = observer?.HesitationTicks ?? 0;
            if (delay <= 0) { publish(report); return; }
            _pending.Add((tick + delay, report));
        }

        /// <summary>
        /// Releases everything now due, oldest first.
        /// </summary>
        /// <remarks>
        /// A List walked in index order, never a Dictionary: delivery order is part of the
        /// replay signature, and the report log would diverge if two reports due on the same
        /// tick could swap. Appends are in observation order, so index order is observation
        /// order.
        /// </remarks>
        private void Flush(int tick, Action<SituationReport> publish)
        {
            for (int i = 0; i < _pending.Count; i++)
            {
                if (_pending[i].Due > tick) continue;
                publish(_pending[i].Report);
                _pending.RemoveAt(i);
                i--;
            }
        }

        /// <summary>Forgets every contact. For resetting between replays.</summary>
        public void Reset()
        {
            Array.Clear(_seen, 0, _seen.Length);
            _pending.Clear();
        }

        // ─── Save/load (#74) ─────────────────────────────────────────────────

        /// <summary>
        /// One held-back report and the tick it becomes due — the serialisable shape of
        /// <see cref="_pending"/>'s tuple, named for a save file rather than left anonymous.
        /// </summary>
        public struct PendingContact
        {
            public int Due;
            public SituationReport Report;
        }

        /// <summary>
        /// A copy of who has seen whom, indexed exactly as <see cref="_seen"/> is (observer
        /// index * <c>_n</c> + subject index, in <see cref="_units"/>'s order).
        /// </summary>
        /// <remarks>
        /// **This is the player's knowledge, and it is not derivable from anywhere else once
        /// hesitation delay exists.** It could in principle be replayed back from
        /// <c>ReportLog</c>'s Contact/ContactLost edges — the *held* half cannot: a report a
        /// green observer has not yet got round to sending (<see cref="_pending"/>) has not
        /// reached <c>ReportLog</c> at all, so restoring only from the log would silently drop
        /// exactly the reports a slow unit was mid-way through delaying. Snapshotting both
        /// fields directly avoids relying on that derivation.
        /// </remarks>
        public bool[] SnapshotSeen() => (bool[])_seen.Clone();

        /// <summary>A copy of every report observed and not yet sent. See <see cref="SnapshotSeen"/>.</summary>
        public List<PendingContact> SnapshotPending()
        {
            var copy = new List<PendingContact>(_pending.Count);
            for (int i = 0; i < _pending.Count; i++)
                copy.Add(new PendingContact { Due = _pending[i].Due, Report = _pending[i].Report });
            return copy;
        }

        /// <summary>
        /// Restores both fields wholesale — restore-only, and only valid against a tracker built
        /// over the same unit count and order the snapshot was taken from (<see cref="Commands.Simulation.Restore"/>
        /// guarantees this: the fresh simulation's unit list comes from the same scenario).
        /// </summary>
        public void Restore(bool[] seen, IEnumerable<PendingContact> pending)
        {
            if (seen != null && seen.Length == _seen.Length) Array.Copy(seen, _seen, _seen.Length);
            _pending.Clear();
            if (pending != null)
                foreach (var p in pending) _pending.Add((p.Due, p.Report));
        }
    }
}
