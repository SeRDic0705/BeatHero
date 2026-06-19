using BeatHero.Core;
using UnityEngine;

namespace BeatHero.Player
{
    // Idle State에만 부착. BPM에 비례한 재생 속도 동기화.
    // ⚠️ OnStateEnter에서 animator.Play()를 호출하면 같은 프레임의 트리거(attack/hurt 등)를
    // 소비하거나 전이를 리셋해 Idle→다른 상태 전이가 막히므로 속도 동기만 처리한다.
    public class IdleBpmSyncBehaviour : StateMachineBehaviour
    {
        // 클립 제작 기준 BPM. 이 BPM에서 speed=1이 되도록 조정됨.
        private const float BASE_BPM = 60f;

        private Conductor _conductor;

        public override void OnStateEnter(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
        {
            if (_conductor == null)
                _conductor = Object.FindAnyObjectByType<Conductor>();
            if (_conductor == null) return;
            animator.speed = _conductor.Bpm / BASE_BPM;
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
