// FatigueProbe.cs
// What marching and fighting actually cost, in minutes.
//
// Menu:  Strategos > Probe Fatigue
// Batch: -executeMethod Strategos.Editor.FatigueProbe.Run
//
// THE TABLE IS THE POINT. The assertions catch the mechanism breaking; they cannot tell a
// defensible curve from an indefensible one, and the curve is the design. "How long can a
// rifle company march before it is worth half what it was" is a balance question, and the
// only way to answer it is to read the number.
//
// Fatigue is a world rule, not an opt-in like training, so unlike TrainingProbe there is no
// "unchanged by default" property to lean on — every scenario's numbers move. What can still
// be asserted is the shape: costs only ever fall, recovery only ever rises, neither passes
// its bound, and a unit that does nothing is not worn down by the passage of time alone.

#if UNITY_EDITOR
using System.Text;
using UnityEditor;
using UnityEngine;
using Strategos.Maps;
using Strategos.Units;

namespace Strategos.Editor
{
    public static class FatigueProbe
    {
        private const float SecondsPerTick = 1f;

        [MenuItem("Strategos/Probe Fatigue")]
        public static void Run()
        {
            var log = new StringBuilder();
            bool ok = true;

            PrintCosts(log);
            ok &= RestingRecovers(log);
            ok &= IdleUnitDoesNotTire(log);
            ok &= FloorHolds(log);
            ok &= DestroyedUnitIsUntouched(log);

            Debug.Log(log.ToString());
            Debug.Log(ok ? "[FatigueProbe] PROBE PASSED" : "[FatigueProbe] PROBE FAILED");
        }

        // ─── The table ────────────────────────────────────────────────────────

        private static void PrintCosts(StringBuilder log)
        {
            var catalogue = UnitCatalogue.Default();

            log.AppendLine("  minutes of activity to reach a given readiness (open ground)");
            log.AppendLine("    unit type                marching to 75  to 50  to floor" +
                           "   fighting to 50   rest 25->100");

            foreach (var id in new[]
            {
                UnitCatalogue.InfantryFoot, UnitCatalogue.InfantryMech,
                UnitCatalogue.InfantryMotor, UnitCatalogue.Armor,
                UnitCatalogue.ReconMotor, UnitCatalogue.Artillery,
            })
            {
                var caps = catalogue.Get(id);
                var f = caps.Fatigue;

                log.AppendLine($"    {caps.Name,-24}" +
                    $"{Minutes(f.PerHourMoving, 100f, 75f),14:0.0}" +
                    $"{Minutes(f.PerHourMoving, 100f, 50f),7:0.0}" +
                    $"{Minutes(f.PerHourMoving, 100f, FatigueModel.MinReadiness),10:0.0}" +
                    $"{Minutes(f.PerHourEngaged, 100f, 50f),16:0.0}" +
                    $"{Minutes(f.RecoveryPerHourResting, FatigueModel.MinReadiness, 100f),15:0.0}");
            }

            log.AppendLine($"    floor {FatigueModel.MinReadiness:0}, terrain multiplier up to " +
                           $"{FatigueModel.MaxTerrainMultiplier:0.0}x on hard going");
        }

        /// <summary>Minutes to move <paramref name="from"/> to <paramref name="to"/> at a per-hour rate.</summary>
        private static float Minutes(float perHour, float from, float to) =>
            perHour <= 0f ? float.PositiveInfinity : Mathf.Abs(to - from) / perHour * 60f;

        // ─── Shape properties ─────────────────────────────────────────────────

        private static UnitInstance Unit(string capabilityId, float readiness = 100f) =>
            new()
            {
                Id = new UnitId(1),
                CapabilityId = capabilityId,
                Readiness = readiness,
                Strength = 100f,
            };

        private static bool RestingRecovers(StringBuilder log)
        {
            var catalogue = UnitCatalogue.Default();
            var unit = Unit(UnitCatalogue.InfantryFoot, 50f);
            unit.Posture = Posture.Halted;

            for (int t = 0; t < 3600; t++)
                FatigueModel.Apply(unit, engaged: false, null, catalogue, SecondsPerTick);

            if (unit.Readiness <= 50f)
            {
                log.AppendLine($"  resting: FAILED, an hour of rest left readiness at " +
                               $"{unit.Readiness:0.0}");
                return false;
            }

            if (unit.Readiness > 100f)
            {
                log.AppendLine($"  resting: FAILED, recovered past 100 to {unit.Readiness:0.0}");
                return false;
            }

            log.AppendLine($"  resting: an hour halted took 50 to {unit.Readiness:0.0}  ok");
            return true;
        }

        /// <summary>
        /// A unit doing nothing must not be worn down by time alone.
        /// </summary>
        /// <remarks>
        /// The first cut of the issue proposed a slow drift for "time without rest", which is
        /// real but would mean a scenario is unwinnable if it runs long enough regardless of
        /// what the player does. Left out deliberately; asserted here so it is not added back
        /// without the decision being made again.
        /// </remarks>
        private static bool IdleUnitDoesNotTire(StringBuilder log)
        {
            var catalogue = UnitCatalogue.Default();
            var unit = Unit(UnitCatalogue.InfantryFoot);
            unit.Posture = Posture.Halted;

            for (int t = 0; t < 3600; t++)
                FatigueModel.Apply(unit, engaged: false, null, catalogue, SecondsPerTick);

            if (unit.Readiness < 100f)
            {
                log.AppendLine($"  idle: FAILED, a fresh halted unit fell to {unit.Readiness:0.0} " +
                               "over an hour of doing nothing");
                return false;
            }

            log.AppendLine("  idle: a fresh halted unit stays at 100  ok");
            return true;
        }

        private static bool FloorHolds(StringBuilder log)
        {
            var catalogue = UnitCatalogue.Default();
            var unit = Unit(UnitCatalogue.InfantryFoot);
            unit.Posture = Posture.Moving;

            // Long enough to blow through the floor several times over if nothing stopped it.
            for (int t = 0; t < 6 * 3600; t++)
                FatigueModel.Apply(unit, engaged: true, null, catalogue, SecondsPerTick);

            if (unit.Readiness < FatigueModel.MinReadiness - 0.001f)
            {
                log.AppendLine($"  floor: FAILED, six hours of marching and fighting reached " +
                               $"{unit.Readiness:0.0}, below the floor of " +
                               $"{FatigueModel.MinReadiness:0}");
                return false;
            }

            if (unit.Effectiveness <= 0f)
            {
                log.AppendLine("  floor: FAILED, an exhausted unit has zero effectiveness — " +
                               "that is what IsDestroyed is for");
                return false;
            }

            log.AppendLine($"  floor: six hours of marching and fighting bottomed at " +
                           $"{unit.Readiness:0.0}, effectiveness {unit.Effectiveness:0.00}  ok");
            return true;
        }

        private static bool DestroyedUnitIsUntouched(StringBuilder log)
        {
            var catalogue = UnitCatalogue.Default();
            var unit = Unit(UnitCatalogue.InfantryFoot, 40f);
            unit.Strength = 0f;
            unit.Posture = Posture.Halted;

            FatigueModel.Apply(unit, engaged: false, null, catalogue, SecondsPerTick);

            if (!Mathf.Approximately(unit.Readiness, 40f))
            {
                log.AppendLine($"  destroyed: FAILED, a destroyed unit's readiness moved to " +
                               $"{unit.Readiness:0.00} — it should be inert");
                return false;
            }

            log.AppendLine("  destroyed: a destroyed unit neither tires nor recovers  ok");
            return true;
        }
    }
}
#endif
