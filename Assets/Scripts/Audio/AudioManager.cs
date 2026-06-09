using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.Audio;

namespace BeatHero.Audio
{
    public class AudioManager : MonoBehaviour
    {
        public static AudioManager Instance { get; private set; }

        [BoxGroup("Mixer")]
        [SerializeField] private AudioMixer _mixer;
        [BoxGroup("Mixer")]
        [SerializeField] private AudioMixerGroup _bgmGroup;
        [BoxGroup("Mixer")]
        [SerializeField] private AudioMixerGroup _sfxGroup;
        [BoxGroup("SFX")]
        [SerializeField] private AudioSource _sfxSource;

        private const string PREF_MASTER = "MasterVolume";
        private const string PREF_BGM    = "BGMVolume";
        private const string PREF_SFX    = "SFXVolume";

        private void Awake()
        {
            if (Instance != null) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);
            ApplySavedVolumes();
        }

        public void PlaySFX(AudioClip clip)
        {
            if (clip == null || _sfxSource == null) return;
            _sfxSource.PlayOneShot(clip);
        }

        public void SetMasterVolume(float normalized)
        {
            PlayerPrefs.SetFloat(PREF_MASTER, normalized);
            ApplyVolume("MasterVolume", normalized);
        }

        public void SetBGMVolume(float normalized)
        {
            PlayerPrefs.SetFloat(PREF_BGM, normalized);
            ApplyVolume("BGMVolume", normalized);
        }

        public void SetSFXVolume(float normalized)
        {
            PlayerPrefs.SetFloat(PREF_SFX, normalized);
            ApplyVolume("SFXVolume", normalized);
        }

        public AudioMixerGroup BGMGroup => _bgmGroup;
        public AudioMixerGroup SFXGroup => _sfxGroup;

        private void ApplySavedVolumes()
        {
            ApplyVolume("MasterVolume", PlayerPrefs.GetFloat(PREF_MASTER, 1f));
            ApplyVolume("BGMVolume",    PlayerPrefs.GetFloat(PREF_BGM, 1f));
            ApplyVolume("SFXVolume",    PlayerPrefs.GetFloat(PREF_SFX, 1f));
        }

        private void ApplyVolume(string param, float normalized)
        {
            if (_mixer == null) return;
            float db = normalized > 0.0001f ? Mathf.Log10(normalized) * 20f : -80f;
            _mixer.SetFloat(param, db);
        }
    }
}
