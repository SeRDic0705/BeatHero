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
    // GatherParticles(1+) + ImpactAura 8분음표 싱크(1+, 마디당 8회 재트리거)
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
            new() { color = new Color(1f, 0.95f, 0.4f, 0.85f), startSize = 0.06f, emissionRate = 18f },
            new() { color = new Color(1f, 0.90f, 0.2f, 1.00f), startSize = 0.06f, emissionRate = 28f },
            new() { color = new Color(1f, 1f,    0.5f, 1.00f), startSize = 0.06f, emissionRate = 40f },
        };

        private static readonly float[] ImpactScales = { 0f, 0.25f, 0.45f, 0.70f, 1.0f };

        private bool _impactActive;
        private int  _lastEighthNote = -1;

        private void Update()
        {
            if (!_impactActive || _conductor == null || _impactAura == null) return;

            // 8분음표 경계 감지: SongPositionInBeats * 2 의 정수 변화
            int currentEighth = (int)(_conductor.SongPositionInBeats * 2);
            if (currentEighth != _lastEighthNote)
            {
                _lastEighthNote = currentEighth;
                _impactAura.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                _impactAura.Play();
            }
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
                _lastEighthNote = -1;
                _impactAura.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                return;
            }

            var scale = ImpactScales[Mathf.Clamp(stage, 0, ImpactScales.Length - 1)];
            _impactAura.transform.localScale = Vector3.one * scale;

            var main = _impactAura.main;
            main.loop = false;
            // 8분음표 1개 길이로 duration 설정
            if (_conductor != null)
                main.duration = (float)(_conductor.SecPerBeat * 0.5);

            if (!_impactActive)
            {
                _impactActive = true;
                _lastEighthNote = -1;
                // 즉시 1회 재생 — 다음 8분음표 경계는 Update에서 처리
                _impactAura.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                _impactAura.Play();
            }
        }
    }
}
