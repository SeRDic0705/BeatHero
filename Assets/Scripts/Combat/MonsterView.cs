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
        }

        private void Start()
        {
            _battle = Object.FindAnyObjectByType<BattleStateMachine>();
            if (_battle != null)
                _battle.OnBattleStarted += SetMonster;
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
