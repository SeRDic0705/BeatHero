using BeatHero.Audio;
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

        private void Start()
        {
            BuildResolutionDropdown();
            LoadSettings();

            _masterSlider.onValueChanged.AddListener(v => AudioManager.Instance?.SetMasterVolume(v));
            _bgmSlider.onValueChanged.AddListener(v => AudioManager.Instance?.SetBGMVolume(v));
            _sfxSlider.onValueChanged.AddListener(v => AudioManager.Instance?.SetSFXVolume(v));

            _resolutionDropdown.onValueChanged.AddListener(ApplyResolution);
            _fullscreenToggle.onValueChanged.AddListener(v =>
            {
                Screen.fullScreen = v;
                PlayerPrefs.SetInt(PREF_FULLSCREEN, v ? 1 : 0);
            });

            if (_closeButton != null) _closeButton.onClick.AddListener(Hide);

            gameObject.SetActive(false);
        }

        public void Show() => gameObject.SetActive(true);
        public void Hide() => gameObject.SetActive(false);

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
    }
}
