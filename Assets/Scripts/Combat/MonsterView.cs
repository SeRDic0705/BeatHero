using BeatHero.Data;
using UnityEngine;

namespace BeatHero.Combat
{
    // 씬에 배치된 몬스터 GameObject의 비주얼 담당.
    // BattleStateMachine.OnBattleStarted 구독 → 층 전환마다 스프라이트/애니메이터 자동 교체.
    [RequireComponent(typeof(SpriteRenderer))]
    public class MonsterView : MonoBehaviour
    {
        private SpriteRenderer      _renderer;
        private Animator            _animator;
        private BattleStateMachine  _battle;

        private void Awake()
        {
            _renderer = GetComponent<SpriteRenderer>();
            _animator = GetComponent<Animator>();

            // Awake에서 구독: GameManager.Start()가 StartBattle을 호출하기 전에 등록 보장
            _battle = Object.FindAnyObjectByType<BattleStateMachine>();
            if (_battle != null)
                _battle.OnBattleStarted += SetMonster;
        }

        private void Start()
        {
            // Awake 시점에 이미 StartBattle이 호출된 경우 대비 (스크립트 실행 순서가 다를 때)
            if (_battle != null && _battle.CurrentMonster != null)
                SetMonster(_battle.CurrentMonster);
        }

        private void OnDestroy()
        {
            if (_battle != null)
                _battle.OnBattleStarted -= SetMonster;
        }

        private void SetMonster(MonsterData data)
        {
            _renderer.sprite = data.sprite;

            if (_animator != null)
                _animator.runtimeAnimatorController = data.animator;
        }
    }
}
