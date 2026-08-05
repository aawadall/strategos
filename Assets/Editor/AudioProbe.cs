// AudioProbe.cs
// #265 / #40: AudioService is silence-safe in batchmode; one-shot API accepts a procedural
// clip without throwing; Resources beds for menu/PLAY resolve when present.
//
// Menu:  Strategos > Probe Audio
// Batch: -executeMethod Strategos.Editor.AudioProbe.Run

#if UNITY_EDITOR
using System.Text;
using UnityEditor;
using UnityEngine;
using Strategos.Audio;
using Strategos.Preferences;

namespace Strategos.Editor
{
    public static class AudioProbe
    {
        [MenuItem("Strategos/Probe Audio")]
        public static void Run()
        {
            var log = new StringBuilder();
            int bad = 0;

            bad += CheckServiceSilenceSafe(log);
            bad += CheckOneShotApi(log);
            bad += CheckProceduralSfx(log);
            bad += CheckShippedBeds(log);
            bad += CheckVolumePrefs(log);

            log.AppendLine(bad == 0 ? "PROBE PASSED" : $"PROBE FAILED with {bad} problem(s)");
            if (bad == 0) Debug.Log("[AudioProbe]\n" + log);
            else Debug.LogError("[AudioProbe]\n" + log);
        }

        private static int CheckServiceSilenceSafe(StringBuilder log)
        {
            var go = new GameObject("audio-probe-svc");
            try
            {
                var svc = AudioService.Ensure(go);
                if (svc == null)
                {
                    log.AppendLine("  FAIL AudioService.Ensure returned null");
                    return 1;
                }

                // Missing resource must not throw (#265).
                svc.PlayMusicLoop("Audio/does-not-exist");
                svc.PlayOneShotResource("Audio/does-not-exist");
                svc.StopMusic();
                log.AppendLine("  silence-safe missing clip  ok");
                return 0;
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        private static int CheckOneShotApi(StringBuilder log)
        {
            var go = new GameObject("audio-probe-oneshot");
            AudioClip clip = null;
            try
            {
                var svc = AudioService.Ensure(go);
                // 1-sample silence — proves PlayOneShot accepts a real clip (#263).
                clip = AudioClip.Create("probe-silence", 1, 1, 44100, false);
                svc.PlayOneShot(clip);
                svc.PlayOneShot(null); // null → no-op
                log.AppendLine("  PlayOneShot API  ok");
                return 0;
            }
            finally
            {
                if (clip != null) Object.DestroyImmediate(clip);
                Object.DestroyImmediate(go);
            }
        }

        private static int CheckProceduralSfx(StringBuilder log)
        {
            var go = new GameObject("audio-probe-sfx");
            var clips = new AudioClip[5];
            try
            {
                var svc = AudioService.Ensure(go);
                svc.PlayUiClick();
                svc.PlayUiSelect();
                svc.PlayOrderIssued();
                svc.PlayOrderRejected();
                svc.PlayCombatFire();

                clips[0] = ProceduralSfx.Click();
                clips[1] = ProceduralSfx.Select();
                clips[2] = ProceduralSfx.OrderIssued();
                clips[3] = ProceduralSfx.OrderRejected();
                clips[4] = ProceduralSfx.CombatFire();
                for (int i = 0; i < clips.Length; i++)
                {
                    if (clips[i] == null || clips[i].samples < 10)
                    {
                        log.AppendLine($"  FAIL ProceduralSfx clip[{i}] empty");
                        return 1;
                    }
                }

                log.AppendLine(
                    $"  procedural SFX  ok " +
                    $"(issued {clips[2].samples}, rejected {clips[3].samples}, " +
                    $"fire {clips[4].samples} smp)");
                return 0;
            }
            finally
            {
                for (int i = 0; i < clips.Length; i++)
                    if (clips[i] != null) Object.DestroyImmediate(clips[i]);
                Object.DestroyImmediate(go);
            }
        }

        private static int CheckShippedBeds(StringBuilder log)
        {
            int bad = 0;
            var menu = Resources.Load<AudioClip>(AudioService.MenuLoopResource);
            var play = Resources.Load<AudioClip>(AudioService.PlayAmbientResource);
            if (menu == null)
            {
                log.AppendLine($"  FAIL missing Resources/{AudioService.MenuLoopResource}");
                bad++;
            }
            else
                log.AppendLine($"  menu-loop: {menu.length:0.0}s @ {menu.frequency} Hz");

            if (play == null)
            {
                log.AppendLine($"  FAIL missing Resources/{AudioService.PlayAmbientResource}");
                bad++;
            }
            else
                log.AppendLine($"  play-ambient: {play.length:0.0}s @ {play.frequency} Hz");

            return bad;
        }

        private static int CheckVolumePrefs(StringBuilder log)
        {
            var go = new GameObject("audio-probe-vol");
            try
            {
                var svc = AudioService.Ensure(go);
                var prefs = new PlayerPreferences
                {
                    MasterVolume = 0.5f,
                    MusicVolume = 0.25f,
                    SfxVolume = 0.75f,
                };
                svc.ApplyPreferences(prefs);
                if (!Mathf.Approximately(AudioListener.volume, 0.5f))
                {
                    log.AppendLine(
                        $"  FAIL AudioListener.volume={AudioListener.volume}, expected 0.5");
                    return 1;
                }

                log.AppendLine("  volume prefs → AudioListener  ok");
                return 0;
            }
            finally
            {
                Object.DestroyImmediate(go);
                AudioListener.volume = 1f;
            }
        }
    }
}
#endif
