// CasualtyProbe.cs
// What happens to a unit after it is destroyed.
//
// Menu:  Strategos > Probe Casualties
// Batch: -executeMethod Strategos.Editor.CasualtyProbe.Run
//
// THE BUG THIS FOUND, which is the reason the first assertion is the one it is: until wrecks
// existed, nothing ever asked whether a destroyed unit was still a contact, and the answer was
// silently no-one's — ContactTracker went on reporting a burnt-out company as a live contact
// for the rest of the scenario. The enemy commander's picture said there was a going concern
// on ground that had been cleared, and every consumer of that picture believed it.
//
// A wreck stays on the map deliberately. It is information: ground where a company was
// destroyed is worth knowing about, and removing it would delete the only trace that a fight
// happened there. What it stops being is a *unit* — not commandable, not a contact, not
// counted among a side's troops.

#if UNITY_EDITOR
using System.Text;
using UnityEditor;
using UnityEngine;
using Strategos.Commands;
using Strategos.Reports;
using Strategos.Scenarios;
using Strategos.Units;

namespace Strategos.Editor
{
    public static class CasualtyProbe
    {
        [MenuItem("Strategos/Probe Casualties")]
        public static void Run()
        {
            var log = new StringBuilder();
            bool ok = true;

            ok &= WreckIsNotAContact(log);
            ok &= LossIsRecorded(log);
            ok &= CasualtiesAreInTheSignature(log);

            Debug.Log(log.ToString());
            Debug.Log(ok ? "[CasualtyProbe] PROBE PASSED" : "[CasualtyProbe] PROBE FAILED");
        }

        /// <summary>Two hostile units close enough to see and kill each other.</summary>
        private static Simulation Duel(out UnitInstance attacker, out UnitInstance victim)
        {
            var scenario = ScenarioSamples.Skirmish();
            scenario.Map.EnableErosion = false;
            var map = scenario.GenerateMap();

            attacker = ByName(scenario, "1/A/2-69 AR");   // armour, heavy hitter, BLUFOR
            victim = ByName(scenario, "3/2 MRR");         // motorised infantry, OPFOR

            // Side by side, so the kill happens quickly and detection is certain.
            victim.Cell = attacker.Cell + new Vector2(6f, 0f);

            var sim = new Simulation(scenario, map, UnitCatalogue.Default());
            sim.AddExecutor(new MoveToExecutor());
            sim.AddExecutor(new EngageExecutor());
            return sim;
        }

        private static UnitInstance ByName(Scenario scenario, string designation)
        {
            foreach (var u in scenario.Units)
                if (u.Designation == designation) return u;
            throw new System.InvalidOperationException($"no unit '{designation}'");
        }

        // ─── Assertions ───────────────────────────────────────────────────────

        private static bool WreckIsNotAContact(StringBuilder log)
        {
            var sim = Duel(out var attacker, out var victim);

            int contactsAfterDeath = 0;
            int lostAfterDeath = 0;
            bool dead = false;

            sim.Reports.Subscribe("probe", 300, r =>
            {
                if (!dead) return;
                if (r.Subject == victim.Id && r.Kind == ReportKind.Contact) contactsAfterDeath++;
                if (r.Subject == victim.Id && r.Kind == ReportKind.ContactLost) lostAfterDeath++;
            });

            sim.Issue(Command.Engage(ActorId.ForSide(attacker.Side), attacker.Id, victim.Id));

            for (int t = 0; t < 400 && !dead; t++)
            {
                sim.Step();
                dead = victim.IsDestroyed;
            }

            if (!dead)
            {
                log.AppendLine("  wreck: FAILED, the victim survived 400 ticks — the fixture " +
                               "is not testing what it thinks");
                return false;
            }

            int killedAt = sim.Tick;
            sim.Step(60);

            if (contactsAfterDeath > 0)
            {
                log.AppendLine($"  wreck: FAILED, {contactsAfterDeath} live contact(s) " +
                               "reported on a destroyed unit — its enemy's picture says there " +
                               "is a going concern on cleared ground");
                return false;
            }

            if (lostAfterDeath == 0)
            {
                log.AppendLine("  wreck: FAILED, no ContactLost when the unit was destroyed — " +
                               "an observer holding it still believes it is a threat");
                return false;
            }

            log.AppendLine($"  wreck: destroyed at T+{killedAt}, {lostAfterDeath} " +
                           $"ContactLost, no live contact in the 60 ticks after  ok");
            return true;
        }

        private static bool LossIsRecorded(StringBuilder log)
        {
            var sim = Duel(out var attacker, out var victim);
            sim.Issue(Command.Engage(ActorId.ForSide(attacker.Side), attacker.Id, victim.Id));

            for (int t = 0; t < 400 && !victim.IsDestroyed; t++) sim.Step();

            if (!victim.IsDestroyed)
            {
                log.AppendLine("  record: FAILED, nothing was destroyed");
                return false;
            }

            if (sim.Casualties.Count != 1)
            {
                log.AppendLine($"  record: FAILED, {sim.Casualties.Count} casualties recorded, " +
                               "expected 1");
                return false;
            }

            var c = sim.Casualties.Entries[0];

            if (c.Unit != victim.Id || c.By != attacker.Id || c.Side != victim.Side)
            {
                log.AppendLine($"  record: FAILED, recorded {c} but {victim.Id} was killed by " +
                               $"{attacker.Id}");
                return false;
            }

            // The tick is the whole reason the record exists: afterwards, strength being zero
            // is the only evidence and it cannot say when.
            if (c.Tick != victim.DestroyedAtTick || c.Tick <= 0)
            {
                log.AppendLine($"  record: FAILED, casualty tick {c.Tick} does not match the " +
                               $"unit's DestroyedAtTick {victim.DestroyedAtTick}");
                return false;
            }

            log.AppendLine($"  record: {victim.Designation} lost to {attacker.Designation} at " +
                           $"T+{c.Tick}, {sim.Casualties.CountFor(victim.Side)} for that side  ok");
            return true;
        }

        /// <summary>
        /// Losses must be part of what a replay reproduces.
        /// </summary>
        /// <remarks>
        /// Two runs that end with the same unit strengths but lost them in a different order,
        /// or to different killers, have diverged in everything a campaign would carry
        /// forward — and would look identical to a signature that only covered strengths.
        /// </remarks>
        private static bool CasualtiesAreInTheSignature(StringBuilder log)
        {
            string Run()
            {
                var sim = Duel(out var attacker, out var victim);
                sim.Issue(Command.Engage(ActorId.ForSide(attacker.Side), attacker.Id, victim.Id));
                sim.Step(300);
                return sim.Signature();
            }

            string a = Run(), b = Run();

            if (a != b)
            {
                log.AppendLine("  determinism: FAILED, two identical runs diverged");
                return false;
            }

            if (!a.Contains("C["))
            {
                log.AppendLine("  determinism: FAILED, the casualty log is not in the " +
                               "signature — a divergence in what was lost would go unnoticed");
                return false;
            }

            log.AppendLine("  determinism: 300 ticks IDENTICAL across runs, casualties " +
                           "included in the signature  ok");
            return true;
        }
    }
}
#endif
