using BeatHero.Audio;
using BeatHero.Core;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace BeatHero.UI
{
    public class SettingsPanel : MonoBehaviour
    {
        [Header("Audio")]
        [SerializeField] private Slider _masterSlider;
        [SerializeField] private Slider _bgmSlider;
        [SerializeField] private Slider _sfxSlider;

        [Header("Sync")]
        [SerializeField] private Slider _judgmentSyncSlider;   // wholeNumbers, -5~5 (1스텝=10ms)
        [SerializeField] private TMP_Text _judgmentSyncLabel;  // 단위 없이 정수만 표시 (예: "-3", "0", "+2")
        [SerializeField] private Button _judgmentSyncMinusButton;
        [SerializeField] private Button _judgmentSyncPlusButton;

        [SerializeField] private Slider _audioSyncSlider;      // wholeNumbers, -40~40 (1스텝=0.05초)
        [SerializeField] private TMP_Text _audioSyncLabel;     // 초 단위 표시 (예: "+0.50s")
        [SerializeField] private Button _audioSyncMinusButton;
        [SerializeField] private Button _audioSyncPlusButton;

        [Header("Video")]
        [SerializeField] private TMP_Dropdown _resolutionDropdown;
        [SerializeField] private Toggle _fullscreenToggle;

        [Header("Navigation")]
        [SerializeField] private Button _closeButton;

        private Resolution[] _resolutions;

        private const string PREF_MASTER     = "MasterVolume";
        private const string PREF_BGM        = "BGMVolume";
        private const string PREF_SFX        = "SFXVolume";
        private const string PREF_RESOLUTION = "ResolutionIndex";
        private const string PREF_FULLSCREEN = "Fullscreen";

        private const float JUDGMENT_STEP_SEC = 0.01f; // 10ms
        private const float AUDIO_STEP_SEC    = 0.05f; // 50ms

        [Header("Canvas")]
        [SerializeField] private GameObject _canvasRoot;

        private bool _initialized;

        private void EnsureInitialized()
        {
            if (_initialized) return;
            _initialized = true;

            BuildResolutionDropdown();
            LoadSettings();

            _masterSlider.onValueChanged.AddListener(v => AudioManager.Instance?.SetMasterVolume(v));
            _bgmSlider.onValueChanged.AddListener(v => AudioManager.Instance?.SetBGMVolume(v));
            _sfxSlider.onValueChanged.AddListener(v => AudioManager.Instance?.SetSFXVolume(v));

            _judgmentSyncSlider.wholeNumbers = true;
            _judgmentSyncSlider.minValue = -5;
            _judgmentSyncSlider.maxValue = 5;
            _judgmentSyncSlider.onValueChanged.AddListener(OnJudgmentSyncChanged);
            _judgmentSyncMinusButton.onClick.AddListener(() => _judgmentSyncSlider.value -= 1);
            _judgmentSyncPlusButton.onClick.AddListener(() => _judgmentSyncSlider.value += 1);

            _audioSyncSlider.wholeNumbers = true;
            _audioSyncSlider.minValue = -40;
            _audioSyncSlider.maxValue = 40;
            _audioSyncSlider.onValueChanged.AddListener(OnAudioSyncChanged);
            _audioSyncMinusButton.onClick.AddListener(() => _audioSyncSlider.value -= 1);
            _audioSyncPlusButton.onClick.AddListener(() => _audioSyncSlider.value += 1);

            _resolutionDropdown.onValueChanged.AddListener(ApplyResolution);
            _fullscreenToggle.onValueChanged.AddListener(v =>
            {
                Screen.fullScreen = v;
                PlayerPrefs.SetInt(PREF_FULLSCREEN, v ? 1 : 0);
            });

            if (_closeButton != null) _closeButton.onClick.AddListener(Hide);
        }

        public void Show()
        {
            if (_canvasRoot != null)
                _canvasRoot.SetActive(true);
            else
            {
                // _canvasRoot 미연결 시 부모 Canvas를 자동으로 활성화 (prefab instance 대응)
                var parentCanvas = GetComponentInParent<Canvas>(true);
                if (parentCanvas != null) parentCanvas.gameObject.SetActive(true);
            }
            EnsureInitialized();
            gameObject.SetActive(true);
        }

        public void Hide()
        {
            gameObject.SetActive(false);
            if (_canvasRoot != null)
                _canvasRoot.SetActive(false);
            else
            {
                var parentCanvas = GetComponentInParent<Canvas>(true);
                if (parentCanvas != null) parentCanvas.gameObject.SetActive(false);
            }
        }

        private void BuildResolutionDropdown()
        {
            _resolutions = Screen.resolutions;
            var options = new List<TMP_Dropdown.OptionData>();
            foreach (var r in _resolutions)
                options.Add(new TMP_Dropdown.OptionData($"{r.width}x{r.height}"));
            _resolutionDropdown.ClearOptions();
            _resolutionDropdown.AddOptions(options);
        }

        private void LoadSettings()
        {
            _masterSlider.SetValueWithoutNotify(PlayerPrefs.GetFloat(PREF_MASTER, 1f));
            _bgmSlider.SetValueWithoutNotify(PlayerPrefs.GetFloat(PREF_BGM, 1f));
            _sfxSlider.SetValueWithoutNotify(PlayerPrefs.GetFloat(PREF_SFX, 1f));

            int judgmentSteps = Mathf.RoundToInt(SyncSettings.JudgmentOffsetSec / JUDGMENT_STEP_SEC);
            _judgmentSyncSlider.SetValueWithoutNotify(judgmentSteps);
            UpdateJudgmentSyncLabel(judgmentSteps);

            int audioSteps = Mathf.RoundToInt(SyncSettings.AudioOffsetSec / AUDIO_STEP_SEC);
            _audioSyncSlider.SetValueWithoutNotify(audioSteps);
            UpdateAudioSyncLabel(audioSteps);

            int savedRes = PlayerPrefs.GetInt(PREF_RESOLUTION, _resolutions.Length - 1);
            _resolutionDropdown.SetValueWithoutNotify(Mathf.Clamp(savedRes, 0, _resolutions.Length - 1));

            bool savedFullscreen = PlayerPrefs.GetInt(PREF_FULLSCREEN, 1) == 1;
            _fullscreenToggle.SetIsOnWithoutNotify(savedFullscreen);
        }

        private void ApplyResolution(int index)
        {
            if (_resolutions == null || index < 0 || index >= _resolutions.Length) return;
            var r = _resolutions[index];
            Screen.SetResolution(r.width, r.height, Screen.fullScreen);
            PlayerPrefs.SetInt(PREF_RESOLUTION, index);
        }

        private void OnJudgmentSyncChanged(float steps)
        {
            int s = Mathf.RoundToInt(steps);
            SyncSettings.SetJudgmentOffsetMs(s * JUDGMENT_STEP_SEC * 1000f);
            UpdateJudgmentSyncLabel(s);
        }

        private void UpdateJudgmentSyncLabel(int steps)
        {
            if (_judgmentSyncLabel != null)
                _judgmentSyncLabel.text = steps.ToString("+0;-0;0");
        }

        private void OnAudioSyncChanged(float steps)
        {
            int s = Mathf.RoundToInt(steps);
            SyncSettings.SetAudioOffsetMs(s * AUDIO_STEP_SEC * 1000f);
            UpdateAudioSyncLabel(s);
        }

        private void UpdateAudioSyncLabel(int steps)
        {
            if (_audioSyncLabel != null)
                _audioSyncLabel.text = (steps * AUDIO_STEP_SEC).ToString("+0.00;-0.00;0.00") + "s";
        }
    }
}
