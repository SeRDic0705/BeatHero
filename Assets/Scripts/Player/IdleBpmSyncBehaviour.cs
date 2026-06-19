using BeatHero.Core;
using UnityEngine;

namespace BeatHero.Player
{
    // Idle State에만 부착. BPM에 비례한 재생 속도 + 8분음표 단위 위상 동기화.
    // OnStateEnter에서 animator.Play()를 직접 부르면 같은 프레임 트리거를 소비해
    // Idle→다른 상태 전이가 막히므로, OnStateUpdate(다음 프레임~)로 미루고
    // IsInTransition 중에는 스킵 — 위상동기를 유지하면서 전이도 정상 동작.
    public class IdleBpmSyncBehaviour : StateMachineBehaviour
    {
        private const float BASE_BPM = 60f;

        [SerializeField] private float _clipEighthNotes = 8f;

        private Conductor _conductor;
        private bool      _needsPhaseSync;

        public override void OnStateEnter(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
        {
            if (_conductor == null)
                _conductor = Object.FindAnyObjectByType<Conductor>();
            if (_conductor == null) return;
            animator.speed = _conductor.Bpm / BASE_BPM;
            _needsPhaseSync = true; // 위상 동기는 OnStateUpdate로 미룸
        }

        public override void OnStateUpdate(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
        {
            if (_conductor == null) return;
            animator.speed = _conductor.Bpm / BASE_BPM;

            // 전이 중엔 Play()를 건너뜀 — 진행 중인 전이(attack/hurt 등)를 취소하지 않기 위해.
            if (_needsPhaseSync && !animator.IsInTransition(layerIndex))
            {
                _needsPhaseSync = false;
                float songPosInEighths = (float)(_conductor.SongPositionInBeats * 2.0);
                float normalizedTime   = (songPosInEighths % _clipEighthNotes) / _clipEighthNotes;
                animator.Play(stateInfo.fullPathHash, layerIndex, normalizedTime);
            }
        }

        public override void OnStateExit(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
        {
            _needsPhaseSync = false;
            animator.speed  = 1f;
        }
    }
}
