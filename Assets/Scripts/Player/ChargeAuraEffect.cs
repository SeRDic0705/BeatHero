using System;
using UnityEngine;

namespace BeatHero.Player
{
    [Serializable]
    public struct AuraStageConfig
    {
        public Color  color;
        public float  startSize;
        public float  emissionRate;
    }

    // 차지 단계별 후광 파티클 제어 — SetStage(0)=꺼짐, 1~4=단계별 강도
    public class ChargeAuraEffect : MonoBehaviour
    {
        [SerializeField] private ParticleSystem _particles;

        // 인스펙터에서 단계별 색상·크기·방출 속도를 조정한다 (index 0 = stage 1)
        [SerializeField] private AuraStageConfig[] _stages = new AuraStageConfig[4]
        {
            new() { color = new Color(1f, 0.9f, 0.3f, 0.6f), startSize = 0.4f, emissionRate = 15f },
            new() { color = new Color(1f, 0.6f, 0.1f, 0.7f), startSize = 0.7f, emissionRate = 30f },
            new() { color = new Color(1f, 0.3f, 0.0f, 0.8f), startSize = 1.0f, emissionRate = 50f },
            new() { color = new Color(1f, 0.95f, 0.6f, 1.0f), startSize = 1.4f, emissionRate = 80f },
        };

        private void Awake()
        {
            if (_particles == null) _particles = GetComponent<ParticleSystem>();
        }

        public void SetStage(int stage)
        {
            if (stage <= 0)
            {
                _particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                return;
            }

            int idx = Mathf.Clamp(stage - 1, 0, _stages.Length - 1);
            var cfg = _stages[idx];

            var main = _particles.main;
            main.startColor = cfg.color;
            main.startSize  = cfg.startSize;

            var emission = _particles.emission;
            emission.rateOverTime = cfg.emissionRate;

            if (!_particles.isPlaying)
                _particles.Play();
        }
    }
}
