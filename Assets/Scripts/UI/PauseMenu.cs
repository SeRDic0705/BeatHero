using BeatHero.Core;
using UnityEngine;
using UnityEngine.UI;

namespace BeatHero.UI
{
    public class PauseMenu : MonoBehaviour
    {
        [SerializeField] private GameObject _menuRoot;
        [SerializeField] private Button _resumeButton;
        [SerializeField] private Button _settingsButton;
        [SerializeField] private Button _mainMenuButton;
        [SerializeField] private SettingsPanel _settingsPanel;
        [SerializeField] private Conductor _conductor;

        private bool _isPaused;

        private void Start()
        {
            _resumeButton.onClick.AddListener(Resume);
            _settingsButton.onClick.AddListener(OpenSettings);
            _mainMenuButton.onClick.AddListener(GoToMainMenu);
            _menuRoot.SetActive(false);
            InputReader.Instance.OnPausePressed += Pause;
        }

        private void OnDestroy()
        {
            _resumeButton.onClick.RemoveAllListeners();
            _settingsButton.onClick.RemoveAllListeners();
            _mainMenuButton.onClick.RemoveAllListeners();
            if (InputReader.Instance != null)
                InputReader.Instance.OnPausePressed -= Pause;
        }

        private void Pause()
        {
            _isPaused = true;
            _menuRoot.SetActive(true);
            _conductor?.Pause();
            Time.timeScale = 0f;
            InputReader.Instance.SwitchToUIMap();
        }

        private void Resume()
        {
            _isPaused = false;
            _settingsPanel?.Hide();
            _menuRoot.SetActive(false);
            _conductor?.Resume();
            Time.timeScale = 1f;
            InputReader.Instance.SwitchToGameMap();
        }

        private void OpenSettings()
        {
            if (_settingsPanel != null) _settingsPanel.Show();
        }

        private void GoToMainMenu()
        {
            Time.timeScale = 1f;
            SceneLoader.Instance.LoadTitle();
        }
    }
}
