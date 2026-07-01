using System.Collections;
using BeatHero.Combat;
using TMPro;
using UnityEngine;

namespace BeatHero.UI
{
    // BeatBar 위쪽에 표시하는 타이밍 판정 인디케이터.
    // BattleStateMachine.OnTimingMissed 이벤트 구독 → "Fast" / "Slow" / "Perfect!" 텍스트를
    // 위로 드리프트하며 페이드아웃. 겹침 시 즉시 인터럽트 후 처음부터 재시작.
    public class TimingIndicator : MonoBehaviour
    {
        [SerializeField] private TMP_Text           _text;
        [SerializeField] private BattleStateMachine _battleSM;

        [Header("Colors")]
        [SerializeField] private Color _fastColor    = new Color(1f, 0.549f, 0f, 1f);    // #FF8C00
        [SerializeField] private Color _slowColor    = new Color(0f, 0.749f, 1f, 1f);    // #00BFFF
        [SerializeField] private Color _perfectColor = new Color(1f, 0.843f, 0f, 1f);    // #FFD700

        [Header("Animation")]
        [SerializeField] private float _driftY    = 60f;   // 위로 이동할 픽셀 거리
        [SerializeField] private float _duration  = 0.8f;  // 총 애니메이션 시간(초)

        private Coroutine   _animRoutine;
        private Vector2     _basePosition;   // RectTransform 기준 시작 위치

        private void Awake()
        {
            _basePosition = ((RectTransform)transform).anchoredPosition;
            if (_text != null) _text.alpha = 0f;
        }

        private void OnEnable()
        {
            if (_battleSM != null) _battleSM.OnTimingMissed += Show;
        }

        private void OnDisable()
        {
            if (_battleSM != null) _battleSM.OnTimingMissed -= Show;
        }

        public void Show(TimingResult result)
        {
            if (_text == null) return;

            if (_animRoutine != null) StopCoroutine(_animRoutine);

            switch (result)
            {
                case TimingResult.Fast:    _text.text = "Fast";    _text.color = _fastColor;    break;
                case TimingResult.Slow:    _text.text = "Slow";    _text.color = _slowColor;    break;
                case TimingResult.Perfect: _text.text = "Perfect!"; _text.color = _perfectColor; break;
            }
            ((RectTransform)transform).anchoredPosition = _basePosition;

            _animRoutine = StartCoroutine(PlayAnim());
        }

        private IEnumerator PlayAnim()
        {
            float elapsed = 0f;
            var rt = (RectTransform)transform;

            while (elapsed < _duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / _duration);

                rt.anchoredPosition = _basePosition + new Vector2(0f, _driftY * t);

                var c = _text.color;
                c.a = 1f - t;
                _text.color = c;

                yield return null;
            }

            var final = _text.color;
            final.a = 0f;
            _text.color = final;
            _animRoutine = null;
        }
    }
}
