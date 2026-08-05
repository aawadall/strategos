// AudioService.cs
// #262 / #263 / #40: runtime audio entry point — music loop + one-shot SFX.
// Master bus stub uses AudioListener.volume; Music/SFX ride separate AudioSources.
// Batchmode / missing clips are silence-safe (no throw). Soundtrack slots: #253 / #254.

using UnityEngine;
using Strategos.Preferences;

namespace Strategos.Audio
{
    /// <summary>
    /// Plays music beds and one-shots. Lives on <see cref="UI.AppShell"/>; call
    /// <see cref="Ensure"/> once at boot. Resource names under <c>Resources/Audio/</c>.
    /// </summary>
    public sealed class AudioService : MonoBehaviour
    {
        /// <summary>Menu loop (#253) — <c>Resources/Audio/menu-loop</c>.</summary>
        public const string MenuLoopResource = "Audio/menu-loop";

        /// <summary>PLAY ambient bed (#254) — <c>Resources/Audio/play-ambient</c>.</summary>
        public const string PlayAmbientResource = "Audio/play-ambient";

        public static AudioService Instance { get; private set; }

        private AudioSource _music;
        private AudioSource _sfx;
        private float _musicVolume = 1f;
        private float _sfxVolume = 1f;
        private string _currentMusicResource;
        private AudioClip _uiClick;
        private AudioClip _uiSelect;

        /// <summary>Attach to <paramref name="host"/> if missing; returns the service.</summary>
        public static AudioService Ensure(GameObject host)
        {
            if (host == null) return null;
            var existing = host.GetComponent<AudioService>();
            if (existing != null)
            {
                Instance = existing;
                return existing;
            }

            var svc = host.AddComponent<AudioService>();
            Instance = svc;
            return svc;
        }

        private void Awake()
        {
            Instance = this;
            if (_music == null)
            {
                _music = gameObject.AddComponent<AudioSource>();
                _music.playOnAwake = false;
                _music.loop = true;
                _music.spatialBlend = 0f;
            }

            if (_sfx == null)
            {
                _sfx = gameObject.AddComponent<AudioSource>();
                _sfx.playOnAwake = false;
                _sfx.loop = false;
                _sfx.spatialBlend = 0f;
            }
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
            if (_uiClick != null) { Destroy(_uiClick); _uiClick = null; }
            if (_uiSelect != null) { Destroy(_uiSelect); _uiSelect = null; }
        }

        /// <summary>Procedural UI click (#250) — buttons / tabs.</summary>
        public void PlayUiClick(float volumeScale = 1f)
        {
            _uiClick ??= ProceduralSfx.Click();
            PlayOneShot(_uiClick, volumeScale);
        }

        /// <summary>Procedural unit-select blip (#250).</summary>
        public void PlayUiSelect(float volumeScale = 1f)
        {
            _uiSelect ??= ProceduralSfx.Select();
            PlayOneShot(_uiSelect, volumeScale);
        }

        /// <summary>
        /// Apply master / music / SFX from prefs (#264). Master drives
        /// <see cref="AudioListener.volume"/> (the mixer stub).
        /// </summary>
        public void ApplyPreferences(PlayerPreferences prefs)
        {
            if (prefs == null) prefs = new PlayerPreferences();
            AudioListener.volume = Mathf.Clamp01(prefs.MasterVolume);
            _musicVolume = Mathf.Clamp01(prefs.MusicVolume);
            _sfxVolume = Mathf.Clamp01(prefs.SfxVolume);
            if (_music != null) _music.volume = _musicVolume;
            if (_sfx != null) _sfx.volume = _sfxVolume;
        }

        /// <summary>
        /// Loop a Resources clip by name (no extension). Missing clip or batchmode → silence.
        /// Same resource already playing is a no-op.
        /// </summary>
        public void PlayMusicLoop(string resourceName)
        {
            if (string.IsNullOrEmpty(resourceName)) return;
            if (Application.isBatchMode) return;
            if (_music == null) return;

            if (_currentMusicResource == resourceName && _music.isPlaying) return;

            var clip = Resources.Load<AudioClip>(resourceName);
            if (clip == null)
            {
                Debug.LogWarning($"[AudioService] no clip at Resources/{resourceName}");
                _music.Stop();
                _currentMusicResource = null;
                return;
            }

            _music.clip = clip;
            _music.volume = _musicVolume;
            _music.loop = true;
            _music.Play();
            _currentMusicResource = resourceName;
        }

        /// <summary>Stop the music bed without clearing volume prefs.</summary>
        public void StopMusic()
        {
            if (_music == null) return;
            _music.Stop();
            _currentMusicResource = null;
        }

        /// <summary>
        /// Play a one-shot (#263). Null clip / batchmode → no-op (probe-safe silence).
        /// </summary>
        public void PlayOneShot(AudioClip clip, float volumeScale = 1f)
        {
            if (clip == null || _sfx == null) return;
            if (Application.isBatchMode) return;
            _sfx.PlayOneShot(clip, Mathf.Clamp01(volumeScale) * _sfxVolume);
        }

        /// <summary>Load from Resources and one-shot; missing → warning, no throw.</summary>
        public void PlayOneShotResource(string resourceName, float volumeScale = 1f)
        {
            if (string.IsNullOrEmpty(resourceName)) return;
            var clip = Resources.Load<AudioClip>(resourceName);
            if (clip == null)
            {
                Debug.LogWarning($"[AudioService] no one-shot at Resources/{resourceName}");
                return;
            }

            PlayOneShot(clip, volumeScale);
        }

        /// <summary>Which music resource is loaded, or null.</summary>
        public string CurrentMusicResource => _currentMusicResource;

        /// <summary>True when the music source reports playing (false in batchmode).</summary>
        public bool IsMusicPlaying => _music != null && _music.isPlaying;
    }
}
