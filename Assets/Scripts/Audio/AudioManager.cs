using System.Collections;
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

        // DSP 예약용 AudioSource 2개 (교대로 사용해 짧은 음표 간격 폴리포니 대응)
        private AudioSource[] _scheduledSfxSources;
        private int           _scheduledSfxIndex;

        private void Awake()
        {
            if (Instance != null) { Destroy(gameObject); return; }
            Instance = this;
            transform.SetParent(null);
            DontDestroyOnLoad(gameObject);

            _scheduledSfxSources = new AudioSource[2];
            for (int i = 0; i < 2; i++)
            {
                var src = gameObject.AddComponent<AudioSource>();
                src.outputAudioMixerGroup = _sfxGroup;
                src.playOnAwake = false;
                _scheduledSfxSources[i] = src;
            }
        }

        // AudioMixer 노출 파라미터는 믹서 초기화 다음 프레임부터 접근 가능 —
        // Start에서 바로 SetFloat하면 "Exposed name does not exist"로 실패한다.
        private void Start() => StartCoroutine(ApplyVolumesNextFrame());

        private IEnumerator ApplyVolumesNextFrame()
        {
            yield return null; // 믹서 초기화 대기(1프레임)
            ApplySavedVolumes();
        }

        public void PlaySFX(AudioClip clip)
        {
            if (clip == null || _sfxSource == null) return;
            _sfxSource.PlayOneShot(clip);
        }

        // DSP 정확 타이밍으로 SFX 예약 — BGM PlayScheduled와 동일한 오디오 클럭 기준
        public void PlaySFXScheduled(AudioClip clip, double dspTime)
        {
            if (clip == null) return;
            var src = _scheduledSfxSources[_scheduledSfxIndex & 1];
            _scheduledSfxIndex++;
            src.clip = clip;
            src.PlayScheduled(dspTime);
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
