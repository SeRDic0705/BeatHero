using System;
using BeatHero.Core;
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
    // GatherParticles(1+) + ImpactAura BPM-synced(1+, 마디 경계마다 재트리거)
    public class ChargeAuraEffect : MonoBehaviour
    {
        [Header("Particle Systems")]
        [SerializeField] private ParticleSystem _gatherParticles;
        [SerializeField] private ParticleSystem _impactAura;

        [Header("BPM Sync")]
        [SerializeField] private Conductor _conductor;

        [Header("Gather (stage 1+)")]
        [SerializeField] private AuraLayerConfig[] _gatherConfigs = new AuraLayerConfig[4]
        {
            new() { color = new Color(1f, 1f,    0.7f, 0.70f), startSize = 0.06f, emissionRate = 10f },
            new() { color = new Color(1f, 0.95f, 0.4f, 0.85f), startSize = 0.07f, emissionRate = 18f },
            new() { color = new Color(1f, 0.90f, 0.2f, 1.00f), startSize = 0.09f, emissionRate = 28f },
            new() { color = new Color(1f, 1f,    0.5f, 1.00f), startSize = 0.11f, emissionRate = 40f },
        };

        private static readonly float[] ImpactScales = { 0f, 0.25f, 0.45f, 0.70f, 1.0f };

        private bool _impactActive;

        private void OnDestroy()
        {
            UnsubscribeBeat();
        }

        public void SetStage(int stage)
        {
            ApplyGather(_gatherParticles, stage >= 1, _gatherConfigs, stage - 1);
            ApplyImpact(stage);
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

        private void ApplyImpact(int stage)
        {
            if (_impactAura == null) return;

            if (stage <= 0)
            {
                _impactActive = false;
                UnsubscribeBeat();
                _impactAura.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                return;
            }

            var scale = ImpactScales[Mathf.Clamp(stage, 0, ImpactScales.Length - 1)];
            _impactAura.transform.localScale = Vector3.one * scale;

            // BPM duration 설정 (Conductor 없으면 기본 1.5s 유지)
            var main = _impactAura.main;
            main.loop = false;
            if (_conductor != null)
                main.duration = (float)(_conductor.SecPerBeat * 4);

            if (!_impactActive)
            {
                _impactActive = true;
                SubscribeBeat();
                // 즉시 1회 재생 (다음 마디 경계까지 기다리지 않음)
                _impactAura.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                _impactAura.Play();
            }
        }

        private void OnBeat(int beat)
        {
            if (!_impactActive || _impactAura == null) return;
            // 4박(마디) 경계마다 재트리거 → 비트바 메트로놈과 위상 동기화
            if (beat % 4 == 0)
            {
                _impactAura.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                _impactAura.Play();
            }
        }

        private void SubscribeBeat()
        {
            if (_conductor != null)
                _conductor.OnBeat += OnBeat;
        }

        private void UnsubscribeBeat()
        {
            if (_conductor != null)
                _conductor.OnBeat -= OnBeat;
        }
    }
}
