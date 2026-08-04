// ObservationProbe.cs
// #101: SideObservationEncoder is belief-only, and the fog-leak guard can fail.
//
// The issue's acceptance criterion is not "observations look plausible" — it is that two
// states differing only in an *unseen* enemy's true cell produce identical encodings, and
// that a naive ground-truth encoder on the same pair is *not* identical (so the guard is
// shown live before anyone trusts the green belief path).
//
// Menu:  Strategos > Probe Observation
// Batch: -executeMethod Strategos.Editor.ObservationProbe.Run

#if UNITY_EDITOR
using System.Text;
using UnityEditor;
using UnityEngine;
using Strategos.Commands;
using Strategos.Observation;
using Strategos.Reports;
using Strategos.Scenarios;
using Strategos.Units;

namespace Strategos.Editor
{
    public static class ObservationProbe
    {
        [MenuItem("Strategos/Probe Observation")]
        public static void Run()
        {
            var log = new StringBuilder();
            int bad = 0;

            bad += CheckNaiveEncoderDetectsUnseenMove(log);
            bad += CheckBeliefEncoderFogLeak(log);
            bad += CheckInRangeBeliefDiffers(log);
            bad += CheckDeterminism(log);

            log.AppendLine(bad == 0 ? "PROBE PASSED" : $"PROBE FAILED with {bad} problem(s)");
            if (bad == 0) Debug.Log("[ObservationProbe]\n" + log);
            else Debug.LogError("[ObservationProbe]\n" + log);
        }

        // ─── Fixtures ─────────────────────────────────────────────────────────

        private static Simulation NewSim(out Scenario scenario)
        {
            scenario = ScenarioSamples.Skirmish();
            scenario.Map.EnableErosion = false;
            var map = scenario.GenerateMap();
            var sim = new Simulation(scenario, map, UnitCatalogue.Default());
            sim.AddExecutor(new MoveToExecutor());
            return sim;
        }

        private static void ParkSide(Simulation sim, SideId side, Vector2 cell)
        {
            for (int i = 0; i < sim.Units.Count; i++)
            {
                var u = sim.Units[i];
                if (u.Side == side) u.Cell = cell;
            }
        }

        private static void MaxTrainFriendly(Simulation sim, SideId side)
        {
            for (int i = 0; i < sim.Units.Count; i++)
            {
                var u = sim.Units[i];
                if (u.Side == side) u.Training = 100f;
            }
        }

        private static SideObservation EncodeBelief(Simulation sim, SideId side) =>
            SideObservationEncoder.Encode(
                side, sim.Tick, sim.Map.Width, sim.Map.Height,
                sim.Units, sim.ReportLog.Entries, sim.Victory);

        /// <summary>
        /// Deliberately wrong: copies every living hostile's true Cell into contact slots.
        /// Exists so the fog-leak fixture can prove the guard goes RED against a leak.
        /// </summary>
        private static SideObservation EncodeNaiveGroundTruth(Simulation sim, SideId side)
        {
            var buf = new float[SideObservation.Length];
            float invW = 1f / Mathf.Max(1, sim.Map.Width);
            float invH = 1f / Mathf.Max(1, sim.Map.Height);

            buf[0] = sim.Tick / SideObservationEncoder.TickScale;
            buf[1] = side.Value;

            int ownSlot = 0;
            int contactSlot = 0;
            for (int i = 0; i < sim.Units.Count; i++)
            {
                var u = sim.Units[i];
                if (u == null) continue;

                if (u.Side == side && ownSlot < SideObservation.MaxOwnUnits)
                {
                    int o = SideObservation.OwnUnitsOffset + ownSlot * SideObservation.OwnUnitFloats;
                    buf[o] = u.IsDestroyed ? 0f : 1f;
                    buf[o + 1] = u.Cell.x * invW;
                    buf[o + 2] = u.Cell.y * invH;
                    buf[o + 3] = Mathf.Clamp01(u.Strength / 100f);
                    buf[o + 4] = (int)u.Posture;
                    ownSlot++;
                    continue;
                }

                if (u.Side != side && !u.IsDestroyed && contactSlot < SideObservation.MaxContacts)
                {
                    int o = SideObservation.ContactsOffset + contactSlot * SideObservation.ContactFloats;
                    buf[o] = 1f;
                    buf[o + 1] = u.Cell.x * invW;
                    buf[o + 2] = u.Cell.y * invH;
                    contactSlot++;
                }
            }

            return new SideObservation(buf);
        }

        // ─── Checks ───────────────────────────────────────────────────────────

        /// <summary>
        /// Sensitivity: unseen hostile at two cells → naive encodings MUST differ.
        /// If they matched, the fog-leak fixture could not catch a ground-truth leak.
        /// </summary>
        private static int CheckNaiveEncoderDetectsUnseenMove(StringBuilder log)
        {
            var a = UnseenPair(new Vector2(200f, 200f), out var side);
            var b = UnseenPair(new Vector2(220f, 180f), out _);

            var na = EncodeNaiveGroundTruth(a, side);
            var nb = EncodeNaiveGroundTruth(b, side);
            int differ = na.DifferCount(nb);

            if (differ == 0)
            {
                log.AppendLine("  naive-sensitivity: FAILED — naive encodings identical for " +
                               "two unseen hostile placements (guard would be inert)");
                return 1;
            }

            log.AppendLine($"  naive-sensitivity: OK — {differ} float(s) differ " +
                           "(ground-truth leak would go RED on fog-leak)");
            return 0;
        }

