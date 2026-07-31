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
            if (map == null || publish == null || _n == 0) return;

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

                    float distance = Vector2.Distance(observer.Cell, subject.Cell);

                    if (!held && distance <= rangeCells)
                    {
                        _seen[slot] = true;
                        publish(SituationReport.Contact(observer.Id, subject, tick));
                    }
                    else if (held && distance > lossCells)
                    {
                        _seen[slot] = false;
                        publish(SituationReport.ContactLost(observer.Id, subject, tick));
                    }
                }
            }
        }

        /// <summary>Forgets every contact. For resetting between replays.</summary>
        public void Reset() => Array.Clear(_seen, 0, _seen.Length);
    }
}
