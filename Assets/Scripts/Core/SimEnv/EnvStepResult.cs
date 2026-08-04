// EnvStepResult.cs
// #104: what SideEnv.Step returns — observation, reward, done.

using Strategos.Observation;

namespace Strategos.SimEnv
{
    public readonly struct EnvStepResult
    {
        public readonly SideObservation Observation;
        public readonly float Reward;
        public readonly bool Done;

        public EnvStepResult(SideObservation observation, float reward, bool done)
        {
            Observation = observation;
            Reward = reward;
            Done = done;
        }
    }
}