        /// <summary>
        /// Belief fog-leak: same fixture → belief encodings MUST be identical, and
        /// ActiveContacts must stay zero so we are not accidentally testing in-range.
        /// </summary>
        private static int CheckBeliefEncoderFogLeak(StringBuilder log)
        {
            var a = UnseenPair(new Vector2(200f, 200f), out var side);
            var b = UnseenPair(new Vector2(220f, 180f), out _);

            if (a.ActiveContacts != 0 || b.ActiveContacts != 0)
            {
                log.AppendLine($"  fog-leak: FAILED — expected zero contacts, " +
                               $"got A={a.ActiveContacts} B={b.ActiveContacts}");
                return 1;
            }

            // Re-state the sensitivity on this exact pair so a future change that makes
            // naive identical cannot silently green the belief path.
            var na = EncodeNaiveGroundTruth(a, side);
            var nb = EncodeNaiveGroundTruth(b, side);
            if (na.EqualsExact(nb))
            {
                log.AppendLine("  fog-leak: FAILED — naive encodings matched; " +
                               "fixture no longer distinguishes a ground-truth leak");
                return 1;
            }

            var ba = EncodeBelief(a, side);
            var bb = EncodeBelief(b, side);
            int differ = ba.DifferCount(bb);
            if (differ != 0)
            {
                log.AppendLine($"  fog-leak: FAILED — belief encodings differ by {differ} float(s) " +
                               "for an unseen hostile (encoder leaked ground truth)");
                return 1;
            }

            log.AppendLine($"  fog-leak: OK — belief identical; naive differed by " +
                           $"{na.DifferCount(nb)} float(s); ActiveContacts=0");
            return 0;
        }

        /// <summary>
        /// Positive control: hostile in detection range at two cells → belief MUST differ.
        /// </summary>
        private static int CheckInRangeBeliefDiffers(StringBuilder log)
        {
            var side = new SideId(1);
            var a = InRangePair(new Vector2(55f, 40f), side);
            var b = InRangePair(new Vector2(65f, 40f), side);

            if (a.ActiveContacts < 1 || b.ActiveContacts < 1)
            {
                log.AppendLine($"  in-range: FAILED — expected contacts, " +
                               $"got A={a.ActiveContacts} B={b.ActiveContacts}");
                return 1;
            }

            int contactReportsA = CountKind(a, ReportKind.Contact);
            int contactReportsB = CountKind(b, ReportKind.Contact);
            if (contactReportsA < 1 || contactReportsB < 1)
            {
                log.AppendLine($"  in-range: FAILED — no Contact reports " +
                               $"(A={contactReportsA} B={contactReportsB})");
                return 1;
            }

            var ba = EncodeBelief(a, side);
            var bb = EncodeBelief(b, side);
            int differ = ba.DifferCount(bb);
            if (differ == 0)
            {
                log.AppendLine("  in-range: FAILED — belief encodings identical for two " +
                               "different reported hostile cells");
                return 1;
            }

            log.AppendLine($"  in-range: OK — belief differs by {differ} float(s); " +
                           $"contacts A={a.ActiveContacts} B={b.ActiveContacts}; " +
                           $"Contact reports A={contactReportsA} B={contactReportsB}");
            return 0;
        }

        private static int CheckDeterminism(StringBuilder log)
        {
            var sim = UnseenPair(new Vector2(200f, 200f), out var side);
            var first = EncodeBelief(sim, side);
            var second = EncodeBelief(sim, side);
            if (!first.EqualsExact(second))
            {
                log.AppendLine($"  determinism: FAILED — re-encode differed by " +
                               $"{first.DifferCount(second)} float(s)");
                return 1;
            }

            log.AppendLine($"  determinism: OK — Length={SideObservation.Length}, " +
                           $"identical on re-encode");
            return 0;
        }

        // ─── Scenario placement ───────────────────────────────────────────────

        /// <summary>
        /// All friendlies at (40,40), all hostiles at <paramref name="hostileCell"/> —
        /// well outside default detection (~60 cells at 25 m/cell).
        /// </summary>
        private static Simulation UnseenPair(Vector2 hostileCell, out SideId blue)
        {
            blue = new SideId(1);
            var red = new SideId(2);
            var sim = NewSim(out _);
            MaxTrainFriendly(sim, blue);
            ParkSide(sim, blue, new Vector2(40f, 40f));
            ParkSide(sim, red, hostileCell);
            sim.Step(20);
            return sim;
        }

        /// <summary>
        /// One blue observer at (40,40); all red stacked at an in-range cell so Contact
        /// reports land. Other blues parked with the observer.
        /// </summary>
        private static Simulation InRangePair(Vector2 hostileCell, SideId blue)
        {
            var red = new SideId(2);
            var sim = NewSim(out _);
            MaxTrainFriendly(sim, blue);
            ParkSide(sim, blue, new Vector2(40f, 40f));
            ParkSide(sim, red, hostileCell);
            sim.Step(20);
            return sim;
        }

        private static int CountKind(Simulation sim, ReportKind kind)
        {
            int n = 0;
            var entries = sim.ReportLog.Entries;
            for (int i = 0; i < entries.Count; i++)
                if (entries[i].Kind == kind) n++;
            return n;
        }
    }
}
#endif
