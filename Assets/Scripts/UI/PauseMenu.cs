using System.Collections;
using BeatHero.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace BeatHero.UI
{
    // PauseMenuCanvas 위에 있는 순수 View. 이벤트 구독은 PauseController가 담당.
    public class PauseMenu : MonoBehaviour
    {
        [SerializeField] private GameObject    _menuRoot;
        [SerializeField] private Button        _resumeButton;
        [SerializeField] private Button        _settingsButton;
        [SerializeField] private Button        _mainMenuButton;
        [SerializeField] private SettingsPanel    _settingsPanel;
        [SerializeField] private Conductor        _conductor;
        [SerializeField] private TextMeshProUGUI  _countdownText;

        private void Awake()
        {
            _resumeButton.onClick.AddListener(Close);
            _settingsButton.onClick.AddListener(OpenSettings);
            _mainMenuButton.onClick.AddListener(GoToMainMenu);
            _menuRoot.SetActive(false);
        }

        private void OnDestroy()
        {
            _resumeButton.onClick.RemoveAllListeners();
            _settingsButton.onClick.RemoveAllListeners();
            _mainMenuButton.onClick.RemoveAllListeners();
        }

        public void Open()
        {
            _menuRoot.SetActive(true);
            _conductor?.Pause();
            Time.timeScale = 0f;
            InputReader.Instance?.SwitchToUIMap();
        }

        public void Close() => StartCoroutine(CloseWithCountdown());

        private IEnumerator CloseWithCountdown()
        {
            _settingsPanel?.Hide();
            _menuRoot.SetActive(false);
            _countdownText.gameObject.SetActive(true);
            for (int i = 3; i >= 1; i--)
            {
                _countdownText.text = i.ToString();
                yield return new WaitForSecondsRealtime(1f);
            }
            _countdownText.gameObject.SetActive(false);
            _conductor?.Resume();
            Time.timeScale = 1f;
            InputReader.Instance?.SwitchToGameMap();
            gameObject.SetActive(false);
        }

        private void OpenSettings()
        {
            _settingsPanel?.Show();
        }

        private void GoToMainMenu()
        {
            Time.timeScale = 1f;
            SceneLoader.Instance.LoadTitle();
        }
    }
}
