using UnityEngine;

namespace BeatHero.Player
{
    // 플레이어 Animator 파라미터 래퍼 — 애니메이션 트리거/상태 전환 단일 창구
    public class PlayerAnimationController : MonoBehaviour
    {
        private static readonly int IsRunning   = Animator.StringToHash("isRunning");
        private static readonly int IsCharging  = Animator.StringToHash("isCharging");
        private static readonly int AttackHash  = Animator.StringToHash("attack");
        private static readonly int FinalAtkHash= Animator.StringToHash("finalAttack");
        private static readonly int HurtHash    = Animator.StringToHash("hurt");
        private static readonly int IsDeadHash  = Animator.StringToHash("isDead");

        [SerializeField] private Animator          _animator;
        [SerializeField] private ChargeAuraEffect  _chargeAura;

        private void Awake()
        {
            if (_animator == null) _animator = GetComponent<Animator>();
        }

        public void SetRunning(bool value) => _animator.SetBool(IsRunning, value);

        // 차지 단계 설정 (0=꺼짐, 1~4=단계별 강도)
        public void SetChargeStage(int stage)
        {
            _animator.SetBool(IsCharging, stage > 0);
            _chargeAura?.SetStage(stage);
        }

        // 하위 호환 — BattleStateMachine 외부에서 bool로 호출하는 경로 유지
        public void SetCharging(bool value) => SetChargeStage(value ? 1 : 0);

        public void TriggerAttack()      => _animator.SetTrigger(AttackHash);
        public void TriggerFinalAttack() => _animator.SetTrigger(FinalAtkHash);
        public void TriggerHurt()        => _animator.SetTrigger(HurtHash);

        public void TriggerDeath()
        {
            // 사망 시 미소비 트리거를 모두 클리어해서 Death → Hurt 오전환 방지
            _animator.ResetTrigger(HurtHash);
            _animator.ResetTrigger(AttackHash);
            _animator.ResetTrigger(FinalAtkHash);
            _animator.SetBool(IsDeadHash, true);
        }
    }
}
