// Trajectory.cs
// #106: one side's episode as (observation, actions) steps — export shape, not a live recorder.

using System.Collections.Generic;
using Strategos.Actions;
using Strategos.Observation;

namespace Strategos.Trajectories
{
    public sealed class TrajectoryStep
    {
        public int Tick;
        public float[] Observation;
        public List<TrajectoryAction> Actions = new();
        public int ReportCountThroughTick;
    }

    public sealed class TrajectoryAction
    {
        public int Unit;
        public int Index;
        public string Code;
    }

    public sealed class Trajectory
    {
        public string ScenarioName;
        public int Side;
        public int ObsLength = SideObservation.Length;
        public int ActionCount = SideActionSpace.Count;
        public string ReportSignature;
        public List<TrajectoryStep> Steps = new();
    }
}
