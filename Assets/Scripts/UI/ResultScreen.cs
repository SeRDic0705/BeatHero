using System;
using BeatHero.Core;
using TMPro;
using UnityEngine;

namespace BeatHero.UI
{
    public class ResultScreen : MonoBehaviour
    {
        public enum Mode { BossClear, FinalClear, GameOver }

        [SerializeField] private GameObject _panel;
        [SerializeField] private TMP_Text   _titleText;
        [SerializeField] private TMP_Text   _clearedFloorsText;
        [SerializeField] private TMP_Text   _totalBeatsText;

        [Header("Buttons")]
        [SerializeField] private GameObject _confirmButton;   // BossClear용
        [SerializeField] private GameObject _retryButton;     // GameOver용
        [SerializeField] private GameObject _titleButton;     // FinalClear·GameOver 공용

        private Action _onConfirm;
        private Action _onRetry;
        private Action _onTitle;

        private void Awake()
        {
            _panel.SetActive(false);
        }

        public void Show(Mode mode, int clearedFloors, int totalBeats,
                         Action onConfirm = null, Action onRetry = null, Action onTitle = null)
        {
            _onConfirm = onConfirm;
            _onRetry   = onRetry;
            _onTitle   = onTitle;

            _clearedFloorsText.text = $"클리어 층: {clearedFloors}층";
            _totalBeatsText.text    = $"총 비트: {totalBeats}";

            _confirmButton.SetActive(mode == Mode.BossClear);
            _retryButton.SetActive(mode == Mode.GameOver);
            _titleButton.SetActive(mode == Mode.FinalClear || mode == Mode.GameOver);

            _titleText.text = mode switch
            {
                Mode.BossClear  => "보스 클리어!",
                Mode.FinalClear => "엔딩!",
                Mode.GameOver   => "게임 오버",
                _               => ""
            };

            _panel.SetActive(true);
            InputReader.Instance?.SwitchToUIMap();
        }

        public void Hide()
        {
            _panel.SetActive(false);
        }

        // 버튼 OnClick에서 호출
        public void OnConfirmClicked()
        {
            Hide();
            _onConfirm?.Invoke();
        }

        public void OnRetryClicked()
        {
            Hide();
            _onRetry?.Invoke();
        }

        public void OnTitleClicked()
        {
            Hide();
            _onTitle?.Invoke();
        }
    }
}
