using UnityEngine;

namespace BeatHero.Player
{
    // 방어 피드백 이펙트.
    // ReadyRing(SpriteRenderer+Animator) — 방어 대기 중 펄스 링.
    // AbsorbBurst(ParticleSystem) — 피해 흡수 시 버스트.
    public class BlockShieldEffect : MonoBehaviour
    {
        [Header("Ready Ring (Sprite)")]
        [SerializeField] private GameObject    _readyRing;     // 방어 대기 링 오브젝트 (SpriteRenderer + Animator)

        [Header("Absorb Burst (Particle)")]
        [SerializeField] private ParticleSystem _absorbBurst;  // 피해 흡수 버스트

        [Header("Absorb Burst Settings")]
        [SerializeField] private Color _absorbColorStart = new Color(1f, 1f, 1f, 1f);
        [SerializeField] private Color _absorbColorEnd   = new Color(0.4f, 0.8f, 1f, 0f);
        [SerializeField] private int   _absorbCount      = 28;
        [SerializeField] private float _absorbSpeed      = 2.2f;
        [SerializeField] private float _absorbLifetime   = 0.45f;
        [SerializeField] private float _absorbRadius     = 0.65f;

        public void ShowReady()
        {
            if (_readyRing != null)
                _readyRing.SetActive(true);
        }

        public void PlayAbsorb()
        {
            if (_readyRing != null)
                _readyRing.SetActive(false);

            ApplyAbsorbConfig();
            if (_absorbBurst != null)
            {
                _absorbBurst.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                _absorbBurst.Play();
            }
        }

        public void Hide()
        {
            if (_readyRing   != null) _readyRing.SetActive(false);
            if (_absorbBurst != null) _absorbBurst.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
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
