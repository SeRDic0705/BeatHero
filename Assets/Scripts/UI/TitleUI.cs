using BeatHero.Core;
using UnityEngine;
using UnityEngine.UI;

namespace BeatHero.UI
{
    public class TitleUI : MonoBehaviour
    {
        [SerializeField] private Button _startButton;
        [SerializeField] private Button _settingsButton;
        [SerializeField] private SettingsPanel _settingsPanel;

        private void Start()
        {
            _startButton.onClick.AddListener(OnStartPressed);
            _settingsButton.onClick.AddListener(OnSettingsPressed);
        }

        private void OnDestroy()
        {
            _startButton.onClick.RemoveListener(OnStartPressed);
            _settingsButton.onClick.RemoveListener(OnSettingsPressed);
        }

        private void OnStartPressed() => SceneLoader.Instance.LoadGame();

        private void OnSettingsPressed()
        {
            if (_settingsPanel != null) _settingsPanel.Show();
        }
    }
}
