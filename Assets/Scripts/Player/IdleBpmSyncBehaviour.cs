using BeatHero.Core;
using UnityEngine;

namespace BeatHero.Player
{
    // Idle State에만 부착. BPM에 비례한 재생 속도 + 8분음표 단위 위상 동기화.
    public class IdleBpmSyncBehaviour : StateMachineBehaviour
    {
        // 클립 제작 기준 BPM. 이 BPM에서 speed=1이 되도록 조정됨.
        private const float BASE_BPM = 60f;

        // 클립의 총 길이를 8분음표 수로 지정 (Inspector에서 설정).
        // 키프레임 4개(각 1 eighth note) → 4, 키프레임 8개 → 8
        [SerializeField] private float _clipEighthNotes = 8f;

        private Conductor _conductor;

        public override void OnStateEnter(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
        {
            if (_conductor == null)
                _conductor = Object.FindAnyObjectByType<Conductor>();
            if (_conductor == null) return;

            float bpm = _conductor.Bpm;
            animator.speed = bpm / BASE_BPM;

            float songPosInEighths = (float)(_conductor.SongPositionInBeats * 2.0);
            float normalizedTime   = (songPosInEighths % _clipEighthNotes) / _clipEighthNotes;
            animator.Play(stateInfo.fullPathHash, layerIndex, normalizedTime);
        }

        public override void OnStateUpdate(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
        {
            if (_conductor == null) return;
            animator.speed = _conductor.Bpm / BASE_BPM;
        }

        public override void OnStateExit(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
        {
            animator.speed = 1f;
        }
    }
}
