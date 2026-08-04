// TutorialFirstBeatProbe.cs
// #310: select → MoveTo phase advances on the checklist (real path hooks are PlayView).
// Batch: -executeMethod Strategos.Editor.TutorialFirstBeatProbe.Run

#if UNITY_EDITOR
using System.Text;
using UnityEditor;
using UnityEngine;
using Strategos.UI;

namespace Strategos.Editor
{
    public static class TutorialFirstBeatProbe
    {
        [MenuItem("Strategos/Probe Tutorial First Beat")]
        public static void Run()
        {
            var log = new StringBuilder();
            int bad = 0;

            var hostGo = new GameObject("probe-tut-beat", typeof(RectTransform));
            var beat = hostGo.AddComponent<TutorialFirstBeat>();
            try
            {
                beat.Build(hostGo.GetComponent<RectTransform>());
                if (beat.Phase != TutorialBeatPhase.Inactive)
                {
                    log.AppendLine("  FAIL initial phase not Inactive");
                    bad++;
                }

                beat.Begin();
                if (beat.Phase != TutorialBeatPhase.SelectUnit || !beat.IsActive)
                {
                    log.AppendLine("  FAIL Begin → SelectUnit");
                    bad++;
                }
                else log.AppendLine("  Begin → SelectUnit ok");

                beat.OnUnitSelected(playerCommanded: false);
                if (beat.Phase != TutorialBeatPhase.SelectUnit)
                {
                    log.AppendLine("  FAIL enemy select should not advance");
                    bad++;
                }
                else log.AppendLine("  non-player select ignored ok");

                beat.OnUnitSelected(playerCommanded: true);
                if (beat.Phase != TutorialBeatPhase.IssueMove)
                {
                    log.AppendLine("  FAIL SelectUnit → IssueMove");
                    bad++;
                }
                else log.AppendLine("  SelectUnit → IssueMove ok");

                beat.OnMoveIssued();
                if (beat.Phase != TutorialBeatPhase.Complete || beat.IsActive)
                {
                    log.AppendLine("  FAIL IssueMove → Complete");
                    bad++;
                }
                else log.AppendLine("  IssueMove → Complete ok");

                beat.Reset();
                if (beat.Phase != TutorialBeatPhase.Inactive)
                {
                    log.AppendLine("  FAIL Reset → Inactive");
                    bad++;
                }
                else log.AppendLine("  Reset → Inactive ok");
            }
            catch (System.Exception ex)
            {
                log.AppendLine("  FAIL " + ex.Message);
                bad++;
            }
            finally { UnityEngine.Object.DestroyImmediate(hostGo); }

            log.AppendLine(bad == 0 ? "PROBE PASSED" : ("PROBE FAILED with " + bad + " problem(s)"));
            if (bad == 0) Debug.Log("[TutorialFirstBeatProbe]\n" + log);
            else Debug.LogError("[TutorialFirstBeatProbe]\n" + log);
        }
    }
}
#endif
