using BeatHero.Core;
using UnityEngine;

namespace BeatHero.UI
{
    // 항상-활성 GO에 부착. PauseMenuCanvas 활성화 + PauseMenu.Open() 트리거 담당.
    public class PauseController : MonoBehaviour
    {
        [SerializeField] private GameObject  _pauseMenuCanvas;
        [SerializeField] private PauseMenu   _pauseMenu;

        private void Start()
        {
            InputReader.Instance.OnPausePressed += OnPausePressed;
        }

        private void OnDestroy()
        {
            if (InputReader.Instance != null)
                InputReader.Instance.OnPausePressed -= OnPausePressed;
        }

        private void OnPausePressed()
        {
            if (_pauseMenuCanvas == null || _pauseMenu == null) return;
            _pauseMenuCanvas.SetActive(true);
            _pauseMenu.Open();
        }
    }
}
