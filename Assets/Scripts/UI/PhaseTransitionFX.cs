using System.Collections;
using BeatHero.Combat;
using UnityEngine;
using UnityEngine.UI;

namespace BeatHero.UI
{
    // 보스 페이즈 전환 완충 마디 박자마다 전체화면 플래시 연출.
    // BattleStateMachine.OnBossPhaseChanged 구독 — 씬에 배치된 BattleStateMachine을 자동 탐색.
    public class PhaseTransitionFX : MonoBehaviour
    {
        [SerializeField] private Image _flashImage;
        [SerializeField] private Color _flashColor        = Color.white;
        [SerializeField] private float _flashAlpha        = 0.6f;
        [SerializeField] private float _flashDuration     = 0.25f;

        private BattleStateMachine _battle;

        private void Awake()
        {
            _battle = Object.FindAnyObjectByType<BattleStateMachine>();
            if (_battle != null)
                _battle.OnBossPhaseChanged += OnPhaseChanged;

            if (_flashImage != null)
            {
                var c = _flashColor;
                c.a = 0f;
                _flashImage.color = c;
                _flashImage.raycastTarget = false;
            }
        }

        private void OnDestroy()
        {
            if (_battle != null)
                _battle.OnBossPhaseChanged -= OnPhaseChanged;
        }

        private void OnPhaseChanged() => StartCoroutine(FlashRoutine());

        private IEnumerator FlashRoutine()
        {
            if (_flashImage == null) yield break;

            float elapsed = 0f;
            Color peak = _flashColor;
            peak.a = _flashAlpha;

            while (elapsed < _flashDuration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / _flashDuration;
                // bell curve: 0→peak→0
                float alpha = _flashAlpha * Mathf.Sin(t * Mathf.PI);
                var c = _flashColor;
                c.a = alpha;
                _flashImage.color = c;
                yield return null;
            }

            var zero = _flashColor;
            zero.a = 0f;
            _flashImage.color = zero;
        }
    }
}
