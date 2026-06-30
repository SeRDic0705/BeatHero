using System.Collections;
using UnityEngine;

namespace BeatHero.Player
{
    // 방어 피드백 이펙트.
    // GuardRing(SpriteRenderer+Animator) — 가드 성공 시 확장 페이드 원샷.
    // AbsorbBurst(ParticleSystem) — 가드 성공 시 파티클 버스트.
    // 둘 다 평소 비활성, PlayAbsorb() 호출 시에만 활성화.
    public class BlockShieldEffect : MonoBehaviour
    {
        [Header("Guard Ring (Sprite)")]
        [SerializeField] private GameObject    _guardRing;     // 가드 성공 링 오브젝트 (SpriteRenderer + Animator)

        [Header("Absorb Burst (Particle)")]
        [SerializeField] private ParticleSystem _absorbBurst;  // 피해 흡수 버스트

        [Header("Absorb Burst Settings")]
        [SerializeField] private Color _absorbColorStart = new Color(1f, 1f, 1f, 1f);
        [SerializeField] private Color _absorbColorEnd   = new Color(0.4f, 0.8f, 1f, 0f);
        [SerializeField] private int   _absorbCount      = 28;
        [SerializeField] private float _absorbSpeed      = 2.2f;
        [SerializeField] private float _absorbLifetime   = 0.45f;
        [SerializeField] private float _absorbRadius     = 0.65f;

        [Header("Guard Ring Settings")]
        [SerializeField] private float _guardRingDuration = 0.5f;

        private void Start()
        {
            if (_guardRing   != null) _guardRing.SetActive(false);
            if (_absorbBurst != null) _absorbBurst.gameObject.SetActive(false);
        }

        // 가드 발동 시 — 시각 피드백 없음 (오디오는 BattleStateMachine에서 처리)
        public void ShowReady() { }

        // 가드 성공(피해 흡수) 시 — GuardRing + AbsorbBurst 동시 재생
        public void PlayAbsorb()
        {
            if (_guardRing != null)
            {
                _guardRing.SetActive(true);
                var animator = _guardRing.GetComponent<Animator>();
                if (animator != null) animator.Play(0, 0, 0f);
                StartCoroutine(DisableAfterDelay(_guardRing, _guardRingDuration));
            }

            if (_absorbBurst != null)
            {
                _absorbBurst.gameObject.SetActive(true);
                ApplyAbsorbConfig();
                _absorbBurst.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                _absorbBurst.Play();
                StartCoroutine(DisableAbsorbBurst(_absorbLifetime + 0.1f));
            }
        }

        public void Hide()
        {
            if (_guardRing   != null) _guardRing.SetActive(false);
            if (_absorbBurst != null) _absorbBurst.gameObject.SetActive(false);
        }

        private IEnumerator DisableAfterDelay(GameObject go, float delay)
        {
            yield return new WaitForSeconds(delay);
            if (go != null) go.SetActive(false);
        }

        private IEnumerator DisableAbsorbBurst(float delay)
        {
            yield return new WaitForSeconds(delay);
            if (_absorbBurst != null)
                _absorbBurst.gameObject.SetActive(false);
        }

        private void ApplyAbsorbConfig()
        {
            if (_absorbBurst == null) return;

            var main = _absorbBurst.main;
            main.loop          = false;
            main.startLifetime = _absorbLifetime;
            main.startSpeed    = _absorbSpeed;
            main.startSize     = new ParticleSystem.MinMaxCurve(0.08f, 0.14f);

            var colorModule = _absorbBurst.colorOverLifetime;
            colorModule.enabled = true;
            var grad = new Gradient();
            grad.SetKeys(
                new GradientColorKey[]
                {
                    new GradientColorKey(_absorbColorStart, 0f),
                    new GradientColorKey(_absorbColorEnd,   1f)
                },
                new GradientAlphaKey[]
                {
                    new GradientAlphaKey(1f, 0f),
                    new GradientAlphaKey(0f, 1f)
                }
            );
            colorModule.color = new ParticleSystem.MinMaxGradient(grad);

            var emission = _absorbBurst.emission;
            emission.rateOverTime = 0;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, _absorbCount) });

            var shape = _absorbBurst.shape;
            shape.enabled         = true;
            shape.shapeType       = ParticleSystemShapeType.Circle;
            shape.radius          = _absorbRadius;
            shape.radiusThickness = 0.08f;
        }
    }
}
