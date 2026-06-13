using System;
using UnityEngine;

namespace BeatHero.Player
{
    [Serializable]
    public struct AuraLayerConfig
    {
        public Color color;
        public float startSize;
        public float emissionRate;
    }

    // 차지 단계별 3-레이어 후광 파티클 제어
    // GatherParticles(1+) → AuraSparks(2+) → FlameAura(3+)
    public class ChargeAuraEffect : MonoBehaviour
    {
        [Header("Particle Systems")]
        [SerializeField] private ParticleSystem _gatherParticles;
        [SerializeField] private ParticleSystem _auraSparks;
        [SerializeField] private ParticleSystem _flameAura;

        // index 0 = stage 1
        [Header("Gather (stage 1+)")]
        [SerializeField] private AuraLayerConfig[] _gatherConfigs = new AuraLayerConfig[4]
        {
            new() { color = new Color(1f, 1f,    0.7f, 0.70f), startSize = 0.06f, emissionRate = 10f },
            new() { color = new Color(1f, 0.95f, 0.4f, 0.85f), startSize = 0.07f, emissionRate = 18f },
            new() { color = new Color(1f, 0.90f, 0.2f, 1.00f), startSize = 0.09f, emissionRate = 28f },
            new() { color = new Color(1f, 1f,    0.5f, 1.00f), startSize = 0.11f, emissionRate = 40f },
        };

        // index 0 = stage 2
        [Header("Sparks (stage 2+)")]
        [SerializeField] private AuraLayerConfig[] _sparkConfigs = new AuraLayerConfig[4]
        {
            new() { color = new Color(1f, 0.85f, 0.10f, 0.70f), startSize = 0.08f, emissionRate = 20f },
            new() { color = new Color(1f, 0.70f, 0.05f, 0.85f), startSize = 0.10f, emissionRate = 35f },
            new() { color = new Color(1f, 0.50f, 0.00f, 1.00f), startSize = 0.13f, emissionRate = 55f },
            new() { color = new Color(1f, 0.50f, 0.00f, 1.00f), startSize = 0.13f, emissionRate = 55f },
        };

        // index 0 = stage 3
        [Header("Flame (stage 3+)")]
        [SerializeField] private AuraLayerConfig[] _flameConfigs = new AuraLayerConfig[4]
        {
            new() { color = new Color(1f, 0.45f, 0.00f, 0.80f), startSize = 0.15f, emissionRate = 25f },
            new() { color = new Color(1f, 0.20f, 0.00f, 1.00f), startSize = 0.20f, emissionRate = 50f },
            new() { color = new Color(1f, 0.20f, 0.00f, 1.00f), startSize = 0.20f, emissionRate = 50f },
            new() { color = new Color(1f, 0.20f, 0.00f, 1.00f), startSize = 0.20f, emissionRate = 50f },
        };

        public void SetStage(int stage)
        {
            ApplyLayer(_gatherParticles, stage >= 1, _gatherConfigs, stage - 1);
            ApplyLayer(_auraSparks,      stage >= 2, _sparkConfigs,  stage - 2);
            ApplyLayer(_flameAura,       stage >= 3, _flameConfigs,  stage - 3);
        }

        private static void ApplyLayer(ParticleSystem ps, bool active, AuraLayerConfig[] configs, int idx)
        {
            if (ps == null) return;

            if (!active)
            {
                ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                return;
            }

            idx = Mathf.Clamp(idx, 0, configs.Length - 1);
            var cfg = configs[idx];

            var main     = ps.main;
            main.startColor = cfg.color;
            main.startSize  = new ParticleSystem.MinMaxCurve(cfg.startSize);

            var emission = ps.emission;
            emission.rateOverTime = cfg.emissionRate;

            if (!ps.isPlaying) ps.Play();
        }
    }
}
