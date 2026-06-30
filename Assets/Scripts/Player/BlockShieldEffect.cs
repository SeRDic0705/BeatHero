using UnityEngine;

namespace BeatHero.Player
{
    // 방어 성공 시 플레이어 주변 쉴드 버블 파티클 제어.
    // 자식 ParticleSystem 두 개(_readyRing, _absorbBurst)를 인스펙터에서 연결.
    // ShowReady() → 방어 대기 링, PlayAbsorb() → 피해 흡수 버스트.
    public class BlockShieldEffect : MonoBehaviour
    {
        [Header("Particle Systems")]
        [SerializeField] private ParticleSystem _readyRing;    // 방어 대기 상태 링
        [SerializeField] private ParticleSystem _absorbBurst;  // 피해 흡수 버스트

        [Header("Ready Ring")]
        [SerializeField] private Color _readyColor       = new Color(0.4f, 0.8f, 1f, 0.7f);
        [SerializeField] private float _readyRadius      = 0.65f;
        [SerializeField] private float _readyEmission    = 18f;

        [Header("Absorb Burst")]
        [SerializeField] private Color _absorbColorStart = new Color(1f, 1f, 1f, 1f);
        [SerializeField] private Color _absorbColorEnd   = new Color(0.4f, 0.8f, 1f, 0f);
        [SerializeField] private int   _absorbCount      = 28;
        [SerializeField] private float _absorbSpeed      = 2.2f;
        [SerializeField] private float _absorbLifetime   = 0.45f;

        public void ShowReady()
        {
            ApplyReadyConfig();
            if (_readyRing != null && !_readyRing.isPlaying)
                _readyRing.Play();
        }

        public void PlayAbsorb()
        {
            if (_readyRing != null)
                _readyRing.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            ApplyAbsorbConfig();
            if (_absorbBurst != null)
            {
                _absorbBurst.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                _absorbBurst.Play();
            }
        }

        public void Hide()
        {
            if (_readyRing   != null) _readyRing.Stop(true,   ParticleSystemStopBehavior.StopEmittingAndClear);
            if (_absorbBurst != null) _absorbBurst.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        }

        private void ApplyReadyConfig()
        {
            if (_readyRing == null) return;

            var main = _readyRing.main;
            main.loop         = true;
            main.startColor   = _readyColor;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.4f, 0.65f);
            main.startSpeed   = new ParticleSystem.MinMaxCurve(0f, 0.12f);
            main.startSize    = new ParticleSystem.MinMaxCurve(0.05f, 0.10f);

            var emission = _readyRing.emission;
            emission.rateOverTime = _readyEmission;

            var shape = _readyRing.shape;
            shape.enabled         = true;
            shape.shapeType       = ParticleSystemShapeType.Circle;
            shape.radius          = _readyRadius;
            shape.radiusThickness = 0.08f;
        }

        private void ApplyAbsorbConfig()
        {
            if (_absorbBurst == null) return;

            var main = _absorbBurst.main;
            main.loop          = false;
            main.startLifetime = _absorbLifetime;
            main.startSpeed    = _absorbSpeed;
            main.startSize     = new ParticleSystem.MinMaxCurve(0.08f, 0.14f);

            // 흰색 → 청록 투명으로 Gradient 페이드
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
            shape.radius          = _readyRadius;
            shape.radiusThickness = 0.08f;
        }
    }
}
