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

    // 차지 단계별 후광 파티클 제어
    // GatherParticles(1+) + ImpactAura looping(1+, 단계별 스케일)
    public class ChargeAuraEffect : MonoBehaviour
    {
        [Header("Particle Systems")]
        [SerializeField] private ParticleSystem _gatherParticles;
        [SerializeField] private ParticleSystem _impactAura;

        [Header("Gather (stage 1+)")]
        [SerializeField] private AuraLayerConfig[] _gatherConfigs = new AuraLayerConfig[4]
        {
            new() { color = new Color(1f, 1f,    0.7f, 0.70f), startSize = 0.06f, emissionRate = 10f },
            new() { color = new Color(1f, 0.95f, 0.4f, 0.85f), startSize = 0.07f, emissionRate = 18f },
            new() { color = new Color(1f, 0.90f, 0.2f, 1.00f), startSize = 0.09f, emissionRate = 28f },
            new() { color = new Color(1f, 1f,    0.5f, 1.00f), startSize = 0.11f, emissionRate = 40f },
        };

        // 차지 단계별 ImpactAura localScale
        private static readonly float[] ImpactScales = { 0f, 0.25f, 0.45f, 0.70f, 1.0f };

        public void SetStage(int stage)
        {
            ApplyGather(_gatherParticles, stage >= 1, _gatherConfigs, stage - 1);
            ApplyImpact(_impactAura, stage);
        }

        private static void ApplyGather(ParticleSystem ps, bool active, AuraLayerConfig[] configs, int idx)
        {
            if (ps == null) return;
            if (!active) { ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear); return; }
            idx = Mathf.Clamp(idx, 0, configs.Length - 1);
            var cfg = configs[idx];
            var main = ps.main;
            main.startColor = cfg.color;
            main.startSize  = new ParticleSystem.MinMaxCurve(cfg.startSize);
            var emission = ps.emission;
            emission.rateOverTime = cfg.emissionRate;
            if (!ps.isPlaying) ps.Play();
        }

        private static void ApplyImpact(ParticleSystem ps, int stage)
        {
            if (ps == null) return;
            if (stage <= 0)
            {
                ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                return;
            }
            var scale = ImpactScales[Mathf.Clamp(stage, 0, ImpactScales.Length - 1)];
            ps.transform.localScale = Vector3.one * scale;
            var main = ps.main;
            main.loop = true;
            if (!ps.isPlaying) ps.Play();
        }
    }
}
